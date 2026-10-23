param([string]$SourceReportPath, [string]$SourceBaselinePath)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $SourceReportPath) { $SourceReportPath = Join-Path $repoRoot 'docs/validation/windows-keyboard-performance-1.json' }
if (-not $SourceBaselinePath) { $SourceBaselinePath = Join-Path $repoRoot 'docs/validation/virtualkeyboard-performance-baseline.json' }
$guardScript = Join-Path $repoRoot 'scripts/Test-VirtualKeyboardPerformance.ps1'
$caseRoot = Join-Path $repoRoot ('artifacts/guard-tests/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $caseRoot -Force | Out-Null
$sourceReport = Get-Content -LiteralPath $SourceReportPath -Raw
$sourceBaseline = Get-Content -LiteralPath $SourceBaselinePath -Raw
$shellPath = (Get-Process -Id $PID).Path

function Invoke-RealGuard([string]$Baseline, [string]$Report, [string]$Output) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $shellPath
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-File', $guardScript, '-BaselinePath', $Baseline, '-ReportPaths', $Report, '-OutputPath', $Output)) {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $standardOutput = $process.StandardOutput.ReadToEndAsync()
        $standardError = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Performance guard test child process timed out.' }
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Stdout = $standardOutput.GetAwaiter().GetResult()
            Stderr = $standardError.GetAwaiter().GetResult()
        }
    }
    finally { $process.Dispose() }
}

