using System.Runtime.InteropServices;
using System.Security.Cryptography;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartNativeTextBindingTests
{
    [Fact]
    public void NonLinux_is_a_repeatable_noop_without_managed_or_native_loading()
    {
        var platform = new FakePlatform { IsLinux = false, AvaloniaInitialized = true };
        var engine = new ChartNativeTextBindingEngine(platform);
        Assert.Equal(ChartNativeTextBindingStatus.Uninitialized, engine.Diagnostics.Status);
        Assert.Equal(ChartNativeTextBindingStatus.NotApplicable, engine.Initialize().Status);
        Assert.Equal(ChartNativeTextBindingStatus.NotApplicable, engine.Initialize().Status);
        Assert.Empty(platform.Calls);
        Assert.Null(engine.Diagnostics.AssetPath);
        Assert.False(engine.Diagnostics.ResolverRegistered);
    }

    [Theory]
    [InlineData(Architecture.X64, "linux-x64")]
    [InlineData(Architecture.Arm64, "linux-arm64")]
    public void Early_initialization_is_idempotent_even_after_Avalonia_starts(Architecture architecture, string rid)
    {
        var platform = new FakePlatform { Architecture = architecture };
        var engine = new ChartNativeTextBindingEngine(platform);
        var first = engine.Initialize();
        platform.AvaloniaInitialized = true;
        var second = engine.Initialize();
        Assert.Equal(first, second);
        Assert.Equal(ChartNativeTextBindingStatus.Initialized, first.Status);
        Assert.Equal(rid, first.RuntimeIdentifier);
        Assert.Equal("8.3.1.3", first.PackageVersion);
        Assert.Equal("8.3.1", first.NativeVersion);
        Assert.Equal(new[] { "glibc", "asset:" + rid, "managed", "register", "open", "symbols" }, platform.Calls);
        Assert.True(first.DeepBindingEnabled);
        Assert.True(first.ResolverRegistered);
        Assert.False(first.ResolverObserved);
    }

    [Fact]
    public async Task Concurrent_initialization_has_exactly_one_binding_owner()
    {
        var platform = new FakePlatform();
        var engine = new ChartNativeTextBindingEngine(platform);
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(engine.Initialize)));
        Assert.All(results, result => Assert.Equal(ChartNativeTextBindingStatus.Initialized, result.Status));
        Assert.Equal(1, platform.Calls.Count(call => call == "register"));
        Assert.Equal(1, platform.Calls.Count(call => call == "open"));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Late_initialization_fails_before_asset_or_managed_loading(bool avalonia, bool native)
    {
        var platform = new FakePlatform { AvaloniaInitialized = avalonia, NativeLoaded = native };
        var engine = new ChartNativeTextBindingEngine(platform);
        Assert.Throws<InvalidOperationException>(() => engine.Initialize());
        Assert.Equal("TooLate", engine.Diagnostics.ErrorCode);
        Assert.Equal(ChartNativeTextBindingStatus.Failed, engine.Diagnostics.Status);
        Assert.Equal(new[] { "glibc" }, platform.Calls);
    }

    [Fact]
    public void Unsupported_architecture_is_explicit_and_never_loads_native_code()
    {
        var platform = new FakePlatform { Architecture = Architecture.Arm };
        var engine = new ChartNativeTextBindingEngine(platform);
        Assert.Throws<InvalidOperationException>(() => engine.Initialize());
        Assert.Equal("UnsupportedArchitecture", engine.Diagnostics.ErrorCode);
        Assert.Empty(platform.Calls);
    }

    [Fact]
    public void Unsupported_libc_is_sticky_and_does_not_fall_back()
    {
        var platform = new FakePlatform { GlibcError = new ChartNativeTextInitializationError("UnsupportedLibc", "fixture musl") };
        var engine = new ChartNativeTextBindingEngine(platform);
        Assert.Throws<InvalidOperationException>(() => engine.Initialize());
        Assert.Throws<InvalidOperationException>(() => engine.Initialize());
        Assert.Equal("UnsupportedLibc", engine.Diagnostics.ErrorCode);
        Assert.Equal(new[] { "glibc" }, platform.Calls);
        Assert.Null(platform.Resolver);
    }

    [Fact]
    public void External_resolver_conflict_does_not_replace_it_or_load_the_asset()
    {
        var platform = new FakePlatform { RegistrationError = new ChartNativeTextInitializationError("ResolverConflict", "fixture owner") };
        var engine = new ChartNativeTextBindingEngine(platform);
        Assert.Throws<InvalidOperationException>(() => engine.Initialize());
        Assert.Throws<InvalidOperationException>(() => engine.Initialize());
        Assert.Equal("ResolverConflict", engine.Diagnostics.ErrorCode);
        Assert.False(engine.Diagnostics.ResolverRegistered);
        Assert.DoesNotContain("open", platform.Calls);
        Assert.Null(platform.Resolver);
    }

    [Fact]
    public void Failure_after_registration_keeps_the_resolver_fail_closed()
    {
        var platform = new FakePlatform { OpenError = new IOException("fixture load failure") };
        var engine = new ChartNativeTextBindingEngine(platform);
        Assert.Throws<InvalidOperationException>(() => engine.Initialize());
        Assert.True(engine.Diagnostics.ResolverRegistered);
        Assert.Equal(ChartNativeTextBindingStatus.Failed, engine.Diagnostics.Status);
        Assert.Throws<DllNotFoundException>(() => platform.Resolver!("libHarfBuzzSharp"));
        Assert.Equal((nint)0, platform.Resolver!("unrelated-native-library"));
        Assert.False(engine.Diagnostics.ResolverObserved);
        Assert.Throws<InvalidOperationException>(() => engine.Initialize());
        Assert.Equal(1, platform.Calls.Count(call => call == "open"));
    }

    [Theory]
    [InlineData("9.0.0", false, "NativeVersionMismatch")]
    [InlineData("8.3.1", true, "NativeSymbolMismatch")]
    public void Native_version_and_provider_are_verified_before_imports_are_allowed(string version, bool foreignProvider, string code)
    {
        var platform = new FakePlatform { NativeVersion = version, ForeignProvider = foreignProvider };
        var engine = new ChartNativeTextBindingEngine(platform);
        Assert.Throws<InvalidOperationException>(() => engine.Initialize());
        Assert.Equal(code, engine.Diagnostics.ErrorCode);
        Assert.Throws<DllNotFoundException>(() => platform.Resolver!("HarfBuzzSharp"));
    }

    [Fact]
    public void Resolver_observations_are_actual_and_previous_snapshots_remain_immutable()
    {
        var platform = new FakePlatform();
        var engine = new ChartNativeTextBindingEngine(platform);
        var before = engine.Initialize();
        foreach (var name in new[] { "libHarfBuzzSharp", "libHarfBuzzSharp.so", "HarfBuzzSharp" })
            Assert.Equal((nint)123, platform.Resolver!(name));
        Assert.Equal((nint)0, platform.Resolver!("other"));
        Assert.False(before.ResolverObserved);
        Assert.Equal(0, before.ResolverInvocationCount);
        var after = engine.Diagnostics;
        Assert.True(after.ResolverObserved);
        Assert.Equal(3, after.ResolverInvocationCount);
        Assert.Equal("libHarfBuzzSharp", after.ResolvedLibraryName);
    }

    [Fact]
    public void Null_builder_is_rejected_without_initialization() =>
        Assert.Throws<ArgumentNullException>(() => ChartAppBuilderExtensions.WithChartsNativeTextIsolation(null!));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Real_file_inspection_supports_runtime_and_flat_RID_deployment(bool flattened)
    {
        using var files = new AssetFiles();
        var path = files.Write(flattened, files.Bytes);
        var result = ChartNativeTextAssets.Inspect(files.Root, "linux-x64", files.Bytes.Length, files.Hash);
        Assert.Equal(path, result.Path);
        Assert.True(Path.IsPathFullyQualified(result.Path));
        Assert.Equal(files.Hash, result.Sha256);
    }

    [Fact]
    public void Both_matching_layouts_select_the_runtime_asset_deterministically()
    {
        using var files = new AssetFiles();
        var runtime = files.Write(false, files.Bytes);
        files.Write(true, files.Bytes);
        Assert.Equal(runtime, ChartNativeTextAssets.Inspect(files.Root, "linux-x64", files.Bytes.Length, files.Hash).Path);
    }

    [Fact]
    public void Corrupt_flat_shadow_is_rejected_even_if_runtime_asset_is_valid()
    {
        using var files = new AssetFiles();
        files.Write(false, files.Bytes);
        files.Write(true, files.Bytes.Select(b => (byte)(b ^ 1)).ToArray());
        var error = Assert.Throws<ChartNativeTextInitializationError>(() => ChartNativeTextAssets.Inspect(files.Root, "linux-x64", files.Bytes.Length, files.Hash));
        Assert.Equal("UnsupportedNativeAsset", error.Code);
    }

    [Fact]
    public void Missing_asset_does_not_search_other_directories()
    {
        using var files = new AssetFiles();
        var error = Assert.Throws<ChartNativeTextInitializationError>(() => ChartNativeTextAssets.Inspect(files.Root, "linux-x64", files.Bytes.Length, files.Hash));
        Assert.Equal("MissingNativeAsset", error.Code);
    }

    [Fact]
    public void Unknown_RID_cannot_be_used_as_a_relative_path()
    {
        using var files = new AssetFiles();
        var error = Assert.Throws<ChartNativeTextInitializationError>(() => ChartNativeTextAssets.Inspect(files.Root, "../linux-x64", files.Bytes.Length, files.Hash));
        Assert.Equal("UnsupportedArchitecture", error.Code);
    }

    private sealed class FakePlatform : IChartNativeTextPlatform
    {
        public bool IsLinux { get; set; } = true;
        public Architecture Architecture { get; set; } = Architecture.X64;
        public bool AvaloniaInitialized { get; set; }
        public bool IsAvaloniaInitialized => AvaloniaInitialized;
        public bool NativeLoaded { get; set; }
        public bool ForeignProvider { get; set; }
        public string NativeVersion { get; set; } = "8.3.1";
        public Exception? GlibcError { get; set; }
        public Exception? RegistrationError { get; set; }
        public Exception? OpenError { get; set; }
        public List<string> Calls { get; } = [];
        public Func<string, nint>? Resolver { get; private set; }
        private readonly string _asset = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "fixture-libHarfBuzzSharp.so"));
        public string GetGlibcVersion() { Calls.Add("glibc"); if (GlibcError is not null) throw GlibcError; return "2.43"; }
        public bool IsNativeTextLoaded() => NativeLoaded;
        public ChartNativeTextAsset FindAsset(string rid) { Calls.Add("asset:" + rid); return new(_asset, 42, "fixture-hash"); }
        public void PrepareManagedAssembly() => Calls.Add("managed");
        public void RegisterResolver(Func<string, nint> resolver) { Calls.Add("register"); if (RegistrationError is not null) throw RegistrationError; Resolver = resolver; }
        public nint OpenAsset(string path) { Calls.Add("open"); if (OpenError is not null) throw OpenError; return 123; }
        public ChartNativeTextSymbols InspectSymbols(nint handle, string assetPath)
        {
            Calls.Add("symbols");
            return new(NativeVersion, ForeignProvider ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "foreign.so")) : assetPath);
        }
    }

    private sealed class AssetFiles : IDisposable
    {
        internal string Root { get; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "native-text-test-files", Guid.NewGuid().ToString("N")));
        internal byte[] Bytes { get; } = "actual file fingerprint fixture"u8.ToArray();
        internal string Hash => Convert.ToHexString(SHA256.HashData(Bytes));
        internal AssetFiles() => Directory.CreateDirectory(Root);
        internal string Write(bool flattened, byte[] bytes)
        {
            var path = flattened ? Path.Combine(Root, "libHarfBuzzSharp.so") : Path.Combine(Root, "runtimes", "linux-x64", "native", "libHarfBuzzSharp.so");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
            return path;
        }
        public void Dispose()
        {
            var intendedParent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "native-text-test-files")) + Path.DirectorySeparatorChar;
            if (!Root.StartsWith(intendedParent, StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected fixture deletion path.");
            Directory.Delete(Root, true);
        }
    }
}
