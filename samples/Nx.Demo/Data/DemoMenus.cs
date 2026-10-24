using Microsoft.AspNetCore.Components;
using Nx.Components;
using Nx.Demo.Components.Shared;

namespace Nx.Demo.Data;

/// <summary>Menu item lists the menu demos share.</summary>
public static class DemoMenus
{
    /// <summary>A demo icon as a render fragment, for <see cref="NxMenuItem.Icon"/>.</summary>
    public static RenderFragment Icon(string name) => b =>
    {
        b.OpenComponent<DemoIcon>(0);
        b.AddAttribute(1, nameof(DemoIcon.Name), name);
        b.CloseComponent();
    };

    /// <summary>Edit actions with icons and shortcut text, a separator and a disabled item.</summary>
    public static IReadOnlyList<NxMenuItem> EditItems() =>
    [
        new("Edit") { Id = "edit", Icon = Icon("edit"), Shortcut = "F2" },
        new("Duplicate") { Id = "duplicate", Icon = Icon("plus"), Shortcut = "Ctrl+D" },
        new("Download PDF") { Id = "download", Icon = Icon("download") },
        NxMenuItem.Separator(),
        new("Archive") { Id = "archive", Disabled = true },
        new("Delete") { Id = "delete", Icon = Icon("trash"), Shortcut = "Del" },
    ];

    /// <summary>Export formats.</summary>
    public static IReadOnlyList<NxMenuItem> ExportItems() =>
    [
        new("Excel (.xlsx)") { Id = "xlsx", Icon = Icon("download") },
        new("CSV") { Id = "csv", Icon = Icon("download") },
        new("PDF") { Id = "pdf", Icon = Icon("download") },
        NxMenuItem.Separator(),
        new("Email to…") { Id = "email", Icon = Icon("mail") },
    ];
}
