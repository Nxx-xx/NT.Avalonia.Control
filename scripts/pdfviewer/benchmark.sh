#!/usr/bin/env bash
# Linux entry for the PdfViewer performance harness (PERF19). Mirrors benchmark.ps1; when
# no display is present and xvfb-run is available the UI scenarios run under Xvfb — results
# are then marked as a virtual-display run in the validation notes, never as a real-desktop
# measurement. All results are self performance numbers; official A/B stays blocked-reference.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
run_id="pdf-$(date -u +%Y%m%d-%H%M%S)"
output=""
iterations=0
window="1440x900"
scenarios=""
filter=""
timeout_seconds=300
no_build=0
rendering_mode="default"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --run-id) run_id="$2"; shift 2 ;;
    --output) output="$2"; shift 2 ;;
    --iterations) iterations="$2"; shift 2 ;;
    --window) window="$2"; shift 2 ;;
    --scenarios) scenarios="$2"; shift 2 ;;
    --filter) filter="$2"; shift 2 ;;
    --timeout-seconds) timeout_seconds="$2"; shift 2 ;;
    --rendering-mode) rendering_mode="$2"; shift 2 ;;
    --no-build) no_build=1; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

if [[ -z "$output" ]]; then output="$repo/artifacts/pdfviewer/$run_id/performance"; fi
mkdir -p "$output"

cd "$repo"
if [[ $no_build -eq 0 ]]; then
  # RestoreLockedMode as an MSBuild property: `dotnet build --locked-mode` is rejected by SDK 10.
  dotnet build benchmarks/PdfViewer.Benchmarks/PdfViewer.Benchmarks.csproj -c Release -p:RestoreLockedMode=true -m:1 -nr:false -v:minimal
fi

runner="$repo/benchmarks/PdfViewer.Benchmarks/bin/Release/net10.0/PdfViewer.Benchmarks"
args=(--output "$output" --run-id "$run_id" --window "$window" --timeout-seconds "$timeout_seconds" --rendering-mode "$rendering_mode")
if [[ $iterations -gt 0 ]]; then args+=(--iterations "$iterations"); fi
if [[ -n "$scenarios" ]]; then args+=(--scenarios "$scenarios"); fi
if [[ -n "$filter" ]]; then args+=(--filter "$filter"); fi

run_harness() {
  if [[ -x "$runner" ]]; then
    "$runner" "${args[@]}"
  else
    dotnet "$runner.dll" "${args[@]}"
  fi
}

if [[ -z "${DISPLAY:-}" && -z "${WAYLAND_DISPLAY:-}" ]]; then
  if command -v xvfb-run >/dev/null 2>&1; then
    echo "No display; running under xvfb-run (virtual display — mark results as such)." >&2
    xvfb-run -a bash -c "$(printf '%q ' "$runner" "${args[@]}")"
  else
    echo "No display and no xvfb-run available; UI scenarios cannot run honestly. Aborting." >&2
    exit 3
  fi
else
  run_harness
fi

for artifact in run-env.json aggregates.json report.md; do
  [[ -f "$output/$artifact" ]] || { echo "Missing $artifact in $output" >&2; exit 1; }
done
echo "PdfViewer benchmark artifacts: $output"
