# Ordinary package consumer startup verification

This fixture is copied into a standalone directory with only PackageReference dependencies. It calls the production `WithChartsNativeTextIsolation()` API before platform setup; it never uses the acceptance-host workaround flag or a module initializer.

The caller first builds and packs the current Charts package into a local feed, then runs:

```powershell
./scripts/Test-ChartsNativeTextStartup.ps1 -Feed <local-feed> -OutputDirectory <new-evidence-directory> -SystemHarfBuzzPath /absolute/system/libharfbuzz.so -FontDirectory /absolute/test-fonts
```

On Windows the system-library parameter is omitted. Every required case is a separate supervised process. Linux additionally records an unmodified baseline with explicitly controlled `dlopen(RTLD_NOW|RTLD_GLOBAL)` of the supplied system library. A baseline crash, missing terminal report, graphics failure, or successful non-reproduction is retained exactly; none is automatically classified as a font fix.

`missing` temporarily renames only the selected native asset inside an owned cloned consumer output, restores it, and verifies sticky failure. The package cache and system libraries are never changed. `late`, `native-loaded`, and `resolver-conflict` are distinct failure-policy tests and do not count as native window startup passes. `normal` and `idempotent` exercise a real Desktop window, original chart point identity, production resolver/module diagnostics, and baseline/hidden/restored F glyph PNGs. The independent Python oracle decodes all PNG pixels and checks the chart glyph against a native TextBlock reference.

Each child receives fresh XDG cache/config, dotnet home and temporary directories. Linux also gets its own FONTCONFIG_FILE (including `/etc/fonts/fonts.conf` plus the optional explicit font directory) and an empty private cachedir, with the actual config hash and initial file count recorded. The inherited shared project font configuration is not used. Existing system-wide font caches remain outside this test's control. `-DependencyFeed` accepts additional local dependency mirrors while preserving normal NuGet audit; the Charts package is mapped to the explicit product feed and its assembly bytes are checked against that nupkg. The images are offscreen readbacks of a live native window, not compositor screenshots or physical-device/performance certification. Framework-dependent publish, single-file deployment, trimming and AOT require separate acceptance.
