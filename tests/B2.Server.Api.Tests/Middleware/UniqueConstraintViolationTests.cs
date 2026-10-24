using B2.Server.Api.Middleware;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace B2.Server.Api.Tests.Middleware;

public sealed class UniqueConstraintViolationTests
{
    [Theory]
    [Trait("Issue", "140")]
    [InlineData("SQLite Error 19: 'UNIQUE constraint failed: DeclarationNumbers.Number'.", 2067, "DECLARATION_NUMBER_DUPLICATE")]
    [InlineData("SQLite Error 19: 'UNIQUE constraint failed: Users.Username'.", 2067, "USERNAME_TAKEN")]
    [InlineData("SQLite Error 19: 'UNIQUE constraint failed: Yards.Code'.", 2067, "DUPLICATE_ENTRY")]
    [InlineData("SQLite Error 19: 'UNIQUE constraint failed: Vehicles.Id'.", 1555, "DUPLICATE_ENTRY")]
    public void Unique_violation_translates_to_409(string message, int extendedCode, string expectedCode)
    {
        var ex = new DbUpdateException("save failed", new SqliteException(message, 19, extendedCode));

        UniqueConstraintViolation.TryTranslate(ex, out var dex).Should().BeTrue();

        dex!.Code.Should().Be(expectedCode);
        dex.HttpStatus.Should().Be(409);
    }

    [Fact]
    [Trait("Issue", "140")]
    public void Other_constraint_failures_are_not_translated()
    {
        // 787 = SQLITE_CONSTRAINT_FOREIGNKEY — a real bug, must stay a 500.
        var ex = new DbUpdateException(
            "save failed",
            new SqliteException("SQLite Error 19: 'FOREIGN KEY constraint failed'.", 19, 787));

        UniqueConstraintViolation.TryTranslate(ex, out var dex).Should().BeFalse();
        dex.Should().BeNull();
    }
}
