namespace Nx.Demo.Data;

/// <summary>A shift on the calendar, as ERP's shift schedule builds it.</summary>
public sealed class ShiftAppointment
{
    public string Title { get; init; } = "";
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public string ShiftName { get; init; } = "";
    public List<string> Employees { get; init; } = [];
    public string Color { get; init; } = "#2e6db4";
}

/// <summary>A meeting, for the scheduler's default look.</summary>
public sealed record Meeting(string Title, DateTime Start, DateTime End);

/// <summary>A plant crew's week and some meetings, built around any date so the demo always shows the current week.</summary>
public static class DemoShifts
{
    private static readonly string[] Day = ["Dana Ortiz", "Sam Lee", "Priya Nair"];
    private static readonly string[] Swing = ["Tom Becker", "Alex Kim"];
    private static readonly string[] Night = ["Jo Marsh", "Ray Cole"];

    public static DateTime WeekStart(DateTime date) => date.Date.AddDays(-(int)date.DayOfWeek);

    public static List<ShiftAppointment> Week(DateTime date)
    {
        var start = WeekStart(date);
        var list = new List<ShiftAppointment>();
        // The previous Saturday's night shift spills onto this Sunday morning.
        list.Add(Shift(start.AddDays(-1), 23, 8, "Night", Night, "#4a3f8f"));
        for (var d = 1; d <= 5; d++)
        {
            list.Add(Shift(start.AddDays(d), 7, 8, "Day", Day, "#2e6db4"));
            list.Add(Shift(start.AddDays(d), 15, 8, "Swing", Swing, "#1e8c8c"));
            if (d < 5)
                list.Add(Shift(start.AddDays(d), 23, 8, "Night", Night, "#4a3f8f"));
        }
        // Saturday overtime overlaps the stripping crew.
        list.Add(Shift(start.AddDays(6), 6, 6, "Overtime", ["Dana Ortiz"], "#c0392b"));
        list.Add(Shift(start.AddDays(6), 8, 4, "Stripping", ["Sam Lee", "Tom Becker"], "#d97a1e"));
        return list;
    }

    public static List<Meeting> Meetings(DateTime date)
    {
        var start = WeekStart(date);
        return
        [
            new("Pour planning", start.AddDays(1).AddHours(8), start.AddDays(1).AddHours(9)),
            new("Safety walk", start.AddDays(1).AddHours(8.5), start.AddDays(1).AddHours(10)),
            new("QC review", start.AddDays(1).AddHours(9), start.AddDays(1).AddHours(9.5)),
            new("Route 9 site visit", start.AddDays(3).AddHours(10), start.AddDays(3).AddHours(14)),
            new("Crew lunch", start.AddDays(5).AddHours(12), start.AddDays(5).AddHours(13)),
        ];
    }

    private static ShiftAppointment Shift(DateTime day, int hour, int hours, string name, string[] crew, string color) => new()
    {
        Title = name,
        Start = day.AddHours(hour),
        End = day.AddHours(hour + hours),
        ShiftName = name,
        Employees = [.. crew],
        Color = color
    };
}
