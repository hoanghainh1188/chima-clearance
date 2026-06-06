using System.Data;
using System.Globalization;
using B2.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class VehicleSuggestRepository : IVehicleSuggestRepository
{
    private readonly B2DbContext _db;

    public VehicleSuggestRepository(B2DbContext db) => _db = db;

    public async Task<string?> SuggestAsync(
        string plate,
        string field,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        await _db.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var cmd = _db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = BuildQuery(field);

            AddTextParameter(cmd, "@plate", plate);
            AddTextParameter(cmd, "@cutoff", cutoff.ToString("O", CultureInfo.InvariantCulture));

            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is DBNull or null ? null : (string)result;
        }
        finally
        {
            await _db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    private static string BuildQuery(string field) => field switch
    {
        "trailer" =>
            """
            SELECT v."TrailerOrContainerNumber"
            FROM "Vehicles" AS v
            WHERE v."PlateNumber" = @plate
              AND v."EntryTime" > @cutoff
              AND v."TrailerOrContainerNumber" IS NOT NULL
              AND v."TrailerOrContainerNumber" <> ''
            GROUP BY v."TrailerOrContainerNumber"
            ORDER BY COUNT(*) DESC, MAX(v."EntryTime") DESC
            LIMIT 1
            """,

        "company" =>
            """
            SELECT d."CompanyName"
            FROM "Vehicles" AS v
            INNER JOIN "Declarations" AS d ON d."Id" = v."DeclarationId"
            WHERE v."PlateNumber" = @plate
              AND v."EntryTime" > @cutoff
              AND d."CompanyName" IS NOT NULL
              AND d."CompanyName" <> ''
            GROUP BY d."CompanyName"
            ORDER BY COUNT(*) DESC, MAX(v."EntryTime") DESC
            LIMIT 1
            """,

        "goods" =>
            """
            SELECT d."GoodsName"
            FROM "Vehicles" AS v
            INNER JOIN "Declarations" AS d ON d."Id" = v."DeclarationId"
            WHERE v."PlateNumber" = @plate
              AND v."EntryTime" > @cutoff
              AND d."GoodsName" IS NOT NULL
              AND d."GoodsName" <> ''
            GROUP BY d."GoodsName"
            ORDER BY COUNT(*) DESC, MAX(v."EntryTime") DESC
            LIMIT 1
            """,

        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown suggest field.")
    };

    private static void AddTextParameter(IDbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.DbType = DbType.String;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
