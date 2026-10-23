using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace NT.AvaloniaControl.TreeDataGrid;

public abstract partial class TreeDataGridColumn
{
    private Action<object, object?>? _valueSetter;
    private Type? _valueType;
    private Func<object?, object?>? _valueConverter;
    private Func<object?, string?>? _valueValidator;
    private Func<object, string?>? _textSearchValueSelector;
    private bool _isReadOnly;
    private bool _hasTypedValueAccessors;
    public Action<object, object?>? ValueSetter { get => _valueSetter; set => Set(ref _valueSetter, value); }
    public Type? ValueType { get => _valueType; set => Set(ref _valueType, value); }
    public Func<object?, object?>? ValueConverter { get => _valueConverter; set => Set(ref _valueConverter, value); }
    public Func<object?, string?>? ValueValidator { get => _valueValidator; set => Set(ref _valueValidator, value); }
    public Func<object, string?>? TextSearchValueSelector { get => _textSearchValueSelector; set => Set(ref _textSearchValueSelector, value); }
    public bool IsReadOnly { get => _isReadOnly; set => Set(ref _isReadOnly, value); }
    /// <summary>
    /// Configures typed staged edit accessors. A missing setter makes this accessor read-only.
    /// Display bindings remain available; these accessors read the underlying value for editing.
    /// A setter must complete successfully or throw before modifying its model.
    /// </summary>
    public void SetValueAccessors<TModel, TValue>(Func<TModel, TValue> getter, Action<TModel, TValue>? setter = null)
    {
        ArgumentNullException.ThrowIfNull(getter);
        var column = EffectiveColumn;
        column._hasTypedValueAccessors = true;
        column.ValueType = typeof(TValue);
        column.ValueGetter = model => getter((TModel)model);
        column.ValueSetter = setter is null ? null : (model, value) => setter((TModel)model, (TValue)value!);
    }
    internal TreeDataGridColumn EffectiveColumn => this is TreeDataGridHierarchicalExpanderColumn tree ? tree.InnerColumn : this;
    internal BindingBase? DisplayBinding => EffectiveColumn switch { TreeDataGridTextColumn text => text.Binding, TreeDataGridCheckBoxColumn check => check.Binding, _ => null };
    internal object? ReadValue(object model)
    {
        var column = EffectiveColumn;
        if (column._hasTypedValueAccessors && column.ValueGetter is { } typedGetter) return typedGetter(model);
        if (column.DisplayBinding is { } binding)
        {
            if (binding is ReflectionBinding reflection && IsSimpleDataContextBinding(reflection) && reflection.Path is { } path && TryProperty(model, path, out var target, out var property))
                return property.GetValue(target);
            var sink = new BindingSink { DataContext = model };
            using var subscription = sink.Bind(BindingSink.ResultProperty, binding);
            return sink.GetValue(BindingSink.ResultProperty);
        }
        return column.ValueGetter is { } getter ? getter(model) : model;
    }
    internal Action<object, object?>? GetWriter(object model, Func<bool>? canWrite = null)
    {
        var column = EffectiveColumn;
        if (IsReadOnly || column.IsReadOnly) return null;
        if (column.ValueSetter is { } writer) return writer;
        if (column._hasTypedValueAccessors) return null;
        if (column.DisplayBinding is ReflectionBinding binding && binding.Mode is BindingMode.Default or BindingMode.TwoWay
            && IsSimpleDataContextBinding(binding) && binding.Path is { } path && TryProperty(model, path, out var capturedTarget, out var property, writable: true) && property.SetMethod?.IsPublic == true)
            return (item, value) =>
            {
                if (!ReferenceEquals(column.DisplayBinding, binding) || !TryProperty(item, path, out var target, out var current, writable: true) ||
                    !ReferenceEquals(target, capturedTarget) || !Equals(current, property) || current.SetMethod?.IsPublic != true || canWrite?.Invoke() == false)
                    throw new InvalidOperationException("The binding edit target changed. Cancel the draft and begin a new edit.");
                current.SetValue(target, value);
            };
        return null;
    }
    internal object? ConvertEditValue(object? value, object model)
    {
        var column = EffectiveColumn;
        if (column.ValueConverter is { } converter) return converter(value);
        var type = column.ValueType;
        if (type is null && column.DisplayBinding is ReflectionBinding { Path: { } path } && TryProperty(model, path, out _, out var property)) type = property.PropertyType;
        type ??= column is TreeDataGridCheckBoxColumn check ? check.IsThreeState ? typeof(bool?) : typeof(bool) : column.ReadValue(model)?.GetType() ?? typeof(string);
        var nullable = Nullable.GetUnderlyingType(type);
        if (value is null || value is string { Length: 0 } && nullable is not null)
        {
            if (!type.IsValueType || nullable is not null) return null;
            throw new FormatException($"A value of type {type.Name} is required.");
        }
        type = nullable ?? type;
        if (type.IsInstanceOfType(value)) return value;
        if (type == typeof(string)) return Convert.ToString(value, CultureInfo.CurrentCulture);
        if (type.IsEnum) return Enum.Parse(type, value.ToString()!, ignoreCase: true);
        return TypeDescriptor.GetConverter(type).ConvertFrom(null, CultureInfo.CurrentCulture, value);
    }
    private static bool TryProperty(object model, string path, out object target, out PropertyInfo property, bool writable = false)
    {
        target = model; property = null!;
        var parts = path.Split('.');
        if (parts.Any(part => part.Length == 0 || part.Any(c => !char.IsLetterOrDigit(c) && c != '_'))) return false;
        for (var i = 0; i < parts.Length; ++i)
        {
            var member = target.GetType().GetProperty(parts[i], BindingFlags.Instance | BindingFlags.Public);
            if (member?.GetMethod?.IsPublic != true || member.GetIndexParameters().Length != 0) return false;
            if (i == parts.Length - 1) { property = member; return true; }
            if (writable && member.PropertyType.IsValueType) return false;
            var next = member.GetValue(target); if (next is null) return false; target = next;
        }
        return false;
    }
    private static bool IsSimpleDataContextBinding(ReflectionBinding binding) =>
        ReferenceEquals(binding.Source, AvaloniaProperty.UnsetValue) && binding.Converter is null && binding.ElementName is null && binding.RelativeSource is null;
    private sealed class BindingSink : Control
    {
        internal static readonly StyledProperty<object?> ResultProperty = AvaloniaProperty.Register<BindingSink, object?>("Result");
    }
}
