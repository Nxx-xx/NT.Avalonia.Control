using Avalonia;
using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Core.Tests;

/// <summary>
/// P3-04 layout and search index contracts: an unchanged build signature keeps the cached
/// geometry, a width-only change recenters rows exactly like a full rebuild, row lookups
/// stay correct at scale, and search results group per page in global order.
/// </summary>
public sealed class P3LayoutIndexTests
{
    [Fact]
    public void Build_reuses_the_cached_geometry_when_the_signature_is_unchanged()
    {
        var pages = Enumerable.Range(0, 500).Select(index => new PdfPageInfo(index, 300, 400)).ToArray();
        var layout = new PdfPageLayout();
        Assert.Equal(PdfPageLayout.BuildResult.Rebuilt, layout.Build(pages, 1, 0, 1200, PdfPageDisplayMode.Continuous, 0, true));
        var extent = layout.Extent;
        var rects = layout.Pages.ToArray();
        Assert.Equal(PdfPageLayout.BuildResult.Unchanged, layout.Build(pages, 1, 0, 1200, PdfPageDisplayMode.Continuous, 0, true));
        Assert.Equal(extent, layout.Extent);
        Assert.Equal(rects, layout.Pages.ToArray());
        // Every geometry input is part of the signature: each change rebuilds.
        Assert.Equal(PdfPageLayout.BuildResult.Rebuilt, layout.Build(pages, 1.5, 0, 1200, PdfPageDisplayMode.Continuous, 0, true));
        Assert.Equal(PdfPageLayout.BuildResult.Rebuilt, layout.Build(pages, 1.5, 90, 1200, PdfPageDisplayMode.Continuous, 0, true));
        Assert.Equal(PdfPageLayout.BuildResult.Rebuilt, layout.Build(pages, 1.5, 90, 1200, PdfPageDisplayMode.TwoPageContinuous, 0, true));
        Assert.Equal(PdfPageLayout.BuildResult.Rebuilt, layout.Build(pages, 1.5, 90, 1200, PdfPageDisplayMode.TwoPageContinuous, 0, false));
    }

    [Fact]
    public void Continuous_layouts_ignore_the_current_page_but_discrete_layouts_do_not()
    {
        var pages = Enumerable.Range(0, 50).Select(index => new PdfPageInfo(index, 300, 400)).ToArray();
        var layout = new PdfPageLayout();
        layout.Build(pages, 1, 0, 1200, PdfPageDisplayMode.Continuous, 0, true);
        Assert.Equal(PdfPageLayout.BuildResult.Unchanged, layout.Build(pages, 1, 0, 1200, PdfPageDisplayMode.Continuous, 7, true));
        Assert.Equal(PdfPageLayout.BuildResult.Rebuilt, layout.Build(pages, 1, 0, 1200, PdfPageDisplayMode.SinglePage, 7, true));
        Assert.Equal(7, layout.PageAt(200));
        Assert.Equal(PdfPageLayout.BuildResult.Unchanged, layout.Build(pages, 1, 0, 1200, PdfPageDisplayMode.SinglePage, 7, true));
        Assert.Equal(PdfPageLayout.BuildResult.Rebuilt, layout.Build(pages, 1, 0, 1200, PdfPageDisplayMode.SinglePage, 9, true));
        Assert.Equal(9, layout.PageAt(200));
    }

    [Fact]
    public void Width_only_changes_recenters_rows_without_touching_vertical_geometry()
    {
        // 600x400 page points at zoom 1 become 800x533 DIPs; the padded row forces an 840
        // extent floor, so viewport widths below that cannot move the geometry at all.
        var pages = Enumerable.Range(0, 40).Select(index => new PdfPageInfo(index, 600, 400)).ToArray();
        var layout = new PdfPageLayout();
        Assert.Equal(PdfPageLayout.BuildResult.Rebuilt, layout.Build(pages, 1, 0, 1200, PdfPageDisplayMode.Continuous, 0, true));
        Assert.Equal(PdfPageLayout.BuildResult.Recentered, layout.Build(pages, 1, 0, 700, PdfPageDisplayMode.Continuous, 0, true));
        Assert.Equal(840, layout.Extent.Width, 3);
        Assert.Equal(PdfPageLayout.BuildResult.Unchanged, layout.Build(pages, 1, 0, 720, PdfPageDisplayMode.Continuous, 0, true));
        Assert.Equal(840, layout.Extent.Width, 3);
        Assert.Equal(PdfPageLayout.BuildResult.Recentered, layout.Build(pages, 1, 0, 900, PdfPageDisplayMode.Continuous, 0, true));
        Assert.Equal(900, layout.Extent.Width, 3);
    }

