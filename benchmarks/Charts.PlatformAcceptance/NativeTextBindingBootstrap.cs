using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Charts.PlatformAcceptance;

/// <summary>Explicit, process-local text binding for the Linux acceptance host only.</summary>
/// <remarks>This does not configure the Charts library, Demo, or an ordinary application consumer.</remarks>
internal static class NativeTextBindingBootstrap
{
    private const int RtldNow = 2, RtldLocal = 0, RtldDeepBind = 8;
    private const int BindingFlags = RtldNow | RtldLocal | RtldDeepBind;
    // The resolver can be invoked throughout process shutdown. The dlopen reference
    // deliberately remains valid until process termination; no dlclose is performed.
    private static nint _handle;
    private static bool _attempted, _applied, _resolverRegistered;
    private static string? _path, _sha256, _error, _libcVersion, _symbolProviderPath, _symbolAddress;
    private static string? _resolvedLibraryName;
    private static long? _bytes;

    internal static void Apply(bool requested)
    {
        if (!requested) return;
        if (_attempted) throw new InvalidOperationException("Native text isolation may be initialized only once.");
        _attempted = true;
        try
        {
            if (!OperatingSystem.IsLinux())
                throw new PlatformNotSupportedException("--isolate-native-text is restricted to the glibc Linux acceptance host.");
            _libcVersion = Marshal.PtrToStringUTF8(GetGlibcVersion())
                ?? throw new PlatformNotSupportedException("The native text isolation mode requires glibc.");
            var rid = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                _ => throw new PlatformNotSupportedException("No packaged native text path is defined for this process architecture."),
            };
            _path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", "libHarfBuzzSharp.so"));
            if (!File.Exists(_path)) throw new FileNotFoundException("The selected host must contain its packaged HarfBuzzSharp native asset.", _path);
            using (var stream = File.OpenRead(_path))
            {
                _bytes = stream.Length;
                _sha256 = Convert.ToHexString(SHA256.HashData(stream));
            }
            _handle = Dlopen(_path, BindingFlags);
            if (_handle == 0)
                throw new InvalidOperationException("Packaged HarfBuzzSharp dlopen failed: " + Marshal.PtrToStringUTF8(Dlerror()));
            var symbol = NativeLibrary.GetExport(_handle, "hb_font_create");
            _symbolAddress = $"0x{symbol.ToInt64():X}";
            if (Dladdr(symbol, out var info) == 0 || Marshal.PtrToStringUTF8(info.FileName) is not { } provider)
                throw new InvalidOperationException("The isolated native text symbol provider could not be observed.");
            _symbolProviderPath = Path.GetFullPath(provider);
            if (!string.Equals(_symbolProviderPath, _path, StringComparison.Ordinal))
                throw new InvalidOperationException("The native text symbol provider differs from the requested packaged asset.");
            NativeLibrary.SetDllImportResolver(typeof(HarfBuzzSharp.Blob).Assembly, ResolveHarfBuzz);
            _resolverRegistered = true;
            _applied = true;
        }
        catch (Exception error)
        {
            _error = error.ToString();
            throw;
        }
    }

    private static nint ResolveHarfBuzz(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName is not ("libHarfBuzzSharp" or "libHarfBuzzSharp.so" or "HarfBuzzSharp")) return 0;
        Interlocked.CompareExchange(ref _resolvedLibraryName, libraryName, null);
        return _handle;
    }

    internal static object Capture(bool requested) => new
    {
        requested,
        applied = _applied,
        path = _path,
        sha256 = _sha256,
        bytes = _bytes,
        flags = requested ? BindingFlags : 0,
        flagNames = requested ? "RTLD_NOW|RTLD_LOCAL|RTLD_DEEPBIND" : null,
        handle = _handle == 0 ? null : $"0x{_handle.ToInt64():X}",
        resolverRegistered = _resolverRegistered,
        resolverObserved = Volatile.Read(ref _resolvedLibraryName) is not null,
        resolvedLibraryName = Volatile.Read(ref _resolvedLibraryName),
        resolverAssembly = _resolverRegistered ? "HarfBuzzSharp" : null,
        symbolProviderPath = _symbolProviderPath,
        symbolAddress = _symbolAddress,
        libcVersion = _libcVersion,
        error = _error,
        hostOnly = true,
        ordinaryConsumerNativeTextCertified = false,
        scope = "Explicit acceptance-host bootstrap; it does not configure ordinary consumers or change process-global LD_PRELOAD, dependencies, renderer blacklists, or system settings.",
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct DlInfo { public nint FileName, BaseAddress, SymbolName, SymbolAddress; }
    [DllImport("libdl.so.2", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Dlopen([MarshalAs(UnmanagedType.LPUTF8Str)] string fileName, int flags);
    [DllImport("libdl.so.2", EntryPoint = "dlerror", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint Dlerror();
    [DllImport("libdl.so.2", EntryPoint = "dladdr", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Dladdr(nint address, out DlInfo info);
    [DllImport("libc.so.6", EntryPoint = "gnu_get_libc_version", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint GetGlibcVersion();
}
