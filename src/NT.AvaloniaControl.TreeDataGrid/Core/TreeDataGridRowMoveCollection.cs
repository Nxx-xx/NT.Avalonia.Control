using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>
/// A creating-thread collection with guarded notifications. The default row adapter can atomically transfer storage
/// between two instances before invoking any observer. All observers run even when another observer throws.
/// </summary>
public sealed partial class TreeDataGridRowMoveCollection<T> : IList<T>, IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged,
    ITreeDataGridCollectionChangeTransaction, IRowMoveCollection where T : class
{
    private readonly int _ownerThread = Environment.CurrentManagedThreadId;
    private List<T> _items;
    private bool _notifying;
    private bool _preparing;
    private EventHandler? _changeCompleted;
    internal long StorageVersion { get; private set; }
    object IRowMoveCollection.Storage => this;
    bool IRowMoveCollection.CanMove => true;
    bool IRowMoveCollection.CanMoveBatch => true;
    long? IRowMoveCollection.MoveVersion => StorageVersion;
    int IRowMoveCollection.MoveCount => Count;
    object IRowMoveCollection.MoveItem(int index) => this[index];
    void IRowMoveCollection.MoveSingle(int oldIndex, int newIndex) => Move(oldIndex, newIndex);
    void IRowMoveCollection.MoveBatch(IReadOnlyList<RowMoveSlot> slots, int insertionIndex) => TransferBatch(slots, insertionIndex);

    public TreeDataGridRowMoveCollection() => _items = [];
    public TreeDataGridRowMoveCollection(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToList();
        if (_items.Any(item => item is null)) throw new ArgumentException("Null models are not supported.", nameof(items));
    }

    public int Count { get { VerifyAccess(); return _items.Count; } }
    public bool IsReadOnly { get { VerifyAccess(); return false; } }
    public T this[int index]
    {
        get { VerifyAccess(); return _items[index]; }
        set
        {
            VerifyMutation(); ArgumentNullException.ThrowIfNull(value);
            var old = _items[index];
            var change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, old, index);
            _items[index] = value; StorageVersion++;
            Publish(change, countChanged: false);
        }
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    bool ITreeDataGridCollectionChangeTransaction.IsChangePending { get { VerifyAccess(); return _notifying; } }
    event EventHandler? ITreeDataGridCollectionChangeTransaction.ChangeCompleted
    {
        add { VerifyAccess(); _changeCompleted += value; }
        remove { VerifyAccess(); _changeCompleted -= value; }
    }

    public void Add(T item) => Insert(Count, item);
    /// <summary>
    /// Appends a completely enumerated, non-null range with one indexed Add notification. Enumeration and allocations
    /// precede storage commit; observer errors are thrown after all listeners run and do not undo committed items.
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        VerifyMutation(); ArgumentNullException.ThrowIfNull(items);
        List<T> next;
        NotifyCollectionChangedEventArgs change;
        _preparing = true;
        try
        {
            var values = items.ToList();
            if (values.Any(item => item is null)) throw new ArgumentException("Null models are not supported.", nameof(items));
            if (values.Count == 0) return;
            var start = _items.Count;
            next = new List<T>(checked(start + values.Count));
            next.AddRange(_items); next.AddRange(values);
            change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, values, start);
        }
        finally { _preparing = false; }
        _items = next; StorageVersion++;
        Publish(change, countChanged: true);
    }
    public void Insert(int index, T item)
    {
        VerifyMutation(); ArgumentNullException.ThrowIfNull(item);
        var change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index);
        _items.Insert(index, item); StorageVersion++;
        Publish(change, countChanged: true);
    }
    public bool Remove(T item)
    {
        VerifyMutation();
        var index = _items.IndexOf(item); if (index < 0) return false;
        RemoveAt(index); return true;
    }
    public void RemoveAt(int index)
    {
        VerifyMutation();
        var item = _items[index];
        var change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index);
        _items.RemoveAt(index); StorageVersion++;
        Publish(change, countChanged: true);
    }
    public void Clear()
    {
        VerifyMutation(); if (_items.Count == 0) return;
        var change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
        _items.Clear(); StorageVersion++;
        Publish(change, countChanged: true);
    }
    public void Move(int oldIndex, int newIndex)
    {
        VerifyMutation();
        if (oldIndex < 0 || oldIndex >= _items.Count) throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if (newIndex < 0 || newIndex >= _items.Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        if (oldIndex == newIndex) return;
        var item = _items[oldIndex];
        var next = new List<T>(_items);
        next.RemoveAt(oldIndex); next.Insert(newIndex, item);
        var change = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex);
        _items = next; StorageVersion++;
        Publish(change, countChanged: false);
    }

    public bool Contains(T item) { VerifyAccess(); return _items.Contains(item); }
    public int IndexOf(T item) { VerifyAccess(); return _items.IndexOf(item); }
    public void CopyTo(T[] array, int arrayIndex) { VerifyAccess(); _items.CopyTo(array, arrayIndex); }
    public IEnumerator<T> GetEnumerator() { VerifyAccess(); return Enumerate().GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<T> Enumerate()
    {
        VerifyAccess(); var version = StorageVersion;
        for (var index = 0; index < _items.Count; index++)
        {
            VerifyAccess();
            if (version != StorageVersion) throw new InvalidOperationException("The collection changed during enumeration.");
            yield return _items[index];
        }
        VerifyAccess();
        if (version != StorageVersion) throw new InvalidOperationException("The collection changed during enumeration.");
    }

    internal static void Transfer(TreeDataGridRowMoveCollection<T> from, int fromIndex, TreeDataGridRowMoveCollection<T> to, int toIndex)
    {
        from.VerifyMutation(); to.VerifyMutation();
        if (ReferenceEquals(from, to)) throw new InvalidOperationException("Use Move for a single collection.");
        if (fromIndex < 0 || fromIndex >= from._items.Count) throw new ArgumentOutOfRangeException(nameof(fromIndex));
        if (toIndex < 0 || toIndex > to._items.Count) throw new ArgumentOutOfRangeException(nameof(toIndex));
        var item = from._items[fromIndex];
        // Prepare every allocation before changing either storage reference.
        var fromItems = new List<T>(from._items); fromItems.RemoveAt(fromIndex);
        var toItems = new List<T>(to._items); toItems.Insert(toIndex, item);
        var removed = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, fromIndex);
        var added = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, toIndex);
        from._items = fromItems; to._items = toItems; from.StorageVersion++; to.StorageVersion++;
        from._notifying = to._notifying = true;
        List<Exception> errors = [];
        try
        {
            from.Notify(removed, countChanged: true, errors);
            to.Notify(added, countChanged: true, errors);
        }
        finally { from._notifying = to._notifying = false; }
        from.CompleteChange(errors); to.CompleteChange(errors);
        ThrowErrors(errors);
    }

    private void Publish(NotifyCollectionChangedEventArgs change, bool countChanged)
    {
        _notifying = true; List<Exception> errors = [];
        try { Notify(change, countChanged, errors); }
        finally { _notifying = false; }
        CompleteChange(errors);
        ThrowErrors(errors);
    }
    private void CompleteChange(List<Exception> errors)
    {
        if (_changeCompleted is not { } handlers) return;
        foreach (EventHandler handler in handlers.GetInvocationList())
            try { handler(this, EventArgs.Empty); } catch (Exception error) { errors.Add(error); }
    }
    private void Notify(NotifyCollectionChangedEventArgs change, bool countChanged, List<Exception> errors)
    {
        if (countChanged) NotifyProperty(nameof(Count), errors);
        NotifyProperty("Item[]", errors);
        if (CollectionChanged is not { } handlers) return;
        foreach (NotifyCollectionChangedEventHandler handler in handlers.GetInvocationList())
            try { handler(this, change); } catch (Exception error) { errors.Add(error); }
    }
    private void NotifyProperty(string property, List<Exception> errors)
    {
        if (PropertyChanged is not { } handlers) return;
        var args = new PropertyChangedEventArgs(property);
        foreach (PropertyChangedEventHandler handler in handlers.GetInvocationList())
            try { handler(this, args); } catch (Exception error) { errors.Add(error); }
    }
    private static void ThrowErrors(List<Exception> errors)
    {
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Committed collection observers failed.", errors);
    }
    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("A row move collection must be accessed on its creating thread.");
    }
    private void VerifyMutation()
    {
        VerifyAccess();
        if (_notifying || _preparing) throw new InvalidOperationException("A row move collection cannot be changed during range preparation or notifications.");
    }
}
