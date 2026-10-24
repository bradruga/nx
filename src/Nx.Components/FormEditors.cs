using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Nx.Components;

/// <summary>
/// The editors an <see cref="NxFormItem"/> generates for a property: NxTextBox, NxNumericTextBox, NxCheckBox,
/// NxDatePicker, NxTimePicker or NxDropDown (enums), bound to the property like <c>@bind-Value</c>.
/// </summary>
internal static class FormEditors
{
    private static readonly MethodInfo RenderTypedMethod =
        typeof(FormEditors).GetMethod(nameof(RenderTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo AddEnumParametersMethod =
        typeof(FormEditors).GetMethod(nameof(AddEnumParameters), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Type Underlying(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    public static bool IsCheckBox(Type type) => Underlying(type) == typeof(bool);

    public static bool IsSupported(Type type) => EditorFor(type) is not null;

    private static Type? EditorFor(Type type)
    {
        var u = Underlying(type);
        if (type == typeof(string))
            return typeof(NxTextBox);
        if (u == typeof(bool))
            return typeof(NxCheckBox<>).MakeGenericType(type);
        if (u == typeof(int) || u == typeof(long) || u == typeof(short) || u == typeof(decimal) || u == typeof(double) || u == typeof(float))
            return typeof(NxNumericTextBox<>).MakeGenericType(type);
        if (u == typeof(DateTime) || u == typeof(DateOnly))
            return typeof(NxDatePicker<>).MakeGenericType(type);
        if (u == typeof(TimeOnly) || u == typeof(TimeSpan))
            return typeof(NxTimePicker<>).MakeGenericType(type);
        if (u.IsEnum)
            return typeof(NxDropDown<,>).MakeGenericType(u, type);
        return null;
    }

    /// <summary>The editor for <paramref name="property"/> of <paramref name="model"/>; <paramref name="receiver"/> re-renders on change.</summary>
    public static RenderFragment Render(IHandleEvent receiver, object model, PropertyInfo property, string? id, string? label, bool disabled) =>
        (RenderFragment)RenderTypedMethod.MakeGenericMethod(property.PropertyType)
            .Invoke(null, [receiver, model, property, id, label, disabled])!;

    private static RenderFragment RenderTyped<T>(IHandleEvent receiver, object model, PropertyInfo property, string? id, string? label, bool disabled) => builder =>
    {
        var type = typeof(T);
        builder.OpenComponent(0, EditorFor(type)!);
        builder.AddComponentParameter(1, "Value", (T?)property.GetValue(model));
        builder.AddComponentParameter(2, "ValueChanged", EventCallback.Factory.Create<T?>(receiver, value => property.SetValue(model, value)));
        builder.AddComponentParameter(3, "ValueExpression", Expression.Lambda<Func<T?>>(Expression.Property(Expression.Constant(model), property)));
        if (id is not null)
            builder.AddComponentParameter(4, "Id", id);
        if (disabled)
            builder.AddComponentParameter(5, "Disabled", true);
        if (label is not null)
            builder.AddComponentParameter(6, "Label", label);
        if (Underlying(type).IsEnum)
            AddEnumParametersMethod.MakeGenericMethod(Underlying(type), type).Invoke(null, [builder]);
        builder.CloseComponent();
    };

    /// <summary>An enum's NxDropDown lists its values in declared order, named in words; a nullable one can be cleared.</summary>
    private static void AddEnumParameters<TEnum, T>(RenderTreeBuilder builder) where TEnum : struct, Enum
    {
        builder.AddComponentParameter(7, "Data", Enum.GetValues<TEnum>());
        builder.AddComponentParameter(8, "ItemText", (Func<TEnum, string>)(e => FormLayout.Words(e.ToString())));
        builder.AddComponentParameter(9, "ItemValue", (Func<TEnum, T>)(e => (T)(object)e));
        if (Nullable.GetUnderlyingType(typeof(T)) is not null)
            builder.AddComponentParameter(10, "Clearable", true);
    }
}
