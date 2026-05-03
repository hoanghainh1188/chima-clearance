namespace B2.Application.Common;

public static class PasswordRules
{
    /// <summary>Constitution VII: ≥10 chars and 3 of 4 character classes.</summary>
    public static bool MeetsComplexity(string password)
    {
        if (password.Length < 10)
        {
            return false;
        }

        var classes = new[]
        {
            password.Any(char.IsUpper),
            password.Any(char.IsLower),
            password.Any(char.IsDigit),
            password.Any(ch => !char.IsLetterOrDigit(ch)),
        };

        return classes.Count(x => x) >= 3;
    }
}
