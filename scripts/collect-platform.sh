#!/bin/sh
# Read-only Linux/Kylin environment probe. No hostname, user, serial or network collection.
set -eu
output_path=${1:-artifacts/platform/linux-environment.txt}
mkdir -p "$(dirname "$output_path")"
{
  printf 'TreeDataGrid Linux environment probe\nCollectedAtUtc: '
  date -u '+%Y-%m-%dT%H:%M:%SZ'
  printf '\nKernel and architecture:\n'
  uname -srm
  printf '\nDistribution:\n'
  if [ -r /etc/os-release ]; then cat /etc/os-release; else printf 'unavailable\n'; fi
  printf '\nDesktop session:\nXDG_SESSION_TYPE=%s\nXDG_CURRENT_DESKTOP=%s\n' "${XDG_SESSION_TYPE:-unknown}" "${XDG_CURRENT_DESKTOP:-unknown}"
  printf '\n.NET:\n'
  if command -v dotnet >/dev/null 2>&1; then
    # Replace literal home paths without treating the path as a regular expression.
    dotnet --info 2>&1 | awk '{ text=$0; path=ENVIRON["HOME"]; if (length(path)) { while (i=index(text,path)) text=substr(text,1,i-1) "<HOME>" substr(text,i+length(path)); } print text; }'
  else printf 'dotnet unavailable\n'; fi
  printf '\nChinese font families:\n'
  if command -v fc-list >/dev/null 2>&1; then fc-list :lang=zh family | sort -u; else printf 'fontconfig unavailable\n'; fi
  printf '\nInput-method package versions (availability is not functional validation):\n'
  if command -v fcitx5 >/dev/null 2>&1; then fcitx5 --version 2>&1 || true; fi
  if command -v ibus >/dev/null 2>&1; then ibus version 2>&1 || true; fi
  printf '\nStatus: environment collected only; rendering, native IME, DPI, clipboard, drag-drop and accessibility remain unverified.\n'
} > "$output_path"
printf 'Environment report saved to %s\n' "$output_path"
