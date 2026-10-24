using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using ClosedXML.Excel;
using FluentAssertions;

namespace B2.Server.Api.Tests.Reports;

public sealed class ExcelExportEndpointTests
{
    private const int PlateColumn = 5;   // E
    private const int DeclarationNumberColumn = 3; // C
    private const int EntryTimeColumn = 9;  // I
    private const int ExitTimeColumn = 11; // K

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

        // Tổng xe on the daily row is only filled once the day is summarised (the 23:00 job, or
        // the ledger's "Tính lại" button). Export before that and cột E is legitimately blank.
        var recompute = await client.PostAsJsonAsync(
            $"/api/daily-headers/{date}/recompute",
            new RecomputeDailySummaryRequest("UAT export check"),
            TestJson.Options);
        recompute.StatusCode.Should().Be(HttpStatusCode.OK);

        var ws = await ExportWorksheetAsync(client, date, date);

        // Row 1 is the template column-title row: full Sổ B2 column set preserved.
        ws.Row(1).CellsUsed().Count().Should().BeGreaterThanOrEqualTo(15);

        // The daily row follows the unit's own file: ngày in cột D, tổng xe as a NUMBER in cột E,
        // người trực in cột F — not one merged sentence across A:O.
        var dayRows = DataRows(ws)
            .Where(r => r.Cell(4).GetString().StartsWith("Ngày", StringComparison.Ordinal))
            .ToList();
        dayRows.Should().HaveCount(1, "a single day was exported");
        dayRows[0].Cell(1).GetString().Should().BeEmpty("cột A stays the STT column");
        dayRows[0].Cell(5).DataType.Should().Be(XLDataType.Number, "tổng xe must stay summable");
        dayRows[0].Cell(5).GetValue<int>().Should().Be(4, "four vehicles were entered that day");

        var parentRows = DataRows(ws).Count(r => !string.IsNullOrEmpty(r.Cell(DeclarationNumberColumn).GetString()));
        parentRows.Should().Be(3, "three declarations were created");

        // Skip the daily row: the unit's layout reuses cột E for tổng xe, so scanning that column
        // blindly would count the day's total as if it were a plate.
        var plates = DataRows(ws)
            .Where(r => !r.Cell(4).GetString().StartsWith("Ngày", StringComparison.Ordinal))
            .Select(r => r.Cell(PlateColumn).GetString())
            .Where(s => s.Length > 0)
            .ToList();
        plates.Should().BeEquivalentTo(ExpectedPlates);
    }

    [Fact]
    [Trait("Feature", "F-13")]
    public async Task Template_sample_search_value_is_not_exported()
    {
        // Audit X1: cột P row 1 is the legacy "Tìm xe" box; the template's sample 1358 leaked
        // into every export.
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateDeclarationAsync(client, "31A30020");
        var date = decl.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var ws = await ExportWorksheetAsync(client, date, date);

        ws.Cell(1, 16).GetString().Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "F-13")]
    public async Task Duplicate_declaration_numbers_are_flagged_across_every_exported_row()
    {
        // Audit X1 / business rules cột C: the template's duplicate rule shrank to C1 when its
        // sample rows were deleted, so exported duplicates were never flagged.
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateDeclarationAsync(client, "31A30030");
        await CreateDeclarationAsync(client, "31A30031");
        var date = decl.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var ws = await ExportWorksheetAsync(client, date, date);

        var lastRow = ws.LastRowUsed()!.RowNumber();
        var rule = ws.ConditionalFormats.Should().ContainSingle().Subject;
        rule.ConditionalFormatType.Should().Be(XLConditionalFormatType.IsDuplicate);
        rule.Range.RangeAddress.ToString().Should().Be($"C2:C{lastRow}");
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
    public async Task Should_write_times_as_real_date_values_not_text()
    {
        // Cột I / cột K are date values in the unit's own file (Excel serial + display format).
        // As text the column sorts alphabetically and cannot be subtracted, which is how an
        // operator would work out how long a vehicle sat in the yard.
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var decl = await CreateDeclarationAsync(client, "31A30030");
        var detail = await client.GetFromJsonAsync<DeclarationDetailResponse>(
            $"/api/declarations/{decl.Id}",
            TestJson.Options);
        var exit = await client.PostAsync($"/api/vehicles/{detail!.Vehicles[0].Id}/exit", null);
        exit.StatusCode.Should().Be(HttpStatusCode.OK);

        var date = decl.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var ws = await ExportWorksheetAsync(client, date, date);
        var row = DataRows(ws).Single(r => r.Cell(PlateColumn).GetString() == "31A30030");

        row.Cell(EntryTimeColumn).DataType.Should().Be(XLDataType.DateTime);
        row.Cell(ExitTimeColumn).DataType.Should().Be(XLDataType.DateTime);
        row.Cell(EntryTimeColumn).Style.DateFormat.Format.Should().Be("d/M/yyyy H:mm");
        row.Cell(ExitTimeColumn).GetDateTime()
            .Should().BeOnOrAfter(row.Cell(EntryTimeColumn).GetDateTime());
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

        (await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/cancel",
            new CancelDeclarationRequest("Hủy để kiểm thử"),
            TestJson.Options)).EnsureSuccessStatusCode();

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

    [Fact]
    [Trait("Feature", "F-13")]
    [Trait("Issue", "161")]
    public async Task Formula_like_user_text_is_exported_as_inert_text()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        client.SetBearer((await client.LoginAsAdminAsync()).AccessToken);
        const string company = "=HYPERLINK(\"http://evil.example\",\"x\")";
        const string goods = "+cmd|' /C calc'!A0";
        const string note = "@SUM(1+1)";
        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()], SeedIds.ProcessorId, company, goods, SeedIds.YardId,
                new VehicleAddRequest("31A30009", null, note)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var decl = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;
        var date = decl.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var ws = await ExportWorksheetAsync(client, date, date);

        // ClosedXML writes a string assigned through .Value as a text cell; only FormulaA1 makes a
        // formula. Excel therefore shows these literally and never evaluates them.
        var row = DataRows(ws).Single(r => r.Cell(PlateColumn).GetString() == "31A30009");
        foreach (var (column, expected) in new[] { (4, company), (7, goods), (15, note) })
        {
            var cell = row.Cell(column);
            cell.HasFormula.Should().BeFalse();
            cell.DataType.Should().Be(XLDataType.Text);
            cell.GetString().Should().Be(expected);
        }
    }

    private static async Task<DeclarationDto> CreateDeclarationAsync(HttpClient client, string plate)
    {
        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()],
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
