param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository ('artifacts/guard-tests/charts-ci-' + [guid]::NewGuid().ToString('N')) }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Test evidence already exists; choose a unique output directory.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$python = $env:CHARTS_GATE_PYTHON
if (-not $python) {
    $command = Get-Command python3 -ErrorAction SilentlyContinue
    if (-not $command) { $command = Get-Command python -ErrorAction Stop }
    $python = $command.Source
}
$started = [DateTimeOffset]::UtcNow.ToString('O')
$stdout = Join-Path $output 'unittest.stdout.log'
$stderr = Join-Path $output 'unittest.stderr.log'
$savedEvidence = $env:CHARTS_GUARD_TEST_EVIDENCE
$savedBytecode = $env:PYTHONDONTWRITEBYTECODE
try {
    $env:CHARTS_GUARD_TEST_EVIDENCE = $output
    $env:PYTHONDONTWRITEBYTECODE = '1'
    & $python -m unittest discover -s (Join-Path $repository 'scripts/Tests') -p 'test_charts_regression_gate.py' -v 1> $stdout 2> $stderr
    $testExit = $LASTEXITCODE
} finally {
    $env:CHARTS_GUARD_TEST_EVIDENCE = $savedEvidence
    $env:PYTHONDONTWRITEBYTECODE = $savedBytecode
}
$files = @(Get-ChildItem -LiteralPath $output -Recurse -File | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($output,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;bytes=$_.Length} })
$result = [ordered]@{schemaVersion='charts-ci-guard-contract-tests-v1';passed=($testExit -eq 0);exitCode=$testExit;startedUtc=$started;finishedUtc=[DateTimeOffset]::UtcNow.ToString('O');scope='Portable synthetic positive/adversarial contracts; no product performance measurement';files=$files}
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
Get-Content -LiteralPath $stderr -Tail 7
if (-not $result.passed) { exit 1 }
