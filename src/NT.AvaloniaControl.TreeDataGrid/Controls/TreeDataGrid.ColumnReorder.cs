using System.ComponentModel;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.TreeDataGrid;

public sealed class TreeDataGridColumnReorderingEventArgs(ColumnId columnId, int oldCollectionIndex, int newCollectionIndex, int oldDisplayIndex, int newDisplayIndex) : CancelEventArgs
{
    public ColumnId ColumnId { get; } = columnId;
    public int OldCollectionIndex { get; } = oldCollectionIndex;
    public int NewCollectionIndex { get; } = newCollectionIndex;
    public int OldDisplayIndex { get; } = oldDisplayIndex;
    public int NewDisplayIndex { get; } = newDisplayIndex;
}

public sealed class TreeDataGridColumnReorderedEventArgs(ColumnId columnId, int oldCollectionIndex, int newCollectionIndex, int oldDisplayIndex, int newDisplayIndex) : EventArgs
{
    public ColumnId ColumnId { get; } = columnId;
    public int OldCollectionIndex { get; } = oldCollectionIndex;
    public int NewCollectionIndex { get; } = newCollectionIndex;
    public int OldDisplayIndex { get; } = oldDisplayIndex;
    public int NewDisplayIndex { get; } = newDisplayIndex;
}

public partial class TreeDataGrid
{
    public static readonly StyledProperty<bool> CanUserReorderColumnsProperty =
        AvaloniaProperty.Register<TreeDataGrid, bool>(nameof(CanUserReorderColumns));
    private bool _raisingColumnReordering;
    private ColumnDrag? _columnDrag;
    private DispatcherTimer? _columnDragTimer;

    public bool CanUserReorderColumns { get => GetValue(CanUserReorderColumnsProperty); set => SetValue(CanUserReorderColumnsProperty, value); }
    public event EventHandler<TreeDataGridColumnReorderingEventArgs>? ColumnReordering;
    public event EventHandler<TreeDataGridColumnReorderedEventArgs>? ColumnReordered;

    /// <summary>Moves a visible column within its frozen or scrolling region. User reorder permissions also apply to this entry point.</summary>
    public bool TryReorderColumn(ColumnId column, int targetDisplayIndex)
    {
        VerifyAccess();
        if (_raisingColumnReordering || !CanUserReorderColumns) return false;
        var snapshot = CaptureColumnOrder();
        var oldDisplayIndex = Array.FindIndex(snapshot.Visible, item => item.Id == column);
        if (oldDisplayIndex < 0 || targetDisplayIndex < 0 || targetDisplayIndex >= snapshot.Visible.Length || targetDisplayIndex == oldDisplayIndex) return false;
        var moved = snapshot.Visible[oldDisplayIndex];
        if (!moved.CanUserReorder || (oldDisplayIndex < snapshot.FrozenCount) != (targetDisplayIndex < snapshot.FrozenCount)) return false;
        var oldCollectionIndex = Array.IndexOf(snapshot.All, moved);
        var newCollectionIndex = Array.IndexOf(snapshot.All, snapshot.Visible[targetDisplayIndex]);
        var request = new TreeDataGridColumnReorderingEventArgs(column, oldCollectionIndex, newCollectionIndex, oldDisplayIndex, targetDisplayIndex);
        _raisingColumnReordering = true;
        try { ColumnReordering?.Invoke(this, request); }
        finally { _raisingColumnReordering = false; }
        if (request.Cancel || !IsColumnOrderCurrent(snapshot) || !CanUserReorderColumns || !moved.CanUserReorder) return false;
        Columns.Move(oldCollectionIndex, newCollectionIndex);
        ColumnReordered?.Invoke(this, new TreeDataGridColumnReorderedEventArgs(column, oldCollectionIndex, newCollectionIndex, oldDisplayIndex, targetDisplayIndex));
        return true;
    }

    internal bool IsColumnReordering => _columnDrag is not null;

    internal bool BeginColumnReorder(TreeDataGridColumn column, IPointer pointer, Point position)
    {
        if (!ShowColumnHeaders || !CanUserReorderColumns || !column.CanUserReorder || _columnDrag is not null) return false;
        var snapshot = CaptureColumnOrder();
        var displayIndex = Array.IndexOf(snapshot.Visible, column);
        if (displayIndex < 0) return false;
        _columnDrag = new ColumnDrag(snapshot, column, pointer, displayIndex) { Position = position };
        pointer.Capture(this);
        _columnDragTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _columnDragTimer.Tick -= ColumnDragTick;
        _columnDragTimer.Tick += ColumnDragTick;
        _columnDragTimer.Start();
        UpdateColumnReorder(position);
        return true;
    }

    internal void CancelColumnReorder()
    {
        var pointer = _columnDrag?.Pointer;
        _columnDrag = null;
        _columnDragTimer?.Stop();
        RefreshColumnReorderVisual();
        if (ReferenceEquals(pointer?.Captured, this)) pointer.Capture(null);
    }

