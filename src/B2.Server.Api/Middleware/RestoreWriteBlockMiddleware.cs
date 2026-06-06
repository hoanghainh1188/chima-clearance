using B2.Application.Abstractions;

namespace B2.Server.Api.Middleware;

/// <summary>
/// F-15 — after a restore is staged, refuse mutating API calls until the service restarts
/// and <see cref="IDatabaseRestoreService.ApplyPending"/> promotes the snapshot. Prevents
/// writes to the live DB that would be discarded on the next startup (spec: "stops accepting
/// writes" during restore).
/// </summary>
public sealed class RestoreWriteBlockMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IDatabaseRestoreService restore)
    {
        if (restore.IsRestorePending() && IsMutating(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "RESTORE_PENDING",
                detail = "A database restore is in progress. Writes are disabled until the service restarts.",
            });
            return;
        }

        await next(context);
    }

    private static bool IsMutating(string method) =>
        method == HttpMethods.Post
        || method == HttpMethods.Put
        || method == HttpMethods.Patch
        || method == HttpMethods.Delete;
}
