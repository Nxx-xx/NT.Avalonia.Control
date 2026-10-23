using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class FunnelPyramidTests
{
    private static readonly Rect Plot = new(10, 20, 400, 300);

    [AvaloniaFact]
    public void Public_defaults_and_finite_bounded_parameters_are_explicit()
    {
        var funnel = new FunnelChart(); var pyramid = new PyramidChart();
        Assert.Equal(2, funnel.SegmentGap); Assert.Equal(.3, funnel.NeckWidth); Assert.Equal(2, pyramid.SegmentGap);
        Assert.True(funnel.ShowLabels); Assert.True(funnel.ShowValues);
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, -1, 4097 })
        {
            Assert.Throws<ArgumentException>(() => funnel.SegmentGap = value);
            Assert.Throws<ArgumentException>(() => pyramid.SegmentGap = value);
        }
        foreach (var value in new[] { double.NaN, double.NegativeInfinity, -.01, 1.01 })
            Assert.Throws<ArgumentException>(() => funnel.NeckWidth = value);
        funnel.NeckWidth = 0; funnel.NeckWidth = 1; funnel.SegmentGap = 0; pyramid.SegmentGap = 4096;
    }

    [AvaloniaFact]
    public void Funnel_boundary_widths_follow_the_declared_value_rule_and_final_neck()
    {
        var marks = Funnel(Data([100, 50, 25]), neck: .2);
        Assert.Equal(3, marks.Count);
        AssertPolygon(marks[0], [new(10, 20), new(410, 20), new(330, 120), new(90, 120)]);
        AssertPolygon(marks[1], [new(90, 120), new(330, 120), new(290, 220), new(130, 220)]);
        AssertPolygon(marks[2], [new(130, 220), new(290, 220), new(250, 320), new(170, 320)]);
        Assert.Equal(new Point(210, 70), marks[0].LabelPosition);
        Assert.Equal(new[] { 10, 11, 12 }, marks.Select(mark => mark.Data.SourceIndex));
    }

    [AvaloniaFact]
    public void Funnel_does_not_sort_increasing_values_or_rewrite_original_items()
    {
        var data = Data([20, 100, 50]); var marks = Funnel(data, neck: 0);
        Assert.Equal(new[] { 20d, 100d, 50d }, marks.Select(mark => mark.Data.Point.Y));
        Assert.Equal(80, Width(marks[0], top: true), 9); Assert.Equal(400, Width(marks[0], top: false), 9);
        Assert.Equal(400, Width(marks[1], top: true), 9); Assert.Equal(200, Width(marks[1], top: false), 9);
        Assert.Equal(0, Width(marks[2], top: false), 9);
        for (var index = 0; index < marks.Count; index++)
        {
            Assert.Same(data.Points[index].Item, marks[index].Data.Item);
            Assert.Equal(data.Points[index].StableId, marks[index].Data.StableId);
            Assert.Equal(data.Points[index].Category, marks[index].Data.Category);
        }
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(.3)] [InlineData(1)]
    public void One_funnel_stage_uses_the_full_height_and_requested_neck(double neck)
    {
        var mark = Assert.Single(Funnel(Data([4]), neck, gap: 100));
        Assert.Equal(400, Width(mark, true), 9); Assert.Equal(400 * neck, Width(mark, false), 9);
        var points = Points(mark); Assert.Equal(20, points[0].Y); Assert.Equal(320, points[2].Y);
        Assert.Equal(300 * (400 + 400 * neck) / 2, Area(mark), 7);
    }

    [AvaloniaFact]
    public void Funnel_gap_trims_interpolated_edges_and_contains_no_paint_between_stages()
    {
        var marks = Funnel(Data([100, 50, 25]), .2, gap: 10);
        Assert.Equal(115, Points(marks[0])[2].Y); Assert.Equal(125, Points(marks[1])[0].Y);
        Assert.Equal(248, Width(marks[0], false), 9); Assert.Equal(236, Width(marks[1], true), 9);
        Assert.All(marks, mark => Assert.False(Contains(mark, new Point(210, 120))));
        Assert.True(Contains(marks[0], new Point(210, 114))); Assert.True(Contains(marks[1], new Point(210, 126)));
        Assert.All(marks, mark => Assert.False(Contains(mark, new Point(11, 170))));
    }

    [AvaloniaFact]
    public void Pyramid_squared_boundary_heights_produce_independent_exact_area_shares()
    {
        var marks = Pyramid(Data([1, 3, 5])); Assert.Equal(3, marks.Count);
        AssertPolygon(marks[0], [new(210, 20), new(210, 20), new(830d / 3, 120), new(430d / 3, 120)]);
        Assert.Equal(120, Points(marks[1])[0].Y, 9); Assert.Equal(220, Points(marks[1])[2].Y, 9);
        Assert.Equal(220, Points(marks[2])[0].Y, 9); Assert.Equal(320, Points(marks[2])[2].Y, 9);
        var totalArea = 400d * 300 / 2;
        Assert.Equal(totalArea / 9, Area(marks[0]), 7);
        Assert.Equal(totalArea / 3, Area(marks[1]), 7);
        Assert.Equal(totalArea * 5 / 9, Area(marks[2]), 7);
        Assert.Equal(totalArea, marks.Sum(Area), 7);
    }

    [AvaloniaFact]
    public void Pyramid_keeps_source_order_even_when_layers_have_equal_or_decreasing_contributions()
    {
        var marks = Pyramid(Data([8, 1, 1]));
        Assert.Equal(new[] { 8d, 1d, 1d }, marks.Select(mark => mark.Data.Point.Y));
        Assert.Equal(new[] { 10, 11, 12 }, marks.Select(mark => mark.Data.SourceIndex));
        Assert.Equal(8, Area(marks[0]) / Area(marks[1]), 7); Assert.Equal(Area(marks[1]), Area(marks[2]), 7);
        Assert.Equal(20 + 300 * Math.Sqrt(.8), Points(marks[0])[2].Y, 9);
    }

    [AvaloniaFact]
    public void Pyramid_gap_preserves_the_triangle_outline_and_empty_space_is_not_a_layer()
    {
        var marks = Pyramid(Data([1, 3, 5]), gap: 10);
        Assert.Equal(115, Points(marks[0])[2].Y, 9); Assert.Equal(125, Points(marks[1])[0].Y, 9);
        Assert.Equal(400d * 95 / 300, Width(marks[0], false), 9);
        Assert.Equal(400d * 105 / 300, Width(marks[1], true), 9);
        Assert.All(marks, mark => Assert.False(Contains(mark, new Point(210, 120))));
        Assert.True(Contains(marks[0], new Point(210, 110))); Assert.True(Contains(marks[1], new Point(210, 130)));
        Assert.True(marks.Sum(Area) < 60000);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Missing_nonpositive_and_nonfinite_rows_never_make_stages_or_reassign_source_indices(bool pyramid)
    {
        var data = Data([0, -2, double.NaN, double.PositiveInfinity, 2, 6]);
        var colors = new List<int>();
        var marks = pyramid ? FunnelPyramidGeometry.Pyramid(data, Plot, 0, index => { colors.Add(index); return Brushes.Blue; }) :
            FunnelPyramidGeometry.Funnel(data, Plot, .3, 0, index => { colors.Add(index); return Brushes.Blue; });
        Assert.Equal(2, marks.Count); Assert.Equal(new[] { 14, 15 }, marks.Select(mark => mark.Data.SourceIndex));
        Assert.Equal(new[] { 14, 15 }, colors);
        Assert.Same(data.Points[4].Item, marks[0].Data.Item); Assert.Equal(data.Points[5].StableId, marks[1].Data.StableId);
        Assert.Equal(-2, data.Points[1].Point.Y); Assert.True(double.IsNaN(data.Points[2].Point.Y));
        Assert.Empty(pyramid ? Pyramid(Data([0, -1, double.NaN])) : Funnel(Data([0, -1, double.NaN])));
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void A_finite_positive_but_explicitly_empty_published_row_stays_absent(bool pyramid)
    {
        var data = new ChartDataSnapshot(0, new ChartDataPoint[]
        {
            new(5, "missing", null, "Missing", new ChartPoint(0, 9), true),
            new(8, "real", null, "Real", new ChartPoint(1, 4), false),
        });
        var mark = Assert.Single(pyramid ? Pyramid(data) : Funnel(data));
        Assert.Equal(8, mark.Data.SourceIndex); Assert.Equal("real", mark.Data.StableId);
    }

    [AvaloniaTheory]
    [InlineData(double.MaxValue)] [InlineData(double.Epsilon)] [InlineData(1e-300)]
    public void Overflow_and_subnormal_input_totals_keep_finite_geometry_and_correct_area_ratios(double value)
    {
        var data = Data([value, value]); var funnel = Funnel(data, .3); var pyramid = Pyramid(data);
        Assert.Equal(2, funnel.Count); Assert.Equal(2, pyramid.Count);
        Assert.Equal(400, Width(funnel[0], true), 8); Assert.Equal(400, Width(funnel[0], false), 8);
        Assert.Equal(400, Width(funnel[1], true), 8); Assert.Equal(120, Width(funnel[1], false), 8);
        Assert.All(funnel.Concat(pyramid), AssertFinite);
        Assert.Equal(30000, Area(pyramid[0]), 7); Assert.Equal(30000, Area(pyramid[1]), 7);
        Assert.Equal(value, data.Points[0].Point.Y);
    }

    [AvaloniaFact]
    public void Pyramid_fraction_underflow_does_not_invent_a_minimum_visible_stage()
    {
        var data = Data([double.Epsilon, double.MaxValue]); var mark = Assert.Single(Pyramid(data));
        Assert.Equal(11, mark.Data.SourceIndex); Assert.Equal(60000, Area(mark), 7);
        Assert.Equal(double.Epsilon, data.Points[0].Point.Y);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Oversized_gap_can_consume_interior_stages_without_inversion_or_overlap(bool pyramid)
    {
        var marks = pyramid ? Pyramid(Data([1, 3, 5]), 4096) : Funnel(Data([1, 3, 5]), gap: 4096);
        Assert.Equal(2, marks.Count); Assert.Equal(new[] { 10, 12 }, marks.Select(mark => mark.Data.SourceIndex));
        Assert.All(marks, mark => { AssertFinite(mark); Assert.True(Area(mark) > 0); });
        Assert.True(Points(marks[0])[2].Y < Points(marks[1])[0].Y);
    }

    [AvaloniaTheory]
    [InlineData(0, 100)] [InlineData(100, 0)] [InlineData(0, 0)]
    public void Zero_size_plots_are_empty_without_geometry_or_labels(double width, double height)
    {
        var plot = new Rect(0, 0, width, height);
        Assert.Empty(FunnelPyramidGeometry.Funnel(Data([1, 2]), plot, .3, 2, _ => Brushes.Blue));
        Assert.Empty(FunnelPyramidGeometry.Pyramid(Data([1, 2]), plot, 2, _ => Brushes.Blue));
    }

    [AvaloniaTheory]
    [InlineData(1e-200, 1e-200)] [InlineData(1e-9, 1e-9)] [InlineData(1, 1)]
    public void Tiny_positive_plots_emit_only_finite_vertices_inside_the_requested_bounds(double width, double height)
    {
        var plot = new Rect(0, 0, width, height);
        var marks = FunnelPyramidGeometry.Funnel(Data([double.MaxValue, 1]), plot, 0, 0, _ => Brushes.Blue)
            .Concat(FunnelPyramidGeometry.Pyramid(Data([double.MaxValue, 1]), plot, 0, _ => Brushes.Blue));
        Assert.NotEmpty(marks);
        Assert.All(marks, mark =>
        {
            AssertFinite(mark);
            Assert.All(Points(mark), point => { Assert.InRange(point.X, 0, width); Assert.InRange(point.Y, 0, height); });
        });
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Public_chart_projects_categories_and_hits_real_shapes_with_original_business_identity(bool pyramid)
    {
        var rows = new[] { new Row("drop", "Skipped", 0), new Row("first", "Discovery", 1), new Row("second", "Engagement", 3), new Row("third", "Purchase", 5) };
        AnalysisChartBase chart = pyramid ? new PyramidChart { SegmentGap = 10 } : new FunnelChart { SegmentGap = 10, NeckWidth = .2 };
        chart.ItemsSource = rows; chart.LabelPath = nameof(Row.Category); chart.ValuePath = nameof(Row.Value); chart.IdPath = nameof(Row.Id);
        chart.Padding = new Thickness(0); chart.ShowLabels = false; chart.ShowValues = false;
        chart.Measure(new Size(400, 300)); chart.Arrange(new Rect(0, 0, 400, 300));
        var marks = chart.GetMarks(); Assert.Equal(3, marks.Count); Assert.Equal(new[] { 1, 2, 3 }, marks.Select(mark => mark.Data.SourceIndex));
        foreach (var mark in marks)
        {
            var hit = chart.HitTest(mark.LabelPosition); Assert.NotNull(hit);
            Assert.Same(chart.Series, hit.Series); Assert.Equal(mark.Data.SourceIndex, hit.Index);
            Assert.Same(rows[hit.Index], hit.Item); Assert.Equal(rows[hit.Index].Id, hit.StableId);
            Assert.Equal(rows[hit.Index].Value, hit.Point.Y);
        }
        var boundary = (Points(marks[0])[2].Y + Points(marks[1])[0].Y) / 2;
        Assert.Null(chart.HitTest(new Point(200, boundary)));
        Assert.Null(chart.HitTest(new Point(-1, 100))); Assert.Null(chart.HitTest(new Point(401, 100)));
        chart.SelectedIndex = 2; Assert.Equal("second", chart.SelectedPoint!.StableId);
        chart.ResetViewport(); Assert.Equal(-1, chart.SelectedIndex); Assert.Null(chart.SelectedPoint);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Parameter_changes_rebuild_public_geometry_and_selection_without_mutating_data(bool pyramid)
    {
        AnalysisChartBase chart = pyramid ? new PyramidChart { SegmentGap = 0 } : new FunnelChart { SegmentGap = 0, NeckWidth = 0 };
        var rows = new[] { new Row("a", "A", 1), new Row("b", "B", 3), new Row("c", "C", 5) };
        chart.ItemsSource = rows; chart.CategoryPath = nameof(Row.Category); chart.ValuePath = nameof(Row.Value); chart.IdPath = nameof(Row.Id);
        chart.Padding = new Thickness(0); chart.Measure(new Size(400, 300)); chart.Arrange(new Rect(0, 0, 400, 300));
        var before = chart.GetMarks(); chart.SelectedIndex = 1; Assert.Equal("b", chart.SelectedPoint!.StableId);
        if (chart is PyramidChart pyramidChart) pyramidChart.SegmentGap = 20;
        else ((FunnelChart)chart).SegmentGap = 20;
        var after = chart.GetMarks(); Assert.NotSame(before, after); Assert.True(Area(after[1]) < Area(before[1]));
        Assert.Equal("b", chart.SelectedPoint!.StableId); Assert.Equal(3, chart.SelectedPoint.Point.Y);
        Assert.Equal(new[] { 1d, 3d, 5d }, rows.Select(row => row.Value));
    }

    private static IReadOnlyList<AnalysisMarkInfo> Funnel(ChartDataSnapshot data, double neck = .3, double gap = 0) =>
        FunnelPyramidGeometry.Funnel(data, Plot, neck, gap, _ => Brushes.Blue);
    private static IReadOnlyList<AnalysisMarkInfo> Pyramid(ChartDataSnapshot data, double gap = 0) =>
        FunnelPyramidGeometry.Pyramid(data, Plot, gap, _ => Brushes.Blue);
    private static ChartDataSnapshot Data(double[] values) => new(0, values.Select((value, index) =>
        new ChartDataPoint(index + 10, $"id-{index}", new object(), $"Stage {index}", new ChartPoint(index, value), !double.IsFinite(value))).ToArray());
    private static IReadOnlyList<Point> Points(AnalysisMarkInfo mark) => Assert.IsType<PolylineGeometry>(mark.Geometry).Points.ToArray();
    private static double Width(AnalysisMarkInfo mark, bool top) => top ? Points(mark)[1].X - Points(mark)[0].X : Points(mark)[2].X - Points(mark)[3].X;
    private static double Area(AnalysisMarkInfo mark)
    {
        var points = Points(mark); var origin = points[0]; var twiceArea = 0d;
        for (var index = 0; index < points.Count; index++)
        {
            var current = points[index] - origin; var next = points[(index + 1) % points.Count] - origin;
            twiceArea += current.X * next.Y - next.X * current.Y;
        }
        return Math.Abs(twiceArea) / 2;
    }
    private static bool Contains(AnalysisMarkInfo mark, Point point)
    {
        var vertices = Points(mark); bool? positive = null;
        for (var index = 0; index < vertices.Count; index++)
        {
            var edge = vertices[(index + 1) % vertices.Count] - vertices[index]; var toPoint = point - vertices[index];
            var cross = edge.X * toPoint.Y - edge.Y * toPoint.X;
            if (Math.Abs(cross) < 1e-9) continue;
            if (positive is { } sign && sign != (cross > 0)) return false;
            positive = cross > 0;
        }
        return positive is not null;
    }
    private static void AssertPolygon(AnalysisMarkInfo mark, Point[] expected)
    {
        var geometry = Assert.IsType<PolylineGeometry>(mark.Geometry); Assert.True(geometry.IsFilled);
        Assert.Equal(expected.Length, geometry.Points.Count);
        for (var index = 0; index < expected.Length; index++)
        { Assert.Equal(expected[index].X, geometry.Points[index].X, 9); Assert.Equal(expected[index].Y, geometry.Points[index].Y, 9); }
    }
    private static void AssertFinite(AnalysisMarkInfo mark)
    {
        Assert.True(mark.IsVisible);
        Assert.All(Points(mark), point => { Assert.True(double.IsFinite(point.X)); Assert.True(double.IsFinite(point.Y)); });
        Assert.True(double.IsFinite(mark.LabelPosition.X)); Assert.True(double.IsFinite(mark.LabelPosition.Y));
    }
    private sealed record Row(string Id, string Category, double Value);
}
