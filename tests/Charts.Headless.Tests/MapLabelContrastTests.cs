using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class MapLabelContrastTests
{
    [AvaloniaTheory]
    [InlineData("#F1F5F9", "#111722", 1, 1, "#000000")]
    [InlineData("#172235", "#FFFFFF", 1, 1, "#FFFFFF")]
    [InlineData("#20FFFFFF", "#000000", 1, 1, "#FFFFFF")]
    [InlineData("#FFFFFF", "#000000", .1, 1, "#FFFFFF")]
    [InlineData("#FFFFFF", "#000000", 1, .1, "#FFFFFF")]
    [InlineData("#FF000000", "#FFFFFF", .1, .5, "#000000")]
    public void Categorical_map_labels_use_the_displayed_solid_fill_alpha_and_layer_opacity(
        string color, string background, double brushOpacity, double layerOpacity, string expected)
    {
        var row = new CategoryRow("region", "Available");
        var layer = new ShapeLayer
        {
            ItemsSource = new[] { row }, RegionPath = nameof(CategoryRow.Id), CategoryPath = nameof(CategoryRow.State),
            ColorMode = MapColorMode.Categorical, Opacity = layerOpacity,
            CategoryColors = new Dictionary<object, IBrush> { ["Available"] = new SolidColorBrush(Color.Parse(color), brushOpacity) },
        };
        using var map = NewMap(); map.Foreground = Brushes.White; map.Background = Brush.Parse(background); map.Layers.Add(layer);
        using var host = new Host(map);
        AssertLabels(map, Color.Parse(expected));
        var mark = Assert.Single(map.GetMarks()); Assert.Same(row, map.HitTest(mark.Position)!.Members[0].Item);
        Assert.True(map.SelectItem(layer, row.Id)); Assert.Same(row, map.SelectedPoint!.Members[0].Item);
        Assert.Same(Brushes.White, map.Foreground);
    }

    [AvaloniaFact]
    public void Transparent_map_labels_follow_solid_ancestor_background_changes_without_recapturing_the_source()
    {
        var row = new CategoryRow("region", "Available"); var source = new CountingRows(row);
        var layer = new ShapeLayer
        {
            ItemsSource = source, RegionPath = nameof(CategoryRow.Id), CategoryPath = nameof(CategoryRow.State),
            ColorMode = MapColorMode.Categorical,
            CategoryColors = new Dictionary<object, IBrush> { ["Available"] = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)) },
        };
        using var map = NewMap(); map.Foreground = Brushes.White; map.Background = Brushes.Transparent; map.Layers.Add(layer);
        using var host = new Host(map, Brushes.Black);
        AssertLabels(map, Colors.White); var reads = source.Enumerations; var marks = map.GetMarks();
        host.Background = Brushes.White;
        AssertLabels(map, Colors.Black); Assert.Equal(reads, source.Enumerations); Assert.Same(marks, map.GetMarks());
        host.Background = Brushes.Black; map.Background = new SolidColorBrush(Colors.White, .8);
        AssertLabels(map, Colors.Black); Assert.Equal(reads, source.Enumerations);
    }

    [AvaloniaFact]
    public void No_data_labels_use_their_real_fill_and_keep_regions_nonselectable()
    {
        using var map = NewMap(); map.Foreground = Brushes.White; map.Background = Brushes.Black;
        var layer = new ShapeLayer { NoDataBrush = Brushes.White }; map.Layers.Add(layer); using var host = new Host(map);
        Assert.Empty(map.GetMarks()); AssertLabels(map, Colors.Black);
        layer.NoDataBrush = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255));
        AssertLabels(map, Colors.White); Assert.Empty(map.GetMarks()); Assert.Null(map.HitTest(map.MapViewport!.FittedArea.Center));
    }

    [AvaloniaFact]
    public void Bubble_labels_use_fill_contrast_while_title_and_legend_keep_the_requested_foreground()
    {
        using var map = NewMap(); map.Foreground = Brushes.Red; map.Background = Brushes.Black; map.Title = "Map title";
        map.Layers.Add(new BubbleLayer { ItemsSource = new[] { new MapObservation("bubble", new(5, 5), 1) }, Fill = Brushes.White, MaximumRadius = .25 });
        using var host = new Host(map);
        var glyphs = Glyphs(Record(map)).ToArray(); Assert.NotEmpty(glyphs);
        Assert.Contains(glyphs, glyph => IsColor(glyph.Foreground, Colors.Black));
        Assert.Contains(glyphs, glyph => IsColor(glyph.Foreground, Colors.Red));
        Assert.Same(Brushes.Red, map.Foreground);
        map.Title = null; map.Layers.Clear(); map.ShowLegend = true;
        map.Layers.Add(new ShapeLayer { ItemsSource = new[] { new MapValue("region", 1) }, MinimumColor = Colors.White, MaximumColor = Colors.White });
        var withLegend = Glyphs(Record(map)).ToArray();
        Assert.Contains(withLegend, glyph => IsColor(glyph.Foreground, Colors.Black));
        Assert.Contains(withLegend, glyph => IsColor(glyph.Foreground, Colors.Red));
    }

    private static SeatMap NewMap() => new()
    {
        ShowLegend = false, ShowLabels = true,
        FeaturesSource = [new MapFeature("region", [new MapPolygon([new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)])], "Region")],
    };
    private static void AssertLabels(ShapeMap map, Color expected)
    {
        var glyphs = Glyphs(Record(map)).ToArray(); Assert.NotEmpty(glyphs);
        Assert.All(glyphs, glyph => Assert.True(IsColor(glyph.Foreground, expected), $"Expected label color {expected}; actual {glyph.Foreground}."));
    }
    private static bool IsColor(IBrush? brush, Color color) => brush is ISolidColorBrush solid && solid.Color == color && solid.Opacity == 1;
    private static DrawingGroup Record(ShapeMap map) { var group = new DrawingGroup(); using var context = group.Open(); map.Render(context); return group; }
    private static IEnumerable<GlyphRunDrawing> Glyphs(Drawing drawing)
    {
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var glyph in Glyphs(child)) yield return glyph;
        else if (drawing is GlyphRunDrawing glyph) yield return glyph;
    }
    private sealed record CategoryRow(string Id, string State);
    private sealed class CountingRows(CategoryRow row) : System.Collections.IEnumerable
    {
        public int Enumerations { get; private set; }
        public System.Collections.IEnumerator GetEnumerator() { Enumerations++; return new[] { row }.GetEnumerator(); }
    }
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly Border _container;
        public Host(ShapeMap map, IBrush? background = null)
        {
            _container = new Border { Background = background, Child = map };
            _window = new Window { Width = 420, Height = 280, Content = _container }; _window.Show();
            _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = map.GetMarks();
        }
        public IBrush? Background { set => _container.Background = value; }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
