namespace B2.Infrastructure.Persistence;

public sealed class IdempotencyKeyRecord
{
    public string Key { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public int ResponseStatus { get; set; }
    public string ResponseBody { get; set; } = null!;

    /// <summary>
    /// SHA-256 hex of <see cref="ResponseBody"/>. Captured alongside the body so future
    /// retries / forensic comparisons can detect divergence without rehashing the
    /// (potentially large) body. Mirrors <c>data-model.md § 2.8</c>.
    /// </summary>
    public string ResponseBodyHash { get; set; } = null!;
    public string ResponseContentType { get; set; } = "application/json; charset=utf-8";
    public DateTimeOffset CreatedAt { get; set; }
}
