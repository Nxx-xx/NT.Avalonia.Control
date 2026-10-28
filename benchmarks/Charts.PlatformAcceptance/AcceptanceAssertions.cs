using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.PlatformAcceptance;

internal sealed partial class AcceptanceWindow
{
    private sealed record ObservedRaster(int Width, int Height, byte[] Bytes)
    {
        internal string Hash => Convert.ToHexString(SHA256.HashData(Bytes));
        internal static ObservedRaster Read(Bitmap source)
        {
            var size = source.PixelSize;
            using var converted = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using var buffer = converted.Lock(); source.CopyPixels(buffer);
            var bytes = new byte[checked(size.Width * size.Height * 4)];
            for (var y = 0; y < size.Height; y++) Marshal.Copy(buffer.Address + y * buffer.RowBytes, bytes, y * size.Width * 4, size.Width * 4);
            return new(size.Width, size.Height, bytes);
        }
    }

    private sealed record Artifact(string Path, string Kind, string Purpose, int PixelWidth, int PixelHeight, string Sha256, string DecodedBgraSha256);

    private Artifact CaptureWindowRaster(string scene)
    {
        var scale = RenderScaling;
        var client = new Rect(ClientSize);
        var physical = new Rect(new Size(ClientSize.Width * scale, ClientSize.Height * scale));
        var size = new PixelSize(Math.Max(1, (int)Math.Ceiling(physical.Width)), Math.Max(1, (int)Math.Ceiling(physical.Height)));
        var path = Path.Combine(_options.Output, "raster-window-" + scene + ".png");
        // Keep initial bitmap DPI neutral: backend DPI transforms also enter
        // nested custom-drawing clips. Apply native scaling once to the full
        // live window visual source instead of mixing it into the initial DPI.
        using (var bitmap = new RenderTargetBitmap(size, new Vector(96, 96)))
        {
            using (var drawing = bitmap.CreateDrawingContext())
            {
                var window = new VisualBrush(this)
                {
                    SourceRect = new RelativeRect(client, RelativeUnit.Absolute),
                    DestinationRect = new RelativeRect(physical, RelativeUnit.Absolute),
                    Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top,
                    TileMode = TileMode.None,
                };
                drawing.DrawRectangle(window, null, physical);
            }
            using var stream = File.Open(path, FileMode.CreateNew); bitmap.Save(stream);
        }
        var artifact = AddArtifact(path, "PNG", "software-window-raster/" + scene);
        Check(scene + "-window-raster-actual-scale-dimensions", artifact.PixelWidth == size.Width && artifact.PixelHeight == size.Height);
        _suite.Observations.Add(new { kind = "software-window-raster", scene, actualClientDips = S(ClientSize), actualRenderScaling = scale,
            physicalPixelSize = new[] { size.Width, size.Height }, artifactPath = path,
            captureMethod = "96-DPI RenderTargetBitmap; complete live Window VisualBrush with explicit source-DIP to destination-pixel scaling",
            targetDpi = new[] { 96d, 96d }, explicitScale = new[] { scale, scale }, sourceClientRectDips = R(client), destinationPixelRect = R(physical),
            qualification = "RenderTargetBitmap readback of this native window's visual tree; not a desktop compositor screenshot or GPU-output readback." });
        return artifact;
    }

    private async Task ExportResolutionCasesAsync(CartesianChart chart, string scene)
    {
        foreach (var dpi in new[] { 96d, 120d, 144d, 192d })
        {
            var pixelSize = new PixelSize(720, 360);
            var options = new ChartExportOptions { PixelSize = pixelSize, Dpi = dpi, Background = Background };
            var prefix = scene + "-dpi" + (int)dpi;
            var sync = await ExportArtifactAsync(chart, prefix + "-sync.png", false, false, options);
            var asyncImage = await ExportArtifactAsync(chart, prefix + "-async.png", false, true, options);
            Check(prefix + "-fixed-export-pixels", sync.PixelWidth == 720 && sync.PixelHeight == 360 && asyncImage.PixelWidth == 720 && asyncImage.PixelHeight == 360);
            Check(prefix + "-sync-async-same-frame", sync.DecodedBgraSha256 == asyncImage.DecodedBgraSha256);
            _equalPairs.Add(new { firstPath = sync.Path, secondPath = asyncImage.Path, purpose = prefix + "/same-current-frame" });
        }
    }

