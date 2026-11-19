using System.Globalization;
using Microsoft.JSInterop;

namespace Nx.Components;

// A list of VirtualizeThreshold rows or more renders through <Virtualize>, which needs one row height.
// ItemHeight gives it; otherwise the first such list renders its first rows plainly (hidden), nx-combobox.js
// measures the tallest, and the list renders again virtualized with every row pinned to that height.
public partial class NxComboBox<TItem, TValue>
{
    private const int ProbeCount = 20;
    private const float EstimatedRowHeight = 32;

    private float? measuredPopupHeight;
    private float? measuredOverlayHeight;   // the overlay's rows are taller
    private bool measureFailed;             // nothing could be measured; not tried again until the list reopens

    private bool ShouldVirtualize => rows.Count >= VirtualizeThreshold;

    /// <summary>The height every row is pinned to, or <c>null</c> while it is not known.</summary>
    private float? PinnedRowHeight => ItemHeight ?? (overlayOpen ? measuredOverlayHeight : measuredPopupHeight);

    /// <summary>Without a measurement the rows keep their own height and Virtualize corrects this estimate from them.</summary>
    private float RowHeight => PinnedRowHeight ?? EstimatedRowHeight;

    /// <summary>This render shows the first rows only, for measuring. Never without JS: nothing would replace it.</summary>
    private bool Probing => ShouldVirtualize && PinnedRowHeight is null && !measureFailed && module is not null;

    private string ItemStyle => ShouldVirtualize && PinnedRowHeight is float height
        ? $"height: {height.ToString(CultureInfo.InvariantCulture)}px"
        : "";

    private async Task MeasureItemHeightAsync()
    {
        var overlay = overlayOpen;
        var height = await module!.InvokeAsync<float>("measureItemHeight", overlay ? overlayList : list);
        if (height <= 0)
            measureFailed = true;
        else if (overlay)
            measuredOverlayHeight = height;
        else
            measuredPopupHeight = height;
        StateHasChanged();
    }
}
