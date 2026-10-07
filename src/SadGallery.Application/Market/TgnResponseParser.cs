using System.Globalization;
using System.Text.Json;
using SadGallery.Application.Text;
using SadGallery.Domain.Market;

namespace SadGallery.Application.Market;

/// <summary>
/// تجزیه پاسخ سرویس نرخ (قرارداد واقعی مالک پروژه — docs/API.md §ب).
/// <para>اصول طراحی:</para>
/// <list type="bullet">
///   <item>هیچ استثنایی به بیرون پرتاب نمی‌شود؛ خروجی ساختارمند است.</item>
///   <item>ترتیب و تعداد کلیدها تضمین‌شده نیست (بند ۶ مستندات) ⇒ نگاشت «نام‌محور» و تحمل کلید ناشناخته.</item>
///   <item>عدد متنی (مثل <c>"7,600,000"</c>) و ارقام فارسی/عربی با جداکننده هزارگان پذیرفته می‌شود،
///         اما جداکننده اعشاری اروپایی (<c>7.600.000</c>) عمداً پذیرفته **نمی‌شود** (ابهام‌آفرین است).</item>
///   <item>نبودِ زمان اعلام ⇒ شکست بنیادی؛ چون نرخ بدون زمان، قابل انتشار نیست (معیار پذیرش فاز ۲).</item>
/// </list>
/// </summary>
public static class TgnResponseParser
{
    private const string ErrorPropertyName = "Error";

