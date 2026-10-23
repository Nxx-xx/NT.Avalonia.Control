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
    private readonly List<object> _engineeringProbes = [];
    private async Task RunEngineeringSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var label = theme == ThemeVariant.Dark ? "dark" : "light";
            var background = Brush(label == "dark" ? "#111722" : "#FFFFFF");
            string Image(string kind, string state) => Path.Combine(Path.GetDirectoryName(baseImage)!, Path.GetFileNameWithoutExtension(baseImage) + $"-engineering-{label}-{kind}-{state}.png");
            void Check(string name, bool value) => checks[$"ENGINEERING-{label}-{name}"] = value;
            foreach (var kind in new[] { "carpet", "hexbin", "smith", "ternary", "windrose" })
            {
                double[][] input;
                object config;
                EngineeringChartBase chart;
                switch (kind)
                {
                    case "carpet":
                        var carpet = EngineeringCarpetRows(); input = carpet.Select(p => new[] { p.A, p.B, p.Y }).ToArray();
                        config = new { offset = .5 }; chart = new CarpetPlot { ItemsSource = carpet }; break;
                    case "hexbin":
                        HexbinPoint[] points = [new(0, 0), new(10, 10), new(5, 5), new(5.05, 5.05), new(6, 5), new(5, 6), new(6, 6)];
                        input = points.Select(p => new[] { p.X, p.Y, p.Value }).ToArray(); config = new { radius = 18d, aggregation = "Count" };
                        chart = new HexbinChart { ItemsSource = points, BinRadius = 18 }; break;
                    case "smith":
                        SmithPoint[] loads = [new(25, 25), new(50, 50), new(100, -50)]; input = loads.Select(p => new[] { p.R, p.X }).ToArray();
                        config = new { referenceImpedance = 50d, quantity = "Impedance" }; chart = new SmithChart { ItemsSource = loads }; break;
                    case "ternary":
                        TernaryPoint[] mixtures = [new(1, 1, 2), new(2, 1, 1), new(1, 2, 1)]; input = mixtures.Select(p => new[] { p.A, p.B, p.C }).ToArray();
                        config = new { }; chart = new TernaryChart { ItemsSource = mixtures }; break;
                    default:
                        var winds = Enumerable.Range(0, 4).SelectMany(i => new[] { new WindObservation(i * 90, 1, 2), new WindObservation(i * 90, 4, 1) }).ToArray();
                        input = winds.Select(p => new[] { p.Direction, p.Speed, p.Frequency }).ToArray();
                        config = new { sectorCount = 4, speedUpperBounds = new[] { 3d }, angleUnit = "Degrees" };
                        chart = new WindRoseChart { ItemsSource = winds, SectorCount = 4, SpeedUpperBounds = new[] { 3d } }; break;
                }
                chart.ItemsSource = input.Select((row, i) => kind switch
                {
                    "smith" => new EngineeringProbeRow($"{kind}-{i}", R: row[0], X: row[1]),
                    "ternary" => new EngineeringProbeRow($"{kind}-{i}", A: row[0], B: row[1], C: row[2]),
                    "carpet" => new EngineeringProbeRow($"{kind}-{i}", A: row[0], B: row[1], Y: row[2]),
                    "hexbin" => new EngineeringProbeRow($"{kind}-{i}", X: row[0], Y: row[1], Value: row[2]),
                    _ => new EngineeringProbeRow($"{kind}-{i}", Direction: row[0], Speed: row[1], Frequency: row[2]),
                }).ToArray();
                chart.IdPath = nameof(EngineeringProbeRow.Id);
                if (chart is SmithChart mappedSmith) { mappedSmith.ResistancePath = "R"; mappedSmith.ReactancePath = "X"; }
                if (chart is TernaryChart mappedTernary) { mappedTernary.APath = "A"; mappedTernary.BPath = "B"; mappedTernary.CPath = "C"; }
                if (chart is CarpetPlot mappedCarpet) { mappedCarpet.APath = "A"; mappedCarpet.BPath = "B"; mappedCarpet.YPath = "Y"; }
                if (chart is HexbinChart mappedHex) { mappedHex.XPath = "X"; mappedHex.YPath = "Y"; mappedHex.ValuePath = "Value"; }
                if (chart is WindRoseChart mappedWind) { mappedWind.DirectionPath = "Direction"; mappedWind.SpeedPath = "Speed"; mappedWind.FrequencyPath = "Frequency"; }
                chart.Width = 420; chart.Height = 280; chart.Background = background;
                chart.Palette = new IBrush[] { Brush("#1598B6"), Brush("#D25573"), Brush("#55B47A"), Brush("#E4AB47") };
                chart.MarkerSize = 8; chart.IsTooltipEnabled = false;
                var window = new Window { Width = 420, Height = 280, CanResize = false, RequestedThemeVariant = theme, Background = background, Content = chart, Title = "Engineering probe · " + kind };
                try
                {
                    window.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(window);
                    Check(kind + "-native-window", window.TryGetPlatformHandle() is { Handle: not 0 });
                    var marks = chart.GetMarks();
                    Check(kind + "-source-member-total", marks.Sum(mark => mark.Members.Count) == input.Length);
                    Check(kind + "-members-retain-source-indices", marks.SelectMany(m => m.Members).Select(m => m.SourceIndex).Order().SequenceEqual(Enumerable.Range(0, input.Length)));
                    Check(kind + "-members-retain-original-items", marks.SelectMany(m => m.Members).All(m => m.Item is not null && m.StableId is not null));
                    Check(kind + "-grid-present", chart.GetGridLines().Count > 0);
                    var nativeImage = Image(kind, "native");
                    using (var raster = new RenderTargetBitmap(new PixelSize(420, 280), new Vector(96, 96))) { raster.Render(chart); raster.Save(nativeImage); }
                    var exportImage = Image(kind, "export");
                    Check(kind + "-export96", chart.TryExportPng(exportImage, new ChartExportOptions { Dpi = 96, PixelSize = new PixelSize(420, 280), Background = background }));
                    var dpiImages = new List<string>();
                    if (label == "light" && kind is "smith" or "ternary")
                    {
                        var dpi = kind == "smith" ? 144 : 192; var path = Image(kind, "dpi" + dpi); dpiImages.Add(path);
                        Check(kind + "-dpi-export", chart.TryExportPng(path, new ChartExportOptions { Dpi = dpi, PixelSize = new PixelSize(420 * dpi / 96, 280 * dpi / 96), Background = background }));
                    }
                    var sourceTarget = kind == "carpet" ? 3 : kind == "hexbin" ? 2 : 0;
                    var target = marks.Select((mark, index) => (mark, index)).Single(pair => pair.mark.Members.Any(member => member.SourceIndex == sourceTarget));
                    var hitResult = chart.HitTest(target.mark.Position);
                    Check(kind + "-fixed-raw-target-hit", hitResult?.Index == target.index);
                    chart.SelectedIndex = target.index; FlushControlTemplateWindow(window);
                    Check(kind + "-hit-and-selection-original-members", chart.SelectedPoint?.Members.Select(p => p.SourceIndex).SequenceEqual(hitResult!.Members.Select(p => p.SourceIndex)) == true);
                    var plot = chart.PlotArea;
                    _engineeringProbes.Add(new
                    {
                        kind, theme = label, phase = "baseline", widthDip = 420, heightDip = 280,
                        plot = new { x = plot.X, y = plot.Y, width = plot.Width, height = plot.Height }, input, config,
                        marks = marks.Select((mark, index) => EngineeringProbeMark(mark, index)).ToArray(),
                        selected = EngineeringProbeHit(chart.SelectedPoint), hit = EngineeringProbeHit(hitResult), nativeImage, exportImage, dpiImages,
                        physicalInputValidated = false, performanceMeasured = false,
                    });
                    var ids = marks.Select(m => string.Join(",", m.Members.Select(p => p.SourceIndex))).ToArray();
                    chart.Zoom = 1.2; chart.PanX = .03; chart.PanY = -.02; FlushControlTemplateWindow(window);
                    Check(kind + "-zoom-retains-source-members", ids.SequenceEqual(chart.GetMarks().Select(m => string.Join(",", m.Members.Select(p => p.SourceIndex)))));
                    Check(kind + "-viewport-transform", chart.GetMarks().Zip(marks).All(pair => Math.Abs(pair.First.Position.X - (plot.Center.X + (pair.Second.Position.X - plot.Center.X) * 1.2 + .03 * plot.Width)) < 1e-8));
                    chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Home });
                    chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Enter }); FlushControlTemplateWindow(window);
                    Check(kind + "-keyboard-selects-first-mark", chart.SelectedPoint?.Index == 0);
                    chart.ResetViewport(); FlushControlTemplateWindow(window);
                    Check(kind + "-reset", chart.Zoom == 1 && chart.PanX == 0 && chart.PanY == 0 && chart.SelectedPoint is null);
                }
                finally { window.Close(); }
            }

            var values = new ObservableCollection<HexbinPoint> { new(0, 0), new(10, 10), new(5, 5) };
            var mutable = new HexbinChart { ItemsSource = values, Width = 420, Height = 280 };
            var lifecycle = new Window { Width = 420, Height = 280, Content = mutable, RequestedThemeVariant = theme };
            try
            {
                lifecycle.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(lifecycle);
                var snapshot = mutable.GetMarks(); values.Add(new(5, 5)); FlushControlTemplateWindow(lifecycle);
                Check("observable-append", mutable.GetMarks().Sum(mark => mark.Members.Count) == 4 && snapshot.Sum(mark => mark.Members.Count) == 3);
                mutable.BinRadius = 30; FlushControlTemplateWindow(lifecycle);
                Check("parameter-rebuild-preserves-original-members", mutable.GetMarks().Sum(mark => mark.Members.Count) == 4);
                lifecycle.Content = null; FlushControlTemplateWindow(lifecycle); values.Add(new(6, 6)); lifecycle.Content = mutable; FlushControlTemplateWindow(lifecycle);
                Check("unload-reattach-reads-current-source", mutable.GetMarks().Sum(mark => mark.Members.Count) == 5);
                mutable.ItemsSource = new HexbinPoint[] { new(1, 1), new(2, 2) }; FlushControlTemplateWindow(lifecycle); values.Add(new(8, 8)); FlushControlTemplateWindow(lifecycle);
                Check("replace-detaches-old-source", mutable.GetMarks().Sum(mark => mark.Members.Count) == 2);

                EngineeringProbeRow[] identityRows = [new("resize-0", X: -10, Y: -10), new("resize-1"), new("resize-2", X: .5), new("resize-3", X: 10, Y: 10)];
                mutable.IdPath = "Id"; mutable.XPath = "X"; mutable.YPath = "Y"; mutable.ValuePath = "Value";
                mutable.BinRadius = 18; mutable.ItemsSource = identityRows; FlushControlTemplateWindow(lifecycle);
                mutable.SelectedIndex = mutable.GetMarks().Select((mark, index) => (mark, index)).Single(pair => pair.mark.Members.Any(member => Equals(member.StableId, "resize-3"))).index;
                FlushControlTemplateWindow(lifecycle); mutable.Width = lifecycle.Width = 840; FlushControlTemplateWindow(lifecycle);
                Check("resize-follows-original-selected-member", mutable.SelectedPoint?.Members.Any(member => ReferenceEquals(member.Item, identityRows[3])) == true);
                mutable.BinRadius = 3; FlushControlTemplateWindow(lifecycle);
                Check("rebin-follows-original-selected-member", mutable.SelectedPoint?.Members.Any(member => ReferenceEquals(member.Item, identityRows[3])) == true);
                mutable.ItemsSource = identityRows[..3]; FlushControlTemplateWindow(lifecycle);
                Check("removed-selected-member-clears-selection", mutable.SelectedPoint is null && mutable.SelectedIndex == -1);

                mutable.Width = lifecycle.Width = 420; mutable.Aggregation = HexbinAggregation.Sum;
                mutable.ItemsSource = new EngineeringProbeRow[] { new("overflow-0", Value: 1e308), new("overflow-1", Value: 1e308) }; FlushControlTemplateWindow(lifecycle);
                Check("hex-overflow-reported-without-ui-exception", mutable.GetMarks().Count == 0 && mutable.DataError is not null && mutable.SelectedPoint is null);
                mutable.ItemsSource = new EngineeringProbeRow[] { new("valid-0", Value: 1), new("valid-1", Value: 2) }; FlushControlTemplateWindow(lifecycle);
                Check("hex-data-error-recovers", mutable.DataError is null && mutable.GetMarks().Count == 1 && mutable.GetMarks()[0].Value == 3);
                var invalidWind = new WindRoseChart { Width = 420, Height = 280, ItemsSource = new WindObservation[] { new(0, 1, 1e308), new(0, 1, 1e308) } };
                lifecycle.Content = invalidWind; FlushControlTemplateWindow(lifecycle);
                Check("wind-overflow-reported-without-ui-exception", invalidWind.GetMarks().Count == 0 && invalidWind.DataError is not null);
                invalidWind.ItemsSource = new WindObservation[] { new(0, 1, 1), new(0, 1, 2) }; FlushControlTemplateWindow(lifecycle);
                Check("wind-data-error-recovers", invalidWind.DataError is null && invalidWind.GetMarks().Sum(mark => mark.Value) == 3);
            }
            finally { lifecycle.Close(); }
            var board = CreateEngineeringBoard();
            var overview = new Window { Title = "Charts · 工程图与专业坐标", Width = 1440, Height = 1120, RequestedThemeVariant = theme, Background = background, Content = new Border { Padding = new Thickness(16), Child = board } };
            try
            {
                overview.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(overview);
                Check("six-demo-panels", board.GetVisualDescendants().OfType<EngineeringChartBase>().Count() == 6);
                var path = Image("board", "overview");
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(overview.ClientSize.Width), (int)Math.Ceiling(overview.ClientSize.Height)), new Vector(96, 96)); bitmap.Render(overview); bitmap.Save(path);
                _engineeringProbes.Add(new { kind = "board", theme = label, phase = "board", image = path, physicalInputValidated = false, performanceMeasured = false });
            }
            finally { overview.Close(); }
        }
    }

    private static object? EngineeringProbeHit(EngineeringPointHit? hit) => hit is null ? null : new { index = hit.Index, members = hit.Members.Select(EngineeringProbeMember).ToArray() };
    private static object EngineeringProbeMember(ChartDataPoint point) => new { sourceIndex = point.SourceIndex, stableId = point.StableId, item = point.Item, point = new { x = point.Point.X, y = point.Point.Y }, value = point.Value };
    private static object EngineeringProbeMark(EngineeringMarkInfo mark, int index) => new { index, position = new { x = mark.Position.X, y = mark.Position.Y }, value = mark.Value, fill = mark.Fill is ISolidColorBrush brush ? brush.Color.ToString() : null, members = mark.Members.Select(EngineeringProbeMember).ToArray() };
    private sealed record EngineeringProbeRow(string Id, double R = 0, double X = 0, double A = 0, double B = 0, double C = 0, double Y = 0, double Direction = 0, double Speed = 0, double Frequency = 0, double Value = 1);
}
