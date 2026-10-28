using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;

namespace NT.AvaloniaControl.Charts;

internal sealed class LinuxChartNativeTextPlatform : IChartNativeTextPlatform
{
    private Assembly? _assembly;
    public bool IsLinux => OperatingSystem.IsLinux() && !OperatingSystem.IsAndroid();
    public Architecture Architecture => RuntimeInformation.ProcessArchitecture;
    public bool IsAvaloniaInitialized => Application.Current is not null;

    public string GetGlibcVersion()
    {
        if (RuntimeInformation.RuntimeIdentifier.StartsWith("linux-musl", StringComparison.OrdinalIgnoreCase))
            throw new ChartNativeTextInitializationError("UnsupportedLibc", "The musl .NET runtime is not supported, even when a separate glibc compatibility library is installed.");
        try
        {
            return Marshal.PtrToStringUTF8(GetGnuLibcVersion())
                ?? throw new PlatformNotSupportedException("glibc did not report its version.");
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
        {
            throw new ChartNativeTextInitializationError("UnsupportedLibc", "Native text isolation requires glibc; musl and other Linux C libraries are not supported.", error);
        }
    }

    public bool IsNativeTextLoaded()
    {
        try
        {
            return File.ReadLines("/proc/self/maps").Any(line => line.Contains("libHarfBuzzSharp.so", StringComparison.Ordinal));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new ChartNativeTextInitializationError("NativeModuleInspectionUnavailable", "Cannot establish early native text initialization because /proc/self/maps is unavailable.", error);
        }
    }

    public ChartNativeTextAsset FindAsset(string rid)
    {
        var pin = ChartNativeTextAssets.GetKnownAsset(rid);
        return ChartNativeTextAssets.Inspect(AppContext.BaseDirectory, rid, pin.Bytes, pin.Sha256);
    }

    public void PrepareManagedAssembly()
    {
        try
        {
            // Core Charts deliberately has no Avalonia.Skia/HarfBuzzSharp dependency.
            // Explicit desktop consumers supply their normal locked managed dependency.
            _assembly = Assembly.Load(new AssemblyName("HarfBuzzSharp"));
            var version = _assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            if (_assembly.GetName().Name != "HarfBuzzSharp" || version != ChartNativeTextBinding.SupportedPackageVersion)
                throw new ChartNativeTextInitializationError("UnsupportedManagedVersion", "This binding supports the consumer's HarfBuzzSharp 8.3.1.3 managed assembly only.");
        }
        catch (Exception error) when (error is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            throw new ChartNativeTextInitializationError("ManagedDependencyUnavailable", "The desktop consumer must supply its supported HarfBuzzSharp managed dependency.", error);
        }
    }

    public void RegisterResolver(Func<string, nint> resolver)
    {
        if (_assembly is null) throw new InvalidOperationException("The native text managed assembly was not inspected.");
        try { NativeLibrary.SetDllImportResolver(_assembly, (name, _, _) => resolver(name)); }
        catch (InvalidOperationException error)
        {
            throw new ChartNativeTextInitializationError("ResolverConflict", "HarfBuzzSharp already has a resolver. Charts will not replace or chain an unknown resolver; select one early binding owner and restart.", error);
        }
    }

    public nint OpenAsset(string path)
    {
        var handle = Dlopen(path, 2 | 0 | 8); // RTLD_NOW | RTLD_LOCAL | RTLD_DEEPBIND, glibc only.
        if (handle == 0)
            throw new ChartNativeTextInitializationError("NativeLoadFailed", "HarfBuzzSharp dlopen failed: " + Marshal.PtrToStringUTF8(Dlerror()));
        return handle;
    }

    public ChartNativeTextSymbols InspectSymbols(nint handle, string assetPath)
    {
        var font = NativeLibrary.GetExport(handle, "hb_font_create");
        var version = NativeLibrary.GetExport(handle, "hb_version_string");
        var fontProvider = GetProvider(font);
        var versionProvider = GetProvider(version);
        if (fontProvider != assetPath || versionProvider != assetPath)
            throw new ChartNativeTextInitializationError("NativeSymbolMismatch", "The inspected native text symbols came from another native module.");
        var getVersion = Marshal.GetDelegateForFunctionPointer<HbVersionString>(version);
        return new ChartNativeTextSymbols(Marshal.PtrToStringUTF8(getVersion()) ?? "", fontProvider);
    }

    private static string GetProvider(nint address)
    {
        if (Dladdr(address, out var info) == 0 || Marshal.PtrToStringUTF8(info.FileName) is not { } name)
            throw new ChartNativeTextInitializationError("NativeSymbolMismatch", "The native symbol provider could not be observed.");
        return Path.GetFullPath(name);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint HbVersionString();
    [StructLayout(LayoutKind.Sequential)] private struct DlInfo { public nint FileName, BaseAddress, SymbolName, SymbolAddress; }
    [DllImport("libdl.so.2", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Dlopen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    [DllImport("libdl.so.2", EntryPoint = "dlerror", CallingConvention = CallingConvention.Cdecl)] private static extern nint Dlerror();
    [DllImport("libdl.so.2", EntryPoint = "dladdr", CallingConvention = CallingConvention.Cdecl)] private static extern int Dladdr(nint address, out DlInfo info);
    [DllImport("libc.so.6", EntryPoint = "gnu_get_libc_version", CallingConvention = CallingConvention.Cdecl)] private static extern nint GetGnuLibcVersion();
}

internal static class ChartNativeTextAssets
{
    // Fingerprints are from HarfBuzzSharp.NativeAssets.Linux 8.3.1.3, not the system
    // libharfbuzz. A dependency upgrade must deliberately re-evaluate both artifacts.
    internal static (long Bytes, string Sha256) GetKnownAsset(string rid) => rid switch
    {
        "linux-x64" => (2808040, "1D5C3AFEF13545BF34BF8F068B14E25EE619C3B6DEE235C44E260FE61CB24018"),
        "linux-arm64" => (2657280, "68FC5EF842AC0DBD57E8063A11DE5BB2F3C2B5AF530A2D4FA07A7CF5D6EA4259"),
        _ => throw new ChartNativeTextInitializationError("UnsupportedArchitecture", "There is no evaluated native text asset for this runtime identifier."),
    };

    internal static ChartNativeTextAsset Inspect(string baseDirectory, string rid, long expectedBytes, string expectedSha256)
    {
        _ = GetKnownAsset(rid);
        var root = Path.GetFullPath(baseDirectory);
        // Never search CWD, LD_LIBRARY_PATH, the NuGet cache, or a machine installation.
        var candidates = new[]
        {
            Path.Combine(root, "runtimes", rid, "native", "libHarfBuzzSharp.so"),
            Path.Combine(root, "libHarfBuzzSharp.so"),
        };
        ChartNativeTextAsset? selected = null;
        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bytes = stream.Length;
            var sha256 = Convert.ToHexString(SHA256.HashData(stream));
            if (bytes != expectedBytes || sha256 != expectedSha256)
                throw new ChartNativeTextInitializationError("UnsupportedNativeAsset", "A deployed HarfBuzzSharp asset differs from the evaluated package version and architecture. Repair the deployment or evaluate the dependency upgrade explicitly.");
            selected ??= new ChartNativeTextAsset(Path.GetFullPath(path), bytes, sha256);
        }
        return selected ?? throw new ChartNativeTextInitializationError("MissingNativeAsset", "No supported HarfBuzzSharp asset exists under the application's runtime-native or RID-published base directory.");
    }
}
