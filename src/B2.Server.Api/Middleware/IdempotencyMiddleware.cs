using System.Text;
using B2.Infrastructure.Persistence;
using B2.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace B2.Server.Api.Middleware;

/// <summary>Idempotent replay for <c>POST /api/declarations</c> when <c>Idempotency-Key</c> is present.</summary>
public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        if (context.Request.Method != HttpMethods.Post ||
            !context.Request.Path.Equals("/api/declarations", StringComparison.OrdinalIgnoreCase))
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

        await next(context);

        capture.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(capture).ReadToEndAsync(context.RequestAborted);
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
}
