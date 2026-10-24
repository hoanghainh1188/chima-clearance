using B2.Desktop.Services.Users;
using FluentAssertions;

namespace B2.Desktop.Tests.Users;

/// <summary>F-02 — the user dialog mirrors the server's password and username rules.</summary>
public sealed class TemporaryPasswordTests
{
    [Fact]
    [Trait("Requirement", "F-02")]
    public void Generated_passwords_always_meet_the_complexity_rule()
    {
        for (var i = 0; i < 500; i++)
        {
            var password = TemporaryPassword.Generate();
            password.Should().HaveLength(TemporaryPassword.GeneratedLength);
            TemporaryPassword.MeetsComplexity(password).Should().BeTrue(because: password);
        }
    }

    [Fact]
    public void Generated_passwords_differ()
    {
        Enumerable.Range(0, 50).Select(_ => TemporaryPassword.Generate()).Distinct().Should().HaveCount(50);
    }

    [Theory]
    [InlineData("Abcdefgh12", true)]
    [InlineData("abcdefgh1!", true)]
    [InlineData("ABCDEFGH1!", true)]
    [InlineData("Abcdefgh!x", true)]
    [InlineData("abcdefghij", false)]
    [InlineData("abcdefgh12", false)]
    [InlineData("Ab1!", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Complexity_matches_the_server_rule(string? password, bool expected)
    {
        TemporaryPassword.MeetsComplexity(password).Should().Be(expected);
    }

    [Theory]
    [InlineData("nam", true)]
    [InlineData(" Nam_01 ", true)]
    [InlineData("ab", false)]
    [InlineData("1nam", false)]
    [InlineData("nguyễn", false)]
    [InlineData("n.am", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Username_matches_the_domain_pattern(string? username, bool expected)
    {
        TemporaryPassword.IsValidUsername(username).Should().Be(expected);
    }
}
