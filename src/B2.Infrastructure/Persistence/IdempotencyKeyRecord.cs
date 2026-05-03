namespace B2.Infrastructure.Persistence;

public sealed class IdempotencyKeyRecord
{
    public string Key { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public int ResponseStatus { get; set; }
    public string ResponseBody { get; set; } = null!;
    public string ResponseContentType { get; set; } = "application/json; charset=utf-8";
    public DateTimeOffset CreatedAt { get; set; }
}
