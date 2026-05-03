using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence;

public static class SqliteStartup
{
    /// <summary>Apply PRAGMAs from <c>data-model.md § 9</c> to an open SQLite connection.</summary>
    public static void ApplyPragmas(SqliteConnection connection)
    {
        // One statement per command — some SQLite builds reject batched PRAGMA scripts via prepare_v2.
        foreach (var sql in new[]
        {
            "PRAGMA journal_mode=WAL;",
            "PRAGMA synchronous=NORMAL;",
            "PRAGMA temp_store=MEMORY;",
            "PRAGMA mmap_size=268435456;",
            "PRAGMA busy_timeout=5000;",
            "PRAGMA foreign_keys=ON;",
        })
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    public static void ApplyPragmasForContext(DbContext context)
    {
        var conn = context.Database.GetDbConnection();
        if (conn is SqliteConnection sqlite)
        {
            if (sqlite.State != System.Data.ConnectionState.Open)
            {
                sqlite.Open();
            }

            ApplyPragmas(sqlite);
        }
    }
}
