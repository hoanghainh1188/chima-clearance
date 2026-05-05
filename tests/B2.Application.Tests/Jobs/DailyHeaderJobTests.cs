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
