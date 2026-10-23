using System.Collections;
using System.Collections.Specialized;
using System.Reflection;
using Avalonia;
using Avalonia.Data;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Internal adapters retain non-generic collection notifications and original model references.</summary>
internal sealed class ItemsSourceAdapter : IDisposable
{
    private readonly Dictionary<IEnumerable, ObservableItems> _collections = new(ReferenceEqualityComparer.Instance);
    private readonly BindingBase? _children;
    private readonly BindingBase? _hasChildren;
    private readonly BindingBase? _expanded;
    private readonly string[] _nestedPaths;
    private readonly Dictionary<object, HierarchyPathSubscription> _pathSubscriptions = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;
    private bool _reconcilingPaths;
    private bool _refreshing;
    private bool _writingExpansion;
    private bool _pendingRefresh;
    private bool _sourceReady;
    public TreeDataGridSource Source { get; }

    public ItemsSourceAdapter(IEnumerable items, TreeDataGridHierarchicalExpanderColumn? hierarchy)
    {
        _children = hierarchy?.ChildrenBinding;
        _hasChildren = hierarchy?.HasChildrenBinding;
        _expanded = hierarchy?.IsExpandedBinding;
        _nestedPaths = new[] { _children, _hasChildren, _expanded }
            .OfType<ReflectionBinding>().Select(binding => binding.Path!).Where(path => path.Contains('.')).Distinct().ToArray();
        try
        {
            var roots = Adapt(items);
            if (hierarchy?.ChildrenBinding is not { } children)
            {
                if (hierarchy?.HasChildrenBinding is not null || hierarchy?.IsExpandedBinding is not null)
                    throw new InvalidOperationException("ChildrenBinding is required when configuring hierarchy bindings.");
                Source = new FlatTreeDataGridSource<object>(roots);
            }
            else
            {
                var childrenPath = ItemsSourceHierarchyBinding.Path(children, writable: false);
                var hasChildrenPath = hierarchy.HasChildrenBinding is { } hasChildren ? ItemsSourceHierarchyBinding.Path(hasChildren, writable: false) : null;
                var expandedPath = hierarchy.IsExpandedBinding is { } expanded ? ItemsSourceHierarchyBinding.Path(expanded, writable: true) : null;
                Source = new HierarchicalTreeDataGridSource<object>(roots, model =>
                {
                    var value = ItemsSourceHierarchyBinding.Read(model, childrenPath);
                    return value is null ? null : value is IEnumerable values && value is not string ? Adapt(values)
                        : throw new InvalidOperationException($"ChildrenBinding '{childrenPath}' must return an IEnumerable or null.");
                }, new HierarchicalTreeDataGridSourceOptions<object>
                {
                    HasChildrenSelector = hasChildrenPath is null ? null : model => ItemsSourceHierarchyBinding.ReadBoolean(model, hasChildrenPath, writable: false),
                    IsExpandedSelector = expandedPath is null ? null : model => ItemsSourceHierarchyBinding.ReadBoolean(model, expandedPath, writable: true),
                    IsExpandedWriter = expandedPath is null ? null : (model, value) =>
                    {
                        _writingExpansion = true;
                        try { ItemsSourceHierarchyBinding.WriteBoolean(model, expandedPath, value); }
                        finally { _writingExpansion = false; }
                    },
                });
            }
            _sourceReady = true;
            if (_nestedPaths.Length > 0)
            {
                Source!.ProjectionChanged += ProjectionChanged;
                ReconcilePathSubscriptions();
            }
        }
        catch
        {
            foreach (var subscription in _pathSubscriptions.Values) subscription.Dispose();
            _pathSubscriptions.Clear();
            if (_sourceReady) Source!.Dispose();
            foreach (var collection in _collections.Values.ToArray()) collection.Dispose();
            _collections.Clear();
            throw;
        }
    }

    public bool Matches(TreeDataGridHierarchicalExpanderColumn? hierarchy) =>
        ReferenceEquals(_children, hierarchy?.ChildrenBinding) && ReferenceEquals(_hasChildren, hierarchy?.HasChildrenBinding)
        && ReferenceEquals(_expanded, hierarchy?.IsExpandedBinding);

    private ObservableItems Adapt(IEnumerable items)
    {
        if (!_collections.TryGetValue(items, out var adapter))
        {
            adapter = new ObservableItems(items, () => _collections.Remove(items));
            _collections.Add(items, adapter);
        }
        return adapter;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Source.ProjectionChanged -= ProjectionChanged;
        foreach (var subscription in _pathSubscriptions.Values) subscription.Dispose();
        _pathSubscriptions.Clear();
        Source.Dispose();
        foreach (var collection in _collections.Values.ToArray()) collection.Dispose();
        _collections.Clear();
    }

