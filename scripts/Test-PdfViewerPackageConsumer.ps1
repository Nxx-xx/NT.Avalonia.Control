param([switch]$NoPack, [string]$PackageFeed)
$ErrorActionPreference = 'Stop'
$pdfRepoRoot = Split-Path $PSScriptRoot -Parent
$pdfFeed = if ([string]::IsNullOrWhiteSpace($PackageFeed)) { Join-Path $pdfRepoRoot 'artifacts/packages' } else { [IO.Path]::GetFullPath($PackageFeed) }
$pdfFixtureRoot = Join-Path $PSScriptRoot 'Consumers/PdfViewer'
$pdfWriterFixtureRoot = Join-Path $pdfRepoRoot 'benchmarks/PdfViewer.P0/Fixtures'
$pdfWriterFixtureNames = @('P0AnnotationCjk.ttf', 'Noto-CJK-COPYRIGHT.txt')
foreach ($pdfWriterFixtureName in $pdfWriterFixtureNames) {
    if (-not (Test-Path -LiteralPath (Join-Path $pdfWriterFixtureRoot $pdfWriterFixtureName) -PathType Leaf)) {
        throw "The licensed PDF writer fixture is missing: $pdfWriterFixtureName"
    }
}
$pdfProperties = [xml](Get-Content -LiteralPath (Join-Path $pdfRepoRoot 'Directory.Build.props') -Raw)
$pdfPackageVersion = [string]$pdfProperties.Project.PropertyGroup.Version
$pdfTargetFramework = [string]$pdfProperties.Project.PropertyGroup.TargetFramework
$pdfVersions = [xml](Get-Content -LiteralPath (Join-Path $pdfRepoRoot 'Directory.Packages.props') -Raw)
$pdfAvaloniaVersion = [string]($pdfVersions.Project.ItemGroup.PackageVersion | Where-Object { $_.Include -eq 'Avalonia' }).Version
if ([string]::IsNullOrWhiteSpace($pdfPackageVersion) -or [string]::IsNullOrWhiteSpace($pdfAvaloniaVersion) -or [string]::IsNullOrWhiteSpace($pdfTargetFramework)) {
    throw 'PdfViewer package, Avalonia version or target framework is missing.'
}
$pdfResults = @()
$pdfProducts = @('NT.AvaloniaControl.PdfViewer', 'NT.AvaloniaControl.PdfViewer.Themes')
Push-Location $pdfRepoRoot
try {
    if (-not $NoPack) {
        foreach ($pdfProduct in $pdfProducts) {
            dotnet pack "src/$pdfProduct/$pdfProduct.csproj" -c Release --no-restore -m:1 -o $pdfFeed
            if ($LASTEXITCODE -ne 0) { throw "PdfViewer package creation failed: $pdfProduct" }
        }
    }
    foreach ($pdfProduct in $pdfProducts) {
        if (-not (Test-Path -LiteralPath (Join-Path $pdfFeed "$pdfProduct.$pdfPackageVersion.nupkg"))) {
            throw "PdfViewer package missing: $pdfProduct.$pdfPackageVersion.nupkg"
        }
    }

    foreach ($pdfMode in @('FunctionOnly', 'DefaultTheme')) {
        $pdfConsumer = Join-Path $pdfRepoRoot ('artifacts/consumer/' + [guid]::NewGuid().ToString('N'))
        $pdfCache = Join-Path $pdfConsumer '.packages'
        New-Item -ItemType Directory -Path $pdfConsumer -Force | Out-Null
        foreach ($pdfWriterFixtureName in $pdfWriterFixtureNames) {
            Copy-Item -LiteralPath (Join-Path $pdfWriterFixtureRoot $pdfWriterFixtureName) -Destination (Join-Path $pdfConsumer $pdfWriterFixtureName)
        }
        # Stop inherited repository project/central-package settings. No source project is referenced.
        foreach ($pdfBoundary in @('Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
            Set-Content -LiteralPath (Join-Path $pdfConsumer $pdfBoundary) -Value '<Project />' -Encoding utf8
        }
        $pdfPackageId = if ($pdfMode -eq 'FunctionOnly') { 'NT.AvaloniaControl.PdfViewer' } else { 'NT.AvaloniaControl.PdfViewer.Themes' }
        foreach ($pdfTemplate in @('Consumer.csproj', 'Program.cs', 'Window.axaml', 'SamplePdf.cs')) {
            $pdfTemplateText = Get-Content -LiteralPath (Join-Path $pdfFixtureRoot "$pdfTemplate.template") -Raw
            $pdfTemplateText.Replace('__PACKAGE_ID__', $pdfPackageId).Replace('__PRODUCT_VERSION__', $pdfPackageVersion).
                Replace('__AVALONIA_VERSION__', $pdfAvaloniaVersion).Replace('__TARGET_FRAMEWORK__', $pdfTargetFramework) |
                Set-Content -LiteralPath (Join-Path $pdfConsumer $pdfTemplate) -Encoding utf8
        }
        $pdfStyles = if ($pdfMode -eq 'FunctionOnly') {
            Get-Content -LiteralPath (Join-Path $pdfFixtureRoot 'CustomStyles.axaml.template') -Raw
        } else {
            '<StyleInclude Source="avares://NT.AvaloniaControl.PdfViewer.Themes/Themes/Generic.axaml" />'
        }
        $pdfApp = Get-Content -LiteralPath (Join-Path $pdfFixtureRoot 'App.axaml.template') -Raw
        $pdfApp.Replace('__PDF_STYLES__', $pdfStyles) | Set-Content -LiteralPath (Join-Path $pdfConsumer 'App.axaml') -Encoding utf8
        $pdfEscapedFeed = [Security.SecurityElement]::Escape($pdfFeed)
        $pdfConfig = Join-Path $pdfConsumer 'NuGet.Config'
        "<configuration><packageSources><clear /><add key=`"local-preview`" value=`"$pdfEscapedFeed`" /><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`" /></packageSources></configuration>" |
            Set-Content -LiteralPath $pdfConfig -Encoding utf8

        if (Test-Path -LiteralPath $pdfCache) { throw 'The generated consumer package cache must start empty.' }
        dotnet restore (Join-Path $pdfConsumer 'Consumer.csproj') --packages $pdfCache --configfile $pdfConfig --disable-parallel
        if ($LASTEXITCODE -ne 0) { throw "PdfViewer $pdfMode isolated package restore failed: $pdfConsumer" }
        $pdfAssets = Get-Content -LiteralPath (Join-Path $pdfConsumer 'obj/project.assets.json') -Raw | ConvertFrom-Json
        $pdfAssetNames = @($pdfAssets.libraries.PSObject.Properties.Name)
        if ($pdfMode -eq 'FunctionOnly' -and @($pdfAssetNames | Where-Object { $_ -like 'NT.AvaloniaControl.PdfViewer.Themes/*' }).Count -gt 0) {
            throw 'The function-only restore graph acquired the optional Themes package.'
        }
        if (@($pdfAssets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'project' }).Count -gt 0) {
            throw 'The isolated consumer restore graph contains a source project.'
        }
        if ($pdfAssetNames -notcontains 'PDFsharp/6.2.4') {
            throw 'The isolated consumer did not restore the pinned production structural writer PDFsharp 6.2.4.'
        }
        $pdfRestoredProducts = if ($pdfMode -eq 'FunctionOnly') { @('NT.AvaloniaControl.PdfViewer') } else { $pdfProducts }
        $pdfHashes = foreach ($pdfProduct in $pdfRestoredProducts) {
            $pdfLowerId = $pdfProduct.ToLowerInvariant()
            $pdfRestoredArchive = Join-Path $pdfCache "$pdfLowerId/$pdfPackageVersion/$pdfLowerId.$pdfPackageVersion.nupkg"
            $pdfFeedArchive = Join-Path $pdfFeed "$pdfProduct.$pdfPackageVersion.nupkg"
            if (-not (Test-Path -LiteralPath $pdfRestoredArchive)) { throw "Fresh-cache archive missing: $pdfRestoredArchive" }
            $pdfFeedHash = (Get-FileHash -LiteralPath $pdfFeedArchive -Algorithm SHA256).Hash
            $pdfCacheHash = (Get-FileHash -LiteralPath $pdfRestoredArchive -Algorithm SHA256).Hash
            if ($pdfFeedHash -ne $pdfCacheHash) { throw "Restored $pdfProduct archive differs from the local feed." }
            [PSCustomObject]@{ Package = $pdfProduct; Version = $pdfPackageVersion; SHA256 = $pdfCacheHash; RestoredArchive = $pdfRestoredArchive }
        }
        dotnet build (Join-Path $pdfConsumer 'Consumer.csproj') -c Release --no-restore -m:1 --nologo
        if ($LASTEXITCODE -ne 0) { throw "PdfViewer $pdfMode isolated compiled-AXAML build failed: $pdfConsumer" }

        $pdfConsumerOutput = Join-Path $pdfConsumer "bin/Release/$pdfTargetFramework"
        $pdfWriterFixtureHashes = foreach ($pdfWriterFixtureName in $pdfWriterFixtureNames) {
            $pdfOriginalFixture = Join-Path $pdfWriterFixtureRoot $pdfWriterFixtureName
            $pdfOutputFixture = Join-Path $pdfConsumerOutput $pdfWriterFixtureName
            if (-not (Test-Path -LiteralPath $pdfOutputFixture -PathType Leaf)) { throw "Writer fixture was not copied to consumer output: $pdfWriterFixtureName" }
            $pdfOriginalFixtureHash = (Get-FileHash -LiteralPath $pdfOriginalFixture -Algorithm SHA256).Hash
            $pdfOutputFixtureHash = (Get-FileHash -LiteralPath $pdfOutputFixture -Algorithm SHA256).Hash
            if ($pdfOriginalFixtureHash -ne $pdfOutputFixtureHash) { throw "Writer fixture changed while copying to consumer output: $pdfWriterFixtureName" }
            [PSCustomObject]@{ File = $pdfWriterFixtureName; SHA256 = $pdfOutputFixtureHash; Output = $pdfOutputFixture }
        }
        $pdfConsumerAssembly = Join-Path $pdfConsumerOutput 'Consumer.dll'
        $pdfResultPath = Join-Path $pdfConsumerOutput 'consumer-result.json'
        $pdfStdoutPath = Join-Path $pdfConsumer 'stdout.log'
        $pdfStderrPath = Join-Path $pdfConsumer 'stderr.log'
        $pdfSupervisorPath = Join-Path $pdfConsumer 'supervisor.json'
        $pdfHostFile = [IO.FileInfo]::new((Get-Command dotnet -CommandType Application | Select-Object -First 1).Source)
        $pdfHostTarget = $pdfHostFile.ResolveLinkTarget($true)
        $pdfExpectedHost = if ($pdfHostTarget) { $pdfHostTarget.FullName } else { $pdfHostFile.FullName }
        $pdfPathComparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
        $pdfRuntimeInfo = [Diagnostics.ProcessStartInfo]::new()
        $pdfRuntimeInfo.FileName = $pdfHostFile.FullName
        $pdfRuntimeInfo.ArgumentList.Add($pdfConsumerAssembly)
        $pdfRuntimeInfo.ArgumentList.Add($pdfMode)
        $pdfRuntimeInfo.WorkingDirectory = $pdfConsumerOutput
        $pdfRuntimeInfo.UseShellExecute = $false
        $pdfRuntimeInfo.CreateNoWindow = $true
        $pdfRuntimeInfo.RedirectStandardOutput = $true
        $pdfRuntimeInfo.RedirectStandardError = $true
        $pdfRuntimeInfo.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
        $pdfRuntimeInfo.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
        $pdfRuntimeProcess = [Diagnostics.Process]::new()
        $pdfRuntimeProcess.StartInfo = $pdfRuntimeInfo
        $pdfRuntimeTimedOut = $false
        $pdfRuntimeStopped = $false
        $pdfRuntimeError = $null
        $pdfRuntimePid = $null
        $pdfProcessStartUtc = $null
        $pdfConsumerExit = $null
        $pdfOutputTask = $null
        $pdfErrorTask = $null
        $pdfCapturedOutput = ''
        $pdfCapturedError = ''
        $pdfRuntimeStart = [DateTimeOffset]::UtcNow
        try {
            if (-not $pdfRuntimeProcess.Start()) { throw 'Isolated PDF consumer process did not start.' }
            $pdfRuntimePid = $pdfRuntimeProcess.Id
            $pdfProcessStartUtc = $pdfRuntimeProcess.StartTime.ToUniversalTime()
            $pdfOutputTask = $pdfRuntimeProcess.StandardOutput.ReadToEndAsync()
            $pdfErrorTask = $pdfRuntimeProcess.StandardError.ReadToEndAsync()
            if (-not $pdfRuntimeProcess.WaitForExit(90000)) {
                $pdfRuntimeTimedOut = $true
                $pdfRuntimeProcess.Refresh()
                if (-not $pdfRuntimeProcess.HasExited) {
                    $pdfActualHostFile = [IO.FileInfo]::new([IO.Path]::GetFullPath($pdfRuntimeProcess.MainModule.FileName))
                    $pdfActualHostTarget = $pdfActualHostFile.ResolveLinkTarget($true)
                    $pdfActualHost = if ($pdfActualHostTarget) { $pdfActualHostTarget.FullName } else { $pdfActualHostFile.FullName }
                    if ($pdfRuntimeProcess.StartTime.ToUniversalTime() -ne $pdfProcessStartUtc -or
                        -not [string]::Equals($pdfActualHost, $pdfExpectedHost, $pdfPathComparison)) {
                        throw 'Refusing to stop a process whose start time or executable differs from this isolated PDF consumer.'
                    }
                    # Only the process started and held above may be stopped. Never enumerate other dotnet processes.
                    $pdfRuntimeProcess.Kill()
                    $pdfRuntimeStopped = $true
                    if (-not $pdfRuntimeProcess.WaitForExit(5000)) { throw 'Timed-out PDF consumer did not exit after termination.' }
                }
            }
            $pdfConsumerExit = $pdfRuntimeProcess.ExitCode
        }
        catch { $pdfRuntimeError = $_.Exception.ToString() }
        finally {
            try {
                if ($pdfOutputTask -and $pdfErrorTask) {
                    if (-not [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($pdfOutputTask, $pdfErrorTask)).Wait(5000)) {
                        throw 'PDF consumer output streams did not close after process completion.'
                    }
                    $pdfCapturedOutput = $pdfOutputTask.GetAwaiter().GetResult()
                    $pdfCapturedError = $pdfErrorTask.GetAwaiter().GetResult()
                }
            }
            catch { $pdfRuntimeError = if ($pdfRuntimeError) { $pdfRuntimeError + [Environment]::NewLine + $_.Exception.ToString() } else { $_.Exception.ToString() } }
            [IO.File]::WriteAllText($pdfStdoutPath, $pdfCapturedOutput, [Text.UTF8Encoding]::new($false))
            [IO.File]::WriteAllText($pdfStderrPath, $pdfCapturedError, [Text.UTF8Encoding]::new($false))
            [ordered]@{
                Mode = $pdfMode; StartedUtc = $pdfRuntimeStart; CompletedUtc = [DateTimeOffset]::UtcNow
                RuntimeTimeoutSeconds = 90; TimedOut = $pdfRuntimeTimedOut; StoppedOwnedProcess = $pdfRuntimeStopped
                ProcessId = $pdfRuntimePid; ProcessStartUtc = $pdfProcessStartUtc
                Executable = $pdfHostFile.FullName; ResolvedExecutable = $pdfExpectedHost; ConsumerAssembly = $pdfConsumerAssembly
                ConsumerAssemblySHA256 = (Get-FileHash -LiteralPath $pdfConsumerAssembly -Algorithm SHA256).Hash
                ExitCode = $pdfConsumerExit; Error = $pdfRuntimeError
                ConsumerResultPresent = Test-Path -LiteralPath $pdfResultPath
                Stdout = $pdfStdoutPath; Stderr = $pdfStderrPath
                FreshPackageCache = $pdfCache; LocalFeed = $pdfFeed; Products = @($pdfHashes)
                WriterFixtures = @($pdfWriterFixtureHashes)
            } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $pdfSupervisorPath -Encoding utf8
            $pdfRuntimeProcess.Dispose()
        }
        if ($pdfCapturedOutput) { Write-Output $pdfCapturedOutput.TrimEnd() }
        if ($pdfRuntimeTimedOut -or $pdfRuntimeError) {
            throw "PdfViewer $pdfMode isolated consumer failed; timeout=$pdfRuntimeTimedOut. Evidence: $pdfSupervisorPath. $pdfRuntimeError"
        }
        if (-not (Test-Path -LiteralPath $pdfResultPath)) { throw "PdfViewer $pdfMode produced no result; exit=$pdfConsumerExit. Evidence: $pdfSupervisorPath" }
        Copy-Item -LiteralPath $pdfResultPath -Destination (Join-Path $pdfConsumer 'result.json')
        $pdfResult = Get-Content -LiteralPath $pdfResultPath -Raw | ConvertFrom-Json
        $pdfFailedChecks = @($pdfResult.Checks.PSObject.Properties | Where-Object { $_.Value -ne $true })
        if ($pdfConsumerExit -ne 0 -or $pdfResult.Passed -ne $true -or $pdfFailedChecks.Count -gt 0 -or $pdfResult.Failure) {
            throw "PdfViewer $pdfMode failed; exit=$pdfConsumerExit, checks=$($pdfFailedChecks.Name -join ', '), error=$($pdfResult.Failure). Evidence: $pdfSupervisorPath"
        }
        $pdfResults += [PSCustomObject]@{
            Mode = $pdfMode; Passed = $true; ConsumerDirectory = $pdfConsumer
            CheckCount = @($pdfResult.Checks.PSObject.Properties).Count; Result = $pdfResultPath
            FreshPackageCache = $pdfCache; Products = @($pdfHashes)
            WriterFixtures = @($pdfWriterFixtureHashes)
        }
        Write-Output "PdfViewer $pdfMode package consumer artifacts: $pdfConsumer"
    }
    [PSCustomObject]@{ TimestampUtc = [DateTimeOffset]::UtcNow; Passed = $true; Consumers = $pdfResults } |
        ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $pdfRepoRoot 'artifacts/pdfviewer-package-consumer-results.json') -Encoding utf8
}
finally { Pop-Location }
