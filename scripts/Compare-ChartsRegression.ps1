param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string[]]$BeforeReports,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string[]]$AfterReports,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
$comparisonOutput = [IO.Path]::GetFullPath($OutputPath)
$comparisonEvidence = $comparisonOutput + '.evidence'
if (Test-Path -LiteralPath $comparisonOutput) { throw 'Comparison output already exists; choose a new output path.' }
if (Test-Path -LiteralPath $comparisonEvidence) { throw 'Comparison evidence directory already exists; choose a new output path.' }
$null = [IO.Directory]::CreateDirectory($comparisonEvidence)
$comparisonGuard = Join-Path $PSScriptRoot 'Test-ChartsPerformance.ps1'
$comparisonPowerShell = (Get-Process -Id $PID).Path
$comparisonErrors = [Collections.Generic.List[string]]::new()
$comparisonReceipts = [Collections.Generic.List[object]]::new()
$comparisonRows = [Collections.Generic.List[object]]::new()
$comparisonSeenPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$comparisonSeenHashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$comparisonSeenRunIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$comparisonStages = @('harnessSourceSnapshot','sourceApply','scenePrepare','cachedSceneGetter','renderRecord','firstUsableToRecord')

function Read-ComparisonProperty($Value, [string]$Name) {
    if ($null -eq $Value -or $null -eq $Value.PSObject.Properties[$Name]) { throw "Required comparison field '$Name' is missing." }
    return ,$Value.PSObject.Properties[$Name].Value
}
function Comparison-Json($Value) { ConvertTo-Json -InputObject $Value -Depth 60 -Compress }
function Comparison-Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Comparison-TextHash([string]$Text) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text))) }
function Comparison-Percentile([object[]]$Values, [double]$Fraction) {
    if ($Values.Count -eq 0) { throw 'Cannot compute a percentile from empty input.' }
    $ordered = @($Values | Sort-Object)
    return $ordered[[Math]::Max(0, [int][Math]::Ceiling($ordered.Count * $Fraction) - 1)]
}
function Comparison-Ratio($After, $Before) {
    if ([double]$Before -eq 0) { return $null }
    return [double]$After / [double]$Before
}

function Invoke-ComparisonGuard([string]$Path, [string]$Prefix) {
    $guardOutput = Join-Path $comparisonEvidence ($Prefix + '.guard.json')
    $stdoutPath = Join-Path $comparisonEvidence ($Prefix + '.guard.stdout.txt')
    $stderrPath = Join-Path $comparisonEvidence ($Prefix + '.guard.stderr.txt')
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $comparisonPowerShell
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile','-File',$comparisonGuard,'-ReportPath',$Path,'-OutputPath',$guardOutput)) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw 'Unable to start independent raw guard.' }
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(60000)) { $process.Kill($true); $process.WaitForExit(); throw 'Raw guard exceeded 60 seconds.' }
        $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath $stdoutPath -Encoding utf8
        $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath $stderrPath -Encoding utf8
        if (-not (Test-Path -LiteralPath $guardOutput)) { throw 'Raw guard did not produce evidence.' }
        $guard = Get-Content -LiteralPath $guardOutput -Raw | ConvertFrom-Json
        return [pscustomobject]@{ passed=($process.ExitCode -eq 0 -and $guard.passed -eq $true); exitCode=$process.ExitCode; path=$guardOutput; sha256=(Comparison-Hash $guardOutput); reportSha256=$guard.reportSha256; errors=@($guard.errors) }
    } finally { $process.Dispose() }
}

