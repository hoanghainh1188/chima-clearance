using System.Text.Json;

namespace B2.Desktop.Services.Api;

/// <summary>
/// Reads the domain error <c>code</c> from the server's problem+json body (see
/// <c>GlobalExceptionMiddleware</c>), so a view can show what actually went wrong instead of one
/// generic "check the connection" message. Pure BCL so the test project can link it.
/// </summary>
public static class ApiErrorCode
{
    public const string DeclarationNumberDuplicate = "DECLARATION_NUMBER_DUPLICATE";
    public const string VehicleAlreadyExited = "VEHICLE_ALREADY_EXITED";
    public const string DeclarationCancelled = "DECLARATION_CANCELLED";

    /// <summary>Returns the <c>code</c> property, or <c>null</c> for an empty or non-JSON body.</summary>
    public static string? FromProblemJson(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.String
                    ? code.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
