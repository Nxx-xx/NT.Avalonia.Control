using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;

namespace Charts.PlatformAcceptance;

/// <summary>Observes public native-window and current drawing-context APIs; never changes desktop settings.</summary>
internal static class PlatformDiagnostics
{
    private sealed record CaptureEpoch(long Id, string Phase, TaskCompletionSource<bool> Drawn);
    private static readonly ConcurrentQueue<SurfaceObservation> Surfaces = new();
    private static Options _options = null!;
    private static CaptureEpoch? _capture;
    private static long _epoch;

    internal static void Initialize(Options options) => _options = options;

    internal static string ObservedRenderer
    {
        get
        {
            var observations = Surfaces.ToArray();
            var known = observations.Where(item => item.Error is null && item.HasGrContext is not null).ToArray();
            if (known.Length == 0) return "unknown";
            var gpu = known.Any(item => item.HasGrContext == true);
            var software = known.Any(item => item.HasGrContext == false);
            return gpu && software ? "mixed" : gpu ? "gpu-context" : "software-raster";
        }
    }

    internal static string ActualBackend(Window window)
    {
        var descriptor = window.TryGetPlatformHandle()?.HandleDescriptor;
        var implementation = window.PlatformImpl?.GetType().FullName ?? "";
        if (descriptor == "HWND" && implementation.Contains("Win32", StringComparison.Ordinal)) return "Win32";
        if (implementation.Contains("X11", StringComparison.Ordinal)) return "X11";
        return "unknown";
    }

