param([string]$BaselinePath, [string[]]$ReportPaths, [string]$OutputPath)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $BaselinePath) { $BaselinePath = Join-Path $repoRoot 'docs/validation/virtualkeyboard-performance-baseline.json' }
if (-not $ReportPaths) { $ReportPaths = @(1..3 | ForEach-Object { Join-Path $repoRoot "docs/validation/windows-keyboard-performance-$_.json" }) }
if (-not $OutputPath) { $OutputPath = Join-Path $repoRoot 'artifacts/platform/virtualkeyboard-performance-guard.json' }

function Get-RequiredField($Object, [string]$Name, [string]$Context, [switch]$AllowNull) {
    if ($null -eq $Object -or $Object -isnot [pscustomobject]) { throw "$Context must be a JSON object." }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or ($null -eq $property.Value -and -not $AllowNull)) { throw "$Context.$Name is required." }
    return ,$property.Value
}
function Get-RequiredObject($Object, [string]$Name, [string]$Context) {
    $value = Get-RequiredField $Object $Name $Context
    if ($value -isnot [pscustomobject]) { throw "$Context.$Name must be a JSON object." }
    return $value
}
function Get-RequiredArray($Object, [string]$Name, [string]$Context) {
    $value = Get-RequiredField $Object $Name $Context
    if ($value -isnot [array]) { throw "$Context.$Name must be a JSON array." }
    return ,$value
}
function Get-RequiredString($Object, [string]$Name, [string]$Context) {
    $value = Get-RequiredField $Object $Name $Context
    if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) { throw "$Context.$Name must be a nonempty JSON string." }
    return $value
}
function Get-RequiredBoolean($Object, [string]$Name, [string]$Context) {
    $value = Get-RequiredField $Object $Name $Context
    if ($value -isnot [bool]) { throw "$Context.$Name must be a JSON boolean." }
    return $value
}
function Assert-TrueField($Object, [string]$Name, [string]$Context) {
    if (-not (Get-RequiredBoolean $Object $Name $Context)) { throw "$Context.$Name must be true." }
}
function Assert-FiniteNumber($Value, [string]$Context, [switch]$Positive, [switch]$Integer) {
    $numericTypes = @('System.Byte', 'System.SByte', 'System.Int16', 'System.UInt16', 'System.Int32', 'System.UInt32',
        'System.Int64', 'System.UInt64', 'System.Single', 'System.Double', 'System.Decimal')
    if ($null -eq $Value -or $Value.GetType().FullName -notin $numericTypes) { throw "$Context must be a JSON number." }
    $number = [double]$Value
    if ([double]::IsNaN($number) -or [double]::IsInfinity($number) -or $number -lt 0 -or ($Positive -and $number -le 0)) {
        throw "$Context must be finite and $(if ($Positive) { 'positive' } else { 'nonnegative' })."
    }
    if ($Integer -and [Math]::Truncate($number) -ne $number) { throw "$Context must be an integer." }
    return $number
}
function Get-RequiredNumber($Object, [string]$Name, [string]$Context, [switch]$Positive, [switch]$Integer) {
    return Assert-FiniteNumber (Get-RequiredField $Object $Name $Context) "$Context.$Name" -Positive:$Positive -Integer:$Integer
}
function Assert-SameNumber([double]$Actual, [double]$Expected, [string]$Context) {
    if ([Math]::Abs($Actual - $Expected) -gt [Math]::Max(1e-8, [Math]::Abs($Expected) * 1e-9)) { throw "$Context does not match the raw observations." }
}
function Get-Statistics([double[]]$Values) {
    if ($Values.Count -eq 0) { throw 'Cannot summarize an empty observation array.' }
    $sorted = @($Values | Sort-Object)
    $percentiles = @(.5, .95) | ForEach-Object {
        $rank = ($sorted.Count - 1) * $_
        $lower = [int][Math]::Floor($rank)
        $upper = [int][Math]::Ceiling($rank)
        $sorted[$lower] + ($sorted[$upper] - $sorted[$lower]) * ($rank - $lower)
    }
    return [pscustomobject]@{ Count = $sorted.Count; P50 = $percentiles[0]; P95 = $percentiles[1]; Max = $sorted[-1] }
}
function Assert-Statistics($Object, [string]$Name, [string]$Context, [double[]]$Values) {
    $statistics = Get-RequiredObject $Object $Name $Context
    $expected = Get-Statistics $Values
    if ((Get-RequiredNumber $statistics 'Count' "$Context.$Name" -Integer) -ne $expected.Count) { throw "$Context.$Name.Count does not match the raw observations." }
    foreach ($metric in @('P50', 'P95', 'Max')) { Assert-SameNumber (Get-RequiredNumber $statistics $metric "$Context.$Name") $expected.$metric "$Context.$Name.$metric" }
    return $expected
}
function Assert-Fixture($Fixture, [string]$Context) {
    foreach ($name in @('OS', 'Architecture', 'Runtime', 'AvaloniaVersion', 'BuildConfiguration')) { $null = Get-RequiredString $Fixture $name $Context }
    foreach ($name in @('WindowWidthDip', 'WindowHeightDip', 'RenderScaling', 'NativeHitScenePollingIntervalMs', 'StopwatchFrequencyHz')) { $null = Get-RequiredNumber $Fixture $name $Context -Positive }
    $visual = Get-RequiredObject $Fixture 'VisualFixture' $Context
    foreach ($name in @('ActualThemeVariant', 'KeyFontFamily')) { $null = Get-RequiredString $visual $name "$Context.VisualFixture" }
    $null = Get-RequiredNumber $visual 'KeyFontSize' "$Context.VisualFixture" -Positive
    foreach ($name in @('KeyMinimumWidthDip', 'KeyMinimumHeightDip')) { $null = Get-RequiredNumber $visual $name "$Context.VisualFixture" }
}
function Assert-SameFixture($Report, $Fixture, [string]$Context) {
    Assert-Fixture $Report $Context
    foreach ($name in @('OS', 'Architecture', 'Runtime', 'AvaloniaVersion', 'BuildConfiguration', 'WindowWidthDip', 'WindowHeightDip', 'RenderScaling', 'NativeHitScenePollingIntervalMs', 'StopwatchFrequencyHz')) {
        if ($Report.$name -cne $Fixture.$name) { throw "$Context.$name differs from Baseline.Fixture." }
    }
    foreach ($name in @('ActualThemeVariant', 'KeyFontFamily', 'KeyFontSize', 'KeyMinimumWidthDip', 'KeyMinimumHeightDip')) {
        if ($Report.VisualFixture.$name -cne $Fixture.VisualFixture.$name) { throw "$Context.VisualFixture.$name differs from Baseline.Fixture." }
    }
}

