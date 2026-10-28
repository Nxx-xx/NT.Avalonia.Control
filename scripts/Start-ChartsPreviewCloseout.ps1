[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$BatchDirectory,
 [Parameter(Mandatory)][string]$Repository,
 [Parameter(Mandatory)][string]$BundleDirectory,
 [string]$PythonExecutable='python',
 [ValidatePattern('^v[1-9][0-9]*$')][string]$EvidenceVersion='v2',
 [switch]$Worker
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 3
if(!$IsWindows){throw 'Run from the Windows host of the existing serial campaign'}
$batchRoot=[IO.Path]::GetFullPath($BatchDirectory)
$repoRoot=[IO.Path]::GetFullPath($Repository)
$bundleRoot=[IO.Path]::GetFullPath($BundleDirectory)
$campaignRoot=Join-Path $batchRoot 'soak-campaign-v1'
$closeoutRoot=Join-Path $batchRoot ('closeout-campaign-'+$EvidenceVersion)
$toolRoot=Join-Path $batchRoot ('closeout-tools-'+$EvidenceVersion)
$launch=Get-Content -LiteralPath (Join-Path $campaignRoot 'launch.json') -Raw|ConvertFrom-Json
if($launch.secondsPerPlatform -ne 28800){throw 'Existing eight-hour campaign required'}
if(!$Worker){
 if(Test-Path -LiteralPath $closeoutRoot){throw 'Closeout already exists; inspect its result before retrying'}
 [void][IO.Directory]::CreateDirectory($closeoutRoot)
 [void][IO.Directory]::CreateDirectory($toolRoot)
 foreach($name in @('Start-ChartsPreviewCloseout.ps1','Save-ChartsPreviewRelease.py','charts_preview_release_oracle.py','Verify-ChartsPreviewBundle.py')){
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $toolRoot
 }
 @(Get-ChildItem -LiteralPath $toolRoot -File|ForEach-Object {[ordered]@{path=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $closeoutRoot 'tools-manifest.json') -Encoding utf8
 $pythonPath=(Get-Command $PythonExecutable -ErrorAction Stop).Source
 $workerArgs=@('-NoProfile','-File',(Join-Path $toolRoot 'Start-ChartsPreviewCloseout.ps1'),'-BatchDirectory',$batchRoot,'-Repository',$repoRoot,'-BundleDirectory',$bundleRoot,'-PythonExecutable',$pythonPath,'-EvidenceVersion',$EvidenceVersion,'-Worker')
 $child=Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList $workerArgs -WindowStyle Hidden -RedirectStandardOutput (Join-Path $closeoutRoot 'stdout.log') -RedirectStandardError (Join-Path $closeoutRoot 'stderr.log') -PassThru
 [ordered]@{schema='charts-preview-closeout-launch/v1';processId=$child.Id;processStartedUtc=$child.StartTime.ToUniversalTime().ToString('O');startedUtc=[DateTimeOffset]::UtcNow.ToString('O');existingSoakProcessId=$launch.processId;remotePushPerformed=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $closeoutRoot 'launch.json') -Encoding utf8
 Write-Output "Preview closeout launched: PID $($child.Id), $closeoutRoot"
 exit 0
}
$state=[ordered]@{schema='charts-preview-closeout/v1';status='waiting-for-existing-soak';passed=$false;startedUtc=[DateTimeOffset]::UtcNow.ToString('O');finishedUtc=$null;heartbeatUtc=$null;processId=$PID;soakProcessId=$launch.processId;archive=$null;error=$null;remotePushPerformed=$false}
function Save-State {
 $state.heartbeatUtc=[DateTimeOffset]::UtcNow.ToString('O')
 $tempPath=Join-Path $closeoutRoot 'result.json.tmp'
 $state|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $tempPath -Encoding utf8
 Move-Item -LiteralPath $tempPath -Destination (Join-Path $closeoutRoot 'result.json') -Force
}
function Require-FreshHeartbeat($Soak) {
 # PIDs from an earlier executor session need not be enumerable in this session.
 $receiptPath=Join-Path $batchRoot ($Soak.currentPlatform+'/soak-v1/native-process/process.json')
 $receipt=Get-Content -LiteralPath $receiptPath -Raw|ConvertFrom-Json
 if(!$receipt.heartbeatUtc){throw 'Original native supervisor heartbeat is missing'}
 $age=([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse($receipt.heartbeatUtc)).TotalSeconds
 if($age -gt 120 -or $age -lt -10){throw "Original supervisor heartbeat is stale or future dated: $age seconds"}
}
function Original-PublisherFinished($Soak) {
 $logPath=Join-Path $campaignRoot 'stdout.log'
 if((Get-Item -LiteralPath $logPath).LastWriteTimeUtc -lt [DateTimeOffset]::Parse($Soak.finishedUtc).UtcDateTime){return $false}
 # This JSON line is emitted only after every original publisher write, including SHA256SUMS.
 foreach($line in @(Get-Content -LiteralPath $logPath -Tail 12)){
  try{$receipt=$line|ConvertFrom-Json -ErrorAction Stop}catch{continue}
  if($receipt.PSObject.Properties['bundle'] -and $receipt.PSObject.Properties['checks'] -and $receipt.bundle -eq $bundleRoot){return $true}
 }
 return $false
}
try {
 Save-State
 while($true){
  $soak=Get-Content -LiteralPath (Join-Path $campaignRoot 'result.json') -Raw|ConvertFrom-Json
  if($soak.status -in @('completed','failed')){break}
  Require-FreshHeartbeat $soak
  Save-State
  Start-Sleep -Seconds 30
 }
 # The original worker publishes its own final summary after setting terminal state.
 $exitDeadline=[DateTimeOffset]::UtcNow.AddMinutes(3)
 while(!(Original-PublisherFinished $soak)){
  if([DateTimeOffset]::UtcNow -gt $exitDeadline){throw 'Original publisher did not produce its final write receipt'}
  Start-Sleep -Seconds 5
 }
 $state.status='validating';Save-State
 & $PythonExecutable (Join-Path $toolRoot 'Save-ChartsPreviewRelease.py') --batch $batchRoot --repository $repoRoot --bundle $bundleRoot
 if($LASTEXITCODE -ne 0){throw 'Release evidence publisher rejected one or more checks'}
 if($soak.passed -ne $true -or $soak.runs.Count -ne 2 -or @($soak.runs|Where-Object status -ne 'passed').Count -ne 0){throw 'Both original eight-hour runs must pass'}
 & $PythonExecutable (Join-Path $toolRoot 'Verify-ChartsPreviewBundle.py') --bundle $bundleRoot --require-ready --output (Join-Path $closeoutRoot 'bundle-verification.json')
 if($LASTEXITCODE -ne 0){throw 'Independent portable bundle verification failed'}
 $state.status='archiving';Save-State
 $archivePath=$bundleRoot+'.zip'
 if(Test-Path -LiteralPath $archivePath){throw 'Archive already exists; retain it and inspect before producing a new candidate'}
 Compress-Archive -LiteralPath $bundleRoot -DestinationPath $archivePath -CompressionLevel Optimal
 & $PythonExecutable (Join-Path $toolRoot 'Verify-ChartsPreviewBundle.py') --bundle $bundleRoot --require-ready --archive $archivePath --output (Join-Path $closeoutRoot 'archive-verification.json')
 if($LASTEXITCODE -ne 0){throw 'Release ZIP differs from the verified bundle'}
 $archiveHash=(Get-FileHash -LiteralPath $archivePath).Hash
 "$archiveHash  $([IO.Path]::GetFileName($archivePath))"|Set-Content -LiteralPath ($archivePath+'.sha256') -Encoding utf8
 $state.archive=[ordered]@{path=$archivePath;bytes=(Get-Item -LiteralPath $archivePath).Length;sha256=$archiveHash}
 $state.status='completed';$state.passed=$true
} catch {
 $state.status='blocked';$state.error=$_.Exception.ToString()
} finally {
 $state.finishedUtc=[DateTimeOffset]::UtcNow.ToString('O');Save-State
}
if(!$state.passed){throw $state.error}
