using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SadGallery.Application.Abstractions;
using SadGallery.Application.Market;
using SadGallery.Infrastructure.Market;
using Xunit;

namespace SadGallery.Tests.Integration.Market;

/// <summary>
/// تست‌های هماهنگ‌کننده دریافت نرخ: قفل درون‌فرایندی، حداقل فاصله، به‌روزرسانی کش،
/// مقاومت در برابر خرابی دیتابیس و ثبت وضعیت اجرا.
/// </summary>
public sealed class RateFetchOrchestratorTests
{
    private const string Sample =
        "{\"YekGram18\":1442800,\"SekehEmam\":14500,\"OunceNoghreh\":21940," +
        "\"TimeRead\":\"2022/06/09 11:14:48\"}";

    [Fact]
    public async Task RunOnce_Success_PublishesToCache_AndPersists()
    {
        var store = new FakeStore();
        var cache = new RateSnapshotCache();
        var clock = new TestClock(new DateTimeOffset(2022, 6, 9, 7, 45, 0, TimeSpan.Zero));
        var orchestrator = Create(store, cache, clock, new FakeProvider(Sample));

        var report = await orchestrator.RunOnceAsync(RunTriggers.Manual, TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.Success, report.Status);
        Assert.Equal(3, report.AcceptedCount);
        Assert.True(report.Persisted);
        Assert.Equal(1, store.BeginRunCalls);
        Assert.Equal(1, store.CompleteRunCalls);
        Assert.Single(store.SavedRateBatches);
        Assert.Equal(3, store.SavedRateBatches[0].Count);
        Assert.NotNull(cache.Get());
        Assert.Equal(3, cache.Get()!.Publishable.Count);
        Assert.Equal(RateFetchOutcomeKind.Success, store.Runs[0].Finish!.Outcome);
    }

    [Fact]
    public async Task RunOnce_PersistenceFailure_StillUpdatesCache()
    {
        // خرابی دیتابیس نباید نمایش را از کار بیندازد (اصل «سیستم خراب نشود»).
        var store = new FakeStore { FailOnBeginRun = true, FailOnSave = true };
        var cache = new RateSnapshotCache();
        var clock = new TestClock(new DateTimeOffset(2022, 6, 9, 7, 45, 0, TimeSpan.Zero));

        var report = await Create(store, cache, clock, new FakeProvider(Sample))
            .RunOnceAsync(RunTriggers.Manual, TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.Success, report.Status);
        Assert.False(report.Persisted);
        Assert.NotNull(cache.Get());
        Assert.Equal(3, cache.Get()!.Publishable.Count);
    }

    [Fact]
    public async Task RunOnce_ProviderFailure_KeepsPreviousCacheContent()
    {
        var store = new FakeStore();
        var cache = new RateSnapshotCache();
        var clock = new TestClock(new DateTimeOffset(2022, 6, 9, 7, 45, 0, TimeSpan.Zero));

        // ابتدا یک اجرای موفق، سپس یک شکست: داده قبلی باید بماند.
        var provider = new FakeProvider(Sample);
        var orchestrator = Create(store, cache, clock, provider);

        await orchestrator.RunOnceAsync(RunTriggers.Manual, TestContext.Current.CancellationToken);
        var before = cache.Get();

        provider.FailWith = RateFetchStatus.Timeout;
        var report = await orchestrator.RunOnceAsync(RunTriggers.Manual, TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.Timeout, report.Status);
        Assert.Same(before, cache.Get());
        Assert.Equal(RateFetchOutcomeKind.Failed, store.Runs[^1].Finish!.Outcome);
        Assert.Equal("Timeout", store.Runs[^1].Finish!.ErrorCode);
    }

    [Fact]
    public async Task RunOnce_ConcurrentCalls_InProcessLockRejectsSecond()
    {
        var store = new FakeStore();
        var cache = new RateSnapshotCache();
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var gate = new TaskCompletionSource();
        var provider = new FakeProvider(Sample) { BeforeReturn = gate.Task };
        var orchestrator = Create(store, cache, clock, provider);

        // از انگیزه «Manual» استفاده می‌شود تا بررسی «حداقل فاصله» (که پیش از قفل انجام می‌شود)
        // نتیجه تست را تحت تأثیر نگذارد؛ هدف این تست فقط قفل درون‌فرایندی است.
        var first = orchestrator.RunOnceAsync(RunTriggers.Manual, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);   // اطمینان از ورود اجرای نخست به قفل

        var second = await orchestrator.RunOnceAsync(RunTriggers.Manual, TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.SkippedOverlap, second.Status);

        gate.SetResult();
        var firstReport = await first;
        Assert.Equal(RateFetchStatus.Success, firstReport.Status);
    }

