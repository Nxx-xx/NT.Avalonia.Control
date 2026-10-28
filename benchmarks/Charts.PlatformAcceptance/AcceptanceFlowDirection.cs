using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using NT.AvaloniaControl.Charts;

namespace Charts.PlatformAcceptance;

internal sealed partial class AcceptanceWindow
{
    private sealed record GlyphInk(int InkPixels, int LowerLeftInk, int LowerRightInk, int[] Bounds)
    {
        internal bool LowerStemIsLeft => InkPixels > 12 && LowerLeftInk > LowerRightInk + 3;
    }

    // The fixture exercises the real Cartesian default data-label drawing path.
    // A native TextBlock F with the same font/size supplies an asymmetric visual
    // reference; property values alone cannot satisfy these raster checks.
    private async Task VerifyNativeGlyphOrientationAsync()
    {
        _body.Children.Clear();
        var rows = new[] { new Datum("glyph-A", .5, 1), new Datum("glyph-C", 1.5, 1) };
        ChartDataLabelContext? labelContext = null;
        var series = new ScatterSeries
        {
            ItemsSource = rows, IdPath = nameof(Datum.Id), CategoryPath = nameof(Datum.X), ValuePath = nameof(Datum.Y),
            MarkerSize = 4, Fill = Brushes.Red, MarkerFill = Brushes.Red, ShowDataLabels = true,
            DataLabelFontSize = 48, DataLabelForeground = Brushes.Black, DataLabelBackground = null,
            DataLabelPosition = ChartDataLabelPosition.Center, DataLabelOverlap = ChartDataLabelOverlap.Allow,
            DataLabelFormatter = context =>
            {
                if (!Equals(context.Data.StableId, "glyph-A")) return null;
                labelContext = context;
                return "F";
            },
        };
        var chart = new CartesianChart
        {
            Name = "RtlActualDefaultDataLabel", ShowLegend = false, ShowGridLines = false, Background = Brushes.White, Foreground = Brushes.Black,
            FontSize = 48, FontStyle = FontStyle.Normal, FontWeight = FontWeight.Normal, Padding = new Thickness(0),
            XAxis = new NumericAxis { Minimum = 0, Maximum = 2, IsVisible = false },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 2, IsVisible = false },
            IsTooltipEnabled = false, SelectionMode = ChartSelectionMode.None, IsZoomEnabled = false, IsPanEnabled = false,
        };
        chart.Series.Add(series);
        var reference = new TextBlock { Name = "RtlNativeTextBlockReference", Text = "F", FontFamily = chart.FontFamily,
            FontSize = 48, FontWeight = FontWeight.Normal, FontStyle = FontStyle.Normal, Foreground = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
        var panel = new Grid { ColumnDefinitions = new("*,*"), Background = Brushes.White };
        panel.Children.Add(chart); Grid.SetColumn(reference, 1); panel.Children.Add(reference); _body.Children.Add(panel);
        foreach (var variant in new[] { "explicit-rtl", "inherited-rtl", "runtime-switch-rtl" })
        {
            panel.FlowDirection = variant == "inherited-rtl" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            if (variant == "inherited-rtl")
            {
                chart.ClearValue(Visual.FlowDirectionProperty); reference.ClearValue(Visual.FlowDirectionProperty);
            }
            else
            {
                chart.FlowDirection = FlowDirection.LeftToRight; reference.FlowDirection = FlowDirection.LeftToRight;
            }
            await SettleAsync();
            Artifact? beforeSwitch = null;
            Rect? beforeSwitchRegion = null;
            if (variant == "runtime-switch-rtl")
            {
                beforeSwitch = CaptureWindowRaster("rtl-glyph-runtime-switch-ltr-before");
                beforeSwitchRegion = NativeGlyphRegion(chart, labelContext?.Position ?? chart.PlotArea.Center);
            }
            if (variant != "inherited-rtl")
            {
                chart.FlowDirection = FlowDirection.RightToLeft; reference.FlowDirection = FlowDirection.RightToLeft;
            }
            await SettleAsync();
            var prefix = "rtl-glyph-" + variant;
            var baseline = CaptureWindowRaster(prefix + "-baseline");
            var anchor = labelContext?.Position ?? chart.PlotArea.Center;
            var glyphRegion = NativeGlyphRegion(chart, anchor);
            var referenceRegion = NativeControlRegion(reference, 6);
            var plot = chart.PlotArea;
            var a = new Point(plot.X + plot.Width * .25, plot.Y + plot.Height * .5);
            var c = new Point(plot.X + plot.Width * .75, plot.Y + plot.Height * .5);
            var hitA = chart.HitTestPoint(a, .1); var hitC = chart.HitTestPoint(c, .1);
            var windowA = chart.TranslatePoint(a, this) ?? default; var windowC = chart.TranslatePoint(c, this) ?? default;
            Check(prefix + "-public-inherited-direction", chart.FlowDirection == FlowDirection.RightToLeft && reference.FlowDirection == FlowDirection.RightToLeft);
            Check(prefix + "-current-original-identities", hitA is { StableId: "glyph-A", Index: 0 } && ReferenceEquals(hitA.Item, rows[0]) &&
                hitC is { StableId: "glyph-C", Index: 1 } && ReferenceEquals(hitC.Item, rows[1]) && labelContext?.Data.StableId is "glyph-A");
            Check(prefix + "-off-centre-window-axis-order", windowA.X < windowC.X && !chart.XAxis.IsInversed);
            series.ShowDataLabels = false; await SettleAsync();
            var hidden = CaptureWindowRaster(prefix + "-label-hidden");
            series.ShowDataLabels = true; await SettleAsync();
            var restored = CaptureWindowRaster(prefix + "-label-restored");
            var baselineRaster = ReadArtifact(baseline.Path); var hiddenRaster = ReadArtifact(hidden.Path);
            var glyphInk = Ink(baselineRaster, glyphRegion, hiddenRaster);
            var referenceInk = Ink(baselineRaster, referenceRegion);
            Check(prefix + "-only-label-restores-exact-raster", baseline.DecodedBgraSha256 != hidden.DecodedBgraSha256 && baseline.DecodedBgraSha256 == restored.DecodedBgraSha256);
            Check(prefix + "-native-glyph-lower-stem-left", glyphInk.LowerStemIsLeft && referenceInk.LowerStemIsLeft);
            var exportOptions = new ChartExportOptions { PixelSize = new PixelSize(720, 360), Background = Brushes.White, Dpi = 144 };
            var export = await ExportArtifactAsync(chart, prefix + "-chart-export.png", false, false, exportOptions);
            var exportRegion = ExportAnchorRegion(chart, anchor, exportOptions.PixelSize!.Value);
            var exportInk = Ink(ReadArtifact(export.Path), exportRegion);
            Check(prefix + "-export-glyph-lower-stem-left", exportInk.LowerStemIsLeft && referenceInk.LowerStemIsLeft);
            _equalPairs.Add(new { firstPath = baseline.Path, secondPath = restored.Path, purpose = prefix + "/default-label-restored" });
            _differentPairs.Add(new { firstPath = baseline.Path, secondPath = hidden.Path, purpose = prefix + "/actual-default-label-draws" });
            _glyphOrientationProofs.Add(new
            {
                name = variant, scope = "actual Cartesian default data label and same-font native TextBlock reference", expectedGlyph = "F", expectedLowerStem = "left",
                baselinePath = baseline.Path, hiddenPath = hidden.Path, restoredPath = restored.Path,
                glyphRegion = Region(glyphRegion), referenceRegion = Region(referenceRegion),
                exportPath = export.Path, exportGlyphRegion = Region(exportRegion),
                beforeRuntimeChangePath = beforeSwitch?.Path, beforeRuntimeChangeGlyphRegion = beforeSwitchRegion is { } before ? Region(before) : null,
                fontSizeDips = 48, requestedFontFamily = chart.FontFamily.ToString(), actualRenderScaling = RenderScaling,
                glyphInk, referenceInk, exportInk,
                labelIsolation = "Only the real scatter series ShowDataLabels is toggled; markers, canvas, native reference and layout remain present.",
            });
            _suite.Observations.Add(new
            {
                kind = "RTL-asymmetric-glyph", variant, publicDirection = chart.FlowDirection.ToString(), parentDirection = panel.FlowDirection.ToString(),
                plotDips = R(plot), labelAnchorChartDips = P(anchor), labelAnchorWindowDips = P(chart.TranslatePoint(anchor, this) ?? default),
                originalPointA = new { sourceIndex = hitA?.Index, id = hitA?.StableId?.ToString(), chartDips = P(a), windowDips = P(windowA) },
                originalPointC = new { sourceIndex = hitC?.Index, id = hitC?.StableId?.ToString(), chartDips = P(c), windowDips = P(windowC) },
                actualRenderScaling = RenderScaling, baselinePath = baseline.Path, glyphRegion = Region(glyphRegion), referenceRegion = Region(referenceRegion),
                qualification = "Asymmetric Latin F orientation and explicit numeric-axis order in representative actual drawing paths; not certification of all Arabic shaping or all chart-family text.",
            });
        }
        panel.Children.Clear(); _body.Children.Clear();
    }

