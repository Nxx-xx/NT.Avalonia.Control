using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _comparisonProbes = [];
    private async Task RunAdvancedComparisonSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var label = theme == ThemeVariant.Dark ? "dark" : "light";
            var background = Brush(label == "dark" ? "#111722" : "#FFFFFF");
            string Image(string kind, string state) => Path.Combine(Path.GetDirectoryName(baseImage)!, Path.GetFileNameWithoutExtension(baseImage) + $"-comparison-{label}-{kind}-{state}.png");
            void Check(string name, bool value) => checks[$"COMPARISON-{label}-{name}"] = value;
            foreach (var kind in new[] { "diverging", "dumbbell", "mekko", "mekko-area", "mirror", "population", "tornado", "venn2", "venn3" })
            {
                var rawRows = ComparisonProbeInputs(kind);
                var input = rawRows.Select((row, i) => ComparisonProbeData(kind, i, row)).ToArray();
                ComparisonChartBase chart = kind switch
                {
                    "diverging" => new DivergingBarChart { ValuePath = "Value" },
                    "dumbbell" => new DumbbellChart { LowValuePath = "Low", HighValuePath = "High" },
                    "mekko" or "mekko-area" => new MekkoChart { WidthPath = "Width", SegmentsPath = "Segments", SegmentIdPath = "Id", SegmentLabelPath = "Label", SegmentValuePath = "Value", ColumnGap = kind == "mekko-area" ? 0 : 4 },
                    "mirror" => new MirrorBarChart { LeftValuePath = "Left", RightValuePath = "Right" },
                    "population" => new PopulationPyramidChart { MaleValuePath = "Left", FemaleValuePath = "Right" },
                    "tornado" => new TornadoChart { LeftValuePath = "Left", RightValuePath = "Right" },
                    _ => new VennDiagramChart { MaskPath = "Mask", ValuePath = "Value", SetCount = kind == "venn2" ? 2 : 3 },
                };
                chart.ItemsSource = input; chart.IdPath = "Id"; chart.LabelPath = "Label";
                chart.Width = 420; chart.Height = 280; chart.Background = background;
                chart.ShowLabels = chart.ShowValues = false; chart.IsTooltipEnabled = false;
                chart.Palette = new IBrush[] { Brush("#1598B6"), Brush("#D25573"), Brush("#55B47A"), Brush("#E4AB47"), Brush("#8C73C6"), Brush("#D77F43"), Brush("#477DB3") };
                var config = new { baseline = 0d, orientation = "Horizontal", barHeight = .68, markerSize = 8d, connectorThickness = 2d,
                    centerGap = 12d, barGap = kind == "population" ? 2d : 0d, columnGap = kind == "mekko-area" ? 0d : 4d,
                    sort = kind == "tornado" ? "ValueDescending" : "Source", setCount = kind == "venn2" ? 2 : 3 };
                var window = new Window { Width = 420, Height = 280, CanResize = false, RequestedThemeVariant = theme, Background = background, Content = chart, Title = "Advanced comparison · " + kind };
                try
                {
                    window.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(window);
                    Check(kind + "-native-window", window.TryGetPlatformHandle() is { Handle: not 0 });
                    Check(kind + "-fixed-plot", chart.PlotArea == new Rect(24, 24, 372, 232));
                    var marks = chart.GetMarks(); Check(kind + "-valid-scene", chart.DataError is null && marks.Count > 0);
                    Check(kind + "-all-original-identities", marks.SelectMany(mark => mark.Members).All(member => member.Item is not null && member.StableId is string));
                    var nativeImage = Image(kind, "native");
                    using (var raster = new RenderTargetBitmap(new PixelSize(420, 280), new Vector(96, 96))) { raster.Render(chart); raster.Save(nativeImage); }
                    var exportImage = Image(kind, "export");
                    Check(kind + "-export96", chart.TryExportPng(exportImage, new ChartExportOptions { Dpi = 96, PixelSize = new PixelSize(420, 280), Background = background }));
                    var dpiImages = new List<string>();
                    if (label == "light" && kind is "venn2" or "mekko-area")
                    {
                        var dpi = kind == "venn2" ? 144 : 192; var path = Image(kind, "dpi" + dpi); dpiImages.Add(path);
                        Check(kind + "-dpi-export", chart.TryExportPng(path, new ChartExportOptions { Dpi = dpi, PixelSize = new PixelSize(420 * dpi / 96, 280 * dpi / 96), Background = background }));
                    }
                    var selections = new List<object>();
                    for (var i = 0; i < marks.Count; i++)
                    {
                        var mark = marks[i]; var hit = chart.HitTest(mark.Position);
                        Check(kind + "-hit-" + i, hit is not null);
                        if (chart is VennDiagramChart) Check(kind + "-exclusive-region-hit-" + i, Equals(hit?.Mark.Key, mark.Key));
                        chart.SelectedIndex = i; FlushControlTemplateWindow(window);
                        Check(kind + "-select-original-glyph-" + i, chart.SelectedPoint is { } selected && selected.Index == i && Equals(selected.Mark.Key, mark.Key) && selected.Members.Select(member => member.StableId).SequenceEqual(mark.Members.Select(member => member.StableId)));
                        selections.Add(new { index = i, target = new { x = mark.Position.X, y = mark.Position.Y }, hit = ComparisonProbeHit(hit), selected = ComparisonProbeHit(chart.SelectedPoint) });
                    }
                    var plot = chart.PlotArea;
                    Check(kind + "-outside-is-not-data", chart.HitTest(new Point(410, 10)) is null);
                    if (chart is PairedBarChartBase) Check(kind + "-center-gap-is-not-data", chart.HitTest(new Point(plot.Center.X, plot.Center.Y)) is null);
                    if (chart is VennDiagramChart) Check(kind + "-outside-circles-is-not-data", chart.HitTest(plot.TopLeft + new Vector(2, 2)) is null);
                    var blankPoint = kind switch
                    {
                        "diverging" => new Point(377.4, 24 + 232d / 6),
                        "dumbbell" => new Point(210, 24 + 232d / 6),
                        "mirror" or "population" or "tornado" => new Point(210, 140),
                        "venn2" or "venn3" => new Point(98, 28),
                        _ => new Point(410, 10),
                    };
                    var blankHit = chart.HitTest(blankPoint); Check(kind + "-independent-blank-is-not-data", blankHit is null);
                    _comparisonProbes.Add(new
                    {
                        kind, theme = label, phase = "baseline", widthDip = 420, heightDip = 280, rawRows, config,
                        plot = new { x = plot.X, y = plot.Y, width = plot.Width, height = plot.Height },
                        marks = marks.Select((mark, index) => ComparisonProbeMark(mark, index)).ToArray(), selections,
                        blank = new { target = new { x = blankPoint.X, y = blankPoint.Y }, hit = ComparisonProbeHit(blankHit) },
                        layout = chart is VennDiagramChart venn ? ComparisonProbeVenn(venn) : null,
                        nativeImage, exportImage, dpiImages, physicalInputValidated = false, performanceMeasured = false,
                    });
                    chart.Zoom = 1.2; chart.PanX = .03; chart.PanY = -.02; FlushControlTemplateWindow(window);
                    Check(kind + "-zoom-preserves-glyphs-and-members", chart.GetMarks().Select(mark => (mark.Key?.ToString(), string.Join("|", mark.Members.Select(member => member.StableId)))).SequenceEqual(marks.Select(mark => (mark.Key?.ToString(), string.Join("|", mark.Members.Select(member => member.StableId))))));
                    Check(kind + "-viewport-position", chart.GetMarks().Zip(marks).All(pair => Math.Abs(pair.First.Position.X - (plot.Center.X + (pair.Second.Position.X - plot.Center.X) * 1.2 + .03 * plot.Width)) < 1e-8));
                    chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Home });
                    chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Enter }); FlushControlTemplateWindow(window);
                    Check(kind + "-keyboard-selects-original-glyph", chart.SelectedPoint?.Index == 0);
                    chart.ResetViewport(); FlushControlTemplateWindow(window);
                    Check(kind + "-reset", chart.Zoom == 1 && chart.PanX == 0 && chart.PanY == 0 && chart.SelectedPoint is null);
                }
                finally { window.Close(); }
            }
            await RunComparisonLifecycleSmokeAsync(theme, Check);
            var board = CreateAdvancedComparisonBoard(overview: true);
            var overview = new Window { Title = "Charts · 进阶比较", Width = 1440, Height = 1100, RequestedThemeVariant = theme, Background = background, Content = new Border { Padding = new Thickness(16), Child = board } };
            try
            {
                overview.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(overview);
                Check("eight-demo-panels", board.GetVisualDescendants().OfType<ComparisonChartBase>().Count() == 8);
                var path = Image("board", "overview");
                using var raster = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(overview.ClientSize.Width), (int)Math.Ceiling(overview.ClientSize.Height)), new Vector(96, 96)); raster.Render(overview); raster.Save(path);
                _comparisonProbes.Add(new { kind = "board", theme = label, phase = "board", image = path, physicalInputValidated = false, performanceMeasured = false });
            }
            finally { overview.Close(); }
        }
    }

    private async Task RunComparisonLifecycleSmokeAsync(ThemeVariant theme, Action<string, bool> check)
    {
        var rows = new ObservableCollection<ComparisonProbeRow>(ComparisonProbeInputs("mirror").Select((row, i) => ComparisonProbeData("mirror", i, row)));
        var mirror = new MirrorBarChart { ItemsSource = rows, IdPath = "Id", LabelPath = "Label", LeftValuePath = "Left", RightValuePath = "Right", Width = 420, Height = 280 };
        var window = new Window { Width = 420, Height = 280, Content = mirror, RequestedThemeVariant = theme };
        try
        {
            window.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(window);
            mirror.SelectedIndex = 1; mirror.Sort = ComparisonSort.ValueDescending; mirror.Width = window.Width = 840; FlushControlTemplateWindow(window);
            check("continuous-selection-sort-resize-keeps-right-side", mirror.SelectedPoint is { } hit && Equals(hit.Mark.Key, "right") && ReferenceEquals(hit.Members[0].Item, rows[0]));
            rows.RemoveAt(0); FlushControlTemplateWindow(window); check("removed-source-clears-compound-selection", mirror.SelectedPoint is null);
            var children = new ObservableCollection<ComparisonProbeSegment> { new("child-0", "first", 2), new("child-1", "second", 3) };
            var mekko = new MekkoChart { ItemsSource = new[] { new ComparisonProbeRow("column-0", "column", Segments: children, Width: 2) }, IdPath = "Id", WidthPath = "Width", SegmentsPath = "Segments", SegmentIdPath = "Id", Width = 420, Height = 280 };
            window.Width = 420; window.Content = mekko; FlushControlTemplateWindow(window);
            var old = mekko.GetMarks(); children.Add(new("child-bad", "invalid", -1)); FlushControlTemplateWindow(window);
            check("nested-invalid-segment-clears-scene", mekko.DataError is not null && mekko.GetMarks().Count == 0 && old.Count == 2);
            children.RemoveAt(children.Count - 1); FlushControlTemplateWindow(window);
            check("same-nested-collection-recovers-data-error", mekko.DataError is null && mekko.GetMarks().Count == 2);
            window.Content = null; FlushControlTemplateWindow(window); children.Add(new("child-2", "third", 1)); window.Content = mekko; FlushControlTemplateWindow(window);
            check("reattach-reads-latest-nested-source", mekko.GetMarks().Count == 3);
            mekko.ItemsSource = new[] { new ComparisonProbeRow("new-column", "replacement", Segments: new ComparisonProbeSegment[] { new("new-child", "only", 1) }, Width: 1) }; FlushControlTemplateWindow(window);
            children.Add(new("old-child", "retired", -1)); FlushControlTemplateWindow(window);
            check("replacement-detaches-old-child-observers", mekko.DataError is null && mekko.GetMarks().Count == 1);
        }
        finally { window.Close(); }
    }

    private static double[][] ComparisonProbeInputs(string kind) => kind switch
    {
        "diverging" => new double[][] { new[] { -6d }, new[] { 3d }, new[] { 9d } },
        "dumbbell" => new double[][] { new[] { 2d, 8d }, new[] { 9d, 3d }, new[] { 5d, 5d } },
        "mekko" or "mekko-area" => new double[][] { new[] { 2d, 2d, 3d }, new[] { 3d, 4d, 1d }, new[] { 5d, 1d, 1d } },
        "venn2" => new double[][] { new[] { 1d, 60d }, new[] { 2d, 40d }, new[] { 3d, 20d } },
        "venn3" => new double[][] { new[] { 1d, 100d }, new[] { 2d, 100d }, new[] { 4d, 100d }, new[] { 3d, 30d }, new[] { 5d, 30d }, new[] { 6d, 30d }, new[] { 7d, 10d } },
        _ => new double[][] { new[] { 6d, 4d }, new[] { 3d, 8d }, new[] { 9d, 5d } },
    };
    private static ComparisonProbeRow ComparisonProbeData(string kind, int index, double[] row)
    {
        var id = $"{kind}-{index}"; var label = $"row-{index}";
        return kind switch
        {
            "diverging" => new(id, label, Value: row[0]),
            "dumbbell" => new(id, label, Low: row[0], High: row[1]),
            "mekko" or "mekko-area" => new(id, label, Width: row[0], Segments: row.Skip(1).Select((value, i) => new ComparisonProbeSegment($"{id}-segment-{i}", $"segment-{i}", value)).ToArray()),
            "venn2" or "venn3" => new(id, label, Mask: (int)row[0], Value: row[1]),
            _ => new(id, label, Left: row[0], Right: row[1]),
        };
    }
    private static object ComparisonProbeMember(ChartDataPoint member) => new { sourceIndex = member.SourceIndex, stableId = member.StableId, item = member.Item, point = new { x = member.Point.X, y = member.Point.Y }, value = member.Value, weight = member.Weight };
    private static object ComparisonProbeMark(EngineeringMarkInfo mark, int index) => new { index, key = mark.Key?.ToString(), position = new { x = mark.Position.X, y = mark.Position.Y }, value = mark.Value, fill = mark.Fill is ISolidColorBrush brush ? brush.Color.ToString() : null, members = mark.Members.Select(ComparisonProbeMember).ToArray() };
    private static object? ComparisonProbeHit(EngineeringPointHit? hit) => hit is null ? null : new { index = hit.Index, members = hit.Members.Select(ComparisonProbeMember).ToArray() };
    private static object ComparisonProbeVenn(VennDiagramChart chart) => new { chart.Layout!.Circles, chart.Layout.Regions, chart.Layout.IsApproximate, chart.Layout.MaximumRelativeAreaError, chart.Layout.TripleIntersectionErrorEstimate, chart.UnrepresentedRegionMasks };
    private sealed record ComparisonProbeSegment(string Id, string Label, double Value);
    private sealed record ComparisonProbeRow
    {
        public ComparisonProbeRow(string Id, string Label, double Value = 0, double Low = 0, double High = 0, double Left = 0, double Right = 0, double Width = 0, IReadOnlyList<ComparisonProbeSegment>? Segments = null, int Mask = 0)
        { this.Id = Id; this.Label = Label; this.Value = Value; this.Low = Low; this.High = High; this.Left = Left; this.Right = Right; this.Width = Width; this.Segments = Segments ?? Array.Empty<ComparisonProbeSegment>(); this.Mask = Mask; }
        public string Id { get; } public string Label { get; } public double Value { get; } public double Low { get; } public double High { get; } public double Left { get; } public double Right { get; } public double Width { get; } public IReadOnlyList<ComparisonProbeSegment> Segments { get; } public int Mask { get; }
    }
}
