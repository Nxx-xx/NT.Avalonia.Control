#!/usr/bin/env bash
# Linux execution wrapper. No installs, service changes, or reuse of historical reports.
set -euo pipefail
script_directory=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
mode=headless
if [[ ${1:-} == --mode ]]; then
  [[ $# -ge 2 ]] || { printf '%s\n' '--mode needs headless, xvfb or desktop' >&2; exit 2; }
  mode=$2
  shift 2
fi
[[ $(uname -s) == Linux ]] || { printf '%s\n' 'This wrapper requires Linux.' >&2; exit 2; }
command -v pwsh >/dev/null || { printf '%s\n' 'PowerShell 7 (pwsh) is required; provision it explicitly in the runner image.' >&2; exit 2; }
case "$mode" in
  headless)
    exec pwsh -NoLogo -NoProfile -File "$script_directory/Verify-Charts.ps1" -Mode Headless "$@"
    ;;
  xvfb)
    command -v xvfb-run >/dev/null || { printf '%s\n' 'xvfb-run is unavailable; no native smoke was executed.' >&2; exit 2; }
    export CHARTS_DISPLAY_KIND=xvfb
    exec xvfb-run -a -s '-screen 0 1600x1200x24' pwsh -NoLogo -NoProfile -File "$script_directory/Verify-Charts.ps1" -Mode XvfbSynthetic -RequestedBackend platform-default-with-xvfb-display "$@"
    ;;
  desktop)
    [[ -n ${DISPLAY:-} ]] || { printf '%s\n' 'Desktop mode requires an existing X11/XWayland DISPLAY; Avalonia 12.0.3 native Wayland is not enabled by this wrapper.' >&2; exit 2; }
    export CHARTS_DISPLAY_KIND=desktop-session-declared
    exec pwsh -NoLogo -NoProfile -File "$script_directory/Verify-Charts.ps1" -Mode DesktopSynthetic "$@"
    ;;
  *) printf 'Unknown mode: %s\n' "$mode" >&2; exit 2 ;;
esac
