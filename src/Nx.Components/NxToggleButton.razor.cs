using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Nx.Components;

/// <summary>
/// A button that stays on or off — Show deleted, Link selection, a side panel — bound with <c>@bind-Selected</c> and
/// announced with <c>aria-pressed</c>. Every <see cref="NxButton"/> variant, size and icon applies, and it joins an
/// <see cref="NxButtonGroup"/>. To pick one of several values, use <see cref="NxToggleGroup{TValue}"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;NxToggleButton @bind-Selected="showDeleted"&gt;Show deleted&lt;/NxToggleButton&gt;
/// </code>
/// </example>
public partial class NxToggleButton
{
    private bool selected;

    /// <summary>Whether the button is on. Use <c>@bind-Selected</c>.</summary>
    [Parameter] public bool Selected { get; set; }

    /// <summary>Raised with the new state when the button is pressed.</summary>
    [Parameter] public EventCallback<bool> SelectedChanged { get; set; }

    /// <summary>The label.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Icon markup, as NxButton's; without a label the button is icon-only and needs an <see cref="AriaLabel"/>.</summary>
    [Parameter] public RenderFragment? Icon { get; set; }

    /// <summary>Which side of the label the icon sits on. Default: <see cref="NxIconPosition.Start"/>.</summary>
    [Parameter] public NxIconPosition IconPosition { get; set; } = NxIconPosition.Start;

    /// <summary>Visual style, as NxButton's. Default: the group's, else <see cref="NxButtonVariant.Secondary"/>.</summary>
    [Parameter] public NxButtonVariant? Variant { get; set; }

    /// <summary>Size, as NxButton's. Default: the group's, else <see cref="NxButtonSize.Medium"/>.</summary>
    [Parameter] public NxButtonSize? Size { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Stretches the button to its container's width.</summary>
    [Parameter] public bool FullWidth { get; set; }

    /// <summary>Accessible name; required for an icon-only toggle.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Tooltip text (<c>title</c> attribute).</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Extra CSS class on the <c>&lt;button&gt;</c>.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the <c>&lt;button&gt;</c>.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>id</c>, <c>data-*</c>…) are passed to the <c>&lt;button&gt;</c>.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet() => selected = Selected;

    private IReadOnlyDictionary<string, object> ButtonParameters
    {
        get
        {
            var parameters = new Dictionary<string, object>();
            if (AdditionalAttributes is not null)
            {
                foreach (var (name, value) in AdditionalAttributes)
                    parameters[name] = value;
            }
            if (ChildContent is not null) parameters[nameof(NxButton.ChildContent)] = ChildContent;
            if (Icon is not null) parameters[nameof(NxButton.Icon)] = Icon;
            if (Variant is { } variant) parameters[nameof(NxButton.Variant)] = variant;
            if (Size is { } size) parameters[nameof(NxButton.Size)] = size;
            if (AriaLabel is not null) parameters[nameof(NxButton.AriaLabel)] = AriaLabel;
            if (Title is not null) parameters[nameof(NxButton.Title)] = Title;
            if (Class is not null) parameters[nameof(NxButton.Class)] = Class;
            if (Style is not null) parameters[nameof(NxButton.Style)] = Style;
            parameters[nameof(NxButton.IconPosition)] = IconPosition;
            parameters[nameof(NxButton.Disabled)] = Disabled;
            parameters[nameof(NxButton.FullWidth)] = FullWidth;
            parameters[nameof(NxButton.OnClick)] = EventCallback.Factory.Create<MouseEventArgs>(this, ToggleAsync);
            parameters["aria-pressed"] = selected ? "true" : "false";
            return parameters;
        }
    }

    private async Task ToggleAsync()
    {
        selected = !selected;
        await SelectedChanged.InvokeAsync(selected);
    }
}
