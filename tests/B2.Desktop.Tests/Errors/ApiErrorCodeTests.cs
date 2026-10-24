using B2.Desktop.Services.Api;
using FluentAssertions;

namespace B2.Desktop.Tests.Errors;

/// <summary>Audit F1 — the Desktop reads the server's error code to show a specific message.</summary>
public sealed class ApiErrorCodeTests
{
    [Fact]
    [Trait("Requirement", "FR-024")]
    public void Reads_the_code_from_a_problem_json_body()
    {
        const string body = """
            {"type":"https://b2/errors/domain","title":"VEHICLE_ALREADY_EXITED","status":409,
             "detail":"Vehicle was already marked exited by another operator.","code":"VEHICLE_ALREADY_EXITED",
             "correlationId":"abc"}
            """;

        ApiErrorCode.FromProblemJson(body).Should().Be(ApiErrorCode.VehicleAlreadyExited);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<html>502 Bad Gateway</html>")]
    [InlineData("[1,2]")]
    [InlineData("{\"code\":42}")]
    [InlineData("{\"detail\":\"no code here\"}")]
    public void Anything_else_yields_no_code(string? body)
    {
        ApiErrorCode.FromProblemJson(body).Should().BeNull();
    }

    [Theory]
    [Trait("Requirement", "F-02")]
    [InlineData("USERNAME_TAKEN", ApiErrorCode.UsernameTaken)]
    [InlineData("CANNOT_DEACTIVATE_SELF", ApiErrorCode.CannotDeactivateSelf)]
    [InlineData("LAST_ADMIN_LOCKOUT", ApiErrorCode.LastAdminLockout)]
    public void Reads_the_user_admin_conflict_codes(string serverCode, string expected)
    {
        var body = $$"""{"status":409,"code":"{{serverCode}}","correlationId":"abc"}""";

        ApiErrorCode.FromProblemJson(body).Should().Be(expected);
    }
}
