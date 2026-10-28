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
RTL, wrapping, CFF/TTC/variable fonts, annotation UI or general
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

## Bounded undo history, transactions and user-bookmark persistence

Content edits (FreeText, sanitization, redaction) record bounded undo/redo history:
`UndoAsync` / `RedoAsync` / `CanUndo` / `CanRedo`, `UndoLimit` (entries per stack,
default 100) and `HistoryByteLimit` (retained snapshot bytes, default 64 MiB).
Both limits trim immediately when lowered; the newest undo entry is always
retained, so recorded bytes can exceed the budget by that one entry. A new edit
clears the redo stack. `ExecuteTransactionAsync` runs a group of edits as one
atomic, single-history-entry transaction and rolls bytes, revision and dirty state
back on any failure; nesting, undo/redo, saves and bookmark changes are refused
inside the body. `IsDirty` compares content state identity plus the bookmark
overlay, so undoing back to a save point clears it and save failures retain it.

User bookmarks follow separate rules. `SetUserBookmarksAsync` replaces the session
set (validated: unique non-empty IDs, 1–512 UTF-16 code-unit titles, zero-based
page index, `None`/`Bold`/`Italic` flags; identical set is a no-op). The set is
persisted in a private catalog entry (`/NTUserBookmarks`) that other readers
ignore: it is stripped into session overlay state at open, read back through
`GetUserBookmarksAsync`, and injected into exported and saved bytes. Bookmarks
never enter or roll back with the content history; sanitization and redaction
clear them by policy. Malformed or out-of-range persisted blocks are rejected at
open instead of being silently dropped.

Export and save reuse one injected byte array per session state, so repeated
outputs of the same state are byte-identical. Byte-for-byte reproducibility across
separate sessions is not guaranteed: PDFsharp 6.2.4's save preparation replaces the
catalog `/Metadata` with a freshly generated XMP packet (random document IDs) on
every structural save.

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

## Viewer save workflow (P4-02)

Set `IsReadOnly=false` for the supported unsigned, unencrypted PDFs up to 256 MiB.
`SaveAsync()` saves to `SavePath` (or requests a picker); `SaveAsAsync(path)` adopts
the new local target. `SaveAsync(Stream)` leaves the stream open and marks the
snapshot saved; `ExportDocumentAsync()` returns owned bytes without clearing Dirty.
`AutoSave` debounces changes at `AutoSaveInterval` (default 2 seconds), only with an
established local target. All content edits and saves run in one ordered control queue.

`ExecuteEditAsync` wraps supported host content edits in one backend transaction and
refreshes the reader snapshot. `UndoAsync` / `RedoAsync` and commands refresh the
page while preserving logical session identity; bookmarks persist separately.
Writable loads initialize the edit snapshot and persisted private bookmarks; for an
already open reader, await `EnableEditingAsync()` after enabling writes.

The default theme has Save / Save As / Undo / Redo and an unsaved prompt. Hosts may
replace confirmation through `UnsavedChangesRequested` or `IPdfUnsavedChangesService`.
Window closing must await `RequestCloseDocumentAsync()` before closing / disposal;
`DisposeAsync` unconditionally releases resources. See the
[P4-02 contract and validation](../../docs/pdfviewer-p4-02-validation.md) for file
picker, failure, AutoSave, readonly bookmark and platform boundaries.

## File conflicts and interrupted saves (P4-03)

Original-path saves compare the full content fingerprint captured when editing was
initialized or the last file save committed. `PdfSaveConflictException`,
`LastSaveConflict` and `SaveConflict` preserve the external file and current Dirty
state; AutoSave pauses until a different target is saved successfully or the source
is reloaded. Save As to the same original path retains conflict protection.

The file backend uses a private same-directory transaction with a flushed journal,
validated candidate, final target comparison and atomic replacement. A stable empty
`.nt-pdf-save-*.lock` file coordinates cooperating writers and intentionally survives
cleanup. `SaveAsync(path, PdfFileSaveOptions)` lets standalone editing sessions supply
an original `ExpectedTarget`; without it, the target is captured when that save starts.

After restarting, hosts explicitly inspect `PdfSaveRecovery.ScanAsync(directory)`.
`RecoverAsync(entry.TransactionDirectory)` revalidates the target and complete PDF,
then commits or reconciles an already committed output; `DiscardAsync` only removes
known files of an inactive valid transaction. Partial, changed, invalid or linked
transactions are never blindly replayed. This recovers interrupted save transactions,
not edits that never began saving or the in-memory undo stack.

Windows staging uses an owner-only ACL; Unix staging requires enforced 0700 mode,
with 0600 candidate / journal files and existing target mode preserved on replacement.
Filesystems that cannot enforce private Unix staging are refused. The v2 protocol
keeps independent output bytes and the atomically displaced file (Windows ReplaceFile
backup / Linux RENAME_EXCHANGE). Races detected after publication raise a conflict
with WasPublished and retained paths; Dirty remains and AutoSave pauses. PublishedConflict
entries refuse automatic replay/discard. ReadVersionAsync returns a selected retained
version, up to 256 MiB. Use an explicit new target to resolve it.

PdfSaveDurabilityException / LastSaveDurabilityFailure also pause AutoSave and preserve
Dirty. DurabilityPending recovery can repair a partial commit acknowledgement after
revalidating the target and displaced version. PdfFileSaveResult.Durability distinguishes
Linux file+directory fsync from Windows post-rename file/commit flushes.

Actual bounded NTFS/ext4 full disks, SMB tree disconnects, transient NFS link loss and
three Linux VM power cuts have dedicated evidence. NFS existing-target replacement is
refused when atomic exchange is unavailable; new-target publication never replaces an
existing entry. NFSv4 kernel I/O during permanent disconnection can wait beyond cancellation;
hosts need process isolation/timeouts. This is not filesystem CAS or protection from
later external writes. Windows VM power cuts were deferred by the user; physical device
power removal and complete metadata preservation remain open. See the
[storage validation record](../../docs/pdfviewer-p4-03-storage-validation.md).
