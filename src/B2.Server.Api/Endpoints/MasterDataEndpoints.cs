using B2.Application.MasterData;
using B2.Server.Api.Auth;
using B2.Shared.Contracts.MasterData;
using FluentValidation;

namespace B2.Server.Api.Endpoints;

public static class MasterDataEndpoints
{
    public static void MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var processors = app.MapGroup("/api/master/processors");

        processors.MapGet(
            "",
            async (
                bool? includeInactive,
                int? page,
                int? pageSize,
                MasterDataCommandHandler handler,
                CancellationToken ct) =>
            {
                var p = page is >= 1 ? page.Value : 1;
                var s = pageSize is >= 1 and <= 500 ? pageSize.Value : 200;
                var (items, total) = await handler.ListProcessorsAsync(includeInactive ?? false, p, s, ct);
                return Results.Ok(new ProcessorListResponse(p, s, total, items));
            })
            .RequireAuthorization(B2Policies.MasterRead);

        processors.MapPost(
                "",
                async (
                    HttpContext http,
                    ProcessorWriteRequest request,
                    MasterDataCommandHandler handler,
                    IValidator<ProcessorWriteRequest> validator,
                    CancellationToken ct) =>
                {
                    var actor = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                    await validator.ValidateAndThrowAsync(request, ct);
                    var dto = await handler.CreateProcessorAsync(actor, request, ct);
                    return Results.Created($"/api/master/processors/{dto.Id}", dto);
                })
            .RequireAuthorization(B2Policies.AdminOnly);

        processors.MapPut(
                "/{id:guid}",
                async (
                    Guid id,
                    HttpContext http,
                    ProcessorWriteRequest request,
                    MasterDataCommandHandler handler,
                    IValidator<ProcessorWriteRequest> validator,
                    CancellationToken ct) =>
                {
                    var actor = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                    await validator.ValidateAndThrowAsync(request, ct);
                    var dto = await handler.UpdateProcessorAsync(actor, id, request, ct);
                    return Results.Ok(dto);
                })
            .RequireAuthorization(B2Policies.AdminOnly);

        processors.MapDelete(
                "/{id:guid}",
                async (Guid id, HttpContext http, MasterDataCommandHandler handler, CancellationToken ct) =>
                {
                    var actor = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                    await handler.DeactivateProcessorAsync(actor, id, ct);
                    return Results.NoContent();
                })
            .RequireAuthorization(B2Policies.AdminOnly);

        var yards = app.MapGroup("/api/master/yards");

        yards.MapGet(
            "",
            async (
                bool? includeInactive,
                int? page,
                int? pageSize,
                MasterDataCommandHandler handler,
                CancellationToken ct) =>
            {
                var p = page is >= 1 ? page.Value : 1;
                var s = pageSize is >= 1 and <= 500 ? pageSize.Value : 200;
                var (items, total) = await handler.ListYardsAsync(includeInactive ?? false, p, s, ct);
                return Results.Ok(new YardListResponse(p, s, total, items));
            })
            .RequireAuthorization(B2Policies.MasterRead);

        yards.MapPost(
                "",
                async (
                    HttpContext http,
                    YardWriteRequest request,
                    MasterDataCommandHandler handler,
                    IValidator<YardWriteRequest> validator,
                    CancellationToken ct) =>
                {
                    var actor = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                    await validator.ValidateAndThrowAsync(request, ct);
                    var dto = await handler.CreateYardAsync(actor, request, ct);
                    return Results.Created($"/api/master/yards/{dto.Id}", dto);
                })
            .RequireAuthorization(B2Policies.AdminOnly);

        yards.MapPut(
                "/{id:guid}",
                async (
                    Guid id,
                    HttpContext http,
                    YardWriteRequest request,
                    MasterDataCommandHandler handler,
                    IValidator<YardWriteRequest> validator,
                    CancellationToken ct) =>
                {
                    var actor = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                    await validator.ValidateAndThrowAsync(request, ct);
                    var dto = await handler.UpdateYardAsync(actor, id, request, ct);
                    return Results.Ok(dto);
                })
            .RequireAuthorization(B2Policies.AdminOnly);

        yards.MapDelete(
                "/{id:guid}",
                async (Guid id, HttpContext http, MasterDataCommandHandler handler, CancellationToken ct) =>
                {
                    var actor = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                    await handler.DeactivateYardAsync(actor, id, ct);
                    return Results.NoContent();
                })
            .RequireAuthorization(B2Policies.AdminOnly);
    }
}
