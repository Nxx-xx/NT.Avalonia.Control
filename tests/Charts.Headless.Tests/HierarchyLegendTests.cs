using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class HierarchyLegendTests
{
    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Category_legend_aggregates_visible_original_nodes_and_sorted_bubbles_keep_group_colors(bool packed)
    {
        var a = new Item("a", 1, "west"); var b = new Item("b", 9, "east"); var c = new Item("c", 4, "west");
        var chart = Make(packed); chart.ItemsSource = new[] { a, b, c }; chart.IdPath = "Id"; chart.GroupPath = "Group"; chart.ShowLegend = true;
        using var host = new Host(chart); Assert.Null(chart.DataError);
        Assert.Equal(["west", "east"], chart.LegendItems.Select(item => item.Label));
        Assert.Equal([5d, 9d], chart.LegendItems.Select(item => item.Value));
        var west = chart.LegendItems[0]; Assert.Equal(["a", "c"], west.Nodes.Select(node => node.Id));
        Assert.Same(a, west.Nodes[0].Source); Assert.Same(c, west.Nodes[1].Source);
        Assert.Same(chart.GetMarks().Single(mark => Equals(mark.Key, "a")).Fill, chart.GetMarks().Single(mark => Equals(mark.Key, "c")).Fill);
        Assert.Same(west.Brush, chart.GetMarks().Single(mark => Equals(mark.Key, "a")).Fill);
        Assert.Throws<NotSupportedException>(() => ((IList<HierarchyLegendItem>)chart.LegendItems).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<HierarchyEntry>)west.Nodes).Clear());
    }

    [AvaloniaFact]
    public void Default_legend_preserves_plot_geometry_and_distinct_node_colors()
    {
        var chart = new PackedBubbleChart { ItemsSource = new[] { new HierarchyNode(1, label: "same"), new HierarchyNode(2, label: "same") } };
        using var host = new Host(chart); Assert.False(chart.ShowLegend); Assert.True(chart.ShowSizeLegend); Assert.Null(chart.GroupPath);
        var original = chart.PlotArea; var marks = chart.GetMarks().Select(mark => mark.Bounds).ToArray();
        Assert.Equal(2, chart.LegendItems.Count); Assert.NotSame(chart.LegendItems[0].Brush, chart.LegendItems[1].Brush);
        chart.ShowLegend = true; var smaller = chart.PlotArea;
        Assert.InRange(original.Height - smaller.Height, 0, Math.Min(100, original.Height / 3) + 1e-9);
        Assert.True(smaller.Height < original.Height);
        chart.ShowLegend = false; Assert.Equal(original, chart.PlotArea); Assert.Equal(marks, chart.GetMarks().Select(mark => mark.Bounds));
    }

    [AvaloniaFact]
    public void Category_colors_stay_global_across_drill_and_legend_totals_use_current_terminals()
    {
        var west = new Item("west", 0, "west", new Item("a", 3, "west"), new Item("b", 6, "west"));
        var east = new Item("east", 0, "east", new Item("c", 4, "east"));
        var chart = new PackedBubbleChart { ItemsSource = new[] { west, east }, IdPath = "Id", GroupPath = "Group", ShowLegend = true };
        using var host = new Host(chart); var color = chart.LegendItems.Single(item => item.Label == "east").Brush;
        Assert.True(chart.DrillDown("east")); var item = Assert.Single(chart.LegendItems);
        Assert.Equal("east", item.Label); Assert.Equal(4, item.Value); Assert.Same(color, item.Brush);
        Assert.True(chart.DrillUp()); chart.Collapse("west");
        Assert.Equal(9, chart.LegendItems.Single(entry => entry.Label == "west").Value);
        Assert.Equal("west", Assert.Single(chart.LegendItems.Single(entry => entry.Label == "west").Nodes).Id);
    }

    [AvaloniaFact]
    public void Nested_category_property_changes_recolor_without_losing_selected_source()
    {
        var a = new Item("a", 3, "west"); var b = new Item("b", 8, "east");
        var chart = new BubbleCloudChart { ItemsSource = new[] { a, b }, IdPath = "Id", GroupPath = "Category.Name", ShowLegend = true };
        using var host = new Host(chart); chart.SelectedIndex = 1;
        b.Category.Name = "west"; host.Drain();
        var item = Assert.Single(chart.LegendItems); Assert.Equal("west", item.Label); Assert.Equal(11, item.Value);
        Assert.Same(b, chart.SelectedPoint!.Members[0].Item); Assert.Equal(2, item.Nodes.Count);
        a.Value = 5; host.Drain(); Assert.Equal(13, Assert.Single(chart.LegendItems).Value);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Legend_is_nonselectable_footer_and_area_key_uses_nine_four_one_relative_areas(bool packed)
    {
        var chart = Make(packed); chart.ItemsSource = new[] { new Item("a", 4, "west") }; chart.GroupPath = "Group"; chart.ShowLegend = true; chart.ShowLabels = false;
        using var host = new Host(chart); using var boundary = new EllipseBoundary(); chart.Render(boundary.Drawing);
        // Record the explicit native circle commands rather than assuming that retained DrawEllipse
        // commands are represented as GeometryDrawing/EllipseGeometry by every Avalonia backend.
        var keys = boundary.Recorder.Ellipses.Where(bounds => bounds.Top > chart.PlotArea.Bottom).ToArray();
        Assert.Equal(3, keys.Length);
        Assert.All(keys, bounds => Assert.InRange(Math.Abs(bounds.Width - bounds.Height), 0, 1e-10));
        var areas = keys.Select(bounds => bounds.Width * bounds.Height).OrderDescending().ToArray();
        Assert.InRange(areas[0] / areas[2], 9 - 1e-8, 9 + 1e-8); Assert.InRange(areas[1] / areas[2], 4 - 1e-8, 4 + 1e-8);
        foreach (var key in keys) Assert.Null(chart.HitTest(key.Center));
        chart.ShowSizeLegend = false;
        using var hidden = new EllipseBoundary(); chart.Render(hidden.Drawing);
        Assert.DoesNotContain(hidden.Recorder.Ellipses, bounds => bounds.Top > chart.PlotArea.Bottom);
    }

    [AvaloniaFact]
    public void Legend_api_retains_all_categories_while_visual_footer_is_bounded_and_data_errors_clear_it()
    {
        var source = new ObservableCollection<Item>(Enumerable.Range(0, 35).Select(i => new Item($"node{i}", i + 1, $"group{i}")));
        var chart = new PackedBubbleChart { ItemsSource = source, IdPath = "Id", GroupPath = "Group", ShowLegend = true, ShowLabels = false, ShowSizeLegend = false };
        using var host = new Host(chart); Assert.Equal(35, chart.LegendItems.Count);
        Assert.InRange(Leaves(Record(chart)).OfType<GlyphRunDrawing>().Count(), 1, 20);
        source[0].Value = -1; host.Drain(); Assert.NotNull(chart.DataError); Assert.Empty(chart.LegendItems);
        source[0].Value = 1; host.Drain(); Assert.Null(chart.DataError); Assert.Equal(35, chart.LegendItems.Count);
        source.Clear(); host.Drain(); Assert.Empty(chart.LegendItems);
    }

    private static HierarchyChartBase Make(bool packed) => packed ? new PackedBubbleChart() : new BubbleCloudChart();
    private static DrawingGroup Record(HierarchyChartBase chart) { var group = new DrawingGroup(); using var context = group.Open(); chart.Render(context); return group; }
    private static IEnumerable<Drawing> Leaves(Drawing drawing)
    { if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var leaf in Leaves(child)) yield return leaf; else yield return drawing; }
    private sealed class EllipseBoundary : IDisposable
    {
        public DrawingContext Drawing { get; }
        public EllipseRecorder Recorder { get; }
        public EllipseBoundary()
        {
            var proxy = DispatchProxy.Create<IDrawingContextImpl, EllipseRecorder>(); Recorder = (EllipseRecorder)(object)proxy;
            var wrapper = typeof(DrawingContext).Assembly.GetType("Avalonia.Media.PlatformDrawingContext", true)!;
            Drawing = (DrawingContext)wrapper.GetConstructor([typeof(IDrawingContextImpl), typeof(bool)])!.Invoke([proxy, false]);
        }
        public void Dispose() => Drawing.Dispose();
    }
    public class EllipseRecorder : DispatchProxy
    {
        public List<Rect> Ellipses { get; } = [];
        private Matrix _transform = Matrix.Identity;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_Transform": return _transform;
                case "set_Transform": _transform = (Matrix)args![0]!; return null;
                case "DrawEllipse": foreach (var bounds in args!.OfType<Rect>()) Ellipses.Add(bounds.TransformToAABB(_transform)); return null;
                case "GetFeature": return null;
                case "Clear": case "DrawBitmap": case "DrawRegion": case "DrawGlyphRun": case "Dispose":
                case "DrawRectangle": case "DrawLine": case "DrawGeometry":
                case "PushClip": case "PopClip": case "PushLayer": case "PopLayer":
                case "PushOpacity": case "PopOpacity": case "PushOpacityMask": case "PopOpacityMask":
                case "PushGeometryClip": case "PopGeometryClip": case "PushRenderOptions": case "PopRenderOptions":
                case "PushTextOptions": case "PopTextOptions": return null;
                default: throw new NotSupportedException($"Unexpected platform operation: {targetMethod}.");
            }
        }
    }
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly HierarchyChartBase _chart;
        public Host(HierarchyChartBase chart) { _chart = chart; _window = new Window { Width = 480, Height = 340, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = _chart.GetMarks(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed class Category(string name) : INotifyPropertyChanged
    {
        private string _name = name;
        public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Item(string id, double value, string group, params Item[] children) : INotifyPropertyChanged
    {
        private double _value = value;
        public string Id { get; } = id;
        public string Label => Id;
        public string Group => Category.Name;
        public Category Category { get; } = new(group);
        public ObservableCollection<Item> Children { get; } = new(children);
        public double Value { get => _value; set { _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
