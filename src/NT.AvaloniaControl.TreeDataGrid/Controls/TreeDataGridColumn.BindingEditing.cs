using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace NT.AvaloniaControl.TreeDataGrid;

public abstract partial class TreeDataGridColumn
{
    internal sealed record BindingOptions(BindingMode Mode, object? Source, IValueConverter? Converter, object? Parameter,
        CultureInfo? Culture, object? ExpressionPath, string? ModelPath, string? Element, object? Relative, string? Format, object? PathToken)
    {
        internal static BindingOptions Read(BindingBase binding) => binding switch
        {
            ReflectionBinding value => new(value.Mode, value.Source, value.Converter, value.ConverterParameter,
                value.ConverterCulture, value.Path, TreeDataGridBinding.GetPath(value), value.ElementName, value.RelativeSource, value.StringFormat, TreeDataGridBinding.GetPathToken(value)),
            CompiledBinding value => new(value.Mode, value.Source, value.Converter, value.ConverterParameter,
                value.ConverterCulture, value.Path, TreeDataGridBinding.GetPath(value), null, null, value.StringFormat, TreeDataGridBinding.GetPathToken(value)),
            _ => throw new NotSupportedException("This binding requires an explicit ValueSetter or typed accessors.")
        };
        internal object Root(BindingBase binding, object model) => ReferenceEquals(Source, AvaloniaProperty.UnsetValue)
            || binding is CompiledBinding && Source is null ? model : Source ?? throw new InvalidOperationException("The explicit binding source is null.");
    }

    /// <summary>Returns an automatic-edit diagnostic for this model, or null when a staged writer is available. Does not call a writer or converter.</summary>
    public string? GetEditBindingError(object model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return TryCreateEditAccess(model, null, out _, out var error) ? null : error;
    }

    internal bool TryCreateEditAccess(object model, Func<bool>? canWrite, out EditAccess? access, out string? error)
    {
        access = null; error = null;
        var column = EffectiveColumn;
        if (IsReadOnly || column.IsReadOnly) { error = "The column is read-only."; return false; }
        try
        {
            if (column.ValueSetter is { } explicitWriter)
            {
                access = new(this, column, model, explicitWriter, canWrite, null, null, null);
                return true;
            }
            if (column._hasTypedValueAccessors) { error = "The typed accessor has no setter."; return false; }
            if (column.DisplayBinding is not { } binding) { error = "Configure a writable binding or an explicit ValueSetter."; return false; }
            var options = BindingOptions.Read(binding);
            if (options.Mode is not (BindingMode.Default or BindingMode.TwoWay)) { error = "Automatic staged writes require Default or TwoWay mode."; return false; }
            if (options.Element is not null || options.Relative is not null)
            { error = "ElementName and RelativeSource require an explicit writer; column metadata has no stable visual anchor."; return false; }
            if (options.ModelPath is null)
            { error = "The compiled binding path is opaque. Use TreeDataGridBinding.Create, the XAML TreeDataGridBindingAdapter, or an explicit writer."; return false; }
            var target = TreeDataGridBinding.ModelPath(binding).Capture(options.Root(binding, model), writable: true);
            access = new(this, column, model, null, canWrite, binding, options, target);
            return true;
        }
        catch (Exception failure) when (failure is NotSupportedException or InvalidOperationException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException or System.Reflection.TargetInvocationException)
        { error = failure.InnerException?.Message ?? failure.Message; return false; }
    }

    internal sealed class EditAccess
    {
        private readonly TreeDataGridColumn _outer;
        private readonly TreeDataGridColumn _column;
        private readonly object _model;
        private readonly Action<object, object?>? _explicitWriter;
        private readonly Func<bool>? _canWrite;
        private readonly BindingBase? _binding;
        private readonly BindingOptions? _options;
        private readonly TreeDataGridModelPath.CapturedTarget? _target;
        private readonly Func<object?, object?>? _converter;
        private readonly Func<object?, string?>? _validator;
        private readonly Type? _valueType;
        private readonly Func<object, object?>? _getter;
        private readonly BindingBase? _displayBinding;

