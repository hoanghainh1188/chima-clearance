using System.Globalization;

namespace B2.Server.Api.Tests.Support;

public static class TestDeclarationNumbers
{
    /// <summary>Exactly 12 ASCII digits (valid <see cref="B2.Domain.Declarations.DeclarationNumber"/>).</summary>
    public static string Next()
    {
        // maxValue is exclusive — use 1e12 so the largest value is 999_999_999_999 (12 digits).
        var n = Random.Shared.NextInt64(100_000_000_000L, 1_000_000_000_000L);
        return n.ToString("D12", CultureInfo.InvariantCulture);
    }
}