    internal void RefreshColumnReorderVisual()
    {
        if (_headers is null) return;
        var drag = _columnDrag;
        foreach (var header in _headers.Children.OfType<TreeDataGridColumnHeader>())
        {
            var target = drag is { TargetIndex: >= 0 } && header.Column?.Id == drag.Snapshot.Visible[drag.TargetIndex].Id;
            header.SetReorderIndicator(target && drag!.TargetIndex < drag.DisplayIndex,
                target && drag!.TargetIndex > drag.DisplayIndex, drag?.Column.Id == header.Column?.Id);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_columnDrag is not { } drag || !ReferenceEquals(e.Pointer, drag.Pointer)) return;
        UpdateColumnReorder(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_columnDrag is not { } drag || !ReferenceEquals(e.Pointer, drag.Pointer)) return;
        UpdateColumnReorder(e.GetPosition(this));
        var current = _columnDrag;
        var valid = current is not null && current.TargetIndex >= 0 && IsColumnOrderCurrent(current.Snapshot);
        CancelColumnReorder();
        if (valid) TryReorderColumn(current!.Column.Id, current.TargetIndex);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (ReferenceEquals(e.Source, this) && _columnDrag is not null) CancelColumnReorder();
    }

    private ColumnOrder CaptureColumnOrder() => new(Source, Source?.Version, ColumnOperationVersion, Columns.ToArray(),
        VisibleColumns.ToArray(), Math.Min(FrozenColumnCount, VisibleColumns.Count));

    private bool IsColumnOrderCurrent(ColumnOrder snapshot) =>
        ReferenceEquals(Source, snapshot.Source) && Source?.Version == snapshot.SourceVersion && ColumnOperationVersion == snapshot.OperationVersion &&
        Math.Min(FrozenColumnCount, VisibleColumns.Count) == snapshot.FrozenCount && Columns.SequenceEqual(snapshot.All) && VisibleColumns.SequenceEqual(snapshot.Visible);

    private void UpdateColumnReorder(Point position)
    {
        if (_columnDrag is not { } drag) return;
        if (!IsColumnOrderCurrent(drag.Snapshot) || !CanUserReorderColumns || !drag.Column.CanUserReorder)
        { CancelColumnReorder(); return; }
        drag.Position = position;
        drag.TargetIndex = -1;
        var presenter = RowsPresenter;
        var point = _headers is null ? null : this.TranslatePoint(position, _headers);
        if (point is not { } headerPoint || presenter?.Layout is not { } layout || headerPoint.Y < -12 || headerPoint.Y > _headers!.Bounds.Height + 12 ||
            headerPoint.X < 0 || headerPoint.X > _headers.Bounds.Width)
        { RefreshColumnReorderVisual(); return; }
        var sourceFrozen = drag.DisplayIndex < layout.FrozenColumnCount;
        var targetFrozen = headerPoint.X < layout.FrozenWidth;
        if (sourceFrozen != targetFrozen) { RefreshColumnReorderVisual(); return; }
        var begin = sourceFrozen ? 0 : layout.FrozenColumnCount;
        var end = sourceFrozen ? layout.FrozenColumnCount : layout.ColumnCount;
        // A target display index denotes the final slot, so the indicator is placed before
        // an earlier column or after a later column. Hidden columns never become drop slots.
        for (var i = begin; i < end; i++)
        {
            var left = layout.GetColumnLeft(i, presenter.Offset.X);
            var width = layout.GetColumnWidth(i);
            if (headerPoint.X < left + width)
            {
                var before = headerPoint.X < left + width / 2;
                drag.TargetIndex = i < drag.DisplayIndex ? i + (before ? 0 : 1) : i > drag.DisplayIndex ? i - (before ? 1 : 0) : i;
                break;
            }
        }
        if (drag.TargetIndex < 0 && end > begin) drag.TargetIndex = end - 1;
        RefreshColumnReorderVisual();
    }

    private void ColumnDragTick(object? sender, EventArgs e)
    {
        if (_columnDrag is not { } drag) return;
        UpdateColumnReorder(drag.Position);
        if (!ReferenceEquals(_columnDrag, drag) || RowsPresenter is not { Layout: { } layout } presenter || drag.DisplayIndex < layout.FrozenColumnCount || _headers is null) return;
        var position = this.TranslatePoint(drag.Position, _headers);
        if (position is not { } point || point.Y < -12 || point.Y > _headers.Bounds.Height + 12 || point.X < layout.FrozenWidth || point.X > _headers.Bounds.Width) return;
        const double margin = 28;
        var delta = point.X < layout.FrozenWidth + margin ? -24 : point.X > _headers.Bounds.Width - margin ? 24 : 0;
        if (delta == 0) return;
        presenter.Offset = new Vector(presenter.Offset.X + delta, presenter.Offset.Y);
        UpdateColumnReorder(drag.Position);
    }

    private sealed record ColumnOrder(TreeDataGridSource? Source, long? SourceVersion, long OperationVersion,
        TreeDataGridColumn[] All, TreeDataGridColumn[] Visible, int FrozenCount);
    private sealed class ColumnDrag(ColumnOrder snapshot, TreeDataGridColumn column, IPointer pointer, int displayIndex)
    {
        public ColumnOrder Snapshot { get; } = snapshot;
        public TreeDataGridColumn Column { get; } = column;
        public IPointer Pointer { get; } = pointer;
        public int DisplayIndex { get; } = displayIndex;
        public int TargetIndex { get; set; } = -1;
        public Point Position { get; set; }
    }
}
