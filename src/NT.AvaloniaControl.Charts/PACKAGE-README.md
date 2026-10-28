# NT.AvaloniaControl.Charts — preview

Avalonia charts for Cartesian, statistical, financial, hierarchy, relationship, geographic and dashboard scenarios.

This preview targets .NET 10 and Avalonia 12.0.3. Install `NT.AvaloniaControl.Charts.Themes` for the default chart theme. `NT.AvaloniaControl.Charts.Skia` is an optional adapter pinned to Avalonia 12.0.3.

```csharp
using Avalonia;
using NT.AvaloniaControl.Charts;

public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
    .WithChartsNativeTextIsolation()
    .UsePlatformDetect();
```

Call the native-text extension before Avalonia setup or any HarfBuzzSharp call. Linux/glibc applications must deploy the matching native assets and fonts; failures are reported explicitly. Windows reports this initialization as not applicable.

```csharp
var chart = new CartesianChart();
chart.Series.Add(new LineSeries
{
    ItemsSource = new[] { new ChartPoint(0, 2), new ChartPoint(1, 4), new ChartPoint(2, 3) },
    ShowMarkers = false,
});
```

For default styling, add `new NT.AvaloniaControl.Charts.Themes.ChartsTheme()` to the application's styles after installing the Themes package.

Use ordinary JIT and framework-dependent directory deployment for this preview. Trimming, NativeAOT, single-file, ARM64 and physical-device certification are not qualified. Linux support is limited to the recorded preview test environments. Dense high-frequency scatter has an open performance limit; no official-chart parity or compositor frame-rate claim is made.

Source data identity, current view and export options are part of the API contract. Access chart controls and mutate chart buffers on the UI thread; asynchronous export captures UI state before background encoding. Cancel and dispose operations according to their API contracts.

See the accompanying preview release notes and THIRD-PARTY-NOTICES. Project distribution license is pending; this candidate is prepared for private evaluation.