function Read-ComparisonReport([string]$Path, [string]$Side, [int]$Ordinal) {
    $fullPath = (Resolve-Path -LiteralPath $Path).Path
    $reportHash = Comparison-Hash $fullPath
    if (-not $comparisonSeenPaths.Add($fullPath) -or -not $comparisonSeenHashes.Add($reportHash)) { throw 'Duplicate report path or content; repeated reports do not create independent runs.' }
    $guard = Invoke-ComparisonGuard $fullPath ('{0}-{1:D3}' -f $Side,$Ordinal)
    $raw = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json
    if ($guard.reportSha256 -ne $reportHash -or (Comparison-Hash $fullPath) -ne $reportHash) { throw 'Raw report changed during validation.' }
    if (-not $guard.passed) { throw "Independent raw guard failed: $($guard.errors -join '; ')" }
    if (-not $comparisonSeenRunIds.Add([string]$raw.runId)) { throw 'Duplicate runId; refusing to count it as another process run.' }
    if ($raw.performanceQualification -ne 'pending-C4' -or $raw.presentStatus -ne 'blocked-present' -or $raw.referenceStatus -ne 'blocked-reference') { throw 'Unsupported qualification scope.' }
    $capturePath = Join-Path (Split-Path $fullPath -Parent) 'capture.json'
    if ($raw.phase -match 'allocation-diagnostic|eventpipe|trace-') { throw 'A known trace diagnostic phase cannot be pooled with untraced measurements.' }
    if (Test-Path -LiteralPath $capturePath) {
        $capture = Get-Content -LiteralPath $capturePath -Raw | ConvertFrom-Json
        if ($capture.diagnosticOnly -eq $true -or $null -ne $capture.profiles) { throw 'Adjacent capture manifest identifies a traced diagnostic run.' }
    }
    $assemblyHashes = [ordered]@{}
    foreach ($name in @('harness','charts','themes')) {
        $source = Read-ComparisonProperty $raw.sources $name
        if ($source.sha256 -notmatch '^[A-Fa-f0-9]{64}$') { throw "Invalid reported $name assembly SHA." }
        $assemblyHashes[$name] = [ordered]@{ path=$source.path; sha256=$source.sha256.ToUpperInvariant() }
    }
    $supervisorEvidence = $null
    $supervisorPath = Join-Path (Split-Path $fullPath -Parent) 'supervisor.json'
    if (Test-Path -LiteralPath $supervisorPath) {
        $supervisor = Get-Content -LiteralPath $supervisorPath -Raw | ConvertFrom-Json
        if ($supervisor.runId -ne $raw.runId -or $supervisor.passed -ne $true -or $supervisor.processExitCode -ne 0 -or $supervisor.timedOut -ne $false) { throw 'Adjacent supervisor does not confirm this successful run.' }
        $supervisorEvidence = [ordered]@{ path=$supervisorPath; sha256=(Comparison-Hash $supervisorPath); runId=$supervisor.runId; processExitCode=$supervisor.processExitCode }
    }
    $receipt = [ordered]@{ side=$Side; ordinal=$Ordinal; reportPath=$fullPath; reportSha256=$reportHash; runId=$raw.runId; phase=$raw.phase; startedUtc=$raw.startedUtc; finishedUtc=$raw.finishedUtc; guard=$guard; reportedAssemblyHashes=$assemblyHashes; supervisor=$supervisorEvidence }
    $comparisonReceipts.Add($receipt)
    return [pscustomobject]@{ Raw=$raw; Receipt=$receipt; Side=$Side; Path=$fullPath; Hash=$reportHash }
}

