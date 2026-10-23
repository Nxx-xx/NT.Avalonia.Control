param([string]$SourceHostPath, [string]$ProbePath, [int]$Samples=3, [int]$StreamSessions=1, [int]$LongStreamSessions=1, [string]$Only, [switch]$Formal, [string]$SourceBuildReportPath)
$ErrorActionPreference = 'Stop'
$nativeRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (-not $SourceHostPath) { $SourceHostPath = Join-Path $PSScriptRoot 'bin/Release/net10.0' }
$sdkCompiled=$false
if($SourceBuildReportPath){
    $buildReport=Get-Content -LiteralPath $SourceBuildReportPath -Raw|ConvertFrom-Json
    if($buildReport.LinuxSDK编译 -isnot [bool] -or -not $buildReport.LinuxSDK编译){throw 'Source build report must confirm actual Linux SDK compilation.'}
    $expectedHost=Join-Path $buildReport.ProbePath 'source/benchmarks/Markdown.Benchmarks/NativeHost/bin/Release/net10.0'
    if([IO.Path]::GetFullPath($SourceHostPath) -cne [IO.Path]::GetFullPath($expectedHost)){throw 'Source host differs from Linux build report.'}
    foreach($name in @('NT.AvaloniaControl.Markdown','NT.AvaloniaControl.Markdown.Themes','NT.AvaloniaControl.Markdown.TextMate','Markdown.NativeBenchmarks')){
        if((Get-FileHash -LiteralPath (Join-Path $SourceHostPath "$name.dll")).Hash -cne $buildReport.程序集SHA256.$name){throw 'Source binary differs from actual Linux build report.'}
    }
    $sdkCompiled=$true
}
if ($Formal) {
    if (-not $sdkCompiled -or $buildReport.全部通过 -isnot [bool] -or -not $buildReport.全部通过 -or
        $buildReport.测试 -isnot [array] -or $buildReport.测试.Count -ne 11 -or
        @($buildReport.测试 | Where-Object { $_.是否通过 -isnot [bool] -or -not $_.是否通过 }).Count -ne 0) {
        throw 'Formal Linux sampling requires the actual Linux SDK build and all eleven regression projects to pass.'
    }
    if ($Only) { throw 'Formal Linux sampling requires every group.' }
    if (-not $PSBoundParameters.ContainsKey('Samples')) { $Samples=100 }
    if (-not $PSBoundParameters.ContainsKey('StreamSessions')) { $StreamSessions=100 }
    if (-not $PSBoundParameters.ContainsKey('LongStreamSessions')) { $LongStreamSessions=100 }
    if ($Samples -lt 100 -or $StreamSessions -lt 100 -or $LongStreamSessions -lt 100) { throw 'Formal Linux sampling requires >=100 samples and completions per round.' }
}
if (-not $ProbePath) { $ProbePath = Join-Path $nativeRepo ('artifacts/linux-probe/' + [guid]::NewGuid().ToString('N')) }
$probe = [IO.Path]::GetFullPath($ProbePath)
$allowed = [IO.Path]::GetFullPath((Join-Path $nativeRepo 'artifacts/linux-probe')) + [IO.Path]::DirectorySeparatorChar
if (-not $probe.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Native Linux probe must stay in workspace artifacts/linux-probe.' }
if(Test-Path -LiteralPath $probe){throw 'Use a fresh native probe; preserve previous evidence.'}
if ($Samples -le 0 -or $StreamSessions -le 0 -or $LongStreamSessions -le 0 -or ($Only -and $Only -notin 'reading','long-block','streaming','streaming-long','virtualization','resources')) { throw 'Invalid pilot arguments.' }
$hostPath = Join-Path $probe 'host'
New-Item -ItemType Directory -Path $hostPath -Force | Out-Null
Copy-Item -Path (Join-Path $SourceHostPath '*') -Destination $hostPath -Recurse -Force
$configPath = Join-Path $hostPath 'Markdown.NativeBenchmarks.runtimeconfig.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
if (-not $config.runtimeOptions.configProperties) { $config.runtimeOptions | Add-Member -NotePropertyName configProperties -NotePropertyValue ([pscustomobject]@{}) }
$config.runtimeOptions.configProperties | Add-Member -NotePropertyName 'System.Globalization.AppLocalIcu' -NotePropertyValue '72.1.0.3' -Force
$config.runtimeOptions.configProperties | Add-Member -NotePropertyName 'System.Globalization.Invariant' -NotePropertyValue $false -Force
$config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $configPath -Encoding utf8
function LinuxPath([string]$Path) { $full=[IO.Path]::GetFullPath($Path); return '/mnt/' + $full.Substring(0,1).ToLowerInvariant() + $full.Substring(2).Replace('\','/') }
$linuxProbe = LinuxPath $probe
$linuxRepo = LinuxPath $nativeRepo
$script = @'
#!/usr/bin/env bash
set -euo pipefail
repo='__REPO__'
probe='__PROBE__'
old="$repo/artifacts/linux-probe/99146fc3e4e34c9eac9c3dafb84a2984"
export DOTNET_ROOT="$repo/artifacts/tools/dotnet-linux"
export LD_LIBRARY_PATH="$old/app/runtimes/linux-x64/native:$old/ubuntu-libraries/usr/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export FONTCONFIG_FILE="$old/fontconfig.xml"
export XDG_CACHE_HOME="$probe/font-cache"
cat /etc/os-release > "$probe/os-release.txt"
uname -rsm > "$probe/kernel.txt"
printf 'DISPLAY=%s\nWAYLAND_DISPLAY=%s\n' "${DISPLAY:-}" "${WAYLAND_DISPLAY:-}" > "$probe/display.txt"
cd "$probe/host"
for round in __ROUNDS__; do
    "$DOTNET_ROOT/dotnet" Markdown.NativeBenchmarks.dll __PILOT__ --round "$round" --samples __SAMPLES__ --stream-sessions __SESSIONS__ --long-stream-sessions __LONG_SESSIONS__ __ONLY__ --output "$probe/report-$round.json" 2>&1 | tee "$probe/native-$round.log"
done
'@.Replace('__REPO__',$linuxRepo).Replace('__PROBE__',$linuxProbe).Replace('__SAMPLES__',[string]$Samples).Replace('__SESSIONS__',[string]$StreamSessions).Replace('__LONG_SESSIONS__',[string]$LongStreamSessions).Replace('__ONLY__',$(if ($Only) { '--only '+$Only } else { '' })).Replace('__ROUNDS__',$(if($Formal){'1 2 3 4 5'}else{'1'})).Replace('__PILOT__',$(if($Formal){''}else{'--pilot'}))
$scriptPath = Join-Path $probe 'run-native.sh'
[IO.File]::WriteAllText($scriptPath, $script.Replace("`r`n","`n"), [Text.UTF8Encoding]::new($false))
[pscustomobject]@{ 创建时间UTC=[DateTimeOffset]::UtcNow; LinuxSDK编译=$sdkCompiled; 来源=[IO.Path]::GetFullPath($SourceHostPath); Linux编译报告=$SourceBuildReportPath; 功能程序集SHA256=(Get-FileHash (Join-Path $hostPath 'NT.AvaloniaControl.Markdown.dll')).Hash; 主题程序集SHA256=(Get-FileHash (Join-Path $hostPath 'NT.AvaloniaControl.Markdown.Themes.dll')).Hash; TextMate程序集SHA256=(Get-FileHash (Join-Path $hostPath 'NT.AvaloniaControl.Markdown.TextMate.dll')).Hash; Host程序集SHA256=(Get-FileHash (Join-Path $hostPath 'Markdown.NativeBenchmarks.dll')).Hash; 说明=$(if($sdkCompiled){'真实Linux SDK隔离编译输出，经build报告hash核对，在实际WSLg原生X11运行。'}else{'Windows Release IL的独立复制，在实际WSLg原生X11运行；没有Linux SDK编译证据。'}) } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $probe 'source-snapshot.json') -Encoding utf8
Write-Output "Linux native evidence: $probe"
wsl -d Ubuntu -- bash ($linuxProbe + '/run-native.sh')
if ($LASTEXITCODE -ne 0) { throw "Native Linux pilot failed; preserve report/logs at $probe" }
if($Formal){
    $paths=@(1..5|ForEach-Object{Join-Path $probe "report-$_.json"})
    & (Join-Path $nativeRepo 'scripts/Test-MarkdownPerformance.ps1') -ReportPaths $paths -OutputPath (Join-Path $probe 'guard.json')
}
