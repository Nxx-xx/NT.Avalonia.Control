[CmdletBinding()]
param(
    [ValidateSet('Headless', 'NativeSynthetic', 'XvfbSynthetic', 'DesktopSynthetic')][string]$Mode = 'Headless',
    [string]$OutputRoot = 'artifacts/charts-platform',
    [string]$RunId = ([DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [guid]::NewGuid().ToString('N')),
    [string]$RequestedBackend = 'platform-default',
    [switch]$IncludeC1,
    [switch]$SkipPackageConsumers,
    [ValidateRange(30, 3600)][int]$CommandTimeoutSeconds = 900,
    [ValidateRange(15, 600)][int]$SmokeTimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if ($RunId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,119}$') { throw 'RunId must be a single safe directory name.' }
$outputBase = if ([IO.Path]::IsPathRooted($OutputRoot)) { [IO.Path]::GetFullPath($OutputRoot) } else { [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot)) }
$runDirectory = Join-Path $outputBase $RunId
if (Test-Path -LiteralPath $runDirectory) { throw "Evidence directory already exists; choose a new RunId: $runDirectory" }
New-Item -ItemType Directory -Path $runDirectory | Out-Null
$started = [DateTimeOffset]::UtcNow
$processResults = [Collections.Generic.List[object]]::new()
$passed = $false
$failure = $null
$failureKind = 'failed'
$sourcesUnchanged = $false
$allManifestSourcesUnchanged = $false
$assembliesUnchanged = $false
$tests = $null
$c1 = [ordered]@{ requested = [bool]$IncludeC1; usage = 'pending-C1'; packageConsumers = 'pending-C1' }
$native = [ordered]@{ status = 'not-requested'; inputKind = 'none'; physicalInputValidated = $false; compositorPresentValidated = $false }
$sourceManifest = @()
$validatedInputManifest = @()
$assemblyManifest = @()

function Save-ChartsJson([string]$Name, $Value) {
    $Value | ConvertTo-Json -Depth 18 | Set-Content -LiteralPath (Join-Path $runDirectory $Name) -Encoding utf8
}
function Relative-ChartsPath([string]$Path) { [IO.Path]::GetRelativePath($repoRoot, $Path).Replace('\', '/') }
function Invoke-ChartsProcess {
    param([string]$Name, [string]$File, [string[]]$Arguments = @(), [int]$TimeoutSeconds = $CommandTimeoutSeconds, [switch]$AllowFailure)
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $File
    $info.WorkingDirectory = $repoRoot
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $processStarted = [DateTimeOffset]::UtcNow
    $timedOut = $false
    $processExit = $null
    $stdout = ''
    $stderr = ''
    $pidValue = $null
    try {
        if (-not $process.Start()) { throw "Cannot start $Name." }
        $pidValue = $process.Id
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $timedOut = $true
            # Only the process tree created by this invocation is supervised.
            $process.Kill($true)
            $process.WaitForExit()
        }
        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        $processExit = $process.ExitCode
    }
    catch { $stderr += "`n$($_.Exception.Message)" }
    finally { $process.Dispose() }
    $stdoutPath = Join-Path $runDirectory ($Name + '.stdout.log')
    $stderrPath = Join-Path $runDirectory ($Name + '.stderr.log')
    [IO.File]::WriteAllText($stdoutPath, $stdout)
    [IO.File]::WriteAllText($stderrPath, $stderr)
    $record = [ordered]@{
        name = $Name; executable = $File; arguments = @($Arguments); pid = $pidValue
        startedUtc = $processStarted.ToString('o'); completedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        exitCode = $processExit; timedOut = $timedOut; timeoutSeconds = $TimeoutSeconds
        stdout = Relative-ChartsPath $stdoutPath; stderr = Relative-ChartsPath $stderrPath
    }
    $processResults.Add($record)
    if (-not $AllowFailure -and ($timedOut -or $processExit -ne 0)) { throw "$Name failed or timed out; see $stderrPath" }
    [pscustomobject]@{ Record = $record; Output = $stdout; Error = $stderr }
}
function Get-ChartsSources {
    $paths = @(& rg --files --hidden src tests samples benchmarks scripts .github docs -g '!**/bin/**' -g '!**/obj/**')
    if ($LASTEXITCODE -ne 0) { throw 'Source enumeration requires rg and must succeed.' }
    $paths += @('Directory.Build.props', 'Directory.Packages.props', 'NuGet.Config', 'global.json', 'NT.AvaloniaControl.slnx', '.editorconfig')
    @($paths | Sort-Object -Unique | ForEach-Object {
        [ordered]@{ path = $_.Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath (Join-Path $repoRoot $_) -Algorithm SHA256).Hash }
    })
}
function Get-ChartsAssemblies {
    $paths = @(
        'src/NT.AvaloniaControl.Charts/bin/Release/net10.0/NT.AvaloniaControl.Charts.dll',
        'src/NT.AvaloniaControl.Charts.Themes/bin/Release/net10.0/NT.AvaloniaControl.Charts.Themes.dll',
        'tests/Charts.Headless.Tests/bin/Release/net10.0/Charts.Headless.Tests.dll',
        'tests/Charts.Headless.Tests/bin/Release/net10.0/Avalonia.Base.dll'
    )
    if ($Mode -ne 'Headless') {
        $paths += @('samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.dll',
            'samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.deps.json',
            'samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.runtimeconfig.json')
    }
    if ($IncludeC1) { $paths += 'samples/Charts.Usage/bin/Release/net10.0/Charts.Usage.dll' }
    @($paths | ForEach-Object {
        $full = Join-Path $repoRoot $_
        if (-not (Test-Path -LiteralPath $full)) { throw "Expected build artifact is missing: $_" }
        $version = if ([IO.Path]::GetExtension($full) -eq '.dll') { [Reflection.AssemblyName]::GetAssemblyName($full).Version.ToString() } else { $null }
        [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash; assemblyVersion = $version; bytes = (Get-Item -LiteralPath $full).Length }
    })
}
function Select-ChartsValidatedInputs($Manifest) {
    # The complete repository manifest is retained. Unrelated C0 docs/benchmark tools
    # may be developed concurrently; only actual build/test/harness inputs gate this run.
    @($Manifest | Where-Object {
        $_.path -match '^(src/|samples/NT\.AvaloniaControl\.Demo/|samples/Charts\.Usage/|scripts/Consumers/Charts/|tests/Charts\.Headless\.Tests/)' -or
        $_.path -in @('tests/Directory.Build.props', 'Directory.Build.props', 'Directory.Packages.props', 'NuGet.Config', 'global.json',
            'scripts/Verify-Charts.ps1', 'scripts/Verify-Charts-Linux.sh', 'scripts/Test-ChartsPackageConsumer.ps1', 'scripts/Capture-ChartsBaseline.ps1', '.github/workflows/charts.yml', '.editorconfig')
    })
}
Push-Location $repoRoot
try {
    $isWindowsHost = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)
    $isLinuxHost = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Linux)
    $sourceManifest = Get-ChartsSources
    Save-ChartsJson 'source-manifest.json' $sourceManifest
    $validatedInputManifest = Select-ChartsValidatedInputs $sourceManifest
    Save-ChartsJson 'validated-input-manifest.json' $validatedInputManifest
    $sdk = Invoke-ChartsProcess 'dotnet-info' 'dotnet' @('--info') -AllowFailure
    $git = Invoke-ChartsProcess 'git-revision' 'git' @('rev-parse', 'HEAD') -AllowFailure
    $osRelease = $null
    $windowsVersion = $null
    $fontInfo = $null
    $gpuInfo = $null
    $gpuUnavailableReason = 'No verified GPU identity or rendering backend was reported by the application.'
    if ($isWindowsHost) {
        try {
            $key = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
            $windowsVersion = [ordered]@{
                productNameRegistry = $key.ProductName; editionId = $key.EditionID; displayVersion = $key.DisplayVersion
                currentBuild = $key.CurrentBuildNumber; updateBuildRevision = $key.UBR
                marketingNameConfirmed = $false
                note = 'Kernel 10.0 and the registry ProductName alone do not establish a Windows 10 marketing release.'
            }
        } catch { $windowsVersion = @{ unavailableReason = $_.Exception.Message } }
        $fontDirectory = Join-Path $env:WINDIR 'Fonts'
        $fontInfo = @('segoeui.ttf', 'msyh.ttc', 'simsun.ttc', 'NotoSansCJK-Regular.ttc') | ForEach-Object {
            $path = Join-Path $fontDirectory $_
            if (Test-Path -LiteralPath $path) { @{ file = $_; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } }
        }
        if (Get-Command Get-CimInstance -ErrorAction SilentlyContinue) {
            try {
                $gpuInfo = @(Get-CimInstance Win32_VideoController -OperationTimeoutSec 5 | Select-Object Name, DriverVersion, AdapterCompatibility)
                if ($gpuInfo.Count -gt 0) { $gpuUnavailableReason = $null }
            } catch { $gpuUnavailableReason = $_.Exception.Message }
        }
    }
    if ($isLinuxHost) {
        $osRelease = if (Test-Path -LiteralPath '/etc/os-release') { [IO.File]::ReadAllText('/etc/os-release') } else { $null }
        if (Get-Command fc-list -ErrorAction SilentlyContinue) {
            $fonts = Invoke-ChartsProcess 'fonts-cjk' 'fc-list' @(':lang=zh', 'family', 'file') -TimeoutSeconds 15 -AllowFailure
            $fallback = Invoke-ChartsProcess 'font-fallback' 'fc-match' @('-f', '%{family}|%{file}\n', 'sans-serif') -TimeoutSeconds 15 -AllowFailure
            $fontInfo = @{ cjk = $fonts.Output.Trim(); fallback = $fallback.Output.Trim(); fileHashesCollected = $false; note = 'Font inventory only; shaping and fallback rendering are not validated.' }
        } else { $fontInfo = @{ unavailableReason = 'fontconfig tools are unavailable.' } }
        if (Get-Command lspci -ErrorAction SilentlyContinue) {
            $gpu = Invoke-ChartsProcess 'gpu-pci' 'lspci' @('-nn') -TimeoutSeconds 15 -AllowFailure
            $gpuInfo = @($gpu.Output -split "`n" | Where-Object { $_ -match 'VGA|3D controller|Display controller' })
            if ($gpuInfo.Count -gt 0) { $gpuUnavailableReason = $null }
        }
    }
    $environment = [ordered]@{
        schemaVersion = 1; runId = $RunId; collectedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        osDescription = [Runtime.InteropServices.RuntimeInformation]::OSDescription; osArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString(); logicalProcessorCount = [Environment]::ProcessorCount
        executionOs = if ($isWindowsHost) { 'windows' } elseif ($isLinuxHost) { 'linux' } else { 'other' }
        windowsVersion = $windowsVersion; linuxOsRelease = $osRelease; powershellVersion = $PSVersionTable.PSVersion.ToString()
        dotnetInfo = $sdk.Output; avaloniaRequestedVersion = ([xml](Get-Content Directory.Packages.props -Raw)).Project.ItemGroup.PackageVersion | Where-Object Include -eq 'Avalonia' | ForEach-Object Version
        gitRevision = if ($git.Record.exitCode -eq 0) { $git.Output.Trim() } else { $null }
        userInteractive = [Environment]::UserInteractive; mode = $Mode
        display = @{ requestedBackend = $RequestedBackend; actualBackend = $null; actualRenderer = $null; unavailableReason = 'Existing Charts smoke does not instrument its actual platform backend or renderer.'; display = $env:DISPLAY; waylandDisplay = $env:WAYLAND_DISPLAY; sessionType = $env:XDG_SESSION_TYPE; desktop = $env:XDG_CURRENT_DESKTOP; windowsSession = $env:SESSIONNAME; desktopSessionIndependentlyVerified = $false }
        fonts = $fontInfo; gpuInventory = $gpuInfo; gpuUnavailableReason = $gpuUnavailableReason
        physicalInputValidated = $false; desktopScreenshotValidated = $false; compositorPresentValidated = $false
    }
    Save-ChartsJson 'environment.json' $environment
    if (-not $isWindowsHost -and -not $isLinuxHost) { $failureKind = 'blocked-environment'; throw 'This C0 entry point supports Windows and Linux only.' }
    if ($Mode -eq 'XvfbSynthetic' -and (-not $isLinuxHost -or -not $env:DISPLAY -or $env:CHARTS_DISPLAY_KIND -ne 'xvfb')) {
        $failureKind = 'blocked-environment'; throw 'XvfbSynthetic requires Linux, DISPLAY and the Linux wrapper Xvfb marker.'
    }
    if ($Mode -in @('NativeSynthetic', 'DesktopSynthetic') -and $isLinuxHost -and -not $env:DISPLAY) {
        $failureKind = 'blocked-environment'; throw 'Current Avalonia 12.0.3 Linux native smoke requires an X11 DISPLAY (including XWayland).'
    }
    $projects = @('tests/Charts.Headless.Tests/Charts.Headless.Tests.csproj')
    if ($Mode -ne 'Headless') { $projects += 'samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj' }
    if ($IncludeC1) { $projects += 'samples/Charts.Usage/Charts.Usage.csproj' }
    for ($index = 0; $index -lt $projects.Count; $index++) {
        $null = Invoke-ChartsProcess "restore-$index" 'dotnet' @('restore', $projects[$index], '--locked-mode', '--disable-parallel', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false')
        $null = Invoke-ChartsProcess "build-$index" 'dotnet' @('build', $projects[$index], '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false')
    }
    $assemblyManifest = Get-ChartsAssemblies
    Save-ChartsJson 'assembly-manifest.json' $assemblyManifest
    $testXml = Join-Path $runDirectory 'Charts.Headless.Tests.xml'
    $tests = [ordered]@{ status = 'running'; inputKind = 'headless-injection'; result = Relative-ChartsPath $testXml }
    $testRun = Invoke-ChartsProcess 'charts-headless' 'dotnet' @((Join-Path $repoRoot 'tests/Charts.Headless.Tests/bin/Release/net10.0/Charts.Headless.Tests.dll'), '-noLogo', '-parallel', 'none', '-xml', $testXml)
    $testAssembly = ([xml](Get-Content -LiteralPath $testXml -Raw)).assemblies.assembly
    $tests = [ordered]@{ status = 'passed'; inputKind = 'headless-injection'; total = [int]$testAssembly.total; passed = [int]$testAssembly.passed; failed = [int]$testAssembly.failed; errors = [int]$testAssembly.errors; skipped = [int]$testAssembly.skipped; notRun = [int]$testAssembly.'not-run'; result = Relative-ChartsPath $testXml }
    if ($tests.total -le 0 -or $tests.passed -ne $tests.total -or $tests.failed -ne 0 -or $tests.errors -ne 0 -or $tests.skipped -ne 0 -or $tests.notRun -ne 0) { $tests.status = 'failed'; throw 'Headless XML does not contain a complete passing run.' }
    if ($Mode -ne 'Headless') {
        $native = [ordered]@{ status = 'running'; mode = $Mode; inputKind = 'synthetic-routed-pointer-and-programmatic-viewport'; physicalInputValidated = $false; compositorPresentValidated = $false }
        $nativeStarted = [DateTimeOffset]::UtcNow
        $nativeJson = Join-Path $runDirectory 'charts-smoke.json'
        $nativeImage = Join-Path $runDirectory 'charts-light.png'
        $nativeRun = Invoke-ChartsProcess 'charts-native' 'dotnet' @((Join-Path $repoRoot 'samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.dll'), '--charts-smoke', '--smoke-output', $nativeJson, '--smoke-image', $nativeImage) -TimeoutSeconds $SmokeTimeoutSeconds
        $smoke = Get-Content -LiteralPath $nativeJson -Raw | ConvertFrom-Json
        $checks = @($smoke.'检查'.PSObject.Properties)
        $images = @($smoke.PSObject.Properties | Where-Object { $_.Name -like '*图片' } | ForEach-Object Value)
        if ($smoke.'通过' -isnot [bool] -or -not $smoke.'通过' -or $checks.Count -eq 0 -or @($checks | Where-Object { $_.Value -isnot [bool] -or -not $_.Value }).Count -gt 0 -or $smoke.'错误' -or [DateTimeOffset]$smoke.'时间UTC' -lt $nativeStarted -or $images.Count -lt 2) { throw 'Native smoke report is missing, stale, incomplete or failed.' }
        foreach ($imagePath in $images) {
            if (-not (Test-Path -LiteralPath $imagePath) -or -not ([IO.Path]::GetFullPath($imagePath).StartsWith($runDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) { throw 'Native image is missing or outside this evidence run.' }
        }
        $native = [ordered]@{
            status = 'passed'; mode = $Mode; inputKind = 'synthetic-routed-pointer-and-programmatic-viewport'; checks = $checks.Count
            report = Relative-ChartsPath $nativeJson; images = @($images | ForEach-Object { Relative-ChartsPath $_ })
            nativeWindowCreated = $smoke.'检查'.'原生窗口已创建'; imageKind = 'RenderTargetBitmap-of-native-window'
            physicalInputValidated = $false; desktopScreenshotValidated = $false; compositorPresentValidated = $false
        }
    }
    if ($IncludeC1) {
        $c1.usage = [ordered]@{ status = 'build-passed'; execution = 'not-requested-headless-mode'; physicalInputValidated = $false }
        if ($Mode -ne 'Headless') {
            $usagePath = Join-Path $runDirectory 'charts-usage.json'
            $null = Invoke-ChartsProcess 'charts-usage-native' 'dotnet' @((Join-Path $repoRoot 'samples/Charts.Usage/bin/Release/net10.0/Charts.Usage.dll'), '--smoke', '--smoke-output', $usagePath) -TimeoutSeconds $SmokeTimeoutSeconds
            $usageResult = Get-Content -LiteralPath $usagePath -Raw | ConvertFrom-Json
            if ($usageResult.passed -isnot [bool] -or -not $usageResult.passed -or @($usageResult.checks.PSObject.Properties).Count -eq 0 -or @($usageResult.checks.PSObject.Properties | Where-Object { $_.Value -isnot [bool] -or -not $_.Value }).Count -gt 0) { throw 'Native Charts.Usage bindings did not pass.' }
            $c1.usage = @{ status = 'passed'; report = Relative-ChartsPath $usagePath; execution = 'native-window-programmatic-binding-checks'; physicalInputValidated = $false }
        }
        if (-not $SkipPackageConsumers) {
            $consumerRoot = Join-Path $runDirectory 'package-consumers'
            $pwsh = (Get-Command pwsh -ErrorAction Stop).Source
            $null = Invoke-ChartsProcess 'charts-package-consumers' $pwsh @('-NoLogo', '-NoProfile', '-File', (Join-Path $repoRoot 'scripts/Test-ChartsPackageConsumer.ps1'), '-OutputRoot', $consumerRoot) -TimeoutSeconds ([Math]::Max($CommandTimeoutSeconds, 1200))
            $consumerReports = @(Get-ChildItem -LiteralPath $consumerRoot -Directory | ForEach-Object { Join-Path $_.FullName 'results.json' } | Where-Object { Test-Path -LiteralPath $_ })
            if ($consumerReports.Count -ne 1) { throw 'Expected exactly one immutable package consumer run.' }
            $consumerResult = Get-Content -LiteralPath $consumerReports[0] -Raw | ConvertFrom-Json
            if ($consumerResult.passed -isnot [bool] -or -not $consumerResult.passed -or @($consumerResult.consumers).Count -ne 2) { throw 'The two isolated package consumer variants did not pass.' }
            $c1.packageConsumers = @{ status = 'passed'; report = Relative-ChartsPath $consumerReports[0]; execution = 'headless-package-contracts'; nativeWindowValidated = $false }
        } else { $c1.packageConsumers = @{ status = 'explicitly-skipped'; reason = '-SkipPackageConsumers supplied; this run cannot claim package consumer acceptance.' } }
    }
    $finalSourceManifest = Get-ChartsSources
    Save-ChartsJson 'source-manifest-final.json' $finalSourceManifest
    $allManifestSourcesUnchanged = ($sourceManifest | ConvertTo-Json -Depth 4 -Compress) -ceq ($finalSourceManifest | ConvertTo-Json -Depth 4 -Compress)
    $sourcesUnchanged = ($validatedInputManifest | ConvertTo-Json -Depth 4 -Compress) -ceq ((Select-ChartsValidatedInputs $finalSourceManifest) | ConvertTo-Json -Depth 4 -Compress)
    $assembliesUnchanged = ($assemblyManifest | ConvertTo-Json -Depth 4 -Compress) -ceq ((Get-ChartsAssemblies) | ConvertTo-Json -Depth 4 -Compress)
    if (-not $sourcesUnchanged -or -not $assembliesUnchanged) { throw 'Source or validated assemblies changed during this run; retain evidence and rerun frozen inputs.' }
    $passed = $true
}
catch {
    $failure = $_.Exception.ToString()
    if ($tests -and $tests.status -eq 'running') { $tests.status = 'failed' }
    if ($native.status -eq 'running') { $native.status = 'failed' }
}
finally {
    $evidenceFiles = @(Get-ChildItem -LiteralPath $runDirectory -File | Sort-Object Name | ForEach-Object { @{ path = $_.Name; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; bytes = $_.Length } })
    Save-ChartsJson 'evidence-manifest.json' $evidenceFiles
    Save-ChartsJson 'results.json' ([ordered]@{
        schemaVersion = 1; runId = $RunId; startedUtc = $started.ToString('o'); completedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        passed = $passed; status = if ($passed) { 'passed' } else { $failureKind }; requestedMode = $Mode
        acceptanceScope = if ($Mode -eq 'Headless') { 'contracts-headless-only' } else { 'contracts-and-native-programmatic-smoke' }
        sourceManifest = 'source-manifest.json'; assemblyManifest = if ($assemblyManifest.Count -gt 0) { 'assembly-manifest.json' } else { $null }
        evidenceManifest = 'evidence-manifest.json'; sourceManifestSha256 = if (Test-Path (Join-Path $runDirectory 'source-manifest.json')) { (Get-FileHash (Join-Path $runDirectory 'source-manifest.json') -Algorithm SHA256).Hash } else { $null }
        validatedInputManifest = 'validated-input-manifest.json'; validatedInputSourcesUnchangedDuringRun = $sourcesUnchanged
        sourceStabilityScope = 'All src, Charts tests, Demo/Charts.Usage, consumer templates, shared build configuration and Charts verification harness; full repository manifests retained separately.'
        allManifestSourcesUnchangedDuringRun = $allManifestSourcesUnchanged; assembliesUnchangedDuringRun = $assembliesUnchanged
        environment = if (Test-Path (Join-Path $runDirectory 'environment.json')) { 'environment.json' } else { $null }
        tests = $tests; native = $native; processes = $processResults.ToArray(); error = $failure
        usageAndPackageConsumer = $c1; officialReferenceIncluded = $false; performanceIncluded = $false; physicalInputValidated = $false; linuxDesktopCertified = $false
        historicalEvidenceReused = $false
    })
    Pop-Location
    Write-Output "Charts evidence: $runDirectory"
}
if (-not $passed) { throw "Charts verification did not pass ($failureKind). $failure" }
