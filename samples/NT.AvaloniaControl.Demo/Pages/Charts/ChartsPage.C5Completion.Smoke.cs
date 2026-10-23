using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _c5CompletionProbes = [];
    private static double? C5Finite(double value) => double.IsFinite(value) ? value : null;
    private static object C5Point(Point point) => new { x = C5Finite(point.X), y = C5Finite(point.Y) };
    private static object C5Point(ChartPoint point) => new { x = C5Finite(point.X), y = C5Finite(point.Y) };
    private static object C5Rect(Rect rect) => new { x = C5Finite(rect.X), y = C5Finite(rect.Y), width = C5Finite(rect.Width), height = C5Finite(rect.Height) };
    private static C5ObservedRow[] C5CompletionOriginal(EngineeringChartBase chart) => chart.ItemsSource?.Cast<C5ObservedRow>().ToArray() ?? [];
    private static object C5Raw(C5ObservedRow source) => source switch
    {
        C5SampleRow row => new { id = row.Id, label = row.Label, category = row.Category, value = C5Finite(row.Value), type = nameof(C5SampleRow) },
        C5ContourRow row => new { id = row.Id, label = row.Label, x = row.X, y = row.Y, value = C5Finite(row.Value), type = nameof(C5ContourRow) },
        C5MosaicRow row => new { id = row.Id, label = row.Label, column = row.Column, row = row.Row, value = C5Finite(row.Value), type = nameof(C5MosaicRow) },
        C5ParallelRow row => new { id = row.Id, label = row.Label, values = new[] { C5Finite(row.Cost), C5Finite(row.Quality), C5Finite(row.Speed) }, type = nameof(C5ParallelRow) },
        C5ProfileRow row => new { id = row.Id, label = row.Label, samples = row.Samples.Select(C5Finite).ToArray(), type = nameof(C5ProfileRow) },
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };
    private static object C5Geometry(Geometry geometry) => geometry switch
    {
        PolylineGeometry line => new { type = nameof(PolylineGeometry), points = line.Points.Select(C5Point).ToArray(), bounds = C5Rect(line.Bounds) },
        EllipseGeometry ellipse => new { type = nameof(EllipseGeometry), rect = C5Rect(ellipse.Rect), bounds = C5Rect(ellipse.Bounds) },
        GeometryGroup group => new { type = nameof(GeometryGroup), children = group.Children.Select(C5Geometry).ToArray(), bounds = C5Rect(group.Bounds) },
        _ => new { type = geometry.GetType().Name, bounds = C5Rect(geometry.Bounds) }
    };
    private static object C5Mark(EngineeringChartBase chart, EngineeringMarkInfo mark, int index)
    {
        var raw = C5CompletionOriginal(chart);
        return new
        {
            index, key = mark.Key?.ToString(), value = C5Finite(mark.Value), position = C5Point(mark.Position), bounds = C5Rect(mark.Bounds),
            fill = mark.Fill?.ToString(), stroke = mark.Stroke?.ToString(), geometry = C5Geometry(mark.Geometry), tooltip = mark.TooltipText,
            members = mark.Members.Select(member => new
            {
                sourceIndex = member.SourceIndex, stableId = member.StableId?.ToString(), rawId = (member.Item as C5ObservedRow)?.Id,
                originalSourceReference = raw.Any(item => ReferenceEquals(item, member.Item)),
                sourceIndexReference = member.SourceIndex >= 0 && member.SourceIndex < raw.Length && ReferenceEquals(raw[member.SourceIndex], member.Item),
                point = C5Point(member.Point)
            }).ToArray()
        };
    }
    private static object? C5Layout(EngineeringChartBase chart) => chart switch
    {
        CategoricalDistributionChartBase categorical when categorical.Layout is { } layout => new
        {
            kind = chart.GetType().Name, minimum = layout.Minimum, maximum = layout.Maximum, collisions = layout.CollisionCount, overflow = layout.OverflowCount, workBudgetExceeded = layout.WorkBudgetExceeded,
            categories = layout.Categories.Select(category => new { category = category.Category.ToString(), category.CategoryIndex, category.Count, category.Mean, category.CenterX, category.MeanY }).ToArray(),
            points = layout.Points.Select(point => new { point.SourceIndex, id = point.Id?.ToString(), category = point.Category.ToString(), point.CategoryIndex, point.Value, point.X, point.Y, point.IsOverflow }).ToArray()
        },
        ContourPlot contour when contour.Contours is { } layout => new
        {
            kind = nameof(ContourPlot), levels = layout.Levels, xCoordinates = layout.XCoordinates, yCoordinates = layout.YCoordinates, maskedCellCount = layout.MaskedCellCount, saddleCellCount = layout.SaddleCellCount,
            grid = layout.Grid.Select((point, index) => new { point.X, point.Y, value = C5Finite(point.Value), id = point.Id?.ToString(), sourceIndex = layout.SourceIndices[index] }).ToArray(),
            lines = layout.Lines.Select(line => new { line.Level, line.CellIndex, line.CellIndices, line.SourceIndices, start = C5Point(line.Start), end = C5Point(line.End) }).ToArray(),
            bands = layout.Bands.Select(band => new { band.BandIndex, band.LowerBound, band.UpperBound, band.CellIndex, band.SourceIndices, vertices = band.Vertices.Select(C5Point).ToArray() }).ToArray()
        },
        MosaicChart mosaic when mosaic.Layout is { } layout => new
        {
            kind = nameof(MosaicChart), layout.SourceCount, columns = layout.Columns.Select(x => x.ToString()).ToArray(), rows = layout.Rows.Select(x => x.ToString()).ToArray(),
            cells = layout.Cells.Select(cell => new { column = cell.Column.ToString(), row = cell.Row.ToString(), cell.ColumnIndex, cell.RowIndex, cell.SourceIndices, cell.Value, cell.Share, cell.X, cell.Y, cell.Width, cell.Height }).ToArray()
        },
        ParallelCoordinatesChart parallel when parallel.Layout is { } layout => new
        {
            kind = nameof(ParallelCoordinatesChart), layout.OmittedValues,
            dimensions = layout.Dimensions.Select(axis => new { id = axis.Id.ToString(), axis.Header, axis.Minimum, axis.Maximum, axis.IsReversed }).ToArray(),
            records = layout.Records.Select(row => new { row.SourceIndex, id = row.Source.Id?.ToString(), values = row.Source.Values.Select(C5Finite).ToArray(), coordinates = row.Coordinates.Select(point => new { point.DimensionIndex, point.Value, position = C5Point(point.Position) }).ToArray() }).ToArray()
        },
        DistributionProfileChartBase profile when profile.Layout is { } layout => new
        {
            kind = chart.GetType().Name, layout.Minimum, layout.Maximum, layout.MaximumDensity, layout.GroupCount, layout.InputCount, layout.OmittedCount, emptyGroupIndices = layout.EmptyGroupIndices,
            sampleCount = profile.SampleCount, bandwidth = profile.Bandwidth,
            groups = layout.Groups.Select(group => new { group.SourceIndex, id = group.Id?.ToString(), group.Label, rawSourceReference = C5CompletionOriginal(chart).Any(row => ReferenceEquals(row, group.SourceItem)),
                observations = group.Observations.Select(observation => new { observation.OriginalIndex, observation.Value }).ToArray(),
                median = group.Summary.Median, lowerQuartile = group.Summary.LowerQuartile, upperQuartile = group.Summary.UpperQuartile,
                density = group.Density.Points.Select(C5Point).ToArray() }).ToArray()
        },
        _ => null
    };
    private static void ConfigureC5CompletionFixed(EngineeringChartBase chart, bool dark)
    {
        chart.Width = 420; chart.Height = 280; chart.Title = null; chart.ShowGrid = false; chart.IsTooltipEnabled = false;
        chart.Background = Brush(dark ? "#111722" : "#FFFFFF"); chart.Foreground = Brush(dark ? "#E5E7EB" : "#1F2937");
        if (chart is ComparisonChartBase comparison) { comparison.ShowLabels = false; comparison.ShowValues = false; }
        if (chart is CategoricalDistributionChartBase categorical) { categorical.ShowAxes = false; categorical.ShowCategoryLabels = false; categorical.ShowMean = false; }
    }
    private static IReadOnlyList<(string Id, Point Point, string[] ExpectedAnyRawIds)> C5CompletionAnchors(string kind) => kind switch
    {
        "beeswarm" or "strip" => [("category-a-middle", new(117, 140), ["sample-a1", "sample-a2"]), ("category-b-middle", new(303, 140), ["sample-b1", "sample-b2"])],
        "contour" => [("lower-band", new(117, 198), ["grid-00", "grid-10", "grid-11", "grid-01"]), ("level-one", new(117, 82), ["grid-00", "grid-10", "grid-11", "grid-01"])],
        "mosaic" => [("ax-aggregate", new(117, 82), ["m-ax1", "m-ax2"]), ("ay", new(117, 198), ["m-ay"]), ("bx", new(303, 53), ["m-bx"]), ("by", new(303, 169), ["m-by"])],
        "parallel" => [("ascending-a", new(117, 198), ["parallel-a"]), ("descending-b", new(117, 82), ["parallel-b"])],
        "violin" => [("group-a-median", new(117, 140), ["profile-a"]), ("group-b-median", new(303, 140), ["profile-b"])],
        "ridgeline" => [("group-a-interior", new(210, 82), ["profile-a"]), ("group-b-interior", new(210, 198), ["profile-b"])],
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static object C5CompletionHit(EngineeringChartBase chart, string id, Point point, string[]? expected = null)
    {
        var raw = C5CompletionOriginal(chart); var hit = chart.HitTest(point);
        return new { targetId = id, input = C5Point(point), expectedAnyRawIds = expected, actualIndex = hit?.Index,
            actualKey = hit?.Mark.Key?.ToString(), actualValue = hit is null ? null : C5Finite(hit.Mark.Value),
            sourceIndices = hit?.Members.Select(member => member.SourceIndex).ToArray() ?? [], rawIds = hit?.Members.Select(member => (member.Item as C5ObservedRow)?.Id).ToArray() ?? [],
            originalSourceReference = hit is not null && hit.Members.All(member => raw.Any(item => ReferenceEquals(item, member.Item))),
            sourceIndexReference = hit is not null && hit.Members.All(member => member.SourceIndex >= 0 && member.SourceIndex < raw.Length && ReferenceEquals(raw[member.SourceIndex], member.Item)) };
    }
    private static object[] C5CompletionHits(string kind, EngineeringChartBase chart, bool baseline = false)
    {
        var targets = new List<object>();
        for (var y = 0; y < 10; y++) for (var x = 0; x < 10; x++) targets.Add(C5CompletionHit(chart, $"grid-{x}-{y}", new(21 + 42 * x, 14 + 28 * y)));
        foreach (var anchor in C5CompletionAnchors(kind)) targets.Add(C5CompletionHit(chart, anchor.Id, anchor.Point, baseline ? anchor.ExpectedAnyRawIds : null));
        return targets.ToArray();
    }
    private void AddC5CompletionProbe(string kind, string theme, string phase, EngineeringChartBase chart, object interaction, string? native = null, string? export = null, string[]? dpiImages = null)
    {
        var marks = chart.GetMarks();
        _c5CompletionProbes.Add(new
        {
            schema = "c5-completion-native/v1", kind, theme, phase,
            inputs = C5CompletionOriginal(chart).Select(C5Raw).ToArray(), plot = C5Rect(chart.PlotArea), bounds = C5Rect(chart.Bounds),
            viewport = new { chart.Zoom, chart.PanX, chart.PanY }, layout = C5Layout(chart), marks = marks.Select((mark, index) => C5Mark(chart, mark, index)).ToArray(),
            interaction, nativeImage = native, exportImage = export, dpiImages = dpiImages ?? [], physicalInputValidated = false, performanceMeasured = false
        });
    }
    private async Task RunC5CompletionSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        var directory = Path.GetDirectoryName(baseImage)!; Directory.CreateDirectory(directory); var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark; var themeName = dark ? "dark" : "light"; var background = Brush(dark ? "#111722" : "#FFFFFF");
            string Image(string kind, string phase) => Path.Combine(directory, $"{stem}-c5-completion-{themeName}-{kind}-{phase}.png");
            void Check(string key, bool passed) => checks[$"C5-completion-{themeName}-{key}"] = passed;
            foreach (var kind in C5CompletionKinds)
            {
                var chart = CreateC5CompletionChart(kind); ConfigureC5CompletionFixed(chart, dark);
                var window = new Window { Width = 420, Height = 280, CanResize = false, RequestedThemeVariant = theme, Background = background, Content = chart, Title = "C5 · " + kind };
                try
                {
                    window.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(window); chart.ResetViewport(); FlushControlTemplateWindow(window);
                    Check(kind + "-native-window", window.TryGetPlatformHandle() is { Handle: not 0 }); Check(kind + "-fixed-dip-size", chart.Bounds.Size == new Size(420, 280));
                    var raw = C5CompletionOriginal(chart); var marks = chart.GetMarks();
                    Check(kind + "-valid-scene", chart.DataError is null && marks.Count > 0);
                    Check(kind + "-original-members", marks.All(mark => mark.Members.Count > 0 && mark.Members.All(member => raw.Any(item => ReferenceEquals(item, member.Item)))));
                    Check(kind + "-source-index-members", marks.SelectMany(mark => mark.Members).All(member => member.SourceIndex >= 0 && member.SourceIndex < raw.Length && ReferenceEquals(raw[member.SourceIndex], member.Item)));
                    Check(kind + "-finite-geometry", marks.All(mark => double.IsFinite(mark.Position.X) && double.IsFinite(mark.Position.Y) && double.IsFinite(mark.Bounds.X) && double.IsFinite(mark.Bounds.Y) && double.IsFinite(mark.Bounds.Width) && double.IsFinite(mark.Bounds.Height)));
                    Check(kind + "-root-observers-attached", raw.All(row => row.Subscribers > 0));
                    foreach (var anchor in C5CompletionAnchors(kind))
                    {
                        var hit = chart.HitTest(anchor.Point);
                        Check(kind + "-fixed-hit-" + anchor.Id, hit is not null && hit.Members.Any(member => member.Item is C5ObservedRow row && anchor.ExpectedAnyRawIds.Contains(row.Id)));
                    }
                    if (chart is MosaicChart mosaic) Check(kind + "-duplicate-pair-members", mosaic.GetMarks().Any(mark => mark.Members.Count == 2 && mark.Members.All(member => member.Item is C5MosaicRow { Column: "A", Row: "X" })));
                    if (chart is ParallelCoordinatesChart parallel) Check(kind + "-missing-dimension-gap", parallel.Layout is { OmittedValues: 1 } && parallel.Layout.Records[2].Coordinates.Select(point => point.DimensionIndex).SequenceEqual(new[] { 0, 2 }));
                    var native = Image(kind, "native"); using (var bitmap = new RenderTargetBitmap(new PixelSize(420, 280), new Vector(96, 96))) { bitmap.Render(chart); bitmap.Save(native); }
                    var exported = Image(kind, "export"); Check(kind + "-export96", chart.TryExportPng(exported, new ChartExportOptions { PixelSize = new PixelSize(420, 280), Dpi = 96, Background = background }));
                    var dpiImages = new List<string>();
                    if (!dark && kind is "contour" or "violin")
                    {
                        var dpi = kind == "contour" ? 144 : 192; var path = Image(kind, "dpi" + dpi); dpiImages.Add(path);
                        Check(kind + "-dpi-export", chart.TryExportPng(path, new ChartExportOptions { PixelSize = new PixelSize(420 * dpi / 96, 280 * dpi / 96), Dpi = dpi, Background = background }));
                    }
                    var selections = new List<object>();
                    for (var i = 0; i < marks.Count; i++)
                    {
                        chart.SelectedIndex = i; FlushControlTemplateWindow(window); var selected = chart.SelectedPoint;
                        Check(kind + "-selection-source-" + i, selected is not null && selected.Members.Count == marks[i].Members.Count && selected.Members.Zip(marks[i].Members).All(pair => ReferenceEquals(pair.First.Item, pair.Second.Item)));
                        selections.Add(new { index = i, selected = selected is null ? null : C5Mark(chart, selected.Mark, selected.Index) });
                    }
                    chart.SelectedIndex = -1; FlushControlTemplateWindow(window);
                    AddC5CompletionProbe(kind, themeName, "baseline", chart, new { selections, hits = C5CompletionHits(kind, chart, true), imageState = new { selectedIndex = -1, activePoint = false } }, native, exported, dpiImages.ToArray());
                    chart.Zoom = 1.25; chart.PanX = .03; chart.PanY = -.02; FlushControlTemplateWindow(window);
                    Check(kind + "-viewport-retains-raw-source", chart.DataError is null && chart.GetMarks().SelectMany(mark => mark.Members).All(member => raw.Any(item => ReferenceEquals(item, member.Item))));
                    AddC5CompletionProbe(kind, themeName, "viewport", chart, new { hits = C5CompletionHits(kind, chart) }); chart.ResetViewport(); FlushControlTemplateWindow(window);
                    var before = JsonSerializer.Serialize(raw.Select(C5Raw).ToArray()); UpdateC5CompletionSource(chart); FlushControlTemplateWindow(window);
                    var current = C5CompletionOriginal(chart);
                    Check(kind + "-updates-observed-without-manual-refresh", chart.DataError is null && chart.GetMarks().Count > 0 && before != JsonSerializer.Serialize(current.Select(C5Raw).ToArray()));
                    Check(kind + "-updated-members-reference-current-source", chart.GetMarks().SelectMany(mark => mark.Members).All(member => current.Any(row => ReferenceEquals(row, member.Item))));
                    if (chart is DistributionProfileChartBase profile) Check(kind + "-nested-samples-observed", profile.Layout?.InputCount == 12);
                    AddC5CompletionProbe(kind, themeName, "updated", chart, new { hits = C5CompletionHits(kind, chart) });
                    chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Home }); chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Space }); FlushControlTemplateWindow(window);
                    Check(kind + "-keyboard-selects-original-source", chart.SelectedPoint is { Members.Count: > 0 } selectedPoint && selectedPoint.Members.All(member => current.Any(row => ReferenceEquals(row, member.Item))));
                    using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); using var cancelledStream = new MemoryStream(); var cancelledAsExpected = false;
                    try { _ = await chart.TryExportPngAsync(cancelledStream, cancellationToken: cancelled.Token); } catch (OperationCanceledException) { cancelledAsExpected = true; }
                    Check(kind + "-cancelled-export-is-empty", cancelledAsExpected && cancelledStream.Length == 0);
                    window.Content = null; FlushControlTemplateWindow(window); Check(kind + "-detached-root-observers-released", current.All(row => row.Subscribers == 0));
                    window.Content = chart; FlushControlTemplateWindow(window); Check(kind + "-reattached-source-recovers", chart.DataError is null && chart.GetMarks().Count > 0 && current.All(row => row.Subscribers > 0));
                }
                finally { window.Close(); (chart as IDisposable)?.Dispose(); }
            }
            var board = CreateC5CompletionBoard(true); var host = new Window { Width = 1440, Height = 800, CanResize = false, RequestedThemeVariant = theme, Background = background, Content = new Border { Padding = new Thickness(16), Child = board }, Title = "C5 · completion" };
            EngineeringChartBase[] panels = [];
            try
            {
                host.Show(_hostWindow); await Task.Delay(60); FlushControlTemplateWindow(host); panels = board.GetVisualDescendants().OfType<EngineeringChartBase>().ToArray();
                Check("board-seven-panels", panels.Length == 7 && panels.All(chart => chart.DataError is null && chart.GetMarks().Count > 0));
                var image = Image("all", "overview"); using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(host.ClientSize.Width), (int)Math.Ceiling(host.ClientSize.Height)), new Vector(96, 96))) { bitmap.Render(host); bitmap.Save(image); }
                var actions = new List<object>();
                void Invoke(string caption)
                { board.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, caption)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); FlushControlTemplateWindow(host); actions.Add(new { caption, invokedThroughButtonEvent = true, physicalInputValidated = false }); }
                Invoke("C5 缩放 / 平移"); Check("board-viewport-button", panels.All(chart => chart.Zoom == 1.25 && chart.PanX == .03 && chart.PanY == -.02));
                Invoke("C5 恢复视图"); Check("board-reset-button", panels.All(chart => chart.Zoom == 1 && chart.PanX == 0 && chart.PanY == 0));
                Invoke("C5 更新数据"); Check("board-update-button", panels.All(chart => chart.DataError is null && chart.GetMarks().Count > 0));
                Invoke("等值线填色"); Check("board-contour-fill-button", panels.OfType<ContourPlot>().Single().ShowFill == false);
                Invoke("Strip 抖动"); Check("board-strip-jitter-button", panels.OfType<StripPlot>().Single().Jitter == .25);
                Invoke("小提琴摘要"); Check("board-violin-summary-button", panels.OfType<ViolinPlot>().Single().ShowQuartiles == false);
                Invoke("山峦重叠"); Check("board-ridgeline-overlap-button", panels.OfType<RidgelineChart>().Single().Overlap == .5);
                _c5CompletionProbes.Add(new { schema = "c5-completion-native/v1", kind = "board", theme = themeName, phase = "board", image, panelKinds = C5CompletionKinds, panelCount = panels.Length, actions, physicalInputValidated = false, performanceMeasured = false });
            }
            finally { host.Close(); foreach (var chart in panels) (chart as IDisposable)?.Dispose(); }
        }
    }
}
