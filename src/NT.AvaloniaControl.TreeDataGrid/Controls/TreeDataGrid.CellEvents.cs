namespace NT.AvaloniaControl.TreeDataGrid;

public enum TreeDataGridCellValueChangeReason { EditCommit, ModelNotification, Refresh }

/// <summary>The identity and position captured for one prepared cell lifetime.</summary>
public sealed class TreeDataGridCellEventArgs : EventArgs
{
    internal TreeDataGridCellEventArgs(TreeDataGridSource source, TreeDataGridCellPresenter container,
        TreeDataGridRow row, int rowIndex, TreeDataGridColumn column, int columnIndex)
    { Source = source; Container = container; Model = row.Model; NodeId = row.Id; ColumnId = column.Id; SourceIndex = row.ModelIndex; RowIndex = rowIndex; ColumnIndex = columnIndex; }
    public TreeDataGridSource Source { get; }
    public TreeDataGridCellPresenter Container { get; }
    public object Model { get; }
    public NodeId NodeId { get; }
    public ColumnId ColumnId { get; }
    public IndexPath SourceIndex { get; }
    public int RowIndex { get; }
    public int ColumnIndex { get; }
}

/// <summary>A committed value observation. Positions can be -1 for an offscreen or removed edit target.</summary>
public sealed class TreeDataGridCellValueChangedEventArgs : EventArgs
{
    internal TreeDataGridCellValueChangedEventArgs(TreeDataGridSource source, object model, CellAddress cell,
        IndexPath sourceIndex, int rowIndex, int columnIndex, bool hasOldValue, object? oldValue,
        bool hasNewValue, object? newValue, TreeDataGridCellValueChangeReason reason)
    { Source = source; Model = model; Cell = cell; SourceIndex = sourceIndex; RowIndex = rowIndex; ColumnIndex = columnIndex;
        HasOldValue = hasOldValue; OldValue = oldValue; HasNewValue = hasNewValue; NewValue = newValue; Reason = reason; }
    public TreeDataGridSource Source { get; }
    public object Model { get; }
    public CellAddress Cell { get; }
    public IndexPath SourceIndex { get; }
    public int RowIndex { get; }
    public int ColumnIndex { get; }
    public bool HasOldValue { get; }
    public object? OldValue { get; }
    public bool HasNewValue { get; }
    public object? NewValue { get; }
    public TreeDataGridCellValueChangeReason Reason { get; }
}

public partial class TreeDataGrid
{
    private sealed record CellObservation(TreeDataGridCellEventArgs Identity, TreeDataGridColumn Column)
    {
        internal object? Value;
    }
    private sealed class CellWrite(TreeDataGrid owner, TreeDataGridSource source, TreeDataGridEditSession edit, IndexPath path) : IDisposable
    {
        internal TreeDataGridSource Source { get; } = source;
        internal TreeDataGridEditSession Edit { get; } = edit;
        internal IndexPath Path { get; } = path;
        public void Dispose() { if (ReferenceEquals(owner._cellWrite, this)) owner._cellWrite = null; }
    }
    private readonly Dictionary<TreeDataGridCellPresenter, CellObservation> _observedCells = [];
    private CellWrite? _cellWrite;
    private bool _observingCellValues;
    public event EventHandler<TreeDataGridCellEventArgs>? CellPrepared;
    public event EventHandler<TreeDataGridCellEventArgs>? CellClearing;
    public event EventHandler<TreeDataGridCellValueChangedEventArgs>? CellValueChanged;

