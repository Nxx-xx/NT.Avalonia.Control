using Avalonia;
using Avalonia.Controls;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Recycles only visible/frozen headers; shares geometry with cells.</summary>
public sealed class TreeDataGridColumnHeadersPresenter : Panel
{
    private TreeDataGrid? _owner;
    private readonly Dictionary<int, TreeDataGridColumnHeader> _headers = [];
    private readonly Stack<TreeDataGridColumnHeader> _pool = [];
    private long _generation;
    public TreeDataGridColumnHeadersPresenter() => ClipToBounds = true;
    internal void SetOwner(TreeDataGrid? owner)
    {
        var generation = ++_generation;
        try { ClearHeaders(); }
        finally { try { ReleasePool(); } finally { if (_generation == generation) { _owner = owner; RefreshColumns(); } } }
    }
    internal void ResetFactory()
    {
        _generation++;
        try { ClearHeaders(); } finally { try { ReleasePool(); } finally { InvalidateMeasure(); } }
    }
    internal void RefreshSource()
    {
        _generation++;
        try { ClearHeaders(); } finally { InvalidateMeasure(); }
    }
    internal void RefreshColumns()
    {
        if (_owner?.ShowColumnHeaders != true) ClearHeaders();
        InvalidateMeasure();
    }
    private void ClearHeaders()
    {
        List<Exception>? errors = null;
        foreach (var pair in _headers.ToArray())
            if (_headers.TryGetValue(pair.Key, out var current) && ReferenceEquals(current, pair.Value))
                try { Recycle(pair.Key); } catch (Exception error) { (errors ??= []).Add(error); }
        TreeDataGridEventDispatch.Throw(errors);
    }
    private void Recycle(int index, bool canPool = true)
    {
        if (!_headers.Remove(index, out var header)) return;
        var generation = _generation; var session = header.ElementSession;
        List<Exception>? errors = null;
        try { header.Clear(); } catch (Exception error) { (errors ??= []).Add(error); }
        try { Children.Remove(header); } catch (Exception error) { (errors ??= []).Add(error); }
        try
        {
            if (session.Recycle(header, canPool && errors is null && generation == _generation))
            {
                if (generation == _generation && session.IsCurrent) _pool.Push(header);
                else session.Discard(header);
            }
        }
        catch (Exception error) { (errors ??= []).Add(error); }
        TreeDataGridEventDispatch.Throw(errors);
    }
    private void ReleasePool(int retain = 0)
    {
        List<Exception>? errors = null;
        while (_pool.Count > retain)
        {
            var header = _pool.Pop();
            try { header.ElementSession.Discard(header); } catch (Exception error) { (errors ??= []).Add(error); }
        }
        TreeDataGridEventDispatch.Throw(errors);
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        if (_owner?.ShowColumnHeaders != true) return default;
        var generation = _generation;
        var presenter = _owner?.RowsPresenter;
        presenter?.EnsureCurrentLayout();
        if (presenter?.Layout is { } layout && _owner is { } owner)
        {
            var visible = layout.GetVisibleColumns(presenter.Offset.X, presenter.Viewport.Width).ToHashSet();
            foreach (var index in _headers.Keys.Where(x => !visible.Contains(x) || x >= layout.ColumnCount || !ReferenceEquals(_headers[x].Column, layout.Columns[x])).ToArray())
            {
                Recycle(index);
                if (generation != _generation) return default;
            }
            foreach (var index in visible)
            {
                var column = layout.Columns[index];
                if (!_headers.TryGetValue(index, out var header))
                {
                    var session = owner.FactorySession;
                    header = _pool.TryPop(out var recycled) ? recycled : session.Create<TreeDataGridColumnHeader>(TreeDataGridElementKind.ColumnHeader);
                    if (generation != _generation || !session.IsCurrent) { session.Discard(header); return default; }
                    header.AttachFactory(session);
                    _headers.Add(index, header);
                }
                try
                {
                    if (header.Parent is null) Children.Add(header);
                    if (generation != _generation) return default;
                    header.Prepare(owner, column);
                }
                catch
                {
                    if (_headers.TryGetValue(index, out var failed) && ReferenceEquals(header, failed)) Recycle(index, canPool: false);
                    InvalidateMeasure(); throw;
                }
                if (generation != _generation) return default;
                if (column.Width.IsAuto)
                {
                    header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    presenter.ReportHeaderWidth(column, header.DesiredSize.Width);
                }
                header.Measure(new Size(layout.GetColumnWidth(index), double.PositiveInfinity));
            }
            ReleasePool(retain: 2);
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
