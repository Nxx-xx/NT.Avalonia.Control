# NT.AvaloniaControl.PdfViewer

Independent desktop PDF reader built on PDFium and Avalonia. The functional package
provides the document engine and reader control; add `NT.AvaloniaControl.PdfViewer.Themes`
explicitly for the default interface, or supply your own control template.

The reader supports continuous pages, zoom and rotation, thumbnails, the document
outline, text search, and selectable/copyable text. Page rendering uses cancellation,
visible-page scheduling, and a bounded bitmap cache. Scanned PDFs display normally;
text search and selection require a PDF text layer and do not perform OCR.

## Document API

```csharp
using NT.AvaloniaControl.PdfViewer;

await using var document = await PdfDocument.LoadAsync(await File.ReadAllBytesAsync("manual.pdf"));
var page = await document.RenderAsync(0, 1200, 1600);
var text = await document.GetTextAsync(0);
var matches = await document.SearchAsync("Avalonia", matchCase: false);
```

`PdfDocument.LoadAsync(byte[])` copies its input and owns the copy until disposal.

`PdfDocumentSource` adds owned bytes, local files, and reopenable stream factories.
The reader exposes bindable metadata, permissions, capability/state/session snapshots,
typed errors and commands. Copying follows PDF permissions and host policy; search
remains a reading operation. Unimplemented viewer editing, save, print and share
capabilities stay disabled even when a host service is registered.