    internal bool TrackCell(TreeDataGridCellPresenter cell, int rowIndex)
    {
        if (Source is not { } source || cell.Row is not { } row || cell.Column is not { } column) return false;
        var generation = cell.Generation;
        var value = column.ReadValue(row.Model);
        if (cell.Generation != generation || !ReferenceEquals(Source, source) || cell.Row != row || cell.Column != column) return false;
        _observedCells[cell] = new(new(source, cell, row, rowIndex, column, cell.ColumnIndex), column) { Value = value };
        return true;
    }
    internal void NotifyCellPrepared(TreeDataGridCellPresenter cell)
    {
        if (!_observedCells.TryGetValue(cell, out var observation)) return;
        List<Exception>? errors = null;
        TreeDataGridEventDispatch.Invoke(CellPrepared, this, observation.Identity, ref errors);
        TreeDataGridEventDispatch.Throw(errors);
    }
    internal void NotifyCellClearing(TreeDataGridCellPresenter cell)
    {
        if (!_observedCells.Remove(cell, out var observation)) return;
        List<Exception>? errors = null;
        TreeDataGridEventDispatch.Invoke(CellClearing, this, observation.Identity, ref errors);
        TreeDataGridEventDispatch.Throw(errors);
    }
    internal void UntrackCell(TreeDataGridCellPresenter cell) => _observedCells.Remove(cell);
    internal void ObserveCellValue(TreeDataGridCellPresenter cell)
    {
        if (_observingCellValues || !_observedCells.TryGetValue(cell, out var observation)) return;
        ObserveCellValues(TreeDataGridCellValueChangeReason.ModelNotification, [observation]);
    }
    private void ObserveCellValues(TreeDataGridCellValueChangeReason reason, IEnumerable<CellObservation>? observations = null)
    {
        if (_observingCellValues) return;
        _observingCellValues = true;
        List<Exception>? errors = null;
        try
        {
            foreach (var observation in (observations ?? _observedCells.Values).ToArray())
            {
                var identity = observation.Identity;
                var source = identity.Source;
                var cell = new CellAddress(identity.NodeId, identity.ColumnId);
                if (_cellWrite?.Edit.Cell == cell && ReferenceEquals(_cellWrite.Source, source)) continue;
                if (!ReferenceEquals(Source, source) || !_observedCells.TryGetValue(identity.Container, out var current)
                    || !ReferenceEquals(current, observation) || !source.TryGetRow(cell.Node, out var row) || !ReferenceEquals(row!.Model, identity.Model)) continue;
                try
                {
                    var version = source.Version;
                    var value = observation.Column.ReadValue(identity.Model);
                    if (!ReferenceEquals(Source, source) || source.Version != version || !_observedCells.TryGetValue(identity.Container, out current)
                        || !ReferenceEquals(current, observation)) continue;
                    var previous = observation.Value; observation.Value = value;
                    if (Equals(previous, value)) continue;
                    RaiseCellValueChanged(new(source, identity.Model, cell, row!.ModelIndex, source.GetViewIndex(cell.Node),
                        GetColumnIndex(cell.Column), true, previous, true, value, reason));
                }
                catch (Exception error) { (errors ??= []).Add(error); }
            }
        }
        finally { _observingCellValues = false; }
        TreeDataGridEventDispatch.Throw(errors);
    }
    private IDisposable BeginCellWrite(TreeDataGridSource source, TreeDataGridEditSession edit)
    {
        var path = source.TryGetRow(edit.Cell.Node, out var row) ? row!.ModelIndex : default;
        return _cellWrite = new(this, source, edit, path);
    }
    private void NotifyEditValueCommitted(TreeDataGridEditSession edit, object? value)
    {
        if (_cellWrite is not { } write || !ReferenceEquals(write.Edit, edit)) return;
        var source = write.Source;
        // The accepted writer payload is always known, even when its callback removed the old target.
        var path = source.TryGetRow(edit.Cell.Node, out var row) && ReferenceEquals(row!.Model, edit.Model) ? row.ModelIndex : write.Path;
        var args = new TreeDataGridCellValueChangedEventArgs(source, edit.Model, edit.Cell, path,
            source.GetViewIndex(edit.Cell.Node), ReferenceEquals(Source, source) ? GetColumnIndex(edit.Cell.Column) : -1, true, edit.OriginalValue, true, value,
            TreeDataGridCellValueChangeReason.EditCommit);
        foreach (var observation in _observedCells.Values)
            if (observation.Identity.NodeId == edit.Cell.Node && observation.Identity.ColumnId == edit.Cell.Column
                && ReferenceEquals(observation.Identity.Source, source)) observation.Value = value;
        RaiseCellValueChanged(args);
    }
    private void RaiseCellValueChanged(TreeDataGridCellValueChangedEventArgs args)
    {
        List<Exception>? errors = null;
        TreeDataGridEventDispatch.Invoke(CellValueChanged, this, args, ref errors);
        TreeDataGridEventDispatch.Throw(errors);
    }
}
