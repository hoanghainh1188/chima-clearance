using System.Globalization;
using B2.Application.Export;
using B2.Application.Reports;
using B2.Server.Api.Auth;

namespace B2.Server.Api.Endpoints;

public static class ReportsEndpoints
{
    public static void MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/reports").RequireAuthorization(B2Policies.SupervisorOrAbove);

        g.MapGet(
            "/by-processor",
            async (ByProcessorReportQueryHandler handler, string from, string to, bool? includeCancelled, CancellationToken ct) =>
            {
                if (!TryParseDateRange(from, to, out var fromDate, out var toDate))
                {
                    return Results.BadRequest(new { code = "INVALID_DATE_RANGE" });
                }

                return Results.Ok(await handler.HandleAsync(fromDate, toDate, includeCancelled ?? false, ct));
            });

        g.MapGet(
            "/by-yard",
            async (ByYardReportQueryHandler handler, string from, string to, bool? includeCancelled, CancellationToken ct) =>
            {
                if (!TryParseDateRange(from, to, out var fromDate, out var toDate))
                {
                    return Results.BadRequest(new { code = "INVALID_DATE_RANGE" });
                }

                return Results.Ok(await handler.HandleAsync(fromDate, toDate, includeCancelled ?? false, ct));
            });

        g.MapGet(
            "/by-hour",
            async (ByHourReportQueryHandler handler, string date, int? bucketMinutes, bool? includeCancelled, CancellationToken ct) =>
            {
                if (!DateOnly.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    return Results.BadRequest(new { code = "INVALID_DATE" });
                }

                var bucket = bucketMinutes is > 0 ? bucketMinutes.Value : 60;
                return Results.Ok(await handler.HandleAsync(parsedDate, bucket, includeCancelled ?? false, ct));
            });

        g.MapGet(
            "/by-officer",
            async (ByOfficerReportQueryHandler handler, string from, string to, bool? includeCancelled, CancellationToken ct) =>
            {
                if (!TryParseDateRange(from, to, out var fromDate, out var toDate))
                {
                    return Results.BadRequest(new { code = "INVALID_DATE_RANGE" });
                }

                return Results.Ok(await handler.HandleAsync(fromDate, toDate, includeCancelled ?? false, ct));
            });

        g.MapGet(
            "/pending",
            async (PendingReportQueryHandler handler, DateTimeOffset? asOf, bool? includeCancelled, CancellationToken ct) =>
            {
                var at = asOf ?? DateTimeOffset.UtcNow;
                return Results.Ok(await handler.HandleAsync(at, includeCancelled ?? false, ct));
            });

        // F-13 — Sổ B2 template-based Excel export.
        g.MapGet(
            "/excel",
            async (ExportSoB2QueryHandler handler, string from, string to, CancellationToken ct) =>
            {
                if (!TryParseDateRange(from, to, out var fromDate, out var toDate))
                {
                    return Results.BadRequest(new { code = "INVALID_DATE_RANGE" });
                }

                var bytes = await handler.HandleAsync(fromDate, toDate, ct);
                var fileName = string.Create(
                    CultureInfo.InvariantCulture,
                    $"SoB2_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx");
                return Results.File(
                    bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            });
    }

    private static bool TryParseDateRange(string from, string to, out DateOnly fromDate, out DateOnly toDate)
    {
        var parsedFrom = DateOnly.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.None, out fromDate);
        var parsedTo = DateOnly.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.None, out toDate);
        return parsedFrom && parsedTo && fromDate <= toDate;
    }
}
