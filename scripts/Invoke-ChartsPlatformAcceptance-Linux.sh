#!/usr/bin/env bash
# Uses an already provisioned Linux desktop or Xvfb. Does not install or change host settings.
set -euo pipefail
script_directory=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
[[ $(uname -s) == Linux ]] || { printf '%s\n' 'This wrapper requires Linux.' >&2; exit 2; }
command -v pwsh >/dev/null || { printf '%s\n' 'PowerShell 7 is required.' >&2; exit 2; }
mode=desktop
if [[ ${1:-} == --mode ]]; then mode=${2:-}; shift 2; fi
case "$mode" in
  desktop)
    [[ -n ${DISPLAY:-} ]] || { printf '%s\n' 'An X11/XWayland DISPLAY is required; no native window ran.' >&2; exit 2; }
    export CHARTS_DISPLAY_KIND=${CHARTS_DISPLAY_KIND:-desktop-synthetic}
    exec pwsh -NoLogo -NoProfile -File "$script_directory/Invoke-ChartsPlatformAcceptance.ps1" -NativeMode DesktopSynthetic "$@"
    ;;
  xvfb)
    command -v xvfb-run >/dev/null || { printf '%s\n' 'xvfb-run is required; no native window ran.' >&2; exit 2; }
    export CHARTS_DISPLAY_KIND=xvfb
    exec xvfb-run -a -s '-screen 0 1920x1440x24' pwsh -NoLogo -NoProfile -File "$script_directory/Invoke-ChartsPlatformAcceptance.ps1" -NativeMode XvfbSynthetic "$@"
    ;;
  *) printf '%s\n' 'Mode must be desktop or xvfb.' >&2; exit 2 ;;
esac