    [Fact]
    public void A_recenter_matches_a_full_rebuild_at_the_new_width_exactly()
    {
        var pages = Enumerable.Range(0, 200).Select(index =>
            new PdfPageInfo(index, index % 2 == 0 ? 300 : 600, index % 2 == 0 ? 400 : 200)).ToArray();
        var layout = new PdfPageLayout();
        layout.Build(pages, 1, 0, 1200, PdfPageDisplayMode.TwoPageContinuous, 0, true);
        Assert.Equal(PdfPageLayout.BuildResult.Recentered, layout.Build(pages, 1, 0, 2000, PdfPageDisplayMode.TwoPageContinuous, 0, true));
        var recentered = layout.Pages.ToArray();
        var fresh = new PdfPageLayout();
        fresh.Build(pages, 1, 0, 2000, PdfPageDisplayMode.TwoPageContinuous, 0, true);
        Assert.Equal(fresh.Pages.ToArray(), recentered);
        Assert.Equal(fresh.Extent, layout.Extent);
    }

    [Fact]
    public void Row_lookups_stay_consistent_across_two_thousand_mixed_rows()
    {
        var pages = Enumerable.Range(0, 2000).Select(index =>
            new PdfPageInfo(index, index % 2 == 0 ? 300 : 600, index % 2 == 0 ? 400 : 200)).ToArray();
        var layout = new PdfPageLayout();
        layout.Build(pages, 1, 0, 1400, PdfPageDisplayMode.Continuous, 0, true);
        Assert.Equal(2000, layout.Rows.Count);
        for (var row = 0; row < layout.Rows.Count; row += 13)
        {
            var pagesInRow = layout.Rows[row];
            Assert.Equal(row, layout.RowIndexOf(pagesInRow[0]));
            var bounds = layout.Pages[pagesInRow[0]];
            Assert.Equal(pagesInRow[0], layout.PageAt(bounds.Center.Y));
            Assert.Equal(pagesInRow[0], layout.PageIndexAt(bounds.Center));
            var (first, last) = layout.RowsBetween(bounds.Top + 1, bounds.Top + 2);
            Assert.True(first <= row && row <= last, $"Row {row} escaped RowsBetween [{first}, {last}].");
        }
        // Far lookups stay exact: binary search over the whole row stack.
        Assert.Equal(0, layout.PageAt(-1000));
        Assert.Equal(1999, layout.PageAt(layout.Extent.Height + 1000));
        Assert.Equal(-1, layout.RowIndexOf(-1));
    }

    [Fact]
    public void Two_page_rows_hit_test_both_members_of_the_spread()
    {
        var pages = Enumerable.Range(0, 10).Select(index => new PdfPageInfo(index, 300, 400)).ToArray();
        var layout = new PdfPageLayout();
        layout.Build(pages, 1, 0, 1200, PdfPageDisplayMode.TwoPageContinuous, 0, true);
        // showCover: page 0 alone, then (1,2), (3,4), ...
        Assert.Equal(6, layout.Rows.Count);
        Assert.Equal([1, 2], layout.Rows[1]);
        var left = layout.Pages[1];
        var right = layout.Pages[2];
        Assert.Equal(1, layout.PageIndexAt(left.Center));
        Assert.Equal(2, layout.PageIndexAt(right.Center));
        Assert.Equal(1, layout.RowIndexOf(1));
        Assert.Equal(-1, layout.RowIndexOf(2));
    }

    [Fact]
    public void Search_results_group_per_page_keeping_the_global_order()
    {
        static PdfSearchResult Hit(int page, int start) =>
            new(page, start, 3, [new Rect(0, 0, 10, 10)], "hit");
        var results = new[] { Hit(0, 0), Hit(2, 1), Hit(0, 2), Hit(5, 3), Hit(2, 4), Hit(2, 5) };
        var index = PdfSearchPageIndex.Build(results);
        Assert.Equal(3, index.Count);
        Assert.Equal([0, 2], index[0].Select(entry => entry.Index).ToArray());
        Assert.Equal([1, 4, 5], index[2].Select(entry => entry.Index).ToArray());
        Assert.Equal([3], index[5].Select(entry => entry.Index).ToArray());
        Assert.Equal([0, 2], index[0].Select(entry => entry.Result.StartIndex).ToArray());
        Assert.Equal([1, 4, 5], index[2].Select(entry => entry.Result.StartIndex).ToArray());
        Assert.Equal([3], index[5].Select(entry => entry.Result.StartIndex).ToArray());
        Assert.Empty(PdfSearchPageIndex.Build([]));
    }
}
