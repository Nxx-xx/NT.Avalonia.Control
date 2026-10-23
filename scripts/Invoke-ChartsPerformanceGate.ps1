param(
    [string]$OutputDirectory,
    [ValidateSet('NativeSynthetic','DesktopSynthetic','XvfbSynthetic')][string]$NativeMode = $(if ($IsWindows) { 'DesktopSynthetic' } else { 'NativeSynthetic' }),
    [ValidateRange(1,7200)][int]$StageTimeoutSeconds = 600,
    [ValidateRange(1,7200)][int]$BuildTimeoutSeconds = 600,
    [string]$BaselineDirectory,
    [string]$BaselineSourceDirectory,
    [string]$PolicyPath,
    [string]$PythonExecutable,
    [switch]$SkipBuild,
    [string]$HeadlessHostDirectory,
    [string]$NativeHostDirectory,
    [switch]$PlanOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$gateRepository = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'ChartsPerformanceGate.Process.ps1')
$gateRunId = [Guid]::NewGuid().ToString('N')
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $gateRepository "artifacts/charts-performance-ci/$gateRunId" }
$gateOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $gateOutput) { throw 'Output directory already exists; use a unique immutable run directory.' }
if (($HeadlessHostDirectory -or $NativeHostDirectory) -and -not $SkipBuild) { throw 'Frozen host directories require -SkipBuild.' }
if (-not $PolicyPath) { $PolicyPath = Join-Path $PSScriptRoot 'charts-ci-performance-policy.json' }
$PolicyPath = [IO.Path]::GetFullPath($PolicyPath)
[IO.Directory]::CreateDirectory($gateOutput) | Out-Null
$gateDotnet = (Get-Command dotnet -ErrorAction Stop).Source
$gateShell = (Get-Process -Id $PID).Path
$gateHeadless = if ($HeadlessHostDirectory) { Join-Path ([IO.Path]::GetFullPath($HeadlessHostDirectory)) 'Charts.Benchmarks.dll' } else { Join-Path $gateRepository 'benchmarks/Charts.Benchmarks/bin/Release/net10.0/Charts.Benchmarks.dll' }
$gateNative = if ($NativeHostDirectory) { Join-Path ([IO.Path]::GetFullPath($NativeHostDirectory)) 'Charts.NativeBenchmarks.dll' } else { Join-Path $gateRepository 'benchmarks/Charts.NativeBenchmarks/bin/Release/net10.0/Charts.NativeBenchmarks.dll' }
function Get-ChartsGateSourceManifest {
    $files = [Collections.Generic.List[IO.FileInfo]]::new()
    foreach ($relative in @('src/NT.AvaloniaControl.Charts','src/NT.AvaloniaControl.Charts.Themes','benchmarks/Charts.Benchmarks','benchmarks/Charts.NativeBenchmarks')) {
        $directory = Join-Path $gateRepository $relative
        if (Test-Path -LiteralPath $directory) {
            foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) { $files.Add($file) }
        }
    }
    foreach ($relative in @('global.json','Directory.Build.props','Directory.Packages.props','NuGet.Config','.github/workflows/charts.yml')) {
        $file = Join-Path $gateRepository $relative
        if (Test-Path -LiteralPath $file) { $files.Add((Get-Item -LiteralPath $file)) }
    }
    foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Name -match 'Charts|charts-ci' }) { $files.Add($file) }
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Tests') -File | Where-Object { $_.Name -match 'Charts' }) { $files.Add($file) }
    return @($files | Sort-Object FullName -Unique | ForEach-Object {
        [ordered]@{ path = [IO.Path]::GetRelativePath($gateRepository,$_.FullName).Replace('\','/'); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; bytes = $_.Length }
    })
}
function Save-ChartsGateJson($Value, [string]$Path) {
    $Value | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Get-ChartsGateGitSha([string]$Directory) {
    if (-not (Test-Path -LiteralPath (Join-Path $Directory '.git'))) { return $null }
    $sha = (& git -C $Directory rev-parse HEAD 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -eq 0 -and $sha -match '^[a-fA-F0-9]{40,64}$') { return $sha }
    return $null
}
function Get-ChartsGateTreeHashes([string]$Root, [string[]]$RelativePaths) {
    return @(foreach ($relative in $RelativePaths) {
        $path = Join-Path $Root $relative
        if (Test-Path -LiteralPath $path -PathType Leaf) { $items = @(Get-Item -LiteralPath $path) }
        elseif (Test-Path -LiteralPath $path -PathType Container) { $items = @(Get-ChildItem -LiteralPath $path -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) }
        else { throw "Required baseline source input is missing: $path" }
        foreach ($item in $items | Sort-Object FullName) {
            [ordered]@{ path=[IO.Path]::GetRelativePath($Root,$item.FullName).Replace('\','/'); sha256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash; bytes=$item.Length }
        }
    })
}
function Invoke-ChartsGateNativeBaseline {
    param([string]$SourceDirectory)
    $source = [IO.Path]::GetFullPath($SourceDirectory)
    if ($source.TrimEnd('\','/') -eq $gateRepository.TrimEnd('\','/')) { throw 'Baseline source must be an independent checkout, not the candidate workspace.' }
    $baselineOutput = Join-Path $gateOutput 'baseline'
    if (Test-Path -LiteralPath $baselineOutput) { throw 'Baseline output already exists; prior evidence must be preserved.' }
    [IO.Directory]::CreateDirectory($baselineOutput) | Out-Null
    $baseline = [ordered]@{
        schemaVersion='charts-ci-native-baseline-v1'; sourceDirectory=$source
        candidateGitSha=(Get-ChartsGateGitSha $gateRepository); sourceGitSha=(Get-ChartsGateGitSha $source)
        purpose='independent-source-copy-native-control'; passed=$false; error=$null
        harnessSourceManifestPath='harness-sources.json'; productSourceManifestPath='product-sources.json'
        productSourceAfterManifestPath='product-sources-after.json'; productSourceUnchanged=$false
        buildReceiptPath='build-native/process.json'; nativeReceiptPath='native-window/process.json'
        nativeReportPath='native-window/raw.json'; assemblyManifestPath='assemblies.json'
        originalNativeHarnessDirectory=$null
    }
    if ($null -ne $baseline.sourceGitSha -and $null -ne $baseline.candidateGitSha) {
        $baseline.purpose = if ($baseline.sourceGitSha -eq $baseline.candidateGitSha) { 'same-revision-native-control' } else { 'pull-request-base-product-same-candidate-harness' }
    }
    try {
        $hostRelative = 'benchmarks/Charts.NativeBenchmarks'
        $hostFiles = @(Get-ChartsGateTreeHashes $gateRepository @($hostRelative))
        $existingHostDirectory = Join-Path $source $hostRelative
        if (Test-Path -LiteralPath $existingHostDirectory) {
            $existingFiles = @(Get-ChartsGateTreeHashes $source @($hostRelative))
            if ((ConvertTo-Json -InputObject $hostFiles -Depth 10 -Compress) -cne (ConvertTo-Json -InputObject $existingFiles -Depth 10 -Compress)) {
                # The user supplied an independent baseline copy for this overlay. Preserve
                # its original harness intact; product sources are never replaced.
                $resolvedExisting = [IO.Path]::GetFullPath($existingHostDirectory)
                $expectedExisting = [IO.Path]::GetFullPath((Join-Path $source 'benchmarks/Charts.NativeBenchmarks'))
                if ($resolvedExisting -ne $expectedExisting -or -not $resolvedExisting.StartsWith($source.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Baseline harness archive target escaped the independent baseline directory.' }
                $preserved = "$resolvedExisting.c408-original-$gateRunId"
                if (Test-Path -LiteralPath $preserved) { throw 'Original baseline harness archive already exists.' }
                Save-ChartsGateJson ([ordered]@{ schemaVersion='charts-ci-source-v1'; files=$existingFiles }) (Join-Path $baselineOutput 'original-harness-sources.json')
                Move-Item -LiteralPath $resolvedExisting -Destination $preserved
                $baseline.originalNativeHarnessDirectory = $preserved
            }
        }
        foreach ($file in $hostFiles) {
            $target = Join-Path $source $file.path
            if (Test-Path -LiteralPath $target) {
                if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.sha256) { throw "Unexpected baseline overlay collision: $($file.path)" }
            } else {
                [IO.Directory]::CreateDirectory((Split-Path $target -Parent)) | Out-Null
                Copy-Item -LiteralPath (Join-Path $gateRepository $file.path) -Destination $target
            }
        }
        $copiedHostFiles = @(Get-ChartsGateTreeHashes $source @($hostRelative))
        if ((ConvertTo-Json -InputObject $hostFiles -Depth 10 -Compress) -cne (ConvertTo-Json -InputObject $copiedHostFiles -Depth 10 -Compress)) { throw 'Baseline native harness is not exactly the candidate harness source.' }
        Save-ChartsGateJson ([ordered]@{ schemaVersion='charts-ci-source-v1'; files=$copiedHostFiles; candidateFiles=$hostFiles }) (Join-Path $baselineOutput 'harness-sources.json')
        $productPaths = @('src/NT.AvaloniaControl.Charts','src/NT.AvaloniaControl.Charts.Themes','Directory.Build.props','Directory.Packages.props','global.json','NuGet.Config')
        $productBefore = @(Get-ChartsGateTreeHashes $source $productPaths)
        $candidateProduct = @(Get-ChartsGateTreeHashes $gateRepository $productPaths)
        if ((ConvertTo-Json -InputObject $productBefore -Depth 10 -Compress) -ceq (ConvertTo-Json -InputObject $candidateProduct -Depth 10 -Compress)) { $baseline.purpose = 'same-version-native-control' }
        Save-ChartsGateJson ([ordered]@{ schemaVersion='charts-ci-source-v1'; files=$productBefore }) (Join-Path $baselineOutput 'product-sources.json')
        Save-ChartsGateJson $baseline (Join-Path $baselineOutput 'baseline.json')
        $project = Join-Path $source "$hostRelative/Charts.NativeBenchmarks.csproj"
        $build = Invoke-ChartsGateProcess -Id 'baseline-build-native' -Executable $gateDotnet -Arguments @('build',$project,'-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false') -WorkingDirectory $source -OutputDirectory (Join-Path $baselineOutput 'build-native') -TimeoutSeconds $BuildTimeoutSeconds
        $assembly = Join-Path $source "$hostRelative/bin/Release/net10.0/Charts.NativeBenchmarks.dll"
        $payloadFiles = @()
        $payloadDirectory = Split-Path $assembly -Parent
        if (Test-Path -LiteralPath $payloadDirectory) {
            $payloadFiles = @(Get-ChildItem -LiteralPath $payloadDirectory -Recurse -File | Sort-Object FullName | ForEach-Object { [ordered]@{ host='native'; path=[IO.Path]::GetRelativePath($payloadDirectory,$_.FullName).Replace('\','/'); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; bytes=$_.Length } })
        }
        Save-ChartsGateJson ([ordered]@{ schemaVersion='charts-ci-payload-v1'; files=$payloadFiles }) (Join-Path $baselineOutput 'assemblies.json')
        $native = Invoke-ChartsGateProcess -Id 'baseline-native-window' -Executable $gateDotnet -Arguments @($assembly,'--output',(Join-Path $baselineOutput 'native-window'),'--mode',$NativeMode,'--source-root',$source,'--samples','12','--warmups','2') -WorkingDirectory $source -OutputDirectory (Join-Path $baselineOutput 'native-window') -TimeoutSeconds $StageTimeoutSeconds
        $productAfter = @(Get-ChartsGateTreeHashes $source $productPaths)
        Save-ChartsGateJson ([ordered]@{ schemaVersion='charts-ci-source-v1'; files=$productAfter }) (Join-Path $baselineOutput 'product-sources-after.json')
        $baseline.productSourceUnchanged = (ConvertTo-Json -InputObject $productBefore -Depth 10 -Compress) -ceq (ConvertTo-Json -InputObject $productAfter -Depth 10 -Compress)
        $baseline.passed = $build.passed -and $native.passed -and $baseline.productSourceUnchanged -and (Test-Path -LiteralPath (Join-Path $baselineOutput 'native-window/raw.json'))
    } catch { $baseline.error = $_.Exception.ToString() }
    finally { Save-ChartsGateJson $baseline (Join-Path $baselineOutput 'baseline.json') }
    return $baselineOutput
}
$gateStages = [Collections.Generic.List[object]]::new()
foreach ($definition in @(
    @('layer-cache','--layer-cache-regression'), @('visible-clipping','--visible-clipping-regression'),
    @('level-of-detail','--level-of-detail-regression'), @('efficient-hit','--efficient-hit-regression'),
    @('incremental-streaming','--incremental-streaming-regression'), @('render-resource-budget','--render-resource-budget-regression'),
    @('multichart-background-layout','--multichart-background-layout-regression')
)) {
    $stageDirectory = Join-Path $gateOutput $definition[0]
    $gateStages.Add([ordered]@{ id = $definition[0]; kind = 'mechanism'; outputDirectory = $definition[0]; reportPath = "$($definition[0])/raw.json"; receiptPath = "$($definition[0])/process.json"; executable = $gateDotnet; arguments = @($gateHeadless,$definition[1],'--output',(Join-Path $stageDirectory 'raw.json')); exitCode = $null; timedOut = $false })
}
$gateStages.Add([ordered]@{ id = 'generic-benchmark'; kind = 'headless-benchmark'; outputDirectory = 'generic-benchmark'; reportPath = 'generic-benchmark/raw.json'; receiptPath = 'generic-benchmark/process.json'; executable = $gateDotnet; arguments = @($gateHeadless,'--quick','--output',(Join-Path $gateOutput 'generic-benchmark/raw.json'),'--run-id',$gateRunId,'--phase','C4-08-hosted-ci','--samples','5','--warmups','1','--hit-queries','5','--scenarios','S10-L,S10-S,S10-B'); exitCode = $null; timedOut = $false })
$gateStages.Add([ordered]@{ id = 'native-window'; kind = 'native-window'; outputDirectory = 'native-window'; reportPath = 'native-window/raw.json'; receiptPath = 'native-window/process.json'; executable = $gateDotnet; arguments = @($gateNative,'--output',(Join-Path $gateOutput 'native-window'),'--mode',$NativeMode,'--source-root',$gateRepository,'--samples','12','--warmups','2'); exitCode = $null; timedOut = $false })
$gateCi = [ordered]@{}
foreach ($name in @('GITHUB_ACTIONS','GITHUB_EVENT_NAME','GITHUB_RUN_ID','GITHUB_RUN_ATTEMPT','GITHUB_SHA','GITHUB_REF','GITHUB_BASE_REF','GITHUB_HEAD_REF','GITHUB_WORKFLOW','GITHUB_JOB','GITHUB_REPOSITORY','RUNNER_OS','RUNNER_ARCH','RUNNER_ENVIRONMENT','ImageOS','ImageVersion','CHARTS_DISPLAY_KIND')) {
    $gateCi[$name] = [Environment]::GetEnvironmentVariable($name)
}
$gateRun = [ordered]@{
    schemaVersion = 'charts-ci-performance-run-v1'; runId = $gateRunId
    startedUtc = [DateTimeOffset]::UtcNow.ToString('O'); finishedUtc = $null
    status = 'running'; passed = $false; planOnly = [bool]$PlanOnly; noRetries = $true
    scope = 'Hosted regression guard: deterministic mechanisms, headless allocations/timings, native synthetic input and PNG semantics. Not fixed-hardware C4 SLA, compositor/present, physical input, long-soak or official parity qualification.'
    sourceKind = $(if ($SkipBuild) { 'workspace-capture-plus-supplied-host-payload; source-to-host-binding-requires-separate-frozen-source-receipt' } else { 'workspace-source-before-and-after-isolated-build-processes' })
    environment = [ordered]@{ os = [Runtime.InteropServices.RuntimeInformation]::OSDescription; architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString(); runtime = [Runtime.InteropServices.RuntimeInformation]::FrameworkDescription; processorCount = [Environment]::ProcessorCount; powerShell = $PSVersionTable.PSVersion.ToString(); nativeMode = $NativeMode; ci = $gateCi }
    policyPath = $PolicyPath; baselineDirectory = $BaselineDirectory
    policySnapshotPath = 'policy.json'; policySha256 = $null; preguardEvidencePath = 'preguard-evidence.json'
    baselineSourceDirectory = $BaselineSourceDirectory; baselineManifestPath = $null
    sourceManifestPath = 'source-before.json'; sourceAfterManifestPath = 'source-after.json'; assemblyManifestPath = 'assemblies.json'
    buildReceipts = @(); stages = @($gateStages.ToArray()); guardReceiptPath = 'guard/process.json'; guardReportPath = 'guard/result.json'
    failureRate = [ordered]@{ numerator = 0; denominator = 9; rate = 0.0; definition = 'Unsuccessful supervised producer stages / nine required producer stages; missing report counts unsuccessful; independent guard may find additional semantic failures.' }
    sourceUnchanged = $false; error = $null
}
Save-ChartsGateJson $gateRun (Join-Path $gateOutput 'orchestration.json')
try {
    if (-not $PlanOnly) {
        if (-not (Test-Path -LiteralPath $PolicyPath -PathType Leaf)) { throw 'Performance policy is missing; no measurements may run without preregistered thresholds.' }
        Copy-Item -LiteralPath $PolicyPath -Destination (Join-Path $gateOutput 'policy.json')
        $gateRun.policySha256 = (Get-FileHash -LiteralPath (Join-Path $gateOutput 'policy.json') -Algorithm SHA256).Hash
    }
    $gateBefore = @(Get-ChartsGateSourceManifest)
    Save-ChartsGateJson ([ordered]@{ schemaVersion='charts-ci-source-v1'; capturedUtc=[DateTimeOffset]::UtcNow.ToString('O'); files=$gateBefore }) (Join-Path $gateOutput 'source-before.json')
    if ($PlanOnly) { $gateRun.status = 'planned'; return }
    $gateBuilds = [Collections.Generic.List[object]]::new()
    if (-not $SkipBuild) {
        foreach ($build in @(@('build-headless','benchmarks/Charts.Benchmarks/Charts.Benchmarks.csproj'),@('build-native','benchmarks/Charts.NativeBenchmarks/Charts.NativeBenchmarks.csproj'))) {
            $arguments = @('build',(Join-Path $gateRepository $build[1]),'-c','Release','--disable-build-servers','-m:1','-p:UseSharedCompilation=false')
            $receipt = Invoke-ChartsGateProcess -Id $build[0] -Executable $gateDotnet -Arguments $arguments -WorkingDirectory $gateRepository -OutputDirectory (Join-Path $gateOutput $build[0]) -TimeoutSeconds $BuildTimeoutSeconds
            $gateBuilds.Add([ordered]@{ id=$build[0]; receiptPath="$($build[0])/process.json"; exitCode=$receipt.exitCode; timedOut=$receipt.timedOut; passed=$receipt.passed })
        }
    }
    $gateRun.buildReceipts = @($gateBuilds.ToArray())
    $gateAssemblies = [Collections.Generic.List[object]]::new()
    foreach ($entry in @(@('headless',$gateHeadless),@('native',$gateNative))) {
        $assemblyDirectory = Split-Path $entry[1] -Parent
        if (Test-Path -LiteralPath $assemblyDirectory) {
            foreach ($file in Get-ChildItem -LiteralPath $assemblyDirectory -Recurse -File | Sort-Object FullName) {
                $gateAssemblies.Add([ordered]@{ host=$entry[0]; path=[IO.Path]::GetRelativePath($assemblyDirectory,$file.FullName).Replace('\','/'); sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash; bytes=$file.Length })
            }
        }
    }
    Save-ChartsGateJson ([ordered]@{ schemaVersion='charts-ci-payload-v1'; files=@($gateAssemblies.ToArray()) }) (Join-Path $gateOutput 'assemblies.json')
    foreach ($stage in $gateStages) {
        Write-Host "Charts performance stage: $($stage.id)"
        $receipt = Invoke-ChartsGateProcess -Id $stage.id -Executable $stage.executable -Arguments $stage.arguments -WorkingDirectory $gateRepository -OutputDirectory (Join-Path $gateOutput $stage.outputDirectory) -TimeoutSeconds $StageTimeoutSeconds
        $stage.exitCode = $receipt.exitCode; $stage.timedOut = $receipt.timedOut
        if (-not $receipt.passed -or -not (Test-Path -LiteralPath (Join-Path $gateOutput $stage.reportPath))) { $gateRun.failureRate.numerator++ }
        Save-ChartsGateJson $gateRun (Join-Path $gateOutput 'orchestration.json')
    }
    if ($BaselineSourceDirectory) {
        if ($BaselineDirectory) { throw 'Use either -BaselineDirectory or -BaselineSourceDirectory, not both.' }
        $BaselineDirectory = Invoke-ChartsGateNativeBaseline -SourceDirectory $BaselineSourceDirectory
        $gateRun.baselineDirectory = $BaselineDirectory
        $gateRun.baselineManifestPath = 'baseline/baseline.json'
    }
    $gateRun.failureRate.rate = $gateRun.failureRate.numerator / [double]$gateRun.failureRate.denominator
} catch { $gateRun.error = $_.Exception.ToString() }
finally {
    try {
        $gateAfter = @(Get-ChartsGateSourceManifest)
        Save-ChartsGateJson ([ordered]@{ schemaVersion='charts-ci-source-v1'; capturedUtc=[DateTimeOffset]::UtcNow.ToString('O'); files=$gateAfter }) (Join-Path $gateOutput 'source-after.json')
        if ($null -ne (Get-Variable gateBefore -ErrorAction SilentlyContinue)) {
            $gateRun.sourceUnchanged = (ConvertTo-Json -InputObject $gateBefore -Depth 10 -Compress) -ceq (ConvertTo-Json -InputObject $gateAfter -Depth 10 -Compress)
        }
        if (-not $PlanOnly) {
            $gateRun.failureRate.numerator = 0
            foreach ($stage in $gateStages) {
                $successful = $false
                try {
                    $receiptPath = Join-Path $gateOutput $stage.receiptPath
                    $reportPath = Join-Path $gateOutput $stage.reportPath
                    if ((Test-Path -LiteralPath $receiptPath) -and (Test-Path -LiteralPath $reportPath)) {
                        $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
                        $successful = $receipt.passed -is [bool] -and $receipt.passed -and $receipt.exitCode -eq 0 -and $receipt.timedOut -eq $false
                    }
                } catch { $successful = $false }
                if (-not $successful) { $gateRun.failureRate.numerator++ }
            }
            $gateRun.failureRate.rate = $gateRun.failureRate.numerator / [double]$gateRun.failureRate.denominator
            Save-ChartsGateJson $gateRun (Join-Path $gateOutput 'orchestration.json')
            $preguardFiles = @(Get-ChildItem -LiteralPath $gateOutput -Recurse -File | Where-Object { $_.Name -notin @('orchestration.json','preguard-evidence.json','checksums.json') -and -not $_.FullName.StartsWith((Join-Path $gateOutput 'guard') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) } | Sort-Object FullName | ForEach-Object { [ordered]@{ path=[IO.Path]::GetRelativePath($gateOutput,$_.FullName).Replace('\','/'); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; bytes=$_.Length } })
            Save-ChartsGateJson ([ordered]@{ schemaVersion='charts-ci-preguard-evidence-v1'; runId=$gateRunId; capturedUtc=[DateTimeOffset]::UtcNow.ToString('O'); excludedMutableFiles=@('orchestration.json','preguard-evidence.json','checksums.json','guard/**'); files=$preguardFiles }) (Join-Path $gateOutput 'preguard-evidence.json')
            $arguments = @('-NoLogo','-NoProfile','-File',(Join-Path $PSScriptRoot 'Test-ChartsRegressionGate.ps1'),'-RunDirectory',$gateOutput,'-OutputPath',(Join-Path $gateOutput 'guard/result.json'),'-PolicyPath',(Join-Path $gateOutput 'policy.json'))
            if ($BaselineDirectory) { $arguments += @('-BaselineDirectory',[IO.Path]::GetFullPath($BaselineDirectory)) }
            if ($PythonExecutable) { $arguments += @('-PythonExecutable',$PythonExecutable) }
            $guardReceipt = Invoke-ChartsGateProcess -Id 'guard' -Executable $gateShell -Arguments $arguments -WorkingDirectory $gateRepository -OutputDirectory (Join-Path $gateOutput 'guard') -TimeoutSeconds $StageTimeoutSeconds
            $guardPassed = $false
            if (Test-Path -LiteralPath (Join-Path $gateOutput 'guard/result.json')) {
                $guardResult = Get-Content -LiteralPath (Join-Path $gateOutput 'guard/result.json') -Raw | ConvertFrom-Json
                $guardPassed = $guardResult.passed -is [bool] -and $guardResult.passed
            }
            $allBuildsPassed = @($gateRun.buildReceipts | Where-Object { -not $_.passed }).Count -eq 0
            $gateRun.passed = $guardReceipt.passed -and $guardPassed -and $allBuildsPassed -and $gateRun.sourceUnchanged -and $null -eq $gateRun.error -and $gateRun.failureRate.numerator -eq 0
            $gateRun.status = if ($gateRun.passed) { 'passed-hosted-regression-guard' } else { 'failed' }
        }
    } catch { $gateRun.error = $_.Exception.ToString(); $gateRun.passed = $false; $gateRun.status = 'failed' }
    $gateRun.finishedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    Save-ChartsGateJson $gateRun (Join-Path $gateOutput 'orchestration.json')
    $gateChecksums = @(Get-ChildItem -LiteralPath $gateOutput -Recurse -File | Where-Object { $_.Name -ne 'checksums.json' } | Sort-Object FullName | ForEach-Object { [ordered]@{ path=[IO.Path]::GetRelativePath($gateOutput,$_.FullName).Replace('\','/'); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; bytes=$_.Length } })
    Save-ChartsGateJson ([ordered]@{ schemaVersion='charts-ci-artifacts-v1'; runId=$gateRunId; files=$gateChecksums }) (Join-Path $gateOutput 'checksums.json')
    Write-Host "Charts performance evidence: $gateOutput; status=$($gateRun.status)"
}
if (-not $gateRun.passed) { exit 1 }
