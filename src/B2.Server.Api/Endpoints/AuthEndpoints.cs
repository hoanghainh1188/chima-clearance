using B2.Application.Abstractions;
using B2.Application.Auth;
using B2.Domain.Audit;
using B2.Domain.Users;
using B2.Server.Api.Auth;
using B2.Shared.Contracts.Auth;
using FluentValidation;

namespace B2.Server.Api.Endpoints;

public static class AuthEndpoints
{
    private const string LogoutAuditJson = "{\"refreshTokensRevoked\":true}";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapPost(
                "/login",
                async (
                    LoginRequest request,
                    LoginCommandHandler handler,
                    IValidator<LoginRequest> validator,
                    CancellationToken ct) =>
                {
                    await validator.ValidateAndThrowAsync(request, ct);
                    var response = await handler.HandleAsync(request, ct);
                    return Results.Ok(response);
                })
            .AllowAnonymous();

        // FR-003 exception: refresh only rotates the caller's own token and runs on a client
        // timer, so it is deliberately not audited (login / logout / change-password are).
        g.MapPost(
                "/refresh",
                async (
                    RefreshRequest request,
                    RefreshCommandHandler handler,
                    IValidator<RefreshRequest> validator,
                    CancellationToken ct) =>
                {
                    await validator.ValidateAndThrowAsync(request, ct);
                    var response = await handler.HandleAsync(request, ct);
                    return Results.Ok(response);
                })
            .AllowAnonymous();

        g.MapPost(
                "/logout",
                async (HttpContext http, IRefreshTokenRepository refreshTokens, IAuditContext audit, CancellationToken ct) =>
                {
                    var uid = http.User.GetB2UserId();
                    if (uid is null)
                    {
                        return Results.Unauthorized();
                    }

                    await refreshTokens.RevokeAllForUserAsync(uid.Value, ct);
                    // FR-003: revoking every refresh token is a write, so it is audited like login.
                    audit.Set(new AuditEnvelope(
                        uid.Value,
                        AuditAction.Update,
                        nameof(User),
                        uid.Value,
                        null,
                        LogoutAuditJson,
                        null));
                    return Results.NoContent();
                })
            .RequireAuthorization();

        g.MapPost(
                "/change-password",
                async (
                    HttpContext http,
                    ChangePasswordRequest request,
                    ChangePasswordCommandHandler handler,
                    IValidator<ChangePasswordRequest> validator,
                    CancellationToken ct) =>
                {
                    var uid = http.User.GetB2UserId();
                    if (uid is null)
                    {
                        return Results.Unauthorized();
                    }

                    await validator.ValidateAndThrowAsync(request, ct);
                    await handler.HandleAsync(uid.Value, request, ct);
                    return Results.NoContent();
                })
            .RequireAuthorization();
    }
}