    private async Task VerifyProfessionalExportsAsync(ChartBase chart, TextBlock caption, string family)
    {
        var options = new ChartExportOptions { PixelSize = new PixelSize(800, 480), Dpi = 144, Background = Background };
        var active = await ExportArtifactAsync(chart, family + "-selected-active.png", false, false, options);
        var activeAsync = await ExportArtifactAsync(chart, family + "-selected-active-async.png", false, true, options);
        var jpeg = await ExportArtifactAsync(chart, family + "-selected-active.jpg", true, false, options);
        var jpegAsync = await ExportArtifactAsync(chart, family + "-selected-active-async.jpg", true, true, options);
        Check(family + "-export-fixed-pixels-and-sync-async", active.PixelWidth == 800 && active.PixelHeight == 480 && activeAsync.PixelWidth == 800 && activeAsync.PixelHeight == 480 &&
            jpeg.PixelWidth == 800 && jpeg.PixelHeight == 480 && jpegAsync.PixelWidth == 800 && jpegAsync.PixelHeight == 480 &&
            active.DecodedBgraSha256 == activeAsync.DecodedBgraSha256 && jpeg.DecodedBgraSha256 == jpegAsync.DecodedBgraSha256);
        _equalPairs.Add(new { firstPath = active.Path, secondPath = activeAsync.Path, purpose = family + "/png-sync-async" });
        SetTrackball(chart, ChartTrackballMode.None); await SettleAsync();
        var clearedTransient = await ExportArtifactAsync(chart, family + "-selected-no-transient.png", false, false, options);
        Check(family + "-transient-trackball-excluded", active.DecodedBgraSha256 == clearedTransient.DecodedBgraSha256);
        _equalPairs.Add(new { firstPath = active.Path, secondPath = clearedTransient.Path, purpose = family + "/active-transient-excluded" });
        var original = await ExportArtifactAsync(chart, family + "-glyph-baseline.png", false, false, options);
        var roi = ExportPixelRegion(caption, chart, options.PixelSize!.Value);
        var opacity = caption.Opacity;
        var backdrop = caption.GetVisualParent() as Border;
        var backdropBounds = backdrop?.Bounds;
        var backdropBrush = backdrop?.Background;
        caption.Opacity = 0; await SettleAsync();
        var hidden = await ExportArtifactAsync(chart, family + "-glyph-hidden.png", false, false, options);
        caption.Opacity = opacity; await SettleAsync();
        var restored = await ExportArtifactAsync(chart, family + "-glyph-restored.png", false, false, options);
        Check(family + "-permanent-template-real-raster-difference", original.DecodedBgraSha256 != hidden.DecodedBgraSha256);
        Check(family + "-permanent-template-restored-exact", original.DecodedBgraSha256 == restored.DecodedBgraSha256);
        _glyphProofs.Add(new { baselinePath = original.Path, hiddenPath = hidden.Path, restoredPath = restored.Path,
            pixelRegion = new { x = roi.X, y = roi.Y, width = roi.Width, height = roi.Height }, minimumChangedPixels = 3, purpose = family + "/actual-TextBlock-glyphs",
            isolation = "Only TextBlock.Opacity is set to zero and restored. Its independent permanent backdrop and layout remain present.", originalTextOpacity = opacity,
            parentBackdropPresent = backdrop is not null, parentBackdropBoundsUnchanged = backdrop?.Bounds == backdropBounds,
            parentBackdropBrushInstanceUnchanged = ReferenceEquals(backdrop?.Background, backdropBrush) });
        _differentPairs.Add(new { firstPath = original.Path, secondPath = hidden.Path, purpose = family + "/permanent-template-draws" });
        _equalPairs.Add(new { firstPath = original.Path, secondPath = restored.Path, purpose = family + "/permanent-template-restores" });
        ClearSelection(chart); await SettleAsync();
        var unselected = await ExportArtifactAsync(chart, family + "-unselected.png", false, false, options);
        Check(family + "-permanent-selection-real-raster-difference", unselected.DecodedBgraSha256 != clearedTransient.DecodedBgraSha256);
        _differentPairs.Add(new { firstPath = clearedTransient.Path, secondPath = unselected.Path, purpose = family + "/permanent-selection-draws" });
        _suite.Observations.Add(new { kind = "professional-export", family, pixelSize = new[] { 800, 480 }, dpi = 144,
            sourceBoundsDips = R(chart.Bounds), captionBoundsDips = R(caption.Bounds), captionGlyphExportRegion = R(roi), captionText = caption.Text,
            glyphIsolation = "TextBlock.Opacity-only; permanent parent background remains visible", parentBackdropBoundsDips = backdropBounds is { } bounds ? R(bounds) : null,
            activePath = active.Path, selectedNoTransientPath = clearedTransient.Path, unselectedPath = unselected.Path,
            syncJpegPath = jpeg.Path, asyncJpegPath = jpegAsync.Path,
            jpegPixelComparisonQualification = "Producer-decoded BGRA equality; external independent oracle verifies JPEG structure and dimensions only." });
    }

