using Nx.Components;

namespace Nx.Demo.Data;

/// <summary>A project schedule task, flat, as ERP's Gantt tasks come.</summary>
public sealed class ScheduleTask
{
    public int Id { get; init; }
    public int? ParentId { get; init; }
    public string Title { get; set; } = "";
    public DateTime Start { get; set; }
    /// <summary>The day after the last day, as ERP's chart end.</summary>
    public DateTime End { get; set; }
    public string Resources { get; set; } = "";
    public string Color { get; init; } = "";
    public int Days => (int)Math.Round((End - Start).TotalDays);
}

/// <summary>A precast project: design, production and erection, starting this week.</summary>
public static class DemoGantt
{
    public static List<ScheduleTask> Tasks()
    {
        var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        var id = 1;
        var list = new List<ScheduleTask>();
        ScheduleTask Add(int? parent, string title, int startDay, int days, string resources = "", string color = "")
        {
            var task = new ScheduleTask { Id = id++, ParentId = parent, Title = title, Start = monday.AddDays(startDay), End = monday.AddDays(startDay + days), Resources = resources, Color = color };
            list.Add(task);
            return task;
        }
        var design = Add(null, "Design", 0, 12);
        Add(design.Id, "Shop drawings", 0, 8, "Drafting", "#2e6db4");
        Add(design.Id, "Engineer review", 8, 4, "PE", "#2e6db4");
        var production = Add(null, "Production", 12, 18);
        Add(production.Id, "Forms set", 12, 3, "Crew A", "#1e8c8c");
        Add(production.Id, "Pour wall panels", 15, 10, "Crew A, Crew B", "#1e8c8c");
        Add(production.Id, "Cure and strip", 25, 5, "Crew B", "#1e8c8c");
        var erection = Add(null, "Erection", 30, 9);
        Add(erection.Id, "Ship to site", 30, 2, "Trucking", "#d97a1e");
        Add(erection.Id, "Set panels", 32, 7, "Erection crew", "#d97a1e");
        return list;
    }

    /// <summary>Summary tasks span their children, as ERP recalculates its chart after an edit.</summary>
    public static void Rollup(List<ScheduleTask> tasks)
    {
        foreach (var parent in tasks.Where(p => tasks.Any(c => c.ParentId == p.Id)).Reverse())
        {
            var children = tasks.Where(c => c.ParentId == parent.Id).ToList();
            parent.Start = children.Min(c => c.Start);
            parent.End = children.Max(c => c.End);
        }
    }

    public static List<NxGanttDependency> Links() =>
    [
        new(2, 3), new(3, 5), new(5, 6), new(6, 7), new(7, 9), new(9, 10)
    ];
}
