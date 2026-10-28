using System.Globalization;
using System.Reflection;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Only public model members are inspected. Compiled binding expression internals are never inspected.</summary>
internal sealed class TreeDataGridModelPath
{
    internal sealed record Step(string? Property, object? Index, PropertyInfo? DeclaredMember = null);
    private readonly Step[] _steps;
    private TreeDataGridModelPath(Step[] steps) => _steps = steps;
    internal TreeDataGridModelPath WithDeclaredMembers(IReadOnlyList<PropertyInfo?> members)
    {
        if (members.Count != _steps.Length) throw new NotSupportedException("The expression does not map to a supported property path.");
        return new(_steps.Select((step, index) => step with { DeclaredMember = members[index] }).ToArray());
    }
    internal static TreeDataGridModelPath Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new NotSupportedException("A nonempty model-property path is required.");
        var steps = new List<Step>(); var position = 0;
        while (position < path.Length)
        {
            var start = position;
            if (!char.IsLetter(path[position]) && path[position] != '_') throw new NotSupportedException($"Unsupported model path '{path}'.");
            while (position < path.Length && (char.IsLetterOrDigit(path[position]) || path[position] == '_')) position++;
            steps.Add(new(path[start..position], null));
            while (position < path.Length && path[position] == '[')
            {
                var end = path.IndexOf(']', ++position);
                if (end < 0) throw new NotSupportedException($"Unclosed index in model path '{path}'.");
                var token = path[position..end]; object key;
                if (token.Length >= 2 && token[0] is '\'' or '"' && token[^1] == token[0])
                {
                    var text = token[1..^1];
                    if (text.Length == 0 || text.Any(c => !char.IsLetterOrDigit(c) && c is not ('_' or '-' or ' '))) throw new NotSupportedException("Unsupported string index.");
                    key = text;
                }
                else if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var index)) key = index;
                else throw new NotSupportedException("Only nonnegative integer or quoted string indices are supported.");
                steps.Add(new(null, key)); position = end + 1;
            }
            if (position == path.Length) break;
            if (path[position++] != '.' || position == path.Length) throw new NotSupportedException($"Unsupported model path '{path}'.");
        }
        return new(steps.ToArray());
    }
    internal CapturedTarget Capture(object root, bool writable)
    {
        if (writable && root.GetType().IsValueType) throw new NotSupportedException("Value-type model roots require an explicit writer.");
        var trace = new List<(object Target, PropertyInfo? Property, object? Index)>();
        object current = root;
        for (var i = 0; i < _steps.Length; i++)
        {
            var step = _steps[i]; var final = i == _steps.Length - 1;
            var property = step.Property is { } name ? current.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
                : current is Array ? null : current.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .SingleOrDefault(p => p.GetIndexParameters() is [{ ParameterType: var type }] && type == step.Index!.GetType());
            if (current is Array array && step.Property is null && array.Rank != 1)
                throw new NotSupportedException("Only one-dimensional array index paths are supported.");
            if (step.DeclaredMember is { } declared && !SameMember(property, declared))
                throw new NotSupportedException("The runtime property differs from the compiled expression member (for example a hidden/overridden member). Use an explicit writer or declare an authoritative adapter path.");
            if (property is null && !(step.Property is null && current is Array && step.Index is int))
                throw new NotSupportedException("The binding path requires public properties and supported indices.");
            if (property is not null && (property.GetMethod?.IsPublic != true || step.Property is not null && property.GetIndexParameters().Length != 0))
                throw new NotSupportedException("The binding path requires a public getter.");
            var typeOfValue = property?.PropertyType ?? current.GetType().GetElementType()!;
            if (final && writable && (step.Property is null || property?.SetMethod?.IsPublic != true))
                throw new NotSupportedException("Automatic writes require a public property setter; index slots require an explicit writer.");
            trace.Add((current, property, step.Index));
            if (final) return new(this, root, trace.ToArray(), typeOfValue);
            if (typeOfValue.IsValueType) throw new NotSupportedException("Value-type intermediate nodes require an explicit writer.");
            current = Read(current, property, step.Index) ?? throw new InvalidOperationException("An intermediate binding target is null.");
        }
        throw new InvalidOperationException("The binding path is empty.");
    }
    private static object? Read(object target, PropertyInfo? property, object? index) => property is null
        ? ((Array)target).GetValue((int)index!) : property.GetValue(target, index is null ? null : [index]);
    private static bool SameMember(PropertyInfo? left, PropertyInfo right) => left is not null
        && left.Module == right.Module && left.MetadataToken == right.MetadataToken && left.DeclaringType == right.DeclaringType;

    internal sealed class CapturedTarget(TreeDataGridModelPath path, object root,
        (object Target, PropertyInfo? Property, object? Index)[] trace, Type valueType)
    {
        internal Type ValueType { get; } = valueType;
        internal object? ReadValue() => Read(_trace[^1].Target, _trace[^1].Property, _trace[^1].Index);
        internal void Validate()
        {
            var current = path.Capture(root, writable: true);
            if (current._trace.Length != _trace.Length || current._trace.Where((step, i) => !ReferenceEquals(step.Target, _trace[i].Target)
                || !Equals(step.Property, _trace[i].Property) || !Equals(step.Index, _trace[i].Index)).Any())
                throw new InvalidOperationException("The binding edit target changed. Cancel the draft and begin a new edit.");
        }
        private readonly (object Target, PropertyInfo? Property, object? Index)[] _trace = trace;
        internal void Write(object? value) => _trace[^1].Property!.SetValue(_trace[^1].Target, value);
    }
}
