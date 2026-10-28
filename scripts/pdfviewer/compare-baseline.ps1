param(
    [Parameter(Mandatory = $true)][string]$BaselineAggregates,
    [Parameter(Mandatory = $true)][string]$CurrentAggregates,
    [double]$RegressionFactor = 1.25,
    [string]$OutputMarkdown
)
# Compares two aggregates.json files produced by the PdfViewer performance harness.
# Both sides are self measurements unless a side's metadata names a legal reference host,
# which nothing currently produces — so the comparison is always self-baseline vs self-run
# and never an official A/B. Regression = p95 of a shared metric grows past
# $RegressionFactor x the baseline p95 (the 1.25x goal factor from the A/B protocol).
$ErrorActionPreference = 'Stop'
function Read-Aggregates([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $full)) { throw "Aggregates not found: $full" }
    return (Get-Content -LiteralPath $full -Raw | ConvertFrom-Json)
}
$baseline = Read-Aggregates $BaselineAggregates
$current = Read-Aggregates $CurrentAggregates
$baselineLabel = "self-baseline run $($baseline.runId)"
$currentLabel = "self-current run $($current.runId)"
if ($baseline.referenceAvailability -ne 'blocked-reference' -or $current.referenceAvailability -ne 'blocked-reference') {
    Write-Warning 'One side claims a reference is available. Verify its provenance before reading this as an official comparison.'
}
$index = @{}
foreach ($group in $baseline.groups) {
    foreach ($metric in $group.metrics.PSObject.Properties) {
        $index["$($group.corpusId)|$($group.scenarioId)|$($metric.Name)"] = $metric.Value
    }
}
$lines = [Collections.Generic.List[string]]::new()
$lines.Add("# PdfViewer 性能对比（自身历史基线，非官方 A/B）")
$lines.Add('')
$lines.Add("- baseline: $baselineLabel ($($baseline.generatedUtc))")
$lines.Add("- current: $currentLabel ($($current.generatedUtc))")
$lines.Add("- 回归判定: current p95 > $RegressionFactor x baseline p95；门槛与判定均不构成官方对照。")
$lines.Add('')
$lines.Add('| corpus | scenario | metric | baseline p50 | baseline p95 | current p50 | current p95 | verdict |')
$lines.Add('|---|---|---|---:|---:|---:|---:|---|')
$regressions = 0
foreach ($group in $current.groups) {
    foreach ($metric in $group.metrics.PSObject.Properties) {
        $key = "$($group.corpusId)|$($group.scenarioId)|$($metric.Name)"
        if (-not $index.ContainsKey($key)) { continue }
        $base = $index[$key]
        $now = $metric.Value
        $baseP95 = [double]$base.p95
        $nowP95 = [double]$now.p95
        $verdict = if ($baseP95 -le 0) { 'n/a' } elseif ($nowP95 -gt ($baseP95 * $RegressionFactor)) { $regressions++; 'regression' } else { 'ok' }
        $lines.Add(('| {0} | {1} | {2} | {3:F2} | {4:F2} | {5:F2} | {6:F2} | {7} |' -f `
            $group.corpusId, $group.scenarioId, $metric.Name, [double]$base.p50, $baseP95, [double]$now.p50, $nowP95, $verdict))
    }
}
$lines.Add('')
$lines.Add("regressions: $regressions")
$markdown = $lines -join "`n"
if (-not [string]::IsNullOrWhiteSpace($OutputMarkdown)) {
    Set-Content -LiteralPath ([IO.Path]::GetFullPath($OutputMarkdown)) -Value $markdown -Encoding utf8
}
Write-Output $markdown
if ($regressions -gt 0) { exit 1 }
exit 0
