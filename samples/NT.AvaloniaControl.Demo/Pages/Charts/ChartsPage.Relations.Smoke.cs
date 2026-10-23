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
    private async Task RunRelationsSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var label = theme == ThemeVariant.Dark ? "dark" : "light"; var background = Brush(label == "dark" ? "#111722" : "#FFFFFF");
            string Image(string kind, string state) => Path.Combine(Path.GetDirectoryName(baseImage)!, Path.GetFileNameWithoutExtension(baseImage) + $"-relations-{label}-{kind}-{state}.png");
            void Check(string key, bool value) => checks[$"RELATIONS-{label}-{key}"] = value;
            foreach (var kind in RelationKinds)
            {
                RelationDemoNode[] nodes = [new("a", "A", .25, .5, 0, width: .12, height: .08), new("b", "B", .75, .5, 1, width: .12, height: .08)];
                RelationDemoEdge[] edges = [new("ab", "a", "b", 4)]; var chart = RelationChart(kind); ConfigureRelationPaths(chart);
                if (kind is "network" or "force" or "arc") { chart.XPath = "X"; chart.YPath = "Y"; }
                chart.ItemsSource = nodes; chart.EdgesSource = edges; chart.Width = 420; chart.Height = 280;
                chart.ShowLabels = chart.ShowEdgeLabels = chart.ShowGroups = chart.IsTooltipEnabled = false; chart.Background = background;
                var window = new Window { Width = 420, Height = 280, CanResize = false, RequestedThemeVariant = theme, Background = background, Content = chart, Title = "Relations · " + kind };
                try
                {
                    window.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(window);
                    Check(kind + "-native-window", window.TryGetPlatformHandle() is { Handle: not 0 });
                    Check(kind + "-fixed-plot", chart.PlotArea == new Rect(24, 24, 372, 232));
                    var marks = chart.GetMarks(); Check(kind + "-valid-scene", chart.DataError is null && marks.Count == 3);
                    Check(kind + "-original-node-edge-domains", marks.All(mark => mark.Key is RelationGlyphKey { HasExplicitId: true } && mark.Members.Count == 1 &&
                        (mark.Members[0].Item is RelationDemoNode node && Equals(mark.Members[0].StableId, node.Id) || mark.Members[0].Item is RelationDemoEdge edge && Equals(mark.Members[0].StableId, edge.Id))));
                    var nativeImage = Image(kind, "native"); using (var raster = new RenderTargetBitmap(new PixelSize(420, 280), new Vector(96, 96))) { raster.Render(chart); raster.Save(nativeImage); }
                    var exportImage = Image(kind, "export"); Check(kind + "-export96", chart.TryExportPng(exportImage, new ChartExportOptions { Dpi = 96, PixelSize = new PixelSize(420, 280), Background = background }));
                    var dpiImages = new List<string>();
                    if (label == "light" && kind is "chord" or "sankey")
                    {
                        var dpi = kind == "chord" ? 144 : 192; var path = Image(kind, "dpi" + dpi); dpiImages.Add(path);
                        Check(kind + "-dpi-export", chart.TryExportPng(path, new ChartExportOptions { Dpi = dpi, PixelSize = new PixelSize(420 * dpi / 96, 280 * dpi / 96), Background = background }));
                    }
                    var selections = new List<object>();
                    for (var i = 0; i < marks.Count; i++)
                    {
                        chart.SelectedIndex = i; FlushControlTemplateWindow(window);
                        Check(kind + "-select-original-" + i, chart.SelectedPoint?.Index == i && ReferenceEquals(chart.SelectedPoint?.Members[0].Item, marks[i].Members[0].Item));
                        selections.Add(new { index = i, selected = RelationProbeHit(chart.SelectedPoint) });
                    }
                    var hits = new List<object>();
                    foreach (var (id, target) in RelationIndependentTargets(kind))
                    {
                        var hit = chart.HitTest(target); Check(kind + "-independent-hit-" + id, hit?.Mark.Key is RelationGlyphKey { Kind: RelationElementKind.Node } key && Equals(key.Identity, id));
                        hits.Add(new { expectedId = id, target = new { x = target.X, y = target.Y }, hit = RelationProbeHit(hit) });
                    }
                    var blank = new Point(410, 10); Check(kind + "-blank-is-not-data", chart.HitTest(blank) is null);
                    AddRelationProbe(kind, label, "baseline", chart, nodes, edges, new { selections, hits, blank = new { target = new { x = blank.X, y = blank.Y }, hit = RelationProbeHit(chart.HitTest(blank)) } }, nativeImage, exportImage, dpiImages.ToArray());
                    chart.SelectedIndex = -1; chart.Zoom = 1.2; chart.PanX = .03; chart.PanY = -.02; FlushControlTemplateWindow(window);
                    Check(kind + "-viewport-keeps-originals", chart.GetMarks().Select(mark => ((RelationGlyphKey)mark.Key!).Identity).SequenceEqual(marks.Select(mark => ((RelationGlyphKey)mark.Key!).Identity)));
                    Check(kind + "-viewport-position", chart.GetMarks().Zip(marks).All(pair => Math.Abs(pair.First.Position.X - (210 + (pair.Second.Position.X - 210) * 1.2 + .03 * 372)) < 1e-8));
                    AddRelationProbe(kind, label, "viewport", chart, nodes, edges); chart.ResetViewport(); FlushControlTemplateWindow(window);
                    edges[0].Weight = 6; FlushControlTemplateWindow(window); Check(kind + "-edge-update", chart.Snapshot?.Edges[0].Weight == 6);
                    AddRelationProbe(kind, label, "updated", chart, nodes, edges);
                    edges[0].Weight = -1; FlushControlTemplateWindow(window); Check(kind + "-bad-weight-clears-scene", chart.GetMarks().Count == 0 && chart.DataError is not null);
                    edges[0].Weight = 4; FlushControlTemplateWindow(window); Check(kind + "-source-recovers", chart.GetMarks().Count == 3 && chart.DataError is null);
                    chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Home });
                    chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Space }); FlushControlTemplateWindow(window);
                    Check(kind + "-keyboard-selects-edge", chart.SelectedEdge is { } selected && Equals(selected.Id, "ab"));
                    chart.ResetViewport(); FlushControlTemplateWindow(window);
                    await RunRelationAsyncSmokeAsync(chart, nodes, edges, window, kind, label, Check);
                }
                finally { window.Close(); chart.Dispose(); }
            }
            var board = CreateRelationsBoard(overview: true);
            var overview = new Window { Title = "Charts · 流向和关系", Width = 1440, Height = 1220, RequestedThemeVariant = theme, Background = background, Content = new Border { Padding = new Thickness(16), Child = board } };
            var charts = board.GetVisualDescendants().OfType<RelationsChartBase>().ToArray();
            try
            {
                overview.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(overview);
                charts = board.GetVisualDescendants().OfType<RelationsChartBase>().ToArray();
                Check("nine-demo-panels", charts.Length == 9 && charts.All(chart => chart.GetMarks().Count > 0 && chart.DataError is null));
                var path = Image("board", "overview"); using var raster = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(overview.ClientSize.Width), (int)Math.Ceiling(overview.ClientSize.Height)), new Vector(96, 96)); raster.Render(overview); raster.Save(path);
                _relationsProbes.Add(new { kind = "board", theme = label, phase = "board", image = path, physicalInputValidated = false, performanceMeasured = false });
            }
            finally { overview.Close(); foreach (var chart in charts) chart.Dispose(); }
        }
    }
    private async Task RunRelationAsyncSmokeAsync(RelationsChartBase chart, RelationDemoNode[] nodes, RelationDemoEdge[] edges, Window window, string kind, string theme, Action<string, bool> check)
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); var canceledByToken = false;
        try { await chart.LayoutAsync(canceled.Token); } catch (OperationCanceledException error) { canceledByToken = error.CancellationToken == canceled.Token; }
        check(kind + "-pre-canceled-layout-token", canceledByToken);
        var obsolete = chart.LayoutAsync(); edges[0].Weight = 7; var latest = chart.LayoutAsync();
        var obsoleteAccepted = await obsolete; var latestAccepted = await latest; FlushControlTemplateWindow(window);
        check(kind + "-latest-background-version-only", !obsoleteAccepted && latestAccepted && chart.Snapshot?.Edges[0].Weight == 7 && chart.LayoutState == RelationLayoutState.Completed && !chart.IsLayoutRunning);
        var completedSnapshot = chart.Snapshot is { } snapshot ? RelationSnapshot(snapshot) : null;
        var explicitCancel = chart.LayoutAsync(); chart.CancelLayout(); var canceledAccepted = await explicitCancel;
        var cancelState = chart.LayoutState; _ = chart.GetMarks(); FlushControlTemplateWindow(window);
        check(kind + "-explicit-cancel-no-publication", !canceledAccepted && !chart.IsLayoutRunning && chart.LayoutState == RelationLayoutState.Canceled);
        edges[0].Weight = 4; _ = chart.GetMarks(); var detach = chart.LayoutAsync(); window.Content = null; var detachedAccepted = await detach;
        check(kind + "-unload-cancels-pending-layout", !detachedAccepted && !chart.IsLayoutRunning);
        window.Content = chart; FlushControlTemplateWindow(window); _ = chart.GetMarks();
        check(kind + "-reattach-has-current-sources", chart.Snapshot?.Nodes.Count == nodes.Length && chart.Snapshot?.Edges[0].Weight == 4 && chart.GetMarks().Count == 3);
        _relationsAsyncEvidence.Add(new { kind, theme, preCanceledTokenObserved = canceledByToken, obsoleteAccepted, latestAccepted, completedSnapshot,
            canceledAccepted, cancelState = cancelState.ToString(), detachedAccepted, finalState = chart.LayoutState.ToString(), physicalInputValidated = false, performanceMeasured = false });
    }
}
