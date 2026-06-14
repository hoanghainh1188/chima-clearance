using System.Globalization;
using System.Net.Http.Json;
using B2.Infrastructure.Archive;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;

namespace B2.Server.Api.Tests.Admin;

/// <summary>
/// FR-021 / INV-12 — verifies the archive writer refuses under-populated Excel files
/// so the annual job cannot delete live rows without a complete export.
/// </summary>
public sealed class AnnualArchiveVerifyTests
{
    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_pass_verification_when_excel_contains_expected_declaration_count()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var first = await CreateDeclarationAsync(client, "31A90001");
        await CreateDeclarationAsync(client, "31A90002");
        await CreateDeclarationAsync(client, "31A90003");

        var date = first.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var export = await client.GetAsync($"/api/reports/excel?from={date}&to={date}");
        export.EnsureSuccessStatusCode();
        var bytes = await export.Content.ReadAsByteArrayAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "b2-archive-verify-" + Guid.NewGuid().ToString("N"));
        var writer = new FileSystemArchiveWriter(tempDir);
        try
        {
            var filePath = await writer.WriteExcelAsync(2022, bytes, CancellationToken.None);

            writer.VerifyExcel(filePath, 3).Should().BeTrue();
            writer.VerifyExcel(filePath, 4).Should().BeFalse(
                "under-populated archives must fail verification before live data is deleted");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_pass_verification_when_year_has_zero_declarations()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var export = await client.GetAsync("/api/reports/excel?from=2099-01-01&to=2099-01-01");
        export.EnsureSuccessStatusCode();
        var bytes = await export.Content.ReadAsByteArrayAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "b2-archive-verify-" + Guid.NewGuid().ToString("N"));
        var writer = new FileSystemArchiveWriter(tempDir);
        try
        {
            var filePath = await writer.WriteExcelAsync(2099, bytes, CancellationToken.None);
            writer.VerifyExcel(filePath, 0).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
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
}
