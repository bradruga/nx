using System.Text.Json;
using Microsoft.JSInterop;

namespace Nx.Demo.Data;

/// <summary>
/// Per circuit: the components visited lately and the ones pinned, kept in localStorage (through demo-search.js, which
/// never throws), and a way for any page to open the site search.
/// </summary>
public sealed class SiteSearchState(IJSRuntime js)
{
    private const string RecentKey = "nx-demo-recent", PinnedKey = "nx-demo-pinned";
    private const int MaxRecent = 6;

    private List<string> recent = [], pinned = [];
    private Task? loading;

    /// <summary>Raised when Recent or Pinned changes.</summary>
    public event Action? Changed;

    /// <summary>Raised by <see cref="RequestOpen"/>; the search in the top bar opens.</summary>
    public event Action? OpenRequested;

    /// <summary>The components visited lately, the latest first.</summary>
    public IReadOnlyList<DemoComponent> Recent => Resolve(recent);

    /// <summary>The pinned components, in the order they were pinned.</summary>
    public IReadOnlyList<DemoComponent> Pinned => Resolve(pinned);

    public bool IsPinned(string slug) => pinned.Contains(slug);

    public void RequestOpen() => OpenRequested?.Invoke();

    /// <summary>Reads both lists once; call after the first interactive render.</summary>
    public Task LoadAsync() => loading ??= LoadCoreAsync();

    public async Task VisitAsync(string slug)
    {
        await LoadAsync();
        if (DemoCatalog.Find(slug) is null || recent.FirstOrDefault() == slug)
            return;
        recent.Remove(slug);
        recent.Insert(0, slug);
        if (recent.Count > MaxRecent)
            recent.RemoveRange(MaxRecent, recent.Count - MaxRecent);
        await SaveAsync(RecentKey, recent);
    }

    public async Task TogglePinAsync(string slug)
    {
        await LoadAsync();
        if (!pinned.Remove(slug))
            pinned.Add(slug);
        await SaveAsync(PinnedKey, pinned);
    }

    private async Task LoadCoreAsync()
    {
        recent = await ReadAsync(RecentKey);
        pinned = await ReadAsync(PinnedKey);
        Changed?.Invoke();
    }

    private async Task<List<string>> ReadAsync(string key)
    {
        try
        {
            var json = await js.InvokeAsync<string?>("nxDemoStore.get", key);
            return json is null ? [] : JsonSerializer.Deserialize<List<string>>(json)?.Where(s => DemoCatalog.Find(s) is not null).Distinct().ToList() ?? [];
        }
        catch (Exception e) when (e is JSException or JsonException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            return [];
        }
    }

    private async Task SaveAsync(string key, List<string> slugs)
    {
        Changed?.Invoke();
        try
        {
            await js.InvokeVoidAsync("nxDemoStore.set", key, JsonSerializer.Serialize(slugs));
        }
        catch (Exception e) when (e is JSException or JSDisconnectedException or TaskCanceledException)
        {
        }
    }

    private static List<DemoComponent> Resolve(List<string> slugs) => slugs.Select(DemoCatalog.Find).OfType<DemoComponent>().ToList();
}
