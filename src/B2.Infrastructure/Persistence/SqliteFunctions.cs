namespace B2.Infrastructure.Persistence;

/// <summary>
/// SQLite built-ins exposed to LINQ (registered in <see cref="B2DbContext"/>). The SQLite
/// provider stores <see cref="DateTimeOffset"/> as ISO text and cannot ORDER BY or compare it;
/// <c>julianday()</c> parses that text, offset included, into a UTC-normalised REAL that SQLite
/// can sort and page on (#143).
/// </summary>
public static class SqliteFunctions
{
    public static double JulianDay(DateTimeOffset value) =>
        throw new NotSupportedException($"{nameof(JulianDay)} is only translatable inside an EF Core query.");
}
