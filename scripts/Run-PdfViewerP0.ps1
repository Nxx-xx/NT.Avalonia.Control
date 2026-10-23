param(
    [string]$OutputDirectory = ('artifacts/pdfviewer/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-p0'),
    [string]$PythonPath = 'python',
    [string]$OpenSslPath = 'openssl',
    [string]$PdfToPpmPath = 'pdftoppm',
    [string]$FontToolsPythonPath = 'python',
    [string]$FontSourceCache,
    [string]$RealCorpusSourceCache = 'artifacts/pdfviewer/real-corpus-cache',
    [switch]$IncludeProductionWriter,
    [switch]$IncludeProductionRedaction,
    [switch]$IncludeFontSubsetting,
    [switch]$IncludeAdvancedForms,
    [switch]$IncludePartialRedaction,
    [switch]$IncludeRealCorpus
)
$ErrorActionPreference = 'Stop'
if ($IncludePartialRedaction) { $IncludeProductionRedaction = [switch]::new($true) }
if ($IncludeAdvancedForms) { $IncludeFontSubsetting = [switch]::new($true) }
if ($IncludeFontSubsetting) { $IncludeProductionRedaction = [switch]::new($true) }
if ($IncludeProductionRedaction) { $IncludeProductionWriter = [switch]::new($true) }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$run = [IO.Path]::GetFullPath($OutputDirectory, $repo)
[IO.Directory]::CreateDirectory($run) | Out-Null
$logs = Join-Path $run 'logs'
[IO.Directory]::CreateDirectory($logs) | Out-Null
$steps = [Collections.Generic.List[object]]::new()
function Invoke-P0Command([string]$Name, [string]$Executable, [string[]]$Arguments) {
    $started = [DateTimeOffset]::UtcNow
    $log = Join-Path $logs ($Name + '.log')
    & $Executable @Arguments *> $log
    $code = $LASTEXITCODE
    $steps.Add([ordered]@{ Name = $Name; StartedUtc = $started.ToString('o'); DurationMs = ([DateTimeOffset]::UtcNow - $started).TotalMilliseconds; ExitCode = $code; Log = $log; LogSHA256 = (Get-FileHash -LiteralPath $log).Hash })
    if ($code -ne 0) { throw "$Name failed ($code). See $log" }
    Write-Output "$Name passed"
}
Push-Location -LiteralPath $repo
try {
    foreach ($file in @('p0-run.json','p0-run-failure.json','independent-verification.json')) {
        $path = Join-Path $run $file
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
    Invoke-P0Command 'restore' 'dotnet' @('restore','benchmarks/PdfViewer.P0/PdfViewer.P0.csproj','--locked-mode','--disable-parallel','-m:1','-nr:false','-p:NuGetAudit=false')
    & (Join-Path $PSScriptRoot 'Freeze-PdfViewerBaseline.ps1') -OutputDirectory (Join-Path $run 'baseline') -DownloadOfficialDocuments
    $fontDirectory = Join-Path $run 'fonts'
    [IO.Directory]::CreateDirectory($fontDirectory) | Out-Null
    foreach ($file in @('P0AnnotationCjk.ttf','Noto-CJK-COPYRIGHT.txt','font-provenance.json')) {
        Copy-Item -LiteralPath (Join-Path $repo "benchmarks/PdfViewer.P0/Fixtures/$file") -Destination (Join-Path $fontDirectory $file) -Force
    }
    Invoke-P0Command 'build' 'dotnet' @('build','benchmarks/PdfViewer.P0/PdfViewer.P0.csproj','-c','Release','--no-restore','-m:1','-nr:false','-v:minimal')
    $dll = Join-Path $repo 'benchmarks/PdfViewer.P0/bin/Release/net10.0/PdfViewer.P0.dll'
    $sourceFiles = @(& rg --files benchmarks/PdfViewer.P0 | Where-Object { $_ -notmatch '(^|[\\/])(bin|obj|__pycache__)[\\/]|\.py[co]$' })
    $sourceFiles += @('scripts/Freeze-PdfViewerBaseline.ps1','scripts/Run-PdfViewerP0.ps1','tests/PdfViewer.Core.Tests/PdfFixture.cs')
    [ordered]@{ CapturedAfterBuildUtc = [DateTimeOffset]::UtcNow.ToString('o'); AssemblySHA256 = (Get-FileHash -LiteralPath $dll).Hash; Files = @($sourceFiles | Sort-Object -Unique | ForEach-Object { [ordered]@{ Path = $_.Replace('\','/'); SHA256 = (Get-FileHash -LiteralPath (Join-Path $repo $_)).Hash } }) } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'execution-manifest.json') -Encoding utf8
    Invoke-P0Command 'prepare-fixtures' $PythonPath @('benchmarks/PdfViewer.P0/prepare_fixtures.py','--output',(Join-Path $run 'fixtures'))
    if ($IncludeFontSubsetting -or $IncludePartialRedaction) {
        $fontArguments = @('benchmarks/PdfViewer.P0/prepare_font_subset_corpus.py','--output',$run)
        if ($FontSourceCache) { $fontArguments += @('--source-cache',[IO.Path]::GetFullPath($FontSourceCache, $repo)) }
        Invoke-P0Command 'prepare-font-subset-corpus' $FontToolsPythonPath $fontArguments
    }
    if ($IncludeRealCorpus) {
        $corpusArguments = @('benchmarks/PdfViewer.P0/prepare_real_corpus.py','--output',$run)
        if ($RealCorpusSourceCache) { $corpusArguments += @('--source-cache',[IO.Path]::GetFullPath($RealCorpusSourceCache, $repo)) }
        Invoke-P0Command 'prepare-real-corpus' $PythonPath $corpusArguments
    }
    $probes = @('save','fonts','forms','redaction','baseline')
    if ($IncludeProductionWriter) { $probes += @('production','redaction-advanced') }
    if ($IncludeProductionRedaction) { $probes += 'production-redaction' }
    if ($IncludeFontSubsetting) { $probes += 'font-subset' }
    if ($IncludeAdvancedForms) { $probes += @('forms-advanced','cjk-forms') }
    if ($IncludePartialRedaction) { $probes += @('cjk-cluster-redaction','partial-image-redaction') }
    if ($IncludeRealCorpus) { $probes += 'real-corpus' }
    foreach ($probe in $probes) {
        Invoke-P0Command $probe 'dotnet' @($dll,'--probe',$probe,'--output',$run)
    }
    Invoke-P0Command 'font-bridge' $PythonPath @('benchmarks/PdfViewer.P0/complete_annotation_font_bridge.py',$fontDirectory)
    Invoke-P0Command 'redaction-independent' $PythonPath @('benchmarks/PdfViewer.P0/verify_redaction.py','--output',$run,'--bridge-hidden')
    Invoke-P0Command 'independent' $PythonPath @('benchmarks/PdfViewer.P0/verify_p0.py','--run',$run,'--repo',$repo,'--openssl',$OpenSslPath,'--poppler',$PdfToPpmPath)
    if ($IncludeProductionWriter) {
        Invoke-P0Command 'production-independent' $PythonPath @('benchmarks/PdfViewer.P0/verify_production_writer.py','--run',$run,'--renderer',$PdfToPpmPath,'--self-test')
        Invoke-P0Command 'redaction-advanced-independent' $PythonPath @('benchmarks/PdfViewer.P0/verify_redaction_advanced.py','--output',$run,'--renderer',$PdfToPpmPath,'--self-test')
    }
    if ($IncludeProductionRedaction) {
        Invoke-P0Command 'production-redaction-independent' $PythonPath @('benchmarks/PdfViewer.P0/verify_production_redaction.py','--run',$run,'--renderer',$PdfToPpmPath,'--self-test')
    }
    if ($IncludeFontSubsetting) {
        Invoke-P0Command 'font-subset-independent' $PythonPath @('benchmarks/PdfViewer.P0/verify_font_subset.py','--run',$run,'--renderer',$PdfToPpmPath,'--font-python',$FontToolsPythonPath,'--self-test')
    }
    if ($IncludeAdvancedForms) {
        Invoke-P0Command 'forms-advanced-independent' $PythonPath @('benchmarks/PdfViewer.P0/verify_forms_advanced.py','--run',$run,'--renderer',$PdfToPpmPath,'--self-test')
        Invoke-P0Command 'cjk-forms-independent' $PythonPath @('benchmarks/PdfViewer.P0/verify_cjk_forms.py','--run',$run,'--renderer',$PdfToPpmPath,'--font-python',$FontToolsPythonPath,'--self-test')
    }
    if ($IncludePartialRedaction) {
        Invoke-P0Command 'partial-redaction-independent' $PythonPath @('benchmarks/PdfViewer.P0/verify_partial_redaction.py','--run',$run,'--renderer',$PdfToPpmPath,'--font-python',$FontToolsPythonPath,'--self-test')
    }
    if ($IncludeRealCorpus) {
        Invoke-P0Command 'real-corpus-independent' $PythonPath @('benchmarks/PdfViewer.P0/verify_real_corpus.py','--run',$run,'--renderer',$PdfToPpmPath,'--self-test')
    }
    foreach ($suite in @('Core','Headless')) {
        Invoke-P0Command ('restore-tests-' + $suite) 'dotnet' @('restore',"tests/PdfViewer.$suite.Tests/PdfViewer.$suite.Tests.csproj",'--locked-mode','--disable-parallel','-m:1','-nr:false','-p:NuGetAudit=false')
        Invoke-P0Command ('build-tests-' + $suite) 'dotnet' @('build',"tests/PdfViewer.$suite.Tests/PdfViewer.$suite.Tests.csproj",'-c','Release','--no-restore','-m:1','-nr:false','-v:minimal')
        # xUnit v3 assemblies are executable. The in-process runner avoids a
        # separate VSTest transport while preserving failures and XML results.
        $testDll = Join-Path $repo "tests/PdfViewer.$suite.Tests/bin/Release/net10.0/PdfViewer.$suite.Tests.dll"
        Invoke-P0Command ('regression-' + $suite) 'dotnet' @($testDll,'-noLogo','-xml',(Join-Path $logs ("regression-$suite.xml")))
    }
    [ordered]@{ CompletedUtc = [DateTimeOffset]::UtcNow.ToString('o'); Status = 'experiments-verified-with-open-release-gates'; G0Complete = $false; ProductEditingReady = $false; BoundedFreeTextBackendImplemented = $IncludeProductionWriter.IsPresent; BoundedSanitizationAndRedactionImplemented = $IncludeProductionRedaction.IsPresent; AutomaticTrueTypeSubsettingImplemented = $IncludeFontSubsetting.IsPresent; AdvancedAcroFormFeasibilityExecuted = $IncludeAdvancedForms.IsPresent; PartialRedactionFeasibilityExecuted = $IncludePartialRedaction.IsPresent; RealCorpusFeasibilityExecuted = $IncludeRealCorpus.IsPresent; ProductionFormsImplemented = $false; ProductionPartialRedactionImplemented = $false; Steps = $steps.ToArray() } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'p0-run.json') -Encoding utf8
    Write-Output "P0 experiment evidence: $run"
} catch {
    [ordered]@{ FailedUtc = [DateTimeOffset]::UtcNow.ToString('o'); Status = 'failed'; Error = $_.Exception.Message; Steps = $steps.ToArray() } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'p0-run-failure.json') -Encoding utf8
    throw
} finally { Pop-Location }
