using System.Globalization;
using B2.Application.Export;
using B2.Infrastructure.Archive;
using B2.Infrastructure.Excel;
using FluentAssertions;

namespace B2.Server.Api.Tests.Archive;

/// <summary>
/// FR-021 — verifies the archive writer enforces the declaration-count gate before
/// the annual job is allowed to delete live rows (INV-12).
/// </summary>
public sealed class FileSystemArchiveWriterTests
{
    [Fact]
    [Trait("Requirement", "FR-021")]
    public void VerifyExcel_should_pass_when_declaration_count_matches_minimum()
    {
        var path = WriteArchiveFile(declarationCount: 3);
        var writer = new FileSystemArchiveWriter(Path.GetTempPath());

        writer.VerifyExcel(path, 3).Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public void VerifyExcel_should_fail_when_archive_has_fewer_declarations_than_counted()
    {
        var path = WriteArchiveFile(declarationCount: 0);
        var writer = new FileSystemArchiveWriter(Path.GetTempPath());

        writer.VerifyExcel(path, 5).Should().BeFalse(
            "a header-only archive must not satisfy a non-zero declaration count");
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public void VerifyExcel_should_pass_for_empty_year_with_zero_minimum()
    {
        var path = WriteArchiveFile(declarationCount: 0);
        var writer = new FileSystemArchiveWriter(Path.GetTempPath());

        writer.VerifyExcel(path, 0).Should().BeTrue();
    }

    private static string WriteArchiveFile(int declarationCount)
    {
        var exporter = new ClosedXmlSoB2Exporter(Path.GetTempPath());
        var date = DateOnly.Parse("2022-01-15", CultureInfo.InvariantCulture);
        var now = DateTimeOffset.Parse("2022-01-15T08:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        var declarations = Enumerable.Range(1, declarationCount)
            .Select(i => new SoB2DeclarationBlock(
                date,
                $"12345678901{i}",
                "Processor",
                "Company",
                "Goods",
                "Yard",
                KvgsConfirmed: false,
                IsAllExited: false,
                IsCancelled: false,
                "Officer",
                null,
                new List<SoB2VehicleRow>
                {
                    new($"29A{i:D5}", null, now, false, null, null),
                }))
            .ToList();

        var bytes = exporter.Export(new SoB2ExportData(
            date,
            date,
            new List<SoB2DayBlock>
            {
                new(date, "Officer", declarationCount, declarations),
            }));

        var dir = Path.Combine(Path.GetTempPath(), "b2-archive-writer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var writer = new FileSystemArchiveWriter(dir);
        return writer.WriteExcelAsync(2022, bytes, CancellationToken.None).GetAwaiter().GetResult();
    }
}
