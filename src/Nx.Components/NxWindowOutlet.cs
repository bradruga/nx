using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>One window's place in <see cref="NxWindowHost"/>: draws that window's markup, again whenever the window renders.</summary>
internal sealed class NxWindowOutlet : IComponent
{
    private RenderHandle handle;
    private NxWindow? window;

    /// <summary>The window drawn here.</summary>
    [Parameter] public NxWindow? Window { get; set; }

    void IComponent.Attach(RenderHandle renderHandle) => handle = renderHandle;

    // The host re-renders when any window opens or closes; an outlet whose window is the same has nothing new to draw.
    Task IComponent.SetParametersAsync(ParameterView parameters)
    {
        var next = parameters.GetValueOrDefault<NxWindow>(nameof(Window));
        if (next is not null && next != window)
        {
            window = next;
            window.Attach(this);
            Refresh();
        }
        return Task.CompletedTask;
    }

    internal void Refresh()
    {
        if (window is not null)
            handle.Render(window.RenderInHost);
    }
}
