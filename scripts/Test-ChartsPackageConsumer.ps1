[CmdletBinding()]
param([switch]$NoPack, [string]$Feed, [string]$OutputRoot = 'artifacts/charts-consumer', [int]$TimeoutSeconds = 600)
$ErrorActionPreference = 'Stop'
$chartsRepo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$chartsRunId = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [guid]::NewGuid().ToString('N')
$chartsOutput = if ([IO.Path]::IsPathRooted($OutputRoot)) { Join-Path $OutputRoot $chartsRunId } else { Join-Path (Join-Path $chartsRepo $OutputRoot) $chartsRunId }
if (Test-Path -LiteralPath $chartsOutput) { throw 'Consumer run directory already exists.' }
New-Item -ItemType Directory -Path $chartsOutput | Out-Null
$chartsStarted = [DateTimeOffset]::UtcNow
$chartsProcesses = [Collections.Generic.List[object]]::new()
$chartsResults = [Collections.Generic.List[object]]::new()
$chartsPassed = $false
$chartsFailure = $null

function Invoke-ChartsConsumerProcess([string]$Name, [string[]]$Arguments, [string]$WorkingDirectory, [hashtable]$ChildEnvironment = @{}) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = 'dotnet'; $info.WorkingDirectory = $WorkingDirectory
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    foreach ($entry in $ChildEnvironment.GetEnumerator()) { $info.Environment[$entry.Key] = $entry.Value }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    $started = [DateTimeOffset]::UtcNow
    $exitCode = $null; $timedOut = $false; $stdout = ''; $stderr = ''
    try {
        if (-not $process.Start()) { throw "Cannot start $Name." }
        $outputTask = $process.StandardOutput.ReadToEndAsync(); $errorTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { $timedOut = $true; $process.Kill($true); $process.WaitForExit() }
        $stdout = $outputTask.GetAwaiter().GetResult(); $stderr = $errorTask.GetAwaiter().GetResult(); $exitCode = $process.ExitCode
    } catch { $stderr += $_.Exception.ToString() } finally { $process.Dispose() }
    [IO.File]::WriteAllText((Join-Path $chartsOutput "$Name.stdout.log"), $stdout)
    [IO.File]::WriteAllText((Join-Path $chartsOutput "$Name.stderr.log"), $stderr)
    $chartsProcesses.Add([ordered]@{ name = $Name; arguments = $Arguments; workingDirectory = $WorkingDirectory; startedUtc = $started.ToString('o'); completedUtc = [DateTimeOffset]::UtcNow.ToString('o'); exitCode = $exitCode; timedOut = $timedOut })
    if ($exitCode -ne 0 -or $timedOut) { throw "$Name did not pass. See $chartsOutput/$Name.stderr.log" }
}
function Get-ChartsConsumerSources {
    $paths = @(& rg --files src/NT.AvaloniaControl.Charts src/NT.AvaloniaControl.Charts.Themes samples/Charts.Usage scripts/Consumers/Charts -g '!**/bin/**' -g '!**/obj/**') + @('scripts/Test-ChartsPackageConsumer.ps1', 'Directory.Build.props', 'Directory.Packages.props', 'global.json')
    if ($LASTEXITCODE -ne 0) { throw 'Consumer source enumeration failed.' }
    @($paths | Sort-Object -Unique | ForEach-Object { [ordered]@{ path = $_.Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath (Join-Path $chartsRepo $_) -Algorithm SHA256).Hash } })
}
Push-Location $chartsRepo
try {
    $chartsSourceManifest = Get-ChartsConsumerSources
    $chartsSourceManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $chartsOutput 'source-manifest.json') -Encoding utf8
    $chartsVersion = [string]([xml](Get-Content Directory.Build.props -Raw)).Project.PropertyGroup.Version
    $chartsAvaloniaVersion = [string](([xml](Get-Content Directory.Packages.props -Raw)).Project.ItemGroup.PackageVersion | Where-Object Include -eq 'Avalonia').Version
    $chartsFeed = if ($Feed) { [IO.Path]::GetFullPath($Feed) } elseif ($NoPack) { Join-Path $chartsRepo 'artifacts/packages' } else { Join-Path $chartsOutput 'feed' }
    if (-not $NoPack) {
        New-Item -ItemType Directory -Path $chartsFeed -Force | Out-Null
        foreach ($project in @('NT.AvaloniaControl.Charts', 'NT.AvaloniaControl.Charts.Themes')) {
            Invoke-ChartsConsumerProcess "pack-$project" @('pack', "src/$project/$project.csproj", '-c', 'Release', '--no-build', '--no-restore', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false', '-o', $chartsFeed) $chartsRepo
        }
    }
    $chartsPackages = @('NT.AvaloniaControl.Charts', 'NT.AvaloniaControl.Charts.Themes') | ForEach-Object {
        $path = Join-Path $chartsFeed "$($_).$chartsVersion.nupkg"
        if (-not (Test-Path -LiteralPath $path)) { throw "Expected package is missing: $path" }
        @{ id = $_; version = $chartsVersion; path = $path; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    }
    $corePackage = $chartsPackages | Where-Object id -eq 'NT.AvaloniaControl.Charts'
    $archive = [IO.Compression.ZipFile]::OpenRead($corePackage.path)
    try {
        $payload = $archive.GetEntry('lib/net10.0/NT.AvaloniaControl.Charts.dll')
        if (-not $payload) { throw 'The core nupkg contains no net10.0 assembly.' }
        $payloadStream = $payload.Open()
        try { $chartsCorePayloadSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($payloadStream)) } finally { $payloadStream.Dispose() }
    } finally { $archive.Dispose() }
    foreach ($kind in @('function', 'themes')) {
        $consumer = Join-Path $chartsOutput $kind
        New-Item -ItemType Directory -Path $consumer | Out-Null
        # Stop repository build settings, central versions and ambient package caches.
        '<Project />' | Set-Content -LiteralPath (Join-Path $consumer 'Directory.Build.props') -Encoding utf8
        '<Project />' | Set-Content -LiteralPath (Join-Path $consumer 'Directory.Build.targets') -Encoding utf8
        '<Project />' | Set-Content -LiteralPath (Join-Path $consumer 'Directory.Packages.props') -Encoding utf8
        $packageId = if ($kind -eq 'themes') { 'NT.AvaloniaControl.Charts.Themes' } else { 'NT.AvaloniaControl.Charts' }
        $symbol = if ($kind -eq 'themes') { 'CHARTS_THEME_CONSUMER' } else { 'CHARTS_FUNCTION_CONSUMER' }
        (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Consumers/Charts/Consumer.csproj.template') -Raw).Replace('__PRODUCT_PACKAGE__', $packageId).Replace('__PRODUCT_VERSION__', $chartsVersion).Replace('__AVALONIA_VERSION__', $chartsAvaloniaVersion).Replace('__CONSUMER_SYMBOL__', $symbol) | Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj') -Encoding utf8
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Consumers/Charts/Program.cs') -Destination (Join-Path $consumer 'Program.cs')
        foreach ($source in @('MainWindow.axaml', 'MainWindow.axaml.cs', 'MainWindow.BarComparison.cs', 'MainWindow.ScatterBubble.cs', 'MainWindow.Circular.cs', 'MainWindow.Polar.cs', 'MainWindow.GaugeMicro.cs', 'MainWindow.Analysis.cs', 'MainWindow.Statistics.cs', 'MainWindow.Financial.cs', 'MainWindow.TechnicalTrends.cs', 'MainWindow.Engineering.cs', 'MainWindow.AdvancedComparison.cs', 'MainWindow.Hierarchy.cs', 'MainWindow.Relations.cs', 'MainWindow.Maps.cs', 'MainWindow.C6Completion.cs', 'MainWindow.C5Completion.cs', 'MainWindow.LevelOfDetail.cs', 'MainWindow.EfficientHit.cs', 'MainWindow.IncrementalStreaming.cs', 'MainWindow.RenderResources.cs', 'MainWindow.MultiChart.cs', 'ChartUsageViewModel.cs', 'ChartUsageViewModel.BarComparison.cs', 'ChartUsageViewModel.ScatterBubble.cs', 'ChartUsageViewModel.Circular.cs', 'ChartUsageViewModel.Polar.cs', 'ChartUsageViewModel.GaugeMicro.cs', 'ChartUsageViewModel.Analysis.cs', 'ChartUsageViewModel.Statistics.cs', 'ChartUsageViewModel.Financial.cs', 'ChartUsageViewModel.TechnicalTrends.cs', 'ChartUsageViewModel.Engineering.cs', 'ChartUsageViewModel.AdvancedComparison.cs', 'ChartUsageViewModel.Hierarchy.cs', 'ChartUsageViewModel.Relations.cs', 'ChartUsageViewModel.Maps.cs', 'ChartUsageViewModel.C6Completion.cs', 'ChartUsageViewModel.C5Completion.cs', 'ChartUsageViewModel.LevelOfDetail.cs', 'ChartUsageViewModel.EfficientHit.cs', 'ChartUsageViewModel.IncrementalStreaming.cs', 'ChartUsageViewModel.RenderResources.cs', 'ChartUsageViewModel.MultiChart.cs')) { Copy-Item -LiteralPath (Join-Path $chartsRepo "samples/Charts.Usage/$source") -Destination (Join-Path $consumer $source) }
        $escapedFeed = [Security.SecurityElement]::Escape($chartsFeed)
        "<configuration><packageSources><clear /><add key='local' value='$escapedFeed' /><add key='nuget.org' value='https://api.nuget.org/v3/index.json' /></packageSources><fallbackPackageFolders><clear /></fallbackPackageFolders></configuration>" | Set-Content -LiteralPath (Join-Path $consumer 'NuGet.Config') -Encoding utf8
        $packages = Join-Path $consumer 'packages'
        $childEnvironment = @{ NUGET_PACKAGES = $packages; NUGET_HTTP_CACHE_PATH = (Join-Path $consumer 'http-cache'); DOTNET_CLI_HOME = (Join-Path $consumer 'dotnet-home'); DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1' }
        if (Test-Path -LiteralPath $packages) { throw 'Each consumer must begin with a fresh package directory.' }
        Invoke-ChartsConsumerProcess "$kind-restore" @('restore', 'Consumer.csproj', '--configfile', 'NuGet.Config', '--packages', $packages, '--disable-parallel', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false') $consumer $childEnvironment
        Invoke-ChartsConsumerProcess "$kind-locked-restore" @('restore', 'Consumer.csproj', '--locked-mode', '--configfile', 'NuGet.Config', '--packages', $packages, '--disable-parallel', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false') $consumer $childEnvironment
        Invoke-ChartsConsumerProcess "$kind-build" @('build', 'Consumer.csproj', '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false') $consumer $childEnvironment
        $assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json
        if (@($assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'project' }).Count -ne 0) { throw 'The isolated consumer must contain no source project references.' }
        $resultPath = Join-Path $consumer 'consumer-result.json'
        Invoke-ChartsConsumerProcess "$kind-run" @((Join-Path $consumer 'bin/Release/net10.0/Consumer.dll'), $resultPath) $consumer $childEnvironment
        $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        if ($result.passed -isnot [bool] -or -not $result.passed -or @($result.checks.PSObject.Properties).Count -eq 0 -or @($result.checks.PSObject.Properties | Where-Object Value -ne $true).Count -gt 0) { throw 'Consumer result is incomplete or failed.' }
        if ($result.coreAssemblySha256 -ne $chartsCorePayloadSha256) { throw 'The executed core assembly does not match the recorded local nupkg payload.' }
        $chartsResults.Add(@{ kind = $kind; status = 'passed'; freshPackageCache = $packages; noProjectReferences = $true; result = $resultPath; resultSha256 = (Get-FileHash -LiteralPath $resultPath -Algorithm SHA256).Hash; lockFileSha256 = (Get-FileHash -LiteralPath (Join-Path $consumer 'packages.lock.json') -Algorithm SHA256).Hash })
    }
    $chartsFinalSources = Get-ChartsConsumerSources
    $chartsSourcesUnchanged = ($chartsSourceManifest | ConvertTo-Json -Depth 5 -Compress) -ceq ($chartsFinalSources | ConvertTo-Json -Depth 5 -Compress)
    if (-not $chartsSourcesUnchanged) { throw 'Consumer inputs changed during verification.' }
    $chartsPassed = $true
}
catch { $chartsFailure = $_.Exception.ToString() }
finally {
    @{ schemaVersion = 1; runId = $chartsRunId; startedUtc = $chartsStarted.ToString('o'); completedUtc = [DateTimeOffset]::UtcNow.ToString('o'); passed = $chartsPassed; evidenceKind = 'isolated-nupkg-headless-consumers'; nativeWindowValidated = $false; physicalInputValidated = $false; performanceMeasured = $false; packages = $chartsPackages; consumers = $chartsResults.ToArray(); processes = $chartsProcesses.ToArray(); sourcesUnchangedDuringRun = [bool]$chartsSourcesUnchanged; buildProvenance = 'Release product assemblies must be built by the caller; this script packs without building and records exact package hashes.'; error = $chartsFailure } | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $chartsOutput 'results.json') -Encoding utf8
    Pop-Location
    Write-Output "Charts package consumer evidence: $chartsOutput"
}
if (-not $chartsPassed) { throw "Charts package consumers failed. $chartsFailure" }
