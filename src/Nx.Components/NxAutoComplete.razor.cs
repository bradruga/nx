using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// A text box that suggests: the value is the typed text, and once <see cref="MinLength"/> characters are typed a list
/// of matching items opens; picking one puts its text in the field. Built on the <see cref="NxComboBox{TItem, TValue}"/>
/// engine, so it has the same data provider, templates, grouping, virtualization and validation. Like the other Nx
/// inputs it commits on Enter, on picking and on leaving the field.
/// </summary>
/// <typeparam name="TItem">The suggestion type; <c>string</c> for a plain list of words.</typeparam>
/// <example>
/// <code>
/// &lt;NxAutoComplete Data="cities" @bind-Value="site.City" Placeholder="City" /&gt;
/// &lt;NxAutoComplete Data="places" ItemText="p =&gt; p.City" @bind-Value="site.City" MinLength="2" /&gt;
/// </code>
/// </example>
[CascadingTypeParameter(nameof(TItem))]
public partial class NxAutoComplete<TItem>
{
    private NxComboBox<TItem, string>? combo;
    private ComboMode mode = new() { Suggest = true };

    // ── Data ──────────────────────────────────────────────────────────────────

    /// <summary>The suggestions. Filtered in memory as the user types.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>Loads the suggestions for the typed text instead of <see cref="Data"/>, after <see cref="DebounceDelay"/>.</summary>
    [Parameter] public Func<NxComboBoxRequest, Task<IEnumerable<TItem>>>? DataProvider { get; set; }

    /// <summary>Milliseconds to wait after typing before calling <see cref="DataProvider"/>. Default: <c>300</c>.</summary>
    [Parameter] public int DebounceDelay { get; set; } = 300;

    /// <summary>The text of a suggestion, put in the field when picked and matched against the typed text. Default: <c>ToString()</c>.</summary>
    [Parameter] public Func<TItem, string>? ItemText { get; set; }

    /// <summary>Groups the suggestions under headers by this key.</summary>
    [Parameter] public Func<TItem, string?>? ItemGroup { get; set; }

    // ── Value ─────────────────────────────────────────────────────────────────

    /// <summary>The text. Use <c>@bind-Value</c>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised when the text is committed: on picking a suggestion, on Enter, on leaving the field.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Identifies the bound field for <c>EditForm</c> validation. Set by <c>@bind-Value</c>.</summary>
    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

    // ── Matching ──────────────────────────────────────────────────────────────

    /// <summary>How typed text matches a suggestion's text. Default: <see cref="NxComboBoxFilter.Contains"/>.</summary>
    [Parameter] public NxComboBoxFilter Filter { get; set; } = NxComboBoxFilter.Contains;

    /// <summary>Your own match test (item, typed text); replaces <see cref="Filter"/>.</summary>
    [Parameter] public Func<TItem, string, bool>? CustomFilter { get; set; }

    /// <summary>Characters to type before suggestions show. Default: <c>1</c>.</summary>
    [Parameter] public int MinLength { get; set; } = 1;

    // ── Presentation ──────────────────────────────────────────────────────────

    /// <summary>Text shown while the field is empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Shows a clear button while there is text.</summary>
    [Parameter] public bool Clearable { get; set; }

    /// <summary>Content in the field before the text, e.g. <c>&lt;NxIcon Icon="NxIcons.Search" /&gt;</c>.</summary>
    [Parameter] public RenderFragment? Prefix { get; set; }

    /// <summary>Renders each suggestion. Default: its text, with the typed part marked.</summary>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>Renders a group header. Default: the group key.</summary>
    [Parameter] public RenderFragment<string>? GroupHeaderTemplate { get; set; }

    /// <summary>Width of the list as a CSS length, e.g. <c>500px</c>. Default: as wide as the field.</summary>
    [Parameter] public string? PopupWidth { get; set; }

    /// <summary>Suggestion count from which the list renders only the rows in view, all one height. Default: <c>200</c>.</summary>
    [Parameter] public int VirtualizeThreshold { get; set; } = 200;

    /// <summary>Row height in pixels of a virtualized list. Default: measured from the rendered rows.</summary>
    [Parameter] public float? ItemHeight { get; set; }

    /// <summary>Largest list height in pixels. Default: <c>300</c>.</summary>
    [Parameter] public int PopupMaxHeight { get; set; } = 300;

    // ── State and attributes ──────────────────────────────────────────────────

    /// <summary>Shows the loading state.</summary>
    [Parameter] public bool Loading { get; set; }

    /// <summary>Renders the native <c>disabled</c> attribute.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Renders the native <c>readonly</c> attribute; nothing is suggested.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>The input's <c>id</c>, for a <c>&lt;label for&gt;</c>. Generated when not set.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Any other attributes (<c>name</c>, <c>aria-label</c>…) go to the input.</summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary><c>true</c> while suggestions are shown.</summary>
    public bool IsOpen => combo?.IsOpen ?? false;

    /// <summary>Moves keyboard focus to the input.</summary>
    public ValueTask FocusAsync() => combo?.FocusAsync() ?? ValueTask.CompletedTask;

    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-autocomplete" : $"nx-autocomplete {Class}";

    /// <summary>A picked suggestion's value is its text.</summary>
    private string TextOf(TItem item) => ItemText?.Invoke(item) ?? item?.ToString() ?? "";

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var next = new ComboMode { Suggest = true, MinLength = Math.Max(1, MinLength), Prefix = Prefix };
        if (next != mode)
            mode = next;
    }
}
