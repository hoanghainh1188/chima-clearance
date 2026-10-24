using System.Net;
using System.Net.Http.Json;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Middleware;

/// <summary>#141 — one Idempotency-Key names exactly one operation.</summary>
public sealed class IdempotencyKeyTests
{
    [Fact]
    [Trait("Issue", "141")]
    public async Task Reusing_a_key_with_a_different_payload_is_rejected_with_422()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        client.SetBearer((await client.LoginAsAdminAsync()).AccessToken);
        var key = Guid.NewGuid().ToString("N");

        (await PostDeclarationAsync(client, key, TestDeclarationNumbers.Next())).StatusCode.Should().Be(HttpStatusCode.Created);
        var reused = await PostDeclarationAsync(client, key, TestDeclarationNumbers.Next());

        reused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await reused.Content.ReadAsStringAsync()).Should().Contain("IDEMPOTENCY_KEY_REUSED");
        (await CountDeclarationsAsync(factory)).Should().Be(1);
    }

    [Fact]
    [Trait("Issue", "141")]
    public async Task Concurrent_identical_requests_execute_once()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        client.SetBearer((await client.LoginAsAdminAsync()).AccessToken);
        var key = Guid.NewGuid().ToString("N");
        var number = TestDeclarationNumbers.Next();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => PostDeclarationAsync(client, key, number)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options)));
        bodies.Select(b => b!.Id).Distinct().Should().ContainSingle("every retry replays the first response");
        (await CountDeclarationsAsync(factory)).Should().Be(1);
    }

    private static Task<HttpResponseMessage> PostDeclarationAsync(HttpClient client, string key, string number)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/declarations")
        {
            Content = JsonContent.Create(
                new DeclarationCreateRequest(
                    [number],
                    SeedIds.ProcessorId,
                    "C1",
                    "G1",
                    SeedIds.YardId,
                    new VehicleAddRequest("29A12345", null, null)),
                options: TestJson.Options),
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return client.SendAsync(message);
    }

    private static async Task<int> CountDeclarationsAsync(B2ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<B2DbContext>().Declarations.CountAsync();
    }
}
