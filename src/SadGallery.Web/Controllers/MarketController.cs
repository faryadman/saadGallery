using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadGallery.Application.Market;
using SadGallery.Application.Text;
using SadGallery.Domain.Enums;
using SadGallery.Domain.Market;
using SadGallery.Web.Models.Market;
using SadGallery.Web.Security;

namespace SadGallery.Web.Controllers;

/// <summary>
/// صفحه‌های عمومی بازار (مهمان): نمای کامل، جزئیات نرخ، حباب‌سنج و ماشین‌حساب طلا.
/// صفحه‌های «تاریخچه گسترده» و «حباب‌سنج پیشرفته» با Policy سرور محافظت می‌شوند
/// (پنهان‌کردن لینک در UI کنترل امنیتی نیست — SECURITY.md §5).
/// </summary>
public sealed class MarketController : Controller
{
    private const string ReferenceAssetCode = "GOLD_GRAM_18";

    private readonly RateDisplayService _rates;
    private readonly IBubbleCalculator _bubble;
    private readonly IGoldCalculator _gold;
    private readonly RateHistoryService _history;

    /// <summary>ساخت کنترلر.</summary>
    public MarketController(
        RateDisplayService rates,
        IBubbleCalculator bubble,
        IGoldCalculator gold,
        RateHistoryService history)
    {
        ArgumentNullException.ThrowIfNull(rates);
        ArgumentNullException.ThrowIfNull(bubble);
        ArgumentNullException.ThrowIfNull(gold);
        ArgumentNullException.ThrowIfNull(history);

        _rates = rates;
        _bubble = bubble;
        _gold = gold;
        _history = history;
    }

    /// <summary>نمای کامل بازار (همه گروه‌ها، مهمان).</summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _rates.GetAsync(cancellationToken));

    /// <summary>جزئیات یک نرخ + پیوند تاریخچه اعضا. مسیر عمومی: /market/rate/{code}</summary>
    [HttpGet("/market/rate/{code}")]
    public async Task<IActionResult> Details(string code, CancellationToken cancellationToken)
    {
        var model = await _rates.GetAsync(cancellationToken);
        var item = model.Items.FirstOrDefault(rate => string.Equals(rate.AssetCode, code, StringComparison.Ordinal));

        if (item is null)
        {
            return NotFound();
        }

        return View(new RateDetailsModel(item, model.IsSampleData, model.LastFetchedUtc));
    }

    /// <summary>حباب‌سنج (مهمان) — فرم پیش‌پر از آخرین نرخ‌ها و استاندارد مسکوکات.</summary>
    [HttpGet]
    public async Task<IActionResult> Bubble(CancellationToken cancellationToken)
    {
        var model = await _rates.GetAsync(cancellationToken);
        var emami = CoinStandards.TryGet(CoinStandards.EmamiCode);
        var emamiRate = FindItem(model, CoinStandards.EmamiCode);
        var reference = FindItem(model, ReferenceAssetCode);

        return View(new BubblePageModel
        {
            Coins = BuildCoinOptions(model),
            Form = new BubbleForm
            {
                Coin = emami?.AssetCode,
                MarketPrice = emamiRate is null ? null : RateFormat.Amount(emamiRate.RawAmount),
                Weight = emami?.WeightGrams.ToString(CultureInfo.InvariantCulture),
                Purity = emami?.Purity.ToString(CultureInfo.InvariantCulture),
                ReferenceRate = reference is null ? null : RateFormat.Amount(reference.RawAmount),
                MintingCost = "0",
            },
            Result = null,
            RateNotice = model.Notice,
            ReferenceIsStale = IsStale(reference),
            IsSampleData = model.IsSampleData,
        });
    }

    /// <summary>محاسبه حباب از ورودی صریح کاربر.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Bubble(BubbleForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);

        var model = await _rates.GetAsync(cancellationToken);
        var reference = FindItem(model, ReferenceAssetCode);

        decimal marketPrice = 0m, weight = 0m, purity = 0m, referenceRate = 0m, mintingCost = 0m;
        var ok = true;

