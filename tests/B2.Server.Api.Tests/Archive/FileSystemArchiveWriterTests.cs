using B2.Application.Export;
using B2.Infrastructure.Archive;
using B2.Infrastructure.Excel;
using FluentAssertions;

namespace B2.Server.Api.Tests.Archive;

/// <summary>
/// FR-021 / INV-12 — verifies the archive writer refuses under-populated Excel files
/// so the annual job cannot delete live rows without a complete export.
/// </summary>
public sealed class FileSystemArchiveWriterTests
{
    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_pass_verification_when_archive_contains_all_declarations()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var exporter = new ClosedXmlSoB2Exporter(tempDir);
            var writer = new FileSystemArchiveWriter(Path.Combine(tempDir, "archive"));
            var bytes = exporter.Export(BuildExportData(declarationCount: 3));

            var path = await writer.WriteExcelAsync(2022, bytes, CancellationToken.None);

            writer.VerifyExcel(path, minimumRows: 3).Should().BeTrue(
                "the archive must contain every declaration counted in the live DB");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_fail_verification_when_archive_is_header_only_but_db_had_rows()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var exporter = new ClosedXmlSoB2Exporter(tempDir);
            var writer = new FileSystemArchiveWriter(Path.Combine(tempDir, "archive"));
            var bytes = exporter.Export(BuildExportData(declarationCount: 0));

            var path = await writer.WriteExcelAsync(2022, bytes, CancellationToken.None);

            writer.VerifyExcel(path, minimumRows: 5).Should().BeFalse(
                "INV-12: a header-only archive must not authorize deleting live rows");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_pass_verification_for_empty_year_with_zero_minimum_rows()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var exporter = new ClosedXmlSoB2Exporter(tempDir);
            var writer = new FileSystemArchiveWriter(Path.Combine(tempDir, "archive"));
            var bytes = exporter.Export(BuildExportData(declarationCount: 0));

            var path = await writer.WriteExcelAsync(2022, bytes, CancellationToken.None);

            writer.VerifyExcel(path, minimumRows: 0).Should().BeTrue(
                "an empty year with no declarations is a valid no-op archive");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_count_only_parent_declaration_rows_not_child_vehicles()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var exporter = new ClosedXmlSoB2Exporter(tempDir);
            var writer = new FileSystemArchiveWriter(Path.Combine(tempDir, "archive"));
            var bytes = exporter.Export(BuildExportData(declarationCount: 1, childVehicles: 2));

            var path = await writer.WriteExcelAsync(2022, bytes, CancellationToken.None);

            writer.VerifyExcel(path, minimumRows: 1).Should().BeTrue();
            writer.VerifyExcel(path, minimumRows: 2).Should().BeFalse(
                "child vehicle rows must not inflate the declaration count");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static SoB2ExportData BuildExportData(int declarationCount, int childVehicles = 0)
    {
        var date = new DateOnly(2022, 3, 15);
        var declarations = new List<SoB2DeclarationBlock>(declarationCount);
        for (var i = 0; i < declarationCount; i++)
        {
            var vehicles = new List<SoB2VehicleRow>
            {
                new($"31A{i:0000}1", null, new DateTimeOffset(2022, 3, 15, 8, 0, 0, TimeSpan.Zero), false, null, null),
            };

            for (var c = 0; c < childVehicles; c++)
            {
                vehicles.Add(new($"31A{i:0000}{c + 2}", null, new DateTimeOffset(2022, 3, 15, 9, 0, 0, TimeSpan.Zero), false, null, null));
            }

            declarations.Add(new SoB2DeclarationBlock(
                date,
                $"TK{i:0000}",
                "Processor A",
                "Company",
                "Goods",
                "Yard 1",
                KvgsConfirmed: false,
                IsAllExited: false,
                IsCancelled: false,
                "Officer",
                null,
                vehicles));
        }

        var day = new SoB2DayBlock(date, "Officer", declarationCount, declarations);
        return new SoB2ExportData(date, date, new[] { day });
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "b2-archive-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
