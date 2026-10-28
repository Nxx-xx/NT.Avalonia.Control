using System.Runtime.InteropServices;
using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>The process-local Charts native text initialization state.</summary>
public enum ChartNativeTextBindingStatus
{
    Uninitialized,
    NotApplicable,
    Initialized,
    Failed,
}

/// <summary>An immutable observation; initialization alone is not a rendering or device certification.</summary>
public sealed record ChartNativeTextBindingDiagnostics
{
    public ChartNativeTextBindingStatus Status { get; internal init; }
    public string PackageVersion => ChartNativeTextBinding.SupportedPackageVersion;
    public string? RuntimeIdentifier { get; internal init; }
    public string? GlibcVersion { get; internal init; }
    public string? AssetPath { get; internal init; }
    public string? AssetSha256 { get; internal init; }
    public long? AssetBytes { get; internal init; }
    public string? NativeVersion { get; internal init; }
    public string? SymbolProviderPath { get; internal init; }
    public bool ResolverRegistered { get; internal init; }
    public bool ResolverObserved { get; internal init; }
    public long ResolverInvocationCount { get; internal init; }
    public string? ResolvedLibraryName { get; internal init; }
    public bool DeepBindingEnabled { get; internal init; }
    public string? ErrorCode { get; internal init; }
    public string? ErrorMessage { get; internal init; }
}

/// <summary>Explicit early binding of the supported packaged HarfBuzzSharp asset on glibc Linux.</summary>
/// <remarks>
/// Call before Avalonia setup and before any HarfBuzzSharp native use. This opt-in does not
/// install a module initializer, set LD_PRELOAD, or change another assembly's resolver.
/// </remarks>
public static class ChartNativeTextBinding
{
    public const string SupportedPackageVersion = "8.3.1.3";
    private static readonly ChartNativeTextBindingEngine Engine = new(new LinuxChartNativeTextPlatform());

    /// <summary>Returns a snapshot without loading a native library or initializing text shaping.</summary>
    public static ChartNativeTextBindingDiagnostics Diagnostics => Engine.Diagnostics;

    /// <summary>Initializes once per process; subsequent and concurrent calls share the result.</summary>
    /// <exception cref="InvalidOperationException">
    /// An applicable Linux initialization failed. The failure is sticky: restart after correcting
    /// the configuration; do not catch it and continue with font rendering.
    /// </exception>
    public static ChartNativeTextBindingDiagnostics Initialize() => Engine.Initialize();
}

/// <summary>Early AppBuilder integration for the packaged Linux native text binding.</summary>
public static class ChartAppBuilderExtensions
{
    /// <summary>Initializes immediately and returns the same builder. Put before UsePlatformDetect/setup.</summary>
    public static AppBuilder WithChartsNativeTextIsolation(this AppBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ChartNativeTextBinding.Initialize();
        return builder;
    }
}

internal sealed class ChartNativeTextBindingEngine(IChartNativeTextPlatform platform)
{
    private readonly object _gate = new();
    private ChartNativeTextBindingDiagnostics _diagnostics = new();
    private InvalidOperationException? _failure;
    private bool _initializing;
    // Native imports can remain cached until process shutdown. Never dlclose this handle.
    private nint _handle;
    private long _resolverCalls;
    private string? _resolvedName;

    internal ChartNativeTextBindingDiagnostics Diagnostics
    {
        get { lock (_gate) return Snapshot(); }
    }

