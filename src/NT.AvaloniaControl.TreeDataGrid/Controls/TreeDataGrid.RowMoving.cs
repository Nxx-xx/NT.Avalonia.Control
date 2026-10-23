using System.ComponentModel;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.TreeDataGrid;

public sealed class TreeDataGridRowMoveRequestedEventArgs(TreeDataGridSource source, TreeDataGridRow row,
    TreeDataGridRow target, TreeDataGridRowDropPosition position, long version) : CancelEventArgs
{
    public TreeDataGridSource Source { get; } = source;
    public TreeDataGridRow Row { get; } = row;
    public TreeDataGridRow Target { get; } = target;
    public TreeDataGridRowDropPosition Position { get; } = position;
    public long SourceVersion { get; } = version;
    /// <summary>An application handler may perform a consistent model transaction and supply its result.</summary>
    public bool Handled { get; set; }
    public TreeDataGridRowMoveResult? Result { get; set; }
}

public sealed class TreeDataGridRowMoveCompletedEventArgs(NodeId node, NodeId target,
    TreeDataGridRowDropPosition position, TreeDataGridRowMoveResult result) : EventArgs
{
    public NodeId NodeId { get; } = node;
    public NodeId TargetNodeId { get; } = target;
    public TreeDataGridRowDropPosition Position { get; } = position;
    public TreeDataGridRowMoveResult Result { get; } = result;
}

public partial class TreeDataGrid
{
    public static readonly StyledProperty<bool> CanUserMoveRowsProperty = AvaloniaProperty.Register<TreeDataGrid, bool>(nameof(CanUserMoveRows));
    private bool _rowMoving;
    private RowPointerDrag? _rowPointerDrag;
    private DispatcherTimer? _rowDragTimer;
    public bool CanUserMoveRows { get => GetValue(CanUserMoveRowsProperty); set => SetValue(CanUserMoveRowsProperty, value); }
    public event EventHandler<TreeDataGridRowMoveRequestedEventArgs>? RowMoveRequested;
    public event EventHandler<TreeDataGridRowMoveCompletedEventArgs>? RowMoveCompleted;

    /// <summary>Requests a same-control move. The active edit commits before the model transaction.</summary>
    public TreeDataGridRowMoveResult TryMoveRow(NodeId node, NodeId target, TreeDataGridRowDropPosition position)
    {
        VerifyAccess();
        if (_rowMoving || !CanUserMoveRows) return new(TreeDataGridRowMoveStatus.Rejected);
        var source = Source;
        if (source is null || source.IsRefreshDeferred || !source.TryGetRow(node, out var row) || !source.TryGetRow(target, out var destination) ||
            source.GetViewIndex(node) < 0 || source.GetViewIndex(target) < 0 || !IsValidRowDrop(row!, destination!, position))
            return new(TreeDataGridRowMoveStatus.Rejected);
        var state = State;
        var current = state?.CurrentCell;
        var selected = state?.SelectedNodes.Contains(node) == true || state?.SelectedCells.Any(cell => cell.Node == node) == true;
        TreeDataGridRowMoveResult result;
        using var movingScope = new RowMoveScope(this);
        try
        {
            if (!CommitEdit()) result = new(IsMoveTargetCurrent(source, row!, destination!)
                ? TreeDataGridRowMoveStatus.ValidationFailed : TreeDataGridRowMoveStatus.Stale);
            else if (!CanUserMoveRows || !IsMoveTargetCurrent(source, row!, destination!)) result = new(TreeDataGridRowMoveStatus.Stale);
            else
            {
                var version = source.Version;
                var operation = ColumnOperationVersion;
                var request = new TreeDataGridRowMoveRequestedEventArgs(source, row!, destination!, position, version);
                if (RowMoveRequested is { } handlers)
                    foreach (EventHandler<TreeDataGridRowMoveRequestedEventArgs> handler in handlers.GetInvocationList())
                    {
                        try { handler(this, request); }
                        catch (Exception error)
                        {
                            if (request.Result is { IsCommitted: true } committed)
                            {
                                request.Handled = true;
                                request.Result = new(TreeDataGridRowMoveStatus.CommittedWithObserverError, committed.NodeId,
                                    committed.Error is null ? error : new AggregateException(committed.Error, error));
                            }
                            else throw;
                        }
                        if (request.Cancel || request.Handled) break;
                    }
                if (request.Cancel) result = new(TreeDataGridRowMoveStatus.Cancelled);
                else if (request.Handled) result = request.Result ?? new(TreeDataGridRowMoveStatus.HandlerFailed,
                    Error: new InvalidOperationException("A handled row move must supply its transaction result."));
                else if (!CanUserMoveRows || !ReferenceEquals(Source, source) || source.Version != version || ColumnOperationVersion != operation || !IsMoveTargetCurrent(source, row!, destination!))
                    result = new(TreeDataGridRowMoveStatus.Stale);
                else result = source.MoveRow(node, target, position);
            }
        }
        catch (Exception error) { result = new(TreeDataGridRowMoveStatus.HandlerFailed, Error: error); }
        try
        {
        if (result.IsCommitted && ReferenceEquals(Source, source) && result.NodeId is { } moved && source.GetViewIndex(moved) >= 0 &&
            (selected || current?.Node == node))
        {
            var columns = VisibleColumns;
            if (columns.Count > 0)
            {
                var column = current is { } cell && GetColumnIndex(cell.Column) >= 0 ? cell.Column : columns[0].Id;
                SelectAddress(new CellAddress(moved, column), extend: false, toggle: false);
                if (ReferenceEquals(Source, source)) { RowsPresenter?.ScrollIntoView(moved); BringColumnIntoView(column); }
            }
            else if (selected && SelectionUnit == TreeDataGridSelectionUnit.Row) State?.Select(moved);
        }
        }
        catch (Exception error)
        {
            result = new(result.IsCommitted ? TreeDataGridRowMoveStatus.CommittedWithObserverError : TreeDataGridRowMoveStatus.HandlerFailed,
                result.NodeId, result.Error is null ? error : new AggregateException(result.Error, error));
        }
        if (RowMoveCompleted is { } observers)
        {
            var args = new TreeDataGridRowMoveCompletedEventArgs(node, target, position, result);
            foreach (EventHandler<TreeDataGridRowMoveCompletedEventArgs> observer in observers.GetInvocationList())
                try { observer(this, args); }
                catch (Exception error)
                {
                    result = new(result.IsCommitted ? TreeDataGridRowMoveStatus.CommittedWithObserverError : TreeDataGridRowMoveStatus.HandlerFailed,
                        result.NodeId, result.Error is null ? error : new AggregateException(result.Error, error));
                }
        }
        return result;
    }

