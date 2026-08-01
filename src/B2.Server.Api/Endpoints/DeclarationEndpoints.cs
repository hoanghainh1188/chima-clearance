using System.Globalization;
using B2.Application.Declarations;
using B2.Application.Declarations.Cancel;
using B2.Application.Declarations.ConfirmKvgs;
using B2.Application.Declarations.List;
using B2.Server.Api.Auth;
using B2.Shared.Contracts.Declarations;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace B2.Server.Api.Endpoints;

public static class DeclarationEndpoints
{
    public static void MapDeclarationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/declarations",
                async (
                    HttpRequest request,
                    ListDeclarationsQueryHandler handler,
                    IValidator<ListDeclarationsQuery> listValidator,
                    CancellationToken ct) =>
                {
                    if (!TryParseListDeclarationsQuery(request.Query, out var q, out var parseError))
                    {
                        return Results.BadRequest(new { code = "INVALID_QUERY", detail = parseError });
                    }

                    await listValidator.ValidateAndThrowAsync(q, ct);
                    var result = await handler.HandleAsync(q, ct);
                    return Results.Ok(result);
                })
            .RequireAuthorization(B2Policies.AnyAuthenticated);

        app.MapGet(
                "/api/declarations/{id:guid}",
                async (Guid id, GetDeclarationDetailQueryHandler handler, CancellationToken ct) =>
                {
                    var detail = await handler.HandleAsync(id, ct);
                    return detail is null ? Results.NotFound() : Results.Ok(detail);
                })
            .RequireAuthorization(B2Policies.AnyAuthenticated);

        var gate = app.MapGroup("/api/declarations").RequireAuthorization(B2Policies.GateOperator);
        gate.MapPost(
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

        gate.MapPost(
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

        app.MapPost(
                "/api/declarations/{id:guid}/kvgs-confirm",
                async (
                    Guid id,
                    HttpContext http,
                    ConfirmKvgsCommandHandler handler,
                    IValidator<ConfirmKvgsCommand> validator,
                    CancellationToken ct) =>
                {
                    var uid = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                    var cmd = new ConfirmKvgsCommand(id);
                    await validator.ValidateAndThrowAsync(cmd, ct);
                    await handler.HandleAsync(cmd, uid, ct);
                    return Results.NoContent();
                })
            .RequireAuthorization(B2Policies.GateStaff);

        app.MapPost(
                "/api/declarations/{id:guid}/cancel",
                async (
                    Guid id,
                    HttpContext http,
                    CancelDeclarationRequest request,
                    CancelDeclarationCommandHandler handler,
                    IValidator<CancelDeclarationCommand> validator,
                    CancellationToken ct) =>
                {
                    var uid = http.User.GetB2UserId() ?? throw new InvalidOperationException("Missing user id.");
                    var role = http.User.FindFirst("role")?.Value;
                    var cmd = new CancelDeclarationCommand(id, request.Reason);
                    await validator.ValidateAndThrowAsync(cmd, ct);
                    await handler.HandleAsync(cmd, role, uid, ct);
                    return Results.NoContent();
                })
            .RequireAuthorization(B2Policies.AdminOnly);
    }

    private static bool TryParseListDeclarationsQuery(
        IQueryCollection query,
        out ListDeclarationsQuery queryModel,
        out string? parseError)
    {
        parseError = null;
        queryModel = default!;

        if (!TryOptionalDateOnly(query, "fromDate", out var fromDate, out parseError))
        {
            return false;
        }

        if (!TryOptionalDateOnly(query, "toDate", out var toDate, out parseError))
        {
            return false;
        }

        if (!TryOptionalGuid(query, "officerUserId", out var officerUserId, out parseError))
        {
            return false;
        }

        if (!TryOptionalDateTimeOffset(query, "since", out var sinceUtc, out parseError))
        {
            return false;
        }

        var status = NullIfWhitespace(query["status"].ToString());
        var plate = NullIfWhitespace(query["plate"].ToString());
        var processorIds = ParseGuidList(NullIfWhitespace(query["processorIds"].ToString()));
        var yardIds = ParseGuidList(NullIfWhitespace(query["yardIds"].ToString()));
        var includeCancelled = ReadBool(query, "includeCancelled");
        var page = ReadPositiveIntOrDefault(query, "page", 1);
        var pageSize = ReadPositiveIntOrDefault(query, "pageSize", 50);
        queryModel = new ListDeclarationsQuery(
            fromDate,
            toDate,
            status,
            plate,
            processorIds,
            yardIds,
            officerUserId,
            sinceUtc,
            includeCancelled,
            page,
            pageSize);
        return true;
    }

    private static string? NullIfWhitespace(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    private static bool TryOptionalDateOnly(
        IQueryCollection query,
        string key,
        out DateOnly? value,
        out string? error)
    {
        error = null;
        value = null;
        if (!query.TryGetValue(key, out var sv) || string.IsNullOrWhiteSpace(sv))
        {
            return true;
        }

        if (DateOnly.TryParse(sv.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            value = d;
            return true;
        }

        error = $"'{key}' is not a valid date.";
        return false;
    }

    private static bool TryOptionalDateTimeOffset(
        IQueryCollection query,
        string key,
        out DateTimeOffset? value,
        out string? error)
    {
        error = null;
        value = null;
        if (!query.TryGetValue(key, out var sv) || string.IsNullOrWhiteSpace(sv))
        {
            return true;
        }

        if (DateTimeOffset.TryParse(
                sv.ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dto))
        {
            value = dto;
            return true;
        }

        error = $"'{key}' is not a valid timestamp.";
        return false;
    }

    private static bool TryOptionalGuid(
        IQueryCollection query,
        string key,
        out Guid? value,
        out string? error)
    {
        error = null;
        value = null;
        if (!query.TryGetValue(key, out var sv) || string.IsNullOrWhiteSpace(sv))
        {
            return true;
        }

        if (Guid.TryParse(sv.ToString(), out var g))
        {
            value = g;
            return true;
        }

        error = $"'{key}' is not a valid GUID.";
        return false;
    }

    private static bool ReadBool(IQueryCollection query, string key)
    {
        if (!query.TryGetValue(key, out var sv) || string.IsNullOrWhiteSpace(sv))
        {
            return false;
        }

        return bool.TryParse(sv.ToString(), out var b) && b;
    }

    private static int ReadPositiveIntOrDefault(IQueryCollection query, string key, int fallback)
    {
        if (!query.TryGetValue(key, out var sv) ||
            !int.TryParse(sv.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ||
            i <= 0)
        {
            return fallback;
        }

        return i;
    }

    private static List<Guid>? ParseGuidList(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return null;
        }

        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<Guid>(parts.Length);
        foreach (var p in parts)
        {
            if (Guid.TryParse(p, out var g))
            {
                list.Add(g);
            }
        }

        return list.Count == 0 ? null : list;
    }
}
