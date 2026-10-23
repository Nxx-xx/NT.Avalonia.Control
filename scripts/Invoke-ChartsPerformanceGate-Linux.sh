#!/usr/bin/env bash
# Provision dependencies in the runner image; this wrapper never installs them.
set -euo pipefail
script_directory=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
[[ $(uname -s) == Linux ]] || { printf '%s\n' 'This wrapper requires Linux.' >&2; exit 2; }
command -v pwsh >/dev/null || { printf '%s\n' 'PowerShell 7 is required.' >&2; exit 2; }
command -v xvfb-run >/dev/null || { printf '%s\n' 'xvfb-run is required; no native window benchmark ran.' >&2; exit 2; }
export CHARTS_DISPLAY_KIND=xvfb
exec xvfb-run -a -s '-screen 0 1600x1200x24' pwsh -NoLogo -NoProfile -File "$script_directory/Invoke-ChartsPerformanceGate.ps1" -NativeMode XvfbSynthetic "$@"
