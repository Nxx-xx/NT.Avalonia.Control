param(
    [string]$RunId = ('pdf-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')),
    [string]$OutputDirectory,
    [int]$Iterations = 0,
    [string]$Window = '1440x900',
    [string[]]$Scenarios,
    [string]$Filter,
    [int]$TimeoutSeconds = 300,
    [switch]$NoBuild,
    [string]$BaselineAggregates,
    [string]$RenderingMode = 'default'
)
# Unified performance entry (PERF01/03/16/17/19): runs the benchmark harness and prints the
# single threshold judgment produced by the harness aggregator (engineering initial gates,
# informational). Exit code is non-zero only for measurement failures (error/timeout records
# or harness failure) — not for threshold misses, which are 待校准 by definition. With
# -BaselineAggregates a self-baseline comparison is appended (never an official comparison).
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$benchmarkScript = Join-Path $PSScriptRoot 'pdfviewer/benchmark.ps1'
$arguments = @{ RunId = $RunId; Window = $Window; TimeoutSeconds = $TimeoutSeconds; RenderingMode = $RenderingMode }
if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) { $arguments.OutputDirectory = $OutputDirectory }
if ($Iterations -gt 0) { $arguments.Iterations = $Iterations }
if ($null -ne $Scenarios -and @($Scenarios).Count -gt 0) {
    # Accept -Scenarios a,b,c (binds as array) and 'a,b,c' (single string); always hand the
    # harness one comma-separated id list.
    $arguments.Scenarios = (@($Scenarios) | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ }) -join ','
}
if (-not [string]::IsNullOrWhiteSpace($Filter)) { $arguments.Filter = $Filter }
if ($NoBuild) { $arguments.NoBuild = $true }
$runLines = @(& $benchmarkScript @arguments | ForEach-Object { "$_" })
# benchmark.ps1 returns the run directory as its last output item, but build/progress lines
# share the stream — pick the last line that is an existing directory, not a message.
$run = $runLines | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Container) } | Select-Object -Last 1
if (-not $run) { throw "benchmark.ps1 did not report a run directory. Output: $($runLines -join ' | ')" }
$aggregates = Get-Content -LiteralPath (Join-Path $run 'aggregates.json') -Raw | ConvertFrom-Json
Write-Output ''
Write-Output '== PdfViewer performance thresholds (engineering initial gates, informational) =='
foreach ($group in $aggregates.groups) {
    foreach ($check in $group.thresholds) {
        $value = if ($null -ne $check.value) { $check.value } else { $check.p95 }
        Write-Output ("{0,-22} {1,-22} {2,-10} {3,-28} {4,10:F2}  {5}" -f `
            $group.corpusId, $group.scenarioId, $check.metric, $check.target, [double]$value, $check.verdict)
    }
}
$failures = @($aggregates.groups | ForEach-Object { $_ } | Where-Object { $_.errorCount -gt 0 -or $_.timeoutCount -gt 0 })
if ($failures.Count -gt 0) {
    Write-Error ("Measurement failures in: " + (($failures | ForEach-Object { "$($_.corpusId)/$($_.scenarioId)" }) -join ', '))
    exit 1
}
if (-not [string]::IsNullOrWhiteSpace($BaselineAggregates)) {
    $compare = Join-Path $PSScriptRoot 'pdfviewer/compare-baseline.ps1'
    & $compare -BaselineAggregates $BaselineAggregates -CurrentAggregates (Join-Path $run 'aggregates.json') `
        -OutputMarkdown (Join-Path $run 'baseline-comparison.md')
    if ($LASTEXITCODE -ne 0) {
        Write-Error 'Self-baseline comparison reported regressions.'
        exit 1
    }
}
Write-Output "PdfViewer performance run complete: $run"
exit 0
