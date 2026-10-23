using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Pixel-scrolling prototype with viewport-bounded recycling and shared frozen-column geometry.</summary>
public sealed class TreeDataGridRowsPresenter : Panel, ILogicalScrollable
{
    private readonly Dictionary<int, TreeDataGridRowPresenter> _realized = [];
    private readonly Stack<TreeDataGridRowPresenter> _pool = [];
    private TreeDataGrid? _owner;
    private TreeDataGridSource? _source;
    private Vector _offset;
    private Size _viewport;
    private bool _geometryDirty = true;
    private long _generation;
    private readonly Dictionary<ColumnId, double> _measuredWidths = [];
    private readonly Dictionary<NodeId, double> _measuredHeights = [];
    private IReadOnlyList<TreeDataGridRow> _projectionRows = [];
    private (NodeId Id, int OldIndex, double Within)? _anchor;
    private bool _resetHeightMeasurements;

    public TreeDataGridRowsPresenter() => ClipToBounds = true;
    public TreeDataGridLayout? Layout { get; private set; }
    public int RealizedRowCount => _realized.Count;
    public int CreatedRowCount { get; private set; }
    public int RecycledRowCount { get; private set; }
    public int RealizedCellCount => _realized.Values.Sum(x => x.RealizedCellCount);
    public int CreatedCellCount { get; private set; }
    public int RecycledCellCount { get; private set; }
    internal void NotifyCellCreated() => CreatedCellCount++;
    internal void NotifyCellRecycled() => RecycledCellCount++;
    internal void ReportHeaderWidth(TreeDataGridColumn column, double width)
    {
        width = Math.Clamp(width, column.MinWidth, column.MaxWidth);
        if (!double.IsFinite(width) || _measuredWidths.TryGetValue(column.Id, out var previous) && width <= previous + 0.5) return;
        CaptureAnchor();
        _measuredWidths[column.Id] = width;
        _geometryDirty = true;
        InvalidateMeasure();
    }
    public int RetainedRowCount => _realized.Count + _pool.Count;
    public IReadOnlyList<TreeDataGridRowPresenter> RealizedRows => _realized.OrderBy(x => x.Key).Select(x => x.Value).ToArray();
    public bool IsLogicalScrollEnabled => true;
    public bool CanHorizontallyScroll { get; set; } = true;
    public bool CanVerticallyScroll { get; set; } = true;
    public Size ScrollSize => new(24, _owner?.RowHeight ?? 28);
    public Size PageScrollSize => Viewport;
    public Size Extent => Layout?.Extent ?? default;
    public Size Viewport => _viewport;
    public Vector Offset
    {
        get => _offset;
        set
        {
            _anchor = null; // Explicit scrolling takes precedence over a pending projection anchor.
            var next = CoerceOffset(value);
            if (_offset == next) return;
            _offset = next;
            InvalidateMeasure();
            InvalidateArrange();
            _owner?.InvalidateHeaders();
            RaiseScrollInvalidated(EventArgs.Empty);
        }
    }
    public event EventHandler? ScrollInvalidated;
    public void RaiseScrollInvalidated(EventArgs e) => ScrollInvalidated?.Invoke(this, e);

