using System.Data;
using System.Globalization;
using B2.Application.Abstractions;
using B2.Shared.Contracts.Declarations;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class VehicleSuffixSearchRepository : IVehicleSuffixSearchRepository
{
    private readonly B2DbContext _db;

    public VehicleSuffixSearchRepository(B2DbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<VehicleSearchResultDto>> SearchAsync(
        string suffix,
        bool unexitedOnly,
        int limit,
        CancellationToken cancellationToken)
    {
        var s = suffix.Trim().ToUpperInvariant();
        var isFour = s.Length == 4;
        var hasSuffix = s.Length > 0;

        // SQLite cannot translate DateTimeOffset in ORDER BY from LINQ; use parameterized SQL.
        await _db.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = _db.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                """
                SELECT
                  v."Id",
                  v."DeclarationId",
                  v."PlateNumber",
                  d."DeclarationNumber",
                  d."CompanyName",
                  v."EntryTime",
                  v."ExitMarked"
                FROM "Vehicles" AS v
                INNER JOIN "Declarations" AS d ON d."Id" = v."DeclarationId"
                WHERE (@unexitedOnly = 0 OR v."ExitMarked" = 0)
                  AND (
                    @hasSuffix = 0
                    OR (@isFour = 1 AND v."PlateLast4" = @suffix)
                    OR (@isFour = 0 AND substr(upper(v."PlateNumber"), 0 - @suffixLen) = @suffix)
                  )
                ORDER BY v."EntryTime"
                LIMIT @limit
                """;

            AddIntParameter(command, "@unexitedOnly", unexitedOnly ? 1 : 0);
            AddIntParameter(command, "@hasSuffix", hasSuffix ? 1 : 0);
            AddIntParameter(command, "@isFour", isFour ? 1 : 0);
            AddTextParameter(command, "@suffix", s);
            AddIntParameter(command, "@suffixLen", s.Length);
            AddIntParameter(command, "@limit", limit);

            var list = new List<VehicleSearchResultDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetGuid(0);
                var declarationId = reader.GetGuid(1);
                var plate = reader.GetString(2);
                var declNo = reader.GetString(3);
                var company = reader.GetString(4);
                var entry = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture);
                var exitMarked = reader.GetInt64(6) != 0;
                list.Add(new VehicleSearchResultDto(id, declarationId, plate, declNo, company, entry, exitMarked));
            }

            return list;
        }
        finally
        {
            await _db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    private static void AddIntParameter(IDbCommand command, string name, int value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.DbType = DbType.Int32;
        p.Value = value;
        command.Parameters.Add(p);
    }

    private static void AddTextParameter(IDbCommand command, string name, string value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.DbType = DbType.String;
        p.Value = value;
        command.Parameters.Add(p);
    }
}