    private bool IsMoveTargetCurrent(TreeDataGridSource source, TreeDataGridRow row, TreeDataGridRow target) =>
        ReferenceEquals(Source, source) && !source.IsRefreshDeferred && source.TryGetRow(row.Id, out var current) &&
        source.TryGetRow(target.Id, out var destination) && ReferenceEquals(current!.Model, row.Model) && ReferenceEquals(destination!.Model, target.Model) &&
        current.ModelIndex == row.ModelIndex && destination.ModelIndex == target.ModelIndex && source.GetViewIndex(row.Id) >= 0 && source.GetViewIndex(target.Id) >= 0;

    private static bool IsValidRowDrop(TreeDataGridRow row, TreeDataGridRow target, TreeDataGridRowDropPosition position) =>
        Enum.IsDefined(position) && row.Id != target.Id && !(target.ModelIndex.Count >= row.ModelIndex.Count &&
        target.ModelIndex.Take(row.ModelIndex.Count).SequenceEqual(row.ModelIndex));

    private void InitializeRowMoving()
    {
        AddHandler(PointerPressedEvent, RowMovePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, RowMovePointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, RowMovePointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, (_, e) => { if (ReferenceEquals(e.Source, this)) CancelRowDrag(); }, handledEventsToo: true);
    }

