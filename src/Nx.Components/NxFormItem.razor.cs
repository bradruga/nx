using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>
/// One field of an <see cref="NxForm"/>: a label, an editor and the field's validation messages, spanning
/// <see cref="ColSpan"/> columns. With <see cref="Field"/> alone it renders an Nx editor for the property's type
/// (text, number, check box, date, time, enum); put your own editor inside it for anything else.
/// </summary>
/// <example>
/// <code>
/// &lt;NxFormItem Field="@@nameof(Order.PourDate)" /&gt;
/// &lt;NxFormItem Field="@@nameof(Order.CustomerId)" Label="Customer" Id="customer"&gt;
///     &lt;NxComboBox Id="customer" Data="customers" … @bind-Value="order.CustomerId" /&gt;
/// &lt;/NxFormItem&gt;
/// </code>
/// </example>
public partial class NxFormItem
{
    private object? model;
    private PropertyInfo? property;

    [CascadingParameter] private NxForm? Form { get; set; }
    [CascadingParameter] private NxFormGroup? Group { get; set; }
    [CascadingParameter] private EditContext? EditContext { get; set; }

    /// <summary>A property of the form's model, by name (<c>@@nameof(Order.PourDate)</c>): picks the editor, the default label and the messages shown.</summary>
    [Parameter] public string? Field { get; set; }

    /// <summary>The label. Default: the property's <c>[Display(Name)]</c>, else its name in words; none for your own editor.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>The editor's <c>id</c>, which the label points at. Generated editors default to <c>{form id}-{Field}</c>; give your own editor the same id.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Grid columns the item spans, up to the form's or group's <c>Columns</c>. Default: <c>1</c>.</summary>
    [Parameter] public int ColSpan { get; set; } = 1;

    /// <summary>Disables the generated editor.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Your own editor, instead of the generated one.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Extra CSS class on the item.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the item.</summary>
    [Parameter] public string? Style { get; set; }

    private bool Horizontal => Form?.Orientation == NxOrientation.Horizontal;
    private int Columns => Group?.EffectiveColumns ?? Math.Max(1, Form?.Columns ?? 1);
    private string? EditorId => Id ?? (Field is null ? null : $"{Form?.FormId ?? "nx-form"}-{Field}");

    /// <summary>A generated check box carries the label itself, after the box.</summary>
    private bool IsCheckBox => ChildContent is null && property is not null && FormEditors.IsCheckBox(property.PropertyType);

    private string? LabelText => Label ?? (ChildContent is null && property is not null ? FormLayout.LabelOf(property) : null);
    private string? CheckBoxLabel => IsCheckBox ? LabelText : null;
    private bool ShowLabel => LabelText is not null && !IsCheckBox;
    private string? LabelFor => ChildContent is null || Id is not null ? EditorId : null;

    private string ItemStyle
    {
        get
        {
            var span = $"--nx-form-span: {Math.Clamp(ColSpan, 1, Columns).ToString(CultureInfo.InvariantCulture)}";
            return string.IsNullOrEmpty(Style) ? span : $"{span}; {Style}";
        }
    }

    private string CssClass => string.Join(' ', new[]
    {
        "nx-form-item",
        Horizontal ? "nx-form-item-horizontal" : null,
        IsCheckBox ? "nx-form-item-checkbox" : null,
        Class
    }.Where(c => !string.IsNullOrEmpty(c)));

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        model = EditContext?.Model;
        property = null;
        if (Field is null || model is null)
            return;
        property = model.GetType().GetProperty(Field, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"NxFormItem: {model.GetType().Name} has no public property '{Field}'.");
        if (ChildContent is null && !FormEditors.IsSupported(property.PropertyType))
            throw new InvalidOperationException(
                $"NxFormItem has no editor for {property.PropertyType.Name} ('{Field}'); put one inside the item.");
    }
}
