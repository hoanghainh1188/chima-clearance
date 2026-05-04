using System.Collections.ObjectModel;

namespace B2.Domain;

/// <summary>
/// Domain-level error mapped to HTTP via ProblemDetails (stable <see cref="Code"/>).
/// </summary>
public sealed class DomainException : Exception
{
    public string Code { get; }

    /// <summary>Preferred HTTP status when surfaced from API (nullable = map by code).</summary>
    public int? HttpStatus { get; }

    /// <summary>Optional machine-readable fields merged into problem+json (e.g. FR-024 winner).</summary>
    public IReadOnlyDictionary<string, string>? Extensions { get; }

    public DomainException(
        string code,
        string message,
        int? httpStatus = null,
        IReadOnlyDictionary<string, string>? extensions = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        Extensions = extensions is null ? null : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(extensions));
    }
}