    internal ChartNativeTextBindingDiagnostics Initialize()
    {
        lock (_gate)
        {
            if (_failure is not null)
                throw new InvalidOperationException("Charts native text initialization previously failed; restart the process after correcting the configuration.", _failure);
            if (_diagnostics.Status is ChartNativeTextBindingStatus.Initialized or ChartNativeTextBindingStatus.NotApplicable)
                return Snapshot();
            if (_initializing)
                throw new ChartNativeTextInitializationError("ReentrantInitialization", "Native text initialization cannot be re-entered.");
            _initializing = true;
            try
            {
                if (!platform.IsLinux)
                {
                    _diagnostics = _diagnostics with { Status = ChartNativeTextBindingStatus.NotApplicable };
                    return Snapshot();
                }
                var rid = platform.Architecture switch
                {
                    Architecture.X64 => "linux-x64",
                    Architecture.Arm64 => "linux-arm64",
                    _ => throw new ChartNativeTextInitializationError("UnsupportedArchitecture", "Native text isolation supports glibc Linux x64 and arm64 only."),
                };
                _diagnostics = _diagnostics with { RuntimeIdentifier = rid, GlibcVersion = platform.GetGlibcVersion() };
                RequireEarly();
                var asset = platform.FindAsset(rid);
                _diagnostics = _diagnostics with { AssetPath = asset.Path, AssetBytes = asset.Bytes, AssetSha256 = asset.Sha256 };
                platform.PrepareManagedAssembly();
                RequireEarly();
                // Register before dlopen: a concurrent first import must wait on this gate,
                // never temporarily fall back to the default native loader with a zero handle.
                platform.RegisterResolver(Resolve);
                _diagnostics = _diagnostics with { ResolverRegistered = true };
                RequireEarly();
                _handle = platform.OpenAsset(asset.Path);
                if (_handle == 0)
                    throw new ChartNativeTextInitializationError("NativeLoadFailed", "The isolated native loader returned no handle.");
                _diagnostics = _diagnostics with { DeepBindingEnabled = true };
                var symbols = platform.InspectSymbols(_handle, asset.Path);
                _diagnostics = _diagnostics with { NativeVersion = symbols.Version, SymbolProviderPath = symbols.ProviderPath };
                if (symbols.Version != "8.3.1")
                    throw new ChartNativeTextInitializationError("NativeVersionMismatch", "The native HarfBuzz version differs from the supported 8.3.1 asset.");
                if (!string.Equals(Path.GetFullPath(symbols.ProviderPath), asset.Path, StringComparison.Ordinal))
                    throw new ChartNativeTextInitializationError("NativeSymbolMismatch", "The selected native text symbols are not provided by the inspected asset.");
                _diagnostics = _diagnostics with { Status = ChartNativeTextBindingStatus.Initialized };
                return Snapshot();
            }
            catch (Exception error)
            {
                _diagnostics = _diagnostics with
                {
                    Status = ChartNativeTextBindingStatus.Failed,
                    ErrorCode = error is ChartNativeTextInitializationError known ? known.Code : "InitializationFailed",
                    ErrorMessage = error.Message,
                };
                _failure = new InvalidOperationException("Charts native text initialization failed. Read ChartNativeTextBinding.Diagnostics; do not continue with native text in this process.", error);
                throw _failure;
            }
            finally { _initializing = false; }
        }
    }

    private void RequireEarly()
    {
        if (platform.IsAvaloniaInitialized || platform.IsNativeTextLoaded())
            throw new ChartNativeTextInitializationError("TooLate", "Initialize native text before Avalonia setup and before any HarfBuzzSharp native use.");
    }

    private nint Resolve(string name)
    {
        if (name is not ("libHarfBuzzSharp" or "libHarfBuzzSharp.so" or "HarfBuzzSharp")) return 0;
        lock (_gate)
        {
            if (_diagnostics.Status != ChartNativeTextBindingStatus.Initialized || _handle == 0)
                throw new DllNotFoundException("The explicit Charts native text binding has not initialized successfully; default loading is intentionally disabled for this resolver.", _failure);
            _resolverCalls++;
            _resolvedName ??= name;
            return _handle;
        }
    }

    private ChartNativeTextBindingDiagnostics Snapshot() => _diagnostics with
    {
        ResolverObserved = _resolverCalls > 0,
        ResolverInvocationCount = _resolverCalls,
        ResolvedLibraryName = _resolvedName,
    };
}

internal interface IChartNativeTextPlatform
{
    bool IsLinux { get; }
    Architecture Architecture { get; }
    bool IsAvaloniaInitialized { get; }
    string GetGlibcVersion();
    bool IsNativeTextLoaded();
    ChartNativeTextAsset FindAsset(string rid);
    void PrepareManagedAssembly();
    void RegisterResolver(Func<string, nint> resolver);
    nint OpenAsset(string path);
    ChartNativeTextSymbols InspectSymbols(nint handle, string assetPath);
}

internal sealed record ChartNativeTextAsset(string Path, long Bytes, string Sha256);
internal sealed record ChartNativeTextSymbols(string Version, string ProviderPath);
internal sealed class ChartNativeTextInitializationError(string code, string message, Exception? inner = null)
    : InvalidOperationException(message, inner)
{
    internal string Code { get; } = code;
}
