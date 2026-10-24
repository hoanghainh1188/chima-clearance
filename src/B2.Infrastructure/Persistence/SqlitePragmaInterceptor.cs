using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace B2.Infrastructure.Persistence;

/// <summary>
/// #162: <c>synchronous</c>, <c>temp_store</c>, <c>mmap_size</c> and <c>busy_timeout</c> are
/// connection-scoped in SQLite — applying them once at startup left every pooled request
/// connection on the defaults (synchronous=FULL, temp tables on disk, no mmap, no busy wait).
/// Re-apply them each time EF opens a connection. <c>journal_mode=WAL</c> is persisted in the
/// file and stays in <see cref="SqliteStartup.ApplyPragmas"/>.
/// </summary>
public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    public static SqlitePragmaInterceptor Instance { get; } = new();

    private SqlitePragmaInterceptor()
    {
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (connection is SqliteConnection sqlite)
        {
            SqliteStartup.ApplyConnectionPragmas(sqlite);
        }
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ConnectionOpened(connection, eventData);
        return Task.CompletedTask;
    }
}
