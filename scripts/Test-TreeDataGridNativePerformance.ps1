param(
    [string]$OutputDirectory = ('artifacts/treedatagrid-native/' + [guid]::NewGuid().ToString('N')),
    [string]$DependencyFeed,
    [string]$ApplicationDirectory,
    [ValidateRange(600,86400)][int]$DurationSeconds = 600,
    [switch]$Pilot,
    [switch]$RefreshSourceDuringSoak,
    [switch]$BlockingGcReference,
    [switch]$AutoHeightDuringSoak,
    [string]$AutomationClient,
    [ValidateSet('query','events','both')][string]$AutomationWorkload = 'both',
    [ValidateSet('explicit-com','managed-legacy')][string]$AutomationClientBackend = 'explicit-com',
    [switch]$ShutdownAutomationProviders
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'TreeDataGrid.Validation.ps1')
$nativeRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$nativeOutput=if([IO.Path]::IsPathRooted($OutputDirectory)){[IO.Path]::GetFullPath($OutputDirectory)}else{[IO.Path]::GetFullPath((Join-Path $nativeRepo $OutputDirectory))}
if(Test-Path -LiteralPath $nativeOutput){throw 'Use a new output directory; previous native evidence must remain intact.'}
New-Item -ItemType Directory -Path $nativeOutput -Force | Out-Null
$nativeSteps=[Collections.Generic.List[object]]::new(); $nativePassed=$false; $nativeError=$null; $nativeStarted=[DateTimeOffset]::UtcNow
function Invoke-NativeStep([string]$Name, [scriptblock]$Action) {
    $nativeOkay=$false; $nativeLog=Join-Path $nativeOutput ($Name+'.log')
    try{ & $Action *>&1 | Tee-Object -FilePath $nativeLog; $nativeOkay=$true }
    finally{$nativeSteps.Add([ordered]@{name=$Name;passed=$nativeOkay;log=$nativeLog})}
}
function Assert-NativeExit([string]$Name){if($LASTEXITCODE -ne 0){throw "$Name failed: exit $LASTEXITCODE"}}
function Get-NativeManifest {
    $nativeFiles=@(& rg --files benchmarks/TreeDataGrid.NativeBenchmarks benchmarks/TreeDataGrid.AutomationClient src/NT.AvaloniaControl.TreeDataGrid src/NT.AvaloniaControl.TreeDataGrid.Themes -g '!**/bin/**' -g '!**/obj/**') + @('Directory.Build.props','Directory.Packages.props','global.json','NuGet.Config','docs/public-api.txt','scripts/Test-TreeDataGridNativePerformance.ps1','scripts/TreeDataGrid.Validation.ps1','scripts/verify_treedatagrid_native.py','scripts/Tests/test_treedatagrid_native.py','scripts/Tests/test_treedatagrid_automation.py','scripts/Collect-Platform.ps1')
    if($LASTEXITCODE -ne 0){throw 'Native source enumeration failed.'}
    if($DependencyFeed){$nativeFiles+=@(Get-ChildItem -LiteralPath $DependencyFeed -Recurse -Filter *.nupkg | ForEach-Object FullName)}
    if($ApplicationDirectory){$nativeFiles+=@(Get-ChildItem -LiteralPath $ApplicationDirectory -Recurse -File | ForEach-Object FullName)}
    if($AutomationClient){$nativeFiles+=@(Get-ChildItem -LiteralPath (Split-Path $AutomationClient -Parent) -File | ForEach-Object FullName)}
    @($nativeFiles | Sort-Object -Unique | ForEach-Object{[ordered]@{path=$_.Replace('\','/');sha256=(Get-FileHash -LiteralPath $_).Hash}})
}
function Invoke-NativeProcess([string]$Mode){
    $nativeModeOutput=Join-Path $nativeOutput $Mode
    $nativeInfo=[Diagnostics.ProcessStartInfo]::new('dotnet'); $nativeInfo.UseShellExecute=$false; $nativeInfo.CreateNoWindow=$true; $nativeInfo.WorkingDirectory=$nativeRepo
    $nativeInfo.RedirectStandardOutput=$true; $nativeInfo.RedirectStandardError=$true
    foreach($argument in @($nativeHost,'--mode',$Mode,'--output',$nativeModeOutput)){[void]$nativeInfo.ArgumentList.Add($argument)}
    if($Pilot){[void]$nativeInfo.ArgumentList.Add('--pilot')}elseif($Mode -eq 'soak'){[void]$nativeInfo.ArgumentList.Add('--duration');[void]$nativeInfo.ArgumentList.Add([string]$DurationSeconds)}
    if($Mode -eq 'soak' -and $RefreshSourceDuringSoak){[void]$nativeInfo.ArgumentList.Add('--refresh-source')}
    if($Mode -eq 'soak' -and $BlockingGcReference){[void]$nativeInfo.ArgumentList.Add('--blocking-gc-reference')}
    if($Mode -eq 'soak' -and $AutoHeightDuringSoak){[void]$nativeInfo.ArgumentList.Add('--auto-height-soak')}
    if($AutomationClient){[void]$nativeInfo.ArgumentList.Add('--automation-client');[void]$nativeInfo.ArgumentList.Add($AutomationClient);[void]$nativeInfo.ArgumentList.Add('--automation-workload');[void]$nativeInfo.ArgumentList.Add($AutomationWorkload)}
    if($AutomationClient){[void]$nativeInfo.ArgumentList.Add('--automation-client-backend');[void]$nativeInfo.ArgumentList.Add($AutomationClientBackend)}
    if($Mode -eq 'soak' -and $ShutdownAutomationProviders){[void]$nativeInfo.ArgumentList.Add('--shutdown-automation-providers')}
    $nativeProcess=[Diagnostics.Process]::new(); $nativeProcess.StartInfo=$nativeInfo
    $nativeProcessStart=[DateTimeOffset]::UtcNow; $nativeClock=[Diagnostics.Stopwatch]::StartNew(); $nativeTimeout=$false
    [void]$nativeProcess.Start(); $nativeStdout=$nativeProcess.StandardOutput.ReadToEndAsync(); $nativeStderr=$nativeProcess.StandardError.ReadToEndAsync()
    $nativeLimit=if($Pilot){180}elseif($Mode -eq 'soak'){$DurationSeconds+240}elseif($AutomationClient){1200}else{600}
    $nativeHeartbeat=0
    try {
        while(-not $nativeProcess.WaitForExit(1000)){
            if($nativeClock.Elapsed.TotalSeconds -ge $nativeLimit){$nativeTimeout=$true;$nativeProcess.Kill($true);$nativeProcess.WaitForExit();break}
            if($nativeClock.Elapsed.TotalSeconds -ge $nativeHeartbeat){Write-Host "native $Mode running: $([int]$nativeClock.Elapsed.TotalSeconds)s, process $($nativeProcess.Id)";$nativeHeartbeat=$nativeClock.Elapsed.TotalSeconds+15}
        }
        $nativeOut=$nativeStdout.GetAwaiter().GetResult(); $nativeErr=$nativeStderr.GetAwaiter().GetResult()
        $nativeOut | Set-Content -LiteralPath (Join-Path $nativeOutput ($Mode+'-stdout.log')) -Encoding utf8
        $nativeErr | Set-Content -LiteralPath (Join-Path $nativeOutput ($Mode+'-stderr.log')) -Encoding utf8
        [ordered]@{mode=$Mode;pid=$nativeProcess.Id;startedUtc=$nativeProcessStart;completedUtc=[DateTimeOffset]::UtcNow;exitCode=$nativeProcess.ExitCode;timedOut=$nativeTimeout;elapsedSeconds=$nativeClock.Elapsed.TotalSeconds;hostSha256=(Get-FileHash -LiteralPath $nativeHost).Hash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $nativeOutput ($Mode+'-process.json')) -Encoding utf8
        if($nativeTimeout -or $nativeProcess.ExitCode -ne 0){throw "Native $Mode process failed/timeout: $nativeErr"}
        $nativeGuardArgs=@('scripts/verify_treedatagrid_native.py',$nativeModeOutput,'--output',(Join-Path $nativeModeOutput 'validated.json'));if($Pilot){$nativeGuardArgs+='--allow-pilot'}
        & python @nativeGuardArgs;Assert-NativeExit 'Native protocol guard'
    }
    finally{if(-not $nativeProcess.HasExited){$nativeProcess.Kill($true);$nativeProcess.WaitForExit()};$nativeProcess.Dispose()}
}
Push-Location $nativeRepo
try{
    Invoke-NativeStep 'disk-preflight' {
        $nativeBudget=[long]((2+[Math]::Max(1,$DurationSeconds/3600)*2)*1GB)
        Assert-TreeDataGridDiskBudget -OutputDirectory $nativeOutput -Paths @($nativeOutput,$nativeRepo,[IO.Path]::GetTempPath(),$(if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages'})) -EstimatedBytes $nativeBudget
    }
    if(-not [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)){throw 'Current native performance protocol requires Windows.'}
    if($ShutdownAutomationProviders -and -not $AutomationClient){throw 'Provider shutdown requires an explicit external UIA client.'}
    if($AutomationWorkload -ne 'both' -and -not $AutomationClient){throw 'A grouped automation workload requires an explicit external UIA client.'}
    if($AutomationClientBackend -ne 'explicit-com' -and -not $AutomationClient){throw 'A client backend requires an external UIA client.'}
    if($AutomationClient){
        $AutomationClient=[IO.Path]::GetFullPath($AutomationClient)
        if(-not(Test-Path -LiteralPath $AutomationClient -PathType Leaf)){throw 'AutomationClient must be a built Windows UIA client DLL.'}
    }
    if($ApplicationDirectory){
        $ApplicationDirectory=[IO.Path]::GetFullPath($ApplicationDirectory)
        foreach($nativeRequired in @('TreeDataGrid.NativeBenchmarks.dll','TreeDataGrid.NativeBenchmarks.runtimeconfig.json','TreeDataGrid.NativeBenchmarks.deps.json','NT.AvaloniaControl.TreeDataGrid.dll','NT.AvaloniaControl.TreeDataGrid.Themes.dll')){
            if(-not(Test-Path -LiteralPath (Join-Path $ApplicationDirectory $nativeRequired) -PathType Leaf)){throw "Incomplete supplied application: $nativeRequired"}
        }
        $nativeInputInsideOutput=$ApplicationDirectory.StartsWith($nativeOutput+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)
        $nativeOutputInsideInput=$nativeOutput.StartsWith($ApplicationDirectory+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)
        if($nativeInputInsideOutput -or $nativeOutputInsideInput -or $ApplicationDirectory -eq $nativeOutput){throw 'The supplied application and evidence directories must not overlap.'}
    }
    $nativeRestore=@()
    if($DependencyFeed){
        $DependencyFeed=[IO.Path]::GetFullPath($DependencyFeed);if(-not(Test-Path -LiteralPath $DependencyFeed -PathType Container)){throw 'DependencyFeed must exist.'}
        $nativeConfig=Join-Path $nativeOutput 'NuGet.Offline.Config'
        ('<configuration><packageSources><clear /><add key="dependencies" value="'+[System.Security.SecurityElement]::Escape($DependencyFeed)+'" /></packageSources></configuration>')|Set-Content -LiteralPath $nativeConfig -Encoding utf8
        $nativeRestore=@('--configfile',$nativeConfig)
    }
    $nativeBefore=@(Get-NativeManifest);$nativeBefore|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $nativeOutput 'source-manifest.json') -Encoding utf8
    if($ApplicationDirectory){
        Invoke-NativeStep 'copy-supplied-application'{
            $nativeApplication=Join-Path $nativeOutput 'application';New-Item -ItemType Directory -Path $nativeApplication | Out-Null
            Get-ChildItem -LiteralPath $ApplicationDirectory | Copy-Item -Destination $nativeApplication -Recurse
        }
    }else{Invoke-NativeStep 'locked-restore-build'{
        & dotnet restore benchmarks/TreeDataGrid.NativeBenchmarks/TreeDataGrid.NativeBenchmarks.csproj --locked-mode --disable-parallel -m:1 @nativeRestore;Assert-NativeExit 'Native locked restore'
        & dotnet build benchmarks/TreeDataGrid.NativeBenchmarks/TreeDataGrid.NativeBenchmarks.csproj -c Release --no-restore -m:1;Assert-NativeExit 'Native Release build'
        & dotnet msbuild benchmarks/TreeDataGrid.NativeBenchmarks/TreeDataGrid.NativeBenchmarks.csproj -t:Publish -p:NoBuild=true -p:Configuration=Release "-p:PublishDir=$nativeOutput/application/" -m:1 -nr:false -v:minimal;Assert-NativeExit 'Native isolated publish'
    }}
    Invoke-NativeStep 'guard-tests'{ & python -m unittest discover -s scripts/Tests -p test_treedatagrid_native.py -v;Assert-NativeExit 'Native guard tests' }
    Invoke-NativeStep 'automation-guard-tests'{ & python -m unittest discover -s scripts/Tests -p test_treedatagrid_automation.py -v;Assert-NativeExit 'Automation guard tests' }
    & (Join-Path $PSScriptRoot 'Collect-Platform.ps1') -OutputPath (Join-Path $nativeOutput 'environment.json')
    $nativeHost=Join-Path $nativeOutput 'application/TreeDataGrid.NativeBenchmarks.dll'
    @(Get-ChildItem -LiteralPath (Join-Path $nativeOutput 'application') -File | Where-Object Extension -in @('.dll','.json') | ForEach-Object{[ordered]@{path=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $nativeOutput 'application-manifest.json') -Encoding utf8
    Invoke-NativeStep 'performance'{Invoke-NativeProcess 'performance'}
    Invoke-NativeStep 'soak'{Invoke-NativeProcess 'soak'}
    $nativeAfter=@(Get-NativeManifest);$nativeAfter|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $nativeOutput 'source-manifest-final.json') -Encoding utf8
    if(($nativeBefore|ConvertTo-Json -Compress) -cne ($nativeAfter|ConvertTo-Json -Compress)){throw 'Native scope inputs changed during measurement.'}
    $nativePerformance=Get-Content -LiteralPath (Join-Path $nativeOutput 'performance/report.json') -Raw|ConvertFrom-Json
    $nativeSoak=Get-Content -LiteralPath (Join-Path $nativeOutput 'soak/report.json') -Raw|ConvertFrom-Json
    if($nativePerformance.environment.functionDll.sha256 -cne $nativeSoak.environment.functionDll.sha256 -or $nativePerformance.environment.hostDll.sha256 -cne $nativeSoak.environment.hostDll.sha256){throw 'Native performance/soak used different binaries.'}
    $nativePassed=$true
}
catch{$nativeError=$_.Exception.Message;throw}
finally{
    [ordered]@{stage='A6-Windows-native-measurements';passed=$nativePassed;pilot=[bool]$Pilot;applicationInput=$ApplicationDirectory;startedUtc=$nativeStarted;completedUtc=[DateTimeOffset]::UtcNow;error=$nativeError;steps=$nativeSteps.ToArray();scope='Native render completion and finite soak; not physical scan-out FPS, physical input latency, multi-hour endurance or a product performance budget'}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $nativeOutput 'results.json') -Encoding utf8
    try { Write-TreeDataGridValidationSummary -OutputDirectory $nativeOutput } finally { Pop-Location }
}
