using B2.Application.Export;
using B2.Infrastructure.Archive;
using B2.Infrastructure.Excel;
using FluentAssertions;

namespace B2.Server.Api.Tests.Archive;

/// <summary>
/// FR-021 / INV-12 — integration tests for <see cref="FileSystemArchiveWriter.VerifyExcel"/>.
/// </summary>
public sealed class FileSystemArchiveWriterTests
{
    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_verify_archive_when_declaration_count_matches_minimum()
    {
        var dir = CreateTempDir();
        try
        {
            var writer = new FileSystemArchiveWriter(dir);
            var excelBytes = BuildExport(declarationCount: 2);
            var path = await writer.WriteExcelAsync(2022, excelBytes, CancellationToken.None);

            writer.VerifyExcel(path, minimumRows: 2).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_fail_verification_when_archive_is_missing_declarations()
    {
        var dir = CreateTempDir();
        try
        {
            var writer = new FileSystemArchiveWriter(dir);
            var excelBytes = BuildExport(declarationCount: 1);
            var path = await writer.WriteExcelAsync(2022, excelBytes, CancellationToken.None);

            writer.VerifyExcel(path, minimumRows: 5).Should().BeFalse(
                "INV-12 requires refusing delete when the archive is under-populated");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_pass_verification_for_empty_archive_when_minimum_is_zero()
    {
        var dir = CreateTempDir();
        try
        {
            var writer = new FileSystemArchiveWriter(dir);
            var excelBytes = BuildExport(declarationCount: 0);
            var path = await writer.WriteExcelAsync(2022, excelBytes, CancellationToken.None);

            writer.VerifyExcel(path, minimumRows: 0).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), $"b2-archive-{Guid.NewGuid():N}");

    private static byte[] BuildExport(int declarationCount)
    {
        var date = new DateOnly(2022, 3, 15);
        var declarations = Enumerable.Range(1, declarationCount)
            .Select(i => new SoB2DeclarationBlock(
                date,
                $"31A{i:0000}",
                "Processor",
                "Company",
                "Goods",
                "Yard",
                KvgsConfirmed: false,
                IsAllExited: false,
                IsCancelled: false,
                EnteredByName: "Officer",
                KvgsConfirmedByName: null,
                Vehicles: new List<SoB2VehicleRow>
                {
                    new($"31A{i:0000}", null, DateTimeOffset.UtcNow, false, null, null),
                }))
            .ToList();

        var data = new SoB2ExportData(
            date,
            date,
            new List<SoB2DayBlock>
            {
                new(date, "Officer", declarationCount, declarations),
            });

        var exporter = new ClosedXmlSoB2Exporter(Path.GetTempPath());
        return exporter.Export(data);
    }
}
