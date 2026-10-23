param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$modelRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(-not $OutputDirectory){$OutputDirectory=Join-Path $modelRepo ('artifacts/benchmarks/markdown/model-updates-'+[guid]::NewGuid().ToString('N'))}
$hostPath=Join-Path $OutputDirectory 'host'
New-Item -ItemType Directory -Path $hostPath -Force|Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'bin/Release/net10.0/*') -Destination $hostPath -Recurse -Force
foreach($round in 1..5){
    dotnet (Join-Path $hostPath 'Markdown.Benchmarks.dll') --model-updates --round $round --output (Join-Path $OutputDirectory "model-round-$round.json")
    if($LASTEXITCODE -ne 0){throw "Model update round $round failed; preserve reports at $OutputDirectory"}
}
Write-Output "Model update five independent processes: $OutputDirectory (model-only; no UI budget acceptance)."
