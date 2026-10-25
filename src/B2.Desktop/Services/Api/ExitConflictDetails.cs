using System.Globalization;

namespace B2.Desktop.Services.Api;

/// <summary>
/// Who won a concurrent exit and when, read from the 409 <c>VEHICLE_ALREADY_EXITED</c> problem body
/// (<c>MarkVehicleExitCommandHandler</c> sets <c>winnerUsername</c> and an ISO-8601 <c>exitTime</c>),
/// so the losing gate can say what happened instead of blaming the network (#191).
/// Pure BCL so the test project can link it.
/// </summary>
public sealed record ExitConflictDetails(string WinnerName, DateTimeOffset ExitTime)
{
    /// <summary>Returns the details, or <c>null</c> when the body lacks either field.</summary>
    public static ExitConflictDetails? FromProblemJson(string? content)
    {
        var winner = ApiErrorCode.Extension(content, "winnerUsername");
        var exitTime = ApiErrorCode.Extension(content, "exitTime");
        if (string.IsNullOrWhiteSpace(winner)
            || !DateTimeOffset.TryParse(exitTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
        {
            return null;
        }

        return new ExitConflictDetails(winner.Trim(), at);
    }
}
