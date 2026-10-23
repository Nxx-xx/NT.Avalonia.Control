#!/usr/bin/env bash
# Ubuntu/WSL native smoke; Xvfb is a CI display, never physical desktop certification.
set -Eeuo pipefail
repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
output=""; corpus_cache=""; window=1; skip_build=0; tests=1
while (($#)); do
  case "$1" in
    --output) output="$2"; shift 2 ;;
    --corpus-cache) corpus_cache="$2"; shift 2 ;;
    --backend-only) window=0; shift ;;
    --no-build) skip_build=1; shift ;;
    --skip-tests) tests=0; shift ;;
    *) printf 'Unknown argument: %s\n' "$1" >&2; exit 64 ;;
  esac
done
[[ "$(uname -s)" == Linux ]] || { echo 'This runner requires Linux.' >&2; exit 64; }
output="${output:-$repo/artifacts/pdfviewer-native/linux-$(date -u +%Y%m%dT%H%M%SZ)}"
output="$(realpath -m -- "$output")"
[[ ! -e "$output" ]] || { echo 'Choose a fresh output directory; existing evidence is preserved.' >&2; exit 64; }
mkdir -p -- "$output/logs"
stage=environment
export PDF_P1_OUTPUT="$output" PDF_P1_REPO="$repo" PDF_P1_WINDOW="$window" PDF_P1_BUILD="$((1-skip_build))" PDF_P1_TESTS="$tests" PDF_P1_BUILT=0 PDF_P1_TESTED=0
finish() {
  local code=$?
  trap - EXIT
  PDF_P1_EXIT="$code" PDF_P1_STAGE="$stage" python3 - <<'PY'
import datetime, hashlib, json, os
from pathlib import Path
root=Path(os.environ['PDF_P1_OUTPUT'])
files=[{'Path':p.relative_to(root).as_posix(),'SHA256':hashlib.sha256(p.read_bytes()).hexdigest().upper(),'Bytes':p.stat().st_size} for p in sorted(root.rglob('*')) if p.is_file() and p.name!='run.json']
(root/'run.json').write_text(json.dumps({'SchemaVersion':1,'CompletedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'ExitCode':int(os.environ['PDF_P1_EXIT']),'Passed':os.environ['PDF_P1_EXIT']=='0','LastStage':os.environ['PDF_P1_STAGE'],'WindowRequested':os.environ['PDF_P1_WINDOW']=='1','BuildRequested':os.environ['PDF_P1_BUILD']=='1','BuildExecuted':os.environ['PDF_P1_BUILT']=='1','CoreAndHeadlessRequested':os.environ['PDF_P1_TESTS']=='1','CoreAndHeadlessExecuted':os.environ['PDF_P1_TESTED']=='1','PhysicalDesktopAcceptance':False,'Files':files},indent=2),encoding='utf-8')
PY
  exit "$code"
}
trap finish EXIT
run() {
  stage="$1"; shift
  printf 'PDF P1: %s\n' "$stage"
  "$@" >"$output/logs/$stage.log" 2>&1 || { local code=$?; tail -n 60 -- "$output/logs/$stage.log" >&2; return "$code"; }
}
cd -- "$repo"
run dotnet-info dotnet --info
run system bash -c 'cat /etc/os-release; uname -a; getconf GNU_LIBC_VERSION; ldd --version; printenv DISPLAY WAYLAND_DISPLAY XDG_SESSION_TYPE LD_LIBRARY_PATH DOTNET_ROOT DOTNET_SYSTEM_GLOBALIZATION_INVARIANT || true'
python3 - <<'PY'
import datetime,hashlib,json,os,platform
from pathlib import Path
repo=Path(os.environ['PDF_P1_REPO']); output=Path(os.environ['PDF_P1_OUTPUT'])
paths=[]
for directory in ('src/NT.AvaloniaControl.PdfViewer','src/NT.AvaloniaControl.PdfViewer.Themes','benchmarks/PdfViewer.P1','tests/PdfViewer.Core.Tests','tests/PdfViewer.Headless.Tests'):
    paths += [p for p in (repo/directory).rglob('*') if p.is_file() and not {'bin','obj','__pycache__'}.intersection(p.relative_to(repo).parts) and p.suffix in ('.cs','.csproj','.axaml','.json')]
paths += [repo/p for p in ('Directory.Build.props','tests/Directory.Build.props','Directory.Packages.props','global.json','NuGet.Config','scripts/Verify-PdfViewer-Linux.sh','scripts/Test-PdfViewerNative.ps1','.github/workflows/pdfviewer-native.yml','benchmarks/PdfViewer.P0/Fixtures/real-corpus-sources.json')]
files=[{'Path':p.relative_to(repo).as_posix(),'SHA256':hashlib.sha256(p.read_bytes()).hexdigest().upper()} for p in sorted(set(paths)) if p.exists()]
(output/'source-before.json').write_text(json.dumps({'CapturedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'Files':files},indent=2),encoding='utf-8')
(output/'environment.json').write_text(json.dumps({'CapturedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'Platform':platform.platform(),'Machine':platform.machine(),'Kernel':platform.release(),'Wsl':'microsoft' in platform.release().lower(),'Display':os.environ.get('DISPLAY'),'WaylandDisplay':os.environ.get('WAYLAND_DISPLAY'),'LD_LIBRARY_PATH':os.environ.get('LD_LIBRARY_PATH'),'DOTNET_ROOT':os.environ.get('DOTNET_ROOT'),'DOTNET_SYSTEM_GLOBALIZATION_INVARIANT':os.environ.get('DOTNET_SYSTEM_GLOBALIZATION_INVARIANT'),'FONTCONFIG_FILE':os.environ.get('FONTCONFIG_FILE'),'XDG_CACHE_HOME':os.environ.get('XDG_CACHE_HOME'),'GitHubRunId':os.environ.get('GITHUB_RUN_ID'),'GitHubSha':os.environ.get('GITHUB_SHA'),'Boundary':'Native PDFium and optional X11 window smoke; WSLg/Xvfb is not Ubuntu 24.04 physical desktop, Kylin, input/IME or performance acceptance.'},indent=2),encoding='utf-8')
PY
export PDF_P1_CORPUS_CACHE="$corpus_cache"
run prepare-published-source python3 - <<'PY'
import hashlib,json,os,shutil,urllib.request
from pathlib import Path
repo=Path(os.environ['PDF_P1_REPO']); output=Path(os.environ['PDF_P1_OUTPUT'])
manifest=repo/'benchmarks/PdfViewer.P0/Fixtures/real-corpus-sources.json'
item=next(s for s in json.loads(manifest.read_text(encoding='utf-8-sig'))['Sources'] if s['Id']=='gnu-make')
target=output/'corpus'; target.mkdir()
cache=Path(os.environ['PDF_P1_CORPUS_CACHE']) if os.environ.get('PDF_P1_CORPUS_CACHE') else None
source=cache/item['File'] if cache else None
data=source.read_bytes() if source and source.is_file() else urllib.request.urlopen(urllib.request.Request(item['PdfUrl'],headers={'User-Agent':'PDF-native-validation/1.0'}),timeout=60).read(item['Bytes']+1)
if len(data)!=item['Bytes'] or hashlib.sha256(data).hexdigest().upper()!=item['SHA256']: raise ValueError('Published source changed: frozen GNU make bytes required.')
(target/item['File']).write_bytes(data)
(target/'source.json').write_text(json.dumps({'ManifestSHA256':hashlib.sha256(manifest.read_bytes()).hexdigest().upper(),'Source':item,'Downloaded':not(source and source.is_file()),'ProductPackageRedistribution':False},indent=2),encoding='utf-8')
PY
project=benchmarks/PdfViewer.P1/PdfViewer.P1.csproj
if (( !skip_build )); then
  run restore dotnet restore "$project" --locked-mode --disable-parallel -m:1 -nr:false
  run build dotnet build "$project" -c Release --no-restore -m:1 -nr:false -v:minimal
  export PDF_P1_BUILT=1
fi
app="$repo/benchmarks/PdfViewer.P1/bin/Release/net10.0"
case "$(uname -m)" in x86_64) rid=linux-x64;; aarch64) rid=linux-arm64;; *) echo 'Only x64/ARM64 native asset auditing is supported.' >&2; exit 64;; esac
native="$app/runtimes/$rid/native/libpdfium.so"
[[ -f "$native" ]] || { echo "Missing PDFium asset: $native" >&2; exit 1; }
run elf-header readelf -h "$native"
run elf-dynamic readelf -d "$native"
run elf-versions readelf --version-info "$native"
run native-ldd ldd "$native"
if grep -q 'not found' "$output/logs/native-ldd.log"; then echo 'Unresolved native dependency.' >&2; exit 1; fi
for library in "$app/runtimes/$rid/native/"*.so; do
  name="$(basename -- "$library")"
  run "elf-$name-header" readelf -h "$library"
  run "elf-$name-dynamic" readelf -d "$library"
  run "elf-$name-versions" readelf --version-info "$library"
  run "elf-$name-ldd" ldd "$library"
  if grep -q 'not found' "$output/logs/elf-$name-ldd.log"; then echo "Unresolved dependency for $name." >&2; exit 1; fi
done
export PDF_P1_NATIVE="$native"
python3 - <<'PY'
import hashlib,json,os,re
from pathlib import Path
root=Path(os.environ['PDF_P1_OUTPUT']); native=Path(os.environ['PDF_P1_NATIVE']).resolve()
versions=(root/'logs/elf-versions.log').read_text(encoding='utf-8')
groups={name:sorted(set(re.findall(r'\b'+name+r'_[0-9][0-9.]*',versions)),key=lambda s:tuple(map(int,s.split('_')[-1].split('.')))) for name in ('GLIBC','GLIBCXX','CXXABI')}
assets=[]
for item in sorted(native.parent.glob('*.so')):
    text=(root/('logs/elf-'+item.name+'-versions.log')).read_text(encoding='utf-8')
    found={name:sorted(set(re.findall(r'\b'+name+r'_[0-9][0-9.]*',text)),key=lambda s:tuple(map(int,s.split('_')[-1].split('.')))) for name in ('GLIBC','GLIBCXX','CXXABI')}
    assets.append({'Path':str(item.resolve()),'SHA256':hashlib.sha256(item.read_bytes()).hexdigest().upper(),'Bytes':item.stat().st_size,'VersionRequirements':found,'HighestVersionRequirements':{k:(v[-1] if v else None) for k,v in found.items()},'DynamicDependenciesResolved':True})
(root/'native-asset.json').write_text(json.dumps({'Path':str(native),'SHA256':hashlib.sha256(native.read_bytes()).hexdigest().upper(),'Bytes':native.stat().st_size,'VersionRequirements':groups,'HighestVersionRequirements':{k:(v[-1] if v else None) for k,v in groups.items()},'DynamicDependenciesResolved':True,'RuntimeAssets':assets,'Boundary':'ELF requirements of these exact assets, not a certification of every distribution at or above one symbol version.'},indent=2),encoding='utf-8')
PY
published="$output/corpus/gnu-make.pdf"
sha=A4BC06026984382815E392159E60A4BC356CAC6C5B81E5AE84FA4F58F621F4D3
run backend timeout 120 dotnet "$app/PdfViewer.P1.dll" --output "$output/backend" --real-pdf "$published" --expected-sha "$sha"
if ((window)); then
  [[ -n "${DISPLAY:-}" ]] || { echo 'X11 DISPLAY is required for native window mode; CI may use xvfb-run.' >&2; exit 1; }
  run native-window timeout 120 dotnet "$app/PdfViewer.P1.dll" --output "$output/native-window" --real-pdf "$published" --expected-sha "$sha" --window
fi
if ((tests)); then
  for suite in Core Headless; do
    test_project="tests/PdfViewer.$suite.Tests/PdfViewer.$suite.Tests.csproj"
    if (( !skip_build )); then
      run "restore-$suite" dotnet restore "$test_project" --locked-mode --disable-parallel -m:1 -nr:false
      run "build-$suite" dotnet build "$test_project" -c Release --no-restore -m:1 -nr:false -v:minimal
    fi
    run "test-$suite" timeout 180 dotnet "tests/PdfViewer.$suite.Tests/bin/Release/net10.0/PdfViewer.$suite.Tests.dll" -noLogo -xml "$output/$suite.xml"
  done
  export PDF_P1_TESTED=1
fi
stage=verify-evidence
python3 - <<'PY'
import hashlib,json,os
from pathlib import Path
root=Path(os.environ['PDF_P1_OUTPUT']); repo=Path(os.environ['PDF_P1_REPO'])
manifest=json.loads((root/'source-before.json').read_text(encoding='utf-8'))
for source in manifest['Files']:
    if hashlib.sha256((repo/source['Path']).read_bytes()).hexdigest().upper()!=source['SHA256']: raise ValueError('Source changed during native verification: '+source['Path'])
(root/'source-after.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
asset=json.loads((root/'native-asset.json').read_text(encoding='utf-8'))
for name in ('backend','native-window') if os.environ['PDF_P1_WINDOW']=='1' else ('backend',):
    report=json.loads((root/name/'result.json').read_text(encoding='utf-8'))
    if report.get('Passed') is not True or report.get('Failure') or not report['Checks'] or not all(report['Checks'].values()): raise ValueError('Incomplete native result: '+name)
    loaded=report['Observations']['LoadedPdfium']
    if len(loaded)!=1 or Path(loaded[0]['Path']).resolve()!=Path(asset['Path']).resolve() or loaded[0]['SHA256']!=asset['SHA256']: raise ValueError('The actually loaded PDFium module differs from the audited ELF asset.')
    audited={item['Path']:item['SHA256'] for item in asset['RuntimeAssets']}
    actual=report['Observations']['LoadedNativeRuntimeAssets']
    for item in actual:
        if audited.get(str(Path(item['Path']).resolve()))!=item['SHA256']: raise ValueError('An actually loaded runtime asset differs from its ELF audit.')
    if name=='native-window' and {Path(item['Path']).name for item in actual}!={'libpdfium.so','libSkiaSharp.so','libHarfBuzzSharp.so'}: raise ValueError('Native window did not load every expected runtime asset.')
PY
stage=complete
echo "PDF P1 Linux evidence: $output"
