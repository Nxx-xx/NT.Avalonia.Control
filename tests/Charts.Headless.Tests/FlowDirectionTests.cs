using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class FlowDirectionTests
{
    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Inherited_RTL_preserves_original_pointer_hit_selection_and_template_direction(int family)
    {
        var scene = Create(family);
        using var host = new Host(scene.Chart, FlowDirection.RightToLeft);
        Assert.Equal(FlowDirection.RightToLeft, scene.Chart.FlowDirection);
        Assert.Equal(FlowDirection.RightToLeft, scene.Template.FlowDirection);
        VerifyPlotContract(scene);
        if (family != 1) Assert.True(host.WindowPoint(Anchor(scene, "a")).X < host.WindowPoint(Anchor(scene, "c")).X);
        var anchor = Anchor(scene, "b");
        VerifyOriginalHit(scene, anchor, "b", scene.Rows[1]);
        host.Click(anchor);
        AssertSelectedOriginal(scene, "b", scene.Rows[1]);
        host.Parent.FlowDirection = FlowDirection.LeftToRight; host.Drain();
        Assert.Equal(FlowDirection.LeftToRight, scene.Chart.FlowDirection);
        Assert.Equal(FlowDirection.LeftToRight, scene.Template.FlowDirection);
        VerifyPlotContract(scene);
        VerifyOriginalHit(scene, Anchor(scene, "b"), "b", scene.Rows[1]);
        AssertSelectedOriginal(scene, "b", scene.Rows[1]);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Runtime_flow_direction_binding_survives_roundtrips_without_changing_source_identity(int family)
    {
        var scene = Create(family); var model = new DirectionModel();
        scene.Chart.DataContext = model;
        using var binding = scene.Chart.Bind(Visual.FlowDirectionProperty, new Binding(nameof(DirectionModel.Direction)) { Source = model });
        using var host = new Host(scene.Chart, FlowDirection.LeftToRight);
        host.Click(Anchor(scene, "b")); AssertSelectedOriginal(scene, "b", scene.Rows[1]);
        foreach (var direction in new[] { FlowDirection.RightToLeft, FlowDirection.LeftToRight, FlowDirection.RightToLeft })
        {
            model.Direction = direction; host.Drain();
            Assert.Equal(direction, scene.Chart.FlowDirection);
            Assert.Equal(direction, scene.Template.FlowDirection);
            AssertSelectedOriginal(scene, "b", scene.Rows[1]);
            VerifyOriginalHit(scene, Anchor(scene, "b"), "b", scene.Rows[1]);
            VerifyPlotContract(scene);
            if (family != 1) Assert.True(host.WindowPoint(Anchor(scene, "a")).X < host.WindowPoint(Anchor(scene, "c")).X);
            var business = scene.Template.DataContext switch
            {
                ChartPlotAreaContext context => context.DataContext,
                ChartDataPlotAreaContext context => context.DataContext,
                _ => null,
            };
            Assert.Same(model, business);
        }
    }

    [AvaloniaTheory]
    [InlineData(FlowDirection.LeftToRight)] [InlineData(FlowDirection.RightToLeft)]
    public void Explicit_axis_inversion_controls_geometry_in_both_layout_directions(FlowDirection direction)
    {
        var scene = Create(0); var chart = (CartesianChart)scene.Chart;
        using var host = new Host(chart, direction);
        var context = Assert.IsType<ChartPlotAreaContext>(scene.Template.DataContext);
        var natural = context.Project(1, 20);
        Assert.Equal(context.PlotArea.Width / 4, natural.X, 7);
        Assert.True(host.WindowPoint(Anchor(scene, "a")).X < host.WindowPoint(Anchor(scene, "c")).X);
        chart.XAxis.IsInversed = true; host.Drain();
        context = Assert.IsType<ChartPlotAreaContext>(scene.Template.DataContext);
        var inverted = context.Project(1, 20);
        Assert.Equal(context.PlotArea.Width * .75, inverted.X, 7);
        Assert.Equal(natural.Y, inverted.Y, 7);
        var point = new Point(chart.PlotArea.Left + inverted.X, chart.PlotArea.Top + inverted.Y);
        VerifyOriginalHit(scene, point, "a", scene.Rows[0]);
        host.Click(point); AssertSelectedOriginal(scene, "a", scene.Rows[0]);
        Assert.True(chart.XAxis.IsInversed);
        Assert.True(host.WindowPoint(Anchor(scene, "a")).X > host.WindowPoint(Anchor(scene, "c")).X);
        Assert.Equal(direction, scene.Template.FlowDirection);
    }

    private sealed record Row(string Id, double X, double Value, string Label, string Column, string Group);
    private sealed record Scene(ChartBase Chart, Border Template, Row[] Rows);
    private static Scene Create(int family)
    {
        Row[] rows = [new("a", 1, 20, "中文 A", "甲", "线上"), new("b", 2, 60, "原始 B", "甲", "门店"), new("c", 3, 80, "C", "乙", "线上")];
        var child = new Border();
        ChartBase chart;
        if (family == 0)
        {
            var cartesian = new CartesianChart { XAxis = new NumericAxis { Minimum = 0, Maximum = 4 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
                ShowLegend = false, PlotAreaContent = new object(), PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((_, _) => child) };
            cartesian.Series.Add(new ScatterSeries { ItemsSource = rows, IdPath = nameof(Row.Id), CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Value), MarkerSize = 14 });
            chart = cartesian;
        }
        else if (family == 1)
            chart = new FunnelChart { ItemsSource = rows, IdPath = nameof(Row.Id), CategoryPath = nameof(Row.Label), ValuePath = nameof(Row.Value), ShowLabels = false, ShowValues = false,
                MaxLegendItems = 0, PlotAreaContent = new object(), PlotAreaContentTemplate = new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) => child) };
        else
            chart = new MosaicChart { ItemsSource = rows, IdPath = nameof(Row.Id), LabelPath = nameof(Row.Label), ValuePath = nameof(Row.Value), GroupPath = nameof(Row.Column), SubGroupPath = nameof(Row.Group),
                ShowLabels = false, ShowValues = false, MaxLegendItems = 0, PlotAreaContent = new object(), PlotAreaContentTemplate = new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) => child) };
        return new(chart, child, rows);
    }

    private static void VerifyPlotContract(Scene scene)
    {
        if (scene.Chart is CartesianChart chart)
        {
            var context = Assert.IsType<ChartPlotAreaContext>(scene.Template.DataContext);
            Assert.Equal(chart.PlotArea, context.PlotArea);
            Assert.Equal(chart.PlotArea.Width / 2, context.Project(2, 60).X, 7);
            Assert.Equal(chart.PlotArea.Height * .4, context.Project(2, 60).Y, 7);
        }
        else
        {
            var context = Assert.IsType<ChartDataPlotAreaContext>(scene.Template.DataContext);
            var plot = scene.Chart is EngineeringChartBase e ? e.PlotArea : ((AnalysisChartBase)scene.Chart).PlotArea;
            Assert.Equal(plot, context.PlotArea);
            Assert.Equal(plot.Width * .25, context.ProjectLayout(.25, .75).X, 7);
            Assert.Equal(plot.Height * .75, context.ProjectLayout(.25, .75).Y, 7);
        }
    }

    private static Point Anchor(Scene scene, string id) => scene.Chart switch
    {
        CartesianChart chart => PointInChart(chart, Assert.IsType<ChartPlotAreaContext>(scene.Template.DataContext), scene.Rows.Single(row => row.Id == id)),
        AnalysisChartBase chart => chart.GetMarks().Single(mark => Equals(mark.Data.StableId, id)).Bounds.Center,
        EngineeringChartBase chart => chart.GetMarks().Single(mark => mark.Members.Any(point => Equals(point.StableId, id))).Bounds.Center,
        _ => throw new InvalidOperationException(),
    };
    private static Point PointInChart(CartesianChart chart, ChartPlotAreaContext context, Row row)
    { var local = context.Project(row.X, row.Value); return new(chart.PlotArea.X + local.X, chart.PlotArea.Y + local.Y); }
    private static void VerifyOriginalHit(Scene scene, Point point, string id, Row row)
    {
        ChartDataPoint? data = null;
        if (scene.Chart is CartesianChart cartesian)
        {
            var hit = Assert.IsType<ChartPointHit>(cartesian.HitTestPoint(point, .1));
            Assert.Equal(id, hit.StableId); Assert.Same(row, hit.Item); Assert.Equal(Array.IndexOf(scene.Rows, row), hit.Index);
        }
        else if (scene.Chart is AnalysisChartBase analysis)
        {
            var hit = Assert.IsType<ChartPointHit>(analysis.HitTest(point));
            Assert.Equal(id, hit.StableId); Assert.Same(row, hit.Item); Assert.Equal(Array.IndexOf(scene.Rows, row), hit.Index);
        }
        else data = Assert.Single(Assert.IsType<EngineeringPointHit>(((EngineeringChartBase)scene.Chart).HitTest(point)).Members);
        if (data is { } datum) { Assert.Equal(id, datum.StableId); Assert.Same(row, datum.Item); Assert.Equal(Array.IndexOf(scene.Rows, row), datum.SourceIndex); }
    }
    private static void AssertSelectedOriginal(Scene scene, string id, Row row)
    {
        if (scene.Chart is CartesianChart chart)
        { Assert.Equal(id, chart.SelectedPoint?.StableId); Assert.Same(row, chart.SelectedPoint?.Item); return; }
        var selected = scene.Chart is EngineeringChartBase e ? e.GetSelectedData() : ((AnalysisChartBase)scene.Chart).GetSelectedData();
        var datum = Assert.Single(selected); Assert.Equal(id, datum.StableId); Assert.Same(row, datum.Item);
    }
    private sealed class DirectionModel : INotifyPropertyChanged
    {
        private FlowDirection _direction;
        public FlowDirection Direction { get => _direction; set { if (_direction == value) return; _direction = value; PropertyChanged?.Invoke(this, new(nameof(Direction))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Host : IDisposable
    {
        private readonly Window _window;
        private readonly ChartBase _chart;
        internal Grid Parent { get; }
        internal Host(ChartBase chart, FlowDirection direction)
        { _chart = chart; Parent = new Grid { FlowDirection = direction, Children = { chart } }; _window = new Window { Width = 700, Height = 440, Content = Parent }; _window.Show(); Drain(); }
        internal void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); }
        internal Point WindowPoint(Point point) => _chart.TranslatePoint(point, _window) ?? throw new InvalidOperationException("Chart detached.");
        internal void Click(Point point)
        { var translated = _chart.TranslatePoint(point, _window) ?? throw new InvalidOperationException("Chart detached."); _window.MouseDown(translated, MouseButton.Left); _window.MouseUp(translated, MouseButton.Left); Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
