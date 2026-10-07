using SadGallery.Application.Text;
using Xunit;

namespace SadGallery.Tests.Unit.Text;

/// <summary>
/// تست‌های تبدیل زمان/تاریخ. تاریخ‌های مرجع با تقویم مستقل راستی‌آزمایی شده‌اند
/// (نوروز ۱۴۰۱ = ۲۰۲۲-۰۳-۲۱ و نوروز ۱۴۰۵ = ۲۰۲۶-۰۳-۲۱).
/// </summary>
public sealed class PersianDateTests
{
    [Fact]
    public void ToJalaliDateText_ConvertsOwnerSampleTime()
    {
        // ۲۰۲۲-۰۶-۰۹ ساعت ۱۱:۱۴ به وقت ایران = ۱۹ خرداد ۱۴۰۱
        var moment = new DateTimeOffset(2022, 6, 9, 7, 44, 48, TimeSpan.Zero);

        Assert.Equal("۱۴۰۱/۰۳/۱۹", PersianDate.ToJalaliDateText(moment));
        Assert.Equal("۱۱:۱۴", PersianDate.ToTimeText(moment));
        Assert.Equal("۱۴۰۱/۰۳/۱۹ — ۱۱:۱۴", PersianDate.ToJalaliDateTimeText(moment));
    }

    [Fact]
    public void ToJalaliDateText_HandlesNowruzBoundary()
    {
        // نوروز ۱۴۰۵ = ۲۰۲۶-۰۳-۲۱ (اول فروردین)
        var nowruz = new DateTimeOffset(2026, 3, 21, 9, 0, 0, TimeSpan.Zero);

        Assert.Equal("۱۴۰۵/۰۱/۰۱", PersianDate.ToJalaliDateText(nowruz));
    }

    [Fact]
    public void ToJalaliDateText_UsesTehranOffset_NotUtc()
    {
        // ۲۰:۳۰ UTC در ۲۰۲۶-۱۰-۰۸ ⇒ ۰۰:۰۰ روز بعد به وقت ایران
        var moment = new DateTimeOffset(2026, 10, 8, 20, 30, 0, TimeSpan.Zero);

        Assert.Equal("۱۴۰۵/۰۷/۱۷", PersianDate.ToJalaliDateText(moment));
        Assert.Equal("۰۰:۰۰", PersianDate.ToTimeText(moment));
    }

    [Theory]
    [InlineData("2022/06/09 11:14:48", 2022, 6, 9, 11, 14, 48)]
    [InlineData("2022-06-09 11:14:48", 2022, 6, 9, 11, 14, 48)]
    [InlineData("2022/06/09 11:14", 2022, 6, 9, 11, 14, 0)]
    public void TehranTime_ParsesDocumentedFormats(string raw, int year, int month, int day, int hour, int minute, int second)
    {
        Assert.True(TehranTime.TryParse(raw, out var value));

        var tehran = value.ToOffset(TehranTime.Offset);

        Assert.Equal((year, month, day, hour, minute, second), (tehran.Year, tehran.Month, tehran.Day, tehran.Hour, tehran.Minute, tehran.Second));
        Assert.Equal(TimeSpan.FromHours(3.5), value.Offset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2022/06/09")]
    [InlineData("دیروز")]
    [InlineData("2022/13/45 99:99:99")]
    public void TehranTime_RejectsInvalidInput(string? raw)
    {
        Assert.False(TehranTime.TryParse(raw, out _));
    }

    [Fact]
    public void TehranTime_ParsesPersianDigits()
    {
        Assert.True(TehranTime.TryParse("۲۰۲۲/۰۶/۰۹ ۱۱:۱۴:۴۸", out var value));

        Assert.Equal(new DateTimeOffset(2022, 6, 9, 7, 44, 48, TimeSpan.Zero), value);
    }
}
