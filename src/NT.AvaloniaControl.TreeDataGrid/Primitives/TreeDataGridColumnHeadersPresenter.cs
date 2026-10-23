using Avalonia;
using Avalonia.Controls;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Recycles only visible/frozen headers; shares geometry with cells.</summary>
public sealed class TreeDataGridColumnHeadersPresenter : Panel
{
    private TreeDataGrid? _owner;
    private readonly Dictionary<int, TreeDataGridColumnHeader> _headers = [];
    private readonly Stack<TreeDataGridColumnHeader> _pool = [];
    public TreeDataGridColumnHeadersPresenter() => ClipToBounds = true;
    internal void SetOwner(TreeDataGrid? owner) { ClearHeaders(); _owner = owner; RefreshColumns(); }
    internal void RefreshColumns() { InvalidateMeasure(); }
    private void ClearHeaders()
    {
        foreach (var header in _headers.Values) { Children.Remove(header); header.Clear(); _pool.Push(header); }
        _headers.Clear();
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var presenter = _owner?.RowsPresenter;
        presenter?.EnsureCurrentLayout();
        if (presenter?.Layout is { } layout && _owner is { } owner)
        {
            var visible = layout.GetVisibleColumns(presenter.Offset.X, presenter.Viewport.Width).ToHashSet();
            foreach (var index in _headers.Keys.Where(x => !visible.Contains(x)).ToArray())
            {
                var old = _headers[index]; _headers.Remove(index); Children.Remove(old); old.Clear(); _pool.Push(old);
            }
            foreach (var index in visible)
            {
                var column = layout.Columns[index];
                if (!_headers.TryGetValue(index, out var header))
                {
                    header = _pool.TryPop(out var recycled) ? recycled : new TreeDataGridColumnHeader();
                    header.Prepare(owner, column);
                    _headers.Add(index, header); Children.Add(header);
                }
                header.Prepare(owner, column);
                if (column.Width.IsAuto)
                {
                    header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    presenter.ReportHeaderWidth(column, header.DesiredSize.Width);
                }
                header.Measure(new Size(layout.GetColumnWidth(index), double.PositiveInfinity));
            }
            while (_pool.Count > 2) _pool.Pop();
            owner.RefreshColumnReorderVisual();
        }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, Math.Max(28, _headers.Values.Select(x => x.DesiredSize.Height).DefaultIfEmpty().Max()));
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var presenter = _owner?.RowsPresenter;
        if (presenter?.Layout is { } layout)
            foreach (var (index, header) in _headers)
                if (index < layout.ColumnCount) TreeDataGridRowPresenter.ArrangeCell(header, layout, index, presenter.Offset.X, finalSize);
        return finalSize;
    }
}
