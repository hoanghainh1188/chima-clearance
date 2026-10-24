using B2.Application.Abstractions;
using B2.Domain;
using B2.Domain.Declarations;
using B2.Domain.Users;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Persistence;

/// <summary>
/// #146 / #154 — per-day vehicle tallies follow the vehicle's own EntryTime / ExitTime in
/// Vietnam time, not the parent declaration's BusinessDate (declarations span days).
/// </summary>
public sealed class VehicleDayFilterTests
{
    private static readonly DateOnly Day1 = new(2026, 5, 4);
    private static readonly DateOnly Day2 = new(2026, 5, 5);

    // Vietnam is UTC+7: 10:00 ICT = 03:00Z, 23:30 ICT = 16:30Z, 08:15 ICT = 01:15Z.
    private static readonly DateTimeOffset Day1Morning = new(2026, 5, 4, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day1LateNight = new(2026, 5, 4, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2Morning = new(2026, 5, 5, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2ExitAt0815 = new(2026, 5, 5, 1, 15, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Issue", "146")]
    public async Task CountVehiclesEnteredOnDate_counts_by_vehicle_entry_time()
    {
        await using var factory = new B2ApiFactory();
        _ = factory.CreateClient();
        await SeedAsync(factory, cancelledExtra: true);

        await using var scope = factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IDailyHeaderRepository>();

        // Day1: morning + 23:30 ICT entries. The day-2 AddVehicle and the cancelled one are excluded.
        (await repo.CountVehiclesEnteredOnDateAsync(Day1, CancellationToken.None)).Should().Be(2);
        (await repo.CountVehiclesEnteredOnDateAsync(Day2, CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    [Trait("Issue", "154")]
    public async Task GetByHour_buckets_vehicles_by_the_day_they_exited()
    {
        await using var factory = new B2ApiFactory();
        _ = factory.CreateClient();
        await SeedAsync(factory, cancelledExtra: false);

        await using var scope = factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IReportsRepository>();

        var day2 = await repo.GetByHourAsync(Day2, 60, includeCancelled: false, CancellationToken.None);
        day2.Should().ContainSingle().Which.Should().Match<Shared.Contracts.Reports.HourReportItemDto>(
            x => x.BucketLabel == "08:00-09:00");

        (await repo.GetByHourAsync(Day1, 60, includeCancelled: false, CancellationToken.None))
            .Should().BeEmpty("the day-1 declaration's vehicle exited on day 2");
    }

    [Fact]
    [Trait("Issue", "165")]
    [Trait("Issue", "180")]
    [Trait("Issue", "185")]
    public async Task Large_id_collections_translate_to_a_single_json_parameter()
    {
        // EF Core 8 sends a parameterized Contains() list as one JSON value via json_each, so
        // SQLite's host-parameter limit (999 / 32 766) never applies. Guard that translation.
        await using var factory = new B2ApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var ids = Enumerable.Range(0, 40_000).Select(_ => Guid.NewGuid()).ToList();

        var query = db.Vehicles.Where(v => ids.Contains(v.DeclarationId));

        query.ToQueryString().Should().Contain("json_each");
        (await query.CountAsync()).Should().Be(0);
    }

    private static async Task SeedAsync(B2ApiFactory factory, bool cancelledExtra)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();

        var decl = NewDeclaration(Day1Morning, Day1);
        var first = decl.AddVehicle(Guid.NewGuid(), PlateNumber.Parse("29A11111"), Day1Morning, SeedIds.AdminUserId, null, null);
        decl.AddVehicle(Guid.NewGuid(), PlateNumber.Parse("29A22222"), Day1LateNight, SeedIds.AdminUserId, null, null);
        decl.AddVehicle(Guid.NewGuid(), PlateNumber.Parse("29A33333"), Day2Morning, SeedIds.AdminUserId, null, null);
        first.MarkExit(SeedIds.AdminUserId, Day2ExitAt0815);
        decl.RecomputeStatus();
        db.Declarations.Add(decl);

        if (cancelledExtra)
        {
            var cancelled = NewDeclaration(Day1Morning, Day1);
            cancelled.AddVehicle(Guid.NewGuid(), PlateNumber.Parse("29A44444"), Day1Morning, SeedIds.AdminUserId, null, null);
            cancelled.Cancel(Role.Admin, SeedIds.AdminUserId, "test-cancel", Day1Morning.AddMinutes(5));
            db.Declarations.Add(cancelled);
        }

        await db.SaveChangesAsync();
    }

    private static Declaration NewDeclaration(DateTimeOffset enteredAt, DateOnly businessDate) => Declaration.CreateNew(
        Guid.NewGuid(),
        [DeclarationNumber.Parse(TestDeclarationNumbers.Next())],
        SeedIds.ProcessorId,
        "Co",
        "Hang",
        SeedIds.YardId,
        SeedIds.AdminUserId,
        enteredAt,
        businessDate);
}