`Dispose()` stops admission and initiates cancellation without waiting for native
close. Await `DisposeAsync()` (or the reader's `CloseDocumentAsync()`) when actual
handle/input release must finish. Native calls remain process-wide serial, with
foreground/search/thumbnail scheduling at 4:2:1; running native calls are cooperative
and cannot be forcibly interrupted. See the repository's P1 contracts and validation
records for migration details and Windows/Ubuntu evidence boundaries.
The stream overload reads from the current position and closes the stream unless
`leaveOpen: true` is supplied. `PdfDocumentException.ErrorCode == 4` means the PDF
requires a password or the supplied password was incorrect. Pass the password when
loading; unsupported security handlers and invalid documents produce explicit errors.

Page sizes and text/search rectangles use PDF points (72 per inch), with a displayed
top-left origin. They include the PDF's crop and intrinsic page rotation, before
the viewer's additional clockwise rotation. Character indices refer to PDFium's
character sequence and need not equal UTF-16 offsets in `PdfTextPage.Text`.

All native access is serialized across every document because PDFium is not thread
safe. Native work runs away from the UI thread, and progressive rendering observes
cancellation at PDFium's safe points. Dispose documents when no longer needed.
Single renders are limited to 16384 pixels per side and 64 megapixels; a viewer
can choose a lower resolution to stay within its memory budget.

## Bounded FreeText writing backend

```csharp
using NT.AvaloniaControl.PdfViewer.Editing;

await using var edit = await PdfEditSession.OpenAsync(await File.ReadAllBytesAsync("manual.pdf"));
var font = new PdfEmbeddedTrueTypeFont(await File.ReadAllBytesAsync("licensed-static-font.ttf"));
var note = await edit.AddFreeTextAsync(0, new PdfEditRectangle(40, 300, 300, 50),
    "中文批注 ABC", font, fontSize: 18);
await edit.SaveAsync("annotated.pdf");
```

This backend is implemented in the functional package, using precisely pinned
PDFsharp Core `[6.2.4]` for structural writing and PDFium for embedded-font APs.
It has no Python or system-font runtime dependency. It supports single-line,
black FreeText with a supplied static glyf TrueType font, persisted annotation IDs,
reopening/updating, owned byte export, stream save and same-directory atomic file
replacement. Editing rectangles use the PDF bottom-left origin before crop/rotation,
unlike the reader's displayed text rectangles.

The one-argument font constructor now selects `PdfFontEmbeddingMode.AutomaticSubset`.
A managed C# subsetter keeps requested glyphs, `.notdef` and recursive composite
dependencies, compacts glyph IDs, and rebuilds Unicode maps, metrics and checksums.
The work runs before native AP generation, outside the global PDFium gate. Fonts
declaring `NoSubsetting` remain complete; restricted, preview-only and bitmap-only
embedding is refused. Select `new PdfEmbeddedTrueTypeFont(bytes, PdfFontEmbeddingMode.Full)`
to embed the supplied font program unchanged. No font fallback, complex shaping,
RTL, wrapping, CFF/TTC/variable fonts, undo/history, annotation UI or general
partial-object redaction is provided. Missing glyphs, unsupported text and restricted
embedding declarations are rejected before committing an edit. The host remains
responsible for its font license. Input is limited to 256 MiB, font input to 32 MiB.

Appearances use explicit Unicode-scalar CIDs, UTF-16BE ToUnicode mappings and a
CID-to-glyph map. Supplementary CJK characters and Unicode aliases sharing a glyph
retain their original text. Updates require the original supplied font and produce
a fresh subset that includes newly added characters; a saved subset cannot supply
glyphs it never contained. Page/AP/AcroForm font aliases are replaced for that stable
annotation ID. The tested corpus covers three OFL static TrueType families, SC/TC,
Japanese, precomposed Korean, Ext-B, composite outlines and cmap 4/12. This does not
establish arbitrary CJK fonts, variation sequences, vertical text, external editor
interaction or Linux acceptance. Tiny fonts already containing only requested
glyphs may grow slightly from canonical table/name overhead.

Structural sessions reject encrypted and signed inputs. PDFsharp rewrites the file
and updates Producer/XMP metadata; this is not a lossless byte editor. Current
preservation checks cover page content, fields/APs, outlines, attachments and a
custom dictionary. Arbitrary PDF feature preservation and Linux physical acceptance
remain open. `ExportAsync` does not clear `IsDirty`; successful `SaveAsync` does.
Stream output stays open at its current position and may be partial on failure.
The reader control continues to consume separate immutable document snapshots.

## Native deployment

The package depends on the non-V8 `bblanchon.PDFium.Win32`,
`bblanchon.PDFium.Linux`, and `bblanchon.PDFium.macOS` native packages. Preserve their
runtime assets during publishing, and publish for the intended runtime, for example
`dotnet publish -r win-x64`. Native platform verification is recorded in the
repository's PDF viewer documentation; inclusion of an asset is not a verification
that every operating system and architecture has been tested.

This package does not use Avalonia's commercial PDF Viewer. The reader control
does not yet expose annotation editing UI, interactive form UI, OCR, printing or
browser/mobile targets. PDFium renders existing annotations; the separate bounded
FreeText backend above provides code-driven writing.

Retain `THIRD-PARTY-NOTICES.md` and the native distribution's license information
when redistributing the native libraries.
## Explicit hidden-information removal and whole-object redaction

`PdfEditSession` provides a bounded C# pipeline; Python is used only by the independent verification tools. Callers must explicitly select `PdfHiddenInformationPolicy.RemoveAllNonPageData`. This destructive policy removes all annotations and their visible appearances, widgets and AcroForm values/defaults/AP, outlines, attachments, actions, original Info/XMP, structure, thumbnails and private dictionaries. It keeps supported page painting and geometry, and prunes unused page/Form resources. It does not flatten annotations or form fields.

```csharp
await using var edit = await PdfEditSession.OpenAsync(inputBytes, cancellationToken);
var result = await edit.ApplyRedactionsAsync(
    [new PdfRedactionRegion(0, new PdfEditRectangle(40, 200, 240, 40))],
    PdfHiddenInformationPolicy.RemoveAllNonPageData, cancellationToken);
await edit.SaveAsync(outputPath, cancellationToken);
```

Regions use bottom-left PDF coordinates before page crop/rotation. Every selected text, image, path or whole Form invocation must fit completely inside a region; empty selections and partial intersections fail. Affected pages currently require no rotation, transparency or custom clipping. Removing a shared Form invocation affects that page instance; content deliberately retained on another page remains in the file. Use `SanitizeAsync(policy, token)` for cleanup without deleting page objects.

The pipeline cleans and validates supported painting, removes objects, regenerates content, prunes live resources into a fresh catalog, fully rewrites, and independently reopens/audits the candidate before one session commit. Failure/cancellation retains the prior bytes, revision and dirty state. `ExportAsync` and the existing save methods persist only the committed snapshot. No black cover or incremental revision is used. The returned SHA-256 values identify the input/output snapshots.

Compatibility is deliberately bounded: optional content, marked-content properties, Type3 fonts, patterns/shadings, non-device/non-indexed color spaces, opaque JPEG/JPX/JBIG2 containers, unsupported filters/resources and non-default UserUnit are refused. The output may contain newly generated technical PDFsharp metadata, never copied source metadata. Retained fonts and image pixels remain rendering data; this is not a general detector for steganography or arbitrary covert data. Deleting portions of text glyphs/images/paths, general nested Form editing, editing UI, secure erasure of caller buffers/filesystem history, Linux validation and full official parity remain separate work.