        internal EditAccess(TreeDataGridColumn outer, TreeDataGridColumn column, object model,
            Action<object, object?>? writer, Func<bool>? canWrite, BindingBase? binding, BindingOptions? options,
            TreeDataGridModelPath.CapturedTarget? target)
        {
            _outer = outer; _column = column; _model = model; _explicitWriter = writer; _canWrite = canWrite;
            _binding = binding; _options = options; _target = target;
            _converter = column.ValueConverter; _validator = column.ValueValidator; _valueType = column.ValueType;
            _getter = column.ValueGetter; _displayBinding = column.DisplayBinding;
        }
        private void Validate(bool writing)
        {
            var currentOptions = _binding is null ? null : BindingOptions.Read(_binding);
            if (!ReferenceEquals(_outer.EffectiveColumn, _column) || _outer.IsReadOnly || _column.IsReadOnly
                || !ReferenceEquals(_column.ValueSetter, _explicitWriter) || _column.ValueConverter != _converter
                || _column.ValueGetter != _getter || !ReferenceEquals(_column.DisplayBinding, _displayBinding)
                || _column.ValueValidator != _validator || _column.ValueType != _valueType
                || _binding is not null && (!ReferenceEquals(_column.DisplayBinding, _binding) || currentOptions != _options
                    || !ReferenceEquals(currentOptions!.Source, _options!.Source))
                || writing && _canWrite?.Invoke() == false)
                throw new InvalidOperationException("The column or binding edit configuration changed. Cancel the draft and begin a new edit.");
            _target?.Validate();
            if (writing && _canWrite?.Invoke() == false) throw new InvalidOperationException("The edit owner or model target changed.");
        }
        internal object? ReadOriginal(object model)
        {
            Validate(writing: false);
            var value = _target is null ? _column.ReadValue(model) : _target.ReadValue();
            Validate(writing: false);
            return value;
        }
        internal object? CreateDraft(object? original)
        {
            object? value;
            try
            {
                value = _target is not null && _options?.Converter is { } converter
                    ? converter.Convert(original, typeof(object), _options.Parameter, _options.Culture ?? CultureInfo.CurrentCulture) : original;
            }
            catch (Exception failure) { throw new FormatException("The binding converter could not create an edit draft: " + failure.Message, failure); }
            Validate(writing: false);
            RejectNoValue(value);
            return value;
        }
        internal object? Convert(object? draft)
        {
            Validate(writing: true);
            object? value;
            try
            {
                if (_converter is { } explicitConverter) value = explicitConverter(draft);
                else if (_target is not null && _options?.Converter is { } converter)
                    value = converter.ConvertBack(draft, _target.ValueType, _options.Parameter, _options.Culture ?? CultureInfo.CurrentCulture);
                else value = ConvertValue(draft, _valueType ?? _target?.ValueType ?? InferEditType(_column, _model));
            }
            catch (Exception failure) { throw new FormatException("The edit converter failed: " + failure.Message, failure); }
            RejectNoValue(value);
            if (_target is not null && (value is null ? _target.ValueType.IsValueType && Nullable.GetUnderlyingType(_target.ValueType) is null
                : !_target.ValueType.IsInstanceOfType(value) && !(Nullable.GetUnderlyingType(_target.ValueType)?.IsInstanceOfType(value) ?? false)))
                throw new FormatException("The converted value does not match the binding source property type.");
            Validate(writing: true);
            return value;
        }
        internal void Write(object model, object? value)
        {
            if (!ReferenceEquals(model, _model)) throw new InvalidOperationException("The edit model changed.");
            Validate(writing: true);
            RejectNoValue(value);
            if (_explicitWriter is { } writer) writer(model, value);
            else _target!.Write(value);
        }
    }
    private static void RejectNoValue(object? value)
    {
        if (ReferenceEquals(value, AvaloniaProperty.UnsetValue) || ReferenceEquals(value, BindingOperations.DoNothing))
            throw new FormatException("The converter requested no source update (UnsetValue/DoNothing). The draft was not committed.");
        if (value is BindingNotification notification)
            throw new FormatException(notification.Error?.Message ?? "A binding notification is not a committed model value.", notification.Error);
    }
    private static Type InferEditType(TreeDataGridColumn column, object model) => column.ValueType
        ?? (column is TreeDataGridCheckBoxColumn check ? check.IsThreeState ? typeof(bool?) : typeof(bool) : column.ReadValue(model)?.GetType() ?? typeof(string));
    private static object? ConvertValue(object? value, Type type)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (value is null || value is string { Length: 0 } && nullable is not null)
        {
            if (!type.IsValueType || nullable is not null) return null;
            throw new FormatException($"A value of type {type.Name} is required.");
        }
        type = nullable ?? type;
        if (type.IsInstanceOfType(value)) return value;
        if (type == typeof(string)) return System.Convert.ToString(value, CultureInfo.CurrentCulture);
        if (type.IsEnum) return Enum.Parse(type, value.ToString()!, ignoreCase: true);
        return System.ComponentModel.TypeDescriptor.GetConverter(type).ConvertFrom(null, CultureInfo.CurrentCulture, value);
    }
}
