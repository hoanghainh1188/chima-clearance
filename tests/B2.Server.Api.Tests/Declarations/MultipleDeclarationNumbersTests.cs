using System.Net;
using System.Net.Http.Json;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;

namespace B2.Server.Api.Tests.Declarations;

/// <summary>
/// Cột C may carry several 12-digit numbers on one parent row ("có nhiều dãy số thì mỗi dãy số sẽ
/// tách thành từng dòng riêng biệt trong cùng 1 ô"), and the cell stays editable at the entry gate
/// — confirmed with the unit 2026-08-01.
/// </summary>
public sealed class MultipleDeclarationNumbersTests
{
    private static DeclarationCreateRequest Request(params string[] numbers) =>
        new(numbers, SeedIds.ProcessorId, "Công ty ABC", "Hàng hóa", SeedIds.YardId,
            new VehicleAddRequest("31A" + Random.Shared.Next(10000, 99999), null, null));

    private static async Task<HttpClient> AdminClientAsync(B2ApiFactory factory)
    {
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        return client;
    }

    [Fact]
    [Trait("Requirement", "INV-1")]
    public async Task A_row_can_be_created_with_several_numbers()
    {
        await using var factory = new B2ApiFactory();
        var client = await AdminClientAsync(factory);
        var a = TestDeclarationNumbers.Next();
        var b = TestDeclarationNumbers.Next();

        var create = await client.PostAsJsonAsync("/api/declarations", Request(a, b), TestJson.Options);

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;
        dto.DeclarationNumbers.Should().Equal(a, b);
        dto.DeclarationNumber.Should().Be(a, "ordinal 0 stays the primary shown wherever one value is needed");

        var detail = await client.GetFromJsonAsync<DeclarationDetailResponse>(
            $"/api/declarations/{dto.Id}", TestJson.Options);
        detail!.Declaration.DeclarationNumbers.Should().Equal(a, b);
    }

    [Fact]
    [Trait("Requirement", "INV-1")]
    public async Task A_number_can_be_added_after_the_row_exists()
    {
        await using var factory = new B2ApiFactory();
        var client = await AdminClientAsync(factory);
        var a = TestDeclarationNumbers.Next();
        var b = TestDeclarationNumbers.Next();
        var create = await client.PostAsJsonAsync("/api/declarations", Request(a), TestJson.Options);
        var dto = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;

        var added = await client.PostAsJsonAsync(
            $"/api/declarations/{dto.Id}/numbers",
            new AddDeclarationNumberRequest(b),
            TestJson.Options);

        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
        (await added.Content.ReadFromJsonAsync<List<string>>(TestJson.Options)).Should().Equal(a, b);
    }

    [Fact]
    [Trait("Requirement", "INV-1")]
    public async Task Removing_a_number_reorders_and_keeps_the_primary_in_step()
    {
        await using var factory = new B2ApiFactory();
        var client = await AdminClientAsync(factory);
        var a = TestDeclarationNumbers.Next();
        var b = TestDeclarationNumbers.Next();
        var create = await client.PostAsJsonAsync("/api/declarations", Request(a, b), TestJson.Options);
        var dto = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;

        var removed = await client.DeleteAsync($"/api/declarations/{dto.Id}/numbers/{a}");

        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await removed.Content.ReadFromJsonAsync<List<string>>(TestJson.Options)).Should().Equal(b);

        var detail = await client.GetFromJsonAsync<DeclarationDetailResponse>(
            $"/api/declarations/{dto.Id}", TestJson.Options);
        detail!.Declaration.DeclarationNumber.Should().Be(b, "the cached primary follows ordinal 0");
    }

    [Fact]
    [Trait("Requirement", "INV-1")]
    public async Task The_last_number_cannot_be_removed()
    {
        await using var factory = new B2ApiFactory();
        var client = await AdminClientAsync(factory);
        var a = TestDeclarationNumbers.Next();
        var create = await client.PostAsJsonAsync("/api/declarations", Request(a), TestJson.Options);
        var dto = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;

        var removed = await client.DeleteAsync($"/api/declarations/{dto.Id}/numbers/{a}");

        removed.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Requirement", "INV-1")]
    public async Task A_number_already_used_by_another_live_row_is_rejected()
    {
        await using var factory = new B2ApiFactory();
        var client = await AdminClientAsync(factory);
        var a = TestDeclarationNumbers.Next();
        var b = TestDeclarationNumbers.Next();
        await client.PostAsJsonAsync("/api/declarations", Request(a), TestJson.Options);
        var second = await client.PostAsJsonAsync("/api/declarations", Request(b), TestJson.Options);
        var dto = (await second.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;

        var added = await client.PostAsJsonAsync(
            $"/api/declarations/{dto.Id}/numbers",
            new AddDeclarationNumberRequest(a),
            TestJson.Options);

        added.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Requirement", "INV-1")]
    public async Task Every_number_of_a_cancelled_row_becomes_re_enterable()
    {
        await using var factory = new B2ApiFactory();
        var client = await AdminClientAsync(factory);
        var a = TestDeclarationNumbers.Next();
        var b = TestDeclarationNumbers.Next();
        var create = await client.PostAsJsonAsync("/api/declarations", Request(a, b), TestJson.Options);
        var dto = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;

        var cancel = await client.PostAsJsonAsync(
            $"/api/declarations/{dto.Id}/cancel",
            new CancelDeclarationRequest("Nhập nhầm"),
            TestJson.Options);
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Both numbers — not just the primary — are freed for the corrected entry (FR-022).
        var reentered = await client.PostAsJsonAsync("/api/declarations", Request(b, a), TestJson.Options);

        reentered.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Requirement", "INV-1")]
    public async Task The_same_number_twice_on_one_row_is_rejected()
    {
        await using var factory = new B2ApiFactory();
        var client = await AdminClientAsync(factory);
        var a = TestDeclarationNumbers.Next();

        var create = await client.PostAsJsonAsync("/api/declarations", Request(a, a), TestJson.Options);

        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Requirement", "INV-1")]
    public async Task Creating_without_any_number_is_rejected()
    {
        await using var factory = new B2ApiFactory();
        var client = await AdminClientAsync(factory);

        var create = await client.PostAsJsonAsync("/api/declarations", Request(), TestJson.Options);

        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Requirement", "FR-022")]
    public async Task Numbers_cannot_be_edited_on_a_cancelled_row()
    {
        await using var factory = new B2ApiFactory();
        var client = await AdminClientAsync(factory);
        var create = await client.PostAsJsonAsync(
            "/api/declarations", Request(TestDeclarationNumbers.Next()), TestJson.Options);
        var dto = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;
        await client.PostAsJsonAsync(
            $"/api/declarations/{dto.Id}/cancel",
            new CancelDeclarationRequest("Nhập nhầm"),
            TestJson.Options);

        var added = await client.PostAsJsonAsync(
            $"/api/declarations/{dto.Id}/numbers",
            new AddDeclarationNumberRequest(TestDeclarationNumbers.Next()),
            TestJson.Options);

        added.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
