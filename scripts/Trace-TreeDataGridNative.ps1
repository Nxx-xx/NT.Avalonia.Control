param(
    [Parameter(Mandatory)][string]$HostDll,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('performance','soak')][string]$Mode = 'performance',
    [string]$NuGetConfig,
    [ValidateRange(10,600)][int]$CaptureSeconds = 300,
    [ValidateRange(10,600)][int]$DiagnosticSoakSeconds = 60
)
$ErrorActionPreference = 'Stop'
$profileRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$profileOutput = [IO.Path]::GetFullPath((Join-Path $profileRepo $OutputDirectory))
$profileHost = [IO.Path]::GetFullPath((Join-Path $profileRepo $HostDll))
if (-not (Test-Path -LiteralPath $profileHost -PathType Leaf)) { throw 'HostDll must be an existing isolated native host.' }
if (Test-Path -LiteralPath $profileOutput) { throw 'Use a new diagnostic evidence directory.' }
New-Item -ItemType Directory -Path $profileOutput | Out-Null
$profilePassed = $false; $profileError = $null; $profileStarted = [DateTimeOffset]::UtcNow
Push-Location $profileRepo
try {
    $profileInputs = @(& rg --files benchmarks/TreeDataGrid.ProfileAnalysis src/NT.AvaloniaControl.TreeDataGrid -g '!**/bin/**' -g '!**/obj/**') + @('scripts/Trace-TreeDataGridNative.ps1','Directory.Build.props','Directory.Packages.props')
    function Get-ProfileInputs { @($profileInputs | Sort-Object -Unique | ForEach-Object { [ordered]@{ path=$_; sha256=(Get-FileHash -LiteralPath $_).Hash } }) }
    $profileBefore = Get-ProfileInputs
    $profileBefore | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $profileOutput 'source-manifest.json') -Encoding utf8
    $profileRestore = @(); if ($NuGetConfig) { $profileRestore = @('--configfile', $NuGetConfig) }
    & dotnet restore benchmarks/TreeDataGrid.ProfileAnalysis/TreeDataGrid.ProfileAnalysis.csproj --locked-mode --disable-parallel -m:1 @profileRestore *> (Join-Path $profileOutput 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Profile tool locked restore failed.' }
    & dotnet build benchmarks/TreeDataGrid.ProfileAnalysis/TreeDataGrid.ProfileAnalysis.csproj -c Release --no-restore -m:1 *>> (Join-Path $profileOutput 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Profile tool build failed.' }
    # Run an isolated copy: rebuilding the tool later cannot overwrite a running collector.
    $profileToolDir = Join-Path $profileOutput 'tool'; New-Item -ItemType Directory -Path $profileToolDir | Out-Null
    Copy-Item -Path 'benchmarks/TreeDataGrid.ProfileAnalysis/bin/Release/net10.0/*' -Destination $profileToolDir
    $profileTool = Join-Path $profileToolDir 'TreeDataGrid.ProfileAnalysis.dll'
    $profileCapture = Join-Path $profileOutput 'capture'
    $profileStart = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $profileStart.UseShellExecute=$false; $profileStart.CreateNoWindow=$true; $profileStart.RedirectStandardOutput=$true; $profileStart.RedirectStandardError=$true
    foreach ($profileArg in @($profileTool,'collect',$profileHost,$profileCapture,$Mode,"$CaptureSeconds")) { $profileStart.ArgumentList.Add($profileArg) }
    if ($Mode -eq 'soak') { foreach ($profileArg in @('--pilot','--duration',"$DiagnosticSoakSeconds")) { $profileStart.ArgumentList.Add($profileArg) } }
    $profileProcess = [Diagnostics.Process]::Start($profileStart); $profileClock=[Diagnostics.Stopwatch]::StartNew()
    $profileOut=$profileProcess.StandardOutput.ReadToEndAsync(); $profileErr=$profileProcess.StandardError.ReadToEndAsync(); $profileHeartbeat=15
    try {
        while (-not $profileProcess.WaitForExit(1000)) {
            if ($profileClock.Elapsed.TotalSeconds -gt ($CaptureSeconds + 800)) { throw 'Diagnostic supervisor timeout.' }
            if ($profileClock.Elapsed.TotalSeconds -ge $profileHeartbeat) { Write-Host "native diagnostic $Mode $([int]$profileClock.Elapsed.TotalSeconds)s"; $profileHeartbeat+=15 }
        }
        $profileOut.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $profileOutput 'collector-stdout.log') -Encoding utf8
        $profileErr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $profileOutput 'collector-stderr.log') -Encoding utf8
        if ($profileProcess.ExitCode -ne 0) { throw 'Diagnostic collector or native host failed.' }
    }
    finally { if (-not $profileProcess.HasExited) { $profileProcess.Kill($true); $profileProcess.WaitForExit() }; $profileProcess.Dispose() }
    $profileGuard = @('scripts/verify_treedatagrid_native.py',(Join-Path $profileCapture 'host'),'--output',(Join-Path $profileCapture 'host/validated.json'))
    if ($Mode -eq 'soak') { $profileGuard += '--allow-pilot' }
    & python @profileGuard *> (Join-Path $profileOutput 'host-validation.log')
    if ($LASTEXITCODE -ne 0) { throw 'Profiled host raw-record validation failed.' }
    & dotnet $profileTool analyze (Join-Path $profileCapture 'profile.nettrace') (Join-Path $profileCapture 'host/events.jsonl') (Join-Path $profileOutput 'attribution.json') *> (Join-Path $profileOutput 'analysis.log')
    if ($LASTEXITCODE -ne 0) { throw 'Profile attribution failed or events were lost.' }
    $profileAfter=Get-ProfileInputs
    $profileAfter | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $profileOutput 'source-manifest-final.json') -Encoding utf8
    if (($profileBefore | ConvertTo-Json -Compress) -cne ($profileAfter | ConvertTo-Json -Compress)) { throw 'Diagnostic scope changed during capture.' }
    @('capture/profile.nettrace','capture/host/events.jsonl','capture/host/report.json','attribution.json','tool/TreeDataGrid.ProfileAnalysis.dll') | ForEach-Object { [ordered]@{ path=$_; sha256=(Get-FileHash -LiteralPath (Join-Path $profileOutput $_)).Hash } } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $profileOutput 'evidence-hashes.json') -Encoding utf8
    $profilePassed=$true
}
catch { $profileError=$_.Exception.Message; throw }
finally {
    [ordered]@{ passed=$profilePassed; diagnosticOnly=$true; mode=$Mode; startedUtc=$profileStarted; completedUtc=[DateTimeOffset]::UtcNow; error=$profileError; hostDll=$profileHost; hostSha256=(Get-FileHash -LiteralPath $profileHost).Hash; boundary='Sampled diagnostic attribution only. Performance and soak acceptance require separate unprofiled processes.' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $profileOutput 'results.json') -Encoding utf8
    Pop-Location
}
