using B2.Application.Declarations;
using B2.Server.Api.Auth;
using B2.Shared.Contracts.Declarations;
using FluentValidation;

namespace B2.Server.Api.Endpoints;

public static class DeclarationEndpoints
{
    public static void MapDeclarationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/declarations").RequireAuthorization(B2Policies.GateOperator);

        g.MapPost(
            "",
            async (
                HttpContext http,
                DeclarationCreateRequest request,
                CreateDeclarationCommandHandler handler,
                IValidator<DeclarationCreateRequest> validator,
                CancellationToken ct) =>
            {
                var uid = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                await validator.ValidateAndThrowAsync(request, ct);
                var dto = await handler.HandleAsync(request, uid, ct);
                return Results.Created($"/api/declarations/{dto.Id}", dto);
            });

        g.MapPost(
            "/{id:guid}/vehicles",
            async (
                Guid id,
                HttpContext http,
                VehicleAddRequest request,
                AddVehicleCommandHandler handler,
                IValidator<VehicleAddRequest> validator,
                CancellationToken ct) =>
            {
                var uid = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                await validator.ValidateAndThrowAsync(request, ct);
                await handler.HandleAsync(id, request, uid, ct);
                return Results.NoContent();
            });
    }
}
