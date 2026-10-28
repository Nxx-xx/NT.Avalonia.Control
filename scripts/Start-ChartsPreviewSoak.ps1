[CmdletBinding()]
param([Parameter(Mandatory)][string]$BatchDirectory,[Parameter(Mandatory)][string]$Repository,[Parameter(Mandatory)][string]$BundleDirectory,[string]$PythonExecutable='python',[switch]$Worker)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 3
$batch=[IO.Path]::GetFullPath($BatchDirectory);$repo=[IO.Path]::GetFullPath($Repository);$bundle=[IO.Path]::GetFullPath($BundleDirectory)
if(!$IsWindows){throw 'The serial Windows/Linux campaign is launched from its Windows host'}
function Require-Passed([string]$Path){$j=Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json;if($j.passed -ne $true){throw "Prerequisite not passed: $Path"};return $j}
$required=@('windows/build-v1/result.json','linux/build-v1/result.json','windows/native-core-v3/execution.json','windows/native-themes-v2/execution.json','windows/native-skia-v5/execution.json','linux/native-core-v1/execution.json','linux/native-themes-v1/execution.json','linux/native-skia-v1/execution.json','windows/lifecycle-v1/execution.json','linux/lifecycle-v1/execution.json','windows/pre-soak-v1/execution.json','linux/pre-soak-v1/execution.json','windows/native-text-v1/results.json','linux/native-text-v1/results.json')
$required+=@('windows/usage-smoke-v1/process/process.json','windows/demo-smoke-v1/process/process.json','linux/usage-smoke-v1/process/process.json','linux/demo-smoke-v1/process/process.json')
foreach($p in $required){[void](Require-Passed (Join-Path $batch $p))}
if(!(Test-Path -LiteralPath (Join-Path $batch 'api/charts-preview-public-api.txt'))){throw 'API metadata snapshot required'}
$tools=Join-Path $batch 'soak-tools-v1'
$campaign=Join-Path $batch 'soak-campaign-v1'
if(!$Worker){
 if(Test-Path -LiteralPath $campaign){throw 'Existing campaign retained; inspect it rather than launching another'}
 [IO.Directory]::CreateDirectory($tools)|Out-Null;[IO.Directory]::CreateDirectory($campaign)|Out-Null
 foreach($name in @('Start-ChartsPreviewSoak.ps1','Save-ChartsPreviewRelease.py','charts_preview_release_oracle.py')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $tools}
 Copy-Item -LiteralPath (Join-Path $repo 'docs/charts-preview-validation-protocol.json') -Destination (Join-Path $tools 'protocol.json')
 $manifest=@(Get-ChildItem -LiteralPath $tools -File|ForEach-Object {[ordered]@{path=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
 $manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $campaign 'tools-manifest.json') -Encoding utf8
 $python=(Get-Command $PythonExecutable -ErrorAction Stop).Source
 $args=@('-NoProfile','-File',(Join-Path $tools 'Start-ChartsPreviewSoak.ps1'),'-BatchDirectory',$batch,'-Repository',$repo,'-BundleDirectory',$bundle,'-PythonExecutable',$python,'-Worker')
 $child=Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList $args -WindowStyle Hidden -RedirectStandardOutput (Join-Path $campaign 'stdout.log') -RedirectStandardError (Join-Path $campaign 'stderr.log') -PassThru
 [ordered]@{schema='charts-preview-soak-launch/v1';processId=$child.Id;processStartedUtc=$child.StartTime.ToUniversalTime().ToString('O');startedUtc=[DateTimeOffset]::UtcNow.ToString('O');script=(Join-Path $tools 'Start-ChartsPreviewSoak.ps1');order=@('windows','linux');secondsPerPlatform=28800;sourceUnchangedRequired=$true;remotePushPerformed=$false}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $campaign 'launch.json') -Encoding utf8
 Write-Output "Serial soak campaign launched: PID $($child.Id), evidence $campaign"
 exit 0
}
$runs=[Collections.Generic.List[object]]::new()
$state=[ordered]@{schema='charts-preview-serial-soak/v1';startedUtc=[DateTimeOffset]::UtcNow.ToString('O');finishedUtc=$null;status='starting';currentPlatform=$null;passed=$false;processId=$PID;runs=$runs;error=$null;secondsPerPlatform=28800;publicPushPerformed=$false}
function Save-Campaign {$state|ConvertTo-Json -Depth 15|Set-Content -LiteralPath (Join-Path $campaign 'result.json') -Encoding utf8}
Save-Campaign
try{
 Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public static class PreviewSoakAwake { [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags); }'
 $state.keepSystemAwakeThreadLease=([PreviewSoakAwake]::SetThreadExecutionState([uint32]2147483649) -ne 0);Save-Campaign
 $driver=Join-Path $batch 'native-driver-v6/scripts/Test-ChartsPreviewConsumer.ps1'
 $feed=Join-Path $batch 'windows/build-v1/feed'
 $state.status='running';$state.currentPlatform='windows';Save-Campaign
 & $driver -Feed $feed -OutputDirectory (Join-Path $batch 'windows/soak-v1') -Variant skia -Mode soak -Seconds 28800 -Cycles 1 -ExistingConsumer (Join-Path $batch 'windows/native-skia-v5/consumer')
 if(!$?){throw 'Windows eight-hour process failed'}
 & $PythonExecutable (Join-Path $tools 'charts_preview_release_oracle.py') --execution (Join-Path $batch 'windows/soak-v1/execution.json') --output (Join-Path $batch 'windows/soak-v1/oracle-v1.json') --require-soak
 if($LASTEXITCODE -ne 0){throw 'Windows eight-hour independent oracle failed'}
 $runs.Add([ordered]@{platform='windows';status='passed';oracle=(Join-Path $batch 'windows/soak-v1/oracle-v1.json')});Save-Campaign
 $state.currentPlatform='linux';Save-Campaign
 $linuxPs=Join-Path $campaign 'Run-LinuxSoak.ps1'
 @'
$ErrorActionPreference='Stop'
$batch='/mnt/c/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-preview-release-20261027'
& "$batch/native-driver-v6/scripts/Test-ChartsPreviewConsumer.ps1" -Feed "$batch/windows/build-v1/feed" -OutputDirectory "$batch/linux/soak-v1" -Variant skia -Mode soak -Seconds 28800 -Cycles 1 -ExistingConsumer "$batch/linux/native-skia-v1/consumer" -FontDirectory '/mnt/c/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-level-of-detail-20261019/linux-toolchain-v1/fonts'
if(!$?){throw 'Linux eight-hour process failed'}
'@|Set-Content -LiteralPath $linuxPs -Encoding utf8
 $linuxSh=Join-Path $campaign 'Run-LinuxSoak.sh'
 @'
#!/usr/bin/env bash
set -euo pipefail
source /mnt/c/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-conic-preparation-20261026/env-linux.sh
exec pwsh -NoProfile -File /mnt/c/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-preview-release-20261027/soak-campaign-v1/Run-LinuxSoak.ps1
'@|ForEach-Object {[IO.File]::WriteAllText($linuxSh,$_.Replace("`r",'')+"`n",[Text.UTF8Encoding]::new($false))}
 & wsl --exec bash /mnt/c/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-preview-release-20261027/soak-campaign-v1/Run-LinuxSoak.sh
 if($LASTEXITCODE -ne 0){throw 'Linux eight-hour process failed'}
 & $PythonExecutable (Join-Path $tools 'charts_preview_release_oracle.py') --execution (Join-Path $batch 'linux/soak-v1/execution.json') --output (Join-Path $batch 'linux/soak-v1/oracle-v1.json') --require-soak
 if($LASTEXITCODE -ne 0){throw 'Linux eight-hour independent oracle failed'}
 $runs.Add([ordered]@{platform='linux';status='passed';oracle=(Join-Path $batch 'linux/soak-v1/oracle-v1.json')});$state.status='completed';$state.passed=$true
}catch{$state.status='failed';$state.error=$_.Exception.ToString()}
finally{
 if('PreviewSoakAwake' -as [type]){[void][PreviewSoakAwake]::SetThreadExecutionState([uint32]2147483648)}
 $state.finishedUtc=[DateTimeOffset]::UtcNow.ToString('O');Save-Campaign
 & $PythonExecutable (Join-Path $tools 'Save-ChartsPreviewRelease.py') --batch $batch --repository $repo --bundle $bundle
}
if(!$state.passed){throw $state.error}
