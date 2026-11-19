using Microsoft.AspNetCore.Components;

namespace Nx.Components;

/// <summary>
/// A titled set of <see cref="NxFormItem"/>s inside an <see cref="NxForm"/>: a <c>&lt;fieldset&gt;</c> with a legend
/// and its own grid of <see cref="Columns"/>. It spans the form's full width.
/// </summary>
/// <example>
/// <code>
/// &lt;NxFormGroup Label="Site contact" Columns="2"&gt;
///     &lt;NxFormItem For="() =&gt; site.ContactName" /&gt;
///     &lt;NxFormItem For="() =&gt; site.ContactPhone" /&gt;
/// &lt;/NxFormGroup&gt;
/// </code>
/// </example>
public partial class NxFormGroup
{
    [CascadingParameter] private NxForm? Form { get; set; }

    /// <summary>The legend.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Grid columns inside the group. Default: the form's <see cref="NxForm.Columns"/>.</summary>
    [Parameter] public int? Columns { get; set; }

    /// <summary>Space between columns, as CSS. Default: the form's <see cref="NxForm.ColumnSpacing"/>.</summary>
    [Parameter] public string? ColumnSpacing { get; set; }

    /// <summary>The group's items.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Extra CSS class on the fieldset.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Inline style on the fieldset.</summary>
    [Parameter] public string? Style { get; set; }

    internal int EffectiveColumns => Math.Max(1, Columns ?? Form?.Columns ?? 1);

    private string CssClass => string.IsNullOrEmpty(Class) ? "nx-form-group" : $"nx-form-group {Class}";
}
