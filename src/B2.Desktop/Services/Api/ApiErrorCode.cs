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
    public const string ValidationError = "VALIDATION_ERROR";
    public const string UsernameTaken = "USERNAME_TAKEN";
    public const string UsernameInvalid = "USERNAME_INVALID";
    public const string CannotDeactivateSelf = "CANNOT_DEACTIVATE_SELF";
    public const string LastAdminLockout = "LAST_ADMIN_LOCKOUT";

    /// <summary>Returns the <c>code</c> property, or <c>null</c> for an empty or non-JSON body.</summary>
    public static string? FromProblemJson(string? content) => StringProperty(content, "code");

    /// <summary>
    /// Returns a string extension of the problem body (e.g. <c>winnerUsername</c> / <c>exitTime</c>
    /// on a 409 <c>VEHICLE_ALREADY_EXITED</c>), or <c>null</c> when absent.
    /// </summary>
    public static string? Extension(string? content, string name) => StringProperty(content, name);

    private static string? StringProperty(string? content, string name)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
