using Avalonia;

namespace NT.AvaloniaControl.PdfViewer;

/// <summary>Page geometry in Avalonia DIPs; PDF text geometry stays in unrotated page points.</summary>
internal sealed class PdfPageLayout
{
    internal const double Gap = 20;
    internal const double SpreadGap = 12;
    private Rect[] _pages = [];
    private List<int[]> _rows = [];
    private double[] _rowTops = [];
    private double[] _rowHeights = [];

    internal IReadOnlyList<Rect> Pages => _pages;
    /// <summary>Visible page indexes per row in visual order; one or two pages each.</summary>
    internal IReadOnlyList<int[]> Rows => _rows;
    internal Size Extent { get; private set; }
    internal double Scale { get; private set; } = 96d / 72;
    internal int Rotation { get; private set; }
    internal PdfPageDisplayMode DisplayMode { get; private set; }

    internal void Build(IReadOnlyList<PdfPageInfo> pages, double zoom, int rotation, double viewportWidth,
        PdfPageDisplayMode mode, int currentPage, bool showCover)
    {
        Scale = zoom * 96d / 72;
        Rotation = rotation;
        DisplayMode = mode;
        var rotated = rotation is 90 or 270;
        _rows = [];
        _pages = new Rect[pages.Count];
        if (pages.Count == 0)
        {
            _rowTops = _rowHeights = [];
            Extent = default;
            return;
        }

        _rows = mode switch
        {
            PdfPageDisplayMode.SinglePage => [[Math.Clamp(currentPage, 0, pages.Count - 1)]],
            PdfPageDisplayMode.TwoPage => [SpreadPages(currentPage, pages.Count, showCover)],
            PdfPageDisplayMode.TwoPageContinuous => BuildSpreads(pages.Count, showCover),
            _ => Enumerable.Range(0, pages.Count).Select(index => new[] { index }).ToList(),
        };

        double PageWidth(int index) => (rotated ? pages[index].Height : pages[index].Width) * Scale;
        double PageHeight(int index) => (rotated ? pages[index].Width : pages[index].Height) * Scale;

        var width = Gap;
        foreach (var row in _rows)
        {
            var rowWidth = Gap * 2;
            foreach (var index in row) rowWidth += PageWidth(index);
            if (row.Length > 1) rowWidth += SpreadGap;
            width = Math.Max(width, rowWidth);
        }
        width = Math.Max(width, double.IsFinite(viewportWidth) ? viewportWidth : 0);

        _rowTops = new double[_rows.Count];
        _rowHeights = new double[_rows.Count];
        var y = Gap;
        for (var row = 0; row < _rows.Count; row++)
        {
            var pagesInRow = _rows[row];
            var rowHeight = pagesInRow.Max(PageHeight);
            var rowWidth = pagesInRow.Sum(PageWidth) + (pagesInRow.Length > 1 ? SpreadGap : 0);
            var x = (width - rowWidth) / 2;
            foreach (var index in pagesInRow)
            {
                var w = PageWidth(index);
                var h = PageHeight(index);
                // Pages are centered vertically inside mixed-height rows.
                _pages[index] = new Rect(x, y + (rowHeight - h) / 2, w, h);
                x += w + SpreadGap;
            }
            _rowTops[row] = y;
            _rowHeights[row] = rowHeight;
            y += rowHeight + Gap;
        }
        Extent = new Size(width, y);
    }

    private static List<int[]> BuildSpreads(int pageCount, bool showCover)
    {
        var rows = new List<int[]>();
        var page = 0;
        while (page < pageCount)
        {
            var spread = SpreadPages(page, pageCount, showCover);
            rows.Add(spread);
            page = spread[^1] + 1;
        }
        return rows;
    }

    /// <summary>The one or two pages displayed together with the given page in two-page modes.</summary>
    internal static int[] SpreadPages(int pageIndex, int pageCount, bool showCover)
    {
        pageIndex = Math.Clamp(pageIndex, 0, pageCount - 1);
        if (showCover && pageIndex == 0) return [0];
        var start = showCover ? 1 + (pageIndex - 1) / 2 * 2 : pageIndex / 2 * 2;
        return pageCount - start >= 2 ? [start, start + 1] : [start];
    }

    internal int PageAt(double y)
    {
        if (_rows.Count == 0) return -1;
        var low = 0;
        var high = _rows.Count - 1;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (_rowTops[mid] + _rowHeights[mid] + Gap / 2 < y) low = mid + 1;
            else high = mid;
        }
        return _rows[low][0];
    }

    /// <summary>The page whose rectangle contains the point, or -1 outside every visible page.</summary>
    internal int PageIndexAt(Point point)
    {
        var row = PageAt(point.Y);
        if (row < 0) return -1;
        foreach (var index in _rows[RowIndexOf(row)])
            if (_pages[index].Contains(point))
                return index;
        return -1;
    }

    internal int RowIndexOf(int firstPageOfRow) =>
        _rows.FindIndex(row => row[0] == firstPageOfRow);

    /// <summary>Row indexes whose vertical span intersects the given top and bottom coordinates.</summary>
    internal (int First, int Last) RowsBetween(double top, double bottom)
    {
        if (_rows.Count == 0) return (0, -1);
        var first = PageAt(top);
        var last = PageAt(bottom);
        return (Math.Max(0, RowIndexOf(first) - 1), Math.Min(_rows.Count - 1, RowIndexOf(last) + 1));
    }

    internal Point ToPage(int index, Point point, PdfPageInfo page)
    {
        var bounds = _pages[index];
        var x = (point.X - bounds.X) / Scale;
        var y = (point.Y - bounds.Y) / Scale;
        return Rotation switch
        {
            90 => new Point(y, page.Height - x),
            180 => new Point(page.Width - x, page.Height - y),
            270 => new Point(page.Width - y, x),
            _ => new Point(x, y),
        };
    }

    internal Rect ToDisplay(int index, Rect rect, PdfPageInfo page)
    {
        var transformed = Rotation switch
        {
            90 => new Rect(page.Height - rect.Bottom, rect.X, rect.Height, rect.Width),
            180 => new Rect(page.Width - rect.Right, page.Height - rect.Bottom, rect.Width, rect.Height),
            270 => new Rect(rect.Y, page.Width - rect.Right, rect.Height, rect.Width),
            _ => rect,
        };
        var bounds = _pages[index];
        return new Rect(bounds.X + transformed.X * Scale, bounds.Y + transformed.Y * Scale,
            transformed.Width * Scale, transformed.Height * Scale);
    }
}
