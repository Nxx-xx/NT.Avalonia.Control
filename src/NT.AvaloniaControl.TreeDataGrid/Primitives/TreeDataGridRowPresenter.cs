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
        _owner = owner;
        Row = row;
        RowIndex = index;
        DataContext = row.Model;
    }
    internal void Clear()
    {
        SetDropIndicator(null);
        foreach (var index in _cells.Keys.ToArray()) RecycleCell(index);
        DataContext = null;
        Row = null;
        RowIndex = -1;
        _layout = null;
        _owner = null;
    }
    internal void SetLayout(TreeDataGridLayout layout, double horizontalOffset, double viewportWidth)
    {
        _layout = layout;
        _horizontalOffset = horizontalOffset;
        if (_owner is { } owner && Row is { } row)
        {
            var visible = layout.GetVisibleColumns(horizontalOffset, viewportWidth).ToHashSet();
            foreach (var index in _cells.Keys.Where(x => !visible.Contains(x) || x >= layout.ColumnCount ||
                !ReferenceEquals(_cells[x].Column, layout.Columns[x])).ToArray()) RecycleCell(index);
            foreach (var index in visible)
                if (!_cells.ContainsKey(index))
                {
                    if (!_pool.TryPop(out var cell)) { cell = new TreeDataGridCellPresenter(); owner.RowsPresenter?.NotifyCellCreated(); }
                    else owner.RowsPresenter?.NotifyCellRecycled();
                    _cells.Add(index, cell);
                    Children.Add(cell);
                    cell.Prepare(owner, row, RowIndex, layout.Columns[index], index);
                    if (!ReferenceEquals(Row, row) || !ReferenceEquals(_owner, owner)) return;
                }
            while (_pool.Count > 2) _pool.Pop();
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
    private void RecycleCell(int index)
    {
        if (!_cells.Remove(index, out var cell)) return;
        Children.Remove(cell);
        cell.Clear();
        _pool.Push(cell);
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
