namespace Nx.Demo.Data;

/// <summary>The dialog demo's "New crew" form.</summary>
public sealed class CrewDraft
{
    public string? Name { get; set; }
    public int Size { get; set; } = 4;
    public bool Lead { get; set; }
}
