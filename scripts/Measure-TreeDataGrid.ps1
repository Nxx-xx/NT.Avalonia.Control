param(
    [string]$Output = "artifacts/benchmarks/p6-profile.json",
    [ValidateRange(5, 100)][int]$Samples = 5,
    [string]$CompareTo
)
$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$resultPath = if ([System.IO.Path]::IsPathRooted($Output)) { [System.IO.Path]::GetFullPath($Output) } else { Join-Path $repositoryRoot $Output }
$projectPath = Join-Path $repositoryRoot "benchmarks/TreeDataGrid.Benchmarks/TreeDataGrid.Benchmarks.csproj"
$profileArguments = @("run", "--project", $projectPath, "--configuration", "Release", "--no-build", "--", "--p6", "--samples", "$Samples", "--output", $resultPath)
& dotnet @profileArguments
if ($LASTEXITCODE -ne 0) { throw "TreeDataGrid P6 profile exited with code $LASTEXITCODE." }
if (!(Test-Path -LiteralPath $resultPath)) { throw "TreeDataGrid P6 profile did not produce its JSON result." }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if ($result.Passed -ne $true -or $result.SampleCount -lt 5 -or $result.Environment.Configuration -ne "Release") {
    throw "TreeDataGrid P6 profile failed, lacks five repetitions, or is not a Release build."
}
foreach ($scenario in $result.Scenarios) {
    if ($scenario.Samples.Count -ne $result.SampleCount -or @($scenario.Samples | Where-Object { $_.Passed -ne $true }).Count -ne 0) {
        throw "Incomplete or failed samples for $($scenario.Scenario)."
    }
}
if ($CompareTo) {
    $baselinePath = if ([System.IO.Path]::IsPathRooted($CompareTo)) { $CompareTo } else { Join-Path $repositoryRoot $CompareTo }
    $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    if ($baseline.Passed -ne $true -or $baseline.SampleCount -lt 5) { throw "Comparison requires a successful baseline with five or more samples." }
    foreach ($name in @("MachineName", "Runtime", "OperatingSystem", "Architecture", "Configuration", "Seed", "RenderBackend", "Font")) {
        if ($baseline.Environment.$name -ne $result.Environment.$name) { throw "Cannot compare different environment field '$name'." }
    }
    if ($baseline.Environment.Viewport.Width -ne $result.Environment.Viewport.Width -or $baseline.Environment.Viewport.Height -ne $result.Environment.Viewport.Height) {
        throw "Cannot compare different viewport dimensions."
    }
    $comparisons = foreach ($scenario in $result.Scenarios) {
        $previous = @($baseline.Scenarios | Where-Object { $_.Scenario -eq $scenario.Scenario })
        if ($previous.Count -eq 0) {
            [pscustomobject]@{
                Scenario = $scenario.Scenario
                NewScenario = $true
                BaselineMedianMilliseconds = $null
                CurrentMedianMilliseconds = [double]$scenario.ElapsedMilliseconds.Median
                MedianDeltaMilliseconds = $null
                MedianChangePercent = $null
                AllocationChangePercent = $null
                InvestigationThresholdExceeded = $null
            }
            continue
        }
        if ($previous.Count -ne 1) { throw "Baseline duplicates the exact scenario '$($scenario.Scenario)'." }
        $oldTime = [double]$previous[0].ElapsedMilliseconds.Median
        $newTime = [double]$scenario.ElapsedMilliseconds.Median
        $oldAllocation = [double]$previous[0].AllocatedBytes.Median
        $delta = $newTime - $oldTime
        [pscustomobject]@{
            Scenario = $scenario.Scenario
            NewScenario = $false
            BaselineMedianMilliseconds = $oldTime
            CurrentMedianMilliseconds = $newTime
            MedianDeltaMilliseconds = $delta
            MedianChangePercent = if ($oldTime -gt 0) { 100 * $delta / $oldTime } else { $null }
            AllocationChangePercent = if ($oldAllocation -gt 0) { 100 * ([double]$scenario.AllocatedBytes.Median - $oldAllocation) / $oldAllocation } else { $null }
            InvestigationThresholdExceeded = ($delta -gt 5 -and $newTime -gt $oldTime * 1.2)
        }
    }
    $comparisonPath = [System.IO.Path]::ChangeExtension($resultPath, "comparison.json")
    [pscustomobject]@{ Baseline = $baselinePath; Current = $resultPath; Meaning = "Investigation threshold, not a product performance pass line"; Scenarios = @($comparisons) } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $comparisonPath -Encoding utf8
    Write-Host "Comparison written to $comparisonPath"
}
Write-Host "P6 profile passed: $resultPath ($($result.SampleCount) samples per scenario)."
