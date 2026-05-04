using System.Text.Json.Serialization;

namespace B2.Desktop.Services;

internal sealed class LedgerChangedPayload
{
    [JsonPropertyName("declarationId")]
    public Guid DeclarationId { get; init; }
}
