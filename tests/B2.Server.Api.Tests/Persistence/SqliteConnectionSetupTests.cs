using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Persistence;

public sealed class SqliteConnectionSetupTests
{
    [Fact]
    [Trait("Issue", "162")]
    public async Task Every_request_connection_gets_the_connection_scoped_pragmas()
    {
        await using var factory = new B2ApiFactory();
        _ = factory.CreateClient();

        // A fresh scope = a pooled connection opened after startup, not the startup one.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();

        (await PragmaAsync(db, "synchronous")).Should().Be(1, "NORMAL");
        (await PragmaAsync(db, "temp_store")).Should().Be(2, "MEMORY");
        (await PragmaAsync(db, "busy_timeout")).Should().Be(5000);
        (await PragmaAsync(db, "foreign_keys")).Should().Be(1);
    }

    [Fact]
    [Trait("Issue", "143")]
    public async Task Ledger_paging_is_ordered_and_limited_in_sql()
    {
        await using var factory = new B2ApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();

        var sql = db.Declarations
            .OrderByDescending(d => SqliteFunctions.JulianDay(d.EnteredAt))
            .Skip(50)
            .Take(50)
            .Select(d => d.Id)
            .ToQueryString();

        sql.Should().Contain("ORDER BY julianday(").And.Contain("LIMIT");
    }

    private static async Task<long> PragmaAsync(B2DbContext db, string name)
    {
        // OpenConnectionAsync goes through EF, so the interceptor runs exactly as for a request.
        await db.Database.OpenConnectionAsync();
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"PRAGMA {name};";
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