    private async Task<Artifact> ExportArtifactAsync(ChartBase chart, string filename, bool jpeg, bool asynchronous, ChartExportOptions options)
    {
        var path = Path.Combine(_options.Output, filename);
        if (File.Exists(path)) throw new IOException("Existing artifact is never overwritten: " + path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bool success;
        if (chart is CartesianChart cartesian)
            success = asynchronous ? await (jpeg ? cartesian.TryExportJpegAsync(path, options) : cartesian.TryExportPngAsync(path, options)) : jpeg ? cartesian.TryExportJpeg(path, options) : cartesian.TryExportPng(path, options);
        else if (chart is EngineeringChartBase engineering)
            success = asynchronous ? await (jpeg ? engineering.TryExportJpegAsync(path, options) : engineering.TryExportPngAsync(path, options)) : jpeg ? engineering.TryExportJpeg(path, options) : engineering.TryExportPng(path, options);
        else if (chart is AnalysisChartBase analysis)
            success = asynchronous ? await (jpeg ? analysis.TryExportJpegAsync(path, options) : analysis.TryExportPngAsync(path, options)) : jpeg ? analysis.TryExportJpeg(path, options) : analysis.TryExportPng(path, options);
        else throw new ArgumentException("Unsupported acceptance chart family.", nameof(chart));
        if (!success) throw new InvalidOperationException("Export failed: " + filename);
        return AddArtifact(path, jpeg ? "JPEG" : "PNG", "chart-export/" + filename);
    }

    private Artifact AddArtifact(string path, string kind, string purpose)
    {
        using var bitmap = new Bitmap(path);
        var raster = ObservedRaster.Read(bitmap);
        var artifact = new Artifact(path, kind, purpose, raster.Width, raster.Height, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), raster.Hash);
        _suite.Artifacts.Add(artifact);
        return artifact;
    }

    private static Rect ExportPixelRegion(Control child, ChartBase chart, PixelSize pixels)
    {
        var topLeft = child.TranslatePoint(default, chart) ?? throw new InvalidOperationException("Template glyph not attached to its actual chart.");
        var scale = Math.Min(pixels.Width / chart.Bounds.Width, pixels.Height / chart.Bounds.Height);
        var offsetX = (pixels.Width - chart.Bounds.Width * scale) / 2;
        var offsetY = (pixels.Height - chart.Bounds.Height * scale) / 2;
        return new Rect(offsetX + topLeft.X * scale, offsetY + topLeft.Y * scale, child.Bounds.Width * scale, child.Bounds.Height * scale);
    }

    private static void SetTrackball(ChartBase chart, ChartTrackballMode mode) { if (chart is EngineeringChartBase e) e.TrackballMode = mode; else ((AnalysisChartBase)chart).TrackballMode = mode; }
    private static void ClearSelection(ChartBase chart) { if (chart is EngineeringChartBase e) e.ClearDataSelection(); else ((AnalysisChartBase)chart).ClearDataSelection(); }
    private static object? Hit(ChartPointHit? hit) => hit is null ? null : new { id = hit.StableId?.ToString(), sourceIndex = hit.Index, raw = new[] { hit.Point.X, hit.Point.Y }, positionDips = P(hit.Position), originalItemType = hit.Item?.GetType().FullName };
    private static bool Near(Point a, Point b, double tolerance) => Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance;
    private static bool Finite(Rect rect) => double.IsFinite(rect.X) && double.IsFinite(rect.Y) && double.IsFinite(rect.Width) && double.IsFinite(rect.Height);
}
