using B2.Application.Abstractions;
using B2.Application.DailyHeaders;
using B2.Domain.Audit;
using B2.Domain.DailyHeaders;
using FluentAssertions;

namespace B2.Application.Tests.Jobs;

public sealed class DailyHeaderJobTests
{
    [Fact]
    [Trait("Feature", "F-11")]
    public async Task Should_create_header_at_0001_when_missing()
    {
        var repo = new InMemoryDailyHeaderRepo();
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 05, 0, 1, 0, TimeSpan.Zero));
        var uow = new CountingUow();
        var sut = new EnsureTodayHeaderCommandHandler(repo, clock, uow);

        var header = await sut.HandleAsync(CancellationToken.None);
        header.Date.Should().Be(new DateOnly(2026, 5, 5));
        (await repo.ExistsForDateAsync(new DateOnly(2026, 5, 5), CancellationToken.None)).Should().BeTrue();
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "F-11")]
    public async Task Should_be_idempotent_when_header_already_exists()
    {
        var repo = new InMemoryDailyHeaderRepo();
        repo.Headers.Add(new DailyHeader { Id = Guid.NewGuid(), Date = new DateOnly(2026, 5, 5), CreatedAt = DateTimeOffset.UtcNow });
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 05, 6, 30, 0, TimeSpan.Zero));
        var uow = new CountingUow();
        var sut = new EnsureTodayHeaderCommandHandler(repo, clock, uow);

        _ = await sut.HandleAsync(CancellationToken.None);
        _ = await sut.HandleAsync(CancellationToken.None);

        repo.Headers.Should().HaveCount(1);
        uow.SaveCalls.Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "F-11")]
    public async Task Should_catch_up_today_header_on_startup_when_missing()
    {
        var repo = new InMemoryDailyHeaderRepo();
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 10, 0, 5, 0, TimeSpan.Zero));
        var sut = new EnsureTodayHeaderCommandHandler(repo, clock, new CountingUow());

        var window = await sut.EnsureCatchUpAsync(CancellationToken.None);

        // Vietnam (UTC+7) business date for 00:05Z on 2026-05-10 is the 10th.
        window.Should().ContainSingle().Which.Should().Be(new DateOnly(2026, 5, 10));
        (await repo.ExistsForDateAsync(new DateOnly(2026, 5, 10), CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "F-11")]
    public async Task Should_backfill_every_missed_day_after_a_multi_day_outage()
    {
        var repo = new InMemoryDailyHeaderRepo();
        // Last header on record is three days before "today".
        repo.Headers.Add(new DailyHeader { Id = Guid.NewGuid(), Date = new DateOnly(2026, 5, 7), CreatedAt = DateTimeOffset.UtcNow });
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 10, 0, 5, 0, TimeSpan.Zero));
        var uow = new CountingUow();
        var sut = new EnsureTodayHeaderCommandHandler(repo, clock, uow);

        var window = await sut.EnsureCatchUpAsync(CancellationToken.None);

        window.Should().Equal(new DateOnly(2026, 5, 8), new DateOnly(2026, 5, 9), new DateOnly(2026, 5, 10));
        repo.Headers.Select(h => h.Date).Should().BeEquivalentTo(new[]
        {
            new DateOnly(2026, 5, 7),
            new DateOnly(2026, 5, 8),
            new DateOnly(2026, 5, 9),
            new DateOnly(2026, 5, 10),
        });
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "F-11")]
    public async Task Should_only_create_today_on_fresh_install_with_no_prior_headers()
    {
        var repo = new InMemoryDailyHeaderRepo();
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 10, 0, 5, 0, TimeSpan.Zero));
        var sut = new EnsureTodayHeaderCommandHandler(repo, clock, new CountingUow());

        var window = await sut.EnsureCatchUpAsync(CancellationToken.None);

        window.Should().ContainSingle().Which.Should().Be(new DateOnly(2026, 5, 10));
        repo.Headers.Should().ContainSingle();
    }

    [Fact]
    [Trait("Feature", "F-11")]
    public async Task Should_be_idempotent_catch_up_when_all_days_present()
    {
        var repo = new InMemoryDailyHeaderRepo();
        repo.Headers.Add(new DailyHeader { Id = Guid.NewGuid(), Date = new DateOnly(2026, 5, 10), CreatedAt = DateTimeOffset.UtcNow });
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 10, 0, 5, 0, TimeSpan.Zero));
        var uow = new CountingUow();
        var sut = new EnsureTodayHeaderCommandHandler(repo, clock, uow);

        _ = await sut.EnsureCatchUpAsync(CancellationToken.None);

        repo.Headers.Should().ContainSingle();
        uow.SaveCalls.Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "F-12")]
    public async Task Should_recompute_summary_at_2300()
    {
        var repo = new InMemoryDailyHeaderRepo { VehicleCount = 7 };
        repo.Headers.Add(new DailyHeader { Id = Guid.NewGuid(), Date = new DateOnly(2026, 5, 5), CreatedAt = DateTimeOffset.UtcNow });
        var clock = new FixedClock(new DateTimeOffset(2026, 05, 05, 23, 0, 0, TimeSpan.Zero));
        var uow = new CountingUow();
        var sut = new RecomputeDailySummaryCommandHandler(repo, clock, uow, new NoopAuditContext());

        var count = await sut.HandleAsync(new RecomputeDailySummaryCommand(new DateOnly(2026, 5, 5), "manual"), CancellationToken.None);
        count.Should().Be(7);
        repo.Headers.Single().TotalVehicles.Should().Be(7);
        repo.Headers.Single().LastSummaryComputedAt.Should().NotBeNull();
        uow.SaveCalls.Should().Be(1);
    }

    private sealed class InMemoryDailyHeaderRepo : IDailyHeaderRepository
    {
        public List<DailyHeader> Headers { get; } = [];

        public int VehicleCount { get; set; }

        public Task<DailyHeader?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken) =>
            Task.FromResult<DailyHeader?>(Headers.FirstOrDefault(x => x.Date == date));

        public Task<DateOnly?> GetLatestDateAsync(CancellationToken cancellationToken) =>
            Task.FromResult<DateOnly?>(Headers.Count == 0 ? null : Headers.Max(x => x.Date));

        public Task AddAsync(DailyHeader header, CancellationToken cancellationToken)
        {
            Headers.Add(header);
            return Task.CompletedTask;
        }

        public Task<int> CountVehiclesEnteredOnDateAsync(DateOnly date, CancellationToken cancellationToken) =>
            Task.FromResult(VehicleCount);

        public Task<bool> ExistsForDateAsync(DateOnly date, CancellationToken cancellationToken) =>
            Task.FromResult(Headers.Any(x => x.Date == date));

        public Task<IReadOnlyList<DailyHeader>> GetByDateRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DailyHeader>>(Headers.Where(h => h.Date >= fromDate && h.Date <= toDate).ToList());
    }

    private sealed class CountingUow : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class NoopAuditContext : IAuditContext
    {
        public void Set(AuditEnvelope envelope)
        {
        }
    }
}