    internal void SetOwner(TreeDataGrid? owner, TreeDataGridSource? source)
    {
        var generation = ++_generation;
        ClearRealized();
        if (_generation != generation) return;
        _pool.Clear();
        _owner = owner;
        _source = source;
        _offset = default;
        _anchor = null;
        _projectionRows = [];
        _measuredWidths.Clear();
        _measuredHeights.Clear();
        _geometryDirty = true;
        Layout = null;
        InvalidateMeasure();
        RaiseScrollInvalidated(EventArgs.Empty);
    }
    internal void Refresh(bool resetMeasurements = false)
    {
        CaptureAnchor();
        _geometryDirty = true;
        if (resetMeasurements) { _measuredWidths.Clear(); _measuredHeights.Clear(); }
        else _resetHeightMeasurements = true;
        var generation = ++_generation;
        ClearRealized();
        if (_generation != generation) return;
        _geometryDirty = true;
        InvalidateMeasure();
        _owner?.InvalidateHeaders();
    }
    public TreeDataGridCellHit? HitTestCell(Point point)
    {
        if (_geometryDirty) UpdateLayout(_viewport);
        if (_geometryDirty) return null;
        if (Layout is not null && _source is not null && _owner is not null &&
            Layout.HitTest(point, Offset, Viewport, out var row, out var column))
            return new TreeDataGridCellHit(_source.Rows[row].Id, Layout.Columns[column].Id, row, column);
        return null;
    }
    public bool ScrollIntoView(NodeId id)
    {
        if (_geometryDirty) UpdateLayout(_viewport);
        if (_geometryDirty) return false;
        var row = _source?.GetViewIndex(id) ?? -1;
        if (row < 0 || Layout is null) return false;
        var top = Layout.GetRowTop(row);
        var bottom = top + Layout.GetRowHeight(row);
        if (top < Offset.Y) Offset = new Vector(Offset.X, top);
        else if (bottom > Offset.Y + Viewport.Height) Offset = new Vector(Offset.X, bottom - Viewport.Height);
        return true;
    }
    public bool BringIntoView(Control target, Rect targetRect)
    {
        var ancestors = target.GetSelfAndVisualAncestors().ToArray();
        var row = ancestors.OfType<TreeDataGridRowPresenter>().FirstOrDefault();
        if (row?.Row is not { } model || !_realized.Values.Any(item => ReferenceEquals(item, row))) return false;
        var columnId = ancestors.OfType<TreeDataGridCellPresenter>().FirstOrDefault()?.Column?.Id;
        if (!ScrollIntoView(model.Id)) return false;
        if (columnId is { } id && Layout is { } layout)
        {
            var index = -1;
            for (var i = 0; i < layout.Columns.Count; ++i)
                if (layout.Columns[i].Id == id) { index = i; break; }
            if (index < 0) return false;
            if (index >= layout.FrozenColumnCount)
            {
                var left = layout.GetColumnLeft(index, Offset.X);
                var right = left + layout.GetColumnWidth(index);
                if (left < layout.FrozenWidth) Offset = new Vector(Offset.X + left - layout.FrozenWidth, Offset.Y);
                else if (right > Viewport.Width) Offset = new Vector(Offset.X + right - Viewport.Width, Offset.Y);
            }
        }
        return true;
    }
    public Control? GetControlInDirection(NavigationDirection direction, Control? from) => null;