    private Rect NativeGlyphRegion(Control chart, Point anchor)
    {
        var native = chart.TranslatePoint(anchor, this) ?? throw new InvalidOperationException("Glyph chart detached.");
        return new Rect((native.X - 70) * RenderScaling, (native.Y - 80) * RenderScaling, 140 * RenderScaling, 160 * RenderScaling);
    }

    private Rect NativeControlRegion(Control control, double padding)
    {
        var first = control.TranslatePoint(default, this) ?? throw new InvalidOperationException("Native reference detached.");
        var last = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), this) ?? first;
        return new Rect((Math.Min(first.X, last.X) - padding) * RenderScaling, (Math.Min(first.Y, last.Y) - padding) * RenderScaling,
            (Math.Abs(last.X - first.X) + padding * 2) * RenderScaling, (Math.Abs(last.Y - first.Y) + padding * 2) * RenderScaling);
    }

    private static Rect ExportAnchorRegion(ChartBase chart, Point anchor, PixelSize pixels)
    {
        var scale = Math.Min(pixels.Width / chart.Bounds.Width, pixels.Height / chart.Bounds.Height);
        var x = (pixels.Width - chart.Bounds.Width * scale) / 2 + anchor.X * scale;
        var y = (pixels.Height - chart.Bounds.Height * scale) / 2 + anchor.Y * scale;
        return new Rect(x - 70 * scale, y - 80 * scale, 140 * scale, 160 * scale);
    }

    private static object Region(Rect rect) => new { x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height };
    private static ObservedRaster ReadArtifact(string path) { using var bitmap = new Bitmap(path); return ObservedRaster.Read(bitmap); }

    private static GlyphInk Ink(ObservedRaster image, Rect region, ObservedRaster? hidden = null)
    {
        var pixels = new List<(int X, int Y)>();
        for (var y = Math.Max(0, (int)Math.Floor(region.Y)); y < Math.Min(image.Height, (int)Math.Ceiling(region.Bottom)); y++)
            for (var x = Math.Max(0, (int)Math.Floor(region.X)); x < Math.Min(image.Width, (int)Math.Ceiling(region.Right)); x++)
            {
                var offset = (y * image.Width + x) * 4;
                var selected = hidden is null ? image.Bytes[offset + 3] > 200 && image.Bytes[offset] < 96 && image.Bytes[offset + 1] < 96 && image.Bytes[offset + 2] < 96 :
                    Math.Abs(image.Bytes[offset] - hidden.Bytes[offset]) + Math.Abs(image.Bytes[offset + 1] - hidden.Bytes[offset + 1]) + Math.Abs(image.Bytes[offset + 2] - hidden.Bytes[offset + 2]) + Math.Abs(image.Bytes[offset + 3] - hidden.Bytes[offset + 3]) > 16;
                if (selected) pixels.Add((x, y));
            }
        if (pixels.Count == 0) return new(0, 0, 0, [0, 0, 0, 0]);
        var left = pixels.Min(p => p.X); var right = pixels.Max(p => p.X); var top = pixels.Min(p => p.Y); var bottom = pixels.Max(p => p.Y);
        var lowerStart = top + (bottom - top + 1) * 2 / 3;
        var center = (left + right) / 2d;
        return new(pixels.Count, pixels.Count(p => p.Y >= lowerStart && p.X <= center), pixels.Count(p => p.Y >= lowerStart && p.X > center),
            [left, top, right - left + 1, bottom - top + 1]);
    }
}
