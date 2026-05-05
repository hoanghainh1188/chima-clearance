using System.Text.Json.Serialization;

namespace B2.Desktop.Services;

internal sealed class DailyHeaderOfficerChangedPayload
{
    [JsonPropertyName("date")]
    public string Date { get; init; } = string.Empty;

    [JsonPropertyName("officerName")]
    public string OfficerName { get; init; } = string.Empty;
}