    internal static async Task ObserveNativeFrameAsync(Window window, string phase = "scene-native-frame")
    {
        Dispatcher.UIThread.VerifyAccess();
        if (Volatile.Read(ref _capture) is not null) throw new InvalidOperationException("Native rendering observations cannot overlap.");
        var probe = window.GetVisualDescendants().OfType<PlatformProbeControl>().FirstOrDefault()
            ?? throw new InvalidOperationException("The native window must contain a PlatformProbeControl sentinel.");
        var compositor = ElementComposition.GetElementVisual(probe)?.Compositor
            ?? throw new InvalidOperationException("The sentinel is not attached to a native compositor.");
        var epoch = new CaptureEpoch(Interlocked.Increment(ref _epoch), phase,
            new(TaskCreationOptions.RunContinuationsAsynchronously));
        Volatile.Write(ref _capture, epoch);
        try
        {
            probe.InvalidateVisual();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            var batch = compositor.RequestCompositionBatchCommitAsync();
            await batch.Rendered.WaitAsync(TimeSpan.FromSeconds(15));
            await epoch.Drawn.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally { Volatile.Write(ref _capture, null); }
    }

    internal static void ObserveSurface(ImmediateDrawingContext context)
    {
        var epoch = Volatile.Read(ref _capture);
        if (epoch is null) return;
        // This gate is enabled only around a native composition batch, never during
        // suite RTB/PNG/JPEG capture. An offscreen raster must not label the window CPU.
        if (Surfaces.Count >= 64) { epoch.Drawn.TrySetException(new InvalidOperationException("Native surface evidence budget exceeded.")); return; }
        try
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                Surfaces.Enqueue(new(epoch.Id, epoch.Phase, DateTimeOffset.UtcNow, Environment.CurrentManagedThreadId,
                    null, null, null, null, null, null, "ISkiaSharpApiLeaseFeature was unavailable on the actual draw context."));
                epoch.Drawn.TrySetResult(true);
                return;
            }
            using var lease = feature.Lease();
            var hasGrContext = lease.GrContext is not null;
            var grContextType = lease.GrContext?.GetType().FullName;
            using var marker = new SKPaint { Color = new SKColor(13, 173, 211, 180) };
            lease.SkCanvas.DrawRect(new SKRect(0, 0, 3, 3), marker);
            string? platformContextType, glVendor, glRenderer, glVersion;
            // Leasing the underlying graphics API excludes access to SkCanvas and
            // GrContext. Complete Skia access first, then release the API lease
            // before publishing only immutable observations.
            using (var graphics = lease.TryLeasePlatformGraphicsApi())
            {
                var gl = graphics?.Context as IGlContext;
                platformContextType = graphics?.Context.GetType().FullName;
                glVendor = gl?.GlInterface.Vendor;
                glRenderer = gl?.GlInterface.Renderer;
                glVersion = gl?.GlInterface.Version;
            }
            Surfaces.Enqueue(new(epoch.Id, epoch.Phase, DateTimeOffset.UtcNow, Environment.CurrentManagedThreadId,
                hasGrContext, grContextType, platformContextType, glVendor, glRenderer, glVersion, null));
            epoch.Drawn.TrySetResult(true);
        }
        catch (Exception error)
        {
            Surfaces.Enqueue(new(epoch.Id, epoch.Phase, DateTimeOffset.UtcNow, Environment.CurrentManagedThreadId,
                null, null, null, null, null, null, error.ToString()));
            epoch.Drawn.TrySetException(error);
        }
    }

    internal static Dictionary<string, object?> Capture(Window window)
    {
        Dispatcher.UIThread.VerifyAccess();
        var handle = window.TryGetPlatformHandle();
        var screen = window.Screens.ScreenFromWindow(window);
        var surfaces = Surfaces.ToArray();
        var win32 = OperatingSystem.IsWindows() && handle is { Handle: not 0 } ? ReadWin32Metrics(handle.Handle) : null;
        return new()
        {
            ["runtime"] = new
            {
                osDescription = RuntimeInformation.OSDescription,
                osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                framework = RuntimeInformation.FrameworkDescription,
                runtime = Environment.Version.ToString(),
                processId = Environment.ProcessId,
                managedThreadId = Environment.CurrentManagedThreadId,
                avalonia = typeof(Application).Assembly.GetName().Version?.ToString(),
            },
            ["window"] = new
            {
                title = window.Title,
                isVisible = window.IsVisible,
                isActive = window.IsActive,
                actualBackend = ActualBackend(window),
                backendType = window.PlatformImpl?.GetType().FullName,
                handleDescriptor = handle?.HandleDescriptor,
                handle = handle is null ? null : $"0x{handle.Handle.ToInt64():X}",
                renderScaling = window.RenderScaling,
                clientSize = new { width = window.ClientSize.Width, height = window.ClientSize.Height },
                physicalClientSize = new { width = (int)Math.Ceiling(window.ClientSize.Width * window.RenderScaling), height = (int)Math.Ceiling(window.ClientSize.Height * window.RenderScaling) },
                physicalClientSizeSource = "ceil(observed ClientSize DIP × observed RenderScaling); not a desktop screenshot measurement",
                win32ReportedMetrics = win32,
                positionPixels = new { x = window.Position.X, y = window.Position.Y },
                pointToScreenOrigin = Point(window.PointToScreen(default)),
                currentScreen = screen is null ? null : Describe(screen),
                screenCount = window.Screens.ScreenCount,
            },
            ["screens"] = window.Screens.All.Select(Describe).ToArray(),
            ["rendering"] = new
            {
                requestedMode = _options.Renderer,
                configuredModes = ConfiguredModes(),
                actualRenderer = ObservedRenderer,
                nativeWindowSurfaceObservations = surfaces,
                hardwareGpuProven = false,
                renderingSurfaceEvidence = "Public Skia API lease within a native-window ICustomDrawOperation, gated to composition batches with no overlapping offscreen captures.",
                gpuQualification = "GRContext presence proves the observed GPU context path, not hardware acceleration. GL renderer/vendor and external adapter/driver inventory require separate correlation; WARP/llvmpipe can expose GPU APIs.",
                compositorPresentValidated = false,
            },
            ["culture"] = new
            {
                currentCulture = CultureInfo.CurrentCulture.Name,
                currentUiCulture = CultureInfo.CurrentUICulture.Name,
                isRightToLeft = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft,
                decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator,
                shortDatePattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern,
                localTimeZone = TimeZoneInfo.Local.Id,
            },
            ["fonts"] = Fonts(),
            ["session"] = new
            {
                display = Environment.GetEnvironmentVariable("DISPLAY"),
                waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"),
                sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"),
                currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"),
                screenScaleFactors = Environment.GetEnvironmentVariable("AVALONIA_SCREEN_SCALE_FACTORS"),
                globalScaleFactor = Environment.GetEnvironmentVariable("AVALONIA_GLOBAL_SCALE_FACTOR"),
                nativeTextIsolation = NativeTextBindingBootstrap.Capture(_options.IsolateNativeText),
                windowsSession = Environment.GetEnvironmentVariable("SESSIONNAME"),
                requestedScale = _options.RequestedScale,
                requestedScaleIsOnlyAnExpectedObservation = true,
                physicalInputValidated = false,
                physicalDisplayTopologyCertified = false,
                source = "Reported process environment plus public native-window screens; not independent compositor/session certification.",
            },
            ["assemblies"] = Assemblies(),
            ["nativeModules"] = NativeModules(),
        };
    }

    private static string[] ConfiguredModes() => OperatingSystem.IsWindows() ? _options.Renderer switch
    {
        "software" => ["Software"], "gpu" => ["AngleEgl"], _ => ["AngleEgl", "Software"],
    } : _options.Renderer switch
    {
        "software" => ["Software"], "gpu" => ["Glx"], _ => ["Glx", "Software"],
    };

    private static object Describe(Avalonia.Platform.Screen screen) => new
    {
        displayName = screen.DisplayName,
        scaling = screen.Scaling,
        bounds = new { x = screen.Bounds.X, y = screen.Bounds.Y, width = screen.Bounds.Width, height = screen.Bounds.Height },
        workingArea = new { x = screen.WorkingArea.X, y = screen.WorkingArea.Y, width = screen.WorkingArea.Width, height = screen.WorkingArea.Height },
        isPrimary = screen.IsPrimary,
        orientation = screen.CurrentOrientation.ToString(),
        handleDescriptor = screen.TryGetPlatformHandle()?.HandleDescriptor,
    };

    private static object Point(PixelPoint point) => new { x = point.X, y = point.Y };

    private static object ReadWin32Metrics(nint handle)
    {
        // Passive OS queries only: no input injection, desktop capture or setting changes.
        var clientValid = GetClientRect(handle, out var client);
        var clientError = clientValid ? 0 : Marshal.GetLastWin32Error();
        var frameValid = GetWindowRect(handle, out var frame);
        var frameError = frameValid ? 0 : Marshal.GetLastWin32Error();
        var dpi = GetDpiForWindow(handle);
        var awareness = GetAwarenessFromDpiAwarenessContext(GetWindowDpiAwarenessContext(handle));
        return new
        {
            clientRectAvailable = clientValid,
            clientRect = clientValid ? new { left = client.Left, top = client.Top, right = client.Right, bottom = client.Bottom, width = client.Right - client.Left, height = client.Bottom - client.Top } : null,
            clientError,
            frameRectAvailable = frameValid,
            frameRect = frameValid ? new { left = frame.Left, top = frame.Top, right = frame.Right, bottom = frame.Bottom, width = frame.Right - frame.Left, height = frame.Bottom - frame.Top } : null,
            frameError,
            dpiForWindow = dpi,
            windowDpiAwareness = awareness,
            dpiAwarenessValues = "-1 Invalid, 0 Unaware, 1 SystemAware, 2 PerMonitorAware",
            source = "Passive user32 GetClientRect/GetWindowRect/GetDpiForWindow under this window DPI-awareness context; physical monitor calibration remains separate.",
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint handle, out NativeRect rectangle);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint handle, out NativeRect rectangle);
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint handle);
    [DllImport("user32.dll")]
    private static extern nint GetWindowDpiAwarenessContext(nint handle);
    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(nint context);

    private static object Fonts()
    {
        var manager = FontManager.Current;
        var characters = new[] { 0x41, 0x65, 0x4E2D, 0x6587, 0x1F600, 0x0627, 0x0628, 0x0645, 0x0301 };
        var results = new List<object>();
        foreach (var codepoint in characters)
        {
            try
            {
                var matched = manager.TryMatchCharacter(codepoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal,
                    manager.DefaultFontFamily, CultureInfo.CurrentCulture, out var face);
                string? hash = null;
                long? length = null;
                string? streamError = null;
                if (matched)
                {
                    try
                    {
                        if (face.GlyphTypeface.PlatformTypeface.TryGetStream(out var stream))
                        {
                            using (stream)
                            {
                                length = stream.CanSeek ? stream.Length : null;
                                if (stream.CanSeek) stream.Position = 0;
                                hash = Convert.ToHexString(SHA256.HashData(stream));
                            }
                        }
                        else streamError = "The selected platform font did not expose a readable font stream.";
                    }
                    catch (Exception error) { streamError = error.Message; }
                }
                results.Add(new
                {
                    codepoint = $"U+{codepoint:X}", matched,
                    requestedFamily = manager.DefaultFontFamily.Name,
                    actualFamily = matched ? face.FontFamily.Name : null,
                    glyphTypefaceFamily = matched ? face.GlyphTypeface.FamilyName : null,
                    glyphId = matched ? (int?)face.GlyphTypeface.CharacterToGlyphMap.GetGlyph(codepoint) : null,
                    fontDataStreamSha256 = hash, fontDataStreamBytes = length, fontDataStreamError = streamError,
                    scope = "FontManager matching and selected platform font stream; full shaping, fallback cluster and visual correctness require the separate text scenes.",
                });
            }
            catch (Exception error) { results.Add(new { codepoint = $"U+{codepoint:X}", matched = false, error = error.ToString() }); }
        }
        return new { defaultFamily = manager.DefaultFontFamily.Name, systemFamilyCount = manager.SystemFonts.Count, characterProbes = results };
    }

    internal static object[] Assemblies() => AppDomain.CurrentDomain.GetAssemblies()
        .Where(assembly => !assembly.IsDynamic && (assembly.GetName().Name is { } name &&
            (name.StartsWith("Avalonia", StringComparison.Ordinal) || name.StartsWith("NT.AvaloniaControl.Charts", StringComparison.Ordinal) || name.StartsWith("SkiaSharp", StringComparison.Ordinal) || name.StartsWith("HarfBuzzSharp", StringComparison.Ordinal) || name == "Charts.PlatformAcceptance")))
        .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal).Select(Assembly).ToArray();

    private static object Assembly(Assembly assembly)
    {
        var path = assembly.Location;
        return new { assemblyName = assembly.GetName().Name, version = assembly.GetName().Version?.ToString(), path, sha256 = File.Exists(path) ? Hash(path) : null };
    }

    internal static object[] NativeModules()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.Modules.Cast<ProcessModule>().Where(module => module.ModuleName.Contains("skia", StringComparison.OrdinalIgnoreCase) ||
                module.ModuleName.Contains("harfbuzz", StringComparison.OrdinalIgnoreCase) || module.ModuleName.Contains("egl", StringComparison.OrdinalIgnoreCase) ||
                module.ModuleName.Contains("gles", StringComparison.OrdinalIgnoreCase) || module.ModuleName.Contains("opengl", StringComparison.OrdinalIgnoreCase) ||
                module.ModuleName.Contains("d3d", StringComparison.OrdinalIgnoreCase) || module.ModuleName.Contains("libgl", StringComparison.OrdinalIgnoreCase) ||
                module.ModuleName.Contains("dxgi", StringComparison.OrdinalIgnoreCase))
                .OrderBy(module => module.FileName, StringComparer.Ordinal).Select(module => (object)new
                { moduleName = module.ModuleName, path = module.FileName, sha256 = File.Exists(module.FileName) ? Hash(module.FileName) : null }).ToArray();
        }
        catch (Exception error) { return [new { error = error.ToString(), scope = "Loaded module enumeration unavailable; no renderer inference was substituted." }]; }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record SurfaceObservation(long Epoch, string Phase, DateTimeOffset ObservedUtc, int ManagedThreadId,
        bool? HasGrContext, string? GrContextType, string? PlatformContextType, string? GlVendor, string? GlRenderer, string? GlVersion, string? Error);
}

internal sealed class PlatformProbeControl : Control
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.Custom(new ProbeOperation(new Rect(Bounds.Size)));
    }

    private sealed class ProbeOperation(Rect bounds) : ICustomDrawOperation
    {
        public Rect Bounds { get; } = bounds;
        public bool HitTest(Point point) => false;
        public void Render(ImmediateDrawingContext context) => PlatformDiagnostics.ObserveSurface(context);
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }
    }
}

internal sealed class PlatformLogSink : ILogSink, IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private int _count;
    private volatile bool _disposed;
    internal PlatformLogSink(string path) => _writer = new(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
    public bool IsEnabled(LogEventLevel level, string area) => !_disposed && level >= LogEventLevel.Information;
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate) => Log(level, area, source, messageTemplate, []);
    public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (++_count > 2000) return;
            _writer.WriteLine(JsonSerializer.Serialize(new { observedUtc = DateTimeOffset.UtcNow, level = level.ToString(), area,
                sourceType = source?.GetType().FullName, messageTemplate, propertyValues = propertyValues.Select(value => value?.ToString()).ToArray() }));
        }
    }
    public void Dispose() { lock (_gate) { _disposed = true; _writer.Dispose(); } }
}
