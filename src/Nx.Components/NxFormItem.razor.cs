using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Nx.Components;

/// <summary>
/// One field of an <see cref="NxForm"/>: a label, an editor and the field's validation messages, spanning
/// <see cref="ColSpan"/> columns. With <see cref="For"/> alone it renders an Nx editor for the property's type
/// (text, number, check box, date, time, enum); put your own editor inside it for anything else.
/// </summary>
/// <example>
/// <code>
/// &lt;NxFormItem For="() =&gt; order.PourDate" /&gt;
/// &lt;NxFormItem For="() =&gt; order.CustomerId" Label="Customer" Id="customer"&gt;
///     &lt;NxComboBox Id="customer" Data="customers" … @bind-Value="order.CustomerId" /&gt;
/// &lt;/NxFormItem&gt;
/// </code>
/// </example>
public partial class NxFormItem : IDisposable
{
    private EditContext? watched;
    private ValidationMessageStore? nestedMessages;

    private object? model;          // the object that owns the property: the form's model, or one it holds
    private PropertyInfo? property;
    private string? path;           // the property's names from the form's model, for the editor's id

    [CascadingParameter] private NxForm? Form { get; set; }
    [CascadingParameter] private NxFormGroup? Group { get; set; }
    [CascadingParameter] private EditContext? EditContext { get; set; }

    /// <summary>The field, as a lambda (<c>For="() =&gt; order.PourDate"</c>, or <c>() =&gt; order.Site.City</c>): picks the editor, the default label and the messages shown.</summary>
    [Parameter] public Expression<Func<object?>>? For { get; set; }

    /// <summary>The label. Default: the property's <c>[Display(Name)]</c>, else its name in words; none for your own editor.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>The editor's <c>id</c>, which the label points at. Generated editors default to <c>{form id}-{property}</c> (<c>{form id}-Site-City</c> for a nested one); give your own editor the same id.</summary>
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
    private string? EditorId => Id ?? (path is null ? null : $"{Form?.FormId ?? "nx-form"}-{path}");

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
        (model, property, path) = (null, null, null);
        if (For is null || EditContext?.Model is not { } formModel)
            return;
        // A value type comes boxed: () => (object)order.Quantity.
        var body = For.Body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } boxed ? boxed.Operand : For.Body;
        if (body is not MemberExpression { Member: PropertyInfo info } member)
            throw new InvalidOperationException("NxFormItem's For must name a property: For=\"() => order.PourDate\".");
        property = info;
        model = Evaluate(member.Expression);
        // The id's path starts at the form's model: () => order.Site.City gives Site-City.
        path = info.Name;
        var fromHere = path;
        for (var e = member.Expression; e is not null; e = (e as MemberExpression)?.Expression)
        {
            if (ReferenceEquals(Evaluate(e), formModel))
            {
                path = fromHere;
                break;
            }
            if (e is MemberExpression parent)
                fromHere = $"{parent.Member.Name}-{fromHere}";
        }
        if (ChildContent is null && !FormEditors.IsSupported(property.PropertyType))
            throw new InvalidOperationException(
                $"NxFormItem has no editor for {property.PropertyType.Name} ('{property.Name}'); put one inside the item.");
        Watch();
    }

    // DataAnnotationsValidator checks only the form model's own properties on submit, so a nested field is checked here.
    private void Watch()
    {
        if (ReferenceEquals(watched, EditContext))
            return;
        Unwatch();
        watched = EditContext;
        if (watched is null)
            return;
        nestedMessages = new ValidationMessageStore(watched);
        watched.OnValidationRequested += HandleValidationRequested;
        watched.OnFieldChanged += HandleFieldChanged;
    }

    private void Unwatch()
    {
        if (watched is null)
            return;
        watched.OnValidationRequested -= HandleValidationRequested;
        watched.OnFieldChanged -= HandleFieldChanged;
        nestedMessages?.Clear();
        watched = null;
    }

    private bool IsNested => model is not null && property is not null && watched is not null
        && !ReferenceEquals(model, watched.Model) && Form?.Validation is null;

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        nestedMessages!.Clear();
        if (!IsNested)
            return;
        var results = new List<ValidationResult>();
        Validator.TryValidateProperty(property!.GetValue(model), new ValidationContext(model!) { MemberName = property.Name }, results);
        var field = new FieldIdentifier(model!, property.Name);
        foreach (var result in results)
            nestedMessages.Add(field, result.ErrorMessage ?? "");
        watched!.NotifyValidationStateChanged();
    }

    // An edit is validated by DataAnnotationsValidator itself; the message from the last submit goes.
    private void HandleFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        if (IsNested && e.FieldIdentifier.Equals(new FieldIdentifier(model!, property!.Name)))
            nestedMessages!.Clear(e.FieldIdentifier);
    }

    /// <inheritdoc />
    public void Dispose() => Unwatch();

    // Reads the object a lambda's member chain ends at, without compiling the lambda on every render.
    private static object? Evaluate(Expression? expression)
    {
        if (expression is null or ConstantExpression)
            return (expression as ConstantExpression)?.Value;
        if (expression is not MemberExpression member)
            return Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object))).Compile()();
        var owner = Evaluate(member.Expression);
        if (owner is null && member.Expression is not null)
            return null;    // an object on the way is not set
        return member.Member is PropertyInfo p ? p.GetValue(owner) : ((FieldInfo)member.Member).GetValue(owner);
    }
}
