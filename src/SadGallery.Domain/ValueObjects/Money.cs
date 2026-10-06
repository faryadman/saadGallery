using SadGallery.Domain.Enums;

namespace SadGallery.Domain.ValueObjects;

/// <summary>
/// مبلغ پول همراه با واحد آن.
/// چرا نوع اختصاصی و نه <c>decimal</c> خام؟ چون خطای «ریال/تومان» شایع‌ترین خطای ده‌برابری
/// در سامانه‌های نرخ است؛ این نوع اجازه نمی‌دهد دو واحد متفاوت بی‌تبدیل جمع شوند (ADR-0009).
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }

    public CurrencyUnit Unit { get; }

    public Money(decimal amount, CurrencyUnit unit)
    {
        if (!Enum.IsDefined(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Currency unit is not supported.");
        }

        Amount = amount;
        Unit = unit;
    }

    public static Money Zero(CurrencyUnit unit) => new(0m, unit);

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    /// <summary>جمع دو مبلغ با واحد یکسان. واحدهای متفاوت ⇒ استثنا (تبدیل باید صریح باشد).</summary>
    public Money Add(Money other)
    {
        EnsureSameUnit(other);
        return new Money(Amount + other.Amount, Unit);
    }

    /// <summary>تفریق دو مبلغ با واحد یکسان. نتیجه می‌تواند منفی باشد (مثلاً حباب منفی).</summary>
    public Money Subtract(Money other)
    {
        EnsureSameUnit(other);
        return new Money(Amount - other.Amount, Unit);
    }

    /// <summary>
    /// تبدیل صریح واحد.
    /// </summary>
    /// <param name="target">واحد مقصد.</param>
    /// <param name="targetUnitsPerSourceUnit">
    /// ضریب تبدیل: چند واحد مقصد برای یک واحد مبدأ.
    /// مثال‌ها: تومان = ریال × 0.1 ⇒ <c>ConvertTo(Irt, 0.1m)</c> ؛ ریال = تومان × 10 ⇒ <c>ConvertTo(Irr, 10m)</c>.
    /// </param>
    /// <remarks>فراخوان باید رخداد تبدیل را ثبت کند (Audit/Log)؛ خود این متد ثبت نمی‌کند.</remarks>
    public Money ConvertTo(CurrencyUnit target, decimal targetUnitsPerSourceUnit)
    {
        if (targetUnitsPerSourceUnit <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetUnitsPerSourceUnit), targetUnitsPerSourceUnit, "Conversion rate must be greater than zero.");
        }

        if (target == Unit)
        {
            return this;
        }

        return new Money(Amount * targetUnitsPerSourceUnit, target);
    }

    public int CompareTo(Money other)
    {
        EnsureSameUnit(other);
        return Amount.CompareTo(other.Amount);
    }

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    private void EnsureSameUnit(Money other)
    {
        if (other.Unit != Unit)
        {
            throw new InvalidOperationException(
                $"Money units must match. Left: {Unit}, Right: {other.Unit}. Use an explicit conversion.");
        }
    }

    public override string ToString() => $"{Amount} {Unit}";
}