        ok &= TryRead(form.MarketPrice, "قیمت بازار", value => marketPrice = value);
        ok &= TryRead(form.Weight, "وزن", value => weight = value);
        ok &= TryRead(form.Purity, "خلوص", value => purity = value);
        ok &= TryRead(form.ReferenceRate, "نرخ مرجع", value => referenceRate = value);

        // هزینه ضرب اختیاری است: خالی ⇒ صفر.
        if (!string.IsNullOrWhiteSpace(form.MintingCost))
        {
            ok &= TryRead(form.MintingCost, "هزینه ضرب", value => mintingCost = value);
        }

        BubbleResult? result = null;

        if (ok)
        {
            result = _bubble.Calculate(new BubbleInputs
            {
                MarketPriceIrt = marketPrice,
                WeightGrams = weight,
                Purity = purity,
                ReferenceGramRateIrt = referenceRate,
                MintingCostIrt = mintingCost,
                ReferenceIsStale = IsStale(reference),
            });
        }

        return View(new BubblePageModel
        {
            Coins = BuildCoinOptions(model),
            Form = form,
            Result = result,
            RateNotice = model.Notice,
            ReferenceIsStale = IsStale(reference),
            IsSampleData = model.IsSampleData,
        });

        bool TryRead(string? raw, string label, Action<decimal> assign)
        {
            if (PersianNumber.TryParse(raw, out var value, out var error))
            {
                assign(value);
                return true;
            }

            ModelState.AddModelError(string.Empty, $"{label}: {error}");
            return false;
        }
    }

    /// <summary>ماشین‌حساب طلا (مهمان) — تبدیل وزن/عیار و ارزش‌گذاری.</summary>
    [HttpGet]
    public async Task<IActionResult> Gold(CancellationToken cancellationToken)
    {
        var model = await _rates.GetAsync(cancellationToken);
        var reference = FindItem(model, ReferenceAssetCode);

        return View(new GoldPageModel
        {
            Form = new GoldForm
            {
                Unit = nameof(WeightUnit.Gram),
                Karat = "18",
                ReferenceRate = reference is null ? null : RateFormat.Amount(reference.RawAmount),
            },
            SuggestedRate = reference?.RawAmount,
            RateNotice = model.Notice,
            IsSampleData = model.IsSampleData,
        });
    }

    /// <summary>محاسبه ارزش طلا از ورودی صریح.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Gold(GoldForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);

        var model = await _rates.GetAsync(cancellationToken);
        var reference = FindItem(model, ReferenceAssetCode);

        var unit = Enum.TryParse<WeightUnit>(form.Unit, ignoreCase: true, out var parsedUnit) ? parsedUnit : WeightUnit.Gram;
        decimal amount = 0m, karat = 0m, rate = 0m;
        var ok = true;

        ok &= Read(form.Amount, "وزن", value => amount = value);
        ok &= Read(form.Karat, "عیار", value => karat = value);
        ok &= Read(form.ReferenceRate, "نرخ گرم ۱۸ عیار", value => rate = value);

        GoldCalculationResult? result = null;

        if (ok)
        {
            result = _gold.Calculate(new GoldCalculationInputs
            {
                Weight = amount,
                Unit = unit,
                Karat = karat,
                Gram18KRateIrt = rate,
                RateIsStale = IsStale(reference),
            });
        }

        return View(new GoldPageModel
        {
            Form = form,
            Result = result,
            Unit = unit,
            SuggestedRate = reference?.RawAmount,
            RateNotice = model.Notice,
            IsSampleData = model.IsSampleData,
        });

        bool Read(string? raw, string label, Action<decimal> assign)
        {
            if (PersianNumber.TryParse(raw, out var value, out var error))
            {
                assign(value);
                return true;
            }

            ModelState.AddModelError(string.Empty, $"{label}: {error}");
            return false;
        }
    }

    /// <summary>تاریخچه گسترده نرخ‌ها (فقط اعضا — Policy سرور).</summary>
    [Authorize(Policy = Policies.MemberFeatures)]
    [HttpGet]
    public async Task<IActionResult> History(string? code, int days = 7, CancellationToken cancellationToken = default)
    {
        var assetCode = string.IsNullOrWhiteSpace(code) ? ReferenceAssetCode : code;
        var history = await _history.GetAsync(assetCode, days, cancellationToken);
        var unit = AssetCatalog.FindByCode(assetCode)?.QuoteUnit ?? CurrencyUnit.Irt;

        var chart = history.Points.Count > 0
            ? MarketChart.BuildSvg(history.Points, $"نمودار تاریخچه {history.Title}")
            : string.Empty;

        return View(new HistoryPageModel
        {
            History = history,
            Assets = AssetCatalog.All,
            Chart = chart,
            Unit = unit,
        });
    }

    /// <summary>حباب‌سنج پیشرفته: مقایسه حباب همه مسکوکات با ورودی‌های استاندارد (فقط اعضا).</summary>
    [Authorize(Policy = Policies.MemberFeatures)]
    [HttpGet("/market/advanced-bubble")]
    public async Task<IActionResult> AdvancedBubble(CancellationToken cancellationToken)
    {
        var model = await _rates.GetAsync(cancellationToken);
        var reference = FindItem(model, ReferenceAssetCode);
        var referenceIsStale = IsStale(reference);

        var rows = new List<BubbleComparisonRow>(CoinStandards.Known.Count);

        foreach (var standard in CoinStandards.Known)
        {
            var rate = FindItem(model, standard.AssetCode);

            BubbleResult? result = null;

            if (rate is not null && reference is not null)
            {
                result = _bubble.Calculate(new BubbleInputs
                {
                    MarketPriceIrt = rate.RawAmount,
                    WeightGrams = standard.WeightGrams,
                    Purity = standard.Purity,
                    ReferenceGramRateIrt = reference.RawAmount,
                    ReferenceIsStale = referenceIsStale,
                });
            }

            rows.Add(new BubbleComparisonRow(standard.Title, standard.WeightGrams, standard.Purity, rate?.RawAmount, result));
        }

        return View(new AdvancedBubblePageModel
        {
            Rows = rows,
            ReferenceRateIrt = reference?.RawAmount,
            RateNotice = model.Notice ?? (reference is null ? "نرخ مرجع (گرم ۱۸ عیار) در دسترس نیست؛ مقایسه انجام نمی‌شود." : null),
            ReferenceIsStale = referenceIsStale,
            IsSampleData = model.IsSampleData,
        });
    }

    /// <summary>API تاریخچه (JSON) — همان Policy صفحه؛ برای مصرف برنامه‌ای و PWA آینده.</summary>
    [Authorize(Policy = Policies.MemberFeatures)]
    [HttpGet("/api/rates/{assetCode}/history")]
    public async Task<IActionResult> HistoryApi(string assetCode, int days = 7, CancellationToken cancellationToken = default)
    {
        var history = await _history.GetAsync(assetCode, days, cancellationToken);

        return Json(new
        {
            assetCode = history.AssetCode,
            title = history.Title,
            unit = history.UnitText,
            days = history.Days,
            notice = history.Notice,
            points = history.Points.Select(point => new
            {
                quotedAtUtc = point.QuotedAtUtc,
                quotedAt = point.QuotedAtText,
                amount = point.Amount,
                quality = point.Quality.ToString(),
            }),
        });
    }

    private static RateDisplayItem? FindItem(RateDisplayModel model, string assetCode) =>
        model.Items.FirstOrDefault(item => string.Equals(item.AssetCode, assetCode, StringComparison.Ordinal));

    private static bool IsStale(RateDisplayItem? item) => item is null || item.Quality >= RateQuality.Stale;

    private static IReadOnlyList<BubbleCoinOption> BuildCoinOptions(RateDisplayModel model)
    {
        var options = new List<BubbleCoinOption>(CoinStandards.Known.Count);

        foreach (var standard in CoinStandards.Known)
        {
            var rate = FindItem(model, standard.AssetCode);
            options.Add(new BubbleCoinOption(standard.AssetCode, standard.Title, standard.WeightGrams, standard.Purity, rate?.RawAmount));
        }

        return options;
    }
}