$results = @()
$baselineValid = $false
$scope = $null
try {
    $baseline = Get-Content -LiteralPath $BaselinePath -Raw | ConvertFrom-Json
    Assert-TrueField $baseline 'Passed' 'Baseline'
    if ((Get-RequiredNumber $baseline 'ValidatedRounds' 'Baseline' -Integer) -lt 3) { throw 'Baseline.ValidatedRounds must be at least three.' }
    $fixture = Get-RequiredObject $baseline 'Fixture' 'Baseline'
    Assert-Fixture $fixture 'Baseline.Fixture'
    if ($fixture.OS -notlike '*Windows*' -or $fixture.Architecture -cne 'X64' -or $fixture.BuildConfiguration -cne 'Release') { throw 'Baseline.Fixture must describe the reviewed Windows/X64 Release fixture.' }
    $guard = Get-RequiredObject $baseline 'RegressionGuards' 'Baseline'
    $scope = Get-RequiredString $guard 'Scope' 'Baseline.RegressionGuards'
    foreach ($name in @('ReleaseRouteP95Ms', 'ReleaseRouteMaxMs', 'FirstNativeHitReadyMs', 'CancelCallMs', 'FirstRepeatLatenessMs', 'RepeatIntervalP95ExtraMs')) { $null = Get-RequiredNumber $guard $name 'Baseline.RegressionGuards' -Positive }
    $baselineValid = $true
}
catch { $results += [ordered]@{ Report = $null; Passed = $false; Checks = @{}; Error = $_.Exception.Message } }

