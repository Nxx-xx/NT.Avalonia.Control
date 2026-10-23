param([string]$OutputDirectory, [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$benchmarkRepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $benchmarkRepoRoot ('artifacts/benchmarks/markdown/' + [guid]::NewGuid().ToString('N'))
}
$benchmarkProject = Join-Path $PSScriptRoot 'Markdown.Benchmarks.csproj'
$benchmarkDll = Join-Path $PSScriptRoot 'bin/Release/net10.0/Markdown.Benchmarks.dll'
Push-Location $benchmarkRepoRoot
try {
    if (-not $NoBuild) {
        dotnet build $benchmarkProject -c Release --no-restore -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Markdown benchmark Release build failed.' }
    }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    foreach ($round in 1..5) {
        dotnet $benchmarkDll --round $round --samples 100 --warmup 10 --output (Join-Path $OutputDirectory "round-$round.json")
        if ($LASTEXITCODE -ne 0) { throw "Markdown benchmark round $round failed." }
    }
    dotnet $benchmarkDll --summarize $OutputDirectory --output (Join-Path $OutputDirectory 'summary.json')
    if ($LASTEXITCODE -ne 0) { throw 'Markdown benchmark aggregation failed.' }
    Write-Output "Markdown benchmark reports: $OutputDirectory"
}
finally { Pop-Location }
