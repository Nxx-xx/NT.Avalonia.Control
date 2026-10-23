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
    private static void ConfigureC6Fixed(string kind, EngineeringChartBase chart, IBrush background)
    {
        chart.Width = 420; chart.Height = 280; chart.Title = null; chart.ShowGrid = false; chart.IsTooltipEnabled = false; chart.Background = background;
        chart.Foreground = Brush(background.ToString()!.Contains("111722", StringComparison.OrdinalIgnoreCase) ? "#E5E7EB" : "#1F2937");
        SetC6Labels(chart, false);
        // The word glyph and the KPI/table's principal text are data, rather than optional point labels.
        if (chart is WordCloudChart or TableChart or KpiCardChart) SetC6Labels(chart, true);
        if (chart is WordCloudChart word) word.ShowValues = false;
        if (chart is HierarchyChartBase hierarchy) { hierarchy.ShowLegend = false; hierarchy.ReduceMotion = true; hierarchy.AnimationTriggers = ChartAnimationTriggers.None; }
        if (chart is ShapeMap map) map.ShowLegend = false;
    }
    private static object[] C6GridHits(string kind, EngineeringChartBase chart)
    {
        var original = C6OriginalItems(chart); var targets = new List<(string Id, Point Point)>();
        for (var y = 5; y < 280; y += 10) for (var x = 5; x < 420; x += 10) targets.Add(($"grid-{x}-{y}", new(x, y)));
        targets.Add(("canvas-center", new(210, 140))); targets.Add(("gantt-first-mid", new(98.4, 44))); targets.Add(("gantt-second-mid", new(172.8, 84))); targets.Add(("gantt-milestone", new(321.6, 124)));
        if (kind == "dendrogram")
        {
            targets.Add(("dendrogram-root", new(42.6, 140))); targets.Add(("dendrogram-a", new(210, 105.2)));
            targets.Add(("dendrogram-c", new(377.4, 70.4))); targets.Add(("dendrogram-d", new(377.4, 140))); targets.Add(("dendrogram-b", new(377.4, 209.6)));
        }
        if (kind == "spiral") { targets.Add(("spiral-first", new(210, 110))); targets.Add(("spiral-middle", new(210, 210.5))); targets.Add(("spiral-last", new(210, 29))); }
        // Fixture-coordinate targets are fixed independently of product layouts and mark positions.
        if (kind.StartsWith("map-", StringComparison.Ordinal))
        {
            targets.Add(("map-left-region", new(65, 140))); targets.Add(("map-hole", new(152, 140))); targets.Add(("map-island", new(355, 53)));
            targets.Add(("map-marker-a", new(94, 82))); targets.Add(("map-marker-b", new(210, 82))); targets.Add(("map-marker-c", new(326, 82)));
            targets.Add(("map-line-a", new(210, 70.4))); targets.Add(("map-line-b", new(210, 209.6)));
            targets.Add(("map-vector-line", new(94, 53))); targets.Add(("map-vector-polyline", new(210, 35.6))); targets.Add(("map-vector-polygon", new(326, 58.8)));
            targets.Add(("map-vector-circle", new(94, 209.6))); targets.Add(("map-vector-arc", new(297, 221.6)));
            targets.Add(("map-pie-one", new(152 + Math.Sqrt(72), 82 - Math.Sqrt(72)))); targets.Add(("map-pie-three", new(152 - Math.Sqrt(72), 82 + Math.Sqrt(72))));
        }
        return targets.Select(target =>
        {
            var hit = chart.HitTest(target.Point);
            return (object)new { targetId = target.Id, inputX = target.Point.X, inputY = target.Point.Y, actualKey = hit?.Mark.Key?.ToString(), actualIndices = hit?.Members.Select(member => member.SourceIndex).ToArray() ?? [],
                rawIds = hit?.Members.Select(member => member.StableId?.ToString()).ToArray() ?? [], layerIndex = hit?.Mark.Key is MapGlyphKey key && chart is ShapeMap map ? map.Layers.IndexOf(key.Layer) : (int?)null,
                elementKind = (hit?.Mark.Key as MapGlyphKey)?.Kind.ToString(), sliceIndex = hit?.Mark.Key is MapGlyphKey { Identity: ValueTuple<object, int> sliceKey } ? sliceKey.Item2 : (int?)null,
                referenceIsRawSource = hit is not null && hit.Members.All(member => original.Any(item => ReferenceEquals(item, member.Item))) };
        }).ToArray();
    }
    private async Task RunC6CompletionSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        var directory = Path.GetDirectoryName(baseImage)!; Directory.CreateDirectory(directory); var stem = Path.GetFileNameWithoutExtension(baseImage);
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var themeName = theme == ThemeVariant.Dark ? "dark" : "light"; var background = Brush(themeName == "dark" ? "#111722" : "#FFFFFF");
            string Image(string kind, string phase) => Path.Combine(directory, $"{stem}-c6-{themeName}-{kind}-{phase}.png");
            void Check(string key, bool passed) => checks[$"C6-{themeName}-{key}"] = passed;
            foreach (var kind in C6Groups.SelectMany(group => group))
            {
                var chart = CreateC6Chart(kind); ConfigureC6Fixed(kind, chart, background);
                var window = new Window { Width = 420, Height = 280, CanResize = false, RequestedThemeVariant = theme, Background = background, Content = chart, Title = "C6 · " + kind };
                try
                {
                    window.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(window); chart.ResetViewport(); FlushControlTemplateWindow(window);
                    Check(kind + "-native-window", window.TryGetPlatformHandle() is { Handle: not 0 });
                    Check(kind + "-fixed-dip-size", chart.Bounds.Size == new Size(420, 280));
                    var marks = chart.GetMarks(); var original = C6OriginalItems(chart);
                    Check(kind + "-valid-scene", chart.DataError is null && marks.Count > 0);
                    Check(kind + "-original-source-members", marks.All(mark => mark.Members.Count > 0 && mark.Members.All(member => original.Any(item => ReferenceEquals(item, member.Item)))));
                    var native = Image(kind, "native"); using (var bitmap = new RenderTargetBitmap(new PixelSize(420, 280), new Vector(96, 96))) { bitmap.Render(chart); bitmap.Save(native); }
                    var exported = Image(kind, "export"); Check(kind + "-export96", chart.TryExportPng(exported, new ChartExportOptions { PixelSize = new PixelSize(420, 280), Dpi = 96, Background = background }));
                    var dpiImages = new List<string>();
                    if (themeName == "light" && kind is "gantt" or "map-vector")
                    {
                        var dpi = kind == "gantt" ? 144 : 192; var path = Image(kind, "dpi" + dpi); dpiImages.Add(path);
                        Check(kind + "-dpi-export", chart.TryExportPng(path, new ChartExportOptions { PixelSize = new PixelSize(420 * dpi / 96, 280 * dpi / 96), Dpi = dpi, Background = background }));
                    }
                    var selections = new List<object>();
                    for (var i = 0; i < marks.Count; i++)
                    {
                        chart.SelectedIndex = i; FlushControlTemplateWindow(window); var selected = chart.SelectedPoint;
                        Check(kind + "-selection-source-" + i, selected is not null && selected.Members.Count == marks[i].Members.Count && selected.Members.Zip(marks[i].Members).All(pair => ReferenceEquals(pair.First.Item, pair.Second.Item)));
                        selections.Add(new { index = i, selected = C6Hit(chart, selected) });
                    }
                    chart.SelectedIndex = -1; FlushControlTemplateWindow(window);
                    var baselineHits = C6GridHits(kind, chart);
                    AddC6Probe(kind, themeName, "baseline", chart, new { selections, hits = baselineHits, imageState = new { selectedIndex = -1, activePoint = false } }, native, exported, dpiImages.ToArray());
                    chart.Zoom = 1.25; chart.PanX = .03; chart.PanY = -.02; FlushControlTemplateWindow(window);
                    Check(kind + "-viewport-retains-original-source", chart.DataError is null && chart.GetMarks().SelectMany(mark => mark.Members).All(member => original.Any(item => ReferenceEquals(item, member.Item))));
                    AddC6Probe(kind, themeName, "viewport", chart, new { hits = C6GridHits(kind, chart) }); chart.ResetViewport(); FlushControlTemplateWindow(window);
                    UpdateC6Source(chart); FlushControlTemplateWindow(window);
                    var updatedOriginal = C6OriginalItems(chart);
                    Check(kind + "-updated-source-scene", chart.DataError is null && chart.GetMarks().Count > 0);
                    Check(kind + "-updated-members-reference-current-source", chart.GetMarks().SelectMany(mark => mark.Members).All(member => updatedOriginal.Any(item => ReferenceEquals(item, member.Item))));
                    AddC6Probe(kind, themeName, "updated", chart, new { hits = C6GridHits(kind, chart) });
                    chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Home });
                    chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Space }); FlushControlTemplateWindow(window);
                    Check(kind + "-keyboard-selects-source", chart is SeatMap { IsMultiSelectionEnabled: true } seat ? seat.SelectedItems.Count > 0 : chart.SelectedPoint is { Members.Count: > 0 });
                    if (chart is SeatMap { IsMultiSelectionEnabled: true } multi)
                    {
                        multi.ClearSeatSelection(); multi.ToggleSeat("a"); multi.ToggleSeat("b"); Check(kind + "-multiple-original-rows", multi.SelectedItems.Count == 2 && multi.SelectedItems.All(item => updatedOriginal.Any(raw => ReferenceEquals(item, raw))));
                        multi.ToggleSeat("a"); Check(kind + "-multiple-toggle", multi.SelectedItems.Count == 1); multi.ClearSeatSelection(); Check(kind + "-multiple-clear", multi.SelectedItems.Count == 0);
                    }
                }
                finally { window.Close(); (chart as IDisposable)?.Dispose(); }
            }
            for (var group = 0; group < C6Groups.Length; group++)
            {
                var board = CreateC6Board(group, true); var host = new Window { Width = 1440, Height = 900, RequestedThemeVariant = theme, Background = background, Content = new Border { Padding = new Thickness(16), Child = board }, Title = "C6 · " + C6GroupNames[group] };
                EngineeringChartBase[] charts = [];
                try
                {
                    host.Show(_hostWindow); await Task.Delay(60); FlushControlTemplateWindow(host); charts = board.GetVisualDescendants().OfType<EngineeringChartBase>().ToArray();
                    foreach (var hierarchy in charts.OfType<HierarchyChartBase>()) hierarchy.CancelAnimations(); FlushControlTemplateWindow(host);
                    Check("board-" + C6GroupNames[group] + "-panels", charts.Length == C6Groups[group].Length && charts.All(chart => chart.DataError is null && chart.GetMarks().Count > 0));
                    var path = Image(C6GroupNames[group], "overview"); using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(host.ClientSize.Width), (int)Math.Ceiling(host.ClientSize.Height)), new Vector(96, 96))) { bitmap.Render(host); bitmap.Save(path); }
                    var actions = RunC6BoardActions(group, board, charts, host, Check);
                    _c6Probes.Add(new { schema = "c6-main-native/v1", kind = "board", theme = themeName, phase = "board", group = C6GroupNames[group], image = path, panelKinds = C6Groups[group], panelCount = charts.Length, actions, physicalInputValidated = false, performanceMeasured = false });
                }
                finally { host.Close(); foreach (var chart in charts) (chart as IDisposable)?.Dispose(); }
            }
        }
    }
    private static object RunC6BoardActions(int group, Control board, EngineeringChartBase[] charts, Window window, Action<string, bool> check)
    {
        var actions = new List<object>();
        void Invoke(string caption)
        {
            var button = board.GetVisualDescendants().OfType<Button>().Single(item => Equals(item.Content, caption)); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); FlushControlTemplateWindow(window);
            actions.Add(new { caption, invokedThroughButtonEvent = true, physicalInputValidated = false });
        }
        Invoke("缩放 / 平移"); check("board-" + group + "-viewport-button", charts.All(chart => chart.Zoom == 1.4 && chart.PanX == .03)); Invoke("恢复视图"); check("board-" + group + "-reset-button", charts.All(chart => chart.Zoom == 1 && chart.PanX == 0));
        if (group == 1)
        {
            Invoke("单值指标 / 集合"); check("board-scalars", charts[0] is BulletChart { ItemsSource: null, Value: 68 } && charts[1] is KpiCardChart { ItemsSource: null, Value: 1280, Delta: 28 } && charts[5] is WaffleChart { ItemsSource: null, Value: 68 });
            Invoke("单值指标 / 集合"); check("board-source-restored", charts[0].ItemsSource is BulletPoint[] && charts[1].ItemsSource is KpiPoint[] && charts[5].ItemsSource is WafflePoint[]);
            Invoke("表格业务列 / 矩阵"); var table = (TableChart)charts[4]; check("board-table-business-columns", table.Columns is not null && table.Cells.Count == 6 && table.GetMarks().All(mark => mark.Members[0].Item is OperationalTableRow));
            actions.Add(new { mode = "table-business", cells = C6Json(table.Cells), rows = C6Rows(table.ItemsSource) }); Invoke("表格业务列 / 矩阵"); check("board-table-matrix-restored", table.Columns is null && table.Matrix?.Cells.Count == 4);
        }
        if (group == 3)
        {
            foreach (var hierarchy in charts.OfType<HierarchyChartBase>()) { hierarchy.CancelAnimations(); hierarchy.AnimationDuration = TimeSpan.FromSeconds(1); hierarchy.PauseAnimations(); }
            Invoke("折叠 A"); foreach (var hierarchy in charts.OfType<HierarchyChartBase>())
            {
                _ = hierarchy.GetMarks(); var paused = hierarchy.AnimationState; check("board-" + hierarchy.GetType().Name + "-paused-animation", paused == ChartAnimationState.Paused);
                hierarchy.ResumeAnimations(); var resumed = hierarchy.AnimationState; check("board-" + hierarchy.GetType().Name + "-resume-animation", resumed == ChartAnimationState.Running); hierarchy.CancelAnimations();
                actions.Add(new { kind = hierarchy.GetType().Name, animationPaused = paused.ToString(), animationResumed = resumed.ToString(), animationSettled = hierarchy.AnimationState.ToString(), progress = hierarchy.AnimationProgress });
            }
            check("board-hierarchy-collapse", charts.All(chart => chart.GetMarks().Count == 3)); Invoke("展开 A"); foreach (var hierarchy in charts.OfType<HierarchyChartBase>()) hierarchy.CancelAnimations(); check("board-hierarchy-expand", charts.All(chart => chart.GetMarks().Count == 5));
            Invoke("钻取 A"); foreach (var hierarchy in charts.OfType<HierarchyChartBase>()) hierarchy.CancelAnimations(); check("board-hierarchy-drill", charts.OfType<HierarchyChartBase>().All(chart => Equals(chart.DrillRootId, "A") && chart.GetMarks().Count == 3));
            Invoke("返回父级"); foreach (var hierarchy in charts.OfType<HierarchyChartBase>()) hierarchy.CancelAnimations(); check("board-hierarchy-back", charts.OfType<HierarchyChartBase>().All(chart => Equals(chart.DrillRootId, "R") && chart.GetMarks().Count == 5));
            Invoke("返回父级"); foreach (var hierarchy in charts.OfType<HierarchyChartBase>()) hierarchy.CancelAnimations(); check("board-hierarchy-back-to-forest", charts.OfType<HierarchyChartBase>().All(chart => chart.DrillRootId is null && chart.GetMarks().Count == 5));
        }
        if (group == 4)
        {
            Invoke("切换座位 A / B"); check("board-map-multiselect-button", ((SeatMap)charts[^1]).SelectedItems.Count == 2); Invoke("清空座位"); check("board-map-clear-button", ((SeatMap)charts[^1]).SelectedItems.Count == 0);
            Invoke("切换符号"); check("board-map-marker-button", ((MarkerLayer)((ShapeMap)charts[0]).Layers[1]).MarkerType == MapMarkerType.Diamond); Invoke("直线 / 曲线"); check("board-map-line-button", ((LineLayer)((ShapeMap)charts[1]).Layers[1]).IsCurved);
        }
        Invoke("更新数据"); check("board-" + group + "-update-button", charts.All(chart => chart.DataError is null && chart.GetMarks().Count > 0)); return actions;
    }
}
