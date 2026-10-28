# NT.AvaloniaControl.Charts.Skia

Optional Skia rendering for dense circle series. Call `WithChartsSkiaRendering()`
on the application builder, or `ChartSkiaRenderingExtensions.Initialize()` during
UI startup. The adapter registers a provider; it does not change software/GPU
renderer selection, source data, sampling, or hit testing. Core works without it.

This package pins both Avalonia and Avalonia.Skia to exactly **12.0.3** and uses
the SDK's explicit `AvaloniaAccessUnstablePrivateApis` opt-in. Its APIs are not
stable across Avalonia versions. Revalidate the adapter before changing the pins.
Only AVA3001 is allowed as a warning in this project; other warning and audit
checks retain their normal policy.

Eligible unit-scale raster surfaces reuse an ellipse and its widened stroke
outline, preserving both paint operations in point order. Unknown backends,
picture recording, GPU surfaces, non-unit transforms, spatial brushes and unsupported pens
retain the original ellipse commands. The Core logical cache budget includes
the reserved managed weight. Actual native/GPU allocation remains outside that
existing accounting model.

A series with one chart owner also keeps a bounded history of exact per-point
geometry across source and view replacements. A persistent per-series entry
charges its entire capacity across scene invalidation, including while a small
viewport uses the original ellipse path. Scene entries charge only their own
payload, so the history is not counted twice. Lowering the budget or evicting
that entry retires dependent plans as well. The history grows only during plan creation, holds at
most 32,768 entries, and is cleared when its owner releases rendering resources.
Previously recorded frames keep valid geometry references after that release.

The dense-circle fast paint path additionally reads the pinned backend's public
`RenderOptions` and `FillPath` getters. Their declaring Skia types are internal,
so this is a version-specific metadata bridge rather than a stable C# API. It
never reads private fields or invokes nonpublic getters. It requires the exact
12.0.3 informational version, a software surface with unit device scale and a
finite integer device translation, and ambient opacity equal to one. Fractional
translations, non-unit scales, skew and perspective retain the original path.
It resolves every path and prepares
both paints before drawing; unsupported metadata/state retains the native
geometry route. AOT or unavailable dynamic-code support also retains that route,
as do trimmed deployments whose required public metadata has been removed.
The retained native pair owns both geometries and the cached path references;
releasing a frame does not dispose geometry still used by another frame.
