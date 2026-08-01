using System.Net;
using System.Net.Http.Json;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;

namespace B2.Server.Api.Tests.Declarations;

/// <summary>
/// INV-1 uniqueness is scoped to declarations that still count. FR-022 cancellation exists to
/// withdraw an erroneous entry, so the number on a cancelled declaration must be re-enterable —
/// otherwise correcting a typo destroys a real declaration number for good and the gate has to
/// work around the system.
/// </summary>
public sealed class DeclarationNumberReuseTests
{
    private static DeclarationCreateRequest Request(string number, string plate, string company) =>
        new(number, SeedIds.ProcessorId, company, "Hàng hóa", SeedIds.YardId, new VehicleAddRequest(plate, null, null));

    [Fact]
    [Trait("Requirement", "INV-1")]
    public async Task Number_of_a_cancelled_declaration_can_be_entered_again()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var number = TestDeclarationNumbers.Next();

        // Mistyped company: entered, then withdrawn through FR-022.
        var first = await client.PostAsJsonAsync("/api/declarations", Request(number, "31A40101", "Sai công ty"), TestJson.Options);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await first.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;

        var cancel = await client.PostAsJsonAsync(
            $"/api/declarations/{created.Id}/cancel",
            new CancelDeclarationRequest("Nhập nhầm tên công ty"),
            TestJson.Options);
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The same real declaration number, entered correctly.
        var second = await client.PostAsJsonAsync("/api/declarations", Request(number, "31A40102", "Đúng công ty"), TestJson.Options);

        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var reentered = (await second.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;
        reentered.DeclarationNumber.Should().Be(number);
        reentered.Id.Should().NotBe(created.Id);
    }

    [Fact]
    [Trait("Requirement", "INV-1")]
    public async Task Number_of_a_live_declaration_is_still_rejected()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var number = TestDeclarationNumbers.Next();

        var first = await client.PostAsJsonAsync("/api/declarations", Request(number, "31A40201", "Công ty ABC"), TestJson.Options);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await client.PostAsJsonAsync("/api/declarations", Request(number, "31A40202", "Công ty ABC"), TestJson.Options);

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
