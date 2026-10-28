using Avalonia;

namespace NT.AvaloniaControl.PdfViewer;

/// <summary>Page geometry in Avalonia DIPs; PDF text geometry stays in unrotated page points.</summary>
internal sealed class PdfPageLayout
{
    /// <summary>What a <see cref="Build"/> call did to the cached geometry.</summary>
    internal enum BuildResult
    {
        /// <summary>The geometry was already correct; the cached arrays are untouched.</summary>
        Unchanged,
        /// <summary>Only the horizontal centering of rows moved; rows and sizes are reused.</summary>
        Recentered,
        /// <summary>The row and page geometry was rebuilt from scratch.</summary>
        Rebuilt,
    }

    internal const double Gap = 20;
    internal const double SpreadGap = 12;
    /// <summary>Extent widths closer than this are the same layout.</summary>
    private const double WidthEpsilon = .001;
    private Rect[] _pages = [];
    private List<int[]> _rows = [];
    private double[] _rowTops = [];
    private double[] _rowHeights = [];
    private double[] _rowWidths = [];
    /// <summary>Row index per first-page-of-row, -1 elsewhere; makes <see cref="RowIndexOf"/> O(1).</summary>
    private int[] _rowOfPage = [];
    /// <summary>Widest row including its side gaps; the part of the extent width rows force.</summary>
    private double _maxRowPaddedWidth;
    // Build signature: when every field matches, only the viewport width can move the geometry.
    private IReadOnlyList<PdfPageInfo>? _builtPages;
    private double _builtZoom = double.NaN;
    private int _builtRotation = int.MinValue;
    private PdfPageDisplayMode _builtMode;
    private int _builtCurrentPage = -1;
    private bool _builtShowCover;

    internal IReadOnlyList<Rect> Pages => _pages;
    /// <summary>Visible page indexes per row in visual order; one or two pages each.</summary>
    internal IReadOnlyList<int[]> Rows => _rows;
    internal Size Extent { get; private set; }
    internal double Scale { get; private set; } = 96d / 72;
    internal int Rotation { get; private set; }
    internal PdfPageDisplayMode DisplayMode { get; private set; }

    internal BuildResult Build(IReadOnlyList<PdfPageInfo> pages, double zoom, int rotation, double viewportWidth,
        PdfPageDisplayMode mode, int currentPage, bool showCover)
    {
        // Continuous layouts ignore the current page; the discrete layouts show only it.
        var effectivePage = mode is PdfPageDisplayMode.SinglePage or PdfPageDisplayMode.TwoPage
            ? Math.Clamp(currentPage, 0, Math.Max(0, pages.Count - 1))
            : 0;
        var width = double.IsFinite(viewportWidth) ? viewportWidth : 0;
        if (ReferenceEquals(pages, _builtPages) && zoom == _builtZoom && rotation == _builtRotation &&
            mode == _builtMode && showCover == _builtShowCover && effectivePage == _builtCurrentPage)
        {
            if (pages.Count == 0) return BuildResult.Unchanged;
            // Same geometry under a new viewport width: only the horizontal centering can move,
            // and only when the extent width follows the viewport instead of the widest row.
            var extentWidth = Math.Max(_maxRowPaddedWidth, width);
            if (Math.Abs(extentWidth - Extent.Width) <= WidthEpsilon) return BuildResult.Unchanged;
            RecenterHorizontally(extentWidth);
            return BuildResult.Recentered;
        }

        Scale = zoom * 96d / 72;
        Rotation = rotation;
        DisplayMode = mode;
        _builtPages = pages;
        _builtZoom = zoom;
        _builtRotation = rotation;
        _builtMode = mode;
        _builtCurrentPage = effectivePage;
        _builtShowCover = showCover;
        BuildGeometry(pages, rotation, mode, effectivePage, showCover, width);
        return BuildResult.Rebuilt;
    }

    private void BuildGeometry(IReadOnlyList<PdfPageInfo> pages, int rotation, PdfPageDisplayMode mode,
        int currentPage, bool showCover, double viewportWidth)
    {
        var rotated = rotation is 90 or 270;
        _rows = [];
        _pages = new Rect[pages.Count];
        if (pages.Count == 0)
        {
            _rowTops = _rowHeights = _rowWidths = [];
            _rowOfPage = [];
            _maxRowPaddedWidth = 0;
            Extent = default;
            return;
        }

        _rows = mode switch
        {
            PdfPageDisplayMode.SinglePage => [[currentPage]],
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
        _maxRowPaddedWidth = width;
        width = Math.Max(width, viewportWidth);

        _rowTops = new double[_rows.Count];
        _rowHeights = new double[_rows.Count];
        _rowWidths = new double[_rows.Count];
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
            _rowWidths[row] = rowWidth;
            y += rowHeight + Gap;
        }
        _rowOfPage = new int[pages.Count];
        Array.Fill(_rowOfPage, -1);
        for (var row = 0; row < _rows.Count; row++) _rowOfPage[_rows[row][0]] = row;
        Extent = new Size(width, y);
    }

    // Width-only viewport change: rows keep their heights and page sizes, only the horizontal
    // centering of each row moves. Mirrors the x sequence of a full build exactly.
    private void RecenterHorizontally(double width)
    {
        for (var row = 0; row < _rows.Count; row++)
        {
            var x = (width - _rowWidths[row]) / 2;
            foreach (var index in _rows[row])
            {
                var bounds = _pages[index];
                _pages[index] = new Rect(x, bounds.Y, bounds.Width, bounds.Height);
                x += bounds.Width + SpreadGap;
            }
        }
        Extent = new Size(width, Extent.Height);
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

    /// <summary>Row whose vertical span owns the y coordinate, or -1 when no rows exist.</summary>
    internal int RowAt(double y)
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
        return low;
    }

    internal int PageAt(double y)
    {
        var row = RowAt(y);
        return row < 0 ? -1 : _rows[row][0];
    }

    /// <summary>The page whose rectangle contains the point, or -1 outside every visible page.</summary>
    internal int PageIndexAt(Point point)
    {
        var row = RowAt(point.Y);
        if (row < 0) return -1;
        foreach (var index in _rows[row])
            if (_pages[index].Contains(point))
                return index;
        return -1;
    }

    /// <summary>The row index of the row starting at the given page, or -1 for other pages.</summary>
    internal int RowIndexOf(int firstPageOfRow) =>
        firstPageOfRow >= 0 && firstPageOfRow < _rowOfPage.Length ? _rowOfPage[firstPageOfRow] : -1;

    /// <summary>Row indexes whose vertical span intersects the given top and bottom coordinates.</summary>
    internal (int First, int Last) RowsBetween(double top, double bottom)
    {
        if (_rows.Count == 0) return (0, -1);
        return (Math.Max(0, RowAt(top) - 1), Math.Min(_rows.Count - 1, RowAt(bottom) + 1));
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
