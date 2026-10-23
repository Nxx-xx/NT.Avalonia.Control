using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;
#if CHARTS_THEME_CONSUMER
using NT.AvaloniaControl.Charts.Themes;
#endif

namespace Charts.Usage;

internal static class Program
{
    internal static string[] Arguments => [];

    [STAThread]
    public static int Main(string[] args)
    {
        var checks = new Dictionary<string, bool>();
        var diagnostics = new Dictionary<string, object?>();
        string? error = null;
        MainWindow? window = null;
        try
        {
            AppBuilder.Configure<ConsumerApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            window = new MainWindow();
            window.Show();
            window.FlushBindings();
            foreach (var check in window.VerifyBindings()) checks[check.Key] = check.Value;
            foreach (var check in PumpExportTask(window, window.VerifyExportBindingsAsync())) checks[check.Key] = check.Value;
            foreach (var check in PumpExportTask(window, window.VerifyIncrementalStreamingSessionAsync())) checks[check.Key] = check.Value;
            foreach (var check in PumpExportTask(window, window.VerifyMultiChartBindingsAsync())) checks[check.Key] = check.Value;
            VerifyExports(window, checks, diagnostics, Path.GetDirectoryName(Path.GetFullPath(args.Single()))!);
            var readingChart = window.ReadingChart;
            var plot = readingChart.PlotArea;
            using (var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true))
            {
                var anchor = new Point(plot.Left + plot.Width * .2, plot.Bottom - plot.Height * .37);
                var target = readingChart.TranslatePoint(anchor, window) ?? throw new InvalidOperationException("Packaged tooltip chart has no window transform.");
                readingChart.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, readingChart, pointer, window, target, 1, default, KeyModifiers.None));
                window.FlushBindings();
            }
            var templateCard = readingChart.GetVisualDescendants().OfType<Border>().SingleOrDefault(control => control.Name == "UsageTooltipCard");
            checks["packaged-compiled-tooltip-has-real-business-controls"] = templateCard is { Bounds.Width: > 50, Bounds.Height: > 30 } &&
                templateCard.DataContext is ChartTooltipContext { Hits.Count: 2 } context && ReferenceEquals(context.Primary.Item, window.Model.ReadingRecords[1]) &&
                templateCard.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageTooltipRecordId" && text.Text == "volume-b");
            checks["packaged-template-application-resource-and-axis-readings"] = templateCard?.GetVisualDescendants().OfType<Border>().Any(control => control.Name == "UsageTooltipBadge" && control.Background is ISolidColorBrush brush && brush.Color == Colors.LimeGreen) == true &&
                readingChart.AxisReadings.Count == 3 && readingChart.AxisReadings.Any(row => ReferenceEquals(row.Axis, readingChart.Axes[0]) && !row.IsHorizontal && Math.Abs(row.Value - Math.Pow(10, 1.48)) < 1e-7);
            VerifyControlTemplates(window, checks, diagnostics);
            VerifyTrackball(window, checks, diagnostics);
            VerifySelectionRange(window, checks, diagnostics);
            VerifyInputGestures(window, checks, diagnostics);
            VerifyAnnotationCompletion(window, checks, diagnostics);
            VerifyAnimations(window, checks, diagnostics);
            VerifyAccessibility(window, checks, diagnostics);
            VerifyLineArea(window, checks, diagnostics);
            VerifyBarComparison(window, checks, diagnostics);
            VerifyScatterBubble(window, checks, diagnostics);
            VerifyCircular(window, checks, diagnostics);
            VerifyPolar(window, checks, diagnostics);
            VerifyGaugeMicro(window, checks, diagnostics);
            VerifyAnalysis(window, checks, diagnostics);
            VerifyStatistics(window, checks, diagnostics);
            VerifyFinancial(window, checks, diagnostics);
            VerifyTechnicalTrends(window, checks, diagnostics);
            VerifyEngineering(window, checks, diagnostics);
            VerifyAdvancedComparison(window, checks, diagnostics);
            VerifyHierarchy(window, checks, diagnostics);
            VerifyRelations(window, checks, diagnostics);
            VerifyMaps(window, checks, diagnostics);
            VerifyC6Completion(window, checks, diagnostics);
            checks["packaged-lod-types-come-from-function-assembly"] = typeof(ChartSamplingInfo).Assembly == typeof(CartesianChart).Assembly && typeof(ChartSamplingMode).Assembly == typeof(CartesianChart).Assembly;
            checks["packaged-lod-styled-properties-have-public-metadata"] = LineSeries.SamplingModeProperty.PropertyType == typeof(ChartSamplingMode) && LineSeries.SamplingPixelWidthProperty.PropertyType == typeof(int);
            diagnostics["packagedLevelOfDetail"] = window.LevelOfDetailBindingDiagnostics;
            diagnostics["packagedEfficientHit"] = window.EfficientHitBindingDiagnostics;
            diagnostics["packagedIncrementalStreaming"] = window.IncrementalStreamingBindingDiagnostics;
            diagnostics["packagedRenderResources"] = window.RenderResourceBindingDiagnostics;
            diagnostics["packagedMultiChart"] = window.MultiChartBindingDiagnostics;
            checks["packaged-work-scheduler-types-come-from-function-assembly"] = typeof(ChartWorkScheduler).Assembly == typeof(CartesianChart).Assembly && typeof(ChartWorkBudgetInfo).Assembly == typeof(CartesianChart).Assembly;
            checks["packaged-background-styled-property-has-public-metadata"] = CartesianChart.UseBackgroundLayoutProperty.PropertyType == typeof(bool);
            checks["packaged-resource-info-comes-from-function-assembly"] = window.BoundChart.GetResourceCacheInfo().GetType().Assembly == typeof(CartesianChart).Assembly;
            checks["packaged-resource-budget-styled-property-has-public-metadata"] = CartesianChart.ResourceCacheBudgetBytesProperty.PropertyType == typeof(long);
            checks["packaged-streaming-types-come-from-function-assembly"] = typeof(ChartDataBuffer<int>).Assembly == typeof(CartesianChart).Assembly && typeof(ChartStreamingSession<int>).Assembly == typeof(CartesianChart).Assembly;
            checks["packaged-follow-styled-properties-have-public-metadata"] = CartesianChart.FollowLatestDataProperty.PropertyType == typeof(bool) && CartesianChart.FollowLatestWindowProperty.PropertyType == typeof(double);
            var coreAssembly = typeof(CartesianChart).Assembly;
            checks["packaged-assembly-copied-to-consumer-output"] = Path.GetDirectoryName(coreAssembly.Location) == Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
#if CHARTS_THEME_CONSUMER
            window.RequestedThemeVariant = ThemeVariant.Light;
            window.FlushBindings();
            var light = (window.BoundChart.Foreground as ISolidColorBrush)?.Color;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.FlushBindings();
            checks["packaged-theme-light-dark"] = light is not null && (window.BoundChart.Foreground as ISolidColorBrush)?.Color != light;
            checks["packaged-theme-resource-loaded"] = typeof(ChartsTheme).Assembly.Location.StartsWith(AppContext.BaseDirectory, StringComparison.Ordinal);
#else
            checks["function-only-no-theme-assembly"] = !Directory.EnumerateFiles(AppContext.BaseDirectory, "NT.AvaloniaControl.Charts.Themes*.dll").Any() && !coreAssembly.GetReferencedAssemblies().Any(assembly => assembly.Name == "NT.AvaloniaControl.Charts.Themes");
            checks["function-only-application-theme"] = window.BoundChart.FontSize == 14 && window.BoundChart.Foreground is ISolidColorBrush brush && brush.Color == Colors.DarkBlue;
#endif
        }
        catch (Exception exception) { error = exception.ToString(); }
        finally { window?.Close(); }
        var passed = error is null && checks.Count > 0 && checks.Values.All(value => value);
        var output = Path.GetFullPath(args.Single());
        var assemblyPath = typeof(CartesianChart).Assembly.Location;
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, passed, timestampUtc = DateTimeOffset.UtcNow,
#if CHARTS_THEME_CONSUMER
            consumerKind = "function-and-themes",
#else
            consumerKind = "function-only-application-style",
