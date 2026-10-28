using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

// This diagnostic scene is part of the ordinary sample application. Only its
// native smoke method opens a probe window; constructing it is headless-safe.
internal sealed class FontStartupScene
{
    private sealed record Row(string Id, string Label, double X, double Y);
    private sealed record ShapedRun(string Text, string Family, int GlyphCount, int MissingGlyphs, int[] GlyphIds);
    private sealed record FontProbe(string Codepoint, bool Matched, string? Family, int? GlyphId, string? FontSha256, long? FontBytes, string? Error);
    private sealed record Raster(string Path, int PixelWidth, int PixelHeight, string Sha256, string DecodedBgraSha256, byte[] Bytes);
    private readonly Row[] _rows = [new("font-a", "中文 e\u0301", 1, 18), new("font-b", "😀", 2, 45), new("font-c", "مبيعات", 3, 72)];
    private readonly Dictionary<string, TextBlock> _samples = [];
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    internal CartesianChart Chart { get; }
    internal Control View { get; }
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    internal FontStartupScene()
    {
        var samples = new StackPanel { Spacing = 4 };
        foreach (var sample in new[] { ("cjk", "中文：订单与趋势"), ("combining", "组合字符：e\u0301 · é"), ("emoji", "表情：😀"), ("arabic", "مبيعات العربية") })
        {
            var text = new TextBlock { Name = "FontSample_" + sample.Item1, Text = sample.Item2, FontSize = 18,
                TextWrapping = TextWrapping.Wrap, FlowDirection = sample.Item1 == "arabic" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
            _samples.Add(sample.Item1, text); samples.Children.Add(text);
        }
        Chart = new CartesianChart
        {
            Name = "PlatformFontStartupChart", Height = 310, Title = "字体：中文 / e\u0301 / 😀 / مبيعات",
            XAxis = new NumericAxis { Minimum = 0, Maximum = 4, Interval = 1, Title = "时间 e\u0301" },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100, Title = "数量 مبيعات" },
            IsTooltipEnabled = false, IsZoomEnabled = false, IsPanEnabled = false, ReduceMotion = true,
        };
        Chart.Series.Add(new LineSeries
        {
            Title = "同源显示 · 中文 / e\u0301 / 😀 / مبيعات", ItemsSource = _rows,
            CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y), IdPath = nameof(Row.Id),
            ShowMarkers = true, MarkerSize = 8, ShowDataLabels = true, DataLabelFontSize = 14,
            DataLabelFormatter = context => (context.Data.Item as Row)?.Label,
        });
        var details = new TextBlock { Text = JsonSerializer.Serialize(BindingSnapshot(), JsonOptions), TextWrapping = TextWrapping.Wrap, FontSize = 11 };
        View = new StackPanel
        {
            Spacing = 8, Margin = new Thickness(16), Children =
            {
                new TextBlock { Text = "平台字体与启动诊断", FontSize = 20, FontWeight = FontWeight.SemiBold },
                _status, samples, Chart,
                new Expander { Header = "本次启动诊断", Content = new ScrollViewer { MaxHeight = 170, Content = details } },
            },
        };
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var status = ChartNativeTextBinding.Diagnostics.Status.ToString();
        _status.Text = status == "Initialized" ? "平台字体：应用已在创建界面前接入本机文本运行时。"
            : status == "NotApplicable" ? "平台字体：当前平台使用系统文本运行时。"
            : "平台字体：" + status;
    }

    internal static object BindingSnapshot() => JsonSerializer.SerializeToElement(ChartNativeTextBinding.Diagnostics, JsonOptions);

    internal static void WriteStartupFailure(string[] arguments, Exception error)
    {
        var index = Array.IndexOf(arguments, "--smoke-output");
        var output = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : "artifacts/platform/font-startup.json";
        var path = Path.ChangeExtension(Path.GetFullPath(output), ".startup-failure.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Open(path, FileMode.CreateNew);
            JsonSerializer.Serialize(stream, new { schema = "charts-sample-font-startup-failure/v1", passed = false,
                timestampUtc = DateTimeOffset.UtcNow, processId = Environment.ProcessId, nativeTextBinding = BindingSnapshot(),
                error = error.ToString(), qualification = "Ordinary sample startup failed; no UI or glyph pass is claimed." }, JsonOptions);
        }
        catch (Exception evidenceError) { Console.Error.WriteLine("Could not preserve startup failure evidence: " + evidenceError); }
    }

    internal static async Task<object> RunSmokeAsync(Window owner, string imagePrefix, Dictionary<string, bool> checks, object? beforeUi)
    {
        var scene = new FontStartupScene();
        var window = new Window { Title = "Charts · 平台字体与启动诊断", Width = 1050, Height = 690,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = scene.View };
        var directory = Path.GetDirectoryName(Path.GetFullPath(imagePrefix))!;
        Directory.CreateDirectory(directory);
        var stem = Path.GetFileNameWithoutExtension(imagePrefix) + "-font-startup";
        void Check(string name, bool value) => checks["font-startup-" + name] = value;
        window.Show(owner);
        try
        {
            await FlushAsync(window);
            var nativeRuns = scene._samples.ToDictionary(pair => pair.Key, pair => CaptureRuns(pair.Value));
            var chartRuns = CaptureRuns(scene.Chart);
            var fonts = ProbeFonts();
            scene.RefreshStatus(); await FlushAsync(window);
            var current = ChartNativeTextBinding.Diagnostics;
            var linux = OperatingSystem.IsLinux();
            var assetObserved = !linux || (!string.IsNullOrEmpty(current.AssetPath) && File.Exists(current.AssetPath) &&
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(current.AssetPath))) == current.AssetSha256 &&
                string.Equals(Path.GetFullPath(current.AssetPath), Path.GetFullPath(current.SymbolProviderPath ?? ""), StringComparison.Ordinal));
            var expectedStatus = linux ? "Initialized" : "NotApplicable";
            Check("ordinary-entry-initialized-before-ui", beforeUi is JsonElement { ValueKind: JsonValueKind.Object } early &&
                early.TryGetProperty("status", out var earlyStatus) && earlyStatus.GetString() == expectedStatus && current.Status.ToString() == expectedStatus);
            Check("actual-native-binding", linux ? current.DeepBindingEnabled && current.ResolverObserved && current.ResolverInvocationCount > 0 && assetObserved : !current.DeepBindingEnabled);
            Check("native-window", window.TryGetPlatformHandle() is { Handle: not 0 } && window.IsVisible);
            Check("all-script-glyphs-present", fonts.All(font => font.Matched && font.GlyphId is > 0));
            foreach (var item in nativeRuns)
                Check("textblock-" + item.Key + "-shaped-glyphs", item.Value.Count > 0 && item.Value.Sum(run => run.GlyphCount) > 0 && item.Value.All(run => run.MissingGlyphs == 0));
            Check("chart-real-multilingual-glyphs", chartRuns.Count > 0 && chartRuns.All(run => run.MissingGlyphs == 0) &&
                chartRuns.Any(run => run.Text.Contains("中文", StringComparison.Ordinal)) && chartRuns.Any(run => run.Text.Contains("مبيعات", StringComparison.Ordinal)));
            var plot = scene.Chart.PlotArea;
            ChartPointHit? Hit(Row row) => scene.Chart.HitTestPoint(new Point(plot.X + row.X / 4 * plot.Width, plot.Bottom - row.Y / 100 * plot.Height), .5);
            var hitA = Hit(scene._rows[0]); var hitC = Hit(scene._rows[2]);
            Check("original-off-centre-data-identities", hitA is { StableId: "font-a", Index: 0 } && ReferenceEquals(hitA.Item, scene._rows[0]) &&
                hitC is { StableId: "font-c", Index: 2 } && ReferenceEquals(hitC.Item, scene._rows[2]));
            var baseline = Capture(window, Path.Combine(directory, stem + "-window.png"));
            var hidden = new List<(string Name, Raster Raster, Rect Region)>();
            foreach (var pair in scene._samples)
            {
                var topLeft = pair.Value.TranslatePoint(default, window) ?? throw new InvalidOperationException("Native text sample is detached.");
                var scale = window.RenderScaling;
                var region = new Rect(topLeft.X * scale, topLeft.Y * scale, pair.Value.Bounds.Width * scale, pair.Value.Bounds.Height * scale);
                pair.Value.Opacity = 0; await FlushAsync(window);
                var raster = Capture(window, Path.Combine(directory, stem + "-" + pair.Key + "-hidden.png"));
                Check("textblock-" + pair.Key + "-real-raster-ink", ChangedPixels(baseline, raster, region) >= 3);
                hidden.Add((pair.Key, raster, region));
                pair.Value.Opacity = 1; await FlushAsync(window);
            }
            var restored = Capture(window, Path.Combine(directory, stem + "-restored.png"));
            Check("native-text-restores-exact-raster", baseline.DecodedBgraSha256 == restored.DecodedBgraSha256);
            var export = Path.Combine(directory, stem + "-chart.png");
            var darkExport = scene.Chart.ActualThemeVariant == ThemeVariant.Dark;
            var exportCanvas = darkExport ? Brushes.Black : Brushes.White;
            Check("chart-png-actual-export", scene.Chart.TryExportPng(export, new ChartExportOptions { PixelSize = new PixelSize(960, 360), Dpi = 144, Background = exportCanvas }));
            var exportArtifact = File.Exists(export) ? Read(export) : null;
            Check("chart-export-size", exportArtifact is { PixelWidth: 960, PixelHeight: 360 });
            return new
            {
                schema = "charts-sample-font-startup/v1", processId = Environment.ProcessId, processStartedUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                nativeBackend = window.TryGetPlatformHandle()?.HandleDescriptor, actualRenderScaling = window.RenderScaling,
                actualClientDips = new[] { window.ClientSize.Width, window.ClientSize.Height }, nativeTextBeforeUi = beforeUi,
                nativeTextAfterRealText = BindingSnapshot(), fontCacheEnvironment = new { home = Environment.GetEnvironmentVariable("HOME"),
                    xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME"), xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") },
                nativeTextRuns = nativeRuns, chartTextRuns = chartRuns, fontProbes = fonts,
                actualChartTheme = scene.Chart.ActualThemeVariant.ToString(), exportBackground = darkExport ? "#FF000000" : "#FFFFFFFF",
                originalHits = new[] { new { id = hitA?.StableId, sourceIndex = hitA?.Index }, new { id = hitC?.StableId, sourceIndex = hitC?.Index } },
                baseline = Artifact(baseline), restored = Artifact(restored), chartExport = exportArtifact is null ? null : Artifact(exportArtifact),
                glyphProofs = hidden.Select(item => new { name = item.Name, baselinePath = baseline.Path, hiddenPath = item.Raster.Path, restoredPath = restored.Path,
                    glyphRegion = new { x = item.Region.X, y = item.Region.Y, width = item.Region.Width, height = item.Region.Height },
                    changedPixels = ChangedPixels(baseline, item.Raster, item.Region), minimumChangedPixels = 3, hidden = Artifact(item.Raster) }).ToArray(),
                captureMethod = "Neutral 96-DPI RTB; full live Window VisualBrush scaled explicitly to ceil(client DIP × actual RenderScaling).",
                qualification = "Ordinary application entry without host-isolation CLI. Real TextBlock/Chart shaping and software raster observations; not compositor, physical input, complete shaping or cold-cache certification by itself.",
            };
        }
        finally { window.Content = null; window.Close(); }
    }

    private static async Task FlushAsync(Window window)
    {
        window.UpdateLayout();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(100);
        window.UpdateLayout();
    }

    private static List<ShapedRun> CaptureRuns(Control control)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open()) control.Render(context);
        return Glyphs(drawing).Where(item => item.GlyphRun is not null).Select(item =>
        {
            var run = item.GlyphRun!;
            return new ShapedRun(new string(run.Characters.Span), run.GlyphTypeface.FamilyName, run.GlyphInfos.Count,
                run.GlyphInfos.Count(glyph => glyph.GlyphIndex == 0), run.GlyphInfos.Select(glyph => (int)glyph.GlyphIndex).Take(96).ToArray());
        }).ToList();
    }

    private static IEnumerable<GlyphRunDrawing> Glyphs(Drawing drawing)
    {
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var glyph in Glyphs(child)) yield return glyph;
        else if (drawing is GlyphRunDrawing glyph) yield return glyph;
    }

    private static List<FontProbe> ProbeFonts()
    {
        var result = new List<FontProbe>(); var manager = FontManager.Current;
        foreach (var scalar in new[] { 0x41, 0x65, 0x4E2D, 0x6587, 0x0301, 0x1F600, 0x0627, 0x0628, 0x0645 })
        {
            try
            {
                var matched = manager.TryMatchCharacter(scalar, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal,
                    manager.DefaultFontFamily, CultureInfo.CurrentCulture, out var face);
                string? hash = null; long? bytes = null;
                if (matched && face.GlyphTypeface.PlatformTypeface.TryGetStream(out var stream))
                {
                    using (stream) { if (stream.CanSeek) { bytes = stream.Length; stream.Position = 0; } hash = Convert.ToHexString(SHA256.HashData(stream)); }
                }
                result.Add(new($"U+{scalar:X}", matched, matched ? face.GlyphTypeface.FamilyName : null,
                    matched ? face.GlyphTypeface.CharacterToGlyphMap.GetGlyph(scalar) : null, hash, bytes, null));
            }
            catch (Exception error) { result.Add(new($"U+{scalar:X}", false, null, null, null, null, error.ToString())); }
        }
        return result;
    }

    private static Raster Capture(Window window, string path)
    {
        var scale = window.RenderScaling; var pixels = new PixelSize((int)Math.Ceiling(window.ClientSize.Width * scale), (int)Math.Ceiling(window.ClientSize.Height * scale));
        var physical = new Rect(new Size(window.ClientSize.Width * scale, window.ClientSize.Height * scale));
        using var bitmap = new RenderTargetBitmap(pixels, new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
            context.DrawRectangle(new VisualBrush(window) { SourceRect = new RelativeRect(new Rect(window.ClientSize), RelativeUnit.Absolute),
                DestinationRect = new RelativeRect(physical, RelativeUnit.Absolute), Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, physical);
        using (var stream = File.Open(path, FileMode.CreateNew)) bitmap.Save(stream);
        return Read(path);
    }

    private static Raster Read(string path)
    {
        using var source = new Bitmap(path); var size = source.PixelSize;
        using var converted = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var buffer = converted.Lock(); source.CopyPixels(buffer);
        var bytes = new byte[checked(size.Width * size.Height * 4)];
        for (var y = 0; y < size.Height; y++) Marshal.Copy(buffer.Address + y * buffer.RowBytes, bytes, y * size.Width * 4, size.Width * 4);
        return new(path, size.Width, size.Height, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), Convert.ToHexString(SHA256.HashData(bytes)), bytes);
    }

    private static object Artifact(Raster raster) => new { path = raster.Path, kind = "PNG", pixelWidth = raster.PixelWidth,
        pixelHeight = raster.PixelHeight, sha256 = raster.Sha256, decodedBgraSha256 = raster.DecodedBgraSha256 };

    private static int ChangedPixels(Raster first, Raster second, Rect region)
    {
        if (first.PixelWidth != second.PixelWidth || first.PixelHeight != second.PixelHeight) return 0;
        var count = 0;
        for (var y = Math.Max(0, (int)Math.Floor(region.Y)); y < Math.Min(first.PixelHeight, (int)Math.Ceiling(region.Bottom)); y++)
            for (var x = Math.Max(0, (int)Math.Floor(region.X)); x < Math.Min(first.PixelWidth, (int)Math.Ceiling(region.Right)); x++)
            {
                var index = (y * first.PixelWidth + x) * 4;
                if (Math.Abs(first.Bytes[index] - second.Bytes[index]) + Math.Abs(first.Bytes[index + 1] - second.Bytes[index + 1]) +
                    Math.Abs(first.Bytes[index + 2] - second.Bytes[index + 2]) + Math.Abs(first.Bytes[index + 3] - second.Bytes[index + 3]) > 16) count++;
            }
        return count;
    }
}

