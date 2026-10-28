param(
    [Parameter(Mandatory)][string]$BeforeApplication,
    [Parameter(Mandatory)][string]$AfterApplication,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$WaitForResults,
    [string]$AutomationClient,
    [ValidateSet('explicit-com','managed-legacy')][string]$AutomationClientBackend = 'explicit-com'
)
$ErrorActionPreference='Stop'
$pairRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function Resolve-PairPath([string]$Path){[IO.Path]::GetFullPath((Join-Path $pairRepo $Path))}
$pairBefore=Resolve-PairPath $BeforeApplication; $pairAfter=Resolve-PairPath $AfterApplication
$pairOutput=Resolve-PairPath $OutputDirectory
if(Test-Path -LiteralPath $pairOutput){throw 'Use a new paired evidence directory.'}
foreach($pairInput in @($pairBefore,$pairAfter)){
    if(-not(Test-Path -LiteralPath (Join-Path $pairInput 'TreeDataGrid.NativeBenchmarks.dll') -PathType Leaf)){throw 'Both inputs must be isolated native applications.'}
    if($pairOutput.StartsWith($pairInput+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must be outside the application inputs.'}
}
New-Item -ItemType Directory -Path $pairOutput | Out-Null
$pairPassed=$false; $pairError=$null; $pairStarted=[DateTimeOffset]::UtcNow
function Get-PairManifest([string]$Directory){
    @(Get-ChildItem -LiteralPath $Directory -Recurse -File | Sort-Object FullName | ForEach-Object{[ordered]@{path=[IO.Path]::GetRelativePath($Directory,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
}
function Invoke-PairPerformance([string]$Name,[string]$Application){
    $pairRun=Join-Path $pairOutput $Name; $pairHost=Join-Path $Application 'TreeDataGrid.NativeBenchmarks.dll'
    $pairInfo=[Diagnostics.ProcessStartInfo]::new('dotnet');$pairInfo.UseShellExecute=$false;$pairInfo.CreateNoWindow=$true
    $pairInfo.RedirectStandardOutput=$true;$pairInfo.RedirectStandardError=$true;$pairInfo.WorkingDirectory=$pairRepo
    foreach($pairArg in @($pairHost,'--mode','performance','--output',$pairRun)){$pairInfo.ArgumentList.Add($pairArg)}
    if($AutomationClient){$pairInfo.ArgumentList.Add('--automation-client');$pairInfo.ArgumentList.Add([IO.Path]::GetFullPath($AutomationClient))}
    if($AutomationClient){$pairInfo.ArgumentList.Add('--automation-client-backend');$pairInfo.ArgumentList.Add($AutomationClientBackend)}
    $pairProcess=[Diagnostics.Process]::Start($pairInfo);$pairClock=[Diagnostics.Stopwatch]::StartNew();$pairHeartbeat=15
    $pairOut=$pairProcess.StandardOutput.ReadToEndAsync();$pairErr=$pairProcess.StandardError.ReadToEndAsync();$pairProcessStart=[DateTimeOffset]::UtcNow;$pairTimeout=$false
    try{
        while(-not $pairProcess.WaitForExit(1000)){
            if($pairClock.Elapsed.TotalSeconds -gt $(if($AutomationClient){1200}else{600})){$pairTimeout=$true;throw "Paired $Name timeout."}
            if($pairClock.Elapsed.TotalSeconds -ge $pairHeartbeat){Write-Host "paired $Name $([int]$pairClock.Elapsed.TotalSeconds)s";$pairHeartbeat+=15}
        }
        if($pairProcess.ExitCode -ne 0){throw "Paired $Name failed."}
    }
    finally{
        if(-not $pairProcess.HasExited){$pairProcess.Kill($true);$pairProcess.WaitForExit()}
        $pairOut.GetAwaiter().GetResult()|Set-Content -LiteralPath (Join-Path $pairRun 'stdout.log') -Encoding utf8
        $pairErr.GetAwaiter().GetResult()|Set-Content -LiteralPath (Join-Path $pairRun 'stderr.log') -Encoding utf8
        [ordered]@{pid=$pairProcess.Id;exitCode=$pairProcess.ExitCode;timedOut=$pairTimeout;startedUtc=$pairProcessStart;completedUtc=[DateTimeOffset]::UtcNow;profiled=$false;hostSha256=(Get-FileHash -LiteralPath $pairHost).Hash}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $pairRun 'process.json') -Encoding utf8
        $pairProcess.Dispose()
    }
    & python (Join-Path $PSScriptRoot 'verify_treedatagrid_native.py') $pairRun --output (Join-Path $pairRun 'validated.json')
    if($LASTEXITCODE -ne 0){throw "Paired $Name raw records failed validation."}
}
Push-Location $pairRepo
try{
    [ordered]@{status='waiting-or-starting';order=@('after','before');waitForResults=$WaitForResults;startedUtc=$pairStarted}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $pairOutput 'initial.json') -Encoding utf8
    if($WaitForResults){
        $pairWait=Resolve-PairPath $WaitForResults;$pairWaitClock=[Diagnostics.Stopwatch]::StartNew()
        while($true){
            if($pairWaitClock.Elapsed.TotalSeconds -gt 3600){throw 'Previous run completion timeout.'}
            if(Test-Path -LiteralPath $pairWait){
                $pairPrevious=$null
                try{$pairPrevious=Get-Content -LiteralPath $pairWait -Raw|ConvertFrom-Json}catch{Start-Sleep -Milliseconds 500;continue}
                if(-not $pairPrevious.passed){throw 'Previous run failed; do not start counterbalanced performance.'}
                break
            }
            Start-Sleep -Seconds 1
        }
    }
    $pairManifests=@{before=(Get-PairManifest $pairBefore);after=(Get-PairManifest $pairAfter)}
    $pairManifests|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $pairOutput 'application-manifests.json') -Encoding utf8
    if($pairManifests.before.Count -ne $pairManifests.after.Count){throw 'Application file lists differ.'}
    $pairDifferences=@(for($pairIndex=0;$pairIndex -lt $pairManifests.before.Count;$pairIndex++){
        $pairOld=$pairManifests.before[$pairIndex];$pairNew=$pairManifests.after[$pairIndex]
        if($pairOld.path -cne $pairNew.path){throw 'Application file names differ.'}
        if($pairOld.sha256 -cne $pairNew.sha256){$pairOld.path}
    })
    if($pairDifferences.Count -ne 2 -or $pairDifferences -cnotcontains 'NT.AvaloniaControl.TreeDataGrid.dll' -or $pairDifferences -cnotcontains 'NT.AvaloniaControl.TreeDataGrid.pdb'){throw 'Only the function DLL/PDB may differ in this comparison.'}
    $pairInputs=@('scripts/Compare-TreeDataGridNativePerformance.ps1','scripts/verify_treedatagrid_native.py')|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $_).Hash}}
    if($AutomationClient){$pairInputs+=@(Get-ChildItem -LiteralPath (Split-Path ([IO.Path]::GetFullPath($AutomationClient)) -Parent) -File | ForEach-Object{[ordered]@{path=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})}
    $pairInputs|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $pairOutput 'source-manifest.json') -Encoding utf8
    Invoke-PairPerformance 'after' $pairAfter
    Invoke-PairPerformance 'before' $pairBefore
    $pairFinal=@{before=(Get-PairManifest $pairBefore);after=(Get-PairManifest $pairAfter)}
    $pairFinal|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $pairOutput 'application-manifests-final.json') -Encoding utf8
    foreach($pairKey in @('before','after')){if(($pairManifests[$pairKey]|ConvertTo-Json -Compress) -cne ($pairFinal[$pairKey]|ConvertTo-Json -Compress)){throw 'Paired application input changed.'}}
    foreach($pairInput in $pairInputs){if((Get-FileHash -LiteralPath $pairInput.path).Hash -cne $pairInput.sha256){throw 'Paired driver input changed.'}}
    $pairPassed=$true
}
catch{$pairError=$_.Exception.Message;throw}
finally{
    [ordered]@{passed=$pairPassed;startedUtc=$pairStarted;completedUtc=[DateTimeOffset]::UtcNow;error=$pairError;order=@('after','before');profiled=$false;boundary='Additional reverse-order repetition, not a replacement for the initial pair or a physical performance SLA.'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $pairOutput 'results.json') -Encoding utf8
    Pop-Location
}
