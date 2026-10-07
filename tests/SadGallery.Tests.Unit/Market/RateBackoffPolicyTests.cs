using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using Xunit;

namespace SadGallery.Tests.Unit.Market;

/// <summary>تست‌های عقب‌نشینی نمایی و طبقه‌بندی خطا.</summary>
public sealed class RateBackoffPolicyTests
{
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Max = TimeSpan.FromMinutes(30);

    [Fact]
    public void NoFailures_KeepsBaseInterval()
    {
        Assert.Equal(Base, RateBackoffPolicy.NextDelay(0, Base, Max));
    }

    [Theory]
    [InlineData(1, 2)]    // ۱۲۰ ثانیه
    [InlineData(2, 4)]    // ۲۴۰ ثانیه
    [InlineData(3, 8)]    // ۴۸۰ ثانیه
    public void Failures_DoubleTheDelay(int failures, double expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), RateBackoffPolicy.NextDelay(failures, Base, Max));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(int.MaxValue)]
    public void ManyFailures_AreCapped(int failures)
    {
        Assert.Equal(Max, RateBackoffPolicy.NextDelay(failures, Base, Max));
    }

    [Theory]
    [InlineData(RateFetchStatus.Timeout, true)]
    [InlineData(RateFetchStatus.TransportError, true)]
    [InlineData(RateFetchStatus.HttpError, true)]
    [InlineData(RateFetchStatus.PayloadInvalid, true)]
    [InlineData(RateFetchStatus.Unauthorized, true)]
    [InlineData(RateFetchStatus.ResponseTooLarge, true)]
    [InlineData(RateFetchStatus.Success, false)]
    [InlineData(RateFetchStatus.NotConfigured, false)]
    [InlineData(RateFetchStatus.SkippedOverlap, false)]
    [InlineData(RateFetchStatus.SkippedTooSoon, false)]
    public void IsFailure_ClassifiesStatuses(RateFetchStatus status, bool expected)
    {
        Assert.Equal(expected, RateBackoffPolicy.IsFailure(status));
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RateBackoffPolicy.NextDelay(0, TimeSpan.Zero, Max));
        Assert.Throws<ArgumentOutOfRangeException>(() => RateBackoffPolicy.NextDelay(1, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1)));
    }
}
