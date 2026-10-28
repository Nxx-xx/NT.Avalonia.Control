param(
    [string]$RunId = ('pdf-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
    [string]$OutputDirectory,
    [int]$Iterations = 0,
    [string]$Window = '1440x900',
    [string[]]$Scenarios,
    [string]$Filter,
    [int]$TimeoutSeconds = 300,
    [switch]$NoBuild,
    [switch]$InProcess,
    [string]$RenderingMode = 'default'
)
# Windows entry for the PdfViewer performance harness (PERF19). Builds the benchmark
# executable, runs one worker process per corpus x scenario x iteration and aggregates the
# raw records into artifacts/pdfviewer/<run-id>/performance/. All results are self
# performance numbers; official A/B stays blocked-reference (see benchmarks/pdfviewer/README.md).
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..' '..'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repo ('artifacts/pdfviewer/' + $RunId + '/performance')
}
$run = [IO.Path]::GetFullPath($OutputDirectory, $repo)
[IO.Directory]::CreateDirectory($run) | Out-Null
Push-Location -LiteralPath $repo
try {
    $project = 'benchmarks/PdfViewer.Benchmarks/PdfViewer.Benchmarks.csproj'
    if (-not $NoBuild) {
        # RestoreLockedMode as an MSBuild property: `dotnet build --locked-mode` is rejected by SDK 10.
        dotnet build $project -c Release -p:RestoreLockedMode=true -m:1 -nr:false -v:minimal
        if ($LASTEXITCODE -ne 0) { throw "PdfViewer.Benchmarks build failed ($LASTEXITCODE)." }
    }
    $exe = Join-Path $repo 'benchmarks/PdfViewer.Benchmarks/bin/Release/net10.0/PdfViewer.Benchmarks.exe'
    if (-not (Test-Path -LiteralPath $exe)) {
        $exe = Join-Path $repo 'benchmarks/PdfViewer.Benchmarks/bin/Release/net10.0/PdfViewer.Benchmarks.dll'
    }
    $arguments = @('--output', $run, '--run-id', $RunId, '--window', $Window, '--timeout-seconds', $TimeoutSeconds.ToString(), '--rendering-mode', $RenderingMode)
    if ($Iterations -gt 0) { $arguments += @('--iterations', $Iterations.ToString()) }
    if ($null -ne $Scenarios -and @($Scenarios).Count -gt 0) {
        $arguments += @('--scenarios', ((@($Scenarios) | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ }) -join ','))
    }
    if (-not [string]::IsNullOrWhiteSpace($Filter)) { $arguments += @('--filter', $Filter) }
    if ($InProcess) { $arguments += '--in-process' }
    if ($exe.EndsWith('.dll')) { $arguments = @($exe) + $arguments; $exe = 'dotnet' }
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "PdfViewer benchmark run failed ($LASTEXITCODE). Records with error/timeout status are in $run/raw." }
    foreach ($artifact in @('run-env.json', 'aggregates.json', 'report.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $run $artifact))) { throw "Benchmark output is missing $artifact." }
    }
    Write-Output "PdfViewer benchmark artifacts: $run"
    return $run
}
finally {
    Pop-Location
}
