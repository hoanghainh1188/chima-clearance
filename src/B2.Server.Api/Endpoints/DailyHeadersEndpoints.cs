using System.Globalization;
using B2.Application.DailyHeaders;
using B2.Server.Api.Auth;
using B2.Shared.Contracts.Declarations;
using FluentValidation;

namespace B2.Server.Api.Endpoints;

public static class DailyHeadersEndpoints
{
    public static void MapDailyHeadersEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/daily-headers").RequireAuthorization(B2Policies.AnyAuthenticated);

        g.MapGet(
            "/",
            async (ListDailyHeadersQueryHandler handler, string from, string to, CancellationToken ct) =>
            {
                if (!DateOnly.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate)
                    || !DateOnly.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate)
                    || fromDate > toDate)
                {
                    return Results.BadRequest(new { code = "INVALID_DATE_RANGE" });
                }

                var items = await handler.HandleAsync(fromDate, toDate, ct);
                return Results.Ok(items);
            });

        g.MapPost(
                "/{date}/recompute",
                async (
                    string date,
                    HttpContext http,
                    RecomputeDailySummaryRequest request,
                    RecomputeDailySummaryCommandHandler handler,
                    IValidator<RecomputeDailySummaryCommand> validator,
                    CancellationToken ct) =>
                {
                    if (!DateOnly.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    {
                        return Results.BadRequest(new { code = "INVALID_DATE" });
                    }

                    var cmd = new RecomputeDailySummaryCommand(parsedDate, request.Reason);
                    var actor = http.User.GetB2UserId();
                    cmd = cmd with { ActorUserId = actor };
                    await validator.ValidateAndThrowAsync(cmd, ct);
                    var total = await handler.HandleAsync(cmd, ct);
                    return Results.Ok(new { date = parsedDate, totalVehicles = total });
                })
            .RequireAuthorization(B2Policies.AdminOnly);

        g.MapPatch(
                "/{date}/officer",
                async (
                    string date,
                    HttpContext http,
                    UpdateOfficerNameRequest request,
                    UpdateOfficerNameCommandHandler handler,
                    IValidator<UpdateOfficerNameCommand> validator,
                    CancellationToken ct) =>
                {
                    if (!DateOnly.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    {
                        return Results.BadRequest(new { code = "INVALID_DATE" });
                    }

                    var cmd = new UpdateOfficerNameCommand(parsedDate, request.Name, http.User.GetB2UserId());
                    await validator.ValidateAndThrowAsync(cmd, ct);
                    await handler.HandleAsync(cmd, ct);
                    return Results.NoContent();
                })
            .RequireAuthorization(B2Policies.AnyAuthenticated);
    }
}
