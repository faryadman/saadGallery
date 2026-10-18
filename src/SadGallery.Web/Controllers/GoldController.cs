using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Administration;
using SadGallery.Application.Trading;
using SadGallery.Domain.Trading;
using SadGallery.Infrastructure.Identity;
using SadGallery.Web.Models;
using SadGallery.Web.Security;
namespace SadGallery.Web.Controllers;

[Authorize(Policy = Policies.MemberFeatures), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GoldController(IGoldTradingStore store, IAdministrationStore users, IClock clock, ILogger<GoldController> logger) : Controller
{
    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool Staff => User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Operator);
    private IActionResult Unavailable(Exception ex)
    {
        logger.LogWarning("Gold panel unavailable ({ErrorType})", ex.GetType().Name);
        Response.StatusCode = 503; return View("Unavailable");
    }
    private void Failure(GoldResult result)
    { Response.StatusCode = result == GoldResult.Conflict ? 409 : result == GoldResult.Unavailable ? 503 : 400; ModelState.AddModelError("", GoldTradingLabels.Error(result)); }
    [HttpGet("/gold")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        try { return View(new GoldCustomerModel(await store.WalletAsync(ActorId, page, ct), new())); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpPost("/gold/quote"), EnableRateLimiting(RateLimitPolicies.GoldTrading)]
    public async Task<IActionResult> Quote(GoldBuyForm form, CancellationToken ct)
    {
        try
        {
            var result = ModelState.IsValid && GoldNumber.TryParse(form.Weight, out var grams)
                ? await store.QuoteAsync(ActorId, form.OperationId, grams, ct) : new GoldOutcome(GoldResult.Invalid);
            if (result.Result == GoldResult.Saved) return RedirectToAction(nameof(Order), new { id = result.OrderId });
            if (result.Result == GoldResult.Forbidden) return Forbid();
            Failure(result.Result); return View("Index", new GoldCustomerModel(await store.WalletAsync(ActorId, 1, ct), form));
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpGet("/gold/orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid id, CancellationToken ct)
    {
        try { var order = await store.OrderAsync(ActorId, id, Staff, ct); return order == null ? NotFound() : View(new GoldOrderModel(order, Staff, new(), clock.UtcNow)); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    private async Task<IActionResult> OrderResult(Guid id, GoldResult result, GoldReviewForm? form, CancellationToken ct)
    {
        if (result == GoldResult.Forbidden) return Forbid();
        if (result == GoldResult.NotFound) return NotFound();
        if (result == GoldResult.Saved) { TempData["Notice"] = "عملیات ثبت شد؛ وضعیت و موجودی را می‌توانید بررسی کنید."; return RedirectToAction(nameof(Order), new { id }); }
        Failure(result);
        var order = await store.OrderAsync(ActorId, id, Staff, ct); if (order == null) return NotFound();
        // Render the latest concurrency token while preserving decision/note, including missing decision.
        ModelState.Remove("Version");
        return View("Order", new GoldOrderModel(order, Staff, form ?? new(), clock.UtcNow));
    }
    [HttpPost("/gold/orders/{id:guid}/submit"), EnableRateLimiting(RateLimitPolicies.GoldTrading)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        try { return await OrderResult(id, await store.SubmitAsync(ActorId, id, ct), null, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpPost("/gold/orders/{id:guid}/cancel"), EnableRateLimiting(RateLimitPolicies.GoldTrading)]
    public async Task<IActionResult> Cancel(Guid id, GoldVersionForm form, CancellationToken ct)
    {
        try { return await OrderResult(id, ModelState.IsValid && form.Bytes() is {} version ? await store.DecideAsync(ActorId, id, GoldOrderStatus.Cancelled, version, null, ct) : GoldResult.Invalid, null, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpGet("/operator/gold"), Authorize(Policy = Policies.OperatorArea)]
    public async Task<IActionResult> Queue(GoldOrderStatus? status = null, int? customerId = null, int page = 1, CancellationToken ct = default)
    {
        if (!Request.Query.ContainsKey("status")) status = GoldOrderStatus.Pending;
        if (!ModelState.IsValid || (status.HasValue && (!Enum.IsDefined(status.Value) || status == GoldOrderStatus.Quote)) || customerId <= 0) return BadRequest("فیلتر خرید معتبر نیست.");
        ViewData["Status"] = status; ViewData["CustomerId"] = customerId;
        try { return View(await store.OrdersAsync(ActorId, status, customerId, page, ct)); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpPost("/operator/gold/{id:guid}/review"), Authorize(Policy = Policies.OperatorArea), EnableRateLimiting(RateLimitPolicies.GoldTrading)]
    public async Task<IActionResult> Review(Guid id, GoldReviewForm form, CancellationToken ct)
    {
        try
        {
            var result = ModelState.IsValid && form.Decision is (GoldOrderStatus.Approved or GoldOrderStatus.Rejected) && form.Bytes() is {} version
                ? await store.DecideAsync(ActorId, id, form.Decision.Value, version, form.Note, ct) : GoldResult.Invalid;
            return await OrderResult(id, result, form, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpGet("/admin/gold/settings"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        try { var s = await store.SettingsAsync(ct); return View(new GoldSettingsForm { Enabled = s.Enabled, Fee = s.FeePercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", Version = Convert.ToBase64String(s.Version) }); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpPost("/admin/gold/settings"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Settings(GoldSettingsForm form, CancellationToken ct)
    {
        try
        {
            var result = ModelState.IsValid && GoldNumber.TryParse(form.Fee, out var fee) && form.Bytes() is {} version ? await store.SettingsAsync(ActorId, form.Enabled, fee, version, ct) : GoldResult.Invalid;
            if (result == GoldResult.Forbidden) return Forbid();
            if (result == GoldResult.Saved) { TempData["Notice"] = "تنظیمات خرید و کارمزد ذخیره شد."; return RedirectToAction(nameof(Settings)); }
            Failure(result); var current = await store.SettingsAsync(ct); form.Version = Convert.ToBase64String(current.Version); ModelState.Remove("Version"); return View(form);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    private async Task<IActionResult> WalletView(int id, GoldCreditForm? form, int page, CancellationToken ct)
    {
        var user = await users.UserAsync(id, ct); if (user == null) return NotFound();
        return View("Wallet", new GoldAdminWalletModel(id, user.DisplayName, await store.WalletAsync(id, page, ct), form ?? new()));
    }
    [HttpGet("/admin/gold/wallets/{id:int}"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Wallet(int id, int page = 1, CancellationToken ct = default)
    {
        try { return await WalletView(id, null, page, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpPost("/admin/gold/wallets/{id:int}/enable"), Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> EnableWallet(int id, GoldEnableForm form, CancellationToken ct)
    {
        try
        {
            var result = ModelState.IsValid ? await store.EnableWalletAsync(ActorId, id, form.Enabled, form.Bytes(), ct) : GoldResult.Invalid;
            if (result == GoldResult.Forbidden) return Forbid();
            if (result == GoldResult.Saved) { TempData["Notice"] = "وضعیت مشتری ویژه ذخیره شد."; return RedirectToAction(nameof(Wallet), new { id }); }
            Failure(result); return await WalletView(id, null, 1, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
    [HttpPost("/admin/gold/wallets/{id:int}/credit"), Authorize(Policy = Policies.AdminOnly), EnableRateLimiting(RateLimitPolicies.GoldTrading)]
    public async Task<IActionResult> Credit(int id, GoldCreditForm form, CancellationToken ct)
    {
        try
        {
            var result = ModelState.IsValid && form.Kind is {} kind && GoldNumber.TryParse(form.Amount, out var amount) ? await store.CreditAsync(ActorId, id, form.OperationId, kind, amount, form.Reason, ct) : GoldResult.Invalid;
            if (result == GoldResult.Forbidden) return Forbid();
            if (result == GoldResult.NotFound) return NotFound();
            if (result == GoldResult.Saved) { TempData["Notice"] = "شارژ کیف ثبت شد. ارسال دوباره همین عملیات، موجودی را دوباره افزایش نمی‌دهد."; return RedirectToAction(nameof(Wallet), new { id }); }
            Failure(result); return await WalletView(id, form, 1, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(ex); }
    }
}
