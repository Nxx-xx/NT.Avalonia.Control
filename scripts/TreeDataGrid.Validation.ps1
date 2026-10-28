# Shared bookkeeping for local TreeDataGrid validation. No product behavior changes.
function Get-TreeDataGridDiskBudget {
    param([string[]]$Paths, [long]$EstimatedBytes, [long]$ReserveBytes = 2GB)
    if ($EstimatedBytes -le 0 -or $ReserveBytes -lt 0) { throw 'Invalid disk budget.' }
    $volumes = @{}
    foreach ($path in $Paths) {
        if (-not $path) { continue }
        $full = [IO.Path]::GetFullPath($path)
        $volume = [IO.Path]::GetPathRoot($full)
        if (-not $volumes.ContainsKey($volume)) {
            $available = $null; $errorText = $null
            try { $available = [IO.DriveInfo]::new($volume).AvailableFreeSpace }
            catch { $errorText = $_.Exception.Message }
            $volumes[$volume] = [ordered]@{
                volume=$volume; paths=[Collections.Generic.List[string]]::new()
                availableBytes=$available; requiredBytes=($EstimatedBytes+$ReserveBytes)
                passed=($null -ne $available -and $available -ge ($EstimatedBytes+$ReserveBytes)); error=$errorText
            }
        }
        if (-not $volumes[$volume].paths.Contains($full)) { $volumes[$volume].paths.Add($full) }
    }
    if (-not $volumes.Count) { throw 'No filesystem paths supplied for disk preflight.' }
    $rows = @($volumes.GetEnumerator() | Sort-Object Key | ForEach-Object Value)
    [ordered]@{
        schemaVersion=1; checkedUtc=[DateTimeOffset]::UtcNow; passed=(@($rows | Where-Object { -not $_.passed }).Count -eq 0)
        estimatedAdditionalBytesPerVolume=$EstimatedBytes; reserveBytesPerVolume=$ReserveBytes
        estimateKind='Conservative planning allowance, not a measured upper bound; check once before work.'; volumes=$rows
    }
}

function Assert-TreeDataGridDiskBudget {
    param([string]$OutputDirectory, [string[]]$Paths, [long]$EstimatedBytes)
    $report = Get-TreeDataGridDiskBudget -Paths $Paths -EstimatedBytes $EstimatedBytes
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'disk-preflight.json') -Encoding utf8
    if (-not $report.passed) {
        $details = @($report.volumes | Where-Object { -not $_.passed } | ForEach-Object {
            "$($_.volume) available=$($_.availableBytes), required=$($_.requiredBytes), error=$($_.error)"
        }) -join '; '
        throw "Disk preflight failed: $details. See disk-preflight.json."
    }
}

function Write-TreeDataGridValidationSummary {
    param([string]$OutputDirectory)
    $result = Get-Content -LiteralPath (Join-Path $OutputDirectory 'results.json') -Raw | ConvertFrom-Json
    $failed = @($result.steps | Where-Object { -not $_.passed })
    $message = [string]$result.error
    if ($message.Length -gt 1600) { $message = $message.Substring(0,1600) + ' [see step log]' }
    $count = 0
    foreach ($test in $result.tests) { $count += $test.result.passed }
    $summary = [ordered]@{
        schemaVersion=1; stage=$result.stage; passed=$result.passed; completedUtc=$result.completedUtc
        failureCategory=$(if($result.passed){$null}elseif(@($failed | Where-Object name -eq 'disk-preflight').Count){'DiskPreflight'}else{'ValidationFailed'})
        failedSteps=@($failed | ForEach-Object { [ordered]@{name=$_.name;log=$_.log} }); error=$message
        controlsPassed=$count; results='results.json'; evidenceIndex='evidence-index.json'
        scope='Local automated validation only; physical Windows, actual CI and Kylin have separate gates.'
    }
    $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding utf8
    $entries = @(Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File | Sort-Object FullName | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($OutputDirectory,$_.FullName).Replace('\','/')
        if ($relative -ne 'evidence-index.json' -and $relative -notmatch '^(publish|application)/') {
            [ordered]@{path=$relative;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
        }
    })
    [ordered]@{schemaVersion=1;files=$entries;excluded='Application/publish files have their own binary manifests.'} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'evidence-index.json') -Encoding utf8
}