    private void ProjectionChanged(object? sender, ProjectionChangedEventArgs args)
    {
        ReconcilePathSubscriptions();
        if (_pendingRefresh && !_writingExpansion && !_refreshing) RefreshNestedPaths();
    }

    private void ReconcilePathSubscriptions()
    {
        if (_disposed || _reconcilingPaths) return;
        _reconcilingPaths = true;
        try
        {
            // Source paths enumerate the complete committed graph, including collapsed and filtered nodes.
            // Using that graph avoids invoking application collection getters a second time.
            var models = new HashSet<object>(ReferenceEqualityComparer.Instance);
            void Visit(IndexPath parent)
            {
                for (var index = 0; ; ++index)
                {
                    var path = parent.Append(index);
                    if (!Source.TryGetNodeId(path, out var id)) break;
                    if (Source.TryGetRow(id, out var row)) models.Add(row!.Model);
                    Visit(path);
                }
            }
            Visit(default);
            foreach (var model in _pathSubscriptions.Keys.Where(model => !models.Contains(model)).ToArray())
            {
                _pathSubscriptions[model].Dispose();
                _pathSubscriptions.Remove(model);
            }
            foreach (var model in models)
            {
                if (_disposed) return;
                if (!_pathSubscriptions.TryGetValue(model, out var subscription))
                    _pathSubscriptions.Add(model, subscription = new HierarchyPathSubscription(model, _nestedPaths, NestedPathChanged));
                subscription.Rebind();
            }
        }
        finally { _reconcilingPaths = false; }
    }

    private void NestedPathChanged()
    {
        if (_disposed) return;
        _ = Source.Version; // Enforce the source's creating-thread notification contract.
        if (_reconcilingPaths) throw new InvalidOperationException("Hierarchy properties must not change while their binding paths are evaluated.");
        ReconcilePathSubscriptions(); // Detach replaced intermediate objects before refreshing the graph.
        _pendingRefresh = true;
        if (!_writingExpansion && !_refreshing) RefreshNestedPaths();
    }

    private void RefreshNestedPaths()
    {
        if (_disposed || _refreshing || _writingExpansion) return;
        _refreshing = true;
        try
        {
            do
            {
                _pendingRefresh = false;
                Source.Refresh(); // Refresh queues itself while a projection is being delivered.
            } while (_pendingRefresh && !_disposed);
        }
        finally { _refreshing = false; }
    }

    private sealed class ObservableItems(IEnumerable items, Action detached) : IEnumerable<object>, INotifyCollectionChanged, IDisposable, ITreeDataGridCollectionChangeTransaction
    {
        bool ITreeDataGridCollectionChangeTransaction.IsChangePending => items is ITreeDataGridCollectionChangeTransaction transaction && transaction.IsChangePending;
        event EventHandler? ITreeDataGridCollectionChangeTransaction.ChangeCompleted
        {
            add { if (items is ITreeDataGridCollectionChangeTransaction transaction) transaction.ChangeCompleted += value; }
            remove { if (items is ITreeDataGridCollectionChangeTransaction transaction) transaction.ChangeCompleted -= value; }
        }
        private NotifyCollectionChangedEventHandler? _handlers;
        private List<object> _values = [];
        private bool _subscribed;
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add
            {
                if (!_subscribed && items is INotifyCollectionChanged notifying)
                {
                    notifying.CollectionChanged += Changed;
                    _subscribed = true;
                }
                _handlers += value;
            }
            remove
            {
                _handlers -= value;
                if (_handlers is null) Dispose();
            }
        }
        public IEnumerator<object> GetEnumerator()
        {
            // Reference models pass through unchanged. Reuse boxes for equal value occurrences so refreshing
            // a non-generic collection does not create new identities for every boxed primitive.
            var boxes = new Dictionary<object, Queue<object>>();
            foreach (var old in _values.Where(value => value is not null && value.GetType().IsValueType))
            {
                if (!boxes.TryGetValue(old, out var queue)) boxes.Add(old, queue = new Queue<object>());
                queue.Enqueue(old);
            }
            _values = items.Cast<object>().Select(value => value is null
                ? throw new InvalidOperationException("ItemsSource cannot contain null models.")
                : value.GetType().IsValueType && boxes.TryGetValue(value, out var queue) && queue.Count != 0 ? queue.Dequeue() : value).ToList();
            return _values.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        private void Changed(object? sender, NotifyCollectionChangedEventArgs args)
        {
            // Preserve box occurrence order across indexed operations before the source reconciles identities.
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add when args.NewStartingIndex >= 0 && args.NewStartingIndex <= _values.Count:
                    _values.InsertRange(args.NewStartingIndex, args.NewItems!.Cast<object>());
                    break;
                case NotifyCollectionChangedAction.Remove when args.OldStartingIndex >= 0 && args.OldStartingIndex + args.OldItems!.Count <= _values.Count:
                    _values.RemoveRange(args.OldStartingIndex, args.OldItems!.Count);
                    break;
                case NotifyCollectionChangedAction.Replace when args.OldStartingIndex >= 0 && args.OldStartingIndex + args.OldItems!.Count <= _values.Count:
                    _values.RemoveRange(args.OldStartingIndex, args.OldItems!.Count);
                    _values.InsertRange(args.NewStartingIndex, args.NewItems!.Cast<object>());
                    break;
                case NotifyCollectionChangedAction.Move when args.OldStartingIndex >= 0 && args.NewStartingIndex >= 0
                    && args.OldItems is { Count: > 0 } old && args.OldStartingIndex + old.Count <= _values.Count
                    && args.NewStartingIndex <= _values.Count - old.Count:
                    var moving = _values.GetRange(args.OldStartingIndex, old.Count);
                    _values.RemoveRange(args.OldStartingIndex, old.Count);
                    _values.InsertRange(args.NewStartingIndex, moving);
                    break;
                default: _values.Clear(); break;
            }
            _handlers?.Invoke(this, args);
        }
        public void Dispose()
        {
            if (_subscribed && items is INotifyCollectionChanged notifying) notifying.CollectionChanged -= Changed;
            _subscribed = false;
            _handlers = null;
            detached();
        }
    }
}

