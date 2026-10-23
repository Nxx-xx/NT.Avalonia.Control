param([Parameter(Mandatory)][string]$SourceBuildReportPath,[string]$SourceDemoPath,[string]$ProbePath)
$ErrorActionPreference='Stop'
$linuxSmokeRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$build=Get-Content -LiteralPath $SourceBuildReportPath -Raw|ConvertFrom-Json
if($build.LinuxSDK编译 -isnot [bool] -or -not $build.LinuxSDK编译 -or $build.全部通过 -isnot [bool] -or -not $build.全部通过){throw 'Actual Linux SDK compile and all regression tests must pass before native smoke.'}
if($build.测试 -isnot [array] -or $build.测试.Count -ne 11 -or @($build.测试|Where-Object {$_.是否通过 -isnot [bool] -or -not $_.是否通过}).Count -ne 0){throw 'All eleven actual Linux regression projects must pass; a compile-only report is insufficient.'}
$expectedDemo=Join-Path $build.ProbePath 'source/samples/NT.AvaloniaControl.Demo/bin/Release/net10.0'
if(-not $SourceDemoPath){$SourceDemoPath=$expectedDemo}
if([IO.Path]::GetFullPath($SourceDemoPath) -cne [IO.Path]::GetFullPath($expectedDemo)){throw 'Demo output differs from actual Linux build report.'}
foreach($name in @('NT.AvaloniaControl.Markdown','NT.AvaloniaControl.Markdown.Themes','NT.AvaloniaControl.Markdown.TextMate')){
    if((Get-FileHash -LiteralPath (Join-Path $SourceDemoPath "$name.dll")).Hash -cne $build.程序集SHA256.$name){throw 'Demo product assembly differs from actual Linux build report.'}
}
if(-not $ProbePath){$ProbePath=Join-Path $linuxSmokeRepo ('artifacts/platform/linux-markdown-smoke-'+[guid]::NewGuid().ToString('N'))}
$probe=[IO.Path]::GetFullPath($ProbePath)
$allowed=[IO.Path]::GetFullPath((Join-Path $linuxSmokeRepo 'artifacts/platform'))+[IO.Path]::DirectorySeparatorChar
if(-not $probe.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or $probe.Contains("'") -or $probe.Contains("`n") -or $probe.Contains("`r")){throw 'Smoke output must be a simple path within workspace artifacts/platform.'}
if(Test-Path -LiteralPath $probe){throw 'Use a fresh smoke directory; preserve prior JSON/PNG/logs.'}
$app=Join-Path $probe 'app';New-Item -ItemType Directory -Path $app -Force|Out-Null
Copy-Item -Path (Join-Path $SourceDemoPath '*') -Destination $app -Recurse -Force
$configPath=Join-Path $app 'NT.AvaloniaControl.Demo.runtimeconfig.json'
$config=Get-Content -LiteralPath $configPath -Raw|ConvertFrom-Json
if(-not $config.runtimeOptions.configProperties){$config.runtimeOptions|Add-Member -NotePropertyName configProperties -NotePropertyValue ([pscustomobject]@{})}
$config.runtimeOptions.configProperties|Add-Member -NotePropertyName 'System.Globalization.AppLocalIcu' -NotePropertyValue '72.1.0.3' -Force
$config.runtimeOptions.configProperties|Add-Member -NotePropertyName 'System.Globalization.Invariant' -NotePropertyValue $false -Force
$config|ConvertTo-Json -Depth 15|Set-Content -LiteralPath $configPath -Encoding utf8
function LinuxSmokePath([string]$Path){$full=[IO.Path]::GetFullPath($Path);return '/mnt/'+$full.Substring(0,1).ToLowerInvariant()+$full.Substring(2).Replace('\','/')}
$linuxProbe=LinuxSmokePath $probe;$linuxRepo=LinuxSmokePath $linuxSmokeRepo
$script=@'
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
cd "$probe/app"
"$DOTNET_ROOT/dotnet" NT.AvaloniaControl.Demo.dll --markdown-smoke --smoke-output "$probe/report.json" --smoke-image "$probe/preview.png" 2>&1 | tee "$probe/native.log"
'@.Replace('__REPO__',$linuxRepo).Replace('__PROBE__',$linuxProbe)
$scriptPath=Join-Path $probe 'run-smoke.sh';[IO.File]::WriteAllText($scriptPath,$script.Replace("`r`n","`n"),[Text.UTF8Encoding]::new($false))
$hashes=[ordered]@{};foreach($name in @('NT.AvaloniaControl.Markdown','NT.AvaloniaControl.Markdown.Themes','NT.AvaloniaControl.Markdown.TextMate','NT.AvaloniaControl.Demo')){$hashes[$name]=(Get-FileHash -LiteralPath (Join-Path $app "$name.dll")).Hash}
[pscustomobject]@{创建时间UTC=[DateTimeOffset]::UtcNow;LinuxSDK编译=$true;Linux编译报告=[IO.Path]::GetFullPath($SourceBuildReportPath);来源=[IO.Path]::GetFullPath($SourceDemoPath);程序集SHA256=[pscustomobject]$hashes;说明='真实Linux SDK编译Demo的完整隔离副本；app-local ICU、字体和库只在此进程设置。'}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $probe 'source-snapshot.json') -Encoding utf8
Write-Output "Linux Markdown native smoke: $probe"
wsl -d Ubuntu -- bash ($linuxProbe+'/run-smoke.sh')
$exitCode=$LASTEXITCODE
$reportPath=Join-Path $probe 'report.json';$report=if(Test-Path -LiteralPath $reportPath){Get-Content -LiteralPath $reportPath -Raw|ConvertFrom-Json}else{$null}
$passed=$exitCode -eq 0 -and $null -ne $report -and $report.通过 -is [bool] -and $report.通过 -and (Test-Path -LiteralPath (Join-Path $probe 'preview.png'))
[pscustomobject]@{创建时间UTC=[DateTimeOffset]::UtcNow;全部通过=$passed;ExitCode=$exitCode;JSON已生成=(Test-Path -LiteralPath $reportPath);PNG已生成=(Test-Path -LiteralPath (Join-Path $probe 'preview.png'));ProbePath=$probe;说明='原生WSLg/X11 Demo smoke；复制/恢复由产品探针执行，非文本剪贴板保留并标记未验证；不等于物理输入、辅助工具、DPI或麒麟验收。'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $probe 'result.json') -Encoding utf8
if(-not $passed){throw "Linux native smoke failed or incomplete; preserve $probe"}