    [Fact]
    public async Task RunOnce_MultiInstanceLeaseDenied_SkipsAndRecords()
    {
        var store = new FakeStore { LeaseAvailable = false };
        var cache = new RateSnapshotCache();
        var clock = new TestClock(DateTimeOffset.UtcNow);

        var report = await Create(store, cache, clock, new FakeProvider(Sample))
            .RunOnceAsync(RunTriggers.Scheduled, TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.SkippedOverlap, report.Status);
        Assert.Equal(1, store.BeginRunCalls);
        Assert.Equal(RateFetchOutcomeKind.SkippedOverlap, store.Runs[0].Finish!.Outcome);
        Assert.Null(cache.Get());
    }

    [Fact]
    public async Task RunOnce_ScheduledTooSoon_IsSkipped_ButManualBypasses()
    {
        var store = new FakeStore();
        var cache = new RateSnapshotCache();
        var clock = new TestClock(new DateTimeOffset(2022, 6, 9, 7, 45, 0, TimeSpan.Zero));
        var orchestrator = Create(store, cache, clock, new FakeProvider(Sample));

        await orchestrator.RunOnceAsync(RunTriggers.Scheduled, TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(10)); // کمتر از بازه ۶۰ ثانیه‌ای
        var tooSoon = await orchestrator.RunOnceAsync(RunTriggers.Scheduled, TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.SkippedTooSoon, tooSoon.Status);
        Assert.Equal(1, store.BeginRunCalls); // اجرای ردشده هیچ رکوردی نمی‌سازد

        clock.Advance(TimeSpan.FromSeconds(5));
        var manual = await orchestrator.RunOnceAsync(RunTriggers.Manual, TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.Success, manual.Status);
        Assert.Equal(2, store.BeginRunCalls);
    }

    [Fact]
    public async Task RunOnce_NotConfigured_DoesNotTouchStoreOrCache()
    {
        var store = new FakeStore();
        var cache = new RateSnapshotCache();
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var provider = new FakeProvider(Sample) { IsConfiguredValue = false };

        var report = await Create(store, cache, clock, provider)
            .RunOnceAsync(RunTriggers.Scheduled, TestContext.Current.CancellationToken);

        Assert.Equal(RateFetchStatus.NotConfigured, report.Status);
        Assert.Equal(0, store.BeginRunCalls);
        Assert.Null(cache.Get());
        Assert.NotNull(report.Message);
        Assert.Contains("RateOptions__Username", report.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunOnce_UnknownKeysAndProblems_AreRecordedInRunNotes()
    {
        var store = new FakeStore();
        var cache = new RateSnapshotCache();
        var clock = new TestClock(new DateTimeOffset(2022, 6, 9, 7, 45, 0, TimeSpan.Zero));
        var body = "{\"YekGram18\":1442800,\"Pelatin\":1442818,\"Dollar\":null,\"TimeRead\":\"2022/06/09 11:14:48\"}";

        await Create(store, cache, clock, new FakeProvider(body))
            .RunOnceAsync(RunTriggers.Manual, TestContext.Current.CancellationToken);

        var notes = store.Runs[0].Finish!.Notes;

        Assert.NotNull(notes);
        Assert.Contains("Pelatin", notes!, StringComparison.Ordinal);
        Assert.Contains("Dollar", notes!, StringComparison.Ordinal);
        Assert.Equal(RateFetchOutcomeKind.SuccessWithProblems, store.Runs[0].Finish!.Outcome);
    }

    private static RateFetchOrchestrator Create(IRateStore store, RateSnapshotCache cache, TestClock clock, IRateProvider provider)
    {
        var options = new RateOptions
        {
            Provider = RateOptions.ProviderTgn,
            FetchIntervalSeconds = 60,
            StaleThresholdMinutes = 15,
            MaxStaleHours = 24,
            AnomalyChangeThresholdPercent = 50m,
        };

        return new RateFetchOrchestrator(
            new TestScopeFactory(store),
            provider,
            cache,
            new RateNormalizer(RateFreshnessPolicy.FromOptions(options), new RateAnomalyDetector(50m), options),
            options,
            clock,
            NullLogger<RateFetchOrchestrator>.Instance);
    }

    /// <summary>
    /// کارخانه اسکوپ حداقلی برای تست‌ها: هر اسکوپ همان مخزن شبیه‌سازی‌شده را برمی‌گرداند
    /// (بدون چرخه عمر DI واقعی که باعث «شیء Dispose‌شده» در تست می‌شد).
    /// </summary>
    private sealed class TestScopeFactory(IRateStore store) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new Scope(store);

        private sealed class Scope(IRateStore store) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new ScopeProvider(store);

            public void Dispose()
            {
                // هیچ منبعی برای آزادسازی نیست.
            }
        }

        private sealed class ScopeProvider(IRateStore store) : IServiceProvider
        {
            public object? GetService(Type serviceType)
            {
                ArgumentNullException.ThrowIfNull(serviceType);

                return serviceType == typeof(IRateStore) ? store : null;
            }
        }
    }

