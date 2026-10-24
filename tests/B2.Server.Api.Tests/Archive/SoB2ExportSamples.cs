using B2.Application.Export;

namespace B2.Server.Api.Tests.Archive;

/// <summary>Sổ B2 export payloads for the archive tests.</summary>
internal static class SoB2ExportSamples
{
    public static readonly DateOnly Day = new(2022, 3, 15);

    /// <summary>One day with <paramref name="declarationCount"/> single-vehicle declarations.</summary>
    public static SoB2ExportData Simple(int declarationCount)
    {
        var declarations = Enumerable.Range(1, declarationCount)
            .Select(i => Declaration(
                FormattableString.Invariant($"31A{i:0000}"),
                new SoB2VehicleRow(FormattableString.Invariant($"31A{i:0000}"), null, DateTimeOffset.UtcNow, false, null, null)))
            .ToList();

        return new SoB2ExportData(Day, Day, new List<SoB2DayBlock> { new(Day, "Officer", declarationCount, declarations) });
    }

    /// <summary>
    /// Every formatting branch of the layout: day header with and without officer/total,
    /// multi-number cột C, child vehicle rows, all-exited fill, unexited red text,
    /// cancelled strike-through, a declaration with no vehicle, and a duplicate number.
    /// </summary>
    public static SoB2ExportData Mixed()
    {
        var entry = new DateTimeOffset(2022, 3, 15, 1, 30, 0, TimeSpan.Zero);
        var exitedAll = Declaration(
            "100001",
            new SoB2VehicleRow("24C12345", "CONT01", entry, true, entry.AddHours(2), "Ghi chú"),
            new SoB2VehicleRow("24R00001", null, entry.AddMinutes(5), true, entry.AddHours(3), null)) with
        {
            DeclarationNumbers = new[] { "100001", "300001" },
            IsAllExited = true,
            KvgsConfirmed = true,
            KvgsConfirmedByName = "KVGS",
        };
        var inside = Declaration(
            "100002",
            new SoB2VehicleRow("24C22222", null, entry.AddMinutes(10), false, null, null));
        var cancelled = Declaration(
            "100001",
            new SoB2VehicleRow("24C33333", null, entry.AddMinutes(20), false, null, null),
            new SoB2VehicleRow("24C44444", null, entry.AddMinutes(21), true, entry.AddHours(1), null)) with
        { IsCancelled = true };
        var noVehicle = Declaration("100004");

        return new SoB2ExportData(Day, Day.AddDays(1), new List<SoB2DayBlock>
        {
            new(Day, "Nguyễn Văn Trực", 5, new[] { exitedAll, inside, cancelled }),
            new(Day.AddDays(1), null, null, new[] { noVehicle }),
        });
    }

    private static SoB2DeclarationBlock Declaration(string number, params SoB2VehicleRow[] vehicles) => new(
        Day,
        number,
        new[] { number },
        "Processor",
        "Công ty TNHH",
        "Goods",
        "Yard",
        KvgsConfirmed: false,
        IsAllExited: false,
        IsCancelled: false,
        EnteredByName: "Officer",
        KvgsConfirmedByName: null,
        Vehicles: vehicles);

    public static async IAsyncEnumerable<SoB2DayBlock> Stream(SoB2ExportData data)
    {
        foreach (var day in data.Days)
        {
            await Task.Yield();
            yield return day;
        }
    }
}
