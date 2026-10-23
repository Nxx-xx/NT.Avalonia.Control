# Isolated PDF Viewer package consumer

`scripts/Test-PdfViewerPackageConsumer.ps1` materializes this fixture into a fresh `artifacts/consumer/<guid>` directory for each mode. The generated project consumes actual local-feed NuGet packages with its own empty package cache and no project references.

- `FunctionOnly` references `NT.AvaloniaControl.PdfViewer` and supplies a custom AXAML template containing the two required presenter parts. No Themes package or assembly may appear in its restored graph/output.
- `DefaultTheme` references `NT.AvaloniaControl.PdfViewer.Themes` and loads its packaged `Generic.axaml`, including toolbar/search and sidebar resources.

Both run real PDFium native document loading, rasterization and cross-page searching. Compiled AXAML binds `Source` to a local path and `CurrentPage` to a view model; checks cover continuous layout, cached page decoding, selection, navigation, source replacement and disposal. `SamplePdf.cs.template` is a standalone copy of the deterministic three-page sample generator, so the consumer requires neither repository projects nor downloaded PDFs.

The fixture runs asynchronous control work inside `HeadlessUnitTestSession`, which supplies a real Avalonia dispatcher loop. Its layout checks complement the native desktop smoke; they do not claim physical input or platform-window validation.

```powershell
pwsh -File scripts/Test-PdfViewerPackageConsumer.ps1
pwsh -File scripts/Test-PdfViewerPackageConsumer.ps1 -NoPack -PackageFeed artifacts/packages
```

Per-mode evidence remains in the consumer directory. The aggregate result is written to `artifacts/pdfviewer-package-consumer-results.json`.
