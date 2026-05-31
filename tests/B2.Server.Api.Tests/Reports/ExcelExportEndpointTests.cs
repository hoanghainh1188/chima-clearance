using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using B2.Domain.Declarations;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Reports;

public sealed class ExcelExportEndpointTests
{
    private const int PlateColumn = 5;   // E
    private const int DeclarationNumberColumn = 3; // C

    private static readonly string[] ExpectedPlates = { "31A30001", "31A30002", "31A30003", "31A30004" };

    [Fact]
    [Trait("Feature", "F-13")]
    public async Task Should_export_one_day_with_header_and_declaration_rows()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        // Three declarations; the middle one carries a second (child) vehicle.
        var first = await CreateDeclarationAsync(client, "31A30001");
        var second = await CreateDeclarationAsync(client, "31A30002");
        await client.PostAsJsonAsync(
            $"/api/declarations/{second.Id}/vehicles",
            new VehicleAddRequest("31A30003", null, null),
            TestJson.Options);
        await CreateDeclarationAsync(client, "31A30004");

        var date = first.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var ws = await ExportWorksheetAsync(client, date, date);

        // Row 1 is the template column-title row: full Sổ B2 column set preserved.
        ws.Row(1).CellsUsed().Count().Should().BeGreaterThanOrEqualTo(15);

        var headerRows = DataRows(ws).Count(r => r.Cell(1).GetString().StartsWith("Ngày", StringComparison.Ordinal));
        headerRows.Should().Be(1, "a single day was exported");

        var parentRows = DataRows(ws).Count(r => !string.IsNullOrEmpty(r.Cell(DeclarationNumberColumn).GetString()));
        parentRows.Should().Be(3, "three declarations were created");

        var plates = DataRows(ws).Select(r => r.Cell(PlateColumn).GetString()).Where(s => s.Length > 0).ToList();
        plates.Should().BeEquivalentTo(ExpectedPlates);
    }

    [Fact]
    [Trait("Feature", "F-13")]
    public async Task Should_render_unexited_vehicle_in_red()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var decl = await CreateDeclarationAsync(client, "31A30010");
        var date = decl.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var ws = await ExportWorksheetAsync(client, date, date);

        var row = DataRows(ws).Single(r => r.Cell(PlateColumn).GetString() == "31A30010");
        row.Cell(PlateColumn).Style.Font.FontColor.Color.ToArgb()
            .Should().Be(XLColor.Red.Color.ToArgb(), "an un-exited vehicle is shown in red text");
    }

    [Fact]
    [Trait("Feature", "F-13")]
    public async Task Should_strike_through_cancelled_declaration()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var decl = await CreateDeclarationAsync(client, "31A30020");

        // FR-022 cancel endpoint lands later in S5; drive the status directly for now.
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            var entity = await db.Declarations.SingleAsync(d => d.Id == decl.Id);
            db.Entry(entity).Property(d => d.Status).CurrentValue = DeclarationStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        var date = decl.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var ws = await ExportWorksheetAsync(client, date, date);

        var row = DataRows(ws).Single(r => r.Cell(PlateColumn).GetString() == "31A30020");
        row.Cell(PlateColumn).Style.Font.Strikethrough.Should().BeTrue("a cancelled declaration is struck through (FR-023)");
    }

    [Fact]
    [Trait("Feature", "F-13")]
    public async Task Should_forbid_operator_from_exporting()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);

        var res = await client.GetAsync("/api/reports/excel?from=2026-01-01&to=2026-01-02");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Feature", "F-13")]
    public async Task Should_name_the_file_with_the_date_range()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var res = await client.GetAsync("/api/reports/excel?from=2026-02-01&to=2026-02-05");
        res.EnsureSuccessStatusCode();
        res.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        res.Content.Headers.ContentDisposition!.FileName!.Trim('"')
            .Should().Be("SoB2_20260201_20260205.xlsx");
    }

    private static async Task<DeclarationDto> CreateDeclarationAsync(HttpClient client, string plate)
    {
        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                TestDeclarationNumbers.Next(),
                SeedIds.ProcessorId,
                "Công ty ABC",
                "Hàng hóa",
                SeedIds.YardId,
                new VehicleAddRequest(plate, null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;
    }

    private static async Task<IXLWorksheet> ExportWorksheetAsync(HttpClient client, string from, string to)
    {
        var res = await client.GetAsync($"/api/reports/excel?from={from}&to={to}");
        res.EnsureSuccessStatusCode();
        var bytes = await res.Content.ReadAsByteArrayAsync();
        var workbook = new XLWorkbook(new MemoryStream(bytes));
        return workbook.Worksheet(1);
    }

    private static IEnumerable<IXLRangeRow> DataRows(IXLWorksheet ws)
    {
        var used = ws.RangeUsed();
        if (used is null)
        {
            return Array.Empty<IXLRangeRow>();
        }

        // Skip row 1 (the template column titles).
        return used.Rows().Where(r => r.RangeAddress.FirstAddress.RowNumber > 1);
    }
}
