using B2.Desktop.Services.Api;
using FluentAssertions;

namespace B2.Desktop.Tests.Exit;

/// <summary>#191 — the losing exit gate learns who exited the vehicle and when.</summary>
public sealed class ExitConflictDetailsTests
{
    [Fact]
    [Trait("Feature", "F-08")]
    public void Reads_the_winner_and_exit_time_from_the_409_body()
    {
        const string body = """
            {"title":"VEHICLE_ALREADY_EXITED","status":409,"code":"VEHICLE_ALREADY_EXITED",
             "winnerUsername":"Nguyễn Văn Nam","exitTime":"2026-10-24T09:42:00.0000000+00:00"}
            """;

        var details = ExitConflictDetails.FromProblemJson(body);

        details.Should().NotBeNull();
        details!.WinnerName.Should().Be("Nguyễn Văn Nam");
        details.ExitTime.Should().Be(new DateTimeOffset(2026, 10, 24, 9, 42, 0, TimeSpan.Zero));
    }

    [Theory]
    [Trait("Feature", "F-08")]
    [InlineData(null)]
    [InlineData("{\"code\":\"VEHICLE_ALREADY_EXITED\"}")]
    [InlineData("{\"code\":\"VEHICLE_ALREADY_EXITED\",\"winnerUsername\":\"nam\"}")]
    [InlineData("{\"code\":\"VEHICLE_ALREADY_EXITED\",\"winnerUsername\":\"nam\",\"exitTime\":\"not a time\"}")]
    [InlineData("{\"code\":\"VEHICLE_ALREADY_EXITED\",\"winnerUsername\":\" \",\"exitTime\":\"2026-10-24T09:42:00Z\"}")]
    public void Missing_or_malformed_fields_yield_no_details(string? body)
    {
        ExitConflictDetails.FromProblemJson(body).Should().BeNull();
    }
}
