using B2.Application.Export;
using B2.Infrastructure.Archive;
using B2.Infrastructure.Excel;
using FluentAssertions;

namespace B2.Server.Api.Tests.Archive;

/// <summary>
/// FR-021 / INV-12 — <see cref="FileSystemArchiveWriter.VerifyExcel"/> must refuse to
/// confirm (and therefore refuse to delete live data) when the archive file holds fewer
/// declaration rows than were exported. The archive is written by the streaming exporter (#167).
/// </summary>
public sealed class FileSystemArchiveWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"b2-archive-{Guid.NewGuid():N}");

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_verify_when_declaration_count_meets_minimum()
    {
        var writer = new FileSystemArchiveWriter(_dir);
        var path = await WriteStreamedAsync(writer, SoB2ExportSamples.Simple(declarationCount: 2));

        writer.VerifyExcel(path, minimumRows: 2).Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_fail_verification_when_archive_is_missing_declarations()
    {
        var writer = new FileSystemArchiveWriter(_dir);
        // Only 1 declaration written but the job expected 5 → truncation → must NOT delete.
        var path = await WriteStreamedAsync(writer, SoB2ExportSamples.Simple(declarationCount: 1));

        writer.VerifyExcel(path, minimumRows: 5).Should().BeFalse(
            "INV-12 requires refusing to delete live data when the archive is under-populated");
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_pass_verification_for_empty_archive_when_minimum_is_zero()
    {
        var writer = new FileSystemArchiveWriter(_dir);
        var path = await WriteStreamedAsync(writer, SoB2ExportSamples.Simple(declarationCount: 0));

        writer.VerifyExcel(path, minimumRows: 0).Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_count_one_row_per_declaration_not_per_vehicle_or_day()
    {
        // Mixed(): 4 declarations, 6 data rows, 2 day-header rows.
        var writer = new FileSystemArchiveWriter(_dir);
        var path = await WriteStreamedAsync(writer, SoB2ExportSamples.Mixed());

        writer.VerifyExcel(path, minimumRows: 4).Should().BeTrue();
        writer.VerifyExcel(path, minimumRows: 5).Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_verify_a_closedxml_workbook_that_uses_shared_strings()
    {
        // The streaming reader must also understand shared-string cells (ClosedXML's output).
        var writer = new FileSystemArchiveWriter(_dir);
        var bytes = new ClosedXmlSoB2Exporter(_dir).Export(SoB2ExportSamples.Simple(declarationCount: 3));
        var path = await writer.WriteExcelAsync(2022, (s, ct) => s.WriteAsync(bytes, ct).AsTask(), CancellationToken.None);

        writer.VerifyExcel(path, minimumRows: 3).Should().BeTrue();
        writer.VerifyExcel(path, minimumRows: 4).Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_fail_verification_for_a_corrupt_file()
    {
        var writer = new FileSystemArchiveWriter(_dir);
        var path = await writer.WriteExcelAsync(
            2022, (s, ct) => s.WriteAsync("not a zip"u8.ToArray(), ct).AsTask(), CancellationToken.None);

        writer.VerifyExcel(path, minimumRows: 0).Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_replace_a_read_only_archive_left_by_a_failed_run()
    {
        var writer = new FileSystemArchiveWriter(_dir);
        var first = await WriteStreamedAsync(writer, SoB2ExportSamples.Simple(declarationCount: 1));
        File.GetAttributes(first).Should().HaveFlag(FileAttributes.ReadOnly);

        var second = await WriteStreamedAsync(writer, SoB2ExportSamples.Simple(declarationCount: 3));

        second.Should().Be(first);
        writer.VerifyExcel(second, minimumRows: 3).Should().BeTrue();
        File.GetAttributes(second).Should().HaveFlag(FileAttributes.ReadOnly);
        Directory.GetFiles(Path.GetDirectoryName(second)!).Should().ContainSingle("no .partial file is left behind");
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_not_leave_an_archive_when_the_content_writer_throws()
    {
        var writer = new FileSystemArchiveWriter(_dir);

        var act = () => writer.WriteExcelAsync(
            2022, (_, _) => throw new IOException("disk full"), CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        Directory.GetFiles(Path.Combine(_dir, "2022")).Should().BeEmpty();
    }

    [Fact]
    [Trait("Issue", "145")]
    public void Verification_failure_keeps_the_archive_read_only()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "corrupt.xlsx");
        File.WriteAllText(path, "not a workbook");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        new FileSystemArchiveWriter(_dir).VerifyExcel(path, minimumRows: 1).Should().BeFalse();

        File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly).Should().BeTrue(
            "a workbook that fails to open must not leave the archive writable");
    }

    public void Dispose()
    {
        // WriteExcelAsync marks the archive file read-only (FR-021), so a plain recursive
        // delete throws UnauthorizedAccessException on Windows. Clear the attribute first.
        if (!Directory.Exists(_dir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_dir, recursive: true);
    }

    private Task<string> WriteStreamedAsync(FileSystemArchiveWriter writer, SoB2ExportData data) =>
        writer.WriteExcelAsync(
            2022,
            (stream, ct) => new OpenXmlStreamingSoB2Exporter(_dir).ExportAsync(SoB2ExportSamples.Stream(data), stream, ct),
            CancellationToken.None);
}
