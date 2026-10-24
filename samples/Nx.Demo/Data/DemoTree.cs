namespace Nx.Demo.Data;

/// <summary>A flat tree row, as ERP's permission tree comes: an id, a parent id and the text.</summary>
public sealed record PermissionNode(string Id, string? ParentId, string Text, string? Permission = null, int Users = 0);

/// <summary>A place that holds forms and other places, for the drag and drop demo (ERP's resource locations).</summary>
public sealed class Location(int id, int? containerId, string name, string kind)
{
    public int Id { get; } = id;
    public int? ContainerId { get; set; } = containerId;
    public string Name { get; } = name;
    public string Kind { get; } = kind;
    public int Order { get; set; } = id;
}

/// <summary>A nested tree node, for the Children binding.</summary>
public sealed record Folder(string Name, IReadOnlyList<Folder>? Children = null);

public static class DemoTree
{
    public static IReadOnlyList<PermissionNode> Permissions { get; } =
    [
        new("acc", null, "Accounting"),
        new("acc.bills", "acc", "Bills"),
        new("acc.bills.view", "acc.bills", "View bills", "Acc.Bills.View", 14),
        new("acc.bills.edit", "acc.bills", "Edit bills", "Acc.Bills.Edit", 5),
        new("acc.bills.pay", "acc.bills", "Pay bills", "Acc.Bills.Pay", 2),
        new("acc.inv", "acc", "Invoices"),
        new("acc.inv.view", "acc.inv", "View invoices", "Acc.Invoices.View", 11),
        new("acc.inv.send", "acc.inv", "Send invoices", "Acc.Invoices.Send", 3),
        new("com", null, "Commercial"),
        new("com.proj", "com", "Projects"),
        new("com.proj.view", "com.proj", "View projects", "Com.Projects.View", 42),
        new("com.proj.edit", "com.proj", "Edit projects", "Com.Projects.Edit", 9),
        new("com.sched", "com", "Production schedule"),
        new("com.sched.view", "com.sched", "View the schedule", "Com.Schedule.View", 37),
        new("com.sched.plan", "com.sched", "Plan pours", "Com.Schedule.Plan", 4),
        new("emp", null, "Employee"),
        new("emp.docs", "emp", "Documents"),
        new("emp.docs.i9", "emp.docs", "I-9 documents", "Emp.Documents.I9", 2),
        new("emp.time", "emp", "Time cards", "Emp.TimeCards.Edit", 6),
    ];

    public static List<Location> Locations() =>
    [
        new(1, null, "Main yard", "yard"),
        new(2, 1, "Bay A", "bay"),
        new(3, 1, "Bay B", "bay"),
        new(4, 2, "Wall form 12", "form"),
        new(5, 2, "Wall form 14", "form"),
        new(6, 3, "Deck form 3", "form"),
        new(7, null, "North lot", "yard"),
        new(8, 7, "Rack 1", "bay"),
        new(9, 8, "Column form 2", "form"),
        new(10, null, "Repair shop", "yard"),
    ];

    public static IReadOnlyList<Folder> Drawings { get; } =
    [
        new("Riverside Garage",
        [
            new("Erection", [new("E-101 Level 1"), new("E-102 Level 2"), new("E-103 Roof")]),
            new("Production", [new("P-201 Double tees"), new("P-202 Columns")]),
        ]),
        new("Harbor Lofts", [new("Erection", [new("E-101 Podium")])]),
    ];
}
