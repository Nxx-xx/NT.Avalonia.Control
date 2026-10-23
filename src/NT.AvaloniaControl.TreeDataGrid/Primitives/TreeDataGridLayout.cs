using Avalonia;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>Immutable pixel geometry shared by rows, headers, frozen cells and hit testing.</summary>
public sealed class TreeDataGridLayout
{
    private readonly double[] _rowEdges;
    private readonly double[] _columnEdges;

    public TreeDataGridLayout(IReadOnlyList<TreeDataGridRow> rows, IReadOnlyList<TreeDataGridColumn> columns,
        double viewportWidth, double rowHeight = 28, Func<TreeDataGridRow, double>? rowHeightSelector = null,
        int frozenColumnCount = 0)
        : this(rows, columns, viewportWidth, rowHeight, rowHeightSelector, frozenColumnCount, null, null) { }

    internal TreeDataGridLayout(IReadOnlyList<TreeDataGridRow> rows, IReadOnlyList<TreeDataGridColumn> columns,
        double viewportWidth, double rowHeight, Func<TreeDataGridRow, double>? rowHeightSelector,
        int frozenColumnCount, IReadOnlyDictionary<ColumnId, double>? measuredWidths, IReadOnlyDictionary<NodeId, double>? measuredHeights)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);
        if (!double.IsFinite(rowHeight) || rowHeight <= 0) throw new ArgumentOutOfRangeException(nameof(rowHeight));
        Columns = Array.AsReadOnly(columns.Where(column => column.IsVisible).ToArray());
        columns = Columns;
        viewportWidth = double.IsFinite(viewportWidth) ? Math.Max(0, viewportWidth) : 0;
        _rowEdges = new double[rows.Count + 1];
        for (var i = 0; i < rows.Count; ++i)
        {
            var height = rowHeightSelector?.Invoke(rows[i]) ?? rowHeight;
            if (!double.IsFinite(height) || height <= 0) height = rowHeight;
            if (measuredHeights?.TryGetValue(rows[i].Id, out var measuredHeight) == true) height = Math.Max(height, measuredHeight);
            _rowEdges[i + 1] = _rowEdges[i] + height;
        }

        var widths = new double[columns.Count];
        var fixedWidth = 0d;
        var starColumns = new List<int>();
        for (var i = 0; i < columns.Count; ++i)
        {
            var column = columns[i];
            if (column.Width.IsStar) starColumns.Add(i);
            else
            {
                widths[i] = Math.Clamp(column.Width.IsAuto ? measuredWidths?.GetValueOrDefault(column.Id) ?? column.EstimatedAutoWidth : column.Width.Value,
                    column.MinWidth, column.MaxWidth);
                fixedWidth += widths[i];
            }
        }
        var remainder = Math.Max(0, viewportWidth - fixedWidth);
        if (starColumns.Count > 0)
        {
            var maxWeight = starColumns.Max(i => columns[i].Width.Value);
            double Sum(double multiplier) => starColumns.Sum(i => Math.Clamp(
                maxWeight > 0 ? multiplier * (columns[i].Width.Value / maxWeight) : 0,
                columns[i].MinWidth, columns[i].MaxWidth));
            var low = 0d;
            var target = Math.Min(remainder, Sum(double.MaxValue));
            var high = Math.Max(1, target);
            while (Sum(high) < target && high < double.MaxValue / 2) high *= 2;
            for (var iteration = 0; iteration < 100; ++iteration)
            {
                var middle = low + (high - low) / 2;
                if (Sum(middle) < target) low = middle;
                else high = middle;
            }
            var multiplier = low + (high - low) / 2;
            foreach (var i in starColumns) widths[i] = Math.Clamp(maxWeight > 0 ? multiplier * (columns[i].Width.Value / maxWeight) : 0,
                columns[i].MinWidth, columns[i].MaxWidth);
        }
        _columnEdges = new double[columns.Count + 1];
        for (var i = 0; i < widths.Length; ++i) _columnEdges[i + 1] = _columnEdges[i] + widths[i];
        FrozenColumnCount = Math.Clamp(frozenColumnCount, 0, columns.Count);
    }

    public int RowCount => _rowEdges.Length - 1;
    /// <summary>The visible columns captured for this geometry, in display order.</summary>
    public IReadOnlyList<TreeDataGridColumn> Columns { get; }
    public int ColumnCount => _columnEdges.Length - 1;
    public int FrozenColumnCount { get; }
    public double FrozenWidth => _columnEdges[FrozenColumnCount];
    public Size Extent => new(_columnEdges[^1], _rowEdges[^1]);
    public double GetRowTop(int rowIndex) => _rowEdges[rowIndex];
    public double GetRowHeight(int rowIndex) => _rowEdges[rowIndex + 1] - _rowEdges[rowIndex];
    public double GetColumnWidth(int columnIndex) => _columnEdges[columnIndex + 1] - _columnEdges[columnIndex];
    public double GetColumnLeft(int columnIndex, double horizontalOffset) => _columnEdges[columnIndex] -
        (columnIndex < FrozenColumnCount ? 0 : horizontalOffset);
    public Rect GetCellBounds(int rowIndex, int columnIndex, Vector offset) => new(
        GetColumnLeft(columnIndex, offset.X), GetRowTop(rowIndex) - offset.Y,
        GetColumnWidth(columnIndex), GetRowHeight(rowIndex));

    internal IEnumerable<int> GetVisibleColumns(double offset, double viewportWidth)
    {
        for (var i = 0; i < ColumnCount; ++i)
        {
            var left = GetColumnLeft(i, offset);
            var right = left + GetColumnWidth(i);
            if (right > (i < FrozenColumnCount ? 0 : FrozenWidth) && left < viewportWidth) yield return i;
        }
    }

    /// <summary>Finds a row in O(log n). Edge points belong to the following row.</summary>
    public int FindRow(double contentY)
    {
        if (!double.IsFinite(contentY) || contentY < 0 || contentY >= Extent.Height) return -1;
        var index = Array.BinarySearch(_rowEdges, contentY);
        return index >= 0 ? Math.Min(index, RowCount - 1) : ~index - 1;
    }

    public bool HitTest(Point point, Vector offset, Size viewport, out int rowIndex, out int columnIndex)
    {
        rowIndex = columnIndex = -1;
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.X < 0 || point.Y < 0 ||
            point.X >= viewport.Width || point.Y >= viewport.Height) return false;
        rowIndex = FindRow(point.Y + offset.Y);
        if (rowIndex < 0) return false;
        var frozen = point.X < FrozenWidth;
        var contentX = point.X + (frozen ? 0 : offset.X);
        var begin = frozen ? 0 : FrozenColumnCount;
        var end = frozen ? FrozenColumnCount : ColumnCount;
        for (var i = begin; i < end; ++i)
            if (contentX >= _columnEdges[i] && contentX < _columnEdges[i + 1]) { columnIndex = i; return true; }
        return false;
    }
}

public readonly record struct TreeDataGridCellHit(NodeId NodeId, ColumnId ColumnId, int RowIndex, int ColumnIndex);