internal static class ItemsSourceHierarchyBinding
{
    public static void Validate(BindingBase? binding, bool writable)
    {
        if (binding is not null) _ = Path(binding, writable);
    }
    public static string Path(BindingBase binding, bool writable)
    {
        if (binding is not ReflectionBinding reflection || reflection.Path is not { Length: > 0 } path
            || path.Split('.').Any(part => part.Length == 0 || part.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
            || !ReferenceEquals(reflection.Source, AvaloniaProperty.UnsetValue) || reflection.ElementName is not null || reflection.RelativeSource is not null
            || reflection.Converter is not null || reflection.StringFormat is not null
            || !ReferenceEquals(reflection.FallbackValue, AvaloniaProperty.UnsetValue)
            || !ReferenceEquals(reflection.TargetNullValue, AvaloniaProperty.UnsetValue)
            || (writable ? reflection.Mode is not (BindingMode.Default or BindingMode.TwoWay)
                : reflection.Mode is not (BindingMode.Default or BindingMode.OneWay)))
            throw new NotSupportedException("ItemsSource hierarchy bindings require a public model-property path ReflectionBinding without converters or relative sources; expansion must use Default or TwoWay mode.");
        return path;
    }
    public static (object? Target, PropertyInfo Property) Resolve(object model, string path, bool writable)
    {
        object? target = model;
        var type = model.GetType();
        var parts = path.Split('.');
        for (var index = 0; index < parts.Length; ++index)
        {
            var property = type.GetProperty(parts[index], BindingFlags.Public | BindingFlags.Instance);
            var final = index == parts.Length - 1;
            if (property?.GetMethod?.IsPublic != true || property.GetIndexParameters().Length != 0
                || final && writable && property.SetMethod?.IsPublic != true)
                throw new InvalidOperationException($"Hierarchy property path '{path}' requires a public getter{(final && writable ? " and setter" : "")} for '{parts[index]}'.");
            if (final) return (target, property);
            if (property.PropertyType.IsValueType)
                throw new NotSupportedException($"Hierarchy property path '{path}' must traverse reference objects; '{parts[index]}' is a value type.");
            target = target is null ? null : property.GetValue(target);
            type = target?.GetType() ?? property.PropertyType;
        }
        throw new InvalidOperationException($"Hierarchy property path '{path}' is empty.");
    }
    public static object? Read(object model, string path)
    {
        var resolved = Resolve(model, path, writable: false);
        return resolved.Target is null ? null : resolved.Property.GetValue(resolved.Target);
    }
    public static bool ReadBoolean(object model, string path, bool writable)
    {
        var resolved = Resolve(model, path, writable);
        if (resolved.Property.PropertyType != typeof(bool)) throw new InvalidOperationException($"Hierarchy property '{path}' must be boolean.");
        return resolved.Target is not null && (bool)resolved.Property.GetValue(resolved.Target)!;
    }
    public static void WriteBoolean(object model, string path, bool value)
    {
        var resolved = Resolve(model, path, writable: true);
        if (resolved.Target is null) throw new InvalidOperationException($"Hierarchy property '{path}' cannot be written because an intermediate object is null.");
        resolved.Property.SetValue(resolved.Target, value);
    }
}
