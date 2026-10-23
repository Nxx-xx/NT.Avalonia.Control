param([string]$ProbePath, [switch]$SkipTests, [string]$PackageCachePath, [switch]$PrepareOnly, [switch]$UsePreparedSource)
$ErrorActionPreference='Stop'
$linuxBuildRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if(-not $ProbePath){$ProbePath=Join-Path $linuxBuildRepo ('artifacts/linux-build/'+[guid]::NewGuid().ToString('N'))}
$probe=[IO.Path]::GetFullPath($ProbePath)
$allowed=[IO.Path]::GetFullPath((Join-Path $linuxBuildRepo 'artifacts/linux-build'))+[IO.Path]::DirectorySeparatorChar
if(-not $probe.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Linux build must stay in workspace artifacts/linux-build.'}
if($PrepareOnly -and $UsePreparedSource){throw 'Preparation and execution of a prepared snapshot are separate invocations.'}
if($UsePreparedSource){
    $preparedPath=Join-Path $probe 'prepared.json'
    if(-not(Test-Path -LiteralPath $preparedPath)){throw 'Prepared source evidence is missing.'}
    if(Test-Path -LiteralPath (Join-Path $probe 'result.json')){throw 'Prepared snapshot already has a build result; preserve that evidence.'}
    if(Test-Path -LiteralPath (Join-Path $probe 'restore.log')){throw 'Prepared snapshot has already started; use a fresh snapshot instead of retrying it.'}
    $prepared=Get-Content -LiteralPath $preparedPath -Raw|ConvertFrom-Json
    if([bool]$prepared.测试已跳过 -ne [bool]$SkipTests){throw 'Prepared test scope does not match the execution request.'}
    if(-not $PackageCachePath){$PackageCachePath=$prepared.PackageCachePath}
}elseif(Test-Path -LiteralPath $probe){throw 'Use a fresh Linux build probe; preserve previous evidence.'}
if(-not $PackageCachePath){$PackageCachePath=Join-Path $probe 'packages'}
$packageCache=[IO.Path]::GetFullPath($PackageCachePath)
if(-not $packageCache.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Linux package cache must stay in workspace artifacts/linux-build.'}
$source=Join-Path $probe 'source'
if($UsePreparedSource){
    $manifestPath=Join-Path $probe 'source-files.json'
    if((Get-FileHash -LiteralPath $manifestPath).Hash -ne $prepared.源文件清单SHA256){throw 'Prepared manifest changed.'}
    $sourcePrefix=[IO.Path]::GetFullPath($source)+[IO.Path]::DirectorySeparatorChar
    foreach($entry in (Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json)){
        $target=[IO.Path]::GetFullPath((Join-Path $source $entry.路径))
        if(-not $target.StartsWith($sourcePrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Prepared source path escaped its snapshot.'}
        if((Get-FileHash -LiteralPath $target).Hash -ne $entry.SHA256){throw "Prepared source changed: $($entry.路径)"}
    }
    if($packageCache -ne $prepared.PackageCachePath){throw 'Prepared cache path changed.'}
    $scriptPath=Join-Path $probe 'run-build.sh'
    if((Get-FileHash -LiteralPath $scriptPath).Hash -ne $prepared.执行脚本SHA256){throw 'Prepared execution script changed.'}
}else{
New-Item -ItemType Directory -Path $source -Force|Out-Null
$files=@(Get-ChildItem -LiteralPath $linuxBuildRepo -File|ForEach-Object Name)
Push-Location $linuxBuildRepo
try{$files+=@(rg --files --hidden src samples tests benchmarks docs scripts .github -g '!**/bin/**' -g '!**/obj/**')}
finally{Pop-Location}
$manifest=@()
foreach($file in $files){
    $target=Join-Path $source $file
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
    Copy-Item -LiteralPath (Join-Path $linuxBuildRepo $file) -Destination $target
    $manifest+=[pscustomobject]@{路径=$file;SHA256=(Get-FileHash -LiteralPath $target).Hash}
}
$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $probe 'source-files.json') -Encoding utf8
}
function LinuxPath([string]$Path){$full=[IO.Path]::GetFullPath($Path);return '/mnt/'+$full.Substring(0,1).ToLowerInvariant()+$full.Substring(2).Replace('\','/')}
$linuxProbe=LinuxPath $probe;$linuxRepo=LinuxPath $linuxBuildRepo
$projects='TreeDataGrid.Core.Tests TreeDataGrid.Headless.Tests TreeDataGrid.Themes.Tests VirtualKeyboard.Core.Tests VirtualKeyboard.Headless.Tests VirtualKeyboard.Themes.Tests Markdown.Core.Tests Markdown.Streaming.Tests Markdown.Headless.Tests Markdown.Themes.Tests Markdown.TextMate.Tests'
$script=@'
#!/usr/bin/env bash
set -euo pipefail
repo='__REPO__'
probe='__PROBE__'
sdk="$repo/artifacts/tools/dotnet-linux-sdk"
old="$repo/artifacts/linux-probe/99146fc3e4e34c9eac9c3dafb84a2984"
[[ -x "$sdk/dotnet" ]] || { printf '%s\n' 'Verified Linux SDK must be installed first.'; exit 2; }
export DOTNET_ROOT="$sdk"
export DOTNET_CLI_HOME="$probe/cli-home"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export NUGET_PACKAGES='__PACKAGE_CACHE__'
export NUGET_HTTP_CACHE_PATH="$probe/nuget-http"
export NUGET_SCRATCH="$probe/nuget-scratch"
export TMPDIR="$probe/tmp"
export XDG_CACHE_HOME="$probe/cache"
export LD_LIBRARY_PATH="$old/app/runtimes/linux-x64/native:$old/ubuntu-libraries/usr/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export DOTNET_SYSTEM_GLOBALIZATION_APPLOCALICU=72.1.0.3
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0
export FONTCONFIG_FILE="$old/fontconfig.xml"
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$NUGET_HTTP_CACHE_PATH" "$NUGET_SCRATCH" "$TMPDIR" "$XDG_CACHE_HOME"
cd "$probe/source"
"$sdk/dotnet" --info | tee "$probe/sdk-info.txt"
[[ "$("$sdk/dotnet" --version)" = 10.0.401 ]] || { printf '%s\n' 'Fixed SDK version mismatch.'; exit 2; }
"$sdk/dotnet" restore NT.AvaloniaControl.slnx --locked-mode --packages "$NUGET_PACKAGES" 2>&1 | tee "$probe/restore.log"
"$sdk/dotnet" build NT.AvaloniaControl.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false 2>&1 | tee "$probe/build.log"
printf '%s\n' 'Linux SDK compile completed.' > "$probe/build-passed.txt"
if [[ '__SKIP_TESTS__' = true ]]; then exit 0; fi
failed=0
for name in __PROJECTS__; do
    cd "$probe/source/tests/$name/bin/Release/net10.0"
    if "$sdk/dotnet" "$name.dll" -noLogo 2>&1 | tee "$probe/$name.log"; then
        printf '%s\t0\n' "$name" >> "$probe/status.tsv"
    else
        printf '%s\t1\n' "$name" >> "$probe/status.tsv"
        failed=1
    fi
done
exit "$failed"
'@.Replace('__REPO__',$linuxRepo).Replace('__PROBE__',$linuxProbe).Replace('__PACKAGE_CACHE__',(LinuxPath $packageCache)).Replace('__PROJECTS__',$projects).Replace('__SKIP_TESTS__',$(if($SkipTests){'true'}else{'false'}))
if(-not $UsePreparedSource){
    $scriptPath=Join-Path $probe 'run-build.sh';[IO.File]::WriteAllText($scriptPath,$script.Replace("`r`n","`n"),[Text.UTF8Encoding]::new($false))
}
Write-Output "Linux isolated build evidence: $probe"
if($PrepareOnly){
    [pscustomobject]@{创建时间UTC=[DateTimeOffset]::UtcNow;仅准备源码=$true;LinuxSDK编译=$false;SDK='10.0.401';ProbePath=$probe;PackageCachePath=$packageCache;测试已跳过=[bool]$SkipTests;源文件数=$manifest.Count;源文件清单SHA256=(Get-FileHash -LiteralPath (Join-Path $probe 'source-files.json')).Hash;执行脚本SHA256=(Get-FileHash -LiteralPath $scriptPath).Hash;说明='仅复制源文件并固定manifest/执行脚本；尚未启动WSL、restore、build或测试。执行时逐文件复核，不覆盖或重试已启动的probe。'}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $probe 'prepared.json') -Encoding utf8
    return
}
wsl -d Ubuntu -- bash ($linuxProbe+'/run-build.sh')
$nativeExit=$LASTEXITCODE
$statuses=@();$statusPath=Join-Path $probe 'status.tsv'
if(Test-Path -LiteralPath $statusPath){$statuses=@(Import-Csv -LiteralPath $statusPath -Delimiter "`t" -Header 项目,ExitCode|ForEach-Object{[pscustomobject]@{项目=$_.项目;是否通过=([int]$_.ExitCode -eq 0);ExitCode=[int]$_.ExitCode}})}
$compiled=Test-Path -LiteralPath (Join-Path $probe 'build-passed.txt')
$binaryHashes=[ordered]@{}
if($compiled){foreach($name in @('NT.AvaloniaControl.Markdown','NT.AvaloniaControl.Markdown.Themes','NT.AvaloniaControl.Markdown.TextMate','Markdown.NativeBenchmarks')){
    $binary=Join-Path $source "benchmarks/Markdown.Benchmarks/NativeHost/bin/Release/net10.0/$name.dll"
    if(-not(Test-Path -LiteralPath $binary)){throw "Linux native output missing: $name"}
    $binaryHashes[$name]=(Get-FileHash -LiteralPath $binary).Hash
}}
[pscustomobject]@{创建时间UTC=[DateTimeOffset]::UtcNow;LinuxSDK编译=$compiled;全部通过=($nativeExit -eq 0 -and $compiled -and ($SkipTests -or $statuses.Count -eq 11));SDK='10.0.401';ProbePath=$probe;PackageCachePath=$packageCache;源文件清单SHA256=(Get-FileHash -LiteralPath (Join-Path $probe 'source-files.json')).Hash;程序集SHA256=[pscustomobject]$binaryHashes;测试已跳过=[bool]$SkipTests;测试=$statuses;说明='Linux SDK在完整隔离源文件副本编译；源manifest准确记录复制文件，仅可复用工作区Linux NuGet cache，未复用Windows assets或修改共享产品输出。'}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $probe 'result.json') -Encoding utf8
if($nativeExit -ne 0 -or -not $compiled -or (-not $SkipTests -and $statuses.Count -ne 11)){throw "Linux isolated build/test failed; preserve evidence at $probe"}
