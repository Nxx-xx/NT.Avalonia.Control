param(
    [Parameter(Mandatory = $true)][string] $HostDirectory,
    [Parameter(Mandatory = $true)][string] $OutputDirectory,
    [string] $Phase = 'allocation-diagnostic',
    [string] $BaselineReport,
    [int] $Samples = 2,
    [int] $Warmups = 1,
    [int] $HitQueries = 5,
    [string] $TraceTool
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$hostRoot = (Resolve-Path -LiteralPath $HostDirectory).Path
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw "OutputDirectory must be a new directory: $outputRoot" }
$null = New-Item -ItemType Directory -Path $outputRoot
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $outputRoot 'capture-script.ps1')
if (-not $TraceTool) { $TraceTool = Join-Path $repositoryRoot 'artifacts/tools/charts-diagnostics/dotnet-trace.exe' }
$tracePath = (Resolve-Path -LiteralPath $TraceTool).Path
$dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
$hostDll = Join-Path $hostRoot 'Charts.Benchmarks.dll'
if (-not (Test-Path -LiteralPath $hostDll)) { throw "Missing benchmark host: $hostDll" }

function Get-HostManifest {
    @(Get-ChildItem -LiteralPath $hostRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = [IO.Path]::GetRelativePath($hostRoot, $_.FullName); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
}
$before = Get-HostManifest
$baseline = $null
if ($BaselineReport) {
    $baselinePath = (Resolve-Path -LiteralPath $BaselineReport).Path
    $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    foreach ($entry in @(@('harness', 'Charts.Benchmarks.dll'), @('charts', 'NT.AvaloniaControl.Charts.dll'), @('themes', 'NT.AvaloniaControl.Charts.Themes.dll'))) {
        $expected = $baseline.sources.($entry[0]).sha256
        $actual = ($before | Where-Object path -EQ $entry[1]).sha256
        if (-not $expected -or $actual -ne $expected) { throw "Frozen host SHA mismatch for $($entry[1])." }
    }
}
$runId = Split-Path -Leaf $outputRoot
$traceFile = Join-Path $outputRoot 'trace.nettrace'
$rawFile = Join-Path $outputRoot 'raw.json'
$arguments = @('collect', '--profile', 'gc-verbose,dotnet-sampled-thread-time', '--buffersize', '512',
    '--show-child-io', '--output', $traceFile, '--', $dotnetPath, $hostDll,
    '--quick', '--scenarios', 'S1M-L', '--samples', "$Samples", '--warmups', "$Warmups",
    '--hit-queries', "$HitQueries", '--phase', $Phase, '--run-id', $runId, '--output', $rawFile)
$metadata = [ordered]@{
    schemaVersion = 1; runId = $runId; phase = $Phase; status = 'collecting'; startedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    diagnosticOnly = $true; timingsEligibleForPerformanceComparison = $false
    semantics = 'Allocation ticks and managed thread-stack samples are statistical observations, not exact per-type allocations or native CPU measurements.'
    traceTool = [ordered]@{ path = $tracePath; version = (& $tracePath --version | Out-String).Trim(); sha256 = (Get-FileHash -LiteralPath $tracePath -Algorithm SHA256).Hash }
    captureScript = [ordered]@{ path = Join-Path $outputRoot 'capture-script.ps1'; sha256 = (Get-FileHash -LiteralPath (Join-Path $outputRoot 'capture-script.ps1') -Algorithm SHA256).Hash }
    command = [ordered]@{ executable = $tracePath; arguments = $arguments; workingDirectory = $repositoryRoot }
    profiles = @('gc-verbose', 'dotnet-sampled-thread-time'); bufferMegabytes = 512; bufferingMode = 'Drop (.NET 10)'
    hostDirectory = $hostRoot; hostBefore = $before
    baselineReport = if ($BaselineReport) { [ordered]@{ path = $baselinePath; sha256 = (Get-FileHash -LiteralPath $baselinePath -Algorithm SHA256).Hash; sourceHashesMatched = $true } } else { $null }
    environment = [ordered]@{ osDescription = [Runtime.InteropServices.RuntimeInformation]::OSDescription; processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString(); logicalProcessors = [Environment]::ProcessorCount }
}
function Save-Metadata { $metadata | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $outputRoot 'capture.json') -Encoding utf8 }
Save-Metadata
& $tracePath collect --help | Set-Content -LiteralPath (Join-Path $outputRoot 'collect-help.txt') -Encoding utf8
& $tracePath list-profiles | Set-Content -LiteralPath (Join-Path $outputRoot 'profiles.txt') -Encoding utf8
& $dotnetPath --info 2>&1 | Set-Content -LiteralPath (Join-Path $outputRoot 'dotnet-info.txt') -Encoding utf8
$metadata.environment.dotnetInfoExitCode = $LASTEXITCODE
& $dotnetPath --list-runtimes 2>&1 | Set-Content -LiteralPath (Join-Path $outputRoot 'dotnet-runtimes.txt') -Encoding utf8
& $dotnetPath --list-sdks 2>&1 | Set-Content -LiteralPath (Join-Path $outputRoot 'dotnet-sdks.txt') -Encoding utf8
Save-Metadata
$exitCode = -1
try {
    Push-Location -LiteralPath $repositoryRoot
    try { & $tracePath @arguments 2>&1 | Tee-Object -FilePath (Join-Path $outputRoot 'collect-output.txt'); $exitCode = $LASTEXITCODE }
    finally { Pop-Location }
    $metadata.exitCode = $exitCode
    $metadata.status = if ($exitCode -eq 0) { 'collected' } else { 'failed' }
}
finally {
    $metadata.finishedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $metadata.hostAfter = Get-HostManifest
    $metadata.frozenHostUnchanged = ($before | ConvertTo-Json -Depth 6 -Compress) -eq ($metadata.hostAfter | ConvertTo-Json -Depth 6 -Compress)
    foreach ($entry in @(@('trace', $traceFile), @('raw', $rawFile))) {
        if (Test-Path -LiteralPath $entry[1]) { $metadata.($entry[0]) = [ordered]@{ path = $entry[1]; bytes = (Get-Item -LiteralPath $entry[1]).Length; sha256 = (Get-FileHash -LiteralPath $entry[1] -Algorithm SHA256).Hash } }
    }
    Save-Metadata
}
if ($exitCode -ne 0) { throw "Diagnostic collection failed with exit code $exitCode; inspect capture.json and collect-output.txt." }
if (-not $metadata.frozenHostUnchanged) { throw 'Frozen host files changed during capture.' }
Write-Output "Diagnostic capture: $outputRoot"