#endif
            evidenceKind = "headless-compiled-axaml-mvvm-package-contracts",
            nativeWindowValidated = false, physicalInputValidated = false, performanceMeasured = false,
            os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), runtime = Environment.Version.ToString(),
            avaloniaVersion = typeof(Application).Assembly.GetName().Version?.ToString(),
            coreAssemblyVersion = typeof(CartesianChart).Assembly.GetName().Version?.ToString(),
            coreAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))),
            checks, diagnostics, error,
        }, new JsonSerializerOptions { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
        return passed ? 0 : 1;
    }

    private static T PumpExportTask<T>(MainWindow window, Task<T> operation)
    {
        var timeout = Stopwatch.StartNew();
        while (!operation.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            // Headless SetupWithoutStarting has no application message loop.
            // Pump dispatcher continuations before observing the completed Task.
            window.FlushBindings(); Thread.Sleep(1);
        }
        if (!operation.IsCompleted) throw new TimeoutException("Packaged asynchronous export exceeded its bounded dispatcher pump.");
        window.FlushBindings(); return operation.GetAwaiter().GetResult();
    }

    private static void VerifyC6Completion(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        checks["packaged-c6-completion-twenty-six-controls"] = window.C6Charts.Length == 26 && window.C6Charts.All(c => c.GetType().Assembly == typeof(CartesianChart).Assembly);
        checks["packaged-c6-operational-styled-properties"] = BulletChart.ValueProperty.PropertyType == typeof(double?) && WaffleChart.ValueProperty.PropertyType == typeof(double?) && KpiCardChart.DeltaTypeProperty.PropertyType == typeof(OperationalDeltaType) && TableChart.ColumnsProperty.PropertyType == typeof(IEnumerable<OperationalTableColumn>);
        checks["packaged-c6-schedule-styled-properties"] = ScheduleChartBase.VisibleStartProperty.PropertyType == typeof(DateTimeOffset?) && ScheduleChartBase.StartPathProperty.PropertyType == typeof(string) && SpiralTimelineChart.PeriodProperty.PropertyType == typeof(TimeSpan?);
        checks["packaged-c6-hierarchy-map-extension-metadata"] = DendrogramChart.DistancePathProperty.PropertyType == typeof(string) && MarkerLayer.MarkerTypeProperty.PropertyType == typeof(MapMarkerType) && ShapeLayer.ColorModeProperty.PropertyType == typeof(MapColorMode) && SeatMap.SelectedItemsProperty.PropertyType == typeof(IReadOnlyList<object>);
        var bullet = ChartOperational.Bullets(new[] { new BulletPoint("raw", 25, 75, 0, 100, new double[] { 40, 70, 100 }) })[0];
        checks["packaged-c6-bullet-numeric"] = bullet.Position == .25 && bullet.TargetPosition == .75 && bullet.Boundaries.SequenceEqual(new[] { .4, .7, 1d });
        var shares = ChartOperational.Waffle(new[] { new WafflePoint("a", 1), new WafflePoint("b", 3) }, 16);
        checks["packaged-c6-waffle-exact-apportionment"] = shares.Select(x => x.Units).SequenceEqual(new[] { 4, 12 });
        var kpi = ChartOperational.Kpis(new[] { new KpiPoint("kpi", 120, 100) })[0];
        checks["packaged-c6-kpi-previous-math"] = kpi.Change == 20 && kpi.RelativeChange == .2 && !kpi.IsExplicitDelta;
        var instant = new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero); var source = new object(); var tasks = ChartSchedule.Snapshot(new[] { new ScheduleTask("task", instant, instant.AddHours(1), Source: source) });
        checks["packaged-c6-index-query-crossing-task"] = ReferenceEquals(tasks.QueryVisible(instant.AddMinutes(20), instant.AddMinutes(30)).Entries.Single().Source, source);
        var hierarchy = ChartHierarchy.Snapshot(new[] { new HierarchyNode(10, new[] { new HierarchyNode(4, id: "a"), new HierarchyNode(3, id: "b") }, id: "root") });
        checks["packaged-c6-five-additional-hierarchy-layouts"] = ChartHierarchy.Icicle(hierarchy).Rectangles.Count == 3 && ChartHierarchy.Flame(hierarchy).Rectangles.Count == 3 && ChartHierarchy.Indented(hierarchy).Rectangles.Count == 3 && ChartHierarchy.RadialTree(hierarchy).Nodes.Count == 3 && ChartHierarchy.Dendrogram(hierarchy).Nodes.Count == 3;
        var observations = ChartMaps.Markers(new[] { new MapObservation("marker", new MapCoordinate(0, 0), -2, source) }, MapProjection.Equirectangular);
        checks["packaged-c6-marker-signed-business-value"] = observations.Points.Single().Entry.Value == -2 && ReferenceEquals(observations.Points[0].Entry.Source, source);
        diagnostics["packagedC6Completion"] = new { bindings = window.C6CompletionDiagnostics, types = window.C6Charts.Select(c => c.GetType().Name).ToArray(), physicalInputValidated = false, performanceMeasured = false };
    }

    private static void VerifyMaps(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        checks["packaged-maps-types-share-function-assembly"] = new[] { typeof(ChartMaps), typeof(MapCoordinate), typeof(MapBounds), typeof(MapFeature),
            typeof(MapPolygon), typeof(MapGeometrySnapshot), typeof(MapProjectedGeometry), typeof(MapValue), typeof(MapObservation), typeof(MapHeatmapLayout),
            typeof(ShapeMap), typeof(SeatMap), typeof(ShapeLayer), typeof(BubbleLayer), typeof(HeatmapLayer), typeof(MapGlyphKey) }.All(type => type.Assembly == typeof(CartesianChart).Assembly);
        checks["packaged-maps-styled-metadata"] = ShapeMap.GeoJsonProperty.PropertyType == typeof(string) && ShapeMap.ProjectionProperty.PropertyType == typeof(MapProjection) &&
            MapLayer.ItemsSourceProperty.PropertyType == typeof(System.Collections.IEnumerable) && ShapeMap.SourceStateProperty.PropertyType == typeof(MapSourceState);
        var source = new object();
        var feature = new MapFeature("a", new[] { new MapPolygon(new MapCoordinate[] { new(-20,-10), new(20,-10), new(20,10), new(-20,10), new(-20,-10) },
            new[] { new MapCoordinate[] { new(-5,-5), new(-5,5), new(5,5), new(5,-5), new(-5,-5) } }) }, source: source);
        var snapshot = ChartMaps.Snapshot(new[] { feature }); var projected = ChartMaps.Project(snapshot, MapProjection.Equirectangular);
        checks["packaged-maps-source-geometry-and-hole"] = ReferenceEquals(snapshot.Features[0].Source, source) && projected.Features[0].Polygons[0].Holes.Count == 1 &&
            !ChartMaps.Contains(projected.Features[0].Polygons[0], ChartMaps.ProjectCoordinate(new(0, 0), MapProjection.Equirectangular));
        var join = ChartMaps.Join(snapshot, new[] { new MapValue("a", 4, source), new MapValue("unknown", 2) });
        checks["packaged-maps-join-and-missing-region"] = join.Regions[0].Value == 4 && ReferenceEquals(join.Regions[0].Data!.Source, source) && join.UnknownValues.Count == 1;
        MapObservation[] observations = [new("one", new(-10, 0), 1, source), new("two", new(10, 5), 4)];
        var bubbles = ChartMaps.Bubbles(observations, MapProjection.Equirectangular, 0, .08);
        checks["packaged-maps-area-scaling-and-identity"] = Math.Abs(bubbles.Points[0].Radius / bubbles.Points[1].Radius - .5) < 1e-10 && ReferenceEquals(bubbles.Points[0].Entry.Source, source);
        var heat = ChartMaps.Heatmap(observations, MapProjection.Equirectangular, 8, 4, 1);
        checks["packaged-maps-heat-mass-and-source-members"] = Math.Abs(heat.Cells.Sum(cell => cell.Value) - 5) < 1e-10 && heat.Cells.Any(cell => cell.Members.Any(member => ReferenceEquals(member.Source, source)));
        var mercator = ChartMaps.ProjectCoordinate(new(0, 90), MapProjection.WebMercator);
        checks["packaged-maps-projection-and-planar"] = double.IsFinite(mercator.Y) && ChartMaps.ProjectCoordinate(new(30, 200), MapProjection.Identity) == new ChartPoint(30, 200);
        diagnostics["packagedMaps"] = new { bindings = window.MapBindingDiagnostics, types = window.MapCharts.Select(chart => chart.GetType().Name).ToArray(),
            budgets = new { ChartMaps.MaximumFeatureCount, ChartMaps.MaximumVertexCount, ChartMaps.MaximumObservationCount, ChartMaps.MaximumWork, ChartMaps.MaximumRetainedHeatmapContributions },
            physicalInputValidated = false, performanceMeasured = false };
    }

    private static void VerifyRelations(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        checks["packaged-relations-types-share-function-assembly"] = new[] { typeof(ChartRelations), typeof(GraphNode), typeof(GraphEdge), typeof(GraphSnapshot),
            typeof(GraphNodeEntry), typeof(GraphEdgeEntry), typeof(RelationGraphLayout), typeof(RelationChordLayout), typeof(RelationsChartBase), typeof(RelationGlyphKey),
            typeof(SankeyChart), typeof(NetworkChart), typeof(ChordDiagramChart), typeof(FlowChart) }.All(type => type.Assembly == typeof(CartesianChart).Assembly);
        checks["packaged-relations-binding-metadata"] = RelationsChartBase.EdgesSourceProperty.PropertyType == typeof(System.Collections.IEnumerable) &&
            RelationsChartBase.StagePathProperty.PropertyType == typeof(string) && RelationsChartBase.LayoutStateProperty.PropertyType == typeof(RelationLayoutState);
        GraphNode[] nodes = [new("a", position: new ChartPoint(.25, .5), weight: 4, stage: 0), new("b", position: new ChartPoint(.75, .5), weight: 4, stage: 1)];
        GraphEdge[] edges = [new("a", "b", 4, id: "ab"), new("a", "b", 1, id: "parallel"), new("b", "a", 1, id: "return")];
        var snapshot = ChartRelations.Snapshot(nodes, edges);
        checks["packaged-relations-identity-and-parallel-snapshot"] = snapshot.Nodes.Count == 2 && snapshot.Edges.Count == 3 && ReferenceEquals(snapshot.Nodes[0].Source, nodes[0]) && ReferenceEquals(snapshot.Edges[1].Source, edges[1]) && snapshot.Edges.Select(edge => edge.SourceIndex).SequenceEqual(new[] { 0, 1, 2 });
        var sankey = ChartRelations.Sankey(snapshot); var alluvial = ChartRelations.Sankey(snapshot, mode: RelationSankeyMode.Alluvial);
        checks["packaged-relations-sankey-cycle-and-stage-layout"] = sankey.Nodes.Count == 2 && sankey.Edges.Count == 3 && sankey.Edges.All(edge => edge.Thickness > 0) && alluvial.Nodes[0].Center.X < alluvial.Nodes[1].Center.X && alluvial.Edges[2].IsBackEdge;
        var circular = ChartRelations.Network(snapshot, RelationNetworkMode.Circular); var force = ChartRelations.Network(snapshot, RelationNetworkMode.ForceDirected); var arc = ChartRelations.Network(snapshot, RelationNetworkMode.Arc);
        checks["packaged-relations-network-modes-preserve-pinned-nodes"] = new[] { circular, force, arc }.All(layout => layout.Nodes.Count == 2 && layout.Edges.Count == 3 && Math.Abs(layout.Nodes[0].Center.X - .25) < 1e-9);
        var chord = ChartRelations.Chord(snapshot);
        checks["packaged-relations-chord-endpoint-mass"] = chord.Ribbons.Count == 3 && Math.Abs(chord.Nodes.Sum(node => node.SweepAngle) + 2 * chord.PaddingDegrees - 360) < 1e-9 && chord.Ribbons.All(ribbon => Math.Abs(ribbon.SourceSweepAngle - ribbon.TargetSweepAngle) < 1e-9);
        checks["packaged-relations-flow-modes"] = new[] { RelationFlowMode.Explicit, RelationFlowMode.Process, RelationFlowMode.Mindmap }.All(mode => ChartRelations.Flow(snapshot, mode).Edges.Count == 3);
        checks["packaged-relations-node-edge-key-domains"] = !new RelationGlyphKey(RelationElementKind.Node, "same", true).Equals(new RelationGlyphKey(RelationElementKind.Edge, "same", true));
        diagnostics["packagedRelations"] = new { bindings = window.RelationBindingDiagnostics, types = window.RelationCharts.Select(chart => chart.GetType().Name).ToArray(),
            budgets = new { ChartRelations.MaximumNodeCount, ChartRelations.MaximumEdgeCount, ChartRelations.MaximumForceNodeCount, ChartRelations.MaximumForceIterations }, physicalInputValidated = false, performanceMeasured = false };
    }

    private static void VerifyHierarchy(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        checks["packaged-hierarchy-types-share-function-assembly"] = new[] { typeof(ChartHierarchy), typeof(HierarchyNode), typeof(HierarchyEntry),
            typeof(HierarchySnapshot), typeof(TreemapLayoutMode), typeof(HierarchyChartBase), typeof(TreemapChart), typeof(SunburstChart), typeof(OrganizationChart),
            typeof(BubbleCloudChart), typeof(PackedBubbleChart), typeof(CirclePackingChart) }.All(type => type.Assembly == typeof(CartesianChart).Assembly);
        checks["packaged-hierarchy-binding-metadata"] = HierarchyChartBase.ChildrenPathProperty.PropertyType == typeof(string) &&
            HierarchyChartBase.MaximumVisibleDepthProperty.PropertyType == typeof(int) && HierarchyChartBase.AnimationDurationProperty.PropertyType == typeof(TimeSpan);
        var a = new HierarchyNode(6, label: "A", id: "a"); var b = new HierarchyNode(3, label: "B", id: "b");
        var snapshot = ChartHierarchy.Snapshot([new HierarchyNode(999, [a, b], "Root", id: "root")]);
        checks["packaged-hierarchy-aggregate-and-original-path"] = snapshot.Nodes[0].AggregateValue == 9 && snapshot.Nodes[1].Path.SequenceEqual(new[] { 0, 0 }) && ReferenceEquals(snapshot.Nodes[1].Source, a);
        var treemap = ChartHierarchy.Treemap(snapshot); var sunburst = ChartHierarchy.Sunburst(snapshot); var tree = ChartHierarchy.Tree(snapshot);
        var cloud = ChartHierarchy.BubbleCloud(snapshot); var packed = ChartHierarchy.PackedBubble(snapshot); var circles = ChartHierarchy.CirclePacking(snapshot);
        checks["packaged-hierarchy-six-layouts"] = treemap.Rectangles.Count == 3 && sunburst.Sectors.Count == 3 && tree.Nodes.Count == 3 && tree.Edges.Count == 2 && cloud.Circles.Count == 2 && packed.Circles.Count == 2 && circles.Circles.Count == 3;
        checks["packaged-hierarchy-proportional-geometry"] = Math.Abs(treemap.Rectangles[1].Width * treemap.Rectangles[1].Height - 2d / 3) < 1e-9 &&
            Math.Abs(sunburst.Sectors[1].SweepAngle - 240) < 1e-9 && Math.Abs(Math.Pow(packed.Circles[0].Radius / packed.Circles[1].Radius, 2) - 2) < 1e-9;
        checks["packaged-hierarchy-drill-collapse-numeric"] = ChartHierarchy.Tree(snapshot, 0, new HashSet<int> { 0 }).Nodes.Count == 1;
        diagnostics["packagedHierarchy"] = new { bindings = window.HierarchyBindingDiagnostics, types = window.HierarchyCharts.Select(chart => chart.GetType().Name).ToArray(),
            budgets = new { ChartHierarchy.MaximumCount, ChartHierarchy.MaximumDepth, ChartHierarchy.MaximumPackingCount }, physicalInputValidated = false, performanceMeasured = false };
    }

    private static void VerifyAdvancedComparison(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        checks["packaged-advanced-comparison-types-share-function-assembly"] = new[] { typeof(ChartAdvancedComparison), typeof(ComparisonSort),
            typeof(DivergingValue), typeof(DivergingLayout), typeof(DivergingRow), typeof(PairedComparisonValue), typeof(PairedComparisonLayout), typeof(PairedComparisonRow),
            typeof(MekkoColumn), typeof(MekkoSegment), typeof(MekkoCell), typeof(MekkoLayout), typeof(VennItem), typeof(VennCircle), typeof(VennRegion), typeof(VennLayout),
            typeof(ComparisonChartBase), typeof(PairedBarChartBase), typeof(DivergingBarChart), typeof(DumbbellChart), typeof(MekkoChart), typeof(MirrorBarChart),
            typeof(PopulationPyramidChart), typeof(TornadoChart), typeof(VennDiagramChart) }.All(type => type.Assembly == typeof(CartesianChart).Assembly);
        checks["packaged-advanced-comparison-public-styled-metadata"] = ComparisonChartBase.SortProperty.PropertyType == typeof(ComparisonSort) &&
            DivergingBarChart.BaselineProperty.PropertyType == typeof(double) && DumbbellChart.LowValuePathProperty.PropertyType == typeof(string) &&
            MekkoChart.SegmentIdPathProperty.PropertyType == typeof(string) && PairedBarChartBase.CenterGapProperty.PropertyType == typeof(double) &&
            PopulationPyramidChart.AgeLabelPathProperty.PropertyType == typeof(string) && VennDiagramChart.SetCountProperty.PropertyType == typeof(int);
        var diverging = ChartAdvancedComparison.Diverging([new(-20), new(40)]);
        var paired = ChartAdvancedComparison.Paired([new(20, 40), new(40, 10)], true);
        checks["packaged-advanced-comparison-centered-origin-and-shared-magnitude"] = diverging.Zero == .5 && diverging.Rows[0].End == .25 &&
            diverging.Rows[1].End == 1 && paired.Zero == .5 && paired.Rows[0].FirstPosition == .25 && paired.Rows[0].SecondPosition == 1 && paired.Rows[1].FirstPosition == 0;
        var sorted = ChartAdvancedComparison.Paired([new(1, 5), new(4, 2), new(4, 5)], true, ComparisonSort.ValueDescending);
        checks["packaged-advanced-comparison-stable-sort-retains-original-indices"] = sorted.Rows.Select(row => row.SourceIndex).SequenceEqual(new[] { 2, 0, 1 });
        var mekko = ChartAdvancedComparison.Mekko([new(2, [new(1), new(3)]), new(6, [new(3), new(1)])]);
        checks["packaged-advanced-comparison-mekko-area-and-segment-identity"] = mekko.Cells.Count == 4 && mekko.Cells[0].Width * mekko.Cells[0].Height == 1d / 16 &&
            mekko.Cells[2].Width * mekko.Cells[2].Height == 9d / 16 && mekko.Cells[2].ColumnIndex == 1 && mekko.Cells[2].SegmentIndex == 0;
        var venn = ChartAdvancedComparison.Venn([new(1, 60), new(2, 40), new(3, 20)], 2);
        checks["packaged-advanced-comparison-venn-inclusive-and-exclusive-oracles"] = !venn.IsApproximate && Math.Abs(venn.Regions[0].Value - 40) < 1e-10 &&
            venn.Regions[2].Value == 20 && venn.Regions[0].SourceIndices.SequenceEqual(new[] { 0, 2 }) && venn.MaximumRelativeAreaError < 1e-10;
        var three = ChartAdvancedComparison.Venn([new(1, 100), new(2, 100), new(4, 100), new(3, 30), new(5, 30), new(6, 30), new(7, 10)], 3);
        checks["packaged-advanced-comparison-three-circle-error-is-disclosed"] = three.IsApproximate && three.MaximumRelativeAreaError > 0 &&
            Math.Abs(three.MaximumRelativeAreaError - three.Regions.Max(region => Math.Abs(region.Value - region.EstimatedValue)) / 100) < 1e-10 && three.TripleIntersectionErrorEstimate >= 0;
        checks["packaged-advanced-comparison-business-parent-child-and-summary-members"] =
            ReferenceEquals(window.AdvancedDumbbell.GetMarks()[1].Members[0].Item, window.Model.AdvancedPairRecords[0]) &&
            ReferenceEquals(window.AdvancedMekko.GetMarks()[1].Members[1].Item, window.Model.AdvancedMekkoRecords[0].Parts[1]) &&
            window.AdvancedVenn.GetMarks().All(mark => mark.Members.All(member => ReferenceEquals(member.Item, window.Model.AdvancedVennRecords[member.SourceIndex])));
        diagnostics["packagedAdvancedComparison"] = new { bindings = window.AdvancedComparisonBindingDiagnostics,
            diverging = diverging.Rows, paired = paired.Rows, mekkoCells = mekko.Cells, vennRegions = venn.Regions,
            threeSetMaximumError = three.MaximumRelativeAreaError, threeSetIntegrationEstimate = three.TripleIntersectionErrorEstimate,
            nativeWindowValidated = false, physicalInputValidated = false, nativePixelsValidated = false, performanceMeasured = false };
    }

    private static void VerifyEngineering(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        checks["packaged-engineering-types-share-function-assembly"] = new[] { typeof(ChartEngineering), typeof(SmithPoint), typeof(SmithQuantity),
            typeof(TernaryPoint), typeof(CarpetPoint), typeof(CarpetGridSnapshot), typeof(HexbinPoint), typeof(HexbinAggregation), typeof(HexbinResult), typeof(HexbinBin),
            typeof(WindObservation), typeof(WindRoseResult), typeof(WindRoseSector), typeof(WindRoseBin), typeof(EngineeringPath), typeof(EngineeringMarkInfo),
            typeof(EngineeringChartBase), typeof(CarpetPlot), typeof(HexbinChart), typeof(SmithChart), typeof(TernaryChart), typeof(WindRoseChart) }
            .All(type => type.Assembly == typeof(CartesianChart).Assembly);
        checks["packaged-engineering-public-styled-metadata"] = CarpetPlot.APathProperty.PropertyType == typeof(string) && CarpetPlot.OffsetProperty.PropertyType == typeof(double) &&
            HexbinChart.AggregationProperty.PropertyType == typeof(HexbinAggregation) && HexbinChart.BinRadiusProperty.PropertyType == typeof(double) &&
            SmithChart.ReferenceImpedanceProperty.PropertyType == typeof(double) && SmithChart.QuantityProperty.PropertyType == typeof(SmithQuantity) &&
            TernaryChart.CPathProperty.PropertyType == typeof(string) && WindRoseChart.AngleUnitProperty.PropertyType == typeof(ChartAngleUnit) &&
            EngineeringChartBase.ItemsSourceProperty.PropertyType == typeof(System.Collections.IEnumerable);
        var impedance = ChartEngineering.ProjectSmith(new(50, 50), 50);
        var admittance = ChartEngineering.ProjectSmith(new(.02, .02), 50, SmithQuantity.Admittance);
        var mixture = ChartEngineering.ProjectTernary(new(1e308, 1e308, 1e308));
        checks["packaged-engineering-physical-smith-and-scaled-ternary-oracles"] = Math.Abs(impedance.X - .2) < 1e-10 && Math.Abs(impedance.Y - .4) < 1e-10 &&
            Math.Abs(admittance.X + .2) < 1e-10 && Math.Abs(admittance.Y + .4) < 1e-10 && Math.Abs(mixture.X - .5) < 1e-10 && Math.Abs(mixture.Y - Math.Sqrt(3) / 6) < 1e-10;
        var hex = ChartEngineering.Hexbin([new(0, 0, 2), new(double.NaN, 0, 8), new(.1, .1, 4)], 1, aggregation: HexbinAggregation.Mean);
        var rose = ChartEngineering.WindRose([new(0, 1, 2), new(11.25, 3, 3), new(348.75, 6, 5)], [3, 6]);
        checks["packaged-engineering-aggregates-retain-original-members-and-weighted-frequency"] = hex.Count == 2 && hex.OmittedCount == 1 && hex.Bins.Count == 1 &&
            hex.Bins[0].SourceIndices.SequenceEqual(new[] { 0, 2 }) && hex.Bins[0].Value == 3 && rose.Count == 3 && rose.TotalFrequency == 10 &&
            rose.Sectors[0].Bins[2].SourceIndices.SequenceEqual(new[] { 2 }) && rose.Sectors[1].Bins[1].Frequency == 3 && Math.Abs(rose.Bins.Sum(bin => bin.Percentage) - 100) < 1e-10;
        var grid = ChartEngineering.CarpetGrid([new(0, 0, 0), new(1, 0, 2), new(0, 1, 3), new(1, 1, 6)]);
        var surface = ChartEngineering.InterpolateCarpet(grid, .5, .25);
        var inverse = ChartEngineering.UnprojectCarpet(grid, ChartEngineering.ProjectCarpet(surface));
        checks["packaged-engineering-bilinear-carpet-and-inverse-oracles"] = grid.IsInvertible && grid.Paths.Count == 4 && surface.Y == 1.875 &&
            Math.Abs(inverse.A - .5) < 1e-10 && Math.Abs(inverse.B - .25) < 1e-10;
        checks["packaged-engineering-business-mapping-survives-isolated-consumer"] =
            ReferenceEquals(window.EngineeringSmith.GetMarks()[1].Members[0].Item, window.Model.EngineeringSmithRecords[1]) &&
            window.EngineeringHexbin.GetMarks().SelectMany(mark => mark.Members).Select(member => member.SourceIndex).Order().SequenceEqual(Enumerable.Range(0, 80)) &&
            window.EngineeringWind.GetMarks().SelectMany(mark => mark.Members).All(member => member.Value is WindObservation);
        diagnostics["packagedEngineering"] = new { bindings = window.EngineeringBindingDiagnostics, impedance, admittance, mixture,
            hexMembers = hex.Bins[0].SourceIndices, windTotalFrequency = rose.TotalFrequency, carpetInverse = inverse,
            nativeWindowValidated = false, physicalInputValidated = false, nativePixelsValidated = false, performanceMeasured = false };
    }

    private static void VerifyTechnicalTrends(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        checks["packaged-technical-trends-types-share-function-assembly"] = new[] { typeof(ChartAnalytics), typeof(ChartTrendlineResult), typeof(ChartBollingerPoint),
            typeof(ChartMovingAverageType), typeof(ChartTrendlineType), typeof(ChartAnalyticalSeries), typeof(MovingAverageSeries), typeof(ChartTrendlineSeries), typeof(Trendline),
            typeof(ChartTechnicalIndicator), typeof(SMAIndicator), typeof(EMAIndicator), typeof(WMAIndicator), typeof(BollingerBandsIndicator), typeof(ChartAnalysisSample) }
            .All(type => type.Assembly == typeof(CartesianChart).Assembly);
        checks["packaged-technical-trends-public-styled-metadata"] = MovingAverageSeries.PeriodProperty.PropertyType == typeof(int) &&
            MovingAverageSeries.TypeProperty.PropertyType == typeof(ChartMovingAverageType) && ChartTrendlineSeries.TypeProperty.PropertyType == typeof(ChartTrendlineType) &&
            ChartAnalyticalSeries.SourceSeriesProperty.PropertyType == typeof(ChartSeries) && BollingerBandsIndicator.StandardDeviationMultiplierProperty.PropertyType == typeof(double);
        ChartPoint[] points = [new(1, 2), new(2, 8), new(3, 1), new(4, 9), new(5, 4)];
        var moving = ChartAnalytics.MovingAverage(points, 3, ChartMovingAverageType.Exponential);
        var bands = ChartAnalytics.BollingerBands(points, 3, 2);
        checks["packaged-technical-trends-public-average-and-band-oracles"] = moving.Count == 5 && double.IsNaN(moving[1].Y) &&
            Math.Abs(moving[4].Y - 31d / 6) < 1e-10 && Math.Abs(bands[2].Middle - 11d / 3) < 1e-10 && bands[2].Upper > bands[2].Middle && bands[2].Lower < bands[2].Middle;
        var fit = ChartAnalytics.FitTrend([new(1e12, 3), new(1e12 + 1, 5), new(1e12 + 2, 7)], sampleCount: 8);
        checks["packaged-technical-trends-public-centered-fit-and-predict"] = fit.Count == 3 && fit.Points.Count == 8 && Math.Abs(fit.Predict(1e12 + 3) - 9) < 1e-10 && fit.RSquared > .999999;
        diagnostics["packagedTechnicalTrends"] = new { bindings = window.TechnicalTrendsBindingDiagnostics, moving, bands, fitRSquared = fit.RSquared,
            physicalInputValidated = false, nativePixelsValidated = false, performanceMeasured = false };
    }

    private static void VerifyFinancial(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        checks["packaged-financial-types-share-function-assembly"] = new[] { typeof(FinancialChart), typeof(FinancialSeries),
            typeof(CandlestickSeries), typeof(OhlcSeries), typeof(HiloSeries), typeof(HeikinAshiSeries), typeof(RenkoSeries),
            typeof(KagiSeries), typeof(PointAndFigureSeries), typeof(FinancialVolumeSeries), typeof(ChartFinancialPoint),
            typeof(ChartFinancialSample), typeof(ChartFinancialTransforms) }.All(type => type.Assembly == typeof(CartesianChart).Assembly);
        checks["packaged-financial-styled-property-metadata"] = FinancialSeries.BoxSizeProperty.PropertyType == typeof(double) &&
            FinancialSeries.TransformProperty.PropertyType == typeof(ChartFinancialTransformKind) && FinancialSeries.TimePathProperty.PropertyType == typeof(string);
        var raw = window.Model.FinancialHostPoints;
        var renko = ChartFinancialTransforms.Renko(raw, 2);
        var pointFigure = ChartFinancialTransforms.PointAndFigure(raw, 2);
        checks["packaged-financial-public-transform-and-origin-contract"] = renko.Count == 6 && pointFigure.Count == 3 &&
            renko[2].Open == 12 && renko[2].Close == 10 && renko[2].SourceStartIndex == 2 && renko[2].SourceEndIndex == 4 &&
            renko[2].SourceEnd == raw[4] && pointFigure[1].BoxCount == 3;
        diagnostics["packagedFinancial"] = new { bindings = window.FinancialBindingDiagnostics, renkoCount = renko.Count,
            pointFigureCount = pointFigure.Count, physicalInputValidated = false, nativePixelsValidated = false, performanceMeasured = false };
    }

    private static void VerifyStatistics(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        // The copied, compiled AXAML and its shared verifier already exercise selection, INPC,
        // controls and all four renderers in VerifyBindings. These checks additionally prove that
        // the public statistical API and styled metadata came from the isolated NuGet package.
        checks["packaged-statistics-types-share-function-assembly"] = new[] { typeof(ChartStatistics), typeof(ChartBoxPlotSummary), typeof(ChartHistogramBin), typeof(ChartErrorInterval), typeof(BoxPlotSeries), typeof(ErrorBarSeries), typeof(HistogramSeries), typeof(DensitySeries) }.All(type => type.Assembly == typeof(CartesianChart).Assembly);
        checks["packaged-statistics-public-styled-property-metadata"] = BoxPlotSeries.SamplesPathProperty.PropertyType == typeof(string) && ErrorBarSeries.ErrorModeProperty.PropertyType == typeof(ChartErrorMode) && HistogramSeries.BinGapRatioProperty.PropertyType == typeof(double) && DensitySeries.FillUnderCurveProperty.PropertyType == typeof(bool);
        var interval = ChartStatistics.ErrorInterval(10, 2, 3);
        checks["packaged-statistics-error-interval-endpoints"] = interval.Lower == 8 && interval.Center == 10 && interval.Upper == 13;
        var distribution = ChartStatistics.HistogramByBoundaries([0, 1, 2, 3], [0, 1, 4], ChartHistogramNormalization.Density);
        checks["packaged-statistics-unequal-bin-unit-area"] = Math.Abs(distribution.Bins.Sum(bin => bin.Value * (bin.Upper - bin.Lower)) - 1) < 1e-12 && distribution.IncludedCount == 4;
        diagnostics["packagedStatistics"] = new { bindings = window.StatisticsBindingDiagnostics, interval, distribution, physicalInputValidated = false, nativePixelsValidated = false, performanceMeasured = false };
    }

    private static void VerifyAnalysis(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        var expander = window.FindControl<Expander>("AnalysisExamples")!; expander.IsExpanded = true; window.FlushBindings();
        var model = window.Model; var funnel = window.AnalysisFunnel; var heatmap = window.AnalysisHeatmap; var pyramid = window.FindControl<PyramidChart>("UsageAnalysisPyramid")!;
        var repeatChart = window.FindControl<CartesianChart>("UsageAnalysisRepeat")!; var pictorial = (PictorialBarSeries)repeatChart.Series[0];
        void Check(string name, bool value) => checks["packaged-analysis-" + name] = value;
        static bool Near(double actual, double expected) => double.IsFinite(actual) && Math.Abs(actual - expected) < 1e-8;
        Check("new-types-share-function-assembly", new[] { typeof(AnalysisSeries), typeof(AnalysisMarkInfo), typeof(AnalysisChartBase), typeof(FunnelChart), typeof(PyramidChart), typeof(HeatmapChart), typeof(PictorialBarSeries), typeof(PictorialBarRenderMode), typeof(ThemeRiverStack) }.All(type => type.Assembly == typeof(CartesianChart).Assembly));
        Check("pictorial-enum-and-custom-renderer-contract", Enum.GetValues<PictorialBarRenderMode>().SequenceEqual(new[] { PictorialBarRenderMode.Stretch, PictorialBarRenderMode.Clip, PictorialBarRenderMode.Repeat }) && pictorial.Renderer.GetType().Name == "PictorialBarRenderer");
        Check("compiled-business-sources-and-paths", ReferenceEquals(funnel.ItemsSource, model.AnalysisRecords) && ReferenceEquals(heatmap.ItemsSource, model.AnalysisCells) && ReferenceEquals(pictorial.ItemsSource, model.AnalysisSymbols) && funnel.ValuePath == "Value" && funnel.IdPath == "Id");
        var first = funnel.GetMarks()[1]; var hit = funnel.HitTest(first.LabelPosition);
        Check("real-polygon-hit-retains-business-identity", hit is { Index: 1, Point.Y: 85 } && Equals(hit.StableId, "analysis-b") && ReferenceEquals(hit.Item, model.AnalysisRecords[1]) && ReferenceEquals(hit.Series, funnel.Series));
        model.AnalysisGap = 0; window.FlushBindings();
        var polygon = (PolylineGeometry)funnel.GetMarks()[1].Geometry;
        Check("funnel-width-is-derived-from-raw-positive-value", Near((polygon.Points[1].X - polygon.Points[0].X) / funnel.PlotArea.Width, .2 + .8 * 85 / 120));
        var layers = pyramid.GetMarks();
        double Area(AnalysisMarkInfo mark)
        {
            var p = ((PolylineGeometry)mark.Geometry).Points; var origin = p[0]; var sum = 0d;
            for (var i = 0; i < p.Count; i++) { var a = p[i] - origin; var b = p[(i + 1) % p.Count] - origin; sum += a.X * b.Y - b.X * a.Y; }
            return Math.Abs(sum) / 2;
        }
        Check("pyramid-independent-positive-area-proportion", layers.Count == 4 && Near(Area(layers[1]) / layers.Sum(Area), 85d / 280));
        Check("typed-heatmap-keys-keep-distinct-cells", heatmap.RowKeys.Count == 2 && heatmap.RowKeys[0] is int and 1 && heatmap.RowKeys[1] is string and "1" && heatmap.GetMarks().Count == 5 && heatmap.GetMarks().Single(mark => mark.Data.SourceIndex == 2).Data.IsEmpty);
        var cell = heatmap.GetMarks().Single(mark => mark.Data.SourceIndex == 1); var cellHit = heatmap.HitTest(cell.LabelPosition);
        Check("heatmap-hit-retains-real-cell-source", cellHit is { Index: 1, Point.Y: 55 } && ReferenceEquals(cellHit.Item, model.AnalysisCells[1]) && Equals(cellHit.StableId, "heat-b"));
        Check("pictorial-negative-value-and-hole-geometry-are-bound", pictorial.RenderMode == PictorialBarRenderMode.Repeat && pictorial.Geometry is CombinedGeometry { GeometryCombineMode: GeometryCombineMode.Exclude } && ReferenceEquals(pictorial.Geometry, model.AnalysisSymbolGeometry) && pictorial.CreateSnapshot().Points[1].Point.Y == -55);
        var baseline = ThemeRiverStack.CreateBaseline(new IReadOnlyList<double>[] { new double[] { 20, 40 }, new double[] { 40, 20 } }, 50);
        Check("river-baseline-centers-shared-ordered-slots", baseline.SequenceEqual(new[] { new ChartPoint(0, 20), new ChartPoint(1, 20) }));
        var negativeRejected = false; var missingRejected = false; var overflowRejected = false;
        try { ThemeRiverStack.CreateBaseline(new IReadOnlyList<double>[] { new double[] { -1 } }); } catch (ArgumentException) { negativeRejected = true; }
        try { ThemeRiverStack.CreateBaseline(new IReadOnlyList<double>[] { new double[] { double.NaN } }); } catch (ArgumentException) { missingRejected = true; }
        try { ThemeRiverStack.CreateBaseline(new IReadOnlyList<double>[] { new double[] { double.MaxValue }, new double[] { double.MaxValue } }); } catch (OverflowException) { overflowRejected = true; }
        Check("river-invalid-missing-and-overflow-contracts", negativeRejected && missingRejected && overflowRejected);
        var areas = window.AnalysisRiver.Series.Cast<AreaSeries>().ToArray();
        Check("river-ui-hides-spacer-legend-and-disables-spacer-interaction", !areas[0].ShowInLegend && areas[0].Fill is ISolidColorBrush { Color.A: 0 } && window.AnalysisRiver.LegendItems.Count == 2 && !window.AnalysisRiver.IsLegendInteractive && !window.AnalysisRiver.IsTooltipEnabled && window.AnalysisRiver.SelectionMode == ChartSelectionMode.None && window.AnalysisRiver.SeriesSelectionMode == ChartSelectionMode.None && !window.AnalysisRiver.IsPointBrowsingEnabled);
        var png = VerifyCompactPngEnvelope(funnel); Check("analysis-png-header-or-explicit-backend-failure-contract", png.Passed);
        var pictorialPng = VerifyCompactPngEnvelope(repeatChart); Check("pictorial-png-header-or-explicit-backend-failure-contract", pictorialPng.Passed);
        using (var stream = new MemoryStream())
        {
            var canceled = false; try { PumpExportTask(window, funnel.TryExportPngAsync(stream, null, new CancellationToken(true))); } catch (OperationCanceledException) { canceled = true; }
            Check("analysis-async-precancel-retains-empty-stream", canceled && stream.Length == 0);
        }
        diagnostics["analysis"] = new { first = new { first.Data.SourceIndex, first.Data.StableId, value = first.Data.Point.Y }, layers = layers.Select(mark => new { mark.Data.SourceIndex, area = Area(mark), value = mark.Data.Point.Y }), baseline, analysisExport = png.Evidence, pictorialExport = pictorialPng.Evidence, nativePixelsValidated = false, physicalInputValidated = false, performanceMeasured = false };
        model.AnalysisGap = 2; expander.IsExpanded = false; window.FlushBindings();
    }

    private static void VerifyGaugeMicro(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        window.FindControl<Expander>("GaugeMicroExamples")!.IsExpanded = true; window.FlushBindings();
        var model = window.Model; var gauge = window.CircularGauge; var micro = window.MicroLine;
        void Check(string name, bool value) => checks["packaged-gauge-micro-" + name] = value;
        static bool Near(double? actual, double expected) => actual is { } number && double.IsFinite(number) && Math.Abs(number - expected) < 1e-8;
        Check("types-share-function-package", new[] { typeof(GaugeBase), typeof(GaugeRange), typeof(GaugeValue), typeof(CircularGaugeChart), typeof(GaugeChart), typeof(GradientRingChart), typeof(LinearGaugeChart), typeof(LiquidFillGaugeChart), typeof(ProgressDonutChart), typeof(GaugeLiquidShape), typeof(SparklineChart), typeof(SparklineSeries), typeof(SparklinePointInfo), typeof(SparklineType) }.All(type => type.Assembly == typeof(CartesianChart).Assembly));
        Check("enum-contracts", Enum.GetValues<SparklineType>().SequenceEqual(new[] { SparklineType.Line, SparklineType.Area, SparklineType.Bar, SparklineType.WinLoss }) && Enum.GetValues<GaugeLiquidShape>().Length == 2);
        Check("compiled-value-domain-target", gauge.Value == 68 && gauge.Minimum == 0 && gauge.Maximum == 100 && gauge.TargetValue == 80 && Near(gauge.NormalizedValue, .68));
        Check("compiled-range-ring-and-micro-business-sources", ReferenceEquals(gauge.RangesSource, model.GaugeRanges) && ReferenceEquals(window.FindControl<GradientRingChart>("UsageGradientRing")!.ValuesSource, model.GaugeRings) && ReferenceEquals(micro.ItemsSource, model.MicroRecords));
        var original = micro.GetPoints(); var second = original.Single(value => value.Data.SourceIndex == 1);
        Check("original-item-id-value-and-hit-series", ReferenceEquals(second.Data.Item, model.MicroRecords[1]) && Equals(second.Data.StableId, "micro-b") && second.Data.Point.Y == 55 && ReferenceEquals(second.Hit.Series, micro.Series));
        Check("point-hit-is-original-business-item", micro.HitTest(second.Position, 1) is { Index: 1 } hit && ReferenceEquals(hit.Item, model.MicroRecords[1]));
        model.GaugeMinimum = 20; model.GaugeMaximum = 120; model.GaugeValue = 90; model.GaugeTarget = 110; window.FlushBindings();
        Check("live-value-domain-target-bindings", gauge.Minimum == 20 && gauge.Maximum == 120 && gauge.Value == 90 && Near(gauge.NormalizedValue, .7) && gauge.NormalizedTarget is { } target && Near(target, .9));
        model.GaugeValue = 180; window.FlushBindings(); Check("out-of-domain-value-is-retained-and-display-clamped", gauge.Value == 180 && Near(gauge.NormalizedValue, 1));
        var version = micro.Series.SourceVersion; model.MicroRecords[1].Value = -35; window.FlushBindings(); var changed = micro.GetPoints().Single(value => value.Data.SourceIndex == 1);
        Check("inpc-refreshes-real-business-source", micro.Series.SourceVersion > version && changed.Data.Point.Y == -35 && ReferenceEquals(changed.Data.Item, model.MicroRecords[1]) && Equals(changed.Data.StableId, "micro-b"));
        var added = new MicroUsageRecord("packaged-micro", "H", 80); model.MicroRecords.Add(added); window.FlushBindings();
        Check("collection-add-retains-original-source-slot", micro.GetPoints().Count == 8 && ReferenceEquals(micro.GetPoints()[7].Data.Item, added) && added.SubscriberCount > 0);
        model.MicroRecords.Remove(added); window.FlushBindings(); Check("collection-remove-releases-observer", added.SubscriberCount == 0 && micro.GetPoints().Count == 7);
        model.GaugeOrientation = Orientation.Vertical; model.GaugeLiquidShape = GaugeLiquidShape.Rectangle; model.GaugeReduceMotion = true; window.FlushBindings();
        Check("orientation-shape-and-reduce-motion-bindings", window.FindControl<LinearGaugeChart>("UsageLinearGauge")!.Orientation == Orientation.Vertical && window.FindControl<LiquidFillGaugeChart>("UsageLiquidGauge") is { Shape: GaugeLiquidShape.Rectangle, ReduceMotion: true });
        var invalidSparkline = false; var invalidShape = false;
        try { micro.SparklineType = (SparklineType)999; } catch (ArgumentException) { invalidSparkline = true; }
        try { window.FindControl<LiquidFillGaugeChart>("UsageLiquidGauge")!.Shape = (GaugeLiquidShape)999; } catch (ArgumentException) { invalidShape = true; }
        Check("invalid-sparkline-type-rejected", invalidSparkline); Check("invalid-liquid-shape-rejected", invalidShape);
        var gaugePng = VerifyCompactPngEnvelope(gauge); Check("gauge-png-header-or-explicit-backend-failure-contract", gaugePng.Passed);
        var microPng = VerifyCompactPngEnvelope(micro); Check("micro-png-header-or-explicit-backend-failure-contract", microPng.Passed);
        diagnostics["packagedGaugeMicroContracts"] = new { gaugePngExport = gaugePng.Evidence, microPngExport = microPng.Evidence, bindings = window.GaugeMicroBindingDiagnostics, original = original.Select(value => new { value.Data.SourceIndex, value.Data.StableId, rawValue = value.Data.Point.Y }), changed = new { changed.Data.SourceIndex, changed.Data.StableId, rawValue = changed.Data.Point.Y }, nativeWindowValidated = false, physicalInputValidated = false, performanceMeasured = false };
        model.GaugeMinimum = 0; model.GaugeMaximum = 100; model.GaugeValue = 68; model.GaugeTarget = 80; model.GaugeOrientation = Orientation.Horizontal; model.GaugeLiquidShape = GaugeLiquidShape.Circle; model.GaugeReduceMotion = false; model.MicroRecords[1].Value = 55;
        window.FindControl<Expander>("GaugeMicroExamples")!.IsExpanded = false; window.FlushBindings();
    }

    private static (bool Passed, object Evidence) VerifyCompactPngEnvelope(ChartBase chart)
    {
        using var png = new MemoryStream();
        var eventCount = 0; bool? eventSenderMatches = null, eventOnUi = null; string? eventSenderType = null;
        Exception? failure = null, thrown = null;
        void Failed(object? sender, ChartExportFailedEventArgs args)
        {
            eventCount++; failure = args.Exception; eventSenderMatches = ReferenceEquals(sender, chart);
            eventSenderType = sender?.GetType().FullName; eventOnUi = Dispatcher.UIThread.CheckAccess();
        }
        if (chart is GaugeBase gaugeSource) gaugeSource.ExportFailed += Failed;
        else if (chart is SparklineChart microSource) microSource.ExportFailed += Failed;
        else if (chart is AnalysisChartBase analysisSource) analysisSource.ExportFailed += Failed;
        else ((CartesianChart)chart).ExportFailed += Failed;
        var exported = false;
        try
        {
            var options = new ChartExportOptions { PixelSize = new PixelSize(420, 280), Background = Brushes.White };
            exported = chart switch { GaugeBase gauge => gauge.TryExportPng(png, options), SparklineChart micro => micro.TryExportPng(png, options), AnalysisChartBase analysis => analysis.TryExportPng(png, options), _ => ((CartesianChart)chart).TryExportPng(png, options) };
        }
        catch (Exception exception) { thrown = exception; }
        finally
        {
            if (chart is GaugeBase gauge) gauge.ExportFailed -= Failed;
            else if (chart is SparklineChart micro) micro.ExportFailed -= Failed;
            else if (chart is AnalysisChartBase analysis) analysis.ExportFailed -= Failed;
            else ((CartesianChart)chart).ExportFailed -= Failed;
        }
        var bytes = png.ToArray();
        var signatureMatches = bytes.Length >= 33 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdrMatches = signatureMatches && System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(8, 4)) == 13 && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8);
        int? width = ihdrMatches ? System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) : null;
        int? height = ihdrMatches ? System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) : null;
        var pngSuccess = exported && thrown is null && eventCount == 0 && failure is null && ihdrMatches && width == 420 && height == 280;
        var explicitBackendUnsupported = !exported && thrown is null && bytes.Length == 0 && eventCount == 1 && eventSenderMatches == true && eventOnUi == true &&
            failure is NotSupportedException { Message: "The rendering backend did not produce PNG." };
        return (pngSuccess || explicitBackendUnsupported, new { chartType = chart.GetType().FullName, exported, pngSuccess, explicitBackendUnsupported, bytes = bytes.Length, signatureMatches, ihdrMatches, width, height, bytePrefixHex = Convert.ToHexString(bytes.AsSpan(0, Math.Min(bytes.Length, 33))), eventCount, eventSenderMatches, eventSenderType, eventOnUi, failureType = failure?.GetType().FullName, failure = failure?.ToString(), thrown = thrown?.ToString(), nativePixelsValidated = false, scope = "Headless package export result contract: real PNG IHDR 420x280 on success, or one owner/UI-thread NotSupported event and zero output. Native raster proof is separate." });
    }

    private static void VerifyPolar(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        window.FindControl<Expander>("PolarExamples")!.IsExpanded = true; window.FlushBindings();
        var chart = window.PolarLineChart; var line = (PolarLineSeries)chart.Series[0]; var model = window.Model;
        var initial = chart.GetPoints(); static bool Near(double actual, double expected) => double.IsFinite(actual) && Math.Abs(actual - expected) < 1e-8;
        void Check(string name, bool result) => checks["packaged-polar-" + name] = result;
        Check("types-share-function-package", new[] { typeof(PolarChart), typeof(RadarChart), typeof(PolarSeries), typeof(PolarLineSeries), typeof(PolarScatterSeries), typeof(PolarFilledSeries), typeof(RadarSeries), typeof(RadialLineSeries), typeof(PolarAreaSeries), typeof(NightingaleRoseSeries), typeof(RadialBarSeries), typeof(PolarPointInfo), typeof(PolarSectorInfo), typeof(ChartAngleUnit), typeof(PolarGridShape) }.All(type => type.Assembly == typeof(CartesianChart).Assembly));
        Check("enum-contracts", Enum.GetValues<ChartAngleUnit>().Length == 2 && Enum.GetValues<PolarGridShape>().Length == 2);
        Check("compiled-source-aliases-and-projected-source-identity", ReferenceEquals(line.ItemsSource, model.PolarRecords) && line.CategoryPath == "Angle" && line.ValuePath == "Value" && initial.Count == 4 && initial[1].Data.SourceIndex == 1 && Equals(initial[1].Data.StableId, "polar-b") && ReferenceEquals(initial[1].Data.Item, model.PolarRecords[1]));
        Check("business-color-and-raw-coordinates", initial[1].Data.Point == new ChartPoint(90, 50) && initial[1].Fill is ISolidColorBrush fill && fill.Color == Color.Parse("#19A798"));
        model.PolarUnit = ChartAngleUnit.Radians; model.PolarClockwise = false; model.PolarSweep = 270; window.FlushBindings();
        Check("live-unit-direction-and-angular-domain-bindings", chart.AngleUnit == ChartAngleUnit.Radians && !chart.Clockwise && Near(chart.SweepAngle, 270) && Near(chart.AngularAxis.Maximum!.Value, 2 * Math.PI) && Near(line.CreateSnapshot().Points[1].Point.X, Math.PI / 2));
        model.PolarUnit = ChartAngleUnit.Degrees; model.PolarClockwise = true; model.PolarSweep = 360; window.FlushBindings();
        chart.SelectPoint(chart.GetPoints().Single(value => value.Data.SourceIndex == 1).Hit); window.FlushBindings();
        Check("selected-point-is-compiled-two-way-business-binding", model.PolarSelectedPoint is { Index: 1, Point.Y: 50 } hit && ReferenceEquals(hit.Item, model.PolarRecords[1]) && Equals(hit.StableId, "polar-b"));
        var version = line.SourceVersion; model.PolarRecords[1].Value = 95; model.PolarRecords[1].Color = "#D7256A"; var changed = chart.GetPoints().Single(value => value.Data.SourceIndex == 1); window.FlushBindings();
        Check("inpc-radius-color-and-selected-value-update", line.SourceVersion > version && changed.Data.Point.Y == 95 && changed.Fill is ISolidColorBrush newFill && newFill.Color == Color.Parse("#D7256A") && chart.SelectedPoint is { Index: 1, Point.Y: 95 });
        Check("radar-host-category-union-and-source-order", window.RadarChart.GridShape == PolarGridShape.Polygon && window.RadarChart.GetPoints().Where(value => Equals(value.Data.Category, "质量")).Select(value => value.Angle).Distinct().Count() == 1);
        var areaChart = window.FindControl<PolarChart>("UsagePolarAreaChart")!; model.PolarMixedReference = true; window.FlushBindings();
        Check("mixed-radius-reference-keeps-numeric-points-and-area-sectors", areaChart.Series.OfType<PolarLineSeries>().Single().IsVisible && areaChart.GetPoints().Count == 4 && areaChart.GetSectors().Count == 4 && ReferenceEquals(areaChart.GetPoints()[1].Data.Item, model.PolarRecords[1]));
        model.PolarMixedReference = false; window.FlushBindings();
        var invalidUnit = false; var invalidGrid = false; var invalidSweep = false; var invalidHole = false; var invalidGap = false; var invalidOpacity = false; var invalidMaximum = false;
        try { chart.AngleUnit = (ChartAngleUnit)999; } catch (ArgumentException) { invalidUnit = true; }
        try { chart.GridShape = (PolarGridShape)999; } catch (ArgumentException) { invalidGrid = true; }
        try { chart.SweepAngle = 361; } catch (ArgumentException) { invalidSweep = true; }
        try { line.InnerRadius = 1; } catch (ArgumentException) { invalidHole = true; }
        try { line.SectorGap = -1; } catch (ArgumentException) { invalidGap = true; }
        try { line.FillOpacity = 2; } catch (ArgumentException) { invalidOpacity = true; }
        try { new RadarSeries().MaxValue = 0; } catch (ArgumentException) { invalidMaximum = true; }
        Check("invalid-angle-unit-rejected", invalidUnit); Check("invalid-grid-shape-rejected", invalidGrid); Check("invalid-sweep-rejected", invalidSweep);
        Check("invalid-hole-rejected", invalidHole); Check("invalid-gap-rejected", invalidGap); Check("invalid-fill-opacity-rejected", invalidOpacity); Check("invalid-radar-maximum-rejected", invalidMaximum);
        Check("series-and-axis-inherit-model-context", ReferenceEquals(line.DataContext, model) && ReferenceEquals(chart.AngularAxis.DataContext, model) && ReferenceEquals(chart.RadialAxis.DataContext, model));
        diagnostics["packagedPolarContracts"] = new { bindings = window.PolarBindingDiagnostics, original = initial.Select(value => new { value.Data.SourceIndex, value.Data.StableId, rawAngle = value.Data.Point.X, rawRadius = value.Data.Point.Y, value.Angle, value.Radius }), changed = new { changed.Data.SourceIndex, rawValue = changed.Data.Point.Y }, nativeWindowValidated = false, physicalInputValidated = false, performanceMeasured = false };
        chart.ClearSelection(); model.PolarRecords[1].Value = 50; model.PolarRecords[1].Color = "#19A798"; model.PolarSelectedPoint = null;
        window.FindControl<Expander>("PolarExamples")!.IsExpanded = false; window.FlushBindings();
    }

    private static void VerifyCircular(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        window.FindControl<Expander>("CircularExamples")!.IsExpanded = true; window.FlushBindings();
        var chart = window.CircularPieChart; var donutChart = window.CircularDonutChart; var model = window.Model;
        var pie = (PieSeries)chart.Series[0]; var donut = (DonutSeries)donutChart.Series[0];
        var initial = chart.GetSlices(); static bool Near(double actual, double expected) => double.IsFinite(actual) && Math.Abs(actual - expected) < 1e-9;
        checks["packaged-circular-types-share-function-package"] = new[] { typeof(CircularChart), typeof(CircularSeries), typeof(PieSeries), typeof(DonutSeries), typeof(SemiDonutSeries), typeof(CircularSliceInfo), typeof(CircularLabelPosition), typeof(ChartCircularNegativeValueMode), typeof(ChartCircularSmallSliceMode) }.All(type => type.Assembly == typeof(CartesianChart).Assembly);
        checks["packaged-circular-enum-contracts"] = Enum.GetValues<CircularLabelPosition>().Length == 3 && Enum.GetValues<ChartCircularNegativeValueMode>().Length == 3 && Enum.GetValues<ChartCircularSmallSliceMode>().Length == 2;
        checks["packaged-circular-compiled-source-and-projection"] = ReferenceEquals(pie.ItemsSource, model.CircularRecords) && initial.Count == 3 && Near(initial[0].Fraction, .5) && initial[1].Data.SourceIndex == 1 && Equals(initial[1].Data.StableId, "circular-b") && ReferenceEquals(initial[1].Data.Item, model.CircularRecords[1]);
        checks["packaged-circular-business-color"] = initial[1].Fill is ISolidColorBrush fill && fill.Color == Color.Parse("#19A798");
        model.CircularInnerRadius = .4; model.CircularSweep = 270; model.CircularNegativeMode = ChartCircularNegativeValueMode.Absolute; window.FlushBindings();
        checks["packaged-circular-live-radius-sweep-negative-bindings"] = Near(donut.InnerRadius, .4) && Near(donut.SweepAngle, 270) && chart.GetSlices().Single(value => value.Data.SourceIndex == 4) is { Data.Point.Y: -10 } negative && Near(negative.Fraction, 10d / 110);
        model.CircularNegativeMode = ChartCircularNegativeValueMode.Ignore; window.FlushBindings(); var selected = chart.GetSlices().Single(value => value.Data.SourceIndex == 1); chart.SelectSlice(selected); window.FlushBindings();
        checks["packaged-circular-selection-uses-original-source-identity"] = chart.SelectedPoint is { Index: 1, Point.Y: 25 } hit && ReferenceEquals(hit.Item, model.CircularRecords[1]) && Equals(hit.StableId, "circular-b");
        var version = pie.SourceVersion; model.CircularRecords[1].Value = 50; model.CircularRecords[1].Color = "#D7256A"; var changed = chart.GetSlices().Single(value => value.Data.SourceIndex == 1); window.FlushBindings();
        checks["packaged-circular-inpc-value-color-and-selection-refresh"] = pie.SourceVersion > version && Near(changed.Fraction, .4) && changed.Fill is ISolidColorBrush newFill && newFill.Color == Color.Parse("#D7256A") && chart.SelectedPoint is { Index: 1, Point.Y: 50 };
        var rejectedRadius = false; var rejectedSweep = false; var rejectedNegative = false; var rejectedTiny = false;
        try { donut.InnerRadius = 1; } catch (ArgumentException) { rejectedRadius = true; }
        try { pie.SweepAngle = 361; } catch (ArgumentException) { rejectedSweep = true; }
        try { pie.NegativeValueMode = (ChartCircularNegativeValueMode)999; } catch (ArgumentException) { rejectedNegative = true; }
        try { pie.SmallSliceMode = (ChartCircularSmallSliceMode)999; } catch (ArgumentException) { rejectedTiny = true; }
        checks["packaged-circular-invalid-hole-rejected"] = rejectedRadius;
        checks["packaged-circular-invalid-sweep-rejected"] = rejectedSweep;
        checks["packaged-circular-invalid-negative-policy-rejected"] = rejectedNegative;
        checks["packaged-circular-invalid-small-sector-policy-rejected"] = rejectedTiny;
        checks["packaged-circular-series-inherits-model-context"] = ReferenceEquals(pie.DataContext, model) && ReferenceEquals(donut.DataContext, model);
        diagnostics["packagedCircularContracts"] = new { bindings = window.CircularBindingDiagnostics, original = initial.Select(value => new { value.Data.SourceIndex, value.Data.StableId, rawValue = value.Data.Point.Y, value.Fraction }), changed = new { changed.Data.SourceIndex, rawValue = changed.Data.Point.Y, changed.Fraction }, nativeWindowValidated = false, physicalInputValidated = false, performanceMeasured = false };
        chart.ClearSelection(); model.CircularRecords[1].Value = 25; model.CircularRecords[1].Color = "#19A798"; model.CircularInnerRadius = .6; model.CircularSweep = 360;
        window.FindControl<Expander>("CircularExamples")!.IsExpanded = false; window.FlushBindings();
    }

    private static void VerifyScatterBubble(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        window.FindControl<Expander>("ScatterBubbleExamples")!.IsExpanded = true; window.FlushBindings();
        var chart = window.EncodedScatterChart; var bubbleChart = window.BubbleChart; var lineChart = window.ScatterLineChart; var model = window.Model;
        var scatter = (ScatterSeries)chart.Series[0]; var bubble = (BubbleSeries)bubbleChart.Series[0]; var line = (ScatterLineSeries)lineChart.Series[0];
        foreach (var value in new[] { chart, bubbleChart, lineChart }) _ = value.PlotArea;
        var root = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(bubbleChart)!;
        static bool Near(double? value, double expected) => value is { } number && double.IsFinite(number) && Math.Abs(number - expected) <= 1e-9;
        checks["packaged-scatter-bubble-public-types-share-core-payload"] = new[] { typeof(PointSeries), typeof(BubbleSeries), typeof(ScatterLineSeries), typeof(ChartSizeScale), typeof(ChartPointDrawOrder) }.All(type => type.Assembly == typeof(CartesianChart).Assembly);
        checks["packaged-scatter-bubble-complete-size-and-order-enums"] = Enum.GetValues<ChartSizeScale>().Length == 2 && Enum.GetValues<ChartPointDrawOrder>().Length == 3;
        checks["packaged-scatter-bubble-compiled-paths-keep-business-source"] = ReferenceEquals(scatter.ItemsSource, model.ScatterBubbleRecords) && ReferenceEquals(bubble.ItemsSource, model.ScatterBubbleRecords) && bubble.WeightPath == "Weight" && bubble.SizePath is null;
        var point = root.GetPointPeer(bubble, 1)?.CurrentPoint;
        checks["packaged-scatter-bubble-area-size-color-and-identity"] = point is { Index: 1, SizeValue: 50, Point.X: 5, Point.Y: 55 } && Near(point.MarkerDiameter, Math.Sqrt(2000)) && Equals(point.ColorValue, 50d) && ReferenceEquals(point.Item, model.ScatterBubbleRecords[1]) && Equals(point.StableId, "usage-bubble-b");
        model.ScatterBubbleSizeScale = ChartSizeScale.Diameter; model.ScatterBubbleDrawOrder = ChartPointDrawOrder.SizeDescending; window.FlushBindings(); _ = bubbleChart.PlotArea;
        checks["packaged-scatter-bubble-live-scale-and-draw-order-bindings"] = scatter.SizeScale == ChartSizeScale.Diameter && bubble.SizeScale == ChartSizeScale.Diameter && bubble.DrawOrder == ChartPointDrawOrder.SizeDescending && Near(root.GetPointPeer(bubble, 1)?.CurrentPoint?.MarkerDiameter, 40);
        var before = bubble.SourceVersion; model.ScatterBubbleRecords[1].Weight = 75; model.ScatterBubbleRecords[1].Color = 25; _ = bubbleChart.PlotArea; window.FlushBindings();
        var changed = root.GetPointPeer(bubble, 1)?.CurrentPoint;
        checks["packaged-scatter-bubble-inpc-refreshes-raw-and-derived-values"] = bubble.SourceVersion > before && changed is { SizeValue: 75 } && Equals(changed.ColorValue, 25d) && Near(changed.MarkerDiameter, 50);
        bubble.SizePath = "Color"; _ = bubbleChart.PlotArea; window.FlushBindings();
        checks["packaged-scatter-bubble-sizepath-overrides-weightpath"] = root.GetPointPeer(bubble, 1)?.CurrentPoint is { SizeValue: 25 } overrideSize && Near(overrideSize.MarkerDiameter, 30);
        bubble.SizePath = null; _ = bubbleChart.PlotArea; window.FlushBindings();
        var selected = root.GetPointPeer(bubble, 1)?.CurrentPoint; if (selected is not null) bubbleChart.SelectedPoints = new[] { selected }; window.FlushBindings();
        checks["packaged-scatter-bubble-selection-keeps-original-business-record"] = bubbleChart.SelectedPoint is { Index: 1, SizeValue: 75 } selection && ReferenceEquals(selection.Item, model.ScatterBubbleRecords[1]);
        checks["packaged-scatter-bubble-nonmonotonic-line-and-gap-contract"] = lineChart.HitTestSegment(MainWindow.ScatterBubbleProject(lineChart, 4, 50), 3) is { Start.Index: 0, End.Index: 1 } segment && Near(segment.Fraction, .5) && line.CreateSnapshot().Points[3].IsEmpty && lineChart.HitTestSegment(MainWindow.ScatterBubbleProject(lineChart, 5, 40), 3) is null;
        model.ScatterBubbleShowLine = false; window.FlushBindings();
        checks["packaged-scatter-bubble-line-toggle-stops-segment-reading"] = !line.ShowLine && lineChart.HitTestSegment(MainWindow.ScatterBubbleProject(lineChart, 4, 50), 3) is null;
        var sizeRejected = false; var scaleRejected = false; var orderRejected = false;
        try { bubble.MinimumSize = -1; } catch (ArgumentException) { sizeRejected = true; }
        try { bubble.SizeScale = (ChartSizeScale)999; } catch (ArgumentException) { scaleRejected = true; }
        try { bubble.DrawOrder = (ChartPointDrawOrder)999; } catch (ArgumentException) { orderRejected = true; }
        checks["packaged-scatter-bubble-invalid-negative-dip-size-rejected"] = sizeRejected;
        checks["packaged-scatter-bubble-invalid-size-scale-rejected"] = scaleRejected;
        checks["packaged-scatter-bubble-invalid-draw-order-rejected"] = orderRejected;
        checks["packaged-scatter-bubble-compiled-template-and-inheritance"] = chart.TooltipContentTemplate is not null && ReferenceEquals(chart.TooltipContentTemplate, bubbleChart.TooltipContentTemplate) && ReferenceEquals(scatter.DataContext, model) && ReferenceEquals(bubble.DataContext, model);
        diagnostics["packagedScatterBubbleContracts"] = new { bindings = window.ScatterBubbleBindingDiagnostics, original = point is null ? null : new { point.Point, point.SizeValue, point.ColorValue, point.MarkerDiameter }, changed = changed is null ? null : new { changed.Point, changed.SizeValue, changed.ColorValue, changed.MarkerDiameter }, nativeWindowValidated = false, physicalInputValidated = false, performanceMeasured = false };
        bubbleChart.ClearSelection(); model.ScatterBubbleRecords[1].Weight = 50; model.ScatterBubbleRecords[1].Color = 50;
        model.ScatterBubbleSizeScale = ChartSizeScale.Area; model.ScatterBubbleDrawOrder = ChartPointDrawOrder.Source; model.ScatterBubbleShowLine = true;
        window.FindControl<Expander>("ScatterBubbleExamples")!.IsExpanded = false; window.FlushBindings();
    }

    private static void VerifyBarComparison(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        window.FindControl<Expander>("BarComparisonExamples")!.IsExpanded = true; window.FlushBindings();
        var chart = window.BarComparisonChart; var model = window.Model; var first = (BarSeries)chart.Series[0]; var second = (BarSeries)chart.Series[1];
        var root = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(chart)!;
        static bool Near(double? value, double expected) => value is { } number && double.IsFinite(number) && Math.Abs(number - expected) <= 1e-9;
        _ = chart.PlotArea; window.FlushBindings();
        checks["packaged-bars-all-public-comparison-types-share-core-package"] = new[] { typeof(RangeBarSeries), typeof(WaterfallSeries), typeof(ParetoSeries), typeof(DotSeries), typeof(LollipopSeries), typeof(VarianceSeries), typeof(ComparisonSeries) }.All(type => type.Assembly == typeof(CartesianChart).Assembly);
        model.BarOrientation = Orientation.Horizontal; model.BarStackMode = ChartStackMode.Percent; window.FlushBindings(); _ = chart.PlotArea;
        var positive = root.GetPointPeer(second, 0)?.CurrentPoint;
        checks["packaged-bars-horizontal-percent-uses-display-and-source-coordinates"] = positive is { Index: 0, Point.X: 100, Point.Y: 0 } && positive.RawPoint == new ChartPoint(1, 30) && Near(positive.BaseValue, 40) && Near(positive.StackValue, 60);
        checks["packaged-bars-axis-compiled-binding-moves-category-axis"] = chart.XAxis is NumericAxis && chart.YAxis is CategoryAxis && ReferenceEquals(chart.XAxis, model.BarXAxis) && ReferenceEquals(chart.YAxis, model.BarYAxis);
        model.BarSortMode = ChartSeriesSortMode.ValueDescending; window.FlushBindings(); _ = chart.PlotArea;
        var sorted = root.GetPointPeer(first, 0)?.CurrentPoint;
        checks["packaged-bars-value-sort-keeps-business-identity"] = sorted is { Index: 0, Point.Y: 1 } && sorted.RawPoint == new ChartPoint(1, 20) && ReferenceEquals(sorted.Item, model.BarRecords[0]) && Equals(sorted.StableId, "usage-bar-a");
        if (sorted is not null) chart.SelectedPoints = new[] { sorted }; window.FlushBindings();
        checks["packaged-bars-selection-uses-real-business-row"] = chart.SelectedPoint is { Index: 0 } selected && ReferenceEquals(selected.Item, model.BarRecords[0]) && selected.RawPoint == new ChartPoint(1, 20);
        var before = first.SourceVersion; model.BarRecords[0].Value = 25; _ = chart.PlotArea; window.FlushBindings();
        checks["packaged-bars-inpc-updates-raw-business-value"] = first.SourceVersion > before && first.CreateSnapshot().Points[0].Point == new ChartPoint(1, 25) && root.GetPointPeer(first, 0)?.CurrentPoint?.RawPoint == new ChartPoint(1, 25);
        model.BarWidth = 24; model.BarLabels = true; window.FlushBindings();
        checks["packaged-bars-nullable-width-and-label-bindings-remain-live"] = first.BarWidth == 24 && second.BarWidth == 24 && first.ShowDataLabels && first.DataLabelFormatter is not null;
        var range = (RangeBarSeries)window.BarRangeChart.Series[0]; _ = window.BarRangeChart.PlotArea;
        checks["packaged-bars-range-high-low-mapping-needs-no-valuepath"] = range.ValuePath is null && range.CreateSnapshot().Points[0] is { High: 80, Low: 20 } data && ReferenceEquals(data.Item, model.BarRangeRecords[0]);
        var variance = (VarianceSeries)window.BarRangeChart.Series[3];
        model.VarianceRecords[0].Low = 90; model.VarianceRecords[0].High = 15; _ = window.BarRangeChart.PlotArea; window.FlushBindings();
        checks["packaged-bars-variance-retains-directed-high-low"] = variance.CreateSnapshot().Points[0] is { Low: 90, High: 15 };
        var waterfall = (WaterfallSeries)window.WaterfallChart.Series[0]; _ = window.WaterfallChart.PlotArea;
        var waterfallRoot = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(window.WaterfallChart)!;
        var change = waterfallRoot.GetPointPeer(waterfall, 2)?.CurrentPoint;
        checks["packaged-bars-waterfall-keeps-cumulative-and-source-values"] = change is { Index: 2, Point.Y: 45 } && change.RawPoint == new ChartPoint(3, 20) && Near(change.BaseValue, 25);
        model.WaterfallRecords[2].IsTotal = true; _ = window.WaterfallChart.PlotArea; window.FlushBindings();
        checks["packaged-bars-waterfall-totalpath-stays-observable"] = waterfallRoot.GetPointPeer(waterfall, 2)?.CurrentPoint is { Point.Y: 20 } total && Near(total.BaseValue, 0);
        var pareto = (ParetoSeries)window.ParetoChart.Series[0]; _ = window.ParetoChart.PlotArea;
        var paretoRoot = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(window.ParetoChart)!;
        checks["packaged-bars-pareto-descending-default-and-cumulative-style"] = pareto.SortMode == ChartSeriesSortMode.ValueDescending && pareto.ShowCumulativeLine && pareto.CumulativeStrokeThickness == 3 && paretoRoot.GetPointPeer(pareto, 1)?.CurrentPoint is { Index: 1, Point.X: 0, Point.Y: 50 } ranked && ranked.RawPoint == new ChartPoint(2, 50);
        checks["packaged-bars-raw-tooltip-template-is-compiled-and-shared"] = chart.TooltipContentTemplate is not null && ReferenceEquals(chart.TooltipContentTemplate, window.WaterfallChart.TooltipContentTemplate);
        var extent = chart.VisibleRange; chart.Viewport = new ChartViewport(-100, 110, -.25, 1.75); window.FlushBindings();
        checks["packaged-bars-viewport-keeps-current-source-identity"] = chart.VisibleRange != extent && root.GetPointPeer(first, 0)?.CurrentPoint is { Index: 0 } zoom && ReferenceEquals(zoom.Item, model.BarRecords[0]);
        diagnostics["packagedBarComparisonContracts"] = new { bindings = window.BarComparisonBindingDiagnostics, positive = positive is null ? null : new { positive.Point, positive.RawPoint, positive.BaseValue, positive.StackValue }, sorted = sorted is null ? null : new { sorted.Point, sorted.RawPoint, sorted.Index }, physicalInputValidated = false, performanceMeasured = false };
        chart.ClearSelection(); chart.ResetViewport(); model.BarRecords[0].Value = 20; model.VarianceRecords[0].Low = 25; model.VarianceRecords[0].High = 75; model.WaterfallRecords[2].IsTotal = false;
        model.BarOrientation = Orientation.Vertical; model.BarStackMode = ChartStackMode.Stacked; model.BarSortMode = ChartSeriesSortMode.Source; model.BarWidth = null; model.BarLabels = false;
        window.FindControl<Expander>("BarComparisonExamples")!.IsExpanded = false; window.FlushBindings();
    }

    private static void VerifyLineArea(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        window.FindControl<Expander>("LineAreaExamples")!.IsExpanded = true; window.FlushBindings();
        var chart = window.LineAreaChart; var model = window.Model; var first = (AreaSeries)chart.Series[0]; var second = (AreaSeries)chart.Series[1]; var line = (LineSeries)chart.Series[2];
        var range = (RangeAreaSeries)window.RangeAreaChart.Series[0];
        checks["packaged-line-area-new-types-loaded-from-real-package"] = typeof(AreaSeries).Assembly == typeof(CartesianChart).Assembly && typeof(RangeAreaSeries).Assembly == typeof(CartesianChart).Assembly &&
            Path.GetDirectoryName(typeof(AreaSeries).Assembly.Location) == Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        checks["packaged-line-area-complete-interpolation-and-stack-enums"] = Enum.GetValues<ChartLineInterpolation>().Length == 5 && Enum.GetValues<ChartStackMode>().Length == 3;
        checks["packaged-line-area-specialized-renderers-present"] = !ReferenceEquals(first.Renderer, line.Renderer) && !ReferenceEquals(range.Renderer, line.Renderer) && !ReferenceEquals(first.Renderer, range.Renderer);
        model.LineAreaInterpolation = ChartLineInterpolation.Linear; model.LineAreaGap = false; line.StackGroup = "usage-area"; model.LineAreaStackMode = ChartStackMode.Stacked; window.FlushBindings(); _ = chart.PlotArea;
        var root = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(chart)!;
        var point = root.GetPointPeer(line, 0)?.CurrentPoint;
        checks["packaged-line-area-mixed-type-stack-retains-source-contribution"] = point is { Point.Y: 65, BaseValue: 50, StackValue: 15 } && point.RawPoint == new ChartPoint(1, 15);
        model.LineAreaStackMode = ChartStackMode.Percent; window.FlushBindings(); _ = chart.PlotArea; point = root.GetPointPeer(line, 0)?.CurrentPoint;
        checks["packaged-line-area-mixed-percent-normalizes-all-contributions"] = point is not null && Math.Abs(point.Point.Y - 100) < 1e-9 && point.BaseValue is { } baseValue && Math.Abs(baseValue - 50d / 65 * 100) < 1e-9 &&
            point.StackValue is { } value && Math.Abs(value - 15d / 65 * 100) < 1e-9 && point.RawPoint == new ChartPoint(1, 15);
        line.StackGroup = "usage-line"; window.FlushBindings(); _ = chart.PlotArea; point = root.GetPointPeer(line, 0)?.CurrentPoint;
        checks["packaged-line-area-stack-group-change-invalidates-scene"] = point is { Point.Y: 100, BaseValue: 0, StackValue: 100 };
        model.LineAreaStackMode = ChartStackMode.None; model.LineAreaBaseline = -20; window.FlushBindings(); _ = chart.PlotArea;
        checks["packaged-line-area-baseline-binding-preserves-source-snapshot"] = first.Baseline == -20 && first.CreateSnapshot().Points[0].Point == new ChartPoint(1, 20);
        var highLow = range.CreateSnapshot().Points[1];
        checks["packaged-line-area-range-business-identity-and-optional-valuepath"] = range.ValuePath is null && highLow.High == 70 && highLow.Low == 20 && Equals(highLow.StableId, "usage-range-b") && ReferenceEquals(highLow.Item, model.LineAreaRangeRecords[1]);
        model.LineAreaRangeRecords[1].Low = double.NaN; window.FlushBindings(); _ = window.RangeAreaChart.PlotArea;
        checks["packaged-line-area-missing-low-bound-is-a-gap"] = range.CreateSnapshot().Points[1].IsEmpty;
        model.LineAreaRangeRecords[1].Low = 20; window.FlushBindings(); _ = window.RangeAreaChart.PlotArea;
        checks["packaged-line-area-restoring-low-bound-restores-point"] = !range.CreateSnapshot().Points[1].IsEmpty;
        var interpolationRejected = false; var stackRejected = false; var baselineRejected = false;
        try { first.Interpolation = (ChartLineInterpolation)999; } catch (ArgumentException) { interpolationRejected = true; }
        try { line.StackMode = (ChartStackMode)999; } catch (ArgumentException) { stackRejected = true; }
        try { first.Baseline = double.PositiveInfinity; } catch (ArgumentException) { baselineRejected = true; }
        checks["packaged-line-area-invalid-interpolation-rejected"] = interpolationRejected;
        checks["packaged-line-area-invalid-stack-mode-rejected"] = stackRejected;
        checks["packaged-line-area-non-finite-baseline-rejected"] = baselineRejected;
        checks["packaged-line-area-all-compiled-modes-retain-series-inheritance"] = ReferenceEquals(first.DataContext, model) && ReferenceEquals(range.DataContext, model) && first.ItemsSource is ChartPoint[] && range.ItemsSource is RangeAreaUsageRecord[];
        diagnostics["packagedLineAreaContracts"] = new { bindings = window.LineAreaBindingDiagnostics, mixedStackPoint = point is null ? null : new { point.Point, point.RawPoint, point.BaseValue, point.StackValue },
            range = new { highLow.High, highLow.Low, highLow.SourceIndex, highLow.StableId }, nativeWindowValidated = false, physicalInputValidated = false, performanceMeasured = false };
        model.LineAreaInterpolation = ChartLineInterpolation.Smooth; model.LineAreaStackMode = ChartStackMode.Stacked; model.LineAreaBaseline = 0; model.LineAreaGap = true;
        window.FindControl<Expander>("LineAreaExamples")!.IsExpanded = false; window.FlushBindings();
    }

    private static void VerifyAccessibility(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        var chart = window.AccessibilityChart; var line = chart.Series[0]; var model = window.Model;
        var root = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(chart)!;
        var point = root.GetPointPeer(line, 2)!; var identity = point.GetAutomationId();
        var page = root.GetPointPage(line, 0, 4);
        checks["packaged-accessibility-lazy-page-skips-missing-row"] = page.Count == 3 && page.Select(item => item.CurrentPoint?.Index).SequenceEqual(new int?[] { 0, 2, 3 });
        checks["packaged-accessibility-missing-row-has-no-point-peer"] = root.GetPointPeer(line, 1) is null;
        checks["packaged-accessibility-point-provider-original-business-identity"] = point.CurrentPoint is { Index: 2 } hit && ReferenceEquals(hit.Item, model.AccessibilityRecords[2]) && Equals(hit.StableId, "access-b");
        var readOnlyRejected = false;
        try { point.GetProvider<IValueProvider>()!.SetValue("not editable"); } catch (InvalidOperationException) { readOnlyRejected = true; }
        checks["packaged-accessibility-point-value-rejects-writes"] = readOnlyRejected;
        chart.SelectionMode = ChartSelectionMode.Multiple; chart.ClearSelection();
        point.GetProvider<ISelectionItemProvider>()!.Select(); window.FlushBindings();
        checks["packaged-accessibility-provider-selects-point"] = point.GetProvider<ISelectionItemProvider>()!.IsSelected && chart.SelectedPoint is { Index: 2 };
        point.GetProvider<ISelectionItemProvider>()!.RemoveFromSelection(); window.FlushBindings();
        checks["packaged-accessibility-provider-removes-selection"] = !point.GetProvider<ISelectionItemProvider>()!.IsSelected && chart.SelectedPoint is null;
        EventHandler<ChartSelectionChangingEventArgs> cancel = (_, args) => { if (args.CanCancel) args.Cancel = true; };
        chart.SelectionChanging += cancel;
        point.GetProvider<ISelectionItemProvider>()!.Select(); window.FlushBindings();
        checks["packaged-accessibility-provider-honors-selection-cancellation"] = !point.GetProvider<ISelectionItemProvider>()!.IsSelected && chart.SelectedPoint is null;
        chart.SelectionChanging -= cancel;
        var invoked = 0; var originalCommand = chart.PointInvokedCommand; chart.SetCurrentValue(CartesianChart.PointInvokedCommandProperty, new ActionCommand(() => invoked++));
        point.GetProvider<IInvokeProvider>()!.Invoke(); window.FlushBindings();
        checks["packaged-accessibility-provider-invokes-point-command"] = invoked == 1;
        chart.SetCurrentValue(CartesianChart.PointInvokedCommandProperty, originalCommand);
        var oldRecords = model.AccessibilityRecords;
        var replacements = new[] { new TrackballUsageRecord("access-a", "替换开始", 10, 25), new TrackballUsageRecord("access-missing", "缺失", 25, double.NaN),
            new TrackballUsageRecord("access-b", "替换检查", 45, 72), new TrackballUsageRecord("access-c", "替换结束", 80, 40) };
        line.SetCurrentValue(ChartSeries.ItemsSourceProperty, replacements); _ = chart.PlotArea; window.FlushBindings();
        checks["packaged-accessibility-stable-id-retargets-replaced-business-item"] = point.GetAutomationId() == identity && point.CurrentPoint is { Index: 2, Point.Y: 72 } replaced && ReferenceEquals(replaced.Item, replacements[2]);
        line.SetCurrentValue(ChartSeries.ItemsSourceProperty, oldRecords); _ = chart.PlotArea; window.FlushBindings();
        checks["packaged-accessibility-replaced-source-unsubscribed"] = replacements.All(record => record.SubscriberCount == 0) && oldRecords.All(record => record.SubscriberCount > 0);
        chart.IsPointBrowsingEnabled = true; chart.IsKeyboardNavigationEnabled = true; chart.SelectionMode = ChartSelectionMode.None;
        chart.NavigateToPoint(line, 0); window.FlushBindings();
        var next = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Right }; chart.RaiseEvent(next); window.FlushBindings();
        checks["packaged-accessibility-browsing-skip-missing-without-selection"] = next.Handled && chart.CurrentPoint is { Index: 2 } current && ReferenceEquals(current.Item, oldRecords[2]) && chart.SelectedPoint is null;
        model.AccessibilityContrast = ChartHighContrastMode.Light; window.FlushBindings(); var light = chart.GetSeriesBrush(line);
        model.AccessibilityContrast = ChartHighContrastMode.Dark; window.FlushBindings(); var dark = chart.GetSeriesBrush(line);
        checks["packaged-accessibility-high-contrast-palette-has-valid-series-brushes"] = light is ISolidColorBrush && dark is ISolidColorBrush && line.Stroke is null && line.Fill is null;
        line.Stroke = Brushes.HotPink; model.AccessibilityContrast = ChartHighContrastMode.Light; window.FlushBindings();
        checks["packaged-accessibility-high-contrast-keeps-explicit-series-color"] = chart.GetSeriesBrush(line) is ISolidColorBrush custom && custom.Color == Colors.HotPink;
        line.ClearValue(ChartSeries.StrokeProperty);
        _ = chart.PlotArea; window.FlushBindings();
        checks["packaged-accessibility-summary-has-multi-axis-and-original-counts"] = chart.AccessibilitySummary.TotalCount == 10 && chart.AccessibilitySummary.ValidCount == 9 && chart.AccessibilitySummary.MissingCount == 1 && chart.AccessibilitySummary.Axes.Count >= 3;
        diagnostics["packagedAccessibilityContracts"] = new { summary = chart.AccessibilitySummaryText, bindings = window.AccessibilityBindingDiagnostics,
            pointName = point.GetName(), pointValue = point.GetProvider<IValueProvider>()!.Value, pointStatus = point.GetItemStatus(), automationId = identity,
            sourceIndex = point.CurrentPoint?.Index, stableId = point.CurrentPoint?.StableId?.ToString(), cachedPointPeers = root.CachedPointPeerCount,
            nativeBridgeValidated = false, physicalInputValidated = false };
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Escape });
        chart.ClearSelection(); model.AccessibilityContrast = ChartHighContrastMode.None; chart.SelectionMode = ChartSelectionMode.Multiple; window.FlushBindings();
    }

    private static void VerifyExports(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics, string directory)
    {
        var chart = window.ExportChart; var model = window.Model;
        checks["packaged-export-transitive-managed-encoder-copied"] = File.Exists(Path.Combine(AppContext.BaseDirectory, "StbImageWriteSharp.dll")) &&
            typeof(CartesianChart).Assembly.GetReferencedAssemblies().Any(assembly => assembly.Name == "StbImageWriteSharp");
        var eventCount = 0; var eventOnUi = true; Exception? failure = null;
        void Failed(object? sender, ChartExportFailedEventArgs args) { eventCount++; eventOnUi &= Dispatcher.UIThread.CheckAccess(); failure = args.Exception; }
        chart.ExportFailed += Failed;
        using (var stream = new MemoryStream())
        {
            var operation = Task.Run(() => chart.TryExportPngAsync(stream, new ChartExportOptions { Dpi = 0 }));
            var success = PumpExportTask(window, operation);
            checks["packaged-export-background-dispatches-failure-on-ui"] = !success && eventCount == 1 && eventOnUi && failure is ArgumentOutOfRangeException && stream.Length == 0 && stream.CanWrite;
        }
        Directory.CreateDirectory(directory); var path = Path.Combine(directory, "packaged-export-cancel-preserved.png");
        var before = System.Text.Encoding.UTF8.GetBytes("package consumer cancellation preserves this existing destination"); File.WriteAllBytes(path, before);
        var eventsBefore = eventCount; var canceled = false;
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            var operation = chart.TryExportJpegAsync(path, new ChartExportOptions(), cancellation.Token);
            try { PumpExportTask(window, operation); } catch (OperationCanceledException) { canceled = true; }
        }
        var after = File.ReadAllBytes(path);
        checks["packaged-export-pre-canceled-file-preserves-original"] = canceled && eventCount == eventsBefore && before.AsSpan().SequenceEqual(after) && !Directory.EnumerateFiles(directory, ".packaged-export-cancel-preserved.png.*.tmp").Any();
        model.ExportFormat = UsageExportFormat.Jpeg; model.ExportDpi = 192; model.ExportQuality = 77; model.ExportBackground = UsageExportBackground.TransparentOrWhite;
        model.ExportInferredSize = true; window.FlushBindings(); var options = model.CreateExportOptions();
        checks["packaged-export-compiled-viewmodel-creates-request-options"] = model.ExportFormat == UsageExportFormat.Jpeg && options.PixelSize is null && options.Dpi == 192 && options.JpegQuality == 77 && options.Background is null;
        diagnostics["packagedExportContracts"] = new { bindingRuns = window.ExportBindingDiagnostics, backgroundFailureType = failure?.GetType().Name, eventOnUi,
            canceled, preservedFile = path, beforeSha256 = Convert.ToHexString(SHA256.HashData(before)), afterSha256 = Convert.ToHexString(SHA256.HashData(after)),
            scope = "Compiled option bindings, dispatcher and cancellation/result contracts. Headless success is format-envelope evidence; native image pixels are validated separately in Demo." };
        chart.ExportFailed -= Failed;
        model.ExportFormat = UsageExportFormat.Png; model.ExportDpi = 96; model.ExportQuality = 90; model.ExportBackground = UsageExportBackground.White; model.ExportInferredSize = false; window.FlushBindings();
    }

    private static void VerifyAnimations(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        window.FindControl<Expander>("AnimationExamples")!.IsExpanded = true; window.FlushBindings();
        var chart = window.AnimationChart; var model = window.Model;
        model.AnimationDuration = TimeSpan.FromSeconds(20); model.AnimationDelay = TimeSpan.Zero;
        model.AnimationPaused = true; model.AnimationEnabled = true; window.FlushBindings();
        chart.ReplayEntranceAnimation(); window.FlushBindings();
        checks["packaged-animation-compiled-paused-entrance"] = chart.AnimationState == ChartAnimationState.Paused && chart.AnimationProgress == 0;
        chart.CancelAnimations(); window.FlushBindings();
        model.AnimationUpdateCommand.Execute(null); window.FlushBindings();
        var dataPaused = chart.AnimationState == ChartAnimationState.Paused;
        model.AnimationReduceMotion = true; window.FlushBindings();
        checks["packaged-animation-data-reduced-motion-cancels"] = dataPaused && chart.AnimationState == ChartAnimationState.Idle && chart.AnimationProgress == 1;
        model.AnimationViewCommand.Execute(null); window.FlushBindings();
        checks["packaged-animation-view-model-target-and-render-range"] = chart.Viewport == model.AnimationViewport && chart.VisibleRange == model.AnimationViewport;
        model.AnimationReduceMotion = false; model.AnimationPointLimit = 1; model.AnimationUpdateCommand.Execute(null); window.FlushBindings();
        checks["packaged-animation-point-budget-shows-final-without-session"] = chart.AnimationState == ChartAnimationState.Idle && chart.AnimationPointLimit == 1;
        diagnostics["packagedAnimationContracts"] = new { chart.AnimationTriggers, chart.AnimationDuration, chart.AnimationDelay, chart.AnimationState, chart.AnimationProgress, dataPaused,
            platformFramesMeasured = false, scope = "Compiled AXAML, MVVM, pause/cancel/reduce and bounded-point policy; native RAF and raster proof live in Demo." };
        model.AnimationEnabled = false; model.AnimationPaused = false; model.AnimationPointLimit = 10_000; model.AnimationViewport = null;
        model.AnimationDuration = TimeSpan.FromMilliseconds(800); model.AnimationDelay = TimeSpan.FromMilliseconds(150); window.FlushBindings();
    }

    private static void VerifyAnnotationCompletion(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        var expander = window.FindControl<Expander>("AnnotationCompletionExamples")!; expander.IsExpanded = true;
        var chart = window.AnnotationCompletionChart; chart.ResetViewport(); window.FlushBindings();
        expander.BringIntoView(); chart.BringIntoView(); window.FlushBindings();
        var card = chart.GetVisualDescendants().OfType<Border>().Single(item => item.Name == "UsageAnnotationPlotCard");
        var context = (ChartPlotAreaContext)card.DataContext!;
        checks["packaged-annotation-only-axis-projection"] = chart.Series.All(series => series.YAxisKey != "reference") &&
            context.TryGetProjection(null, "reference", out var projection) && Math.Abs(projection.MapY(1500) - chart.PlotArea.Height / 2) < 1e-8;
        checks["packaged-rounded-label-and-center-radius-api"] = chart.Annotations.OfType<RectangleAnnotation>().Single().CornerRadius == 12 &&
            chart.Annotations.OfType<EllipseAnnotation>().Single().GeometryMode == ChartEllipseGeometryMode.CenterRadius;
        var before = chart.VisibleRange!.Value;
        var position = chart.TranslatePoint(chart.PlotArea.Center, window)!.Value;
        window.MouseWheel(position, new Vector(0, 1)); window.FlushBindings();
        var after = chart.VisibleRange!.Value;
        var updated = chart.GetVisualDescendants().OfType<Border>().Single(item => item.Name == "UsageAnnotationPlotCard");
        var current = (ChartPlotAreaContext)updated.DataContext!;
        checks["packaged-plot-overlay-passive-wheel-updates-named-range"] = ReferenceEquals(card, updated) &&
            Math.Abs(after.XMaximum - after.XMinimum - (before.XMaximum - before.XMinimum) / 1.2) < 1e-8 &&
            current.TryGetVisibleRange(null, "reference", out var extra) && Math.Abs(extra.YMinimum - (1000 + after.YMinimum * 10)) < 1e-8;
        using var stream = new MemoryStream();
        Exception? exportFailure = null;
        void Failed(object? sender, ChartExportFailedEventArgs args) => exportFailure = args.Exception;
        chart.ExportFailed += Failed;
        bool exported;
        try { exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(920, 400), Dpi = 192 }); }
        finally { chart.ExportFailed -= Failed; }
        checks["packaged-annotation-export-result-or-backend-failure-contract"] = exported ? stream.Length > 1000 && exportFailure is null :
            exportFailure is NotSupportedException && stream.Length == 0;
        diagnostics["packagedAnnotationCompletion"] = new { before, after, plot = chart.PlotArea, card.Bounds, current.VisibleRange, exportBytes = stream.Length,
            exported, exportFailure = exportFailure?.ToString(), bindingExportBackendFailure = window.AnnotationExportBackendDiagnostic,
            inputScope = "Headless raw window wheel through passive plot content; PNG backend success or explicit NotSupported failure; native raster proof is separate" };
    }

    private static void VerifyInputGestures(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        var examples = window.FindControl<Expander>("SelectionRangeExamples")!; examples.IsExpanded = true;
        var chart = window.SelectionPrimaryChart;
        chart.IsPanEnabled = true; chart.IsZoomEnabled = true;
        window.Model.InputTouchEnabled = true; window.Model.InputWheelMode = ChartWheelMode.Auto; window.Model.InputRoutingMode = ChartInputRoutingMode.Auto;
        window.Model.InputTouchThreshold = 8; window.Model.InputPanSensitivity = 48; window.Model.InputZoomSensitivity = 1.2;
        chart.ResetViewport(); window.FlushBindings();
        void Bring() { examples.BringIntoView(); chart.BringIntoView(); window.FlushBindings(); }
        Bring();
        var scroll = chart.GetVisualAncestors().OfType<ScrollViewer>().Last();
        var sourcePosition = chart.TranslatePoint(chart.PlotArea.Center, window)!.Value;
        var offsetBefore = scroll.Offset; var rangeBefore = chart.VisibleRange;
        // BringIntoView may place this late-page example at the parent's bottom boundary.
        // Choose a direction with actual capacity before asserting that the parent scrolls.
        var scrollDelta = offsetBefore.Y > 0 ? .5 : -.5;
        window.MouseWheel(sourcePosition, new Vector(0, scrollDelta)); window.FlushBindings();
        var offsetAfterScroll = scroll.Offset;
        checks["packaged-input-unmodified-wheel-moves-real-parent-only"] = new Rect(window.ClientSize).Contains(sourcePosition) &&
            (scrollDelta > 0 ? offsetAfterScroll.Y < offsetBefore.Y : offsetAfterScroll.Y > offsetBefore.Y) && chart.VisibleRange == rangeBefore;
        Bring(); var zoomPosition = chart.TranslatePoint(chart.PlotArea.Center, window)!.Value; var parentBeforeZoom = scroll.Offset;
        window.MouseWheel(zoomPosition, new Vector(0, 1), RawInputModifiers.Control); window.FlushBindings();
        var zoomRange = chart.VisibleRange!.Value;
        checks["packaged-input-control-wheel-zooms-chart-without-parent"] = Math.Abs(zoomRange.XMaximum - zoomRange.XMinimum - 100 / 1.2) < 1e-8 && scroll.Offset == parentBeforeZoom;
        window.Model.InputRoutingMode = ChartInputRoutingMode.Chart; chart.SetVisibleFraction(new(.2, .8)); window.FlushBindings(); Bring();
        var original = chart.VisibleRange!.Value; var plot = chart.PlotArea;
        var root = chart.GetVisualAncestors().LastOrDefault() ?? chart;
        Point RootPoint(double fraction) => chart.TranslatePoint(new Point(plot.Left + plot.Width * fraction, plot.Center.Y), root)!.Value;
        using var first = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        using var second = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, false);
        void Press(Pointer finger, double fraction) => chart.RaiseEvent(new PointerPressedEventArgs(chart, finger, root, RootPoint(fraction), 1, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1) { RoutedEvent = InputElement.PointerPressedEvent });
        Point Move(Pointer finger, double fraction)
        {
            var args = new PointerEventArgs(InputElement.PointerMovedEvent, chart, finger, root, RootPoint(fraction), 2, default, KeyModifiers.None);
            var actual = args.GetPosition(chart); chart.RaiseEvent(args); window.FlushBindings(); return actual;
        }
        void Release(Pointer finger, double fraction) => chart.RaiseEvent(new PointerReleasedEventArgs(chart, finger, root, RootPoint(fraction), 3, default, KeyModifiers.None, MouseButton.Left) { RoutedEvent = InputElement.PointerReleasedEvent });
        Press(first, .35); Press(second, .65);
        var startA = RootPoint(.35); var startB = RootPoint(.65);
        var a = Move(first, .25); var b = Move(second, .75);
        var startLocalA = root.TranslatePoint(startA, chart)!.Value; var startLocalB = root.TranslatePoint(startB, chart)!.Value;
        var factor = Math.Abs(startLocalB.X - startLocalA.X) / Math.Abs(b.X - a.X);
        var beforeFraction = ((startLocalA.X + startLocalB.X) / 2 - plot.Left) / plot.Width;
        var afterFraction = ((a.X + b.X) / 2 - plot.Left) / plot.Width;
        var span = (original.XMaximum - original.XMinimum) * factor;
        var expected = original.XMinimum + beforeFraction * (original.XMaximum - original.XMinimum) - afterFraction * span;
        var actualRange = chart.VisibleRange!.Value;
        checks["packaged-input-touch-pinch-original-anchor-and-shared-state"] = ReferenceEquals(first.Captured, chart) && ReferenceEquals(second.Captured, chart) &&
            Math.Abs(actualRange.XMinimum - expected) < 1e-8 && Math.Abs(actualRange.XMaximum - expected - span) < 1e-8 &&
            Math.Abs(window.Model.SelectionRange.Lower - expected / 100) < 1e-8;
        Release(first, .25); Release(second, .75); window.FlushBindings();
        checks["packaged-input-touch-release-clears-both-captures"] = first.Captured is null && second.Captured is null;
        diagnostics["packagedInputGestures"] = new { offsetBefore, scrollDelta, offsetAfterScroll, parentAfterZoom = scroll.Offset,
            zoomRange, initialTouchRange = original, finalTouchRange = actualRange, expectedMinimum = expected, expectedSpan = span,
            firstActual = a, secondActual = b, inputKind = "Headless raw window MouseWheel plus public synthetic routed multi-touch pointers; no physical hardware validation" };
    }

    private static void VerifySelectionRange(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        var examples = window.FindControl<Expander>("SelectionRangeExamples")!;
        examples.IsExpanded = true; window.Model.SeriesSelectionMode = ChartSelectionMode.Multiple; window.FlushBindings();
        var chart = window.SelectionPrimaryChart; var other = window.SelectionSecondaryChart; var selector = window.SelectionRangeControl;
        chart.ResetViewport(); chart.ClearSeriesSelection(); chart.IsPanEnabled = false; window.FlushBindings();
        examples.BringIntoView(); chart.BringIntoView(); window.FlushBindings();
        var local = new Point(chart.PlotArea.Left + chart.PlotArea.Width * .3, chart.PlotArea.Bottom - chart.PlotArea.Height * .65);
        var target = chart.TranslatePoint(local, window)!.Value;
        var parentScroll = chart.GetVisualAncestors().OfType<ScrollViewer>().Last();
        var offsetBeforeInput = parentScroll.Offset;
        // Multiple expanded examples can leave BringIntoView requests pending at different
        // ancestors. Position this real pointer target inside the client before raw injection.
        parentScroll.Offset = new Vector(parentScroll.Offset.X, Math.Clamp(parentScroll.Offset.Y + target.Y - window.ClientSize.Height / 2,
            0, Math.Max(0, parentScroll.Extent.Height - parentScroll.Viewport.Height)));
        window.FlushBindings(); target = chart.TranslatePoint(local, window)!.Value;
        var routed = 0; var sourceInside = false;
        void Observed(object? sender, PointerPressedEventArgs args)
        {
            routed++; sourceInside = args.Source is Visual source && (ReferenceEquals(source, chart) || source.GetVisualAncestors().Contains(chart));
        }
        chart.AddHandler(InputElement.PointerPressedEvent, Observed, RoutingStrategies.Bubble, handledEventsToo: true);
        try { window.MouseMove(target); window.MouseDown(target, MouseButton.Left); window.MouseUp(target, MouseButton.Left); }
        finally { chart.RemoveHandler(InputElement.PointerPressedEvent, Observed); }
        window.FlushBindings();
        checks["packaged-series-selection-window-input-routes-to-chart"] = new Rect(window.ClientSize).Contains(target) && routed == 1 && sourceInside;
        checks["packaged-series-selection-window-click-updates-compiled-model"] = window.Model.SelectedSeries.Count == 1 && ReferenceEquals(window.Model.SelectedSeries[0], chart.Series[0]);
        selector.Range = new(.2, .7); window.FlushBindings();
        static bool Near(double a, double b) => Math.Abs(a - b) < 1e-8;
        static bool ExpectedFraction(ChartRange? range) => range is { } value && Near(value.Lower, .2) && Near(value.Upper, .7);
        var firstRange = chart.VisibleRange; var secondRange = other.VisibleRange;
        checks["packaged-range-selector-two-way-and-normalized-multichart"] = window.Model.SelectionRange == selector.Range && ExpectedFraction(chart.GetVisibleFraction(ChartInteractionAxes.X)) && ExpectedFraction(other.GetVisibleFraction(ChartInteractionAxes.X)) &&
            firstRange is { } first && Near(first.XMinimum, 20) && Near(first.XMaximum, 70) && secondRange is { } second && Near(second.XMinimum, 200) && Near(second.XMaximum, 700);
        diagnostics["packagedSelectionRangeFirstRequest"] = new { requested = new ChartRange(.2, .7), selector.Range, firstRange, secondRange, absoluteNumericTolerance = 1e-8 };
        other.SetVisibleFraction(new(.3, .8)); window.FlushBindings();
        checks["packaged-follower-view-updates-primary-selector-and-model"] = Math.Abs(window.Model.SelectionRange.Lower - .3) < 1e-8 && Math.Abs(window.Model.SelectionRange.Upper - .8) < 1e-8;
        diagnostics["packagedSelectionRange"] = new { target, routed, sourceInside, offsetBeforeInput, offsetForInput = parentScroll.Offset, selector.Range, primaryRange = chart.VisibleRange, secondaryRange = other.VisibleRange,
            selectedSeries = window.Model.SelectedSeries.Select(series => series.Title).ToArray(), inputKind = "headless-window-raw-MouseDown-and-MouseUp; synthetic; no physical-input claim" };
    }

    private static void VerifyTrackball(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        var examples = window.FindControl<Expander>("TrackballExamples")!;
        examples.IsExpanded = true;
        window.Model.UseBusinessTrackball = true;
        window.Model.TrackballMode = ChartTrackballMode.Vertical;
        window.Model.TrackballDisplayMode = ChartTrackballDisplayMode.GroupAllPoints;
        window.Model.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        window.Model.TrackballPrimaryRecords[1].Y = 60;
        window.FlushBindings();
        var chart = window.TrackballChart;
        examples.BringIntoView(); window.FlushBindings();
        chart.BringIntoView(new Rect(chart.Bounds.Size)); window.FlushBindings();
        Border[] Cards() => chart.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "UsageTrackballCard").ToArray();
        static bool Near(double a, double b) => Math.Abs(a - b) < 1e-7;
        Point WindowPoint(double x, double y) => chart.TranslatePoint(new Point(chart.PlotArea.Left + chart.PlotArea.Width * x / 10,
            chart.PlotArea.Bottom - chart.PlotArea.Height * y / 100), window)!.Value;
        var pointerPosition = WindowPoint(2.5, 50);
        var insideClient = new Rect(window.ClientSize).Contains(pointerPosition);
        var routedMoves = 0;
        var routedSourceInsideChart = false;
        string? routedSourceType = null;
        Point? routedChartPosition = null;
        Rect? routedPlot = null;
        EventHandler<PointerEventArgs> observeMove = (_, args) =>
        {
            routedMoves++;
            routedSourceType = args.Source?.GetType().FullName;
            routedChartPosition = args.GetPosition(chart);
            routedPlot = chart.PlotArea;
            routedSourceInsideChart = args.Source is Visual source && (ReferenceEquals(source, chart) ||
                source.GetVisualAncestors().Any(ancestor => ReferenceEquals(ancestor, chart)));
        };
        void MoveWindow(Point requested)
        {
            routedChartPosition = null; routedPlot = null;
            window.AddHandler(InputElement.PointerMovedEvent, observeMove, RoutingStrategies.Bubble, true);
            try { window.MouseMove(requested); }
            finally { window.RemoveHandler(InputElement.PointerMovedEvent, observeMove); }
            window.FlushBindings();
        }
        MoveWindow(pointerPosition);
        var firstRoutedPosition = routedChartPosition;
        var firstRoutedPlot = routedPlot;
        var expectedX = firstRoutedPosition is { } delivered && firstRoutedPlot is { } inputPlot
            ? (delivered.X - inputPlot.Left) / inputPlot.Width * 10 : double.NaN;
        var expectedPrimary = new ChartPoint(expectedX, 20 + (expectedX - 1) * 10);
        var expectedSecondary = new ChartPoint(expectedX, 80 - expectedX * 10);
        var viewportBefore = chart.Viewport;
        var primarySeries = chart.Series[0]; var secondarySeries = chart.Series[1];
        var primary = chart.TrackballReadings.SingleOrDefault(reading => ReferenceEquals(reading.Series, primarySeries));
        var secondary = chart.TrackballReadings.SingleOrDefault(reading => ReferenceEquals(reading.Series, secondarySeries));
        checks["packaged-trackball-real-window-input-with-disabled-tooltips"] = insideClient && routedMoves > 0 && routedSourceInsideChart &&
            !chart.IsTooltipEnabled && chart.TrackballMode == ChartTrackballMode.Vertical && chart.TrackballReadings.Count == 2;
        checks["packaged-trackball-derived-values-retain-real-endpoint-business-identities"] =
            primary is { IsInterpolated: true, Segment: { } a } && secondary is { IsInterpolated: true, Segment: { } b } &&
            Near(primary.Point.X, expectedPrimary.X) && Near(primary.Point.Y, expectedPrimary.Y) &&
            Near(secondary.Point.X, expectedSecondary.X) && Near(secondary.Point.Y, expectedSecondary.Y) &&
            ReferenceEquals(a.Start.Item, window.Model.TrackballPrimaryRecords[0]) && ReferenceEquals(a.End.Item, window.Model.TrackballPrimaryRecords[1]) &&
            ReferenceEquals(b.Start.Item, window.Model.TrackballSecondaryRecords[0]) && ReferenceEquals(b.End.Item, window.Model.TrackballSecondaryRecords[1]) &&
            Equals(a.Start.StableId, "track-a0") && Equals(a.End.StableId, "track-a1") &&
            Equals(b.Start.StableId, "track-b0") && Equals(b.End.StableId, "track-b1") &&
            chart.HoveredPoints.All(hit => hit.Item is TrackballUsageRecord row && hit.Point == new ChartPoint(row.X, row.Y));
        var grouped = Cards();
        checks["packaged-trackball-compiled-template-shows-derived-values-and-both-original-ids"] = grouped.Length == 1 &&
            grouped[0] is { Bounds.Width: > 0, Bounds.Height: > 0, DataContext: ChartTooltipContext { Readings.Count: 2, Segments.Count: 2 } } &&
            grouped[0].GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageTrackballLeftId" && text.Text == "track-a0") &&
            grouped[0].GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageTrackballRightId" && text.Text == "track-a1") &&
            grouped[0].GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageTrackballValueY" && text.Text == "Y " + expectedPrimary.Y.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture));

        window.Model.TrackballDisplayMode = ChartTrackballDisplayMode.FloatAllPoints;
        window.FlushBindings();
        var floating = Cards();
        checks["packaged-trackball-display-binding-releases-group-and-creates-one-card-per-reading"] = floating.Length == 2 &&
            floating.All(card => card is { Bounds.Width: > 0, Bounds.Height: > 0, DataContext: ChartTooltipContext { Readings.Count: 1, Segments.Count: 1 } }) &&
            grouped.All(card => card.GetVisualParent() is null && card.DataContext is null) && chart.Viewport == viewportBefore;
        window.Model.TrackballSnapMode = ChartTrackballSnapMode.NearestPoint;
        window.FlushBindings(); MoveWindow(WindowPoint(2.5, 50));
        checks["packaged-trackball-nearest-mode-keeps-original-items-and-removes-derived-segments"] = chart.TrackballReadings.Count == 2 &&
            chart.TrackballReadings.All(reading => !reading.IsInterpolated && reading.Segment is null && reading.Original.Item is TrackballUsageRecord row && reading.Point == new ChartPoint(row.X, row.Y)) &&
            chart.HoveredSegments.Count == 0 && chart.PrimarySegment is null &&
            Cards().SelectMany(card => card.GetVisualDescendants().OfType<TextBlock>()).Any(text => text.Name == "UsageTrackballOriginalId" && text.Text == "track-a0");

        window.Model.TrackballTooltipMode = ChartTooltipMode.NearestSegment;
        window.Model.TrackballMode = ChartTrackballMode.None;
        window.FlushBindings();
        var fallbackRequestedPosition = WindowPoint(2.5, 35);
        MoveWindow(fallbackRequestedPosition);
        var expectedFallback = new ChartPoint(double.NaN, double.NaN);
        var expectedFallbackFraction = double.NaN;
        if (routedChartPosition is { } query && routedPlot is { } fallbackPlot)
        {
            // Orthogonal screen projection onto the known raw line, independently
            // of the production projection and the requested (possibly quantized) point.
            var start = new Point(fallbackPlot.Left + fallbackPlot.Width * .1, fallbackPlot.Bottom - fallbackPlot.Height * .2);
            var end = new Point(fallbackPlot.Left + fallbackPlot.Width * .5, fallbackPlot.Bottom - fallbackPlot.Height * .6);
            var dx = end.X - start.X; var dy = end.Y - start.Y;
            expectedFallbackFraction = Math.Clamp(((query.X - start.X) * dx + (query.Y - start.Y) * dy) / (dx * dx + dy * dy), 0, 1);
            expectedFallback = new(1 + 4 * expectedFallbackFraction, 20 + 40 * expectedFallbackFraction);
        }
        var fallback = Cards();
        checks["packaged-trackball-none-restores-bound-segment-tooltip-without-trackball-host"] = chart.IsTooltipEnabled &&
            chart.TooltipMode == ChartTooltipMode.NearestSegment && chart.PrimarySegment is { } segment && ReferenceEquals(segment.Series, primarySeries) &&
            routedChartPosition is not null && routedSourceInsideChart && new Rect(window.ClientSize).Contains(fallbackRequestedPosition) &&
            Near(segment.Point.X, expectedFallback.X) && Near(segment.Point.Y, expectedFallback.Y) && Near(segment.Fraction, expectedFallbackFraction) &&
            ReferenceEquals(segment.Start.Item, window.Model.TrackballPrimaryRecords[0]) && ReferenceEquals(segment.End.Item, window.Model.TrackballPrimaryRecords[1]) &&
            !chart.GetVisualDescendants().OfType<Control>().Any(control => control.Name == "PART_ChartTrackballOverlay") &&
            fallback.Length == 1 && fallback[0].DataContext is ChartTooltipContext { Segments.Count: 1 };
        diagnostics["packagedTrackballWindowInput"] = new
        {
            pointerPosition, insideClient, routedMoves, routedSourceInsideChart, routedSourceType,
            firstRoutedPosition, firstRoutedPlot, expectedPrimary, expectedSecondary,
            fallbackRequestedPosition, fallbackDeliveredPosition = routedChartPosition, fallbackDeliveredPlot = routedPlot, expectedFallback, expectedFallbackFraction,
            initialPrimaryPoint = primary?.Point, initialSecondaryPoint = secondary?.Point,
            viewportBefore, viewportAfter = chart.Viewport,
            inputKind = "headless-window-MouseMove; actual routed source captured; no physical input claim",
        };
        window.Model.TrackballTooltipMode = ChartTooltipMode.NearestPoint;
        window.Model.TrackballMode = ChartTrackballMode.Vertical;
        window.Model.TrackballDisplayMode = ChartTrackballDisplayMode.GroupAllPoints;
        window.Model.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        window.FlushBindings();
    }

    private static void VerifyControlTemplates(MainWindow window, Dictionary<string, bool> checks, Dictionary<string, object?> diagnostics)
    {
        var examples = window.FindControl<Expander>("ControlTemplateExamples")!;
        examples.IsExpanded = true;
        window.Model.UseLegendControlTemplate = true;
        window.Model.UseDataLabelControlTemplate = true;
        window.FlushBindings();
        var chart = window.TemplateChart;
        var chartOriginBeforeBringIntoView = chart.TranslatePoint(default, window);
        var outerOffsetsBeforeBringIntoView = chart.GetVisualAncestors().OfType<ScrollViewer>().Select(scroll => scroll.Offset).ToArray();
        // Unlike the Usage verifier's directly raised routed events, MouseWheel/
        // MouseDown below use the window's real hit test. This expander follows
        // several already-expanded examples and can be entirely below the fold.
        examples.BringIntoView();
        window.FlushBindings();
        chart.BringIntoView(new Rect(chart.Bounds.Size));
        window.FlushBindings();
        // Keep this standalone hit test as diagnostic evidence. MouseWheel also
        // drains pending jobs and renders until stable before delivering input;
        // its actual routed source is the input oracle below.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        window.FlushBindings();
        Border[] Labels() => chart.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "UsageDataLabelCard").ToArray();
        Border[] Legends() => chart.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "UsageLegendCard").ToArray();
        var record = window.Model.TemplateRecords[0];
        var label = Labels().FirstOrDefault(control => control.DataContext is ChartDataLabelContext context &&
            ReferenceEquals(context.Series, chart.Series[0]) && ReferenceEquals(context.Data.Item, record));
        checks["packaged-label-template-builds-original-business-controls"] = label is { Bounds.Width: > 0, Bounds.Height: > 0 } &&
            label.DataContext is ChartDataLabelContext labelContext && labelContext.Data.SourceIndex == 0 &&
            Equals(labelContext.Data.StableId, record.Id) && labelContext.Data.Point.Y == record.Value &&
            label.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageDataLabelRecordId" && text.Text == record.Id) &&
            label.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageDataLabelRecordName" && text.Text == record.Name);
        var legends = Legends();
        checks["packaged-legend-template-builds-bound-series-titles"] = legends.Length > 0 && legends.All(control =>
            control.Bounds.Width > 0 && control.Bounds.Height > 0 && control.DataContext is ChartLegendItem item &&
            control.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageLegendTitle" && text.Text == item.Series.Title));
        var expectedAccent = window.Resources["UsageControlTemplateAccent"] as ISolidColorBrush;
        var expectedBackground = window.Resources["UsageTooltipBackground"] as ISolidColorBrush;
        var expectedForeground = window.Resources["UsageControlTemplateForeground"] as ISolidColorBrush;
        checks["packaged-label-and-legend-inherit-application-template-resources"] = expectedAccent is not null && expectedBackground is not null && expectedForeground is not null &&
            label?.Background is ISolidColorBrush labelBrush && labelBrush.Color == expectedBackground.Color &&
            label.BorderBrush is ISolidColorBrush borderBrush && borderBrush.Color == expectedAccent.Color &&
            legends.All(control => control.Background is ISolidColorBrush brush && brush.Color == expectedBackground.Color &&
                control.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageLegendTitle" && text.Foreground is ISolidColorBrush foreground && foreground.Color == expectedForeground.Color));

        var viewer = chart.GetVisualDescendants().OfType<ScrollViewer>().SingleOrDefault(control => control.Name == "PART_ChartLegendScrollViewer");
        var visibleRange = chart.VisibleRange;
        var viewport = chart.Viewport;
        var lastSeries = chart.Series[^1];
        var invoked = false;
        var unchangedVisibility = lastSeries.IsVisible;
        var wheelInsideClient = false;
        var wheelHitInsideLegend = false;
        var routedWheelCount = 0;
        var routedWheelSourceInsideLegend = false;
        var routedWheelHandled = false;
        var routedWheelHandledAtChart = false;
        string? routedWheelSourceType = null;
        string? routedWheelSourceName = null;
        string? routedWheelCaptureType = null;
        Point? routedWheelPositionInChart = null;
        var clickInsideClient = false;
        Point? observedWheelPoint = null;
        Point? observedClickPoint = null;
        string? wheelHitType = null;
        string? wheelHitName = null;
        var offsetBeforeWheel = viewer?.Offset;
        EventHandler<ChartLegendEventArgs> cancel = (_, args) => { invoked = ReferenceEquals(args.Series, lastSeries); args.Cancel = true; };
        if (viewer is not null)
        {
            var legendHost = viewer.GetVisualAncestors().OfType<Control>().Single(control => control.Name == "PART_ChartLegend");
            EventHandler<PointerWheelEventArgs> observeAtChart = (_, args) =>
            {
                routedWheelPositionInChart = args.GetPosition(chart);
                routedWheelHandledAtChart = args.Handled;
            };
            EventHandler<PointerWheelEventArgs> observeAtWindow = (_, args) =>
            {
                routedWheelCount++;
                routedWheelSourceType = args.Source?.GetType().FullName;
                routedWheelSourceName = (args.Source as StyledElement)?.Name;
                routedWheelCaptureType = args.Pointer.Captured?.GetType().FullName;
                routedWheelSourceInsideLegend = args.Source is Visual source &&
                    (ReferenceEquals(source, legendHost) || source.GetVisualAncestors().Any(ancestor => ReferenceEquals(ancestor, legendHost)));
                routedWheelHandled = args.Handled;
            };
            var wheelPoint = viewer.TranslatePoint(new Point(viewer.Bounds.Width / 2, viewer.Bounds.Height / 2), window);
            if (wheelPoint is { } point)
            {
                observedWheelPoint = point;
                wheelInsideClient = new Rect(window.ClientSize).Contains(point);
                var input = window.InputHitTest(point);
                wheelHitType = input?.GetType().FullName;
                wheelHitName = (input as StyledElement)?.Name;
                wheelHitInsideLegend = input is Visual visual &&
                    (ReferenceEquals(visual, viewer) || visual.GetVisualAncestors().Any(ancestor => ReferenceEquals(ancestor, viewer)));
                chart.AddHandler(InputElement.PointerWheelChangedEvent, observeAtChart, RoutingStrategies.Bubble, true);
                window.AddHandler(InputElement.PointerWheelChangedEvent, observeAtWindow, RoutingStrategies.Bubble, true);
                try { window.MouseWheel(point, new Vector(0, -100)); }
                finally
                {
                    chart.RemoveHandler(InputElement.PointerWheelChangedEvent, observeAtChart);
                    window.RemoveHandler(InputElement.PointerWheelChangedEvent, observeAtWindow);
                }
                window.FlushBindings();
                var finalItem = chart.LegendItems.SingleOrDefault(item => ReferenceEquals(item.Series, lastSeries));
                if (finalItem is not null && chart.TranslatePoint(finalItem.Bounds.Center, window) is { } target)
                {
                    observedClickPoint = target;
                    clickInsideClient = new Rect(window.ClientSize).Contains(target);
                    chart.LegendItemInvoked += cancel;
                    try
                    {
                        window.MouseDown(target, MouseButton.Left);
                        window.MouseUp(target, MouseButton.Left);
                        window.FlushBindings();
                    }
                    finally { chart.LegendItemInvoked -= cancel; }
                }
            }
        }
        checks["packaged-legend-scroll-reaches-and-cancels-the-original-last-series"] = viewer is not null && viewer.Offset.Y > 0 &&
            wheelInsideClient && routedWheelCount == 1 && routedWheelSourceInsideLegend && routedWheelHandledAtChart && routedWheelHandled && clickInsideClient &&
            chart.LegendItems.Any(item => ReferenceEquals(item.Series, lastSeries)) && invoked && lastSeries.IsVisible == unchangedVisibility &&
            chart.Viewport == viewport && chart.VisibleRange == visibleRange;
        diagnostics["packagedLegendWindowInput"] = new
        {
            chartOriginBeforeBringIntoView, chartOriginAfterBringIntoView = chart.TranslatePoint(default, window),
            outerOffsetsBeforeBringIntoView,
            outerOffsetsAfterBringIntoView = chart.GetVisualAncestors().OfType<ScrollViewer>().Select(scroll => scroll.Offset).ToArray(),
            windowClientSize = window.ClientSize, chartBounds = chart.Bounds,
            viewerFound = viewer is not null, viewerExtent = viewer?.Extent, viewerViewport = viewer?.Viewport,
            offsetBeforeWheel, offsetAfterWheel = viewer?.Offset,
            observedWheelPoint, wheelInsideClient, wheelHitInsideLegend, wheelHitType, wheelHitName,
            routedWheelCount, routedWheelSourceInsideLegend, routedWheelSourceType, routedWheelSourceName,
            routedWheelCaptureType, routedWheelPositionInChart, routedWheelHandledAtChart, routedWheelHandled,
            inputOracle = "actual-window-MouseWheel-routed-source-and-chart/window-bubble-handled",
            standaloneInputHitTestRole = "diagnostic-only; Headless MouseWheel drains jobs/renders to stability before raw input",
            observedClickPoint, clickInsideClient, invoked, lastSeriesTitle = lastSeries.Title,
            visibilityBefore = unchangedVisibility, visibilityAfter = lastSeries.IsVisible,
            viewportBefore = viewport, viewportAfter = chart.Viewport,
            visibleRangeBefore = visibleRange, visibleRangeAfter = chart.VisibleRange,
            visibleItemsAfterWheel = chart.LegendItems.Select(item => new { title = item.Series.Title, item.Bounds }).ToArray(),
        };

        window.Model.UseDataLabelControlTemplate = false;
        window.Model.UseLegendControlTemplate = false;
        window.FlushBindings();
        checks["packaged-template-toggle-releases-business-controls-and-contexts"] = Labels().Length == 0 && Legends().Length == 0 &&
            label is not null && label.GetVisualParent() is null && label.DataContext is null &&
            legends.All(control => control.GetVisualParent() is null && control.DataContext is null);
        window.Model.UseDataLabelControlTemplate = true;
        window.Model.UseLegendControlTemplate = true;
        window.FlushBindings();
        checks["packaged-template-toggle-restores-real-label-and-legend-controls"] = Labels().Length > 0 && Legends().Length > 0;
    }
}

public sealed class ConsumerApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
#if CHARTS_THEME_CONSUMER
        Styles.Add(new ChartsTheme());
#else
        Resources[typeof(CartesianChart)] = new ControlTheme
        {
            TargetType = typeof(CartesianChart),
            Setters =
            {
                new Setter(CartesianChart.ForegroundProperty, Brushes.DarkBlue),
                new Setter(CartesianChart.BackgroundProperty, Brushes.White),
                new Setter(CartesianChart.FontSizeProperty, 14d),
                new Setter(CartesianChart.AxisBrushProperty, Brushes.Gray),
                new Setter(CartesianChart.GridLineBrushProperty, Brushes.LightGray),
                new Setter(CartesianChart.TooltipBackgroundProperty, Brushes.White),
                new Setter(CartesianChart.TooltipForegroundProperty, Brushes.DarkBlue),
            },
        };
#endif
    }
}
