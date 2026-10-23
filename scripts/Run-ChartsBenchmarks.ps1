param(
    [switch]$Quick,
    [string]$Phase = 'C0-before-C1',
    [string]$OutputDirectory,
    [ValidateRange(1,1000)][int]$Samples = 2,
    [ValidateRange(0,100)][int]$Warmups = 1,
    [ValidateRange(1,10000)][int]$HitQueries = 5,
    [ValidateRange(1,7200)][int]$TimeoutSeconds = 300,
    [ValidateRange(1,3600)][int]$SceneTimeoutSeconds = 120,
    [string]$Scenarios,
    [switch]$SkipBuild,
    [string]$HostDirectory
)
$ErrorActionPreference = 'Stop'
$chartsRepo = Split-Path $PSScriptRoot -Parent
$chartsRunId = [Guid]::NewGuid().ToString('N')
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $chartsRepo "artifacts/charts-performance/$chartsRunId" }
$chartsOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $chartsOutput) { throw 'Output directory already exists; use a new immutable run directory.' }
[IO.Directory]::CreateDirectory($chartsOutput) | Out-Null
$chartsProject = Join-Path $chartsRepo 'benchmarks/Charts.Benchmarks/Charts.Benchmarks.csproj'
$chartsAssembly = Join-Path $chartsRepo 'benchmarks/Charts.Benchmarks/bin/Release/net10.0/Charts.Benchmarks.dll'
if ($HostDirectory) {
    if (-not $SkipBuild) { throw '-HostDirectory requires -SkipBuild; frozen hosts must never be rebuilt in place.' }
    $chartsAssembly = Join-Path ([IO.Path]::GetFullPath($HostDirectory)) 'Charts.Benchmarks.dll'
}
$chartsRaw = Join-Path $chartsOutput 'raw.json'
$chartsGuard = Join-Path $chartsOutput 'guard.json'
$chartsSupervisor = [ordered]@{ runId=$chartsRunId; phase=$Phase; startedUtc=[DateTimeOffset]::UtcNow.ToString('O'); timedOut=$false; processExitCode=$null; guardPassed=$false; passed=$false; error=$null; scope='C0 diagnostic prototype; no FPS/framePipeline/present qualification' }
try {
    $chartsFiles = @(
        Get-ChildItem -LiteralPath (Join-Path $chartsRepo 'src/NT.AvaloniaControl.Charts') -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
        Get-ChildItem -LiteralPath (Join-Path $chartsRepo 'src/NT.AvaloniaControl.Charts.Themes') -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
        Get-ChildItem -LiteralPath (Join-Path $chartsRepo 'benchmarks/Charts.Benchmarks') -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
        Get-Item -LiteralPath (Join-Path $chartsRepo 'Directory.Packages.props'),(Join-Path $chartsRepo 'global.json'),$PSCommandPath,(Join-Path $PSScriptRoot 'Test-ChartsPerformance.ps1')
    )
    $chartsSource = @($chartsFiles | Sort-Object FullName -Unique | ForEach-Object { [ordered]@{ path=[IO.Path]::GetRelativePath($chartsRepo,$_.FullName); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } })
    [ordered]@{ runId=$chartsRunId; phase=$Phase; files=$chartsSource; capturedUtc=[DateTimeOffset]::UtcNow.ToString('O'); sourceKind= $(if ($HostDirectory) { 'workspace-capture-not-proof-of-frozen-host-source; use-frozen-source-and-raw-assembly-hashes' } else { 'workspace-source-at-launch' }); hostAssembly=$chartsAssembly } | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $chartsOutput 'source.json') -Encoding utf8
    if (-not $SkipBuild) {
        & dotnet build $chartsProject -c Release --disable-build-servers -m:1 -p:UseSharedCompilation=false *> (Join-Path $chartsOutput 'build.log')
        if ($LASTEXITCODE -ne 0) { throw 'Benchmark build failed; inspect build.log.' }
    }
    if (-not (Test-Path -LiteralPath $chartsAssembly)) { throw 'Benchmark assembly missing; build Release or omit -SkipBuild.' }
    $chartsStart = [Diagnostics.ProcessStartInfo]::new()
    $chartsStart.FileName = (Get-Command dotnet -ErrorAction Stop).Source
    $chartsStart.WorkingDirectory = $chartsRepo
    $chartsStart.UseShellExecute = $false
    $chartsStart.CreateNoWindow = $true
    $chartsStart.RedirectStandardOutput = $true
    $chartsStart.RedirectStandardError = $true
    $chartsArguments = @($chartsAssembly,'--output',$chartsRaw,'--run-id',$chartsRunId,'--phase',$Phase,'--samples',[string]$Samples,'--warmups',[string]$Warmups,'--hit-queries',[string]$HitQueries,'--scene-timeout-seconds',[string]$SceneTimeoutSeconds)
    if ($Quick) { $chartsArguments += '--quick' }
    if ($Scenarios) { $chartsArguments += @('--scenarios',$Scenarios) }
    foreach ($argument in $chartsArguments) { $chartsStart.ArgumentList.Add($argument) }
    $chartsProcess = [Diagnostics.Process]::new(); $chartsProcess.StartInfo = $chartsStart
    try {
        if (-not $chartsProcess.Start()) { throw 'Unable to start benchmark process.' }
        $chartsStdoutTask = $chartsProcess.StandardOutput.ReadToEndAsync(); $chartsStderrTask = $chartsProcess.StandardError.ReadToEndAsync()
        if (-not $chartsProcess.WaitForExit($TimeoutSeconds * 1000)) {
            $chartsSupervisor.timedOut = $true
            $chartsProcess.Kill($true)
            $chartsProcess.WaitForExit()
        }
        $chartsStdoutTask.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $chartsOutput 'stdout.log') -Encoding utf8
        $chartsStderrTask.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $chartsOutput 'stderr.log') -Encoding utf8
        $chartsSupervisor.processExitCode = $chartsProcess.ExitCode
    } finally { $chartsProcess.Dispose() }
    if ($chartsSupervisor.timedOut) { throw 'Benchmark process wall timeout; partial raw/logs are retained.' }
    if ($chartsSupervisor.processExitCode -ne 0) { throw 'Benchmark process failed; inspect raw.json/stderr.log.' }
    & (Join-Path $PSScriptRoot 'Test-ChartsPerformance.ps1') -ReportPath $chartsRaw -OutputPath $chartsGuard
    if (-not (Test-Path -LiteralPath $chartsGuard)) { throw 'Guard report missing.' }
    $chartsGuardResult = Get-Content -LiteralPath $chartsGuard -Raw | ConvertFrom-Json
    $chartsSupervisor.guardPassed = $chartsGuardResult.passed -eq $true
    if (-not $chartsSupervisor.guardPassed) { throw 'Independent raw validation failed.' }
    $chartsSupervisor.passed = $true
} catch { $chartsSupervisor.error = $_.Exception.Message }
finally {
    $chartsSupervisor.finishedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $chartsSupervisor | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $chartsOutput 'supervisor.json') -Encoding utf8
}
$chartsSupervisor | ConvertTo-Json -Depth 20
if (-not $chartsSupervisor.passed) { exit 1 }
