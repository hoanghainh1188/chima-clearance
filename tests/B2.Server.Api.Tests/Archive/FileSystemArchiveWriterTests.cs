using System.Globalization;
using System.Net.Http.Json;
using B2.Infrastructure.Archive;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using ClosedXML.Excel;
using FluentAssertions;

namespace B2.Server.Api.Tests.Archive;

/// <summary>
/// FR-021 / INV-12 — verifies the real archive writer refuses under-populated exports.
/// </summary>
public sealed class FileSystemArchiveWriterTests
{
    [Fact]
    [Trait("Requirement", "FR-021")]
    public void Should_pass_when_declaration_count_matches_export()
    {
        var dir = Path.Combine(Path.GetTempPath(), "b2-archive-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var writer = new FileSystemArchiveWriter(dir);
            var filePath = WriteSampleArchive(dir, declarationCount: 2);

            writer.VerifyExcel(filePath, 2).Should().BeTrue();
            writer.VerifyExcel(filePath, 1).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public void Should_fail_when_export_has_fewer_declarations_than_live_count()
    {
        var dir = Path.Combine(Path.GetTempPath(), "b2-archive-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var writer = new FileSystemArchiveWriter(dir);
            var filePath = WriteSampleArchive(dir, declarationCount: 1);

            writer.VerifyExcel(filePath, 500)
                .Should().BeFalse("header-only or partial exports must not unlock deletion");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public void Should_pass_for_empty_year_when_minimum_is_zero()
    {
        var dir = Path.Combine(Path.GetTempPath(), "b2-archive-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var writer = new FileSystemArchiveWriter(dir);
            var filePath = WriteSampleArchive(dir, declarationCount: 0);

            writer.VerifyExcel(filePath, 0).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_reject_real_export_with_inflated_minimum_row_count()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var decl = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                TestDeclarationNumbers.Next(),
                SeedIds.ProcessorId,
                "Công ty ABC",
                "Hàng hóa",
                SeedIds.YardId,
                new VehicleAddRequest("31A90001", null, null)),
            TestJson.Options);
        decl.EnsureSuccessStatusCode();
        var dto = (await decl.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;
        var date = dto.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var export = await client.GetAsync($"/api/reports/excel?from={date}&to={date}");
        export.EnsureSuccessStatusCode();
        var bytes = await export.Content.ReadAsByteArrayAsync();

        var dir = Path.Combine(Path.GetTempPath(), "b2-archive-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        try
        {
            var writer = new FileSystemArchiveWriter(dir);
            var filePath = await writer.WriteExcelAsync(dto.BusinessDate.Year, bytes, CancellationToken.None);

            writer.VerifyExcel(filePath, 99).Should().BeFalse();
            writer.VerifyExcel(filePath, 1).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string WriteSampleArchive(string dir, int declarationCount)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sổ B2");
        ws.Cell(1, 1).Value = "STT";
        ws.Cell(1, 3).Value = "Số tờ khai";

        var row = 2;
        ws.Range(row, 1, row, 15).Merge();
        ws.Cell(row, 1).Value = "Ngày 01/01/2022";
        row++;

        for (var i = 1; i <= declarationCount; i++)
        {
            ws.Cell(row, 1).Value = i;
            ws.Cell(row, 3).Value = $"TK{i:D4}";
            row++;
        }

        var filePath = Path.Combine(dir, "sample.xlsx");
        wb.SaveAs(filePath);
        return filePath;
    }
}
