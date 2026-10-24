using B2.Application.Abstractions;
using B2.Application.Archive;
using B2.Application.Common;
using B2.Application.Export;
using B2.Domain.DailyHeaders;
using FluentAssertions;
using NSubstitute;

namespace B2.Application.Tests.Jobs;

/// <summary>
/// T-S6-08 — unit tests for <see cref="AnnualArchiveJobHandler"/> covering FR-021 / INV-12.
/// All I/O dependencies are substituted so tests run without file system or database.
/// </summary>
public sealed class AnnualArchiveJobTests
{
    // -----------------------------------------------------------------------
    // Happy-path: old data gets archived and deleted
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_archive_and_delete_declarations_older_than_3_years()
    {
        // Arrange: one year of data older than the 3-year cutoff
        var archiveRepo = Substitute.For<IAnnualArchiveRepository>();
        archiveRepo.GetYearsOlderThanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<int> { 2022 });
        archiveRepo.CountDeclarationsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(5);

        var writer = Substitute.For<IAnnualArchiveWriter>();
        writer.WriteExcelAsync(2022, Arg.Any<Func<Stream, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns("/archive/2022/file.xlsx");
        writer.VerifyExcel("/archive/2022/file.xlsx", 5).Returns(true);

        var exportHandler = BuildExportHandler();
        var clock = new FixedClock(new DateTimeOffset(2026, 6, 6, 0, 0, 0, TimeSpan.Zero));
        var uow = new CountingUow();

        var sut = new AnnualArchiveJobHandler(archiveRepo, writer, exportHandler, Substitute.For<ISoB2StreamingExporter>(), clock, uow);

        // Act
        var results = await sut.HandleAsync(CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        results[0].Year.Should().Be(2022);
        results[0].Success.Should().BeTrue();
        results[0].DeclarationsArchived.Should().Be(5);
        results[0].ErrorMessage.Should().BeNull();

        await archiveRepo.Received(1).DeleteAsync(
            new DateOnly(2022, 1, 1), new DateOnly(2022, 12, 31), Arg.Any<CancellationToken>());
        uow.SaveCalls.Should().Be(1);
    }

    // -----------------------------------------------------------------------
    // FR-021 INV-12: rollback when verification fails
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_not_delete_when_verification_fails()
    {
        var archiveRepo = Substitute.For<IAnnualArchiveRepository>();
        archiveRepo.GetYearsOlderThanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<int> { 2022 });
        archiveRepo.CountDeclarationsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(10);

        var writer = Substitute.For<IAnnualArchiveWriter>();
        writer.WriteExcelAsync(2022, Arg.Any<Func<Stream, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns("/archive/2022/file.xlsx");
        writer.VerifyExcel("/archive/2022/file.xlsx", 10).Returns(false); // FAIL

        var sut = new AnnualArchiveJobHandler(
            archiveRepo, writer, BuildExportHandler(),
            Substitute.For<ISoB2StreamingExporter>(),
            new FixedClock(new DateTimeOffset(2026, 6, 6, 0, 0, 0, TimeSpan.Zero)),
            new CountingUow());

        var results = await sut.HandleAsync(CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].Success.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("Verification failed");

        // Live data MUST NOT be deleted when verification fails (INV-12).
        await archiveRepo.DidNotReceive().DeleteAsync(
            Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // No-op when there is nothing older than 3 years
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_return_empty_when_no_data_is_older_than_3_years()
    {
        var archiveRepo = Substitute.For<IAnnualArchiveRepository>();
        archiveRepo.GetYearsOlderThanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<int>());

        var sut = new AnnualArchiveJobHandler(
            archiveRepo,
            Substitute.For<IAnnualArchiveWriter>(),
            BuildExportHandler(),
            Substitute.For<ISoB2StreamingExporter>(),
            new FixedClock(new DateTimeOffset(2026, 6, 6, 0, 0, 0, TimeSpan.Zero)),
            new CountingUow());

        var results = await sut.HandleAsync(CancellationToken.None);

        results.Should().BeEmpty();
        await archiveRepo.DidNotReceive().DeleteAsync(
            Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Cutoff is exactly today − 3 years
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_use_today_minus_3_years_as_cutoff()
    {
        var capturedCutoff = DateOnly.MinValue;
        var archiveRepo = Substitute.For<IAnnualArchiveRepository>();
        archiveRepo.GetYearsOlderThanAsync(
                Arg.Do<DateOnly>(c => capturedCutoff = c),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<int>());

        var today = new DateTimeOffset(2026, 6, 6, 0, 0, 0, TimeSpan.Zero);
        var sut = new AnnualArchiveJobHandler(
            archiveRepo,
            Substitute.For<IAnnualArchiveWriter>(),
            BuildExportHandler(),
            Substitute.For<ISoB2StreamingExporter>(),
            new FixedClock(today),
            new CountingUow());

        await sut.HandleAsync(CancellationToken.None);

        capturedCutoff.Should().Be(new DateOnly(2023, 6, 6),
            because: "cutoff must be exactly today - 3 years per FR-021");
    }

    // -----------------------------------------------------------------------
    // Multiple years are archived in year order
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_archive_multiple_years_in_ascending_order()
    {
        var archiveRepo = Substitute.For<IAnnualArchiveRepository>();
        archiveRepo.GetYearsOlderThanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<int> { 2020, 2021, 2022 });
        archiveRepo.CountDeclarationsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(3);

        var writer = Substitute.For<IAnnualArchiveWriter>();
        writer.WriteExcelAsync(Arg.Any<int>(), Arg.Any<Func<Stream, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(c => $"/archive/{c.ArgAt<int>(0)}/file.xlsx");
        writer.VerifyExcel(Arg.Any<string>(), Arg.Any<int>()).Returns(true);

        var sut = new AnnualArchiveJobHandler(
            archiveRepo, writer, BuildExportHandler(),
            Substitute.For<ISoB2StreamingExporter>(),
            new FixedClock(new DateTimeOffset(2026, 6, 6, 0, 0, 0, TimeSpan.Zero)),
            new CountingUow());

        var results = await sut.HandleAsync(CancellationToken.None);

        results.Should().HaveCount(3);
        results.Select(r => r.Year).Should().BeInAscendingOrder();
        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
    }

    // -----------------------------------------------------------------------
    // Exception during write does not delete live data
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_record_failure_result_when_write_throws()
    {
        var archiveRepo = Substitute.For<IAnnualArchiveRepository>();
        archiveRepo.GetYearsOlderThanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<int> { 2022 });
        archiveRepo.CountDeclarationsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var writer = Substitute.For<IAnnualArchiveWriter>();
        writer.WriteExcelAsync(Arg.Any<int>(), Arg.Any<Func<Stream, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new IOException("disk full"));

        var sut = new AnnualArchiveJobHandler(
            archiveRepo, writer, BuildExportHandler(),
            Substitute.For<ISoB2StreamingExporter>(),
            new FixedClock(new DateTimeOffset(2026, 6, 6, 0, 0, 0, TimeSpan.Zero)),
            new CountingUow());

        var results = await sut.HandleAsync(CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].Success.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("disk full");

        await archiveRepo.DidNotReceive().DeleteAsync(
            Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // #167: the year is streamed month by month into the archive file
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_stream_the_year_into_the_archive_one_month_at_a_time()
    {
        var archiveRepo = Substitute.For<IAnnualArchiveRepository>();
        archiveRepo.GetYearsOlderThanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<int> { 2022 });
        archiveRepo.CountDeclarationsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var exportRepo = Substitute.For<ISoB2ExportRepository>();
        exportRepo
            .GetDeclarationsForExportAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SoB2DeclarationBlock>());
        var exportHandler = new ExportSoB2QueryHandler(
            exportRepo, new InMemoryDailyHeaderRepo(), Substitute.For<ISoB2Exporter>());

        // The writer runs the content callback against a stream, as the real one does on disk.
        var target = new MemoryStream();
        var writer = Substitute.For<IAnnualArchiveWriter>();
        writer.WriteExcelAsync(2022, Arg.Any<Func<Stream, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async c =>
            {
                await c.ArgAt<Func<Stream, CancellationToken, Task>>(1)(target, CancellationToken.None);
                return "/archive/2022/file.xlsx";
            });
        writer.VerifyExcel("/archive/2022/file.xlsx", 0).Returns(true);

        // The streaming exporter drains the day sequence, which is what triggers the loads.
        var streamingExporter = Substitute.For<ISoB2StreamingExporter>();
        streamingExporter
            .ExportAsync(Arg.Any<IAsyncEnumerable<SoB2DayBlock>>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(async c =>
            {
                await foreach (var _ in c.ArgAt<IAsyncEnumerable<SoB2DayBlock>>(0))
                {
                }
            });

        var sut = new AnnualArchiveJobHandler(
            archiveRepo, writer, exportHandler, streamingExporter,
            new FixedClock(new DateTimeOffset(2026, 6, 6, 0, 0, 0, TimeSpan.Zero)),
            new CountingUow());

        var results = await sut.HandleAsync(CancellationToken.None);

        results.Should().ContainSingle().Which.Success.Should().BeTrue();
        await streamingExporter.Received(1).ExportAsync(
            Arg.Any<IAsyncEnumerable<SoB2DayBlock>>(), target, Arg.Any<CancellationToken>());
        await exportRepo.Received(12).GetDeclarationsForExportAsync(
            Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await exportRepo.Received(1).GetDeclarationsForExportAsync(
            new DateOnly(2022, 2, 1), new DateOnly(2022, 2, 28), Arg.Any<CancellationToken>());
        await exportRepo.Received(1).GetDeclarationsForExportAsync(
            new DateOnly(2022, 12, 1), new DateOnly(2022, 12, 31), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a real <see cref="ExportSoB2QueryHandler"/> wired to empty in-memory
    /// repositories so the archive handler can call it without throwing.
    /// </summary>
    [Fact]
    [Trait("Issue", "144")]
    public async Task Should_compute_the_cutoff_from_the_Vietnam_business_date()
    {
        // 2025-12-31 17:30Z is already 2026-01-01 00:30 in Vietnam (UTC+7): cutoff = 2023-01-01.
        var archiveRepo = Substitute.For<IAnnualArchiveRepository>();
        archiveRepo.GetYearsOlderThanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<int>());
        var clock = new FixedClock(new DateTimeOffset(2025, 12, 31, 17, 30, 0, TimeSpan.Zero));
        var sut = new AnnualArchiveJobHandler(
            archiveRepo, Substitute.For<IAnnualArchiveWriter>(), BuildExportHandler(),
            Substitute.For<ISoB2StreamingExporter>(), clock, new CountingUow());

        await sut.HandleAsync(CancellationToken.None);

        await archiveRepo.Received(1).GetYearsOlderThanAsync(new DateOnly(2023, 1, 1), Arg.Any<CancellationToken>());
    }

    private static ExportSoB2QueryHandler BuildExportHandler()
    {
        var exportRepo = Substitute.For<ISoB2ExportRepository>();
        exportRepo
            .GetDeclarationsForExportAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SoB2DeclarationBlock>());

        var headerRepo = new InMemoryDailyHeaderRepo();

        var exporter = Substitute.For<ISoB2Exporter>();
        exporter.Export(Arg.Any<SoB2ExportData>()).Returns(Array.Empty<byte>());

        return new ExportSoB2QueryHandler(exportRepo, headerRepo, exporter);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
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

    private sealed class InMemoryDailyHeaderRepo : IDailyHeaderRepository
    {
        public Task<DailyHeader?> GetByDateAsync(DateOnly date, CancellationToken ct) => Task.FromResult<DailyHeader?>(null);
        public Task<DateOnly?> GetLatestDateAsync(CancellationToken ct) => Task.FromResult<DateOnly?>(null);
        public Task AddAsync(DailyHeader header, CancellationToken ct) => Task.CompletedTask;
        public Task<int> CountVehiclesEnteredOnDateAsync(DateOnly date, CancellationToken ct) => Task.FromResult(0);
        public Task<bool> ExistsForDateAsync(DateOnly date, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<DailyHeader>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DailyHeader>>(Array.Empty<DailyHeader>());
    }
}
