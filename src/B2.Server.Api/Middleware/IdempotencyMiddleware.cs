using System.Text;
using B2.Infrastructure.Persistence;
using B2.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace B2.Server.Api.Middleware;

/// <summary>
/// Idempotent replay for <c>POST /api/declarations</c> and <c>POST /api/vehicles/{{id}}/exit</c> when
/// <c>Idempotency-Key</c> is present (FR-019).
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next, IdempotencyConcurrencyGate idempotencyGate)
{
    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        if (context.Request.Method != HttpMethods.Post || !IsPostSupportingIdempotency(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var idemKey) ||
            string.IsNullOrWhiteSpace(idemKey))
        {
            await next(context);
            return;
        }

        var key = idemKey.ToString().Trim();
        if (key.Length > 128)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { code = "IDEMPOTENCY_KEY_INVALID", detail = "Idempotency-Key too long." });
            return;
        }

        context.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted);
        }

        context.Request.Body.Position = 0;

        var requestHash = TokenHash.Sha256Hex($"{context.Request.Method}:{context.Request.Path}:{body}");
        var lockKey = string.Concat(key, "\u001f", requestHash);

        await idempotencyGate.RunSerializedAsync(
            lockKey,
            async () =>
            {
                {
                    using var lookupScope = scopeFactory.CreateScope();
                    var dbLookup = lookupScope.ServiceProvider.GetRequiredService<B2DbContext>();
                    var existing = await dbLookup.IdempotencyKeys.AsNoTracking()
                        .FirstOrDefaultAsync(k => k.Key == key && k.RequestHash == requestHash, context.RequestAborted);
                    if (existing is not null)
                    {
                        context.Response.StatusCode = existing.ResponseStatus;
                        context.Response.ContentType = existing.ResponseContentType;
                        await context.Response.WriteAsync(existing.ResponseBody, context.RequestAborted);
                        return;
                    }
                }

                var originalBody = context.Response.Body;
                await using var capture = new MemoryStream();
                context.Response.Body = capture;

                try
                {
                    await next(context);

                    capture.Seek(0, SeekOrigin.Begin);
                    var responseText = await new StreamReader(capture, Encoding.UTF8, leaveOpen: true)
                        .ReadToEndAsync(context.RequestAborted);
                    context.Response.Body = originalBody;

                    if (context.Response.StatusCode is >= 200 and < 300)
                    {
                        using var saveScope = scopeFactory.CreateScope();
                        var dbSave = saveScope.ServiceProvider.GetRequiredService<B2DbContext>();
                        dbSave.IdempotencyKeys.Add(
                            new IdempotencyKeyRecord
                            {
                                Key = key,
                                RequestHash = requestHash,
                                ResponseStatus = context.Response.StatusCode,
                                ResponseBody = responseText,
                                ResponseBodyHash = TokenHash.Sha256Hex(responseText),
                                ResponseContentType = context.Response.ContentType ?? "application/json; charset=utf-8",
                                CreatedAt = DateTimeOffset.UtcNow,
                            });
                        await dbSave.SaveChangesAsync(context.RequestAborted);
                    }

                    context.Response.ContentType ??= "application/json; charset=utf-8";
                    await context.Response.WriteAsync(responseText, context.RequestAborted);
                }
                catch
                {
                    context.Response.Body = originalBody;
                    throw;
                }
            },
            context.RequestAborted);
    }

    private static bool IsPostSupportingIdempotency(PathString path)
    {
        if (path.Equals("/api/declarations", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var p = path.Value ?? "";
        return p.StartsWith("/api/vehicles/", StringComparison.OrdinalIgnoreCase)
               && p.EndsWith("/exit", StringComparison.OrdinalIgnoreCase);
    }
}