    /// <summary>تجزیه متن پاسخ. ورودی <c>null</c>/خالی ⇒ شکست بنیادی EmptyResponse.</summary>
    public static RateParseResult Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return RateParseResult.Fatal("EmptyResponse", "پاسخ منبع خالی بود.");
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(body, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 8,
            });
        }
        catch (JsonException exception)
        {
            // متن کامل پاسخ لاگ نمی‌شود؛ فقط محل خطا برای عیب‌یابی.
            return RateParseResult.Fatal("InvalidJson", $"JSON نامعتبر در موقعیت {exception.BytePositionInLine}.");
        }

        using (document)
        {
            var root = document.RootElement;

            // برخی واسط‌ها پاسخ را «رشته‌ای حاوی JSON» برمی‌گردانند؛ یک لایه باز می‌کنیم.
            if (root.ValueKind == JsonValueKind.String)
            {
                var inner = root.GetString();

                if (string.IsNullOrWhiteSpace(inner))
                {
                    return RateParseResult.Fatal("EmptyResponse", "پاسخ رشته‌ای خالی بود.");
                }

                return Parse(inner);
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return RateParseResult.Fatal("UnexpectedRoot", $"ریشه پاسخ از نوع {root.ValueKind} بود.");
            }

            return ParseObject(root);
        }
    }

    private static RateParseResult ParseObject(JsonElement root)
    {
        // نگاشت بی‌توجه به بزرگی/کوچکی حروف تا تغییر جزئی قرارداد منبع، سامانه را از کار نیندازد.
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in root.EnumerateObject())
        {
            properties[property.Name] = property.Value;
        }

        if (properties.TryGetValue(ErrorPropertyName, out var errorElement) &&
            errorElement.ValueKind == JsonValueKind.String)
        {
            var error = errorElement.GetString();

            if (!string.IsNullOrWhiteSpace(error))
            {
                return new RateParseResult { ProviderError = error };
            }
        }

        var rates = new List<ParsedRate>();
        var problems = new List<RateParseProblem>();
        var unmapped = new List<string>();
        var missing = new List<string>();

        var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { AssetCatalog.TimeProviderKey };

        foreach (var definition in AssetCatalog.All)
        {
            knownKeys.Add(definition.ProviderKey);

            if (!properties.TryGetValue(definition.ProviderKey, out var element))
            {
                // بند ۶ مستندات سرویس: تعداد کلیدها تضمین‌شده نیست ⇒ غیبت کلید «خطا» نیست.
                missing.Add(definition.ProviderKey);
                continue;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Null or JsonValueKind.Undefined:
                    problems.Add(new RateParseProblem(definition.ProviderKey, RateProblemReason.NullValue));
                    break;

                case JsonValueKind.Number or JsonValueKind.String:
                    if (TryReadNumber(element, out var value))
                    {
                        rates.Add(new ParsedRate(definition.ProviderKey, definition.Code, value));
                    }
                    else
                    {
                        problems.Add(new RateParseProblem(
                            definition.ProviderKey,
                            RateProblemReason.NotANumber,
                            DescribeValue(element)));
                    }

                    break;

                default:
                    problems.Add(new RateParseProblem(
                        definition.ProviderKey,
                        RateProblemReason.UnexpectedType,
                        element.ValueKind.ToString()));
                    break;
            }
        }

        foreach (var property in properties.Keys)
        {
            if (!knownKeys.Contains(property))
            {
                unmapped.Add(property);
            }
        }

        unmapped.Sort(StringComparer.Ordinal);
        missing.Sort(StringComparer.Ordinal);

        if (!properties.TryGetValue(AssetCatalog.TimeProviderKey, out var timeElement))
        {
            return new RateParseResult
            {
                IsFatal = true,
                FatalCode = "MissingTime",
                FatalDetail = $"فیلد {AssetCatalog.TimeProviderKey} در پاسخ نبود؛ نرخ بدون زمان منتشر نمی‌شود.",
                Rates = rates,
                Problems = problems,
                UnmappedKeys = unmapped,
                MissingKeys = missing,
            };
        }

        var timeRaw = timeElement.ValueKind == JsonValueKind.String ? timeElement.GetString() : timeElement.ToString();

        if (!TehranTime.TryParse(timeRaw, out var quotedAt))
        {
            return new RateParseResult
            {
                IsFatal = true,
                FatalCode = "InvalidTime",
                FatalDetail = $"فیلد {AssetCatalog.TimeProviderKey} قابل تجزیه نبود (قالب موردانتظار: yyyy/MM/dd HH:mm:ss).",
                TimeRaw = timeRaw,
                Rates = rates,
                Problems = problems,
                UnmappedKeys = unmapped,
                MissingKeys = missing,
            };
        }

        return new RateParseResult
        {
            QuotedAtUtc = quotedAt,
            TimeRaw = timeRaw,
            Rates = rates,
            Problems = problems,
            UnmappedKeys = unmapped,
            MissingKeys = missing,
        };
    }

    /// <summary>خواندن عدد از عنصر JSON (عددی یا متنی با جداکننده هزارگان/ارقام فارسی).</summary>
    public static bool TryReadNumber(JsonElement element, out decimal value)
    {
        value = 0m;

        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDecimal(out value);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return TryParseTextNumber(element.GetString(), out value);
        }

        return false;
    }

    /// <summary>
    /// تجزیه «عدد متنی». جداکننده‌های پذیرفته‌شده: <c>,</c> و <c>٬</c> (U+066C) و فاصله‌های رایج.
    /// ارقام فارسی/عربی تبدیل می‌شوند. هر نویسه دیگر ⇒ رد (تا عدد مبهم تفسیر نشود).
    /// </summary>
    public static bool TryParseTextNumber(string? raw, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = PersianText.ToAsciiDigits(raw)
            .Replace("\u066C", string.Empty, StringComparison.Ordinal) // ٬
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (normalized.Length == 0)
        {
            return false;
        }

        // فقط رقم، علامت و یک نقطه اعشار — هیچ حرف/نماد دیگری.
        var seenDot = false;

        for (var index = 0; index < normalized.Length; index++)
        {
            var character = normalized[index];

            if (character is >= '0' and <= '9')
            {
                continue;
            }

            if (character == '.' && !seenDot)
            {
                seenDot = true;
                continue;
            }

            if ((character == '-' || character == '+') && index == 0)
            {
                continue;
            }

            return false;
        }

        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    private static string DescribeValue(JsonElement element)
    {
        var raw = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();

        if (string.IsNullOrEmpty(raw))
        {
            return "مقدار خالی";
        }

        // مقدار به‌طور کامل در لاگ نمی‌رود؛ فقط ۳۲ نویسه اول برای عیب‌یابی.
        return raw.Length <= 32 ? $"مقدار نامعتبر: {raw}" : $"مقدار نامعتبر: {raw[..32]}…";
    }
}
