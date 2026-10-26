namespace Nx.Demo.Data;

/// <summary>A day of yard activity, as ERP's storage and shipping charts read it.</summary>
public sealed record YardDay(DateTime Date, int Loads, int Pieces, double Hours);

/// <summary>One concrete test in a control chart: its sequence, slump and the limits.</summary>
public sealed record SlumpTest(int Index, double Slump, double Mean, double Upper, double Lower, string Ticket);

/// <summary>A slice of a pie: a name, a count and an optional color.</summary>
public sealed record Share(string Name, double Count, string? Color = null);

/// <summary>Loads shipped per plant per month, for the column charts.</summary>
public sealed record PlantMonth(string Plant, DateTime Month, int Loads);

/// <summary>The chart demos' data: fixed, so screenshots and tests see the same numbers every time.</summary>
public static class DemoCharts
{
    /// <summary>Thirty days of yard activity ending on 2026-10-23.</summary>
    public static IReadOnlyList<YardDay> Days { get; } = Enumerable.Range(0, 30).Select(i =>
    {
        var date = new DateTime(2026, 9, 24).AddDays(i);
        var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var loads = weekend ? 2 + i % 3 : 9 + (i * 7 % 11);
        return new YardDay(date, loads, 180 + i * 4 - (i % 5) * 9, Math.Round(loads * 1.6 + (i % 4) * 0.7, 1));
    }).ToList();

    /// <summary>Twenty slump tests around 4.5 in, with their 95% limits.</summary>
    public static IReadOnlyList<SlumpTest> Slumps { get; } = Enumerable.Range(1, 20).Select(i =>
        new SlumpTest(i, Math.Round(4.5 + Math.Sin(i * 1.3) * 0.6 + (i % 7 == 0 ? 0.9 : 0), 2), 4.5, 5.6, 3.4, $"T-{4100 + i}")).ToList();

    /// <summary>Where the pieces in storage are.</summary>
    public static IReadOnlyList<Share> Yards { get; } =
    [
        new("North yard", 412), new("South yard", 288), new("Rail siding", 97), new("Riverside site", 64), new("Repair shop", 21),
    ];

    /// <summary>Loads shipped per plant, May to October 2026.</summary>
    public static IReadOnlyList<PlantMonth> PlantMonths { get; } =
        (from plant in new[] { "Plant 1", "Plant 2", "Plant 3" }
         from m in Enumerable.Range(0, 6)
         select new PlantMonth(plant, new DateTime(2026, 5, 1).AddMonths(m),
             plant switch { "Plant 1" => 120 + m * 9, "Plant 2" => 85 + (m % 3) * 14, _ => 40 + m * 6 })).ToList();

    /// <summary>Inventory on hand by material, for the bar chart.</summary>
    public static IReadOnlyList<Share> Inventory { get; } =
    [
        new("Rebar #4", 1240), new("Rebar #5", 860), new("Strand 0.5 in", 640), new("Lifting loops", 420), new("Embed plates", 310), new("Insulation", 145),
    ];
}
