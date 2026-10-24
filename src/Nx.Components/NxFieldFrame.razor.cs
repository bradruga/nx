using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// The shared chrome of Nx text inputs — label or floating label, bordered box, prefix and suffix
/// slots, clear button — used by <see cref="NxTextBox"/>, <see cref="NxNumericTextBox{TValue}"/>, <see cref="NxTextArea"/> and the pickers.
/// A building block for new Nx inputs; applications use those components instead.
/// </summary>
public partial class NxFieldFrame
{
    /// <summary>The host's class prefix, e.g. <c>nx-textbox</c>: every part also gets <c>{prefix}-{part}</c>.</summary>
    [Parameter, EditorRequired] public string ClassPrefix { get; set; } = "";

    /// <summary>The input's id, which the label targets.</summary>
    [Parameter, EditorRequired] public string InputId { get; set; } = "";

    /// <summary>Label text.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Renders the label inside the box, lifted on focus or when <see cref="HasValue"/>.</summary>
    [Parameter] public bool FloatingLabel { get; set; }

    /// <summary>Whether the input has text: keeps a floating label lifted.</summary>
    [Parameter] public bool HasValue { get; set; }

    /// <summary>Content inside the box before the input.</summary>
    [Parameter] public RenderFragment? PrefixContent { get; set; }

    /// <summary>Content inside the box after the input.</summary>
    [Parameter] public RenderFragment? SuffixContent { get; set; }

    /// <summary>Shows the clear button.</summary>
    [Parameter] public bool ShowClear { get; set; }

    /// <summary>Raised by the clear button.</summary>
    [Parameter] public EventCallback OnClear { get; set; }

    /// <summary>The host's own buttons, after the clear button.</summary>
    [Parameter] public RenderFragment? Buttons { get; set; }

    /// <summary>Content under the box, e.g. a counter.</summary>
    [Parameter] public RenderFragment? Below { get; set; }

    /// <summary>The input itself.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Disabled styling.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Read-only styling.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Invalid styling.</summary>
    [Parameter] public bool Invalid { get; set; }

    /// <summary>A multi-line control (a <c>&lt;textarea&gt;</c>): the box grows with it, buttons sit at the top.</summary>
    [Parameter] public bool Multiline { get; set; }

    /// <summary>Extra CSS class on the outer element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the outer element.</summary>
    [Parameter] public string? Style { get; set; }

    private string RootClass
    {
        get
        {
            var modifiers = new List<string>();
            void Add(bool on, string name)
            {
                if (!on)
                    return;
                modifiers.Add($"nx-field-{name}");
                modifiers.Add($"{ClassPrefix}-{name}");
            }
            Add(FloatingLabel && Label is not null, "floating");
            Add(HasValue, "has-value");
            Add(PrefixContent is not null, "has-prefix");
            Add(Disabled, "disabled");
            Add(ReadOnly, "readonly");
            Add(Invalid, "invalid");
            Add(Multiline, "multiline");
            return string.Join(' ', new[] { "nx-field", ClassPrefix }.Concat(modifiers).Append(Class ?? "").Where(c => c.Length > 0));
        }
    }
}
