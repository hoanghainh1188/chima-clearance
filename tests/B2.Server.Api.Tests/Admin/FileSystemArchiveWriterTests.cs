using B2.Infrastructure.Archive;
using ClosedXML.Excel;
using FluentAssertions;

namespace B2.Server.Api.Tests.Admin;

/// <summary>
/// FR-021 / INV-12 — archive verification must gate deletion on declaration count.
/// </summary>
public sealed class FileSystemArchiveWriterTests
{
    [Fact]
    [Trait("Requirement", "FR-021")]
    public void Should_reject_header_only_workbook_when_declarations_expected()
    {
        var path = WriteWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "STT";
            ws.Cell(1, 3).Value = "Số tờ khai";
        });

        var writer = new FileSystemArchiveWriter(Path.GetTempPath());

        writer.VerifyExcel(path, minimumRows: 5).Should().BeFalse(
            "a header-only archive must not satisfy a non-zero declaration count");
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public void Should_accept_workbook_with_expected_declaration_stt_rows()
    {
        var path = WriteWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = "STT";
            ws.Cell(2, 1).Value = 1;
            ws.Cell(3, 1).Value = 2;
            ws.Cell(4, 1).Value = 3;
        });

        var writer = new FileSystemArchiveWriter(Path.GetTempPath());

        writer.VerifyExcel(path, minimumRows: 3).Should().BeTrue();
        writer.VerifyExcel(path, minimumRows: 4).Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public void Should_accept_empty_archive_when_no_declarations_expected()
    {
        var path = WriteWorkbook(ws => ws.Cell(1, 1).Value = "STT");

        var writer = new FileSystemArchiveWriter(Path.GetTempPath());

        writer.VerifyExcel(path, minimumRows: 0).Should().BeTrue();
    }

    private static string WriteWorkbook(Action<IXLWorksheet> configure)
    {
        var path = Path.Combine(Path.GetTempPath(), $"archive-verify-{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sổ B2");
        configure(ws);
        wb.SaveAs(path);
        return path;
    }
}