function Comparison-GlobalQuality($Raw) {
    $configuration = [ordered]@{}
    foreach ($name in @('widthDip','heightDip','dpi','fontSizeDip','strokeThicknessDip','quality','warmups','hitQueries','budgetVersion','targetStatus')) {
        $configuration[$name] = Read-ComparisonProperty $Raw.configuration $name
    }
    $environment = [ordered]@{}
    foreach ($name in @('executionOs','processArchitecture','framework','avaloniaVersion','processorCount','gcServer','backend','rendering','privateMeasurementKind','fontQualification')) {
        $environment[$name] = Read-ComparisonProperty $Raw.environment $name
    }
    $targets = Read-ComparisonProperty $Raw 'frozenTargets'
    $targetHash = Read-ComparisonProperty $targets 'sha256'
    if ($targetHash -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Invalid frozen target manifest SHA.' }
    return [ordered]@{ configuration=$configuration; environment=$environment; harnessSha256=$Raw.sources.harness.sha256.ToUpperInvariant(); budgetSha256=$targetHash.ToUpperInvariant(); scopes=@($Raw.performanceQualification,$Raw.presentStatus,$Raw.referenceStatus); boundaries=@($Raw.boundaries) }
}

function Comparison-ScenarioQuality($Scenario) {
    $datasets = [Collections.Generic.List[object]]::new()
    foreach ($dataset in @($Scenario.datasets | Sort-Object revision)) {
        $manifest = [ordered]@{}
        foreach ($name in @('id','revision','generatorVersion','distribution','seedHex','sha256','inputPointCount','validPointCount','seriesPointCounts','seriesSha256','monotonicX','containsGaps','realtimePoc','markerSize','downsampling','pendingScope')) {
            $manifest[$name] = Read-ComparisonProperty $dataset $name
        }
        $manifest.seriesCount = @($dataset.seriesPointCounts).Count
        $sampleQuality = $null
        foreach ($sample in @($Scenario.samples | Where-Object datasetRevision -EQ $dataset.revision)) {
            $queries = @($sample.queries | ForEach-Object { [ordered]@{
                queryIndex=$_.queryIndex; screenX=$_.screenX; screenY=$_.screenY; probeSeriesIndex=$_.probeSeriesIndex; probeRawIndex=$_.probeRawIndex
                probeXBits=$_.probeXBits; probeYBits=$_.probeYBits; oracleMode=$_.oracleMode; expectedSeriesIndex=$_.expectedSeriesIndex; expectedRawIndex=$_.expectedRawIndex
                hitSeriesIndex=$_.hitSeriesIndex; hitRawIndex=$_.hitRawIndex; actualXBits=$_.actualXBits; actualYBits=$_.actualYBits
            } })
            $quality = [ordered]@{ datasetSha256=$sample.datasetSha256; inputPointCount=$sample.inputPointCount; primitiveCount=$sample.primitiveCount; geometryFinite=$sample.geometryFinite; plotArea=@($sample.plotArea); visibleRange=@($sample.visibleRange); queries=$queries }
            $encoded = Comparison-Json $quality
            if ($null -ne $sampleQuality -and $sampleQuality -cne $encoded) { throw "Scenario $($Scenario.id), dataset revision $($dataset.revision) changes quality within one run." }
            $sampleQuality = $encoded
        }
        if ($null -eq $sampleQuality) { throw 'Dataset revision has no quality-bearing sample.' }
        $datasets.Add([ordered]@{ manifest=$manifest; sampleQuality=($sampleQuality | ConvertFrom-Json) })
    }
    return [ordered]@{ id=$Scenario.id; qualification=$Scenario.qualification; datasets=$datasets.ToArray() }
}

function Comparison-Metric([object[]]$Timings) {
    $times = @($Timings | ForEach-Object { [double]$_.elapsedMilliseconds })
    $allocations = @($Timings | ForEach-Object { [long]$_.allocatedBytes })
    return [ordered]@{
        count=$Timings.Count
        p50Milliseconds=(Comparison-Percentile $times .5); p95Milliseconds=(Comparison-Percentile $times .95)
        p95AllocatedBytes=(Comparison-Percentile $allocations .95)
        maximumMilliseconds=($times | Measure-Object -Maximum).Maximum
        maximumAllocatedBytes=($allocations | Measure-Object -Maximum).Maximum
    }
}

function Comparison-Side([object[]]$Reports, [string]$ScenarioId) {
    $samples = [Collections.Generic.List[object]]::new()
    $allMemory = [Collections.Generic.List[long]]::new()
    $measuredMemory = [Collections.Generic.List[long]]::new()
    $processSummaries = [Collections.Generic.List[object]]::new()
    foreach ($report in $Reports) {
        $scenario = @($report.Raw.scenarios | Where-Object id -CEQ $ScenarioId)[0]
        $processPrivate = [Collections.Generic.List[long]]::new()
        $measuredCount = 0
        foreach ($sample in $scenario.samples) {
            $allMemory.Add([long]$sample.memoryBefore.privateBytes); $allMemory.Add([long]$sample.memoryAfter.privateBytes)
            $processPrivate.Add([long]$sample.memoryBefore.privateBytes); $processPrivate.Add([long]$sample.memoryAfter.privateBytes)
            if (-not $sample.isWarmup) {
                $samples.Add($sample); $measuredCount++
                $measuredMemory.Add([long]$sample.memoryBefore.privateBytes); $measuredMemory.Add([long]$sample.memoryAfter.privateBytes)
            }
        }
        $processSummaries.Add([ordered]@{ runId=$report.Raw.runId; reportSha256=$report.Hash; measuredSamples=$measuredCount; allRecordedSamples=@($scenario.samples).Count; maxObservedCurrentPrivateBytes=($processPrivate | Measure-Object -Maximum).Maximum })
    }
    $stages = [ordered]@{}
    foreach ($name in $comparisonStages) { $stages[$name] = Comparison-Metric @($samples | ForEach-Object { $_.$name }) }
    $queries = @($samples | ForEach-Object { $_.queries } | ForEach-Object { $_.timing })
    return [ordered]@{
        processCount=$Reports.Count; processCountEvidence='distinct successful runId/report; supervisor receipt when present; raw schema has no PID'
        measuredSampleCount=$samples.Count; hitQueryCount=$queries.Count
        stages=$stages; hitQuery=(Comparison-Metric $queries)
        maxObservedCurrentPrivateBytes=($allMemory | Measure-Object -Maximum).Maximum
        maxObservedCurrentPrivateBytesScope='maximum recorded memoryBefore/memoryAfter current private value, including warmups; not OS peak private and not retained/native bytes'
        measuredSamplesMaxObservedCurrentPrivateBytes=($measuredMemory | Measure-Object -Maximum).Maximum
        processes=$processSummaries.ToArray()
    }
}

function Comparison-MetricRatios($Before, $After) {
    return [ordered]@{
        p50Milliseconds=(Comparison-Ratio $After.p50Milliseconds $Before.p50Milliseconds)
        p95Milliseconds=(Comparison-Ratio $After.p95Milliseconds $Before.p95Milliseconds)
        p95AllocatedBytes=(Comparison-Ratio $After.p95AllocatedBytes $Before.p95AllocatedBytes)
    }
}

$loadedBefore = [Collections.Generic.List[object]]::new()
$loadedAfter = [Collections.Generic.List[object]]::new()
foreach ($side in @('before','after')) {
    $paths = @(if ($side -eq 'before') { $BeforeReports } else { $AfterReports })
    for ($ordinal = 0; $ordinal -lt $paths.Count; $ordinal++) {
        try {
            $entry = Read-ComparisonReport $paths[$ordinal] $side ($ordinal + 1)
            if ($side -eq 'before') { $loadedBefore.Add($entry) } else { $loadedAfter.Add($entry) }
        } catch { $comparisonErrors.Add("$side report $($ordinal + 1): $($_.Exception.Message)") }
    }
}

$qualityFingerprints = [Collections.Generic.List[object]]::new()
$qualityReference = $null
if ($comparisonErrors.Count -eq 0) {
    try {
        $allReports = @($loadedBefore.ToArray()) + @($loadedAfter.ToArray())
        $reference = $allReports[0]
        $globalReference = Comparison-Json (Comparison-GlobalQuality $reference.Raw)
        $scenarioIds = @($reference.Raw.scenarios.id | Sort-Object -CaseSensitive)
        $referenceScenarioQuality = @{}
        foreach ($scenario in $reference.Raw.scenarios) { $referenceScenarioQuality[$scenario.id] = Comparison-Json (Comparison-ScenarioQuality $scenario) }
        $qualityReference = [ordered]@{ global=($globalReference | ConvertFrom-Json); scenarios=@($reference.Raw.scenarios | ForEach-Object { Comparison-ScenarioQuality $_ }) }
        foreach ($report in $allReports) {
            $global = Comparison-Json (Comparison-GlobalQuality $report.Raw)
            if ($global -cne $globalReference) { $comparisonErrors.Add("$($report.Path): global configuration/environment, harness SHA, target SHA or scope differs.") }
            $currentIds = @($report.Raw.scenarios.id | Sort-Object -CaseSensitive)
            if ((Comparison-Json $currentIds) -cne (Comparison-Json $scenarioIds)) { $comparisonErrors.Add("$($report.Path): scenario ID set differs."); continue }
            $scenarioFingerprints = [ordered]@{}
            foreach ($scenario in $report.Raw.scenarios) {
                $quality = Comparison-Json (Comparison-ScenarioQuality $scenario)
                $scenarioFingerprints[$scenario.id] = Comparison-TextHash $quality
                if ($quality -cne $referenceScenarioQuality[$scenario.id]) { $comparisonErrors.Add("$($report.Path): $($scenario.id) dataset/seed/series/markers/downsampling/layout/range/primitive or query quality differs.") }
            }
            $qualityFingerprints.Add([ordered]@{ reportPath=$report.Path; globalSha256=(Comparison-TextHash $global); scenarios=$scenarioFingerprints })
        }
        if ($comparisonErrors.Count -eq 0) {
            foreach ($id in $scenarioIds) {
                $before = Comparison-Side $loadedBefore.ToArray() $id
                $after = Comparison-Side $loadedAfter.ToArray() $id
                $ratios = [ordered]@{}
                foreach ($name in $comparisonStages) { $ratios[$name] = Comparison-MetricRatios $before.stages[$name] $after.stages[$name] }
                $comparisonRows.Add([ordered]@{ id=$id; comparable=$true; before=$before; after=$after; afterOverBefore=[ordered]@{
                    stages=$ratios; hitQuery=(Comparison-MetricRatios $before.hitQuery $after.hitQuery)
                    maxObservedCurrentPrivateBytes=(Comparison-Ratio $after.maxObservedCurrentPrivateBytes $before.maxObservedCurrentPrivateBytes)
                    measuredSamplesMaxObservedCurrentPrivateBytes=(Comparison-Ratio $after.measuredSamplesMaxObservedCurrentPrivateBytes $before.measuredSamplesMaxObservedCurrentPrivateBytes)
                } })
            }
        }
    } catch { $comparisonErrors.Add($_.Exception.Message); $comparisonRows.Clear() }
}

# Inputs are immutable evidence; catch any concurrent replacement before publishing the comparison.
foreach ($receipt in $comparisonReceipts) {
    try {
        if ((Comparison-Hash $receipt.reportPath) -ne $receipt.reportSha256) { $comparisonErrors.Add("Input changed: $($receipt.reportPath)") }
    } catch { $comparisonErrors.Add("Input no longer readable: $($receipt.reportPath)") }
}
if ($comparisonErrors.Count -gt 0) { $comparisonRows.Clear() }
$comparisonResult = [ordered]@{
    schemaVersion='charts-prototype-regression-v1'
    status=$(if ($comparisonErrors.Count -eq 0) { 'comparable-prototype-statistics' } else { 'blocked-not-comparable' })
    comparable=($comparisonErrors.Count -eq 0)
    performanceQualification='pending-C4'; presentStatus='blocked-present'; referenceStatus='blocked-reference'
    formalSlaEvaluated=$false; formalPerformancePassed=$null
    generatedUtc=[DateTimeOffset]::UtcNow.ToString('O')
    comparisonScript=[ordered]@{ path=$PSCommandPath; sha256=(Comparison-Hash $PSCommandPath) }
    guardScript=[ordered]@{ path=$comparisonGuard; sha256=(Comparison-Hash $comparisonGuard) }
    evidenceDirectory=$comparisonEvidence; requestedBeforeProcesses=$BeforeReports.Count; requestedAfterProcesses=$AfterReports.Count
    requestedInputs=[ordered]@{ before=@($BeforeReports); after=@($AfterReports) }
    reports=$comparisonReceipts.ToArray(); qualityFingerprints=$qualityFingerprints.ToArray(); qualityReference=$qualityReference
    scenarios=$comparisonRows.ToArray(); errors=$comparisonErrors.ToArray()
    notes=@(
        'Each raw report is validated by a fresh child invocation of Test-ChartsPerformance; original guard files are not overwritten.'
        'Pooled percentiles use all non-warmup samples across distinct report/run IDs; hit percentiles pool non-warmup query timings. This is not a percentile of per-process percentiles.'
        'Nearest-rank percentiles match the raw guard. Ratios are after/before; a zero before denominator produces null, not Infinity or an improvement claim.'
        'Allocated bytes are reported current-thread stage deltas. Stage intervals can nest: firstUsable includes other work, source enumeration belongs to scene preparation; do not sum them.'
        'Current private values are sampled at recorded memoryBefore/memoryAfter boundaries; their maximum is not measured peak private, retained memory, or native allocations.'
        'Distinct run IDs and successful supervisor receipts identify process invocations; this raw schema does not include OS PID, CPU model, power state, or concurrent-process audit.'
        'Matching recorded manifests/primitive counts/probes does not constitute full image-golden or complete C4 quality-corpus acceptance. Native present, GPU, Linux, official reference and SLA acceptance remain outside scope.'
        'Known trace diagnostic reports are rejected. The caller remains responsible for providing untraced runs with externally verified isolated execution.'
        'Only recorded known quality fields are compared; no rendering data is downsampled or silently omitted to make reports comparable.'
    )
}
$comparisonStream = [IO.File]::Open($comparisonOutput, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $comparisonWriter = [IO.StreamWriter]::new($comparisonStream, [Text.UTF8Encoding]::new($false))
    try { $comparisonWriter.Write(($comparisonResult | ConvertTo-Json -Depth 80)) } finally { $comparisonWriter.Dispose() }
} finally { $comparisonStream.Dispose() }
Write-Output "$($comparisonResult.status): $comparisonOutput"
if ($comparisonErrors.Count -gt 0) { exit 1 }
