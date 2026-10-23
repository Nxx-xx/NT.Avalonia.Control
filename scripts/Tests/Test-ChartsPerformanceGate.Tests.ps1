param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$testRepository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
. (Join-Path $testRepository 'scripts/ChartsPerformanceGate.Process.ps1')
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $testRepository ('artifacts/charts-performance-gate-tests/' + [Guid]::NewGuid().ToString('N')) }
$testOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $testOutput) { throw 'Test output already exists; preserve prior evidence.' }
[IO.Directory]::CreateDirectory($testOutput) | Out-Null
$testShell = (Get-Process -Id $PID).Path
$testChecks = [ordered]@{}
$testError = $null
function Check([string]$Name, [bool]$Condition) {
    $testChecks[$Name] = $Condition
    if (-not $Condition) { throw "Contract check failed: $Name" }
}
try {
    $normal = Invoke-ChartsGateProcess -Id 'normal' -Executable $testShell -Arguments @('-NoLogo','-NoProfile','-Command','[Console]::Out.WriteLine("output-receipt"); [Console]::Error.WriteLine("error-receipt"); exit 0') -WorkingDirectory $testRepository -OutputDirectory (Join-Path $testOutput 'normal') -TimeoutSeconds 20
    Check 'normal:exit-zero-and-passed' ($normal.passed -and $normal.exitCode -eq 0 -and -not $normal.timedOut)
    Check 'normal:separate-stdout' ((Get-Content -LiteralPath (Join-Path $testOutput 'normal/stdout.log') -Raw).Contains('output-receipt'))
    Check 'normal:separate-stderr' ((Get-Content -LiteralPath (Join-Path $testOutput 'normal/stderr.log') -Raw).Contains('error-receipt'))
    $large = Invoke-ChartsGateProcess -Id 'large' -Executable $testShell -Arguments @('-NoLogo','-NoProfile','-Command','$line="x"*4096; 1..100 | ForEach-Object { [Console]::Out.WriteLine($line); [Console]::Error.WriteLine($line) }; exit 0') -WorkingDirectory $testRepository -OutputDirectory (Join-Path $testOutput 'large') -TimeoutSeconds 20
    Check 'large:both-pipes-drained-without-deadlock' ($large.passed -and (Get-Item -LiteralPath (Join-Path $testOutput 'large/stdout.log')).Length -gt 400000 -and (Get-Item -LiteralPath (Join-Path $testOutput 'large/stderr.log')).Length -gt 400000)
    $failure = Invoke-ChartsGateProcess -Id 'nonzero' -Executable $testShell -Arguments @('-NoLogo','-NoProfile','-Command','[Console]::Out.WriteLine("before-failure"); exit 7') -WorkingDirectory $testRepository -OutputDirectory (Join-Path $testOutput 'nonzero') -TimeoutSeconds 20
    Check 'nonzero:exit-preserved-and-fails' (-not $failure.passed -and $failure.exitCode -eq 7 -and -not $failure.timedOut)
    Check 'nonzero:evidence-retained' ((Get-Content -LiteralPath (Join-Path $testOutput 'nonzero/stdout.log') -Raw).Contains('before-failure'))
    $timeout = Invoke-ChartsGateProcess -Id 'timeout' -Executable $testShell -Arguments @('-NoLogo','-NoProfile','-Command','[Console]::Out.WriteLine("before-timeout"); [Console]::Out.Flush(); Start-Sleep -Seconds 30') -WorkingDirectory $testRepository -OutputDirectory (Join-Path $testOutput 'timeout') -TimeoutSeconds 2
    Check 'timeout:timed-out-and-fails' ($timeout.timedOut -and -not $timeout.passed)
    Check 'timeout:partial-output-retained' ((Get-Content -LiteralPath (Join-Path $testOutput 'timeout/stdout.log') -Raw).Contains('before-timeout'))
    Check 'timeout:terminal-receipt-retained' ($null -ne (Get-Content -LiteralPath (Join-Path $testOutput 'timeout/process.json') -Raw | ConvertFrom-Json).finishedUtc)
    $missing = Invoke-ChartsGateProcess -Id 'missing-executable' -Executable (Join-Path $testOutput 'missing-command') -WorkingDirectory $testRepository -OutputDirectory (Join-Path $testOutput 'missing') -TimeoutSeconds 2
    Check 'missing-executable:structured-failure' (-not $missing.passed -and $null -ne $missing.error -and $null -eq $missing.exitCode)
    $duplicateRejected = $false
    try { $null = Invoke-ChartsGateProcess -Id 'overwrite' -Executable $testShell -WorkingDirectory $testRepository -OutputDirectory (Join-Path $testOutput 'normal') -TimeoutSeconds 2 } catch { $duplicateRejected = $true }
    Check 'immutable-stage:rejects-existing-directory' $duplicateRejected
    $planPath = Join-Path $testOutput 'plan'
    $plan = Invoke-ChartsGateProcess -Id 'plan' -Executable $testShell -Arguments @('-NoLogo','-NoProfile','-File',(Join-Path $testRepository 'scripts/Invoke-ChartsPerformanceGate.ps1'),'-OutputDirectory',$planPath,'-NativeMode','DesktopSynthetic','-PlanOnly') -WorkingDirectory $testRepository -OutputDirectory (Join-Path $testOutput 'plan-process') -TimeoutSeconds 60
    Check 'plan:command-success' $plan.passed
    $run = Get-Content -LiteralPath (Join-Path $planPath 'orchestration.json') -Raw | ConvertFrom-Json
    Check 'plan:never-claims-measured-pass' ($run.planOnly -and -not $run.passed -and $run.status -eq 'planned')
    $ids = @('layer-cache','visible-clipping','level-of-detail','efficient-hit','incremental-streaming','render-resource-budget','multichart-background-layout','generic-benchmark','native-window')
    Check 'plan:nine-required-unique-producers' ($run.stages.Count -eq 9 -and @($run.stages.id | Sort-Object -Unique).Count -eq 9 -and (@($run.stages.id | Sort-Object) -join ',') -eq (@($ids | Sort-Object) -join ','))
    Check 'plan:seven-mechanisms' (@($run.stages | Where-Object kind -EQ 'mechanism').Count -eq 7)
    $generic = @($run.stages | Where-Object id -EQ 'generic-benchmark')[0]
    $native = @($run.stages | Where-Object id -EQ 'native-window')[0]
    Check 'plan:generic-samples-five' ($generic.arguments[([array]::IndexOf($generic.arguments,'--samples') + 1)] -eq '5')
    Check 'plan:generic-three-fixed-scenarios' ($generic.arguments -contains 'S10-L,S10-S,S10-B')
    Check 'plan:native-twelve-samples-two-warmups' ($native.arguments[([array]::IndexOf($native.arguments,'--samples') + 1)] -eq '12' -and $native.arguments[([array]::IndexOf($native.arguments,'--warmups') + 1)] -eq '2')
    Check 'plan:native-source-and-mode-bound' ($native.arguments -contains '--source-root' -and $native.arguments -contains $testRepository -and $native.arguments -contains 'DesktopSynthetic')
    Check 'plan:failure-denominator-explicit' ($run.failureRate.denominator -eq 9 -and $run.failureRate.definition.Length -gt 0)
    Check 'plan:source-and-tool-manifests-retained' ((Test-Path -LiteralPath (Join-Path $planPath 'source-before.json')) -and (Test-Path -LiteralPath (Join-Path $planPath 'source-after.json')) -and (Test-Path -LiteralPath (Join-Path $planPath 'checksums.json')))
    $workflow = Get-Content -LiteralPath (Join-Path $testRepository '.github/workflows/charts.yml') -Raw
    Check 'ci:benchmark-and-chart-script-path-triggers' ($workflow.Contains('benchmarks/Charts') -and $workflow.Contains('scripts/*Charts*') -and $workflow.Contains('scripts/charts-ci-performance-policy.json'))
    Check 'ci:scheduled-and-manual-runs' ($workflow.Contains('schedule:') -and $workflow.Contains('workflow_dispatch:'))
    Check 'ci:always-upload-thirty-days' ($workflow.Contains('if: always()') -and $workflow.Contains('retention-days: 30'))
    Check 'ci:no-continue-on-error' (-not $workflow.Contains('continue-on-error: true'))
    Check 'ci:strict-guard-tests-and-orchestration-tests-wired' ($workflow.Contains('Test-ChartsRegressionGate.Tests.ps1') -and $workflow.Contains('Test-ChartsPerformanceGate.Tests.ps1'))
    Check 'ci:independent-pr-base-checkout' ($workflow.Contains('github.event.pull_request.base.sha || github.sha') -and $workflow.Contains('path: .charts-baseline') -and $workflow.Contains('persist-credentials: false'))
    Check 'ci:baseline-required-on-both-platform-commands' (([regex]::Matches($workflow,'-BaselineSourceDirectory "\.charts-baseline"')).Count -eq 2)
    Check 'ci:guard-python-provisioned' ($workflow.Contains('actions/setup-python@v5') -and $workflow.Contains("python-version: '3.12'"))
    Check 'ci:lowercase-python-tool-changes-trigger' ($workflow.Contains("'scripts/*charts*'") -and $workflow.Contains("'scripts/Tests/*charts*'"))
} catch { $testError = $_.Exception.ToString() }
$passed = $null -eq $testError -and $testChecks.Count -gt 0 -and @($testChecks.Values | Where-Object { $_ -ne $true }).Count -eq 0
$result = [ordered]@{ schemaVersion='charts-ci-orchestrator-tests-v1'; passed=$passed; checks=$testChecks; error=$testError; outputDirectory=$testOutput }
$result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $testOutput 'result.json') -Encoding utf8
$result | ConvertTo-Json -Depth 20
if (-not $passed) { exit 1 }
