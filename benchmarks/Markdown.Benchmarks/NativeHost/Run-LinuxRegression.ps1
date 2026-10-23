param([string]$ProbePath)
$ErrorActionPreference = 'Stop'
$regressionRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (-not $ProbePath) { $ProbePath = Join-Path $regressionRepo ('artifacts/linux-probe/' + [guid]::NewGuid().ToString('N')) }
$fullProbePath = [IO.Path]::GetFullPath($ProbePath)
$allowedProbeRoot = [IO.Path]::GetFullPath((Join-Path $regressionRepo 'artifacts/linux-probe')) + [IO.Path]::DirectorySeparatorChar
if (-not $fullProbePath.StartsWith($allowedProbeRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Linux regression probe must stay in workspace artifacts/linux-probe.' }
New-Item -ItemType Directory -Path $fullProbePath -Force | Out-Null
$projectNames = @('TreeDataGrid.Core.Tests','TreeDataGrid.Headless.Tests','TreeDataGrid.Themes.Tests','VirtualKeyboard.Core.Tests','VirtualKeyboard.Headless.Tests','VirtualKeyboard.Themes.Tests','Markdown.Core.Tests','Markdown.Streaming.Tests','Markdown.Headless.Tests','Markdown.Themes.Tests','Markdown.TextMate.Tests')
foreach ($name in $projectNames) {
    $target = Join-Path $fullProbePath ('tests/' + $name)
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -Path (Join-Path $regressionRepo "tests/$name/bin/Release/net10.0/*") -Destination $target -Recurse -Force
    $configPath = Join-Path $target "$name.runtimeconfig.json"
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    if (-not $config.runtimeOptions.configProperties) { $config.runtimeOptions | Add-Member -NotePropertyName configProperties -NotePropertyValue ([pscustomobject]@{}) }
    $config.runtimeOptions.configProperties | Add-Member -NotePropertyName 'System.Globalization.AppLocalIcu' -NotePropertyValue '72.1.0.3' -Force
    $config.runtimeOptions.configProperties | Add-Member -NotePropertyName 'System.Globalization.Invariant' -NotePropertyValue $false -Force
    $config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $configPath -Encoding utf8
}
$probeLinuxPath = '/mnt/' + $fullProbePath.Substring(0, 1).ToLowerInvariant() + $fullProbePath.Substring(2).Replace('\','/')
$repoLinuxPath = '/mnt/' + $regressionRepo.Substring(0, 1).ToLowerInvariant() + $regressionRepo.Substring(2).Replace('\','/')
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
printf '%s\n' 'Windows Release IL executed on actual Ubuntu/WSLg; no Linux SDK compilation.'
cat /etc/os-release
uname -rsm
failed=0
for name in __PROJECTS__; do
    cd "$probe/tests/$name"
    if "$DOTNET_ROOT/dotnet" "$name.dll" -noLogo | tee "$probe/$name.log"; then
        printf '%s\n' "$name PASS"
        printf '%s\t0\n' "$name" >> "$probe/status.tsv"
    else
        printf '%s\n' "$name FAIL"
        printf '%s\t1\n' "$name" >> "$probe/status.tsv"
        failed=1
    fi
done
exit "$failed"
'@.Replace('__REPO__', $repoLinuxPath).Replace('__PROBE__', $probeLinuxPath).Replace('__PROJECTS__', ($projectNames -join ' '))
$scriptPath = Join-Path $fullProbePath 'run-regression.sh'
[IO.File]::WriteAllText($scriptPath, $script.Replace("`r`n", "`n"), [Text.UTF8Encoding]::new($false))
wsl -d Ubuntu -- bash ($probeLinuxPath + '/run-regression.sh')
$nativeExitCode=$LASTEXITCODE
$statusPath=Join-Path $fullProbePath 'status.tsv'
$observations=if(Test-Path -LiteralPath $statusPath){@(Import-Csv -LiteralPath $statusPath -Delimiter "`t" -Header 项目,ExitCode|ForEach-Object{[pscustomobject]@{项目=$_.项目;是否通过=([int]$_.ExitCode -eq 0);ExitCode=[int]$_.ExitCode}})}else{@()}
[PSCustomObject]@{ 全部通过=($nativeExitCode -eq 0 -and $observations.Count -eq 11); 环境='Ubuntu26.04/WSL2/WSLg'; LinuxSDK编译=$false; ProbePath=$fullProbePath; 项目=$observations } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $fullProbePath 'regression-result.json') -Encoding utf8
if ($nativeExitCode -ne 0 -or $observations.Count -ne 11) { throw "Linux regression failed; see $fullProbePath" }
Write-Output "Linux regression evidence: $fullProbePath"
