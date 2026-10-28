using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Metadata;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Preserves a verifiable model path alongside an opaque compiled display binding.</summary>
public static class TreeDataGridBinding
{
    private sealed record Registration(string Path, CompiledBindingPath? CompiledPath, TreeDataGridModelPath ModelPath);
    private static readonly ConditionalWeakTable<BindingBase, Registration> Paths = new();

    /// <summary>Creates a compiled binding whose public property/index path can also be captured for staged writes.</summary>
    public static CompiledBinding Create<TModel, TValue>(Expression<Func<TModel, TValue>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var members = new List<PropertyInfo?>();
        var path = ExpressionPath(expression.Body, expression.Parameters[0], members);
        var binding = CompiledBinding.Create(expression);
        Paths.Add(binding, new(path, binding.Path, TreeDataGridModelPath.Parse(path).WithDeclaredMembers(members)));
        return binding;
    }

    /// <summary>Declares an authoritative model path for editing and hierarchy access. This does not infer a path from a binding's diagnostic string.</summary>
    public static BindingBase Attach(BindingBase binding, string path)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var modelPath = TreeDataGridModelPath.Parse(path);
        if (binding is not (CompiledBinding or ReflectionBinding)) throw new NotSupportedException("The binding adapter supports CompiledBinding and ReflectionBinding.");
        Paths.Remove(binding);
        Paths.Add(binding, new(path, (binding as CompiledBinding)?.Path, modelPath));
        return binding;
    }

    internal static string? GetPath(BindingBase binding)
    {
        if (Paths.TryGetValue(binding, out var registration))
        {
            if (binding is CompiledBinding compiled && !ReferenceEquals(compiled.Path, registration.CompiledPath))
                throw new InvalidOperationException("The compiled binding path changed after registration. Register its new model path before editing.");
            return registration.Path;
        }
        return (binding as ReflectionBinding)?.Path;
    }

    internal static object? GetPathToken(BindingBase binding) => Paths.TryGetValue(binding, out var registration) ? registration.ModelPath : null;
    internal static TreeDataGridModelPath ModelPath(BindingBase binding)
    {
        var path = GetPath(binding) ?? throw new NotSupportedException("The compiled binding path is opaque.");
        return Paths.TryGetValue(binding, out var registration) ? registration.ModelPath : TreeDataGridModelPath.Parse(path);
    }
    internal static void ValidateHierarchyAdapter(BindingBase binding, object model)
    {
        if (!Paths.TryGetValue(binding, out _)) return;
        try { _ = ModelPath(binding).Capture(model, writable: false); }
        catch (InvalidOperationException error) when (error.Message == "An intermediate binding target is null.") { }
    }
    private static string ExpressionPath(Expression expression, ParameterExpression parameter, List<PropertyInfo?> members)
    {
        if (expression == parameter) return "";
        if (expression is MemberExpression { Member: PropertyInfo property, Expression: { } parent })
        {
            var prefix = ExpressionPath(parent, parameter, members);
            members.Add(property);
            return prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
        }
        if (expression is MethodCallExpression { Method.Name: "get_Item", Object: { } owner, Arguments.Count: 1 } call)
        {
            var prefix = ExpressionPath(owner, parameter, members);
            members.Add(call.Method.DeclaringType!.GetProperties(BindingFlags.Public | BindingFlags.Instance).Single(property => property.GetMethod == call.Method));
            return prefix + Index(call.Arguments[0]);
        }
        if (expression is IndexExpression { Object: { } indexed, Arguments.Count: 1 } index)
        {
            var prefix = ExpressionPath(indexed, parameter, members); members.Add(index.Indexer);
            return prefix + Index(index.Arguments[0]);
        }
        if (expression is BinaryExpression { NodeType: ExpressionType.ArrayIndex } array)
        {
            var prefix = ExpressionPath(array.Left, parameter, members); members.Add(null);
            return prefix + Index(array.Right);
        }
        throw new NotSupportedException("The staged binding adapter requires public property paths and constant integer/string indices; casts, methods, fields and computed expressions require an explicit writer.");
    }
    private static string Index(Expression index) => index is ConstantExpression constant ? constant.Value switch
    {
        int number when number >= 0 => "[" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]",
        string key when key.Length > 0 && key.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or ' ') => "['" + key + "']",
        _ => throw new NotSupportedException("Only nonnegative integer and simple string index constants are supported.")
    } : throw new NotSupportedException("A staged index must be a constant, not a captured variable.");
}

/// <summary>XAML adapter, for example {tg:TreeDataGridBindingAdapter Path=Detail.Name, Binding={CompiledBinding Detail.Name}}.</summary>
public sealed class TreeDataGridBindingAdapterExtension : MarkupExtension
{
    public string Path { get; set; } = "";
    [AssignBinding]
    public BindingBase? Binding { get; set; }
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        TreeDataGridBinding.Attach(Binding ?? throw new InvalidOperationException("The binding adapter requires Binding."), Path);
}
