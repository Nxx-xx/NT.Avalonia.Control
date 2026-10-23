using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _hierarchyProbes = [];
    private readonly List<object> _hierarchyAnimationEvidence = [];
    private static HierarchyChartBase HierarchyProbeChart(string kind) => kind switch
    {
        "treemap" => new TreemapChart(), "sunburst" => new SunburstChart(), "organization" => new OrganizationChart(),
        "bubble-cloud" => new BubbleCloudChart(), "packed-bubble" => new PackedBubbleChart(), _ => new CirclePackingChart(),
    };
    private static HierarchyDemoRecord[] HierarchyProbeData(string kind) => kind switch
    {
        "bubble-cloud" or "packed-bubble" => [new("a", "A", 9), new("b", "B", 4)],
        "circle-packing" => [new("root", "Root", 999, [new("a", "A", 9), new("b", "B", 4)])],
        _ => [new("north", "North", 999, [new("a", "A", 6), new("b", "B", 3)]), new("south", "South", 222, [new("c", "C", 2), new("d", "D", 1)])],
    };
    private static object HierarchyProbeRaw(HierarchyDemoRecord row) => new { id = row.Id, label = row.Label, value = row.Value, children = row.Children.Select(HierarchyProbeRaw).ToArray() };
    private static object HierarchyProbeNode(HierarchyEntry entry) => new
    {
        index = entry.Index, parentIndex = entry.ParentIndex, sourceIndex = entry.SourceIndex, path = entry.Path, depth = entry.Depth,
        id = entry.Id, label = entry.Label, value = entry.Value, aggregateValue = entry.AggregateValue, children = entry.Children,
        source = entry.Source is HierarchyDemoRecord row ? HierarchyProbeRaw(row) : null,
    };
    private static object HierarchyProbeMark(EngineeringMarkInfo mark, int index) => new
    {
        index, key = mark.Key, value = mark.Value,
        position = new { x = mark.Position.X, y = mark.Position.Y },
        bounds = new { x = mark.Bounds.X, y = mark.Bounds.Y, width = mark.Bounds.Width, height = mark.Bounds.Height },
        members = mark.Members.Select(member => new { sourceIndex = member.SourceIndex, stableId = member.StableId,
            category = member.Category, point = new { x = member.Point.X, y = member.Point.Y }, value = member.Value, weight = member.Weight,
            item = member.Item is HierarchyDemoRecord row ? HierarchyProbeRaw(row) : null }).ToArray(),
    };
    private static object? HierarchyProbeHit(EngineeringPointHit? hit) => hit is null ? null : HierarchyProbeMark(hit.Mark, hit.Index);
    private void AddHierarchyProbe(string kind, string theme, string phase, HierarchyChartBase chart, HierarchyDemoRecord[] source, object? observations = null,
        string? nativeImage = null, string? exportImage = null, string[]? dpiImages = null)
    {
        var marks = chart.GetMarks(); var plot = chart.PlotArea;
        _hierarchyProbes.Add(new { kind, theme, phase, widthDip = chart.Bounds.Width, heightDip = chart.Bounds.Height,
            rawRoots = source.Select(HierarchyProbeRaw).ToArray(), config = new { gap = chart.Gap, maximumVisibleDepth = chart.MaximumVisibleDepth, drillRootId = chart.DrillRootId,
                drillPath = chart.DrillPath.Select(entry => entry.Id).ToArray(), collapsedIds = chart.Snapshot?.Nodes.Where(entry => entry.Children.Count > 0 && !chart.IsExpanded(entry.Id ?? entry.Source!)).Select(entry => entry.Id).ToArray(),
                zoom = chart.Zoom, panX = chart.PanX, panY = chart.PanY, animationState = chart.AnimationState.ToString(), animationProgress = chart.AnimationProgress },
            plot = new { x = plot.X, y = plot.Y, width = plot.Width, height = plot.Height },
            snapshot = chart.Snapshot?.Nodes.Select(HierarchyProbeNode).ToArray(), marks = marks.Select(HierarchyProbeMark).ToArray(), observations,
            nativeImage, exportImage, dpiImages = dpiImages ?? [], physicalInputValidated = false, performanceMeasured = false });
    }

    private async Task RunHierarchySmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var label = theme == ThemeVariant.Dark ? "dark" : "light"; var background = Brush(label == "dark" ? "#111722" : "#FFFFFF");
            string Image(string kind, string state) => Path.Combine(Path.GetDirectoryName(baseImage)!, Path.GetFileNameWithoutExtension(baseImage) + $"-hierarchy-{label}-{kind}-{state}.png");
            void Check(string key, bool value) => checks[$"HIERARCHY-{label}-{key}"] = value;
            foreach (var kind in new[] { "treemap", "sunburst", "organization", "bubble-cloud", "packed-bubble", "circle-packing" })
            {
                var source = HierarchyProbeData(kind); var chart = HierarchyProbeChart(kind);
                chart.ItemsSource = source; chart.IdPath = "Id"; chart.LabelPath = "Label"; chart.ChildrenPath = "Children"; chart.ValuePath = "Value";
                chart.Width = 420; chart.Height = 280; chart.Background = background; chart.Gap = 0;
                chart.ShowLabels = chart.ShowValues = chart.IsTooltipEnabled = false; chart.ReduceMotion = true; chart.Palette = HierarchyPalette();
                var window = new Window { Width = 420, Height = 280, CanResize = false, RequestedThemeVariant = theme, Background = background, Content = chart, Title = "Hierarchy · " + kind };
                try
                {
                    window.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(window);
                    Check(kind + "-native-window", window.TryGetPlatformHandle() is { Handle: not 0 });
                    Check(kind + "-fixed-plot", chart.PlotArea == new Rect(24, 24, 372, 232));
                    var marks = chart.GetMarks(); Check(kind + "-valid-scene", chart.DataError is null && marks.Count > 0);
                    Check(kind + "-original-source-identity", marks.All(mark => mark.Members.Count == 1 && mark.Members[0].Item is HierarchyDemoRecord row && Equals(row.Id, mark.Key) && Equals(row.Id, mark.Members[0].StableId)));
                    var nativeImage = Image(kind, "native"); using (var raster = new RenderTargetBitmap(new PixelSize(420, 280), new Vector(96, 96))) { raster.Render(chart); raster.Save(nativeImage); }
                    var exportImage = Image(kind, "export"); Check(kind + "-export96", chart.TryExportPng(exportImage, new ChartExportOptions { Dpi = 96, PixelSize = new PixelSize(420, 280), Background = background }));
                    var dpiImages = new List<string>();
                    if (label == "light" && kind is "treemap" or "circle-packing")
                    {
                        var dpi = kind == "treemap" ? 192 : 144; var path = Image(kind, "dpi" + dpi); dpiImages.Add(path);
                        Check(kind + "-dpi-export", chart.TryExportPng(path, new ChartExportOptions { Dpi = dpi, PixelSize = new PixelSize(420 * dpi / 96, 280 * dpi / 96), Background = background }));
                    }
                    var selections = new List<object>();
                    for (var i = 0; i < marks.Count; i++)
                    {
                        chart.SelectedIndex = i; FlushControlTemplateWindow(window);
                        Check(kind + "-select-original-" + i, chart.SelectedPoint?.Index == i && ReferenceEquals(chart.SelectedPoint?.Members[0].Item, marks[i].Members[0].Item));
                        selections.Add(new { index = i, selected = HierarchyProbeHit(chart.SelectedPoint) });
                    }
                    var hits = new List<object>();
                    foreach (var (id, point) in HierarchyIndependentTargets(kind))
                    {
                        var hit = chart.HitTest(point); Check(kind + "-independent-hit-" + id, Equals(hit?.Mark.Key, id));
                        hits.Add(new { expectedId = id, target = new { x = point.X, y = point.Y }, hit = HierarchyProbeHit(hit) });
                    }
                    var blank = new Point(410, 10); Check(kind + "-blank-not-data", chart.HitTest(blank) is null);
                    AddHierarchyProbe(kind, label, "baseline", chart, source, new { selections, hits, blank = new { target = new { x = blank.X, y = blank.Y }, hit = HierarchyProbeHit(chart.HitTest(blank)) } }, nativeImage, exportImage, dpiImages.ToArray());
                    chart.SelectedIndex = -1; chart.Zoom = 1.2; chart.PanX = .03; chart.PanY = -.02; FlushControlTemplateWindow(window);
                    Check(kind + "-viewport-preserves-original-nodes", chart.GetMarks().Select(mark => mark.Key).SequenceEqual(marks.Select(mark => mark.Key)));
                    Check(kind + "-viewport-position", chart.GetMarks().Zip(marks).All(pair => Math.Abs(pair.First.Position.X - (210 + (pair.Second.Position.X - 210) * 1.2 + .03 * 372)) < 1e-8));
                    AddHierarchyProbe(kind, label, "viewport", chart, source); chart.ResetViewport(); FlushControlTemplateWindow(window);
                    if (source[0].Children.Count > 0)
                    {
                        Check(kind + "-drill-down", chart.DrillDown(source[0].Id)); FlushControlTemplateWindow(window);
                        Check(kind + "-drill-path-and-source", Equals(chart.DrillRootId, source[0].Id) && chart.DrillPath.Count == 1 && chart.GetMarks().All(mark => ((HierarchyDemoRecord)mark.Members[0].Item!).Id != "south"));
                        AddHierarchyProbe(kind, label, "drill", chart, source);
                        chart.Collapse(source[0].Id); FlushControlTemplateWindow(window);
                        Check(kind + "-collapse-aggregates-branch", chart.GetMarks().Count == 1 && !chart.IsExpanded(source[0].Id)); AddHierarchyProbe(kind, label, "collapsed", chart, source);
                        chart.Expand(source[0].Id); FlushControlTemplateWindow(window); Check(kind + "-expand-restores-descendants", chart.GetMarks().Count >= 3);
                        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Back }); FlushControlTemplateWindow(window);
                        Check(kind + "-keyboard-back-to-forest", chart.DrillRootId is null);
                    }
                    var leaf = source[0].Children.Count > 0 ? source[0].Children[0] : source[0]; var old = leaf.Value; leaf.Value = old + 2; FlushControlTemplateWindow(window);
                    Check(kind + "-nested-property-update", chart.Snapshot?.Nodes.Single(entry => Equals(entry.Id, leaf.Id)).Value == old + 2); AddHierarchyProbe(kind, label, "updated", chart, source);
                    leaf.Value = -1; FlushControlTemplateWindow(window); Check(kind + "-invalid-update-clears-scene", chart.GetMarks().Count == 0 && chart.DataError is not null);
                    leaf.Value = old; FlushControlTemplateWindow(window); Check(kind + "-same-source-error-recovery", chart.GetMarks().Count == marks.Count && chart.DataError is null);
                    window.Content = null; FlushControlTemplateWindow(window); leaf.Value = old + 1; window.Content = chart; FlushControlTemplateWindow(window);
                    Check(kind + "-reattach-latest-source", chart.Snapshot?.Nodes.Single(entry => Equals(entry.Id, leaf.Id)).Value == old + 1); leaf.Value = old;
                    FlushControlTemplateWindow(window); _ = chart.GetMarks();
                    chart.ReduceMotion = false; chart.AnimationTriggers = ChartAnimationTriggers.All;
                    chart.AnimationDuration = TimeSpan.FromMilliseconds(250); chart.AnimationDelay = TimeSpan.FromMilliseconds(100);
                    chart.IsAnimationPaused = true;
                    if (chart is OrganizationChart) chart.Zoom = 1.15;
                    leaf.Value = old + 4; var paused = chart.GetMarks(); var pausedState = chart.AnimationState;
                    Check(kind + "-animation-paused", pausedState == ChartAnimationState.Paused);
                    await Task.Delay(60); FlushControlTemplateWindow(window);
                    Check(kind + "-paused-geometry-does-not-advance", chart.GetMarks().Select(mark => mark.Bounds).SequenceEqual(paused.Select(mark => mark.Bounds)));
                    chart.ResumeAnimations(); var resumedState = chart.AnimationState;
                    await Task.Delay(450); FlushControlTemplateWindow(window); var settled = chart.GetMarks();
                    Check(kind + "-animation-completes", chart.AnimationState == ChartAnimationState.Idle && chart.AnimationProgress == 1 && chart.Snapshot?.Nodes.Single(entry => Equals(entry.Id, leaf.Id)).Value == old + 4);
                    chart.IsAnimationPaused = true; leaf.Value = old + 1; _ = chart.GetMarks(); chart.CancelAnimations(); _ = chart.GetMarks();
                    Check(kind + "-cancel-settles-current-target", chart.AnimationState == ChartAnimationState.Idle && chart.AnimationProgress == 1);
                    chart.IsAnimationPaused = false; chart.ReduceMotion = true; leaf.Value = old; chart.ResetViewport(); FlushControlTemplateWindow(window); _ = chart.GetMarks();
                    Check(kind + "-reduced-motion-settles", chart.AnimationState == ChartAnimationState.Idle && chart.AnimationProgress == 1);
                    _hierarchyAnimationEvidence.Add(new { kind, theme = label, durationMs = 250, delayMs = 100, pausedWaitMs = 60, completionWaitMs = 450, animationIncludesViewport = chart is OrganizationChart,
                        pausedState = pausedState.ToString(), resumedState = resumedState.ToString(), pausedBounds = paused.Select(mark => new { x = mark.Bounds.X, y = mark.Bounds.Y, width = mark.Bounds.Width, height = mark.Bounds.Height }).ToArray(),
                        settledBounds = settled.Select(mark => new { x = mark.Bounds.X, y = mark.Bounds.Y, width = mark.Bounds.Width, height = mark.Bounds.Height }).ToArray(),
                        settledState = chart.AnimationState.ToString(), physicalInputValidated = false, performanceMeasured = false });
                }
                finally { window.Close(); }
            }
            var board = CreateHierarchyBoard(overview: true);
            var overview = new Window { Title = "Charts · 层级和打包", Width = 1440, Height = 1000, RequestedThemeVariant = theme, Background = background, Content = new Border { Padding = new Thickness(16), Child = board } };
            try
            {
                overview.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(overview);
                var charts = board.GetVisualDescendants().OfType<HierarchyChartBase>().ToArray(); foreach (var chart in charts) chart.ReduceMotion = true;
                FlushControlTemplateWindow(overview); Check("six-demo-panels", charts.Length == 6 && charts.All(chart => chart.GetMarks().Count > 0 && chart.DataError is null));
                var path = Image("board", "overview"); using var raster = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(overview.ClientSize.Width), (int)Math.Ceiling(overview.ClientSize.Height)), new Vector(96, 96)); raster.Render(overview); raster.Save(path);
                _hierarchyProbes.Add(new { kind = "board", theme = label, phase = "board", image = path, physicalInputValidated = false, performanceMeasured = false });
            }
            finally { overview.Close(); }
        }
    }

    // Fixed business-value positions, calculated independently of the product's layout output.
    private static (string Id, Point Point)[] HierarchyIndependentTargets(string kind)
    {
        Point Rect(double x, double y) => new(24 + 372 * x, 24 + 232 * y);
        Point Square(double x, double y) => new(94 + 232 * x, 24 + 232 * y);
        Point Polar(double radius, double angle) => Square(.5 + radius * Math.Cos(angle * Math.PI / 180), .5 + radius * Math.Sin(angle * Math.PI / 180));
        if (kind == "treemap") return [("a", Rect(.375, 1d / 3)), ("b", Rect(.375, 5d / 6)), ("c", Rect(.875, 1d / 3)), ("d", Rect(.875, 5d / 6))];
        if (kind == "sunburst") return [("north", Polar(.125, 45)), ("a", Polar(.375, 0)), ("b", Polar(.375, 135)), ("south", Polar(.125, 225)), ("c", Polar(.375, 210)), ("d", Polar(.375, 255))];
        if (kind == "organization") return [("north", Rect(.25, .25)), ("a", Rect(.125, .75)), ("b", Rect(.375, .75)), ("south", Rect(.75, .25)), ("c", Rect(.625, .75)), ("d", Rect(.875, .75))];
        var angle = kind == "bubble-cloud" ? 2.39996322972865332 : 0; var x = 5 * Math.Cos(angle); var y = 5 * Math.Sin(angle);
        var ox = (Math.Min(-3, x - 2) + Math.Max(3, x + 2)) / 2; var oy = (Math.Min(-3, y - 2) + Math.Max(3, y + 2)) / 2;
        var radius = Math.Max(Math.Sqrt(ox * ox + oy * oy) + 3, Math.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy)) + 2) * (kind == "circle-packing" ? 1.06 : 1);
        var targets = new List<(string, Point)> { ("a", Square(.5 - ox / (2 * radius), .5 - oy / (2 * radius))), ("b", Square(.5 + (x - ox) / (2 * radius), .5 + (y - oy) / (2 * radius))) };
        if (kind == "circle-packing") targets.Insert(0, ("root", Square(.5, .15)));
        return targets.ToArray();
    }
}
