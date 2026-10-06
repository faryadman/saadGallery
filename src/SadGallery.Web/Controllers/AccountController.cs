using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SadGallery.Application.Abstractions;
using SadGallery.Infrastructure.Identity;
using SadGallery.Web.Identity;
using SadGallery.Web.Models.Account;
using SadGallery.Web.Security;
using SadGallery.Application.Text;

namespace SadGallery.Web.Controllers;

/// <summary>
/// ورود/ثبت‌نام با رمز عبور (روش دوم از ADR-0004).
/// ورود با رمز یک‌بارمصرف (OTP) در فاز ۶ با همان حساب کاربری افزوده می‌شود.
/// </summary>
public sealed class AccountController : Controller
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IClock _clock;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IClock clock,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _clock = clock;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var identifier = PersianText.ToAsciiDigits(model.Identifier).Trim();

        var user = await _userManager.FindByNameAsync(identifier).ConfigureAwait(false)
                   ?? await _userManager.FindByEmailAsync(identifier).ConfigureAwait(false);

        // پیام یکنواخت برای «کاربر ناموجود»، «رمز نادرست» و «حساب غیرفعال»
        // تا وجود/عدم وجود حساب افشا نشود (جلوگیری از User Enumeration).
        const string genericError = "شناسه یا رمز عبور نادرست است.";

        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, genericError);
            return View(model);
        }

        var result = await _signInManager
            .PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            user.LastLoginAtUtc = _clock.UtcNow;
            await _userManager.UpdateAsync(user).ConfigureAwait(false);

            _logger.LogInformation("User {UserId} signed in.", user.Id);

            return RedirectToLocal(returnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Account locked out after failed attempts. UserId={UserId}", user.Id);
            ModelState.AddModelError(string.Empty, "به دلیل تلاش‌های ناموفق، حساب موقتاً قفل شده است. چند دقیقه بعد تلاش کنید.");
            return View(model);
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "ورود به این حساب در حال حاضر مجاز نیست. با پشتیبانی تماس بگیرید.");
            return View(model);
        }

        _logger.LogWarning("Failed sign-in attempt. UserId={UserId}", user.Id);
        ModelState.AddModelError(string.Empty, genericError);
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // ورودی کاربر ممکن است با ارقام فارسی/عربی ارسال شود؛ پیش از ذخیره به لاتین تبدیل می‌شود.
        var identifier = PersianText.ToAsciiDigits(model.Identifier).Trim();
        var isEmail = EmailValidator.IsValid(identifier);

        var user = new ApplicationUser
        {
            UserName = identifier,
            Email = isEmail ? identifier : null,
            PhoneNumber = isEmail ? null : identifier,
            DisplayName = string.IsNullOrWhiteSpace(model.DisplayName) ? null : model.DisplayName.Trim(),
            CreatedAtUtc = _clock.UtcNow,
            IsActive = true,
        };

        var createResult = await _userManager.CreateAsync(user, model.Password).ConfigureAwait(false);

        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors.ToList();

            // خطاهای Identity به پیام فارسی کاربرپسند ترجمه می‌شوند (جزئیات فنی لاگ نمی‌شود).
            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, IdentityErrorTranslator.Translate(error));
            }

            _logger.LogWarning("Registration failed. ErrorCount={ErrorCount}", errors.Count);
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, RoleNames.Customer).ConfigureAwait(false);

        if (!roleResult.Succeeded)
        {
            // کاربر ساخته شده اما نقش نگرفته است؛ این وضعیت باید دیده و رسیدگی شود.
            _logger.LogError("Failed to assign Customer role to user {UserId}.", user.Id);
            await _userManager.DeleteAsync(user).ConfigureAwait(false);
            ModelState.AddModelError(string.Empty, "ثبت‌نام انجام نشد. لطفاً بعداً تلاش کنید.");
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false).ConfigureAwait(false);
        _logger.LogInformation("New customer registered. UserId={UserId}", user.Id);

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync().ConfigureAwait(false);
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    /// <summary>
    /// بازگشت امن به URL داخلی. مقصدهای بیرونی نادیده گرفته می‌شوند (جلوگیری از Open Redirect).
    /// </summary>
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }
}
