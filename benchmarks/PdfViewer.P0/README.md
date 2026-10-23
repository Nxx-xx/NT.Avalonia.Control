# PdfViewer P0 verification harness

This console project probes the pinned PDFium `155.0.8044` ABI and verifies the functional package's bounded production FreeText backend. Every native operation in a process is serial. Probes run in separate processes so a native failure cannot be mistaken for another probe's success.

From the repository root, run:

```powershell
pwsh -NoProfile -File scripts/Run-PdfViewerP0.ps1 `
  -PythonPath python -OpenSslPath openssl -PdfToPpmPath pdftoppm -IncludeProductionRedaction
```

The Python environment needs `pypdf`, `cryptography` and Pillow. Poppler supplies `pdftoppm`; OpenSSL supplies CMS verification. These are research/verification tools, not product dependencies. The repository carries a 7 KB OFL font fixture and its provenance/license. `fontTools` is only needed to regenerate that fixture with `prepare_annotation_font.py`.

`Freeze-PdfViewerBaseline.ps1` preserves a source manifest/archive, native asset/header hashes, target availability and seven official document snapshots. Existing snapshots are verified before reuse. Public documentation does not freeze a commercial package version. New runs need network access for the document snapshots; the first recorded run can be replayed with `-OutputDirectory artifacts/pdfviewer/20261015T072111Z-p0` while its local snapshots remain present.

The runner builds in Release, executes `save`, `fonts`, `forms`, `redaction`, `baseline`, performs historical writer bridge experiments and independently checks PDF structure/CMS/Poppler output. `-IncludeProductionWriter` also runs the C# `production` path and advanced deletion corpus, including independent raster/structure checks and oracle negative controls. The current Core/Headless suites include production writing regressions. Each report records the loaded native module hash and managed assembly hash. Unexpected checks return nonzero; stale success reports are cleared before execution.

`-IncludeProductionRedaction` includes all of those probes and adds `production-redaction`, exercising the shipped `SanitizeAsync` / `ApplyRedactionsAsync` pipeline with explicit `RemoveAllNonPageData`. Its independent verifier parses all current xref objects/decompressed streams, checks page/Form resource closures, page-scoped text and Poppler preservation pixels, and runs fail-closed negative controls. The shared Form intentionally remains on the unselected page. It verifies whole-object deletion, not arbitrary partial text/image/curve editing or arbitrary PDF forensic erasure. The fixture is self-authored and licensed for repository verification.

`-IncludeFontSubsetting -FontToolsPythonPath <python-with-fontTools>` includes both earlier options and adds three pinned OFL full CJK fonts, the `font-subset` public probe and `verify_font_subset.py`. `-FontSourceCache <directory>` reuses hash-checked downloads; otherwise preparation downloads official sources listed in `Fixtures/font-subset-sources.json`. Large fonts stay in the run's `font-subset/sources`, outside product packages. The independent fontTools process checks outlines/metrics/composite closure and sfnt integrity; pypdf checks scalar CID mappings and resources, while Poppler compares Full/Auto pixels. Ten cases include SC/TC/JP/KR, Ext-B, cmap4/12, composite outlines, NoSubsetting and Unicode aliases. Python/fontTools remain verification-only dependencies. The `production` regression explicitly uses Full; the new probe tests default AutomaticSubset as well as Full. Use `export_font_subset_record.py` after all visual/package/audit evidence is complete.

Important result meanings:

- `limited-feasible` proves the stated small scope, not production editing.
- The FreeText negative control deliberately demonstrates an unresolved font resource. The separate writer bridge retains annotation AP resources; it does not implement a production managed writer.
- The newer `production` probe uses the shipped C# `PdfEditSession` and precisely pinned MIT PDFsharp 6.2.4. It has no Python runtime bridge. Its font is explicitly supplied and fully embedded; automatic subsetting/shaping remains open.
- Redaction native candidates include unsafe counterexamples. The independent audit must recognize them as unsafe. Partial character/image requests are explicitly refused; candidate PDFs are research evidence.
- Synthetic backend timings are not UI p95, peak-memory acceptance or an official comparison. Linux and physical IME require their own runs.

To export a compact review record after a successful run:

```powershell
python benchmarks/PdfViewer.P0/export_p0_record.py --run artifacts/pdfviewer/RUN `
  --output artifacts/pdfviewer/RUN/verification-record.json --openssl openssl --poppler pdftoppm
```

The tracked conclusions and open gates are in `docs/pdfviewer-p0-validation.md` and `docs/validation/pdfviewer-p0-20261015.json`. Raw PDFs, images, logs and snapshots stay under the ignored `artifacts/` directory.
