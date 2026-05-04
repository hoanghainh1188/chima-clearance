using B2.Application.Declarations.Exit;
using B2.Application.Vehicles;
using B2.Application.Vehicles.Validators;
using B2.Server.Api.Auth;
using B2.Shared.Contracts.Declarations;
using FluentValidation;

namespace B2.Server.Api.Endpoints;

public static class VehiclesEndpoints
{
    public static void MapVehiclesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/vehicles/search",
                async (
                    string suffix,
                    bool? unexited,
                    SearchVehiclesBySuffixQueryHandler handler,
                    IValidator<VehicleSuffixSearchQuery> validator,
                    CancellationToken ct) =>
                {
                    var q = new VehicleSuffixSearchQuery { Suffix = suffix, Unexited = unexited ?? true };
                    await validator.ValidateAndThrowAsync(q, ct);
                    var results = await handler.HandleAsync(q.Suffix, q.Unexited, ct);
                    return Results.Ok(results);
                })
            .RequireAuthorization(B2Policies.AnyAuthenticated);

        app.MapPost(
                "/api/vehicles/{id:guid}/exit",
                async (
                    Guid id,
                    HttpContext http,
                    MarkVehicleExitCommandHandler handler,
                    CancellationToken ct) =>
                {
                    var uid = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                    var dto = await handler.HandleAsync(id, uid, ct);
                    return Results.Ok(dto);
                })
            .RequireAuthorization(B2Policies.GateOperator);

        app.MapPost(
                "/api/vehicles/{id:guid}/un-exit",
                async (
                    Guid id,
                    HttpContext http,
                    UnExitVehicleRequest request,
                    UnExitVehicleCommandHandler handler,
                    IValidator<UnExitVehicleRequest> validator,
                    CancellationToken ct) =>
                {
                    var uid = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                    await validator.ValidateAndThrowAsync(request, ct);
                    await handler.HandleAsync(id, uid, request.Reason, ct);
                    return Results.NoContent();
                })
            .RequireAuthorization(B2Policies.AdminOnly);
    }
}
