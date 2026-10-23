param(
    [Parameter(Mandatory)][string]$ReportPath,
    [string]$OutputPath
)
$ErrorActionPreference = 'Stop'
if (-not $OutputPath) { $OutputPath = Join-Path (Split-Path ([IO.Path]::GetFullPath($ReportPath)) -Parent) 'guard.json' }
$chartsGuardErrors = [Collections.Generic.List[string]]::new()
function Required($Object, [string]$Name) {
    if ($Object -isnot [pscustomobject] -or $null -eq $Object.PSObject.Properties[$Name]) { throw "Missing field $Name." }
    return ,$Object.PSObject.Properties[$Name].Value
}
function Number($Object, [string]$Name, [switch]$Integer) {
    $value = Required $Object $Name
    if ($null -eq $value -or $value -is [string] -or $value -is [bool] -or $value -isnot [ValueType]) { throw "$Name must be JSON number." }
    $numeric = [double]$value
    if (-not [double]::IsFinite($numeric) -or $numeric -lt 0 -or ($Integer -and [Math]::Truncate($numeric) -ne $numeric)) { throw "$Name must be finite nonnegative number." }
    return $numeric
}
function Text($Object, [string]$Name) {
    $value = Required $Object $Name
    if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) { throw "$Name must be nonempty string." }
    return $value
}
function JsonArray($Object, [string]$Name) {
    $value = Required $Object $Name
    if ($value -isnot [array]) { throw "$Name must be JSON array." }
    return ,$value
}
function Bool($Object, [string]$Name) {
    $value = Required $Object $Name
    if ($value -isnot [bool]) { throw "$Name must be JSON boolean." }
    return $value
}
function Same([double]$Actual, [double]$Expected, [string]$Name) {
    if ([Math]::Abs($Actual - $Expected) -gt [Math]::Max(1e-8, [Math]::Abs($Expected) * 1e-9)) { throw "$Name differs from raw data." }
}
function Percentile([double[]]$Values, [double]$Fraction) {
    if ($Values.Count -eq 0) { throw 'Empty percentile input.' }
    $ordered = @($Values | Sort-Object)
    return $ordered[[Math]::Max(0, [int][Math]::Ceiling($ordered.Count * $Fraction) - 1)]
}
function Timing($Value, [double]$Frequency) {
    $start = Number $Value 'startTicks' -Integer; $end = Number $Value 'endTicks' -Integer
    if ($end -lt $start) { throw 'Stopwatch interval is reversed.' }
    [void](Number $Value 'allocatedBytes' -Integer)
    $elapsed = ($end - $start) * 1000.0 / $Frequency
    Same (Number $Value 'elapsedMilliseconds') $elapsed 'ElapsedMilliseconds'
    return $elapsed
}
function HashText($Object, [string]$Name, [int]$Length = 64) {
    $value = Text $Object $Name
    if ($value -notmatch "^[A-Fa-f0-9]{$Length}$") { throw "Invalid $Name hexadecimal hash." }
    return $value
}
try {
    $chartsRaw = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
    if ((Text $chartsRaw 'schemaVersion') -ne 'charts-c0-headless-v1') { throw 'Unsupported report schema.' }
    if ((Text $chartsRaw 'status') -ne 'completed-prototype' -or -not (Bool $chartsRaw 'producerPassed')) { throw 'Producer did not complete successfully.' }
    [void](Text $chartsRaw 'runId'); [void](Text $chartsRaw 'phase')
    if ((Text $chartsRaw 'performanceQualification') -ne 'pending-C4') { throw 'Prototype may not claim formal performance qualification.' }
    if ((Text $chartsRaw 'presentStatus') -ne 'blocked-present' -or (Text $chartsRaw 'referenceStatus') -ne 'blocked-reference') { throw 'Unsupported present/reference qualification claim.' }
    foreach ($field in @('framePipelineMs','presentMs','submitMs','error')) { if ($null -ne (Required $chartsRaw $field)) { throw "$field must remain null for this host." } }
    $frequency = Number $chartsRaw 'stopwatchFrequency' -Integer
    if ($frequency -le 0) { throw 'Invalid stopwatch frequency.' }
    $config = Required $chartsRaw 'configuration'
    $sampleCount = [int](Number $config 'samples' -Integer); $warmupCount = [int](Number $config 'warmups' -Integer)
    $queryCount = [int](Number $config 'hitQueries' -Integer)
    if ($sampleCount -lt 1 -or $queryCount -lt 1) { throw 'Missing required samples/queries.' }
    $expectedScenarios = JsonArray $config 'scenarios'
    $scenarios = JsonArray $chartsRaw 'scenarios'
    if ($scenarios.Count -ne $expectedScenarios.Count -or $scenarios.Count -eq 0) { throw 'Scenario count differs.' }
    $seen = [Collections.Generic.HashSet[string]]::new()
    foreach ($scenario in $scenarios) {
        $id = Text $scenario 'id'
        if (-not $seen.Add($id) -or $id -notin $expectedScenarios) { throw 'Duplicate or unexpected scenario.' }
        if ((Text $scenario 'status') -ne 'completed-prototype' -or $null -ne (Required $scenario 'error')) { throw "$id did not complete." }
        $datasets = JsonArray $scenario 'datasets'; $byRevision = @{}
        foreach ($dataset in $datasets) {
            if ((Text $dataset 'id') -ne $id) { throw 'Dataset scenario differs.' }
            [void](HashText $dataset 'sha256'); [void](Text $dataset 'generatorVersion'); [void](HashText $dataset 'seedHex' 16)
            $revision = [int](Number $dataset 'revision' -Integer)
            if ($byRevision.ContainsKey($revision)) { throw 'Duplicate dataset revision.' }
            $counts = JsonArray $dataset 'seriesPointCounts'; $hashes = JsonArray $dataset 'seriesSha256'
            if ($counts.Count -eq 0 -or $hashes.Count -ne $counts.Count) { throw 'Invalid dataset series manifest.' }
            $total = 0
            foreach ($count in $counts) {
                if ($count -is [string] -or $count -is [bool] -or [double]$count -le 0 -or [Math]::Truncate([double]$count) -ne [double]$count) { throw 'Invalid series point count.' }
                $total += [long]$count
            }
            foreach ($hash in $hashes) { if ($hash -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Invalid series SHA.' } }
            Same (Number $dataset 'inputPointCount' -Integer) $total 'dataset total'
            if ((Number $dataset 'validPointCount' -Integer) -gt $total) { throw 'Valid points exceed input.' }
            $manifestText = @((Text $dataset 'generatorVersion'), $id, (Text $dataset 'seedHex'), [string]$revision) + $hashes
            $combinedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($manifestText -join "`n"))))
            if ($combinedHash -ne $dataset.sha256) { throw 'Dataset combined SHA differs.' }
            $byRevision[$revision] = $dataset
        }
        $samples = JsonArray $scenario 'samples'
        if ($samples.Count -ne $sampleCount + $warmupCount) { throw 'Incomplete sample count.' }
        $metrics = @{}
        foreach ($name in @('harnessSourceSnapshot','sourceApply','scenePrepare','cachedSceneGetter','renderRecord','firstUsableToRecord','hitQuery')) { $metrics[$name] = [Collections.Generic.List[double]]::new() }
        for ($index = 0; $index -lt $samples.Count; $index++) {
            $sample = $samples[$index]
            Same (Number $sample 'sampleIndex' -Integer) $index 'sample index'
            $warmup = Bool $sample 'isWarmup'
            if ($warmup -ne ($index -lt $warmupCount)) { throw 'Warmup flag differs from configured sequence.' }
            $revision = [int](Number $sample 'datasetRevision' -Integer)
            if (-not $byRevision.ContainsKey($revision)) { throw 'Sample lacks dataset revision.' }
            $dataset = $byRevision[$revision]
            if ((HashText $sample 'datasetSha256') -ne $dataset.sha256) { throw 'Sample dataset SHA differs.' }
            Same (Number $sample 'inputPointCount' -Integer) $dataset.inputPointCount 'sample points'
            if (-not (Bool $sample 'geometryFinite') -or (Number $sample 'primitiveCount' -Integer) -lt 1) { throw 'Missing/nonfinite recorded geometry.' }
            foreach ($name in @('harnessSourceSnapshot','sourceApply','scenePrepare','cachedSceneGetter','renderRecord','firstUsableToRecord')) {
                $duration = Timing (Required $sample $name) $frequency
                if (-not $warmup) { $metrics[$name].Add($duration) }
            }
            $plot = JsonArray $sample 'plotArea'; $range = JsonArray $sample 'visibleRange'
            if ($plot.Count -ne 4 -or $range.Count -ne 4 -or $plot[2] -le 0 -or $plot[3] -le 0 -or $range[0] -ge $range[1] -or $range[2] -ge $range[3]) { throw 'Invalid layout/range.' }
            foreach ($value in @($plot) + @($range)) { if ($value -is [string] -or $value -is [bool] -or -not [double]::IsFinite([double]$value)) { throw 'Nonfinite coordinates.' } }
            foreach ($name in @('memoryBefore','memoryAfter')) {
                $memory = Required $sample $name
                foreach ($field in @('workingSetBytes','privateBytes','peakWorkingSetBytes','managedHeapBytes','totalAllocatedBytes','processCpuTicks')) { [void](Number $memory $field -Integer) }
            }
            $enumerations = JsonArray $sample 'sourceEnumerations'
            if ($enumerations.Count -ne $dataset.seriesPointCounts.Count) { throw 'Missing source enumeration diagnostics.' }
            for ($series = 0; $series -lt $enumerations.Count; $series++) {
                Same (Number $enumerations[$series] 'seriesIndex' -Integer) $series 'enumeration series'
                $completed = Number $enumerations[$series] 'completedCount' -Integer
                if ($completed -lt 1 -or $completed -gt (Number $enumerations[$series] 'enumerationCount' -Integer)) { throw 'Incomplete source enumeration.' }
                Same (Number $enumerations[$series] 'visitedPointCount' -Integer) ($completed * $dataset.seriesPointCounts[$series]) 'enumerated points'
                [void](Number $enumerations[$series] 'elapsedTicks' -Integer)
            }
            $queries = JsonArray $sample 'queries'
            if ($queries.Count -ne $queryCount) { throw 'Incomplete query count.' }
            for ($queryIndex = 0; $queryIndex -lt $queries.Count; $queryIndex++) {
                $query = $queries[$queryIndex]
                Same (Number $query 'queryIndex' -Integer) $queryIndex 'query index'
                $series = [int](Number $query 'hitSeriesIndex' -Integer); $rawIndex = Number $query 'hitRawIndex' -Integer
                if ($series -ge $dataset.seriesPointCounts.Count -or $rawIndex -ge $dataset.seriesPointCounts[$series]) { throw 'Hit source index out of range.' }
                if (-not (Bool $query 'oracleMatched')) { throw 'Query oracle failed.' }
                if ((HashText $query 'actualXBits' 16) -ne (HashText $query 'sourceOracleXBits' 16) -or (HashText $query 'actualYBits' 16) -ne (HashText $query 'sourceOracleYBits' 16)) { throw 'Hit point differs from source-index oracle.' }
                if ((Text $query 'oracleMode') -eq 'exact-probe-nearest-and-source-index') {
                    Same $series (Number $query 'expectedSeriesIndex' -Integer) 'nearest series'
                    Same $rawIndex (Number $query 'expectedRawIndex' -Integer) 'nearest raw index'
                    if ($query.actualXBits -ne (HashText $query 'probeXBits' 16) -or $query.actualYBits -ne (HashText $query 'probeYBits' 16)) { throw 'Nearest hit differs from fixed probe.' }
                } elseif ($query.oracleMode -ne 'source-index-consistency-with-overlapping-columns') { throw 'Unknown oracle mode.' }
                $duration = Timing (Required $query 'timing') $frequency
                if (-not $warmup) { $metrics.hitQuery.Add($duration) }
            }
        }
        $summary = Required $scenario 'summary'
        foreach ($name in $metrics.Keys) {
            $values = $metrics[$name].ToArray(); $stats = Required $summary $name
            Same (Number $stats 'count' -Integer) $values.Count "$name sample count"
            Same (Number $stats 'p50Milliseconds') (Percentile $values 0.5) "$name p50"
            Same (Number $stats 'p95Milliseconds') (Percentile $values 0.95) "$name p95"
            Same (Number $stats 'maximumMilliseconds') (($values | Measure-Object -Maximum).Maximum) "$name maximum"
        }
    }
} catch { $chartsGuardErrors.Add($_.Exception.Message) }
$chartsGuardResult = [ordered]@{
    schemaVersion='charts-c0-guard-v1'; passed=($chartsGuardErrors.Count -eq 0); scope='prototype-schema-raw-statistics-and-index-oracle'
    performanceQualification='pending-C4'; presentStatus='blocked-present'; referenceStatus='blocked-reference'
    reportPath=[IO.Path]::GetFullPath($ReportPath); reportSha256=if(Test-Path -LiteralPath $ReportPath){(Get-FileHash -LiteralPath $ReportPath -Algorithm SHA256).Hash}else{$null}
    errors=$chartsGuardErrors.ToArray(); checkedUtc=[DateTimeOffset]::UtcNow.ToString('O')
}
$chartsGuardOutput = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory((Split-Path $chartsGuardOutput -Parent)) | Out-Null
$chartsGuardResult | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $chartsGuardOutput -Encoding utf8
$chartsGuardResult | ConvertTo-Json -Depth 20
if (-not $chartsGuardResult.passed) { exit 1 }
