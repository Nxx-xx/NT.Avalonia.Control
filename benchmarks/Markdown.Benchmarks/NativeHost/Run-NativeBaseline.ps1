param([string]$OutputDirectory, [string]$SourceHostPath)
$ErrorActionPreference='Stop'
$nativeRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $nativeRepo ('artifacts/benchmarks/markdown/native-' + [guid]::NewGuid().ToString('N')) }
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh native baseline directory; preserve previous evidence.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if (-not $SourceHostPath) { $SourceHostPath=Join-Path $PSScriptRoot 'bin/Release/net10.0' }
if (-not (Test-Path -LiteralPath (Join-Path $SourceHostPath 'Markdown.NativeBenchmarks.dll'))) { throw 'Root must build native Release host first.' }
$isolatedHost=Join-Path $OutputDirectory 'host'
New-Item -ItemType Directory -Path $isolatedHost -Force | Out-Null
Copy-Item -Path (Join-Path $SourceHostPath '*') -Destination $isolatedHost -Recurse -Force
$nativeDll=Join-Path $isolatedHost 'Markdown.NativeBenchmarks.dll'
[pscustomobject]@{
    创建时间UTC=[DateTimeOffset]::UtcNow
    来源=[IO.Path]::GetFullPath($SourceHostPath)
    功能程序集SHA256=(Get-FileHash (Join-Path $isolatedHost 'NT.AvaloniaControl.Markdown.dll')).Hash
    主题程序集SHA256=(Get-FileHash (Join-Path $isolatedHost 'NT.AvaloniaControl.Markdown.Themes.dll')).Hash
    TextMate程序集SHA256=(Get-FileHash (Join-Path $isolatedHost 'NT.AvaloniaControl.Markdown.TextMate.dll')).Hash
    Host程序集SHA256=(Get-FileHash $nativeDll).Hash
    说明='五轮均运行同一份完整隔离副本；当前源码或后续共享build不会替换报告程序集。'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'source-snapshot.json') -Encoding utf8
$reports=@()
foreach ($round in 1..5) {
    $path=Join-Path $OutputDirectory "native-round-$round.json"
    dotnet $nativeDll --round $round --samples 100 --stream-sessions 100 --long-stream-sessions 100 --output $path
    if (-not (Test-Path -LiteralPath $path)) { throw "Native round $round produced no report." }
    $reports+=$path
    if ($LASTEXITCODE -ne 0) { throw "Native round $round failed budget or semantic checks; see $path. Budgets remain fixed." }
}
& (Join-Path $nativeRepo 'scripts/Test-MarkdownPerformance.ps1') -ReportPaths $reports -OutputPath (Join-Path $OutputDirectory 'guard.json')
