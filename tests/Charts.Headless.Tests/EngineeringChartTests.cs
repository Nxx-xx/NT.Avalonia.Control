using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class EngineeringChartTests
{
    [AvaloniaTheory]
    [InlineData(SmithQuantity.Impedance, 50, 50, 50)]
    [InlineData(SmithQuantity.Admittance, 50, .01, -.01)]
    public void Smith_projects_physical_units_against_independent_complex_division_and_roundtrips_the_viewport(SmithQuantity quantity, double z0, double real, double imaginary)
    {
        var original = new SmithPoint(real, imaginary);
        var chart = new SmithChart { ItemsSource = new[] { original }, ReferenceImpedance = z0, Quantity = quantity };
        using var host = new Host(chart);
        Assert.Equal(new Rect(94, 24, 232, 232), chart.PlotArea);
        var normalizedR = quantity == SmithQuantity.Impedance ? real / z0 : real * z0;
        var normalizedX = quantity == SmithQuantity.Impedance ? imaginary / z0 : imaginary * z0;
        var divisor = (normalizedR + 1) * (normalizedR + 1) + normalizedX * normalizedX;
        var gammaX = (normalizedR * normalizedR + normalizedX * normalizedX - 1) / divisor;
        var gammaY = 2 * normalizedX / divisor;
        if (quantity == SmithQuantity.Admittance) { gammaX = -gammaX; gammaY = -gammaY; }
        var mark = Assert.Single(chart.GetMarks()); Near(210 + 116 * gammaX, mark.Position.X); Near(140 - 116 * gammaY, mark.Position.Y);
        Assert.Equal(original, Assert.IsType<SmithPoint>(Assert.Single(chart.HitTest(mark.Position)!.Members).Item));
        Assert.Equal(new ChartPoint(real, imaginary), mark.Members[0].Point);
        Assert.Equal(17, chart.GetGridLines().Count);
        chart.Zoom = 1.3; chart.PanX = .03; chart.PanY = -.04;
        var inverse = chart.Unproject(chart.Project(original)); Near(real, inverse.R); Near(imaginary, inverse.X);
        chart.ResetViewport(); Assert.Equal(1, chart.Zoom); Assert.Equal(0, chart.PanX); Assert.Equal(0, chart.PanY);
    }

    [AvaloniaFact]
    public void Ternary_grid_has_three_parallel_families_and_keeps_absolute_source_components()
    {
        TernaryPoint[] source = [new(20, 10, 10), new(2, 1, 1), new(0, 0, 0), new(-1, 1, 1), new(double.NaN, 1, 1)];
        var chart = new TernaryChart { ItemsSource = source, ALabel = "water", BLabel = "oil", CLabel = "solids" };
        using var host = new Host(chart);
        var marks = chart.GetMarks(); Assert.Equal(2, marks.Count); Assert.Equal(marks[0].Position, marks[1].Position);
        Assert.Equal(new ChartPoint(20, 10), marks[0].Members[0].Point); Assert.Equal(source[0], Assert.IsType<TernaryPoint>(marks[0].Members[0].Value));
        Near(210, marks[0].Position.X); Near(140, marks[0].Position.Y);
        Assert.Equal(31, chart.GetGridLines().Count);
        Assert.Contains(chart.GetGridLines(), line => line.Label == "water");
        chart.Zoom = 1.2; chart.PanX = .01;
        var normalized = chart.Unproject(chart.Project(source[0])); Near(.5, normalized.A); Near(.25, normalized.B); Near(.25, normalized.C);
        Assert.Throws<ArgumentOutOfRangeException>(() => chart.Unproject(new Point(chart.PlotArea.Left, chart.PlotArea.Top)));
    }

    [AvaloniaFact]
    public void Carpet_projects_all_parameter_intersections_and_inverse_uses_the_bilinear_surface()
    {
        var points = (from b in new[] { 0d, 1, 2 } from a in new[] { 0d, 1, 2 } select new CarpetPoint(a, b, 10 + a + 2 * b + .5 * a * b)).ToArray();
        var chart = new CarpetPlot { ItemsSource = points, CarpetOffset = .5 };
        using var host = new Host(chart);
        Assert.Equal(9, chart.GetMarks().Count); Assert.Equal(6, chart.GetGridLines().Count); Assert.Null(chart.MeshError); Assert.True(chart.Grid!.IsInvertible);
        Assert.Equal(new ChartSeriesBounds(0, 3, 10, 18), chart.DataBounds);
        var interpolated = chart.Interpolate(.5, 1.25); Near(13.3125, interpolated.Y);
        chart.Zoom = 1.1; chart.PanX = -.02; chart.PanY = .03;
        var inverse = chart.Unproject(chart.Project(interpolated)); Near(.5, inverse.A); Near(1.25, inverse.B); Near(13.3125, inverse.Y);
        chart.ResetViewport();
        var center = chart.Project(points[4]); var hit = Assert.IsType<EngineeringPointHit>(chart.HitTest(center));
        Assert.Equal(4, Assert.Single(hit.Members).SourceIndex); Assert.Equal(points[4], Assert.IsType<CarpetPoint>(hit.Members[0].Value));
        chart.ItemsSource = points[..^1]; Assert.Null(chart.Grid); Assert.NotNull(chart.MeshError); Assert.Equal(8, chart.GetMarks().Count);
    }

    [AvaloniaTheory]
    [InlineData(HexbinAggregation.Count, 3)]
    [InlineData(HexbinAggregation.Sum, 15)]
    [InlineData(HexbinAggregation.Mean, 5)]
    public void Hexbin_keeps_invalid_source_offsets_and_all_members_without_forging_one_raw_point(HexbinAggregation aggregation, double expected)
    {
        HexbinPoint[] points = [new(double.NaN, 0), new(0, 0, 2), new(.01, 0, 5), new(0, .01, 8), new(-10, -10), new(10, 10)];
        var chart = new HexbinChart { ItemsSource = points, Aggregation = aggregation, HexRadius = 18 };
        using var host = new Host(chart);
        var center = Assert.Single(chart.GetMarks(), mark => mark.Members.Count == 3);
        Assert.Equal(new[] { 1, 2, 3 }, center.Members.Select(member => member.SourceIndex)); Assert.Equal(expected, center.Value);
        Assert.Equal(5, chart.Binning.Count); Assert.Equal(1, chart.Binning.OmittedCount);
        var hit = Assert.IsType<EngineeringPointHit>(chart.HitTest(center.Position)); Assert.Equal(3, hit.Members.Count);
        Assert.Equal(new[] { 2d, 5, 8 }, hit.Members.Select(member => Assert.IsType<double>(member.Value)));
        Assert.Null(chart.HitTest(new Point(center.Bounds.Left + .1, center.Bounds.Top + .1)));
        chart.Zoom = 1.3; chart.PanX = .01; chart.PanY = -.02;
        var transformed = Assert.Single(chart.GetMarks(), mark => mark.Members.Count == 3);
        Assert.Equal(new[] { 1, 2, 3 }, chart.HitTest(transformed.Position)!.Members.Select(member => member.SourceIndex));
        var inverse = chart.Unproject(chart.Project(new ChartPoint(3, -2))); Near(3, inverse.X); Near(-2, inverse.Y);
    }

    [AvaloniaFact]
    public void Wind_rose_speed_boundaries_and_weighted_percentages_have_true_member_identity_and_radial_inverse()
    {
        WindObservation[] points = [new(0, 2, 2), new(360, 3, 3), new(90, 5, 4), new(180, 8, 1), new(270, -1, 5), new(0, 1, 0)];
        var chart = new WindRoseChart { ItemsSource = points, SectorCount = 4, SpeedUpperBounds = new double[] { 3, 6 } };
        using var host = new Host(chart);
        Assert.Equal(4, chart.Rose.Count); Assert.Equal(2, chart.Rose.OmittedCount); Assert.Equal(10, chart.Rose.TotalFrequency); Assert.Equal(5, chart.MaximumFrequency);
        var northFast = Assert.Single(chart.GetMarks(), mark => mark.Members.Count == 1 && mark.Members[0].SourceIndex == 1);
        Near(3, northFast.Value); Assert.Contains("30%", northFast.TooltipText);
        Assert.Equal(points[1], Assert.IsType<WindObservation>(Assert.Single(chart.HitTest(northFast.Position)!.Members).Value));
        chart.Zoom = 1.1; chart.PanX = -.01; chart.PanY = .02;
        var inverse = chart.Unproject(chart.Project(90, 2.5)); Near(90, inverse.X); Near(2.5, inverse.Y);
        chart.AngleUnit = ChartAngleUnit.Radians;
        inverse = chart.Unproject(chart.Project(Math.PI / 2, 2.5)); Near(Math.PI / 2, inverse.X); Near(2.5, inverse.Y);
    }

    [AvaloniaFact]
    public void Direction_strings_respect_compass_and_explicit_angle_units_in_business_mapping()
    {
        var rows = new[] { new { Bearing = "East", Speed = 1d, Weight = 2d }, new { Bearing = "1.5707963267948966rad", Speed = 4d, Weight = 3d }, new { Bearing = "nonsense", Speed = 2d, Weight = 1d } };
        var chart = new WindRoseChart { ItemsSource = rows, DirectionPath = "Bearing", SpeedPath = "Speed", ValuePath = "Weight", SectorCount = 4, AngleUnit = ChartAngleUnit.Radians };
        using var host = new Host(chart);
        Assert.Equal(2, chart.Rose.Count); Assert.Equal(5, chart.Rose.Sectors[1].Frequency); Assert.Equal(0, chart.Rose.Sectors[0].Frequency);
        Assert.Equal(new[] { 0, 1 }, chart.GetMarks().SelectMany(mark => mark.Members).Select(member => member.SourceIndex).Order());
    }

    [AvaloniaFact]
    public void Nested_mapping_observes_edits_reorders_replacements_and_releases_every_subscription_on_unload()
    {
        var a = new Row("a", 25, 25); var b = new Row("b", 50, 50);
        var rows = new ObservableCollection<Row> { a, b };
        var model = new Model(rows);
        var chart = new SmithChart { ResistancePath = "Value.Real", ReactancePath = "Value.Imaginary", IdPath = "Id", DataContext = model, SelectedIndex = 1 };
        chart.Bind(EngineeringChartBase.ItemsSourceProperty, new Binding(nameof(Model.Items)));
        using var host = new Host(chart);
        Assert.Equal(1, a.Observers); Assert.Equal(1, a.Value.Observers); Assert.Equal(1, b.Observers); Assert.Equal(1, b.Value.Observers);
        Assert.Same(b, Assert.Single(chart.SelectedPoint!.Members).Item);
        rows.Move(1, 0); b.Value.Real = 75; host.Drain();
        Assert.Equal(0, chart.SelectedIndex); Assert.Same(b, chart.SelectedPoint!.Members[0].Item); Assert.Equal(75, chart.SelectedPoint.Members[0].Point.X);
        var replacement = new Row("b", 100, -20); model.Items = new ObservableCollection<Row> { replacement }; host.Drain();
        Assert.Same(replacement, chart.SelectedPoint!.Members[0].Item); Assert.Equal(0, a.Observers); Assert.Equal(0, b.Value.Observers); Assert.Equal(1, replacement.Value.Observers);
        host.Detach(); Assert.Equal(0, replacement.Observers); Assert.Equal(0, replacement.Value.Observers);
        replacement.Value.Real = 150; Assert.Equal(150, Assert.Single(chart.GetMarks()).Members[0].Point.X); Assert.Equal(0, replacement.Value.Observers);
        host.Attach(); Assert.Equal(1, replacement.Value.Observers); model.Items.Clear(); host.Drain();
        Assert.Null(chart.SelectedPoint); Assert.Equal(-1, chart.SelectedIndex); Assert.Equal(0, replacement.Observers);
    }

    [AvaloniaFact]
    public void Keyboard_selection_reset_resize_and_render_publication_use_current_original_data()
    {
        var row = new Row("same", 50, 25); var source = new ObservableCollection<Row> { row };
        var chart = new SmithChart { ItemsSource = source, ResistancePath = "Value.Real", ReactancePath = "Value.Imaginary", IdPath = "Id" };
        using var host = new Host(chart);
        Key(chart, Avalonia.Input.Key.Home); Key(chart, Avalonia.Input.Key.Enter); Assert.Equal("same", chart.SelectedPoint!.Members[0].StableId);
        var before = chart.SelectedPoint.Position; var rendering = false; var raisedDuringRender = false;
        chart.PropertyChanged += (_, args) => { if (rendering && (args.Property == EngineeringChartBase.ActivePointProperty || args.Property == EngineeringChartBase.SelectedPointProperty || args.Property == EngineeringChartBase.SelectedIndexProperty)) raisedDuringRender = true; };
        row.Value.Real = 100; rendering = true; Record(chart); rendering = false;
        Assert.False(raisedDuringRender); Dispatcher.UIThread.RunJobs();
        Assert.Equal(100, chart.SelectedPoint!.Members[0].Point.X); Assert.NotEqual(before, chart.SelectedPoint.Position);
        var identity = chart.SelectedPoint.Members[0].StableId;
        host.Resize(520, 360); Assert.Equal(identity, chart.SelectedPoint!.Members[0].StableId); Assert.Equal(100, chart.SelectedPoint.Members[0].Point.X);
        Key(chart, Avalonia.Input.Key.Escape); Assert.Null(chart.SelectedPoint); Assert.Null(chart.ActivePoint); Assert.Equal(-1, chart.SelectedIndex);
    }

    [AvaloniaFact]
    public void A_binding_callback_can_override_a_followed_index_without_publishing_a_mismatched_selected_point()
    {
        var a = new Row("a", 25, 10); var b = new Row("b", 50, 20); var c = new Row("c", 75, 30);
        var rows = new ObservableCollection<Row> { a, b, c };
        var chart = new SmithChart { ItemsSource = rows, ResistancePath = "Value.Real", ReactancePath = "Value.Imaginary", IdPath = "Id", SelectedIndex = 1 };
        using var host = new Host(chart); Assert.Same(b, chart.SelectedPoint!.Members[0].Item);
        var changed = false;
        chart.PropertyChanged += (_, args) =>
        {
            if (!changed && args.Property == EngineeringChartBase.SelectedIndexProperty && chart.SelectedIndex == 0)
            { changed = true; chart.SelectedIndex = 2; }
        };
        rows.Move(1, 0); host.Drain(); Dispatcher.UIThread.RunJobs();
        Assert.True(changed); Assert.Equal(2, chart.SelectedIndex); Assert.Same(c, chart.SelectedPoint!.Members[0].Item);
    }

    [AvaloniaFact]
    public void Finite_extreme_observations_keep_finite_geometry_and_invalid_screen_queries_are_rejected()
    {
        var wind = new WindRoseChart { ItemsSource = new[] { new WindObservation(1e308, 2, 1.7e308) } };
        using (var host = new Host(wind))
        {
            Assert.Equal(1.7e308, wind.MaximumFrequency);
            Assert.All(wind.GetGridLines(), line => Assert.True(double.IsFinite(line.Geometry.Bounds.Width) && double.IsFinite(line.Geometry.Bounds.Height)));
            var projected = wind.Project(1e308, 8.5e307); Assert.True(double.IsFinite(projected.X) && double.IsFinite(projected.Y));
            var inverse = wind.Unproject(projected); Near(8.5e307, inverse.Y);
            Assert.Throws<ArgumentOutOfRangeException>(() => wind.Unproject(new Point(double.NaN, 0)));
        }
        var hex = new HexbinChart { ItemsSource = new[] { new HexbinPoint(-1e308, -1e308), new HexbinPoint(1e308, 1e308) } };
        using (var host = new Host(hex))
        {
            Assert.All(hex.GetMarks(), mark => Assert.True(double.IsFinite(mark.Position.X) && double.IsFinite(mark.Position.Y)));
            Assert.Equal(new Point(210, 140), hex.Project(new ChartPoint(0, 0)));
            var inverse = hex.Unproject(new Point(210, 140)); Assert.Equal(new ChartPoint(0, 0), inverse);
            Assert.Throws<ArgumentOutOfRangeException>(() => hex.Project(new ChartPoint(double.PositiveInfinity, 0)));
        }
        var ternary = new TernaryChart { ItemsSource = new[] { new TernaryPoint(1e308, 1e308, 1e308) } };
        using (var host = new Host(ternary))
        {
            var fractions = ternary.Unproject(Assert.Single(ternary.GetMarks()).Position); Near(1d / 3, fractions.A); Near(1d / 3, fractions.B); Near(1d / 3, fractions.C);
            Assert.Equal(1e308, Assert.IsType<TernaryPoint>(ternary.GetMarks()[0].Members[0].Value).C);
        }
    }

    [AvaloniaFact]
    public void Resize_and_rebin_follow_the_selected_source_instead_of_the_retired_glyph_ordinal()
    {
        var low = new Row("low", -10, -10); var a = new Row("a", 0, 0); var b = new Row("b", .5, 0); var high = new Row("high", 10, 10);
        var chart = new HexbinChart { ItemsSource = new[] { low, a, b, high }, XPath = "Value.Real", YPath = "Value.Imaginary", IdPath = "Id", BinRadius = 18, SelectedIndex = 2 };
        using var host = new Host(chart);
        Assert.Equal(new[] { 1, 2, 1 }, chart.GetMarks().Select(mark => mark.Members.Count)); Assert.Same(high, chart.SelectedPoint!.Members[0].Item);
        host.Resize(840, 280);
        Assert.Equal(4, chart.GetMarks().Count); Assert.Equal(3, chart.SelectedIndex); Assert.Same(high, chart.SelectedPoint!.Members[0].Item);
        host.Resize(420, 280); Assert.Equal(2, chart.SelectedIndex); Assert.Same(high, chart.SelectedPoint!.Members[0].Item);
        chart.BinRadius = 9; host.Drain();
        Assert.Equal(4, chart.GetMarks().Count); Assert.Equal(3, chart.SelectedIndex); Assert.Same(high, chart.SelectedPoint!.Members[0].Item);
    }

    [AvaloniaFact]
    public void Setting_a_glyph_index_captures_its_members_before_resize_without_a_selection_getter_or_render()
    {
        var low = new Row("low", -10, -10); var a = new Row("a", 0, 0); var b = new Row("b", .5, 0); var high = new Row("high", 10, 10);
        var rows = new ObservableCollection<Row> { low, a, b, high };
        var chart = new HexbinChart { ItemsSource = rows, XPath = "Value.Real", YPath = "Value.Imaginary", IdPath = "Id", BinRadius = 18 };
        using var host = new Host(chart);
        chart.SelectedIndex = 2;
        // No SelectedPoint/GetMarks query, UI drain, or paint occurs between assigning
        // the old glyph ordinal and changing the layout that splits the middle bin.
        host.Resize(840, 280);
        Assert.Equal(3, chart.SelectedIndex); Assert.Same(high, chart.SelectedPoint!.Members[0].Item);
        chart.SelectedIndex = 2;
        chart.BinRadius = 36;
        host.Drain(); Assert.Equal(new[] { "a", "b" }, chart.SelectedPoint!.Members.Select(member => member.StableId));
        chart.SelectedIndex = 2;
        rows.Remove(high);
        host.Drain(); Assert.Null(chart.SelectedPoint); Assert.Equal(-1, chart.SelectedIndex);
    }

    [AvaloniaFact]
    public void Setting_a_glyph_index_then_removing_its_source_does_not_select_the_new_occupant_of_that_ordinal()
    {
        var high = new Row("high", 10, 10);
        var rows = new ObservableCollection<Row> { new("low", -10, -10), new("a", 0, 0), new("b", .5, 0), high };
        var chart = new HexbinChart { ItemsSource = rows, XPath = "Value.Real", YPath = "Value.Imaginary", IdPath = "Id", BinRadius = 18 };
        using var host = new Host(chart);
        chart.SelectedIndex = 2;
        rows.Remove(high); // No query/paint publishes the requested selection before removal.
        host.Drain();
        Assert.Equal(3, chart.GetMarks().Count); Assert.Null(chart.SelectedPoint); Assert.Equal(-1, chart.SelectedIndex);
    }

    [AvaloniaFact]
    public void An_aggregate_follows_the_first_surviving_original_member_and_clears_when_only_strangers_occupy_its_old_cell()
    {
        var first = new Row("first", 0, 0); var second = new Row("second", .01, 0);
        var rows = new ObservableCollection<Row> { new("low", -10, -10), first, second, new("high", 10, 10) };
        var chart = new HexbinChart { ItemsSource = rows, XPath = "Value.Real", YPath = "Value.Imaginary", IdPath = "Id", SelectedIndex = 1 };
        using var host = new Host(chart); Key(chart, Avalonia.Input.Key.Home); Key(chart, Avalonia.Input.Key.Right);
        Assert.Equal(new[] { "first", "second" }, chart.SelectedPoint!.Members.Select(member => member.StableId));
        first.Value.Real = double.NaN; host.Drain();
        Assert.Same(second, Assert.Single(chart.SelectedPoint!.Members).Item); Assert.Same(second, Assert.Single(chart.ActivePoint!.Members).Item);
        second.Value.Real = double.NaN; rows.Add(new Row("stranger1", 0, 0)); rows.Add(new Row("stranger2", .01, 0)); host.Drain();
        Assert.Contains(chart.GetMarks(), mark => mark.Members.Count == 2);
        Assert.Null(chart.SelectedPoint); Assert.Null(chart.ActivePoint); Assert.Equal(-1, chart.SelectedIndex); Assert.Null(chart.DataError);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Aggregation_overflow_clears_stale_geometry_and_hits_defers_error_notifications_and_recovers_from_valid_data(bool wind)
    {
        var first = new WeightedRow("a", 0, 1, 1e308); var second = new WeightedRow("b", 0, 1, 1);
        var rows = new ObservableCollection<WeightedRow> { first, second };
        EngineeringChartBase chart = wind
            ? new WindRoseChart { ItemsSource = rows, DirectionPath = "X", SpeedPath = "Y", FrequencyPath = "Weight", IdPath = "Id" }
            : new HexbinChart { ItemsSource = rows, XPath = "X", YPath = "Y", ValuePath = "Weight", Aggregation = HexbinAggregation.Sum, IdPath = "Id" };
        chart.SelectedIndex = 0;
        using var host = new Host(chart); Key(chart, Avalonia.Input.Key.Home);
        Assert.Equal(2, chart.SelectedPoint!.Members.Count); Assert.NotNull(chart.ActivePoint); Assert.Null(chart.DataError);
        var rendering = false; var duringRender = 0; var errors = 0;
        chart.PropertyChanged += (_, args) =>
        {
            if (args.Property == EngineeringChartBase.DataErrorProperty)
            {
                errors++;
                if (chart.DataError is not null) { Assert.Equal(-1, chart.SelectedIndex); Assert.Null(chart.SelectedPoint); Assert.Null(chart.ActivePoint); }
            }
            if (rendering && (args.Property == EngineeringChartBase.DataErrorProperty || args.Property == EngineeringChartBase.ActivePointProperty || args.Property == EngineeringChartBase.SelectedPointProperty || args.Property == EngineeringChartBase.SelectedIndexProperty)) duringRender++;
        };
        second.Weight = 1e308; rendering = true; var failedFrame = Record(chart); rendering = false;
        Assert.Equal(0, duringRender); Assert.Equal(0, errors);
        Assert.NotEmpty(Drawings(failedFrame).OfType<GlyphRunDrawing>());
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<OverflowException>(chart.DataError); Assert.Equal(1, errors);
        Assert.Empty(chart.GetMarks()); Assert.Empty(chart.GetGridLines()); Assert.Null(chart.ActivePoint); Assert.Null(chart.SelectedPoint); Assert.Equal(-1, chart.SelectedIndex);
        if (chart is HexbinChart hex) Assert.Empty(hex.Bins); else Assert.Equal(0, ((WindRoseChart)chart).Rose.Count);
        second.Weight = 2; host.Drain();
        Assert.Null(chart.DataError); Assert.Equal(2, errors); Assert.NotEmpty(chart.GetMarks()); Assert.NotEmpty(chart.GetGridLines());
        Assert.Equal(new[] { "a", "b" }, Assert.Single(chart.GetMarks()).Members.Select(member => member.StableId));
        Assert.Null(chart.SelectedPoint); Assert.Null(chart.ActivePoint);
    }

    [AvaloniaFact]
    public void Source_budget_errors_are_visible_and_recoverable_while_programming_errors_are_not_swallowed()
    {
        var chart = new SmithChart { ItemsSource = new SourceFailure() };
        using (var host = new Host(chart))
        {
            Assert.IsType<ArgumentOutOfRangeException>(chart.DataError); Assert.Empty(chart.GetMarks());
            chart.ItemsSource = new[] { new SmithPoint(50, 0) }; host.Drain(); Assert.Null(chart.DataError); Assert.Single(chart.GetMarks());
        }
        var broken = new BrokenEngineering(); broken.Measure(new Size(420, 280)); broken.Arrange(new Rect(0, 0, 420, 280));
        Assert.Throws<InvalidOperationException>(() => broken.GetMarks());
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task Each_engineering_family_draws_geometry_and_uses_shared_export_validation_and_cancellation(int kind)
    {
        EngineeringChartBase chart = kind switch
        {
            0 => new SmithChart { ItemsSource = new[] { new SmithPoint(50, 25) } },
            1 => new TernaryChart { ItemsSource = new[] { new TernaryPoint(1, 1, 1) } },
            2 => new CarpetPlot { ItemsSource = new[] { new CarpetPoint(0, 0, 0), new CarpetPoint(1, 0, 1), new CarpetPoint(0, 1, 2), new CarpetPoint(1, 1, 3) } },
            3 => new HexbinChart { ItemsSource = new[] { new HexbinPoint(0, 0) } },
            _ => new WindRoseChart { ItemsSource = new[] { new WindObservation(0, 2) } },
        };
        using var host = new Host(chart); var group = Record(chart);
        Assert.NotEmpty(Drawings(group).OfType<GeometryDrawing>()); Assert.NotEmpty(chart.GetMarks());
        var failures = new List<ChartExportFailedEventArgs>(); chart.ExportFailed += (_, args) => failures.Add(args);
        using var output = new MemoryStream(); Assert.False(chart.TryExportJpeg(output, new ChartExportOptions { JpegQuality = 0 }));
        Assert.IsType<ArgumentOutOfRangeException>(Assert.Single(failures).Exception); Assert.Equal(0, output.Length);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.TryExportPngAsync(output, cancellationToken: cancellation.Token));
        Assert.Equal(0, output.Length); Assert.Single(failures);
    }

    private static DrawingGroup Record(EngineeringChartBase chart) { var group = new DrawingGroup(); using var drawing = group.Open(); chart.Render(drawing); return group; }
    private static IEnumerable<Drawing> Drawings(Drawing drawing)
    { yield return drawing; if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var nested in Drawings(child)) yield return nested; }
    private static void Key(EngineeringChartBase chart, Key key) => chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
    private static void Near(double expected, double actual) => Assert.True(Math.Abs(expected - actual) <= 1e-8 * Math.Max(1, Math.Abs(expected)), $"Expected {expected:R}, got {actual:R}");
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly EngineeringChartBase _chart;
        public Host(EngineeringChartBase chart) { _chart = chart; _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = _chart.GetMarks(); }
        public void Detach() { _window.Content = null; Drain(); }
        public void Attach() { _window.Content = _chart; Drain(); }
        public void Resize(double width, double height) { _window.Width = width; _window.Height = height; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed class Model(ObservableCollection<Row> items) : INotifyPropertyChanged
    {
        private ObservableCollection<Row> _items = items;
        public ObservableCollection<Row> Items { get => _items; set { _items = value; PropertyChanged?.Invoke(this, new(nameof(Items))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Row(string id, double real, double imaginary) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        public string Id { get; } = id; public ComplexValue Value { get; } = new(real, imaginary); public int Observers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
    private sealed class ComplexValue(double real, double imaginary) : INotifyPropertyChanged
    {
        private double _real = real; private PropertyChangedEventHandler? _changed;
        public double Real { get => _real; set { _real = value; _changed?.Invoke(this, new(nameof(Real))); } }
        public double Imaginary { get; } = imaginary; public int Observers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
    private sealed class WeightedRow(string id, double x, double y, double weight) : INotifyPropertyChanged
    {
        private double _weight = weight;
        public string Id { get; } = id; public double X { get; } = x; public double Y { get; } = y;
        public double Weight { get => _weight; set { _weight = value; PropertyChanged?.Invoke(this, new(nameof(Weight))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class SourceFailure : System.Collections.IEnumerable
    { public System.Collections.IEnumerator GetEnumerator() => throw new ArgumentOutOfRangeException("source", "Source exceeds the engineering observation budget."); }
    private sealed class BrokenEngineering : EngineeringChartBase
    { protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) => throw new InvalidOperationException("Renderer programming failure."); }
}
