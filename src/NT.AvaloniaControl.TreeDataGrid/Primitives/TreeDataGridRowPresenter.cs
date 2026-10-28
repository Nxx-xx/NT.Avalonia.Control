using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>A recyclable row containing only viewport-visible and frozen cells.</summary>
public sealed class TreeDataGridRowPresenter : Panel
{
    private readonly Dictionary<int, TreeDataGridCellPresenter> _cells = [];
    private readonly Stack<TreeDataGridCellPresenter> _pool = [];
    private TreeDataGridLayout? _layout;
    private TreeDataGrid? _owner;
    private double _horizontalOffset;
    private DropAdorner? _dropAdorner;
    private ElementFactorySession? _elementSession;
    private bool _prepared, _clearing;
    private long _generation;
    public TreeDataGridRowPresenter() => ClipToBounds = true;
    public TreeDataGridRow? Row { get; private set; }
    public int RowIndex { get; private set; } = -1;
    public int RealizedCellCount => _cells.Count;
    public IReadOnlyList<TreeDataGridCellPresenter> RealizedCells => _cells.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
    internal TreeDataGrid? GridOwner => _owner;
    protected override AutomationPeer OnCreateAutomationPeer() => new TreeDataGridRowPresenterAutomationPeer(this);

    internal void Prepare(TreeDataGrid owner, TreeDataGridRow row, int index)
    {
        Clear();
        SetIdentity(owner, row, index);
    }
    private void SetIdentity(TreeDataGrid owner, TreeDataGridRow row, int index)
    {
        _elementSession = owner.FactorySession;
        _owner = owner;
        Row = row;
        RowIndex = index;
        DataContext = row.Model;
    }
    internal bool PrepareForScrollReuse(TreeDataGridRowsPresenter presenter, long generation, TreeDataGrid owner,
        TreeDataGridSource source, TreeDataGridRow row, int index, ElementFactorySession session)
    {
        Clear(retainHosts: true);
        if (!presenter.IsReuseCurrent(generation, owner, source, session)) return false;
        SetIdentity(owner, row, index);
        return true;
    }
    internal void NotifyPrepared()
    {
        var generation = _generation;
        var owner = _owner!;
        if (!_elementSession!.Prepare(this, owner.Source, Row, RowIndex, null, -1) || generation != _generation) return;
        _prepared = true;
        owner.NotifyRowPrepared(this);
    }
    internal void Clear() => Clear(retainHosts: false);
    internal void ClearForReuse() => Clear(retainHosts: true);
    private void Clear(bool retainHosts)
    {
        if (_clearing) return;
        _clearing = true; _generation++;
        List<Exception>? errors = null;
        try
        {
            if (_prepared)
            {
                _prepared = false;
                try { _owner?.NotifyRowClearing(this); } catch (Exception error) { (errors ??= []).Add(error); }
            }
            try { _elementSession?.Clear(this); } catch (Exception error) { (errors ??= []).Add(error); }
            SetDropIndicator(null);
            foreach (var index in _cells.Keys.ToArray())
                try { RecycleCell(index, retainHost: retainHosts); } catch (Exception error) { (errors ??= []).Add(error); }
        }
        finally
        {
            Row = null; RowIndex = -1; _layout = null; _owner = null;
            try { DataContext = null; } finally { _clearing = false; }
        }
        if (!retainHosts) try { DetachPooledHosts(); } catch (Exception error) { (errors ??= []).Add(error); }
        TreeDataGridEventDispatch.Throw(errors);
    }
    internal void DetachFactory() => _elementSession = null;
    internal ElementFactorySession ElementSession => _elementSession!;
    internal void ReleaseFactory() => _elementSession!.Discard(this);
    internal void ReleaseElements()
    {
        List<Exception>? errors = null;
        while (_pool.TryPop(out var cell))
        {
            try { Children.Remove(cell); } catch (Exception error) { (errors ??= []).Add(error); }
            try { _elementSession!.Discard(cell); } catch (Exception error) { (errors ??= []).Add(error); }
        }
        TreeDataGridEventDispatch.Throw(errors);
    }
    internal void SetLayout(TreeDataGridLayout layout, double horizontalOffset, double viewportWidth)
    {
        try { SetLayoutCore(layout, horizontalOffset, viewportWidth); }
        finally { DetachPooledHosts(); }
    }
    private void SetLayoutCore(TreeDataGridLayout layout, double horizontalOffset, double viewportWidth)
    {
        _layout = layout;
        _horizontalOffset = horizontalOffset;
        if (_owner is { } owner && Row is { } row)
        {
            var generation = _generation;
            var session = _elementSession!;
            var visible = layout.GetVisibleColumns(horizontalOffset, viewportWidth).ToHashSet();
            foreach (var index in _cells.Keys.Where(x => !visible.Contains(x) || x >= layout.ColumnCount ||
                !ReferenceEquals(_cells[x].Column, layout.Columns[x])).ToArray()) RecycleCell(index, retainHost: session.CanRetainHosts);
            if (generation != _generation || !session.IsCurrent) return;
            foreach (var index in visible)
                if (!_cells.ContainsKey(index))
                {
                    if (!_pool.TryPop(out var cell)) { cell = session.Create<TreeDataGridCellPresenter>(TreeDataGridElementKind.Cell); owner.RowsPresenter?.NotifyCellCreated(); }
                    else owner.RowsPresenter?.NotifyCellRecycled();
                    if (generation != _generation || !session.IsCurrent) { session.Discard(cell); return; }
                    _cells.Add(index, cell);
                    try
                    {
                        var retainedHost = ReferenceEquals(cell.Parent, this);
                        if (!retainedHost) Children.Add(cell);
                        if (generation != _generation) return;
                        cell.Prepare(owner, row, RowIndex, layout.Columns[index], index);
                        // Rebinding an attached host must discard its old measurement lifetime.
                        if (retainedHost) cell.InvalidateRecycledMeasure(owner.AutoRowHeight);
                    }
                    catch
                    {
                        if (_cells.TryGetValue(index, out var failed) && ReferenceEquals(failed, cell)) RecycleCell(index, canPool: false);
                        InvalidateMeasure();
                        throw;
                    }
                    if (generation != _generation || !ReferenceEquals(Row, row) || !ReferenceEquals(_owner, owner)) return;
                }
            List<Exception>? errors = null;
            while (_pool.Count > 2)
            {
                var spare = _pool.Pop();
                try { Children.Remove(spare); } catch (Exception error) { (errors ??= []).Add(error); }
                try { session.Discard(spare); } catch (Exception error) { (errors ??= []).Add(error); }
            }
            TreeDataGridEventDispatch.Throw(errors);
        }
        InvalidateMeasure();
        InvalidateArrange();
    }
    internal bool MeasureAutoWidths(IDictionary<ColumnId, double> widths)
    {
        var changed = false;
        foreach (var cell in _cells.Values.ToArray())
            if (cell.Column is { Width.IsAuto: true } column)
            {
                var measured = Math.Clamp(cell.MeasureNaturalWidth(), column.MinWidth, column.MaxWidth);
                if (!widths.TryGetValue(column.Id, out var old) || measured > old + 0.5) { widths[column.Id] = measured; changed = true; }
            }
        return changed;
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_layout is null) return default;
        var height = _owner?.RowHeight ?? 28;
        foreach (var (index, cell) in _cells.ToArray())
        {
            cell.Measure(new Size(_layout.GetColumnWidth(index), _owner?.AutoRowHeight == true ? double.PositiveInfinity : availableSize.Height));
            height = Math.Max(height, cell.DesiredSize.Height);
        }
        return new Size(availableSize.Width, _owner?.AutoRowHeight == true ? height : availableSize.Height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_layout is not null)
            foreach (var (index, cell) in _cells) ArrangeCell(cell, _layout, index, _horizontalOffset, finalSize);
        _dropAdorner?.Arrange(new Rect(finalSize));
        return finalSize;
    }
    internal void SetDropIndicator(TreeDataGridRowDropPosition? position)
    {
        if (position is null)
        {
            if (_dropAdorner is { } existing) Children.Remove(existing);
            _dropAdorner = null;
            return;
        }
        if (_dropAdorner is null)
        {
            _dropAdorner = new DropAdorner { IsHitTestVisible = false, ZIndex = 2 };
            Children.Add(_dropAdorner);
        }
        _dropAdorner.Position = position.Value;
        _dropAdorner.Brush = _owner?.Foreground;
        _dropAdorner.Measure(Bounds.Size);
        _dropAdorner.Arrange(new Rect(Bounds.Size));
        _dropAdorner.InvalidateVisual();
    }
    private sealed class DropAdorner : Control
    {
        public TreeDataGridRowDropPosition Position { get; set; }
        public IBrush? Brush { get; set; }
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var pen = new Pen(Brush, 2);
            if (Position == TreeDataGridRowDropPosition.Inside) context.DrawRectangle(null, pen, new Rect(Bounds.Size).Deflate(1));
            else
            {
                var y = Position == TreeDataGridRowDropPosition.Before ? 1 : Math.Max(1, Bounds.Height - 1);
                context.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
            }
        }
    }
    private void DetachPooledHosts()
    {
        var attached = false;
        foreach (var cell in _pool) if (ReferenceEquals(cell.Parent, this)) { attached = true; break; }
        if (!attached) return;
        List<Exception>? errors = null;
        foreach (var cell in _pool.ToArray())
            if (ReferenceEquals(cell.Parent, this))
                try { Children.Remove(cell); } catch (Exception error) { (errors ??= []).Add(error); }
        TreeDataGridEventDispatch.Throw(errors);
    }
    private void RecycleCell(int index, bool canPool = true, bool retainHost = false)
    {
        if (!_cells.Remove(index, out var cell)) return;
        var session = _elementSession!; var generation = _generation;
        List<Exception>? errors = null;
        try { cell.Clear(); } catch (Exception error) { (errors ??= []).Add(error); }
        // Only a synchronous default-factory row rebind may keep cleared hosts attached. Custom
        // factories still observe fully detached Recycle callbacks. Unused hosts detach in finally.
        if (!retainHost || !session.CanRetainHosts || errors is not null || generation != _generation)
            try { Children.Remove(cell); } catch (Exception error) { (errors ??= []).Add(error); }
        try
        {
            if (session.Recycle(cell, canPool && errors is null && generation == _generation))
            {
                if (generation == _generation && session.IsCurrent) _pool.Push(cell);
                else session.Discard(cell);
            }
        }
        catch (Exception error) { (errors ??= []).Add(error); }
        TreeDataGridEventDispatch.Throw(errors);
    }
    internal static void ArrangeCell(Control cell, TreeDataGridLayout layout, int column, double offset, Size size)
    {
        var x = layout.GetColumnLeft(column, offset);
        var width = layout.GetColumnWidth(column);
        cell.ZIndex = column < layout.FrozenColumnCount ? 1 : 0;
        cell.Arrange(new Rect(x, 0, width, size.Height));
        var bounds = cell.Bounds;
        var clipStart = Math.Max(bounds.X, column < layout.FrozenColumnCount ? 0 : layout.FrozenWidth);
        var clipEnd = Math.Min(bounds.Right, size.Width);
        cell.Clip = new RectangleGeometry(new Rect(Math.Max(0, clipStart - bounds.X), 0, Math.Max(0, clipEnd - clipStart), bounds.Height));
    }
}

public sealed class TreeDataGridRowEventArgs(TreeDataGridRowPresenter container, TreeDataGridRow row, int rowIndex) : EventArgs
{
    public TreeDataGridRowPresenter Container { get; } = container;
    public TreeDataGridRow Row { get; } = row;
    public int RowIndex { get; } = rowIndex;
}
