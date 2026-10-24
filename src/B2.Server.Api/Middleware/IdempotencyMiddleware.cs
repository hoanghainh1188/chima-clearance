using System.Text;
using B2.Infrastructure.Persistence;
using B2.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace B2.Server.Api.Middleware;

/// <summary>
/// Idempotent replay for <c>POST /api/declarations</c> and <c>POST /api/vehicles/{{id}}/exit</c> when
/// <c>Idempotency-Key</c> is present (FR-019).
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next)
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

        // #141: serialise requests that share a key, so a retry racing the in-flight original
        // waits and then replays its stored response instead of executing a second time.
        var gate = KeyLocks[(int)((uint)StringComparer.Ordinal.GetHashCode(key) % KeyLockStripes)];
        await gate.WaitAsync(context.RequestAborted);
        try
        {
            await ExecuteOnceAsync(context, scopeFactory, key, requestHash);
        }
        finally
        {
            gate.Release();
        }
    }

    private const int KeyLockStripes = 64;

    private static readonly SemaphoreSlim[] KeyLocks =
        Enumerable.Range(0, KeyLockStripes).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private async Task ExecuteOnceAsync(
        HttpContext context,
        IServiceScopeFactory scopeFactory,
        string key,
        string requestHash)
    {
        {
            using var lookupScope = scopeFactory.CreateScope();
            var dbLookup = lookupScope.ServiceProvider.GetRequiredService<B2DbContext>();
            var existing = await dbLookup.IdempotencyKeys.AsNoTracking()
                .FirstOrDefaultAsync(k => k.Key == key, context.RequestAborted);
            if (existing is not null && existing.RequestHash != requestHash)
            {
                // #141: a key names one logical operation — reusing it for a different payload
                // is a client bug, not a new request (IETF draft-ietf-httpapi-idempotency-key).
                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                await context.Response.WriteAsJsonAsync(
                    new
                    {
                        code = "IDEMPOTENCY_KEY_REUSED",
                        detail = "Idempotency-Key was already used with a different request payload.",
                    },
                    context.RequestAborted);
                return;
            }

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
                .ReadToEndAsync(CancellationToken.None);
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
                // The write has committed: record the key even if the client already hung up,
                // otherwise its retry would execute the operation a second time.
                await dbSave.SaveChangesAsync(CancellationToken.None);
            }

            context.Response.ContentType ??= "application/json; charset=utf-8";
            await context.Response.WriteAsync(responseText, context.RequestAborted);
        }
        catch
        {
            context.Response.Body = originalBody;
            throw;
        }
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
