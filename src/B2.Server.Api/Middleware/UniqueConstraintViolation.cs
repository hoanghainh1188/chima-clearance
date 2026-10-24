using System.Diagnostics.CodeAnalysis;
using B2.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace B2.Server.Api.Middleware;

/// <summary>
/// #140: an application-level "exists?" pre-check cannot stop two concurrent writers from both
/// passing it; the SQLite unique index is the real guard. Translate its violation into the same
/// 409 <see cref="DomainException"/> the pre-check would have thrown, instead of a 500.
/// </summary>
internal static class UniqueConstraintViolation
{
    private const int SqliteConstraint = 19;
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    public static bool TryTranslate(DbUpdateException exception, [NotNullWhen(true)] out DomainException? domainException)
    {
        if (exception.InnerException is not SqliteException
            {
                SqliteErrorCode: SqliteConstraint,
                SqliteExtendedErrorCode: SqliteConstraintUnique or SqliteConstraintPrimaryKey,
            } sqlite)
        {
            domainException = null;
            return false;
        }

        domainException = sqlite.Message switch
        {
            var m when m.Contains("DeclarationNumbers.Number", StringComparison.Ordinal) =>
                new DomainException("DECLARATION_NUMBER_DUPLICATE", "Declaration number already exists.", 409),
            var m when m.Contains("Users.Username", StringComparison.Ordinal) =>
                new DomainException("USERNAME_TAKEN", "Username already exists.", 409),
            _ => new DomainException("DUPLICATE_ENTRY", "A record with the same unique value already exists.", 409),
        };
        return true;
    }
}
