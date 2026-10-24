using System.Net;
using System.Text.Json;
using B2.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace B2.Server.Api.Middleware;

public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException vex)
        {
            await WriteValidationProblemAsync(context, vex);
        }
        catch (DomainException dex)
        {
            await WriteDomainAsync(context, dex);
        }
        catch (DbUpdateException uex) when (UniqueConstraintViolation.TryTranslate(uex, out var dex))
        {
            logger.LogWarning(uex, "Unique constraint violation translated to {Code}", dex.Code);
            await WriteDomainAsync(context, dex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteUnhandledAsync(context, ex);
        }
    }

    private static async Task WriteDomainAsync(HttpContext context, DomainException dex)
    {
        var status = dex.HttpStatus ?? 400;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString() ?? "";
        var payload = new Dictionary<string, object?>
        {
            ["type"] = "https://b2/errors/domain",
            ["title"] = dex.Code,
            ["status"] = status,
            ["detail"] = dex.Message,
            ["code"] = dex.Code,
            ["correlationId"] = correlationId,
        };

        if (dex.Extensions is not null)
        {
            foreach (var kv in dex.Extensions)
            {
                payload[kv.Key] = kv.Value;
            }
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private static async Task WriteValidationProblemAsync(HttpContext context, ValidationException vex)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        context.Response.ContentType = "application/problem+json";
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString() ?? "";
        var errors = vex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = "https://b2/errors/validation",
            title = "Validation failed",
            status = 400,
            code = "VALIDATION_ERROR",
            errors,
            correlationId,
        }));
    }

    private static async Task WriteUnhandledAsync(HttpContext context, Exception ex)
    {
        // Detail intentionally omits ex.Message — the full exception is logged server-side
        // by InvokeAsync. Surfacing ex.Message risks leaking SQL fragments, file paths,
        // or other internal state to clients.
        _ = ex;
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/problem+json";
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString() ?? "";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            type = "https://b2/errors/unhandled",
            title = "Server error",
            status = 500,
            detail = "An unexpected error occurred. Reference the correlationId for support.",
            code = "INTERNAL_ERROR",
            correlationId,
        }));
    }
}
