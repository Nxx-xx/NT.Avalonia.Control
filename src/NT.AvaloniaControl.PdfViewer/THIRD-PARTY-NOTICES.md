# Third-party notices

This library calls the public PDFium C API. It does not contain or depend on
Avalonia's commercial PDF Viewer implementation.

## PDFium native version and notice coverage

The native dependencies are `bblanchon.PDFium.Win32`, `bblanchon.PDFium.Linux`,
and `bblanchon.PDFium.macOS`, version `155.0.8044`. They correspond to the official
[PDFium 155.0.8044.0 / chromium/8044 release](https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium%2F8044)
from the `bblanchon/pdfium-binaries` project.

`Licenses/PDFium-ThirdParty/<runtime>/` retains the root license and the complete
`licenses/` text directory supplied in each corresponding official release archive.
All 12 runtime assets present in the native NuGet packages are covered:

- Windows: `win-arm64`, `win-x64`, `win-x86`.
- Linux glibc: `linux-arm`, `linux-arm64`, `linux-x64`, `linux-x86`.
- Linux musl: `linux-musl-arm64`, `linux-musl-x64`, `linux-musl-x86`.
- macOS: `osx-arm64`, `osx-x64`.

Every archive's SHA-256 was checked against the official GitHub release API.
Every native library inside those archives was also checked against the installed
NuGet runtime asset and matched byte for byte. Only the 180 original license text
files were extracted; native libraries were not copied into this source directory.

The packaged
`Licenses/PDFium-ThirdParty/verification-manifest.json` records archive URLs,
archive and native library SHA-256 hashes, and each retained notice's hash.
`Licenses/PDFium-155.0.8044-NOTICE-SOURCES.md` records provenance and the upstream
build recipes. These files reproduce the notices supplied by the upstream native
distributor; they are not an independently generated inventory of linked code.

## PDFium

- Upstream: https://pdfium.googlesource.com/pdfium/
- Copyright 2014 The PDFium Authors. Original code copyright 2014 Foxit Software Inc.
- The matching release's PDFium root license is reproduced in
  `Licenses/PDFium-LICENSE.txt` and in each runtime's `licenses/pdfium.txt`.

The retained upstream notices also cover the bundled components identified by
the release's license directory, such as FreeType, ICU, libjpeg-turbo, OpenJPEG,
libpng, Little CMS, zlib, and platform-specific runtime components. The original
notice text and copyright statements govern each component. Keep the matching
runtime's entire notice directory when redistributing the native library.
Some upstream text files use Windows-1252; their original bytes were preserved.

## bblanchon PDFium binaries

- Project: https://github.com/bblanchon/pdfium-binaries
- Matching build/packaging commit:
  `5453f3afc4785cbad82c05f6ceb4dabea0cb81a0`.
- Build project license: MIT, reproduced in
  `Licenses/pdfium-binaries-LICENSE.txt` and each runtime's `LICENSE.txt`.
- The NuGet packages declare Apache-2.0 in their package metadata. The native
  PDFium library and its bundled components retain their respective notices;
  the build project's MIT license does not relicense those components.

The non-V8 native packages are used; this library does not enable PDF JavaScript.
Recheck and refresh the version-specific notices when updating native dependencies.

## PDFsharp Core 6.2.4 and its managed dependencies

The bounded structural writing layer uses the platform-independent `PDFsharp`
Core package, exactly version `6.2.4`. This version is MIT licensed. Its matching
upstream license is retained without modification in
`Licenses/PDFsharp-6.2.4-LICENSE.txt`.

- Upstream source tag: https://github.com/empira/PDFsharp/tree/v6.2.4
- Exact package: https://www.nuget.org/packages/PDFsharp/6.2.4
- Copyright (c) 2001-2026 empira Software GmbH, Troisdorf (Cologne Area), Germany.
- The GDI/WPF builds and MigraDoc are not dependencies of this package.

For the current `net10.0` target, the writing layer resolves the following additional MIT
licensed packages: `Microsoft.Extensions.Logging.Abstractions` `8.0.3`,
`Microsoft.Extensions.DependencyInjection.Abstractions` `8.0.2`, and
`System.Security.Cryptography.Pkcs` `10.0.13`. The latter is an explicit security
override of PDFsharp's minimum dependency; the rejected `8.0.1` version is not
part of the current dependency set. Their exact upstream `LICENSE.TXT`
and `THIRD-PARTY-NOTICES.TXT` contents are retained in the `retainedTextFiles`
section of `Licenses/PDFsharp-6.2.4-SOURCES.json`, with original byte lengths,
SHA-256 values, and per-package references. Re-encoding each `text` value as
UTF-8 reproduces its original bytes. Identical upstream notice texts are stored
once and referenced by hash.

That provenance file also records the official NuGet download URLs, a fresh
download-to-cache SHA-256 comparison for all four packages, package file SHA-512,
NuGet restore content hashes, and hashes of the selected managed runtime DLLs.
The upstream Microsoft notice text includes runtime-wide notices; retaining it
does not assert that every component listed there runs in this PDF feature.

Keep the exact version pin and retained notices when redistributing this writing
layer. PDFsharp `6.5.0` declares AGPL-3.0-only instead of MIT; do not substitute
that version or use a floating version range without a fresh license and
compatibility review. Caller-supplied annotation font embedding permissions are
separate from the PDFsharp license; this adoption bundles no production font.
