using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using B2.Server.Api.Auth;
using B2.Server.Api.Tests.Support;

namespace B2.Server.Api.Tests.Architecture;

public sealed class EndpointPolicyAlignmentTests
{
    // Markers for the two non-policy authorization intents.
    private const string AllowAnonymous = "<anonymous>";
    private const string AuthenticatedNoPolicy = "<authenticated>";

    /// <summary>
    /// FR-027 role × endpoint matrix — the single source of truth. Keyed by
    /// "METHOD rawRoute". Every mapped /api/ endpoint MUST appear here, so a new
    /// endpoint cannot ship without a deliberate authorization decision recorded.
    /// </summary>
    private static readonly Dictionary<string, string> Fr027Matrix = new(StringComparer.Ordinal)
    {
        // Auth
        ["POST /api/auth/login"] = AllowAnonymous,
        ["POST /api/auth/refresh"] = AllowAnonymous,
        ["POST /api/auth/logout"] = AuthenticatedNoPolicy,
        ["POST /api/auth/change-password"] = AuthenticatedNoPolicy,

        // Health probe (minimal info, anonymous by design)
        ["GET /api/admin/health"] = AllowAnonymous,

        // Audit log viewer — Admin only (F-16)
        ["GET /api/admin/audit"] = B2Policies.AdminOnly,

        // Health & logs — Admin only (F-18); the anonymous /health probe stays above
        ["GET /api/admin/logs"] = B2Policies.AdminOnly,
        ["GET /api/admin/health/detail"] = B2Policies.AdminOnly,

        // Users — Admin only
        ["GET /api/users"] = B2Policies.AdminOnly,
        ["POST /api/users"] = B2Policies.AdminOnly,
        ["PUT /api/users/{id:guid}"] = B2Policies.AdminOnly,
        ["POST /api/users/{id:guid}/reset-password"] = B2Policies.AdminOnly,

        // Master data — read for operators, writes Admin only
        ["GET /api/master/processors"] = B2Policies.MasterRead,
        ["POST /api/master/processors"] = B2Policies.AdminOnly,
        ["PUT /api/master/processors/{id:guid}"] = B2Policies.AdminOnly,
        ["DELETE /api/master/processors/{id:guid}"] = B2Policies.AdminOnly,
        ["GET /api/master/yards"] = B2Policies.MasterRead,
        ["POST /api/master/yards"] = B2Policies.AdminOnly,
        ["PUT /api/master/yards/{id:guid}"] = B2Policies.AdminOnly,
        ["DELETE /api/master/yards/{id:guid}"] = B2Policies.AdminOnly,

        // Declarations
        ["GET /api/declarations"] = B2Policies.AnyAuthenticated,
        ["GET /api/declarations/{id:guid}"] = B2Policies.AnyAuthenticated,
        ["POST /api/declarations"] = B2Policies.GateOperator,
        ["POST /api/declarations/{id:guid}/vehicles"] = B2Policies.GateOperator,
        ["POST /api/declarations/{id:guid}/kvgs-confirm"] = B2Policies.SupervisorOrAbove,
        ["POST /api/declarations/{id:guid}/cancel"] = B2Policies.AdminOnly,

        // Vehicles
        ["GET /api/vehicles/search"] = B2Policies.AnyAuthenticated,
        ["POST /api/vehicles/{id:guid}/exit"] = B2Policies.GateOperator,
        ["POST /api/vehicles/{id:guid}/un-exit"] = B2Policies.AdminOnly,

        // Daily headers
        ["GET /api/daily-headers"] = B2Policies.AnyAuthenticated,
        ["POST /api/daily-headers/{date}/recompute"] = B2Policies.AdminOnly,
        ["PATCH /api/daily-headers/{date}/officer"] = B2Policies.AnyAuthenticated,

        // Reports — Supervisor or above
        ["GET /api/reports/by-processor"] = B2Policies.SupervisorOrAbove,
        ["GET /api/reports/by-yard"] = B2Policies.SupervisorOrAbove,
        ["GET /api/reports/by-hour"] = B2Policies.SupervisorOrAbove,
        ["GET /api/reports/by-officer"] = B2Policies.SupervisorOrAbove,
        ["GET /api/reports/pending"] = B2Policies.SupervisorOrAbove,
        ["GET /api/reports/excel"] = B2Policies.SupervisorOrAbove,
    };

    [Fact]
    public async Task Should_all_mapped_api_endpoints_have_authorization_or_explicit_anonymous()
    {
        await using var factory = new B2ApiFactory();
        foreach (var ep in ApiEndpoints(factory))
        {
            var hasAuthorize = ep.Metadata.GetMetadata<IAuthorizeData>() is not null;
            var hasAnonymous = ep.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            (hasAuthorize || hasAnonymous).Should().BeTrue($"endpoint {ep.RoutePattern.RawText} must declare auth intent");
        }
    }

    [Fact]
    public async Task Should_each_endpoint_bind_the_specific_fr027_policy()
    {
        await using var factory = new B2ApiFactory();
        var endpoints = ApiEndpoints(factory);

        endpoints.Should().NotBeEmpty();
        foreach (var ep in endpoints)
        {
            var key = $"{HttpMethodOf(ep)} {NormalizeRoute(ep.RoutePattern.RawText)}";
            Fr027Matrix.Should().ContainKey(
                key,
                $"endpoint '{key}' must be classified in the FR-027 matrix before it can ship");

            var expected = Fr027Matrix[key];
            var anonymous = ep.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var policies = ep.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToList();

            switch (expected)
            {
                case AllowAnonymous:
                    anonymous.Should().BeTrue($"{key} is anonymous in FR-027");
                    break;
                case AuthenticatedNoPolicy:
                    anonymous.Should().BeFalse($"{key} requires authentication in FR-027");
                    policies.Should().NotBeEmpty($"{key} must require authorization");
                    policies.Should().OnlyContain(p => string.IsNullOrEmpty(p), $"{key} requires only authentication, no named policy");
                    break;
                default:
                    anonymous.Should().BeFalse($"{key} is not anonymous in FR-027");
                    policies.Should().Contain(
                        expected,
                        $"{key} must bind the specific FR-027 policy '{expected}', not merely any auth attribute");
                    break;
            }
        }
    }

    private static List<RouteEndpoint> ApiEndpoints(B2ApiFactory factory)
    {
        var dataSources = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
        return dataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .ToList();
    }

    private static string HttpMethodOf(RouteEndpoint ep)
    {
        var methods = ep.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        return methods is { Count: > 0 } ? methods[0] : "ANY";
    }

    // Grouped endpoints mapped with "" or "/" surface a trailing slash in RawText
    // (e.g. "/api/users/"); normalize it away so the matrix keys stay canonical.
    private static string NormalizeRoute(string? rawText)
    {
        var route = rawText ?? string.Empty;
        return route.Length > 1 ? route.TrimEnd('/') : route;
    }
}
