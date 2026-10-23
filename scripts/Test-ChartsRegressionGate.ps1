param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$PolicyPath = (Join-Path $PSScriptRoot 'charts-ci-performance-policy.json'),
    [string]$BaselineDirectory,
    [string]$PythonExecutable
)
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $output) { throw 'Guard output already exists; preserve old evidence and choose a new path.' }
[IO.Directory]::CreateDirectory((Split-Path $output -Parent)) | Out-Null
if (-not $PythonExecutable) { $PythonExecutable = $env:CHARTS_GATE_PYTHON }
if (-not $PythonExecutable) {
    $command = Get-Command python3 -ErrorAction SilentlyContinue
    if (-not $command) { $command = Get-Command python -ErrorAction Stop }
    $PythonExecutable = $command.Source
}
$legacyReport = Join-Path ([IO.Path]::GetFullPath($RunDirectory)) 'generic-benchmark/raw.json'
$legacyOutput = Join-Path (Split-Path $output -Parent) 'generic-integrity.json'
$legacyLog = Join-Path (Split-Path $output -Parent) 'generic-integrity.log'
if ((Test-Path -LiteralPath $legacyOutput) -or (Test-Path -LiteralPath $legacyLog)) { throw 'Legacy guard evidence already exists.' }
$shell = (Get-Process -Id $PID).Path
& $shell -NoLogo -NoProfile -File (Join-Path $PSScriptRoot 'Test-ChartsPerformance.ps1') -ReportPath $legacyReport -OutputPath $legacyOutput *> $legacyLog
$legacyExit = $LASTEXITCODE
[ordered]@{ exitCode=$legacyExit; reportPath=$legacyReport; guardPath=$legacyOutput; scriptSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Test-ChartsPerformance.ps1') -Algorithm SHA256).Hash } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path (Split-Path $output -Parent) 'generic-integrity-process.json') -Encoding utf8
$arguments = @((Join-Path $PSScriptRoot 'charts_regression_gate.py'),'--run-directory',[IO.Path]::GetFullPath($RunDirectory),'--output',$output,'--policy',[IO.Path]::GetFullPath($PolicyPath),'--legacy-integrity',$legacyOutput)
if ($BaselineDirectory) { $arguments += @('--baseline-directory',[IO.Path]::GetFullPath($BaselineDirectory)) }
& $PythonExecutable @arguments
exit $LASTEXITCODE
