using B2.Application.Common;
using FluentAssertions;

namespace B2.Application.Tests.Auth;

/// <summary>F-01 — the per-install initial Admin password shown by the installer.</summary>
public sealed class InitialPasswordGeneratorTests
{
    private const int Samples = 2_000;

    [Fact]
    [Trait("Feature", "F-01")]
    public void Every_generated_password_meets_the_complexity_rules()
    {
        for (var i = 0; i < Samples; i++)
        {
            PasswordRules.MeetsComplexity(InitialPasswordGenerator.Generate()).Should().BeTrue();
        }
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public void Format_is_four_groups_of_four_without_ambiguous_characters()
    {
        for (var i = 0; i < Samples; i++)
        {
            var password = InitialPasswordGenerator.Generate();

            password.Should().MatchRegex("^[A-Za-z2-9]{4}-[A-Za-z2-9]{4}-[A-Za-z2-9]{4}-[A-Za-z2-9]{4}$");
            password.IndexOfAny(['I', 'l', 'O', 'o', '0', '1']).Should().Be(-1);
        }
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public void Passwords_are_not_repeated()
    {
        var passwords = Enumerable.Range(0, Samples).Select(_ => InitialPasswordGenerator.Generate()).ToList();

        passwords.Should().OnlyHaveUniqueItems();
    }
}