    internal sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = now;

        public void Advance(TimeSpan delta) => UtcNow += delta;
    }

    private sealed class FakeProvider(string body) : IRateProvider
    {
        public bool IsConfiguredValue { get; set; } = true;

        public RateFetchStatus? FailWith { get; set; }

        public Task? BeforeReturn { get; set; }

        public string ProviderId => "Tgn";

        public bool IsConfigured => IsConfiguredValue;

        public async Task<RateProviderFetchResult> FetchAsync(CancellationToken cancellationToken)
        {
            if (BeforeReturn is not null)
            {
                await BeforeReturn.WaitAsync(cancellationToken);
            }

            var now = DateTimeOffset.UtcNow;

            if (FailWith is { } status)
            {
                return RateProviderFetchResult.Failure(ProviderId, status, now, "Timeout", "شکست شبیه‌سازی‌شده (تست).");
            }

            return RateProviderFetchResult.Ok(ProviderId, now, TgnResponseParser.Parse(body), 5);
        }
    }

    private sealed class FakeStore : IRateStore
    {
        public record RunRecord(RateFetchRunStart Start, RateFetchRunFinish? Finish);

        public List<RunRecord> Runs { get; } = [];

        public List<IReadOnlyList<NormalizedRate>> SavedRateBatches { get; } = [];

        public int BeginRunCalls { get; private set; }

        public int CompleteRunCalls { get; private set; }

        public bool FailOnBeginRun { get; init; }

        public bool FailOnSave { get; init; }

        public bool LeaseAvailable { get; init; } = true;

        public Task<long> BeginRunAsync(RateFetchRunStart start, CancellationToken cancellationToken)
        {
            BeginRunCalls++;

            if (FailOnBeginRun)
            {
                throw new InvalidOperationException("دیتابیس در دسترس نیست (تست).");
            }

            Runs.Add(new RunRecord(start, null));
            return Task.FromResult((long)Runs.Count);
        }

        public Task CompleteRunAsync(long runId, RateFetchRunFinish finish, CancellationToken cancellationToken)
        {
            CompleteRunCalls++;
            var index = (int)runId - 1;
            Runs[index] = Runs[index] with { Finish = finish };
            return Task.CompletedTask;
        }

        public Task SaveRatesAsync(long runId, IReadOnlyList<NormalizedRate> rates, CancellationToken cancellationToken)
        {
            if (FailOnSave)
            {
                throw new InvalidOperationException("ثبت نرخ ناموفق (تست).");
            }

            SavedRateBatches.Add(rates);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<NormalizedRate>> GetLatestAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NormalizedRate>>([]);

        public Task<IReadOnlyList<NormalizedRate>> GetHistoryAsync(string assetCode, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NormalizedRate>>([]);

        public Task<bool> TryAcquireLeaseAsync(string ownerId, TimeSpan ttl, CancellationToken cancellationToken) =>
            Task.FromResult(LeaseAvailable);

        public Task ReleaseLeaseAsync(string ownerId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
