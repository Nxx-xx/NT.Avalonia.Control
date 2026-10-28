# Charts desktop platform acceptance

This host exercises real Win32/X11 windows for C7-02. It is a correctness and environment-observation tool, not a frame-rate benchmark or an official-runtime comparison. The actual renderer, window scale, font observations and input source must be retained with the results.

Run the bounded automatic cases from the repository root:

```powershell
pwsh -NoProfile -File scripts/Invoke-ChartsPlatformAcceptance.ps1 `
  -NativeMode DesktopSynthetic -OutputDirectory artifacts/charts-platform-acceptance/windows-new-run
```

```bash
bash scripts/Invoke-ChartsPlatformAcceptance-Linux.sh --mode desktop \
  -OutputDirectory artifacts/charts-platform-acceptance/linux-new-run
```

Use `--mode xvfb` only for a separately labelled virtual-display run. Output directories must be fresh. Each case runs in its own supervised process; failed, unsupported and timed-out cases retain their evidence. Normal NuGet audit remains enabled. Custom `-CasesPath` plans explicitly list renderer, culture, theme, expected actual scale and optional Linux process-only screen scale; a request is never a substitute for the observed value. The defaults are orthogonal cases, not every combination of the dimensions.

Linux `processScreenScale` is a child `AVALONIA_GLOBAL_SCALE_FACTOR` multiplier, with indexed screen overrides cleared only for that child and both original values restored afterward. The four default scale cases assume an observed base scale of 1. On another base scale, supply an explicit plan with the desired multiplier and independent `expectedScale`; the host and oracle require the actual scale before and after the scenes. No OS display setting is changed. A blacklisted software GL adapter remains a failed explicit GPU request.

For external system-input checking, start the built host with `--interactive`, a fresh `--output` directory and the normal renderer/culture/theme options. The host publishes readiness and records the actual window's routed events and state. The operator drives only that returned window, records which actions were system-injected or physical, and finishes with “保存并结束验证”. Automatic scenarios do not manufacture a physical-device pass. No component changes Windows display settings or installs global input hooks.

Interpret window-frame Skia/GL observations separately from offscreen `RenderTargetBitmap` captures. A GPU API context can still use a software adapter. Record the actual GL vendor/renderer when available and keep unknown devices unqualified. Export raster DPI is independent of desktop `RenderScaling`; Linux process-scale cases exercise the X11 client and do not certify physical mixed-DPI monitors.

Window-tree readback uses a neutral 96-DPI bitmap and maps the complete live Window client visual through an explicit scale into the actual native-scale pixel size. This avoids nesting the bitmap's initial DPI with custom-drawing clips. The output remains offscreen evidence, separate from desktop capture and compositor timing.

The result and remaining hardware gaps are documented in [the C7-02 report](../../docs/charts-c7-platform-acceptance.md). This batch does not upload its screenshots or diagnostics to a new CI destination.

On glibc Linux, the explicit `-LinuxNativeTextIsolation` runner switch passes `--isolate-native-text` to this host. Before Avalonia initializes, it loads the pinned packaged HarfBuzzSharp asset with `RTLD_NOW|RTLD_LOCAL|RTLD_DEEPBIND`, keeps its handle for the process lifetime and registers a resolver only for that managed assembly. The plan, actual resolver invocation, native path/hash, flags and loaded module are checked independently. This mode defaults to off, is rejected on Windows, and does not configure Charts, Demo, Usage or ordinary application consumers. It is a scoped diagnostic mitigation for the native symbol collision documented in [upstream issue 3038](https://github.com/mono/SkiaSharp/issues/3038); the unmodified consumer startup issue remains open.
