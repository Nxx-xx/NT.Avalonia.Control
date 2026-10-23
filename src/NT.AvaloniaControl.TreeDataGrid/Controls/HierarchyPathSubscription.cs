using System.ComponentModel;
using System.Reflection;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Observes intermediate hierarchy objects; Source already observes each root model.</summary>
internal sealed class HierarchyPathSubscription(object model, IReadOnlyList<string> paths, Action changed) : IDisposable
{
    private readonly Dictionary<INotifyPropertyChanged, HashSet<string>> _subscriptions = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;

    public void Rebind()
    {
        if (_disposed) return;
        var next = new Dictionary<INotifyPropertyChanged, HashSet<string>>(ReferenceEqualityComparer.Instance);
        foreach (var path in paths)
        {
            object? target = model;
            foreach (var part in path.Split('.'))
            {
                if (target is null) break;
                var property = target.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
                if (property?.GetMethod?.IsPublic != true || property.GetIndexParameters().Length != 0)
                    throw new InvalidOperationException($"Hierarchy property path '{path}' requires a public getter for '{part}'.");
                if (!ReferenceEquals(target, model) && target is INotifyPropertyChanged notifying)
                {
                    if (!next.TryGetValue(notifying, out var names)) next.Add(notifying, names = []);
                    names.Add(part);
                }
                target = property.GetValue(target);
                if (_disposed) return;
            }
        }
        foreach (var notifying in _subscriptions.Keys.Where(notifying => !next.ContainsKey(notifying)).ToArray())
        {
            notifying.PropertyChanged -= PropertyChanged;
            _subscriptions.Remove(notifying);
        }
        foreach (var (notifying, names) in next)
        {
            if (!_subscriptions.ContainsKey(notifying)) notifying.PropertyChanged += PropertyChanged;
            _subscriptions[notifying] = names;
        }
    }

    private void PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is INotifyPropertyChanged notifying && _subscriptions.TryGetValue(notifying, out var names)
            && (string.IsNullOrEmpty(args.PropertyName) || names.Contains(args.PropertyName))) changed();
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var notifying in _subscriptions.Keys) notifying.PropertyChanged -= PropertyChanged;
        _subscriptions.Clear();
    }
}
