param([switch]$SkipConsumer, [string]$MarkdownGuardContractEvidence)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$verificationRun = Join-Path $repoRoot ('artifacts/verification/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $verificationRun -Force | Out-Null
$verificationStarted = [DateTimeOffset]::UtcNow
$verificationResults = @()
$verificationPassed = $false
$markdownGuardContractReused = $false
function Get-VerificationSourceManifest {
    $verificationPaths = @(& rg --files --hidden src tests samples benchmarks scripts .github -g '!**/bin/**' -g '!**/obj/**') +
        @('Directory.Build.props', 'Directory.Packages.props', 'NuGet.Config', 'global.json', 'NT.AvaloniaControl.slnx', '.editorconfig')
    if ($LASTEXITCODE -ne 0) { throw 'Verification source enumeration failed.' }
    @($verificationPaths | Sort-Object -Unique | ForEach-Object {
        [ordered]@{ path = $_.Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
    })
}
$verificationSources = @()
$verificationSourcesUnchanged = $false
$verificationSourceComparisonCompleted = $false
Push-Location $repoRoot
try {
    $verificationSources = Get-VerificationSourceManifest
    $verificationSources | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $verificationRun 'source-manifest.json') -Encoding utf8
    dotnet restore NT.AvaloniaControl.slnx --locked-mode --disable-parallel -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    dotnet build NT.AvaloniaControl.slnx -c Release --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    # xUnit v3 has a direct runner: no VSTest testhost IPC dependency.
    foreach ($project in 'PdfViewer.Core.Tests', 'PdfViewer.Headless.Tests', 'Charts.Headless.Tests', 'TreeDataGrid.Core.Tests', 'TreeDataGrid.Headless.Tests', 'TreeDataGrid.Themes.Tests',
                         'VirtualKeyboard.Core.Tests', 'VirtualKeyboard.Headless.Tests', 'VirtualKeyboard.Themes.Tests',
                         'Markdown.Core.Tests', 'Markdown.Streaming.Tests', 'Markdown.Headless.Tests', 'Markdown.Themes.Tests', 'Markdown.TextMate.Tests') {
        $verificationAssembly = Join-Path $repoRoot "tests/$project/bin/Release/net10.0/$project.dll"
        $verificationXml = Join-Path $verificationRun "$project.xml"
        dotnet $verificationAssembly -noLogo -xml $verificationXml
        $verificationExitCode = $LASTEXITCODE
        $verificationResult = if (Test-Path -LiteralPath $verificationXml) { ([xml](Get-Content -LiteralPath $verificationXml -Raw)).assemblies.assembly } else { $null }
        $verificationResults += [ordered]@{
            project = $project; exitCode = $verificationExitCode
            total = if ($verificationResult) { [int]$verificationResult.GetAttribute('total') } else { $null }
            passed = if ($verificationResult) { [int]$verificationResult.GetAttribute('passed') } else { $null }
            failed = if ($verificationResult) { [int]$verificationResult.GetAttribute('failed') } else { $null }
            errors = if ($verificationResult) { [int]$verificationResult.GetAttribute('errors') } else { $null }
            skipped = if ($verificationResult) { [int]$verificationResult.GetAttribute('skipped') } else { $null }
            notRun = if ($verificationResult) { [int]$verificationResult.GetAttribute('not-run') } else { $null }
            assemblySha256 = (Get-FileHash -LiteralPath $verificationAssembly -Algorithm SHA256).Hash
            resultsXml = $verificationXml
        }
        if ($verificationExitCode -ne 0) { throw "$project failed. Evidence: $verificationRun" }
    }
    # Validate recorded native data and the guard's rejection cases without opening a GUI.
    & (Join-Path $PSScriptRoot 'Test-VirtualKeyboardPerformance.ps1')
    & (Join-Path $PSScriptRoot 'Tests/Test-VirtualKeyboardPerformance.Tests.ps1')
    $markdownGuardTests = Join-Path $PSScriptRoot 'Tests/Test-MarkdownPerformance.Tests.ps1'
    if ($MarkdownGuardContractEvidence) {
        $MarkdownGuardContractEvidence = [IO.Path]::GetFullPath($MarkdownGuardContractEvidence)
        $markdownGuardProof = Get-Content -LiteralPath $MarkdownGuardContractEvidence -Raw | ConvertFrom-Json
        $markdownGuardHash = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Test-MarkdownPerformance.ps1')).Hash
        if ($markdownGuardProof.全部通过 -isnot [bool] -or -not $markdownGuardProof.全部通过 -or
            $markdownGuardProof.守卫SHA256 -ne $markdownGuardHash -or
            (Get-Item -LiteralPath $MarkdownGuardContractEvidence).LastWriteTimeUtc -lt (Get-Item -LiteralPath $markdownGuardTests).LastWriteTimeUtc) {
            throw 'Markdown guard contract evidence is failed or older than the current guard/test scripts.'
        }
        $markdownGuardContractReused = $true
        Write-Host "PASS: unchanged Markdown guard contracts, saved evidence: $MarkdownGuardContractEvidence"
    }
    else { & $markdownGuardTests }
    dotnet pack NT.AvaloniaControl.slnx -c Release --no-build --no-restore -m:1 -o artifacts/packages
    if ($LASTEXITCODE -ne 0) { throw 'Product pack failed.' }
    if (-not $SkipConsumer) {
        & (Join-Path $PSScriptRoot 'Test-PackageConsumer.ps1') -NoPack
        & (Join-Path $PSScriptRoot 'Test-TreeDataGridFunctionConsumer.ps1') -NoPack
    }
    $verificationFinalSources = Get-VerificationSourceManifest
    $verificationSourcesUnchanged = ($verificationSources | ConvertTo-Json -Depth 4 -Compress) -ceq
        ($verificationFinalSources | ConvertTo-Json -Depth 4 -Compress)
    $verificationSourceComparisonCompleted = $true
    if (-not $verificationSourcesUnchanged) { throw 'Product/test/validation sources changed during verification. Retain this run and verify the new frozen sources.' }
    $verificationPassed = $true
}
finally {
    [ordered]@{
        startedUtc = $verificationStarted; completedUtc = [DateTimeOffset]::UtcNow
        passed = $verificationPassed; consumerSkipped = [bool]$SkipConsumer
        markdownGuardContractReused = $markdownGuardContractReused
        markdownGuardContractEvidence = $MarkdownGuardContractEvidence
        testTotal = if ($verificationResults.Count -gt 0) { [int]($verificationResults | ForEach-Object { $_.total } | Measure-Object -Sum).Sum } else { $null }
        tests = $verificationResults
        markdownCoreSha256 = if (Test-Path 'src/NT.AvaloniaControl.Markdown/bin/Release/net10.0/NT.AvaloniaControl.Markdown.dll') { (Get-FileHash 'src/NT.AvaloniaControl.Markdown/bin/Release/net10.0/NT.AvaloniaControl.Markdown.dll' -Algorithm SHA256).Hash } else { $null }
        nativePerformanceIncluded = $false
        sourceManifest = Join-Path $verificationRun 'source-manifest.json'
        sourceManifestSha256 = if (Test-Path -LiteralPath (Join-Path $verificationRun 'source-manifest.json')) { (Get-FileHash -LiteralPath (Join-Path $verificationRun 'source-manifest.json') -Algorithm SHA256).Hash } else { $null }
        sourcesUnchangedDuringVerification = $verificationSourcesUnchanged
        sourceComparisonCompleted = $verificationSourceComparisonCompleted
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $verificationRun 'results.json') -Encoding utf8
    Write-Host "Verification evidence: $verificationRun"
    Pop-Location
}
