using B2.Application.Export;
using B2.Infrastructure.Archive;
using B2.Infrastructure.Excel;
using FluentAssertions;

namespace B2.Server.Api.Tests.Archive;

/// <summary>
/// FR-021 / INV-12 — <see cref="FileSystemArchiveWriter.VerifyExcel"/> must refuse to
/// confirm (and therefore refuse to delete live data) when the archive file holds fewer
/// declaration rows than were exported.
/// </summary>
public sealed class FileSystemArchiveWriterTests
{
    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_verify_when_declaration_count_meets_minimum()
    {
        var dir = CreateTempDir();
        try
        {
            var writer = new FileSystemArchiveWriter(dir);
            var path = await writer.WriteExcelAsync(2022, BuildExport(declarationCount: 2), CancellationToken.None);

            writer.VerifyExcel(path, minimumRows: 2).Should().BeTrue();
        }
        finally
        {
            DeleteDir(dir);
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
            // Only 1 declaration written but the job expected 5 → truncation → must NOT delete.
            var path = await writer.WriteExcelAsync(2022, BuildExport(declarationCount: 1), CancellationToken.None);

            writer.VerifyExcel(path, minimumRows: 5).Should().BeFalse(
                "INV-12 requires refusing to delete live data when the archive is under-populated");
        }
        finally
        {
            DeleteDir(dir);
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
            var path = await writer.WriteExcelAsync(2022, BuildExport(declarationCount: 0), CancellationToken.None);

            writer.VerifyExcel(path, minimumRows: 0).Should().BeTrue();
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    [Fact]
    [Trait("Issue", "145")]
    public void Verification_failure_keeps_the_archive_read_only()
    {
        var dir = CreateTempDir();
        try
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "corrupt.xlsx");
            File.WriteAllText(path, "not a workbook");
            File.SetAttributes(path, FileAttributes.ReadOnly);

            new FileSystemArchiveWriter(dir).VerifyExcel(path, minimumRows: 1).Should().BeFalse();

            File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly).Should().BeTrue(
                "a workbook that fails to open must not leave the archive writable");
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    private static string CreateTempDir() =>
        Path.Combine(Path.GetTempPath(), $"b2-archive-{Guid.NewGuid():N}");

    // WriteExcelAsync marks the archive file read-only (FR-021), so a plain recursive
    // delete throws UnauthorizedAccessException on Windows. Clear the attribute first.
    private static void DeleteDir(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(dir, recursive: true);
    }

    private static byte[] BuildExport(int declarationCount)
    {
        var date = new DateOnly(2022, 3, 15);
        var declarations = Enumerable.Range(1, declarationCount)
            .Select(i => new SoB2DeclarationBlock(
                date,
                FormattableString.Invariant($"31A{i:0000}"),
                new[] { FormattableString.Invariant($"31A{i:0000}") },
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
                    new(FormattableString.Invariant($"31A{i:0000}"), null, DateTimeOffset.UtcNow, false, null, null),
                }))
            .ToList();

        var data = new SoB2ExportData(
            date,
            date,
            new List<SoB2DayBlock>
            {
                new(date, "Officer", declarationCount, declarations),
            });

        return new ClosedXmlSoB2Exporter(Path.GetTempPath()).Export(data);
    }
}