    private void RowMovePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || !CanUserMoveRows || IsColumnReordering || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Source is not Visual visual) return;
        var cell = visual.GetSelfAndVisualAncestors().OfType<TreeDataGridCellPresenter>().FirstOrDefault();
        if (cell?.Row is not { } row || cell.Column is not TreeDataGridRowHeaderColumn || Source is not { } source) return;
        _rowPointerDrag = new(source, source.Version, ColumnOperationVersion, row, e.Pointer, e.GetPosition(this));
    }

    private bool IsRowDragCurrent(RowPointerDrag drag) => CanUserMoveRows && ReferenceEquals(Source, drag.Source) &&
        drag.Source.Version == drag.Version && ColumnOperationVersion == drag.Operation && drag.Source.TryGetRow(drag.Row.Id, out var row) && row!.ModelIndex == drag.Row.ModelIndex;

    private void RowMovePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_rowPointerDrag is not { } drag || !ReferenceEquals(e.Pointer, drag.Pointer)) return;
        if (!IsRowDragCurrent(drag)) { CancelRowDrag(); return; }
        var point = e.GetPosition(this);
        if (!drag.Started)
        {
            if (Math.Abs(point.X - drag.Start.X) < 5 && Math.Abs(point.Y - drag.Start.Y) < 5) return;
            drag.Started = true; e.Pointer.Capture(this);
            _rowDragTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _rowDragTimer.Tick -= RowDragTick; _rowDragTimer.Tick += RowDragTick; _rowDragTimer.Start();
        }
        UpdateRowDrag(point); e.Handled = true;
    }

    private void UpdateRowDrag(Point point)
    {
        if (_rowPointerDrag is not { } drag || !drag.Started) return;
        if (!IsRowDragCurrent(drag)) { CancelRowDrag(); return; }
        drag.Position = point;
        var presenter = RowsPresenter;
        var local = presenter is null ? null : this.TranslatePoint(point, presenter);
        var hit = local is { } p ? presenter!.HitTestCell(p) : null;
        NodeId? target = null;
        var position = TreeDataGridRowDropPosition.Before;
        if (hit is { } cell && drag.Source.TryGetRow(cell.NodeId, out var row))
        {
            var top = presenter!.Layout!.GetRowTop(cell.RowIndex) - presenter.Offset.Y;
            var fraction = (local!.Value.Y - top) / presenter.Layout.GetRowHeight(cell.RowIndex);
            position = fraction < 0.25 ? TreeDataGridRowDropPosition.Before : fraction > 0.75 ? TreeDataGridRowDropPosition.After : TreeDataGridRowDropPosition.Inside;
            if (IsValidRowDrop(drag.Row, row!, position)) target = cell.NodeId;
        }
        if (target != drag.Target || position != drag.DropPosition) drag.HoverStarted = DateTime.UtcNow;
        drag.Target = target; drag.DropPosition = position;
        RefreshRowDragVisual();
    }

    private void RowDragTick(object? sender, EventArgs e)
    {
        if (_rowPointerDrag is not { Started: true } drag) return;
        UpdateRowDrag(drag.Position);
        if (!ReferenceEquals(_rowPointerDrag, drag) || RowsPresenter is not { } presenter) return;
        var local = this.TranslatePoint(drag.Position, presenter);
        if (local is { } point && point.X >= 0 && point.X < presenter.Viewport.Width)
        {
            var delta = point.Y < 28 ? -24 : point.Y > presenter.Viewport.Height - 28 ? 24 : 0;
            if (delta != 0) { presenter.Offset = new(presenter.Offset.X, presenter.Offset.Y + delta); UpdateRowDrag(drag.Position); }
        }
        if (drag.Target is { } target && drag.DropPosition == TreeDataGridRowDropPosition.Inside &&
            DateTime.UtcNow - drag.HoverStarted > TimeSpan.FromMilliseconds(600) && drag.Source.TryGetRow(target, out var row) && row is { HasChildren: true, IsExpanded: false })
        {
            var path = row.ModelIndex;
            drag.Source.SetExpanded(target, true);
            if (!ReferenceEquals(Source, drag.Source) || !drag.Source.TryGetRow(target, out var current) || current!.ModelIndex != path ||
                !drag.Source.TryGetRow(drag.Row.Id, out var moving) || moving!.ModelIndex != drag.Row.ModelIndex) { CancelRowDrag(); return; }
            drag.Version = drag.Source.Version;
            drag.HoverStarted = DateTime.UtcNow;
            UpdateRowDrag(drag.Position);
        }
    }

    private void RowMovePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_rowPointerDrag is not { } drag || !ReferenceEquals(e.Pointer, drag.Pointer)) return;
        if (!drag.Started) { _rowPointerDrag = null; return; }
        UpdateRowDrag(e.GetPosition(this));
        var valid = ReferenceEquals(_rowPointerDrag, drag) && IsRowDragCurrent(drag) && drag.Target is not null;
        CancelRowDrag();
        if (valid) TryMoveRow(drag.Row.Id, drag.Target!.Value, drag.DropPosition);
        e.Handled = true;
    }

    private void CancelRowDrag()
    {
        var pointer = _rowPointerDrag?.Pointer;
        _rowPointerDrag = null; _rowDragTimer?.Stop(); RefreshRowDragVisual();
        if (ReferenceEquals(pointer?.Captured, this)) pointer.Capture(null);
    }

    private void RefreshRowDragVisual()
    {
        if (RowsPresenter is not { } presenter) return;
        foreach (var row in presenter.RealizedRows)
            row.SetDropIndicator(_rowPointerDrag is { Started: true } drag && row.Row?.Id == drag.Target ? drag.DropPosition : null);
    }

    private sealed class RowPointerDrag(TreeDataGridSource source, long version, long operation, TreeDataGridRow row, IPointer pointer, Point start)
    {
        public TreeDataGridSource Source { get; } = source;
        public long Version { get; set; } = version;
        public long Operation { get; } = operation;
        public TreeDataGridRow Row { get; } = row;
        public IPointer Pointer { get; } = pointer;
        public Point Start { get; } = start;
        public Point Position { get; set; }
        public bool Started { get; set; }
        public NodeId? Target { get; set; }
        public TreeDataGridRowDropPosition DropPosition { get; set; }
        public DateTime HoverStarted { get; set; }
    }
    private sealed class RowMoveScope : IDisposable
    {
        private readonly TreeDataGrid _owner;
        public RowMoveScope(TreeDataGrid owner) { _owner = owner; owner._rowMoving = true; }
        public void Dispose() => _owner._rowMoving = false;
    }
}
