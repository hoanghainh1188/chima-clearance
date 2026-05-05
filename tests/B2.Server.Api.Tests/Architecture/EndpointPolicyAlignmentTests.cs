using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using B2.Server.Api.Tests.Support;

namespace B2.Server.Api.Tests.Architecture;

public sealed class EndpointPolicyAlignmentTests
{
    [Fact]
    public async Task Should_all_mapped_api_endpoints_have_authorization_or_explicit_anonymous()
    {
        await using var factory = new B2ApiFactory();
        var dataSources = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
        var apiEndpoints = dataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .ToList();

        apiEndpoints.Should().NotBeEmpty();
        foreach (var ep in apiEndpoints)
        {
            var hasAuthorize = ep.Metadata.GetMetadata<IAuthorizeData>() is not null;
            var hasAnonymous = ep.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            (hasAuthorize || hasAnonymous).Should().BeTrue($"endpoint {ep.RoutePattern.RawText} must declare auth intent");
        }
    }
}
