[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$CorpusCache = 'artifacts/pdfviewer/real-corpus-cache',
    [switch]$NoBuild,
    [switch]$SkipTests,
    [switch]$BackendOnly
)
$ErrorActionPreference = 'Stop'
if (-not [OperatingSystem]::IsWindows()) { throw 'Use Verify-PdfViewer-Linux.sh on Linux; this wrapper records Windows native evidence.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = 'artifacts/pdfviewer-native/windows-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') }
$output = [IO.Path]::GetFullPath($OutputDirectory, $repo)
if (Test-Path -LiteralPath $output) { throw 'Choose a fresh output directory; existing evidence is preserved.' }
$null = [IO.Directory]::CreateDirectory((Join-Path $output 'logs'))
$steps = [Collections.Generic.List[object]]::new()
$state = [ordered]@{ SchemaVersion = 1; StartedUtc = [DateTimeOffset]::UtcNow.ToString('O'); CompletedUtc = $null; Passed = $false; Failure = $null; BuildRequested = -not $NoBuild; BuildExecuted = $false; CoreAndHeadlessRequested = -not $SkipTests; CoreAndHeadlessExecuted = $false; WindowRequested = -not $BackendOnly; PhysicalDesktopAcceptance = $false; Steps = $steps }
function Save-Json($Value, [string]$Path) { $Value | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $Path -Encoding utf8 }
function Invoke-NativeChild([string]$Name, [string[]]$Arguments, [int]$TimeoutSeconds = 600) {
    $started = [DateTimeOffset]::UtcNow
    $info = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application).Source)
    $info.WorkingDirectory = $repo
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.CreateNoWindow = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $stdoutPath = Join-Path $output "logs/$Name.stdout.log"
    $stderrPath = Join-Path $output "logs/$Name.stderr.log"
    $timedOut = $false
    $exitCode = $null
    try {
        if (-not $process.Start()) { throw "Could not start owned process: $Name" }
        $processId = $process.Id
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            $process.Kill($true)
            if (-not $process.WaitForExit(10000)) { throw "Owned child did not exit after timeout: $Name" }
        }
        $exitCode = $process.ExitCode
        [IO.File]::WriteAllText($stdoutPath, $stdout.GetAwaiter().GetResult(), [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText($stderrPath, $stderr.GetAwaiter().GetResult(), [Text.UTF8Encoding]::new($false))
    }
    finally {
        $steps.Add([ordered]@{ Name = $Name; ProcessId = $processId; StartedUtc = $started.ToString('O'); CompletedUtc = [DateTimeOffset]::UtcNow.ToString('O'); Arguments = $Arguments; TimeoutSeconds = $TimeoutSeconds; TimedOut = $timedOut; ExitCode = $exitCode; Stdout = $stdoutPath; Stderr = $stderrPath })
        $process.Dispose()
    }
    if ($timedOut -or $exitCode -ne 0) { throw "$Name failed; exit=$exitCode, timeout=$timedOut. See $stdoutPath and $stderrPath." }
    Write-Output "PDF P1: $Name passed"
}
function Get-SourceIdentity {
    $roots = @('src/NT.AvaloniaControl.PdfViewer','src/NT.AvaloniaControl.PdfViewer.Themes','benchmarks/PdfViewer.P1','tests/PdfViewer.Core.Tests','tests/PdfViewer.Headless.Tests')
    $files = @($roots | ForEach-Object { Get-ChildItem -LiteralPath (Join-Path $repo $_) -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|__pycache__)[\\/]' -and $_.Extension -in @('.cs','.csproj','.axaml','.json') } })
    $files += @('Directory.Build.props','tests/Directory.Build.props','Directory.Packages.props','global.json','NuGet.Config','scripts/Test-PdfViewerNative.ps1','scripts/Verify-PdfViewer-Linux.sh','.github/workflows/pdfviewer-native.yml','benchmarks/PdfViewer.P0/Fixtures/real-corpus-sources.json') | ForEach-Object { Get-Item -LiteralPath (Join-Path $repo $_) }
    return @($files | Sort-Object FullName -Unique | ForEach-Object { [ordered]@{ Path = [IO.Path]::GetRelativePath($repo, $_.FullName).Replace('\','/'); SHA256 = (Get-FileHash -LiteralPath $_.FullName).Hash } })
}
try {
    $before = @(Get-SourceIdentity)
    Save-Json $before (Join-Path $output 'source-before.json')
    Save-Json ([ordered]@{ CapturedUtc = [DateTimeOffset]::UtcNow.ToString('O'); OS = [Runtime.InteropServices.RuntimeInformation]::OSDescription; ProcessArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString(); GitHubRunId = $env:GITHUB_RUN_ID; GitHubSha = $env:GITHUB_SHA; Boundary = 'Native PDFium and native Avalonia window smoke; no physical input, DPI matrix, printing, scan-out or official performance acceptance.' }) (Join-Path $output 'environment.json')
    Invoke-NativeChild 'dotnet-info' @('--info') 30
    $manifest = Get-Content -LiteralPath (Join-Path $repo 'benchmarks/PdfViewer.P0/Fixtures/real-corpus-sources.json') -Raw | ConvertFrom-Json
    $source = $manifest.Sources | Where-Object Id -EQ 'gnu-make'
    $null = [IO.Directory]::CreateDirectory((Join-Path $output 'corpus'))
    $published = Join-Path $output 'corpus/gnu-make.pdf'
    $cached = Join-Path ([IO.Path]::GetFullPath($CorpusCache, $repo)) $source.File
    if (Test-Path -LiteralPath $cached) { Copy-Item -LiteralPath $cached -Destination $published }
    else { Invoke-WebRequest -Uri $source.PdfUrl -OutFile $published -TimeoutSec 60 }
    if ((Get-Item -LiteralPath $published).Length -ne $source.Bytes -or (Get-FileHash -LiteralPath $published).Hash -cne $source.SHA256) { throw 'Published source differs from the frozen GNU make manual.' }
    Save-Json ([ordered]@{ Source = $source; Downloaded = -not (Test-Path -LiteralPath $cached); ProductPackageRedistribution = $false }) (Join-Path $output 'corpus/source.json')
    $project = 'benchmarks/PdfViewer.P1/PdfViewer.P1.csproj'
    if (-not $NoBuild) {
        Invoke-NativeChild 'restore' @('restore',$project,'--locked-mode','--disable-parallel','-m:1','-nr:false')
        Invoke-NativeChild 'build' @('build',$project,'-c','Release','--no-restore','-m:1','-nr:false','-v:minimal')
        $state.BuildExecuted = $true
    }
    $app = Join-Path $repo 'benchmarks/PdfViewer.P1/bin/Release/net10.0/PdfViewer.P1.dll'
    $common = @($app,'--real-pdf',$published,'--expected-sha',[string]$source.SHA256)
    Invoke-NativeChild 'backend' ($common + @('--output',(Join-Path $output 'backend'))) 120
    if (-not $BackendOnly) { Invoke-NativeChild 'native-window' ($common + @('--output',(Join-Path $output 'native-window'),'--window')) 120 }
    if (-not $SkipTests) {
        foreach ($suite in @('Core','Headless')) {
            $testProject = "tests/PdfViewer.$suite.Tests/PdfViewer.$suite.Tests.csproj"
            Invoke-NativeChild "restore-$suite" @('restore',$testProject,'--locked-mode','--disable-parallel','-m:1','-nr:false')
            Invoke-NativeChild "build-$suite" @('build',$testProject,'-c','Release','--no-restore','-m:1','-nr:false','-v:minimal')
            Invoke-NativeChild "test-$suite" @((Join-Path $repo "tests/PdfViewer.$suite.Tests/bin/Release/net10.0/PdfViewer.$suite.Tests.dll"),'-noLogo','-xml',(Join-Path $output "$suite.xml")) 180
        }
        $state.CoreAndHeadlessExecuted = $true
    }
    foreach ($name in @('backend') + $(if (-not $BackendOnly) { 'native-window' })) {
        $report = Get-Content -LiteralPath (Join-Path $output "$name/result.json") -Raw | ConvertFrom-Json
        if ($report.Passed -ne $true -or $report.Failure -or @($report.Checks.PSObject.Properties | Where-Object Value -NE $true).Count -gt 0) { throw "Incomplete native report: $name" }
        if (@($report.Observations.LoadedPdfium).Count -ne 1) { throw "Missing actual PDFium module identity: $name" }
        $loaded = $report.Observations.LoadedPdfium[0]
        if ((Get-FileHash -LiteralPath $loaded.Path).Hash -cne $loaded.SHA256) { throw 'Actually loaded native module bytes changed.' }
    }
    $after = @(Get-SourceIdentity)
    if (($before | ConvertTo-Json -Depth 5 -Compress) -cne ($after | ConvertTo-Json -Depth 5 -Compress)) { throw 'Source changed during native validation.' }
    Save-Json $after (Join-Path $output 'source-after.json')
    $state.Passed = $true
}
catch { $state.Failure = $_.Exception.ToString() }
finally {
    $state.CompletedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $state.Files = @(Get-ChildItem -LiteralPath $output -File -Recurse | ForEach-Object { [ordered]@{ Path = [IO.Path]::GetRelativePath($output, $_.FullName).Replace('\','/'); SHA256 = (Get-FileHash -LiteralPath $_.FullName).Hash; Bytes = $_.Length } })
    Save-Json $state (Join-Path $output 'run.json')
}
if (-not $state.Passed) { Write-Error "PDF native verification failed: $($state.Failure)" -ErrorAction Continue; exit 1 }
Write-Output "PDF P1 Windows evidence: $output"
