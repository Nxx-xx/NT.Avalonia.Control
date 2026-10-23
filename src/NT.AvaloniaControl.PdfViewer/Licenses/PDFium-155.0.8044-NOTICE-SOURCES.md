# PDFium 155.0.8044 notice provenance

Verified on 2026-10-15. The complete notice text directories supplied in the official
native release archives are retained in `PDFium-ThirdParty/<runtime>/`.
The root archive `LICENSE` was renamed `LICENSE.txt`; all retained file contents
are byte-for-byte copies. No native binary is stored in this license directory.

## Version match and official sources

The installed `bblanchon.PDFium.Win32`, `bblanchon.PDFium.Linux`, and
`bblanchon.PDFium.macOS` packages are version `155.0.8044`. Their repository commit
is `5453f3afc4785cbad82c05f6ceb4dabea0cb81a0`, the target of the official
[chromium/8044 release](https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium%2F8044).

The release is named `PDFium 155.0.8044.0`, was published on 2026-09-07,
and has GitHub release ID `384053218`.
Its [official API metadata](https://api.github.com/repos/bblanchon/pdfium-binaries/releases/384053218)
supplies archive URLs and SHA-256 digests. The release states that it was built from
PDFium branch `chromium/8044`.

The NuGet package specification includes the native libraries but does not package
the release archives' generated `licenses/` directories. This functional package
therefore preserves those directories separately for all 12 supported runtime assets.

## Verification performed

For every runtime listed in `PDFium-ThirdParty/verification-manifest.json`:

1. Downloaded the matching non-V8 `.tgz` from the exact official release and verified
   its SHA-256 digest against the official API metadata.
2. Streamed the native library from the archive through SHA-256 and compared it
   with the corresponding installed NuGet runtime asset. All 12 pairs matched.
   The native libraries were not extracted or executed during this audit.
3. Read the archive's version metadata and checked build `155.0.8044`.
4. Extracted only the root `LICENSE` and all regular text files under `licenses/`,
   preserving their bytes. Rejected absolute paths, traversal, links, binary NULs,
   and unexpectedly large notice files.
5. Recorded each notice's original archive path, output path, size, original text
   encoding, and SHA-256 in the packaged verification manifest.

There are 15 retained text files per runtime, 180 in total. Each directory contains
14 component notice files and the root archive license.

The notice directories are identical across architectures within each of these
four groups, as shown by their manifest hashes:

- Windows: `win-arm64`, `win-x64`, `win-x86`.
- Linux glibc: `linux-arm`, `linux-arm64`, `linux-x64`, `linux-x86`.
- Linux musl: `linux-musl-arm64`, `linux-musl-x64`, `linux-musl-x86`.
- macOS: `osx-arm64`, `osx-x64`.

All runtime directories are retained separately for straightforward redistribution.
The official notice set is preserved in full; this record does not claim an
independent audit of the upstream distributor's license collection logic.

## Upstream generation recipes

The matching repository commit documents the source of the retained files:

- [07-stage.sh](https://github.com/bblanchon/pdfium-binaries/blob/5453f3afc4785cbad82c05f6ceb4dabea0cb81a0/steps/07-stage.sh)
  stages the build project's MIT license.
- [08-licenses.sh](https://github.com/bblanchon/pdfium-binaries/blob/5453f3afc4785cbad82c05f6ceb4dabea0cb81a0/steps/08-licenses.sh)
  examines the platform's `build.ninja` and gathers the corresponding notices.
- [10-pack.sh](https://github.com/bblanchon/pdfium-binaries/blob/5453f3afc4785cbad82c05f6ceb4dabea0cb81a0/steps/10-pack.sh)
  includes every staged file in the platform archive.

Matching recipe snapshots are retained as `pdfium-binaries-8044-*-script.txt`,
with their Git blob hashes and sources. They provide provenance; the actual
redistribution notices are in the runtime directories.

`PDFium-LICENSE.txt` is copied from this release's `win-x64/licenses/pdfium.txt`.
Per-runtime copies retain each archive's original line endings and contents.
`pdfium-binaries-LICENSE.txt` retains the matching build project's MIT terms.

## Redistributing or updating

Preserve `THIRD-PARTY-NOTICES.md` and the matching runtime's complete notice
directory alongside redistributed native binaries. For applications shipping
multiple runtimes, keep each corresponding directory.

When changing native package versions, repeat archive and binary matching and
replace the retained notices and verification record. The current manifest
covers version `155.0.8044` exclusively.

The acquisition scripts and original archives used for this audit are local
working artifacts in `artifacts/pdfium-license-source/`; they are not executable
dependencies of this library and are not included in its NuGet package.
