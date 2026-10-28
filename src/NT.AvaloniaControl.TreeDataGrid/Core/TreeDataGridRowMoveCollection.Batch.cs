using System.Collections.Specialized;

namespace NT.AvaloniaControl.TreeDataGrid;

public sealed partial class TreeDataGridRowMoveCollection<T>
{
    /// <summary>Atomically moves indexed occurrences in source order. The insertion index addresses the collection after removing them.</summary>
    public void MoveMany(IReadOnlyList<int> indices, int insertionIndex)
    {
        ArgumentNullException.ThrowIfNull(indices);
        VerifyMutation();
        RowMoveSlot[] slots;
        _preparing = true;
        try { slots = indices.Select(index => new RowMoveSlot(this, index)).ToArray(); }
        finally { _preparing = false; }
        TransferBatch(slots, insertionIndex);
    }

    private void TransferBatch(IReadOnlyList<RowMoveSlot> slots, int insertionIndex)
    {
        VerifyMutation();
        var collections = slots.Select(slot => slot.Collection.Storage as TreeDataGridRowMoveCollection<T>
            ?? throw new NotSupportedException("Every batch collection must use the same transaction model type."))
            .Append(this).Distinct().ToArray();
        foreach (var collection in collections) collection.VerifyMutation();
        foreach (var collection in collections) collection._preparing = true;
        Dictionary<TreeDataGridRowMoveCollection<T>, List<T>> next;
        Dictionary<TreeDataGridRowMoveCollection<T>, List<NotifyCollectionChangedEventArgs>> changes;
        try
        {
            var keys = slots.Select(slot => (Collection: (TreeDataGridRowMoveCollection<T>)slot.Collection.Storage, slot.Index)).ToArray();
            if (keys.Distinct().Count() != keys.Length) throw new ArgumentException("Batch indices must be unique.", nameof(slots));
            foreach (var (collection, index) in keys)
                if (index < 0 || index >= collection._items.Count) throw new ArgumentOutOfRangeException(nameof(slots));
            if (keys.Length == 0) return;
            if (keys.All(key => ReferenceEquals(key.Collection, this))) keys = keys.OrderBy(key => key.Index).ToArray();
            var local = keys.Where(key => ReferenceEquals(key.Collection, this)).Select(key => key.Index).ToHashSet();
            if (insertionIndex < 0 || insertionIndex > _items.Count - local.Count) throw new ArgumentOutOfRangeException(nameof(insertionIndex));
            next = collections.ToDictionary(collection => collection, collection => new List<T>(collection._items));
            changes = collections.ToDictionary(collection => collection, _ => new List<NotifyCollectionChangedEventArgs>());
            foreach (var collection in collections.Where(collection => !ReferenceEquals(collection, this)))
                foreach (var index in keys.Where(key => ReferenceEquals(key.Collection, collection)).Select(key => key.Index).OrderDescending())
                {
                    var value = next[collection][index]; next[collection].RemoveAt(index);
                    changes[collection].Add(new(NotifyCollectionChangedAction.Remove, value, index));
                }
            var order = Enumerable.Range(0, _items.Count).Select(index => (Collection: this, Index: index)).ToList();
            var desired = order.Where(key => !local.Contains(key.Index)).ToList();
            desired.InsertRange(insertionIndex, keys);
            next[this] = desired.Select(key => key.Collection._items[key.Index]).ToList();
            var remaining = order.Where(key => !local.Contains(key.Index)).ToArray();
            (TreeDataGridRowMoveCollection<T> Collection, int Index)? anchor = insertionIndex < remaining.Length ? remaining[insertionIndex] : null;
            foreach (var key in keys.Reverse())
            {
                var oldIndex = order.IndexOf(key);
                var anchorIndex = anchor is { } nextKey ? order.IndexOf(nextKey) : order.Count;
                var index = anchorIndex - (oldIndex >= 0 && oldIndex < anchorIndex ? 1 : 0);
                var value = key.Collection._items[key.Index];
                if (oldIndex >= 0)
                {
                    if (oldIndex != index)
                    {
                        order.RemoveAt(oldIndex); order.Insert(index, key);
                        changes[this].Add(new(NotifyCollectionChangedAction.Move, value, index, oldIndex));
                    }
                }
                else
                {
                    order.Insert(index, key);
                    changes[this].Add(new(NotifyCollectionChangedAction.Add, value, index));
                }
                anchor = key;
            }
        }
        finally { foreach (var collection in collections) collection._preparing = false; }
        var changed = collections.Where(collection => changes[collection].Count > 0).ToArray();
        foreach (var collection in changed) { collection._items = next[collection]; collection.StorageVersion++; collection._notifying = true; }
        List<Exception> errors = [];
        try
        {
            foreach (var collection in changed)
                foreach (var change in changes[collection])
                    collection.Notify(change, change.Action != NotifyCollectionChangedAction.Move, errors);
        }
        finally { foreach (var collection in changed) collection._notifying = false; }
        foreach (var collection in changed) collection.CompleteChange(errors);
        ThrowErrors(errors);
    }
}
