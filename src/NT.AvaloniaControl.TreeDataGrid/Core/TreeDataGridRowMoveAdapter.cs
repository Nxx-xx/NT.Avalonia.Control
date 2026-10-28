using System.Collections;
using System.Collections.ObjectModel;

namespace NT.AvaloniaControl.TreeDataGrid;

// Capabilities are explicit. A writable IList or a coincidentally named Move method is insufficient.
internal interface IRowMoveCollection
{
    object Storage { get; }
    bool CanMove { get; }
    bool CanMoveBatch { get; }
    long? MoveVersion { get; }
    int MoveCount { get; }
    object MoveItem(int index);
    void MoveSingle(int oldIndex, int newIndex);
    void MoveBatch(IReadOnlyList<RowMoveSlot> slots, int insertionIndex);
}

internal readonly record struct RowMoveSlot(IRowMoveCollection Collection, int Index);

internal static class RowMoveAdapters
{
    internal static IRowMoveCollection? Get<T>(IEnumerable<T>? collection) where T : class => collection switch
    {
        IRowMoveCollection { CanMove: true } adapter => adapter,
        ObservableCollection<T> observable when observable.GetType() == typeof(ObservableCollection<T>) => new ObservableMoveAdapter<T>(observable),
        _ => null,
    };

    internal static IRowMoveCollection? GetUntyped(IEnumerable collection)
    {
        if (collection is IRowMoveCollection adapter) return adapter;
        var type = collection.GetType();
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ObservableCollection<>)
            || type.GenericTypeArguments[0].IsValueType) return null;
        return (IRowMoveCollection)Activator.CreateInstance(typeof(ObservableMoveAdapter<>).MakeGenericType(type.GenericTypeArguments), collection)!;
    }

    private sealed class ObservableMoveAdapter<T>(ObservableCollection<T> collection) : IRowMoveCollection where T : class
    {
        public object Storage => collection;
        public bool CanMove => true;
        public bool CanMoveBatch => false;
        public long? MoveVersion => null;
        public int MoveCount => collection.Count;
        public object MoveItem(int index) => collection[index];
        public void MoveSingle(int oldIndex, int newIndex) => collection.Move(oldIndex, newIndex);
        public void MoveBatch(IReadOnlyList<RowMoveSlot> slots, int insertionIndex) => throw new NotSupportedException("Batch moves require transaction collections.");
    }
}