# Each case invokes the real script in a separate process with isolated JSON files.
# Mutations never touch the reviewed reports/baseline and never include editor text.
$cases = @(
    @{ Name = 'valid'; Passed = $true; Mutate = { param($report, $baseline) } },
    @{ Name = 'passed-string-false'; Error = 'JSON boolean'; Mutate = { param($report, $baseline) $report.Passed = 'false' } },
    @{ Name = 'passed-false'; Error = 'Passed must be true'; Mutate = { param($report, $baseline) $report.Passed = $false } },
    @{ Name = 'missing-first-hit'; Error = 'firstKeyCoordinateHitFromWindowConstructionMs'; Mutate = { param($report, $baseline) $report.FirstObservations.PSObject.Properties.Remove('firstKeyCoordinateHitFromWindowConstructionMs') } },
    @{ Name = 'missing-raw-timing'; Error = 'ReleaseRouteToTextPropertyMs'; Mutate = { param($report, $baseline) $report.SampleGroups[0].Samples[0].PSObject.Properties.Remove('ReleaseRouteToTextPropertyMs') } },
    @{ Name = 'negative-raw-timing'; Error = 'nonnegative'; Mutate = { param($report, $baseline) $report.SampleGroups[0].Samples[0].ReleaseRouteToTextPropertyMs = -1 } },
    @{ Name = 'numeric-string'; Error = 'JSON number'; Mutate = { param($report, $baseline) $report.SampleGroups[0].Samples[0].ReleaseRouteToTextPropertyMs = '0.1' } },
    @{ Name = 'nonfinite-cancel'; Error = 'finite'; Mutate = { param($report, $baseline) }; RawTransform = { param($json) [regex]::Replace($json, '"CancelCallMs":\s*[0-9.eE+\-]+', '"CancelCallMs": 1e999') } },
    @{ Name = 'invalid-text-change-count'; Error = 'TextChanges'; Mutate = { param($report, $baseline) $report.SampleGroups[0].Samples[0].TextChanges = 2 } },
    @{ Name = 'view-model-false'; Error = 'ViewModelMatched'; Mutate = { param($report, $baseline) $report.SampleGroups[0].Samples[0].ViewModelMatched = $false } },
    @{ Name = 'view-model-string-false'; Error = 'JSON boolean'; Mutate = { param($report, $baseline) $report.SampleGroups[0].Samples[0].ViewModelMatched = 'false' } },
    @{ Name = 'forged-summary'; Error = 'raw observations'; Mutate = { param($report, $baseline) $report.SampleGroups[0].ReleaseRouteToTextPropertyMs.P95 = 0 } },
    @{ Name = 'incorrect-summary-count'; Error = 'Count'; Mutate = { param($report, $baseline) $report.SampleGroups[0].ReleaseRouteToTextPropertyMs.Count = 1 } },
    @{ Name = 'raw-regression-with-consistent-summary'; ThresholdFailure = $true; Mutate = {
        param($report, $baseline)
        foreach ($sample in $report.SampleGroups[0].Samples) { $sample.ReleaseRouteToTextPropertyMs = 100; $sample.PreparedPointerRouteToTextPropertyMs = 101 }
        $report.SampleGroups[0].ReleaseRouteToTextPropertyMs = [pscustomobject]@{ Count = 100; P50 = 100; P95 = 100; Max = 100 }
        $report.SampleGroups[0].PreparedPointerRouteToTextPropertyMs = [pscustomobject]@{ Count = 100; P50 = 101; P95 = 101; Max = 101 }
    } },
    @{ Name = 'duplicate-scenario'; Error = 'exactly once'; Mutate = { param($report, $baseline) $report.SampleGroups[1].Scenario = 'character-insertion' } },
    @{ Name = 'unknown-scenario'; Error = 'exactly once'; Mutate = { param($report, $baseline) $report.SampleGroups[0].Scenario = 'other' } },
    @{ Name = 'missing-scenario'; Error = 'two required'; Mutate = { param($report, $baseline) $report.SampleGroups = @($report.SampleGroups[0]) } },
    @{ Name = 'duplicate-profile'; Error = 'exactly once'; Mutate = { param($report, $baseline) $report.RepeatProfiles[1].Name = 'default-500-70' } },
    @{ Name = 'unknown-profile'; Error = 'exactly once'; Mutate = { param($report, $baseline) $report.RepeatProfiles[0].Name = 'other' } },
    @{ Name = 'wrong-default-delay'; Error = 'configured delay'; Mutate = { param($report, $baseline) $report.RepeatProfiles[0].ConfiguredDelayMs = 100 } },
    @{ Name = 'wrong-short-interval'; Error = 'configured delay'; Mutate = { param($report, $baseline) $report.RepeatProfiles[1].ConfiguredIntervalMs = 70 } },
    @{ Name = 'inconsistent-repeat-interval'; Error = 'raw observations'; Mutate = { param($report, $baseline) $report.RepeatProfiles[0].SubsequentIntervalsMs[0] = 1 } },
    @{ Name = 'invalid-repeat-summary'; Error = 'raw observations'; Mutate = { param($report, $baseline) $report.RepeatProfiles[0].SubsequentIntervalStatisticsMs.P95 = 0 } },
    @{ Name = 'changed-os-build'; Error = 'Report.OS differs'; Mutate = { param($report, $baseline) $report.OS = 'Microsoft Windows 10.0.1' } },
    @{ Name = 'changed-runtime'; Error = 'Report.Runtime differs'; Mutate = { param($report, $baseline) $report.Runtime = '1.0.0' } },
    @{ Name = 'changed-font'; Error = 'KeyFontFamily differs'; Mutate = { param($report, $baseline) $report.VisualFixture.KeyFontFamily = 'Different fixture font' } },
    @{ Name = 'changed-theme'; Error = 'ActualThemeVariant differs'; Mutate = { param($report, $baseline) $report.VisualFixture.ActualThemeVariant = if ($report.VisualFixture.ActualThemeVariant -eq 'Dark') { 'Light' } else { 'Dark' } } },
    @{ Name = 'changed-font-size'; Error = 'KeyFontSize differs'; Mutate = { param($report, $baseline) $report.VisualFixture.KeyFontSize += 1 } },
    @{ Name = 'changed-key-size'; Error = 'KeyMinimumWidthDip differs'; Mutate = { param($report, $baseline) $report.VisualFixture.KeyMinimumWidthDip += 1 } },
    @{ Name = 'changed-scaling'; Error = 'RenderScaling differs'; Mutate = { param($report, $baseline) $report.RenderScaling = 1.5 } },
    @{ Name = 'old-baseline-without-fixture'; Error = 'Baseline.Fixture'; Mutate = { param($report, $baseline) $baseline.PSObject.Properties.Remove('Fixture') } },
    @{ Name = 'report-without-visual-fixture'; Error = 'VisualFixture'; Mutate = { param($report, $baseline) $report.PSObject.Properties.Remove('VisualFixture') } },
    @{ Name = 'negative-baseline-threshold'; Error = 'positive'; Mutate = { param($report, $baseline) $baseline.RegressionGuards.ReleaseRouteP95Ms = -1 } },
    @{ Name = 'missing-semantic-check'; Error = 'nativeWindowCreated'; Mutate = { param($report, $baseline) $report.Checks.PSObject.Properties.Remove('nativeWindowCreated') } },
    @{ Name = 'false-native-focus'; Error = 'NativeEditingFocusValidated'; Mutate = { param($report, $baseline) $report.NativeEditingFocusValidated = $false } },
    @{ Name = 'wrong-mode'; Error = 'Mode'; Mutate = { param($report, $baseline) $report.Mode = 'keyboard-smoke' } }
    @{ Name = 'incomplete-hold-window'; Error = 'observation windows'; Mutate = { param($report, $baseline) $report.RepeatProfiles[0].HoldObservationWindowMs = 0 } },
    @{ Name = 'incomplete-cancel-window'; Error = 'cancellation observation period'; Mutate = { param($report, $baseline) $report.RepeatProfiles[0].PostCancelObservationWindowMs = 0 } },
    @{ Name = 'focus-observation-string-false'; Error = 'JSON boolean'; Mutate = { param($report, $baseline) $report.FocusObservations[1].EditorIsFocused = 'false' } }
    @{ Name = 'cancel-window-quantization-boundary'; Passed = $true; Mutate = { param($report, $baseline) $report.RepeatProfiles[0].PostCancelObservationWindowMs = 918 } },
    @{ Name = 'cancel-window-below-quantization-boundary'; Error = 'cancellation observation period'; Mutate = { param($report, $baseline) $report.RepeatProfiles[0].PostCancelObservationWindowMs = 917.9 } }
)
$results = @()
foreach ($case in $cases) {
    $report = $sourceReport | ConvertFrom-Json
    $baseline = $sourceBaseline | ConvertFrom-Json
    & $case.Mutate $report $baseline
    $reportJson = $report | ConvertTo-Json -Depth 30
    if ($case.RawTransform) { $reportJson = & $case.RawTransform $reportJson }
    $baselinePath = Join-Path $caseRoot ($case.Name + '.baseline.json')
    $reportPath = Join-Path $caseRoot ($case.Name + '.report.json')
    $resultPath = Join-Path $caseRoot ($case.Name + '.result.json')
    $baseline | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $baselinePath -Encoding utf8
    $reportJson | Set-Content -LiteralPath $reportPath -Encoding utf8
    $processResult = Invoke-RealGuard $baselinePath $reportPath $resultPath
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "Case $($case.Name) failed to write a structured result." }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    $expectedPass = $case.Passed -eq $true
    if ($result.Passed -isnot [bool] -or $result.Passed -ne $expectedPass -or (($processResult.ExitCode -eq 0) -ne $expectedPass)) {
        throw "Case $($case.Name) returned the wrong pass state or exit code; see $resultPath"
    }
    if ($expectedPass -and $processResult.Stdout -notlike '*PASS:*') { throw "Case $($case.Name) did not announce PASS." }
    if (-not $expectedPass) {
        if ($processResult.Stderr -notlike '*FAIL:*') { throw "Case $($case.Name) did not announce FAIL." }
        if ($case.ThresholdFailure) {
            if ($null -ne $result.Results[0].Error -or $result.Results[0].Checks.'character-insertion:releaseP95' -ne $false) {
                throw "Case $($case.Name) did not recompute and reject its raw latency regression."
            }
        }
        elseif ($result.Results[0].Error -notlike ('*' + $case.Error + '*')) { throw "Case $($case.Name) did not identify its invalid field; see $resultPath" }
    }
    $results += [ordered]@{ Name = $case.Name; Passed = $true; ExpectedGuardPass = $expectedPass; GuardExitCode = $processResult.ExitCode }
}
[ordered]@{ Passed = $true; CaseCount = $results.Count; Cases = $results } | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $caseRoot 'test-results.json') -Encoding utf8
Write-Output ("PASS: VirtualKeyboard performance guard tests, {0} cases. Evidence: {1}" -f $results.Count, $caseRoot)