if ($baselineValid) {
    $seenReports = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($reportPath in $ReportPaths) {
        $checks = [ordered]@{}
        $reportError = $null
        $fullReportPath = [IO.Path]::GetFullPath($reportPath)
        try {
            if (-not $seenReports.Add($fullReportPath)) { throw 'Each report path must be unique.' }
            $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
            Assert-TrueField $report 'Passed' 'Report'
            if ((Get-RequiredString $report 'Mode' 'Report') -cne 'keyboard-benchmark') { throw 'Report.Mode must be keyboard-benchmark.' }
            if ($null -ne (Get-RequiredField $report 'Error' 'Report' -AllowNull)) { throw 'Report.Error must be null.' }
            Assert-TrueField $report 'NativeEditingFocusValidated' 'Report'
            $semanticChecks = Get-RequiredObject $report 'Checks' 'Report'
            foreach ($name in @('nativeWindowCreated', 'nativeWindowActive', 'editorFocusEstablished', 'actualEditingFocus', 'defaultTemplateLoaded', 'firstKeyCoordinateHit', 'character-insertion:100ValidSamples', 'selection-replacement:100ValidSamples',
                'default-500-70:initialAndRepeatedActionsObserved', 'default-500-70:cancelReleasesCapture', 'default-500-70:noInputAfterCancelOrStaleRelease',
                'short-100-40:initialAndRepeatedActionsObserved', 'short-100-40:cancelReleasesCapture', 'short-100-40:noInputAfterCancelOrStaleRelease', 'focusPreservedThroughBenchmark')) { Assert-TrueField $semanticChecks $name 'Report.Checks' }
            foreach ($property in $semanticChecks.PSObject.Properties) { if ($property.Value -isnot [bool] -or -not $property.Value) { throw 'Report.Checks must contain only true JSON booleans.' } }
            $checks.semanticChecks = $true
            Assert-SameFixture $report $fixture 'Report'
            $checks.expectedFixture = $true
            $focus = Get-RequiredArray $report 'FocusObservations' 'Report'
            if ($focus.Count -ne 2) { throw 'Report.FocusObservations must contain initial and completed observations.' }
            for ($index = 0; $index -lt $focus.Count; $index++) {
                $context = "Report.FocusObservations[$index]"
                if ((Get-RequiredString $focus[$index] 'Stage' $context) -cne @('initial', 'completed')[$index]) { throw "$context.Stage is incorrect." }
                foreach ($name in @('WindowIsActive', 'EditorIsFocused', 'IsInputAvailable')) { Assert-TrueField $focus[$index] $name $context }
            }
            $first = Get-RequiredObject $report 'FirstObservations' 'Report'
            foreach ($name in @('focusAvailableFromWindowConstructionMs', 'keyboardLayoutObservedFromWindowConstructionMs', 'firstKeyPreparationMs', 'firstNativeHitSceneWaitMs')) { $null = Get-RequiredNumber $first $name 'Report.FirstObservations' }
            $checks.firstHitReady = (Get-RequiredNumber $first 'firstKeyCoordinateHitFromWindowConstructionMs' 'Report.FirstObservations') -le $guard.FirstNativeHitReadyMs
            $groups = Get-RequiredArray $report 'SampleGroups' 'Report'
            if ($groups.Count -ne 2) { throw 'Report.SampleGroups must contain the two required input scenarios.' }
            $seenScenarios = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            $timingNames = @('PreparationTotalMs', 'PreparationBeforeSceneWaitMs', 'SceneWaitMs', 'PreparedPointerRouteToTextPropertyMs', 'ReleaseRouteToTextPropertyMs')
            foreach ($group in $groups) {
                $name = Get-RequiredString $group 'Scenario' 'Report.SampleGroups entry'
                if ($name -cnotin @('character-insertion', 'selection-replacement') -or -not $seenScenarios.Add($name)) { throw 'Report.SampleGroups must contain character-insertion and selection-replacement exactly once.' }
                $context = "Report.SampleGroups.$name"
                if ((Get-RequiredNumber $group 'DiscardedWarmupCount' $context -Integer) -ne 20 -or (Get-RequiredNumber $group 'SampleCount' $context -Integer) -ne 100) { throw "$context requires 20 discarded warmups and 100 samples." }
                $samples = Get-RequiredArray $group 'Samples' $context
                if ($samples.Count -ne 100) { throw "$context.Samples must contain 100 entries." }
                $observations = @{}
                foreach ($metric in $timingNames) { $observations[$metric] = [Collections.Generic.List[double]]::new() }
                for ($index = 0; $index -lt $samples.Count; $index++) {
                    $sample = $samples[$index]
                    $sampleContext = "$context.Samples[$index]"
                    if ((Get-RequiredNumber $sample 'Iteration' $sampleContext -Integer) -ne $index) { throw "$sampleContext.Iteration must match its sample position." }
                    if ((Get-RequiredNumber $sample 'TextChanges' $sampleContext -Integer) -ne 1) { throw "$sampleContext.TextChanges must equal one." }
                    Assert-TrueField $sample 'ViewModelMatched' $sampleContext
                    foreach ($metric in $timingNames) { $observations[$metric].Add((Get-RequiredNumber $sample $metric $sampleContext)) }
                    Assert-SameNumber $sample.PreparationTotalMs ($sample.PreparationBeforeSceneWaitMs + $sample.SceneWaitMs) "$sampleContext.PreparationTotalMs"
                    if ($sample.PreparedPointerRouteToTextPropertyMs -lt $sample.ReleaseRouteToTextPropertyMs) { throw "$sampleContext has inconsistent pointer-route timings." }
                }
                $summaries = @{}
                foreach ($metric in $timingNames) { $summaries[$metric] = Assert-Statistics $group $metric $context $observations[$metric].ToArray() }
                $checks["${name}:samples"] = $true
                $checks["${name}:releaseP95"] = $summaries.ReleaseRouteToTextPropertyMs.P95 -le $guard.ReleaseRouteP95Ms
                $checks["${name}:releaseMax"] = $summaries.ReleaseRouteToTextPropertyMs.Max -le $guard.ReleaseRouteMaxMs
            }
            $profiles = Get-RequiredArray $report 'RepeatProfiles' 'Report'
            if ($profiles.Count -ne 2) { throw 'Report.RepeatProfiles must contain the two required repeat profiles.' }
            $seenProfiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach ($profile in $profiles) {
                $name = Get-RequiredString $profile 'Name' 'Report.RepeatProfiles entry'
                if ($name -cnotin @('default-500-70', 'short-100-40') -or -not $seenProfiles.Add($name)) { throw 'Report.RepeatProfiles must contain default-500-70 and short-100-40 exactly once.' }
                $context = "Report.RepeatProfiles.$name"
                $delay = Get-RequiredNumber $profile 'ConfiguredDelayMs' $context -Integer
                $interval = Get-RequiredNumber $profile 'ConfiguredIntervalMs' $context -Integer
                if (($name -ceq 'default-500-70' -and ($delay -ne 500 -or $interval -ne 70)) -or ($name -ceq 'short-100-40' -and ($delay -ne 100 -or $interval -ne 40))) { throw "$context has incorrect configured delay or interval." }
                foreach ($metric in @('PreparationTotalMs', 'SceneWaitMs', 'HoldObservationWindowMs', 'PostCancelObservationWindowMs')) { $null = Get-RequiredNumber $profile $metric $context }
                $times = Get-RequiredArray $profile 'TextChangeTimesFromPreparedPressMs' $context
                $intervals = Get-RequiredArray $profile 'SubsequentIntervalsMs' $context
                if ($intervals.Count -lt 20 -or $times.Count -ne $intervals.Count + 2) { throw "$context requires at least 20 intervals and matching event timestamps." }
                for ($index = 0; $index -lt $times.Count; $index++) {
                    $null = Assert-FiniteNumber $times[$index] "$context.TextChangeTimesFromPreparedPressMs[$index]"
                    if ($index -gt 0 -and $times[$index] -le $times[$index - 1]) { throw "$context event timestamps must increase strictly." }
                }
                if ($profile.SceneWaitMs -gt $profile.PreparationTotalMs -or $profile.HoldObservationWindowMs -lt $times[-1]) {
                    throw "$context observation windows are inconsistent with their recorded events."
                }
                # Two integer-millisecond Task.Delay awaits can finish within their
                # timer-clock quantization. This 2 ms data tolerance is not a latency guard.
                if ($profile.PostCancelObservationWindowMs + 2 -lt $delay + 6 * $interval) {
                    throw "$context.PostCancelObservationWindowMs does not cover the required cancellation observation period."
                }
                Assert-SameNumber (Get-RequiredNumber $profile 'InitialActionFromPreparedPressMs' $context) $times[0] "$context.InitialActionFromPreparedPressMs"
                Assert-SameNumber (Get-RequiredNumber $profile 'FirstRepeatFromPreparedPressMs' $context) $times[1] "$context.FirstRepeatFromPreparedPressMs"
                $lateness = Get-RequiredNumber $profile 'FirstRepeatLatenessMs' $context
                Assert-SameNumber $lateness ($times[1] - $times[0] - $delay) "$context.FirstRepeatLatenessMs"
                for ($index = 0; $index -lt $intervals.Count; $index++) {
                    $value = Assert-FiniteNumber $intervals[$index] "$context.SubsequentIntervalsMs[$index]" -Positive
                    Assert-SameNumber $value ($times[$index + 2] - $times[$index + 1]) "$context.SubsequentIntervalsMs[$index]"
                }
                $statistics = Assert-Statistics $profile 'SubsequentIntervalStatisticsMs' $context $intervals
                Assert-TrueField $profile 'CaptureReleasedByCancel' $context
                Assert-TrueField $profile 'TextAndViewModelStableAfterCancel' $context
                if ((Get-RequiredNumber $profile 'TextChangesAfterCancel' $context -Integer) -ne 0) { throw "$context.TextChangesAfterCancel must equal zero." }
                $checks["${name}:cancelDuration"] = (Get-RequiredNumber $profile 'CancelCallMs' $context) -le $guard.CancelCallMs
                $checks["${name}:cancelStopsInput"] = $true
                $checks["${name}:firstRepeatLateness"] = $lateness -le $guard.FirstRepeatLatenessMs
                $checks["${name}:repeatSampleCount"] = $true
                $checks["${name}:intervalP95"] = $statistics.P95 -le $interval + $guard.RepeatIntervalP95ExtraMs
            }
        }
        catch { $reportError = $_.Exception.Message; $checks.validData = $false }
        $passedReport = $null -eq $reportError -and $checks.Count -gt 0 -and @($checks.Values | Where-Object { -not $_ }).Count -eq 0
        $results += [ordered]@{ Report = $fullReportPath; Passed = $passedReport; Checks = $checks; Error = $reportError }
    }
}
$passed = $baselineValid -and $results.Count -gt 0 -and @($results | Where-Object { -not $_.Passed }).Count -eq 0
$fullOutput = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path $fullOutput -Parent) -Force | Out-Null
[ordered]@{
    Passed = $passed; TimestampUtc = [DateTimeOffset]::UtcNow.ToString('O'); Baseline = [IO.Path]::GetFullPath($BaselinePath)
    BaselineValid = $baselineValid; Scope = $scope; Results = $results; PhysicalInputLatencyValidated = $false
    Notes = 'Same-fixture regression alarms recomputed from validated raw observations. Does not certify device delivery, rendered-frame latency, other platforms or load conditions.'
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $fullOutput -Encoding utf8
if (-not $passed) { throw "VirtualKeyboard performance guard FAIL: invalid data, fixture mismatch or regression; see $fullOutput" }
Write-Output ("PASS: VirtualKeyboard local performance guard, {0} validated native reports." -f $results.Count)