    protected override Size MeasureOverride(Size availableSize)
    {
        var viewport = new Size(double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width) : Math.Max(0, Bounds.Width),
            double.IsFinite(availableSize.Height) ? Math.Max(0, availableSize.Height) : Math.Max(0, Bounds.Height));
        for (var pass = 0; pass < 3; ++pass)
        {
            UpdateLayout(viewport);
            if (_geometryDirty) continue;
            RealizeRows();
            var generation = _generation;
            var widthsChanged = false;
            var heightsChanged = false;
            foreach (var (index, row) in _realized.ToArray())
            {
                widthsChanged |= row.MeasureAutoWidths(_measuredWidths);
                row.Measure(new Size(viewport.Width, _owner?.AutoRowHeight == true ? double.PositiveInfinity : Layout!.GetRowHeight(index)));
                if (_generation != generation) break;
                if (_owner?.AutoRowHeight == true && row.Row is { } model && row.DesiredSize.Height > _measuredHeights.GetValueOrDefault(model.Id) + 0.5)
                { _measuredHeights[model.Id] = row.DesiredSize.Height; heightsChanged = true; }
            }
            if (_generation != generation) continue;
            if (!widthsChanged && !heightsChanged) break;
            CaptureAnchor();
            _geometryDirty = true;
        }
        if (_geometryDirty) { UpdateLayout(viewport); if (!_geometryDirty) RealizeRows(); }
        return viewport;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_viewport != finalSize) { UpdateLayout(finalSize); RealizeRows(); }
        foreach (var (index, row) in _realized)
            row.Arrange(new Rect(0, Layout!.GetRowTop(index) - Offset.Y, finalSize.Width, Layout.GetRowHeight(index)));
        return finalSize;
    }

    private void UpdateLayout(Size viewport)
    {
        var generation = _generation;
        var changed = _viewport != viewport;
        var widthChanged = _viewport.Width != viewport.Width;
        if (widthChanged) { CaptureAnchor(); _measuredHeights.Clear(); }
        _viewport = viewport;
        if (_geometryDirty || widthChanged || Layout is null)
        {
            if (_resetHeightMeasurements) { _measuredHeights.Clear(); _resetHeightMeasurements = false; }
            if (_owner is { } owner)
                foreach (var column in owner.VisibleColumns)
                    if (column.Width.IsAuto && !_measuredWidths.ContainsKey(column.Id))
                    {
                        var header = new TextBlock { Text = column.Header?.ToString(), FontSize = owner.FontSize,
                            FontFamily = owner.FontFamily, FontWeight = owner.FontWeight };
                        header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        _measuredWidths[column.Id] = Math.Clamp(header.DesiredSize.Width + 16, column.MinWidth, column.MaxWidth);
                    }
            var currentRows = _source?.Rows ?? Array.Empty<TreeDataGridRow>();
            var nextLayout = new TreeDataGridLayout(currentRows,
                _owner?.Columns.ToArray() ?? [], viewport.Width, _owner?.RowHeight ?? 28, _owner?.RowHeightSelector, _owner?.FrozenColumnCount ?? 0,
                _measuredWidths, _owner?.AutoRowHeight == true ? _measuredHeights : null);
            if (_generation != generation) return;
            Layout = nextLayout;
            RestoreAnchor(currentRows);
            _projectionRows = currentRows;
            _geometryDirty = false;
            changed = true;
        }
        _offset = CoerceOffset(_offset);
        if (changed) { RaiseScrollInvalidated(EventArgs.Empty); _owner?.InvalidateHeaders(); }
    }
    private Vector CoerceOffset(Vector offset) => new(
        Math.Clamp(double.IsFinite(offset.X) ? offset.X : 0, 0, Math.Max(0, Extent.Width - Viewport.Width)),
        Math.Clamp(double.IsFinite(offset.Y) ? offset.Y : 0, 0, Math.Max(0, Extent.Height - Viewport.Height)));
    internal void EnsureCurrentLayout() { if (_geometryDirty) UpdateLayout(_viewport); }
    private void RealizeRows()
    {
        var generation = _generation;
        if (Layout is null || _source is null || _owner is null || Viewport.Width <= 0 || Viewport.Height <= 0)
        { ClearRealized(); TrimPool(); return; }
        var first = Layout.FindRow(Offset.Y);
        var last = Layout.FindRow(Math.Min(Math.Max(0, Layout.Extent.Height - 0.001), Offset.Y + Viewport.Height));
        if (first < 0) { ClearRealized(); TrimPool(); return; }
        first = Math.Max(0, first - 1);
        last = Math.Min(Layout.RowCount - 1, last + 1);
        foreach (var index in _realized.Keys.Where(x => x < first || x > last).ToArray())
        {
            Recycle(index);
            if (_generation != generation) return;
        }
        for (var index = first; index <= last; ++index)
        {
            if (!_realized.TryGetValue(index, out var row))
            {
                if (_pool.TryPop(out row)) RecycledRowCount++;
                else { row = new TreeDataGridRowPresenter(); CreatedRowCount++; }
                row.Prepare(_owner, _source.Rows[index], index);
                if (_generation != generation) { row.Clear(); return; }
                _realized.Add(index, row);
                Children.Add(row);
                _owner.NotifyRowPrepared(row);
                if (_generation != generation) return;
            }
            row.SetLayout(Layout, Offset.X, Viewport.Width);
            if (_generation != generation) return;
        }
        // Retained containers are bounded by the current viewport, including overscan.
        TrimPool();
    }
    private void Recycle(int index)
    {
        // Remove the index first so a RowClearing callback may replace Source without recursive clearing.
        if (!_realized.Remove(index, out var row)) return;
        try { _owner?.NotifyRowClearing(row); }
        finally
        {
            Children.Remove(row);
            row.Clear();
            _pool.Push(row);
        }
    }
    private void ClearRealized()
    {
        foreach (var index in _realized.Keys.ToArray()) Recycle(index);
    }
    private void TrimPool() { while (_pool.Count > 2) _pool.Pop(); }
    private void CaptureAnchor()
    {
        if (_anchor is not null || Layout is null) return;
        var index = Layout.FindRow(Offset.Y);
        if (index >= 0 && index < _projectionRows.Count)
            _anchor = (_projectionRows[index].Id, index, Offset.Y - Layout.GetRowTop(index));
    }
    private void RestoreAnchor(IReadOnlyList<TreeDataGridRow> currentRows)
    {
        if (_anchor is not { } anchor || Layout is null) return;
        _anchor = null;
        var index = _source?.GetViewIndex(anchor.Id) ?? -1;
        if (index < 0 && _source?.TryGetRow(anchor.Id, out _) == true)
            for (var old = anchor.OldIndex - 1; old >= 0 && index < 0; --old)
                index = _source.GetViewIndex(_projectionRows[old].Id);
        if (index < 0) index = Math.Min(anchor.OldIndex, currentRows.Count - 1);
        if (index >= 0) _offset = new Vector(_offset.X, Layout.GetRowTop(index) + Math.Min(anchor.Within, Math.Max(0, Layout.GetRowHeight(index) - 0.001)));
    }
}
