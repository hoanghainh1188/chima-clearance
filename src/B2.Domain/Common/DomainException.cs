namespace B2.Domain;

/// <summary>
/// Domain-level error mapped to HTTP via ProblemDetails (stable <see cref="Code"/>).
/// </summary>
public sealed class DomainException : Exception
{
    public string Code { get; }

    /// <summary>Preferred HTTP status when surfaced from API (nullable = map by code).</summary>
    public int? HttpStatus { get; }

    public DomainException(string code, string message, int? httpStatus = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
    }
}
