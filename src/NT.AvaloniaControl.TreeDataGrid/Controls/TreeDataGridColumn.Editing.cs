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
            try
            {
                var options = BindingOptions.Read(binding);
                if (options.ModelPath is { } path && options.Element is null && options.Relative is null)
                    return TreeDataGridBinding.ModelPath(binding).Capture(options.Root(binding, model), writable: false).ReadValue();
            }
            catch (Exception failure) when (failure is NotSupportedException or InvalidOperationException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException) { }
            var sink = new BindingSink { DataContext = model };
            try
            {
                using var subscription = sink.Bind(BindingSink.ResultProperty, binding);
                return sink.GetValue(BindingSink.ResultProperty);
            }
            finally { sink.DataContext = null; }
        }
        return column.ValueGetter is { } getter ? getter(model) : model;
    }
    internal Action<object, object?>? GetWriter(object model, Func<bool>? canWrite = null)
    {
        return TryCreateEditAccess(model, canWrite, out var access, out _) ? access!.Write : null;
    }
    internal object? ConvertEditValue(object? value, object model)
    {
        var column = EffectiveColumn;
        var type = column.ValueType;
        if (type is null && column.DisplayBinding is { } binding)
        {
            var options = BindingOptions.Read(binding);
            if (options.ModelPath is { } path && options.Element is null && options.Relative is null)
                type = TreeDataGridBinding.ModelPath(binding).Capture(options.Root(binding, model), writable: false).ValueType;
        }
        var converted = column.ValueConverter is { } converter ? converter(value) : ConvertValue(value, type ?? InferEditType(column, model));
        RejectNoValue(converted);
        return converted;
    }
    private sealed class BindingSink : Control
    {
        internal static readonly StyledProperty<object?> ResultProperty = AvaloniaProperty.Register<BindingSink, object?>("Result");
    }
}
