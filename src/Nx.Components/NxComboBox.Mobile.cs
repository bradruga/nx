using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Nx.Components;

public partial class NxComboBox<TItem, TValue>
{
    private ElementReference overlayInput;
    private ElementReference overlayList;
    private bool isNarrow;
    private readonly TaskCompletionSource viewportKnown = new();

    // A tap in the first moments after the page loads waits for the browser's answer, so a phone gets its sheet.
    private Task ViewportKnownAsync() => MobileMode != NxMobileMode.Auto || viewportKnown.Task.IsCompleted
        ? Task.CompletedTask
        : Task.WhenAny(viewportKnown.Task, Task.Delay(3000));
    private bool overlayOpen;
    private string overlayText = "";
    private bool overlayFocusPending;
    private bool scrollLockPending;
    private bool returnFocusPending;

    /// <summary>Whether a tap opens the full-screen overlay rather than the popup.</summary>
    private bool UseOverlay => MobileMode switch
    {
        NxMobileMode.Always => true,
        NxMobileMode.Never  => false,
        _                   => isNarrow
    };

    private string OverlayTitle => MobileTitle ?? Placeholder ?? "Search";

    /// <summary>Called by nx-combobox.js when the viewport crosses <see cref="MobileBreakpoint"/>. Not for application use.</summary>
    [JSInvokable]
    public void OnViewportChanged(bool narrow)
    {
        if (narrow == isNarrow)
            return;
        isNarrow = narrow;
        InvokeAsync(StateHasChanged);
    }

    private async Task OpenOverlayAsync()
    {
        if (!Editable || isOpen)
            return;
        isOpen = true;
        overlayOpen = true;
        overlayText = "";
        isFiltering = false;
        staged = Multiple ? [.. chips] : null;
        overlayFocusPending = true;
        scrollLockPending = true;
        await RefreshListAsync(debounce: false);
    }

    /// <summary>Back, Escape and (with <see cref="Multiple"/>) Done. Only Done commits the working selection.</summary>
    private async Task CloseOverlayAsync(bool commit)
    {
        var working = staged;
        Close();
        returnFocusPending = true;
        if (commit && Multiple && working is not null && !working.SequenceEqual(chips))
        {
            chips = working;
            await NotifyMultipleAsync();
        }
    }

    /// <summary>The overlay's part of <see cref="Close"/>.</summary>
    private void CloseOverlayState()
    {
        overlayOpen = false;
        overlayText = "";
        staged = null;
        scrollLockPending = true;
    }

    private async Task HandleOverlayInputAfter()
    {
        overlayText ??= "";
        isFiltering = true;
        await RefreshListAsync(debounce: true);
    }

    private async Task HandleOverlayKeyDown(KeyboardEventArgs e) => await HandleListKeyAsync(e);

    /// <summary>Pick-only overlays have no search box; the list takes the keys, Space and Enter included.</summary>
    private async Task HandleOverlayListKeyDown(KeyboardEventArgs e)
    {
        if (!PickOnly)
            return;
        if (e.Key == " " && HasActiveItem)
            await ActivateRowAsync(rows[activeRow]);
        else
            await HandleListKeyAsync(e);
    }

    private Task HandleOverlayBack() => CloseOverlayAsync(commit: false);

    private Task HandleOverlayDone() => CloseOverlayAsync(commit: true);

    private async Task AfterRenderOverlayAsync()
    {
        if (module is null)
            return;
        if (scrollLockPending)
        {
            scrollLockPending = false;
            await module.InvokeVoidAsync("lockScroll", overlayOpen);
        }
        if (overlayFocusPending && overlayOpen)
        {
            overlayFocusPending = false;
            if (PickOnly)
            {
                // No search box when nothing is typed: the list itself takes focus and the keys.
                await overlayList.FocusAsync();
            }
            else
            {
                await module.InvokeVoidAsync("init", overlayInput);
                await overlayInput.FocusAsync();
            }
            scrollPending = true;
        }
        if (returnFocusPending && !overlayOpen)
        {
            returnFocusPending = false;
            await input.FocusAsync();
        }
    }
}
