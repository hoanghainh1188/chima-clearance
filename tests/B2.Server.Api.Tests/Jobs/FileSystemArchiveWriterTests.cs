using B2.Infrastructure.Archive;
using ClosedXML.Excel;
using FluentAssertions;

namespace B2.Server.Api.Tests.Jobs;

/// <summary>
/// FR-021 — <see cref="FileSystemArchiveWriter.VerifyExcel"/> must gate deletion on
/// the archived declaration count, not merely on the file being readable.
/// </summary>
public sealed class FileSystemArchiveWriterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "b2-archive-test-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemArchiveWriter _writer;

    public FileSystemArchiveWriterTests()
    {
        Directory.CreateDirectory(_tempDir);
        _writer = new FileSystemArchiveWriter(_tempDir);
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public void Should_reject_archive_when_declaration_count_is_below_minimum()
    {
        var path = WriteWorkbook(declarationNumbers: ["TK001"]);

        _writer.VerifyExcel(path, minimumRows: 5).Should().BeFalse(
            because: "a header-only or under-populated archive must not authorize live-data deletion");
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public void Should_accept_archive_when_declaration_count_meets_minimum()
    {
        var path = WriteWorkbook(declarationNumbers: ["TK001", "TK002", "TK003"]);

        _writer.VerifyExcel(path, minimumRows: 3).Should().BeTrue();
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public void Should_accept_empty_archive_when_no_declarations_were_archived()
    {
        var path = WriteWorkbook(declarationNumbers: []);

        _writer.VerifyExcel(path, minimumRows: 0).Should().BeTrue();
    }

    private string WriteWorkbook(IReadOnlyList<string> declarationNumbers)
    {
        var path = Path.Combine(_tempDir, "archive.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sổ B2");
        ws.Cell(1, 1).Value = "STT";
        ws.Cell(1, 3).Value = "Số tờ khai";

        var row = 2;
        for (var i = 0; i < declarationNumbers.Count; i++)
        {
            ws.Cell(row, 1).Value = i + 1;
            ws.Cell(row, 3).Value = declarationNumbers[i];
            row++;
        }

        wb.SaveAs(path);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
