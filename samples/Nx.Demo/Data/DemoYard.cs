namespace Nx.Demo.Data;

/// <summary>A yard location as ERP's piece-locations page has it: flat, with the piece count of everything inside.</summary>
public sealed record YardLocation(int ResourceId, int? CurrentContainerId, string Name, int AggregatePieceCount, string Kind);

/// <summary>A stockyard for the tree list demos: yards, rows, bays.</summary>
public static class DemoYard
{
    public static IReadOnlyList<YardLocation> Locations { get; } = Build();

    private static List<YardLocation> Build()
    {
        var list = new List<YardLocation>();
        var id = 1;
        void Yard(string name, (string Row, int[] Bays)[] rows)
        {
            var yardId = id++;
            var yardIndex = list.Count;
            list.Add(new(yardId, null, name, 0, "Yard"));
            var yardTotal = 0;
            foreach (var (row, bays) in rows)
            {
                var rowId = id++;
                var rowIndex = list.Count;
                list.Add(new(rowId, yardId, row, 0, "Row"));
                for (var b = 0; b < bays.Length; b++)
                    list.Add(new(id++, rowId, $"{row}-{b + 1}", bays[b], "Bay"));
                list[rowIndex] = list[rowIndex] with { AggregatePieceCount = bays.Sum() };
                yardTotal += bays.Sum();
            }
            list[yardIndex] = list[yardIndex] with { AggregatePieceCount = yardTotal };
        }
        Yard("Lincoln Ave", [("A", [6, 4, 0, 8]), ("B", [3, 5, 7]), ("C", [12, 2])]);
        Yard("Stockyards", [("North", [9, 11, 4]), ("South", [0, 6, 3, 5])]);
        Yard("Rail spur", [("Spur", [2, 2])]);
        return list;
    }

    public static IEnumerable<YardLocation> Named(params string[] names) => Locations.Where(l => names.Contains(l.Name));
}
