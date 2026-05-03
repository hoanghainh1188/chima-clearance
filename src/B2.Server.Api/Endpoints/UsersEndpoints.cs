using B2.Application.Users;
using B2.Server.Api.Auth;
using B2.Shared.Contracts.Users;
using FluentValidation;

namespace B2.Server.Api.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/users").RequireAuthorization(B2Policies.AdminOnly);

        g.MapGet(
            "",
            async (int? page, int? pageSize, UserCommandHandler handler, CancellationToken ct) =>
            {
                var p = page is >= 1 ? page.Value : 1;
                var s = pageSize is >= 1 and <= 200 ? pageSize.Value : 50;
                var (items, total) = await handler.ListAsync(p, s, ct);
                return Results.Ok(new UserListResponse(p, s, total, items));
            });

        g.MapPost(
            "",
            async (
                HttpContext http,
                UserCreateRequest request,
                UserCommandHandler handler,
                IValidator<UserCreateRequest> validator,
                CancellationToken ct) =>
            {
                var actor = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                await validator.ValidateAndThrowAsync(request, ct);
                var dto = await handler.CreateAsync(actor, request, ct);
                return Results.Created($"/api/users/{dto.Id}", dto);
            });

        g.MapPut(
            "/{id:guid}",
            async (
                Guid id,
                HttpContext http,
                UserUpdateRequest request,
                UserCommandHandler handler,
                IValidator<UserUpdateRequest> validator,
                CancellationToken ct) =>
            {
                var actor = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                await validator.ValidateAndThrowAsync(request, ct);
                var dto = await handler.UpdateAsync(actor, id, request, ct);
                return Results.Ok(dto);
            });

        g.MapPost(
            "/{id:guid}/reset-password",
            async (
                Guid id,
                HttpContext http,
                PasswordResetRequest request,
                UserCommandHandler handler,
                IValidator<PasswordResetRequest> validator,
                CancellationToken ct) =>
            {
                var actor = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                await validator.ValidateAndThrowAsync(request, ct);
                await handler.ResetPasswordAsync(actor, id, request, ct);
                return Results.NoContent();
            });
    }
}
