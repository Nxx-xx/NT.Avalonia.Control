[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$OutputDirectory,
    [string]$OfficialPackageDirectory,
    [switch]$CaptureDocumentation,
    [switch]$FreezeBaseline,
    [switch]$ReplaceFrozenBaseline
)

# Capture only: this script never restores, builds, initializes a chart, or reads a license value.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$chartsRepo = Split-Path $PSScriptRoot -Parent
$chartsStarted = [DateTimeOffset]::UtcNow
$chartsRunId = $chartsStarted.ToString('yyyyMMddTHHmmssfffZ') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$chartsUtf8 = [Text.UTF8Encoding]::new($false)
$chartsOfficialVersion = '12.3.0'
$chartsExpectedPackageSha = '90F31AC154DDC3E98B635A24E62CB965ED5A1D3C5A981A8850F16A2A5E1C2387'
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $chartsRepo ('artifacts/charts/c0-baseline/' + $chartsRunId)
}
$chartsOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $chartsOutput) { throw 'Capture output must be a new directory; existing evidence is immutable.' }
if ([string]::IsNullOrWhiteSpace($OfficialPackageDirectory)) {
    $OfficialPackageDirectory = Join-Path $chartsRepo 'benchmarks/Charts.Reference/evidence/official-12.3.0'
}
$chartsOfficialDirectory = [IO.Path]::GetFullPath($OfficialPackageDirectory)
$chartsFreezeJson = Join-Path $chartsRepo 'docs/charts-c0-baseline.json'
$chartsFreezeApi = Join-Path $chartsRepo 'docs/charts-c0-api.txt'
if ($ReplaceFrozenBaseline -and !$FreezeBaseline) { throw '-ReplaceFrozenBaseline requires -FreezeBaseline.' }
if ($FreezeBaseline -and !$ReplaceFrozenBaseline -and
    ((Test-Path -LiteralPath $chartsFreezeJson) -or (Test-Path -LiteralPath $chartsFreezeApi))) {
    throw 'C0 is already frozen. Use a new capture, or explicitly replace the freeze before C1 starts.'
}
New-Item -ItemType Directory -Path $chartsOutput | Out-Null

function Write-ChartsText([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text.Replace("`r`n", "`n"), $chartsUtf8)
}
function Write-ChartsJson([string]$Path, $Value) {
    Write-ChartsText $Path (($Value | ConvertTo-Json -Depth 45) + "`n")
}
function Get-ChartsSha([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
function Get-ChartsTextSha([string]$Text) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($chartsUtf8.GetBytes($Text)))
}
function Get-ChartsRelative([string]$Path) {
    [IO.Path]::GetRelativePath($chartsRepo, [IO.Path]::GetFullPath($Path)).Replace('\', '/')
}
function Get-ChartsFileRecord([string]$Path) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [ordered]@{ path = Get-ChartsRelative $Path; status = 'missing'; sha256 = $null }
    }
    $chartsFile = Get-Item -LiteralPath $Path
    [ordered]@{ path = Get-ChartsRelative $Path; status = 'captured'; sha256 = Get-ChartsSha $Path;
        bytes = $chartsFile.Length; lastWriteUtc = $chartsFile.LastWriteTimeUtc.ToString('O') }
}

$chartsMatrixPath = Join-Path $chartsRepo 'docs/charts-official-alignment.md'
$chartsMatrixText = [IO.File]::ReadAllText($chartsMatrixPath)
$chartsMatrixRows = @($chartsMatrixText -split '\r?\n' | Where-Object { $_ -match '^\| [A-Z]{2}\d+ \|' })
if ($chartsMatrixRows.Count -ne 200) { throw "Expected the frozen 200-row alignment matrix; found $($chartsMatrixRows.Count). Review scope explicitly." }
$chartsMatrixItems = @($chartsMatrixRows | ForEach-Object {
    $chartsColumns = $_.Trim('|').Split('|') | ForEach-Object { $_.Trim() }
    [ordered]@{ id = $chartsColumns[0]; name = $chartsColumns[1]; status = $chartsColumns[2];
        remainingWork = $chartsColumns[3]; phases = $chartsColumns[4]; rowSnapshot = $_ }
})
$chartsSourceUrls = @([regex]::Matches($chartsMatrixText, '\]\((https://[^)]+)\)') |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
$chartsSourceUrls += @(
    'https://www.nuget.org/packages/Avalonia.Controls.Charts/12.3.0',
    'https://api.nuget.org/v3-flatcontainer/avalonia.controls.charts/index.json',
    'https://api.nuget.org/v3-flatcontainer/avalonia.controls.charts/12.3.0/avalonia.controls.charts.12.3.0.nupkg',
    'https://docs.avaloniaui.net/tools/installing-avalonia-pro'
)
$chartsSourceUrls = @($chartsSourceUrls | Sort-Object -Unique)
$chartsSourceSnapshots = [Collections.Generic.List[object]]::new()
$chartsWebDirectory = Join-Path $chartsOutput 'documentation'
if ($CaptureDocumentation) { New-Item -ItemType Directory -Path $chartsWebDirectory | Out-Null }
foreach ($chartsUrl in $chartsSourceUrls) {
    $chartsSource = [ordered]@{ url = $chartsUrl; accessedDate = '2026-10-15';
        evidenceKind = 'primary-public-source'; status = 'listed-not-downloaded'; sha256 = $null; capturedUtc = $null }
    if ($CaptureDocumentation -and !$chartsUrl.EndsWith('.nupkg', [StringComparison]::Ordinal)) {
        $chartsName = (Get-ChartsTextSha $chartsUrl).ToLowerInvariant() + '.snapshot'
        $chartsSnapshotPath = Join-Path $chartsWebDirectory $chartsName
        try {
            Invoke-WebRequest -Uri $chartsUrl -OutFile $chartsSnapshotPath -TimeoutSec 15
            $chartsSource.status = 'captured-raw-http'
            $chartsSource.sha256 = Get-ChartsSha $chartsSnapshotPath
            $chartsSource.capturedUtc = [DateTimeOffset]::UtcNow.ToString('O')
            $chartsSource['snapshotPath'] = Get-ChartsRelative $chartsSnapshotPath
        }
        catch {
            # No response/body or arbitrary environment values are logged.
            $chartsSource.status = 'blocked-http'
            $chartsSource['failureType'] = $_.Exception.GetType().FullName
        }
    }
    $chartsSourceSnapshots.Add($chartsSource)
}

$chartsManifestFiles = [Collections.Generic.List[string]]::new()
foreach ($chartsBoundary in @('Directory.Build.props', 'Directory.Packages.props', 'global.json', 'NuGet.Config', 'NT.AvaloniaControl.slnx',
    'docs/charts-development-plan.md', 'docs/charts-official-alignment.md', 'docs/charts-performance-platform-plan.md',
    'scripts/Capture-ChartsBaseline.ps1')) {
    $chartsBoundaryPath = Join-Path $chartsRepo $chartsBoundary
    if (Test-Path -LiteralPath $chartsBoundaryPath -PathType Leaf) { $chartsManifestFiles.Add($chartsBoundaryPath) }
}
foreach ($chartsSourceRoot in @('src/NT.AvaloniaControl.Charts', 'src/NT.AvaloniaControl.Charts.Themes',
    'tests/Charts.Headless.Tests', 'benchmarks/Charts.Reference')) {
    $chartsSourceDirectory = Join-Path $chartsRepo $chartsSourceRoot
    if (!(Test-Path -LiteralPath $chartsSourceDirectory)) { continue }
    Get-ChildItem -LiteralPath $chartsSourceDirectory -File -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|evidence|\.git)[\\/]' } |
        ForEach-Object { $chartsManifestFiles.Add($_.FullName) }
}
$chartsSourceManifest = @($chartsManifestFiles | Sort-Object -Unique | ForEach-Object { Get-ChartsFileRecord $_ })
$chartsManifestPath = Join-Path $chartsOutput 'source-manifest.json'
Write-ChartsJson $chartsManifestPath $chartsSourceManifest
$chartsLocks = @('src/NT.AvaloniaControl.Charts/packages.lock.json',
    'src/NT.AvaloniaControl.Charts.Themes/packages.lock.json', 'samples/NT.AvaloniaControl.Demo/packages.lock.json',
    'tests/Charts.Headless.Tests/packages.lock.json', 'benchmarks/Charts.Reference/packages.lock.json') |
    ForEach-Object { Get-ChartsFileRecord (Join-Path $chartsRepo $_) }
$chartsAssemblies = @('NT.AvaloniaControl.Charts', 'NT.AvaloniaControl.Charts.Themes') | ForEach-Object {
    Get-ChartsFileRecord (Join-Path $chartsRepo "src/$_/bin/$Configuration/net10.0/$_.dll")
}
$chartsOfficialArchivePath = Join-Path $chartsOfficialDirectory 'avalonia.controls.charts.12.3.0.nupkg'
$chartsOfficialDll = Join-Path $chartsOfficialDirectory 'lib/net9.0/Avalonia.Controls.Charts.dll'
$chartsOfficialXml = Join-Path $chartsOfficialDirectory 'lib/net9.0/Avalonia.Controls.Charts.xml'
$chartsOfficialArchive = Get-ChartsFileRecord $chartsOfficialArchivePath
$chartsOfficialArchiveVerified = $chartsOfficialArchive.status -eq 'captured' -and $chartsOfficialArchive.sha256 -eq $chartsExpectedPackageSha
if ($chartsOfficialArchive.status -eq 'captured' -and !$chartsOfficialArchiveVerified) {
    throw 'Official archive does not match the pinned C0 package SHA-256.'
}

$chartsDotnetInfo = (& dotnet --info 2>&1 | Out-String)
$chartsSdkList = @(& dotnet --list-sdks)
$chartsRuntimeList = @(& dotnet --list-runtimes)
Write-ChartsText (Join-Path $chartsOutput 'dotnet-info.txt') $chartsDotnetInfo
$chartsGlobalJson = Get-Content -LiteralPath (Join-Path $chartsRepo 'global.json') -Raw | ConvertFrom-Json
$chartsSdkVersion = [string]$chartsGlobalJson.sdk.version
$chartsSdkLine = $chartsSdkList | Where-Object { $_ -match ('^' + [regex]::Escape($chartsSdkVersion) + ' \[') } | Select-Object -First 1
$chartsSdkPath = if ($chartsSdkLine -match '\[(.+)\]') { Join-Path $Matches[1] $chartsSdkVersion } else { $null }
$chartsMetadataPath = if ($chartsSdkPath) { Join-Path $chartsSdkPath 'System.Reflection.MetadataLoadContext.dll' } else { $null }
$chartsApiText = [Text.StringBuilder]::new()
[void]$chartsApiText.AppendLine('Charts C0 API snapshot. Metadata only; no constructors or static property getters are executed.')
[void]$chartsApiText.AppendLine('Public/protected declared members; inherited members omitted; generated record members retained.')
[void]$chartsApiText.AppendLine('Official runtime defaults and licensing are NOT validated by this metadata snapshot.')
[void]$chartsApiText.AppendLine('RunId: ' + $chartsRunId)
[void]$chartsApiText.AppendLine('Configuration: ' + $Configuration)
$chartsApiResults = [Collections.Generic.List[object]]::new()

function Test-ChartsApiAccess($Member) { $Member.IsPublic -or $Member.IsFamily -or $Member.IsFamilyOrAssembly }
function Get-ChartsParameterDefaults($Parameters) {
    $chartsOptional = @($Parameters | Where-Object HasDefaultValue | ForEach-Object {
        $chartsDefault = if ($null -eq $_.RawDefaultValue) { 'null' } else {
            [Convert]::ToString($_.RawDefaultValue, [Globalization.CultureInfo]::InvariantCulture)
        }
        $_.Name + '=' + $chartsDefault
    })
    if ($chartsOptional.Count) { ' [optional: ' + ($chartsOptional -join ', ') + ']' } else { '' }
}
function Export-ChartsAssemblyApi($Context, [string]$AssemblyPath, [string]$Kind) {
    $chartsResult = [ordered]@{ kind = $Kind; path = Get-ChartsRelative $AssemblyPath; status = 'missing'; exportedTypes = 0 }
    if (!(Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) { return $chartsResult }
    try {
        $chartsAssembly = $Context.LoadFromAssemblyPath($AssemblyPath)
        $chartsTypes = @($chartsAssembly.GetExportedTypes() | Sort-Object FullName)
        [void]$chartsApiText.AppendLine()
        [void]$chartsApiText.AppendLine('Assembly: ' + $chartsAssembly.FullName)
        [void]$chartsApiText.AppendLine('SHA256: ' + (Get-ChartsSha $AssemblyPath))
        $chartsFlags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
        foreach ($chartsType in $chartsTypes) {
            $chartsBase = if ($chartsType.BaseType) { $chartsType.BaseType.FullName } else { 'none' }
            [void]$chartsApiText.AppendLine($chartsType.FullName + ' : ' + $chartsBase +
                " [abstract=$($chartsType.IsAbstract), sealed=$($chartsType.IsSealed), valueType=$($chartsType.IsValueType)]")
            foreach ($chartsInterface in $chartsType.GetInterfaces() | Sort-Object FullName) {
                [void]$chartsApiText.AppendLine('  Interface: ' + $chartsInterface.FullName)
            }
            foreach ($chartsConstructor in $chartsType.GetConstructors($chartsFlags) | Where-Object { Test-ChartsApiAccess $_ } | Sort-Object { $_.ToString() }) {
                [void]$chartsApiText.AppendLine('  Constructor: ' + $chartsConstructor + (Get-ChartsParameterDefaults $chartsConstructor.GetParameters()))
            }
            foreach ($chartsField in $chartsType.GetFields($chartsFlags) | Where-Object { Test-ChartsApiAccess $_ } | Sort-Object Name) {
                $chartsConstant = if ($chartsField.IsLiteral) { ' = ' + [Convert]::ToString($chartsField.GetRawConstantValue(), [Globalization.CultureInfo]::InvariantCulture) } else { '' }
                [void]$chartsApiText.AppendLine('  Field: ' + $chartsField + $chartsConstant + " [static=$($chartsField.IsStatic), readonly=$($chartsField.IsInitOnly)]")
            }
            foreach ($chartsProperty in $chartsType.GetProperties($chartsFlags) | Where-Object {
                ($_.GetMethod -and (Test-ChartsApiAccess $_.GetMethod)) -or ($_.SetMethod -and (Test-ChartsApiAccess $_.SetMethod))
            } | Sort-Object Name) {
                [void]$chartsApiText.AppendLine('  Property: ' + $chartsProperty +
                    " [get=$($null -ne $chartsProperty.GetMethod), set=$($null -ne $chartsProperty.SetMethod)]")
            }
            foreach ($chartsMethod in $chartsType.GetMethods($chartsFlags) | Where-Object { !$_.IsSpecialName -and (Test-ChartsApiAccess $_) } | Sort-Object { $_.ToString() }) {
                [void]$chartsApiText.AppendLine('  Method: ' + $chartsMethod + (Get-ChartsParameterDefaults $chartsMethod.GetParameters()) +
                    " [static=$($chartsMethod.IsStatic), abstract=$($chartsMethod.IsAbstract), virtual=$($chartsMethod.IsVirtual)]")
            }
            foreach ($chartsEvent in $chartsType.GetEvents($chartsFlags) | Where-Object { $_.AddMethod -and (Test-ChartsApiAccess $_.AddMethod) } | Sort-Object Name) {
                [void]$chartsApiText.AppendLine('  Event: ' + $chartsEvent)
            }
        }
        $chartsResult.status = 'captured-metadata-only'
        $chartsResult.exportedTypes = $chartsTypes.Count
        $chartsResult['assemblyIdentity'] = $chartsAssembly.FullName
        $chartsResult['sha256'] = Get-ChartsSha $AssemblyPath
    }
    catch {
        $chartsResult.status = 'blocked-metadata'
        $chartsResult['failureType'] = $_.Exception.GetType().FullName
        $chartsResult['failureDetail'] = $_.Exception.Message
        [void]$chartsApiText.AppendLine('Metadata export blocked: ' + $chartsResult.path)
    }
    return $chartsResult
}

if ($chartsMetadataPath -and (Test-Path -LiteralPath $chartsMetadataPath)) {
    [void][Reflection.Assembly]::LoadFrom($chartsMetadataPath)
    $chartsResolverMap = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    $chartsDotnetRoot = Split-Path (Split-Path $chartsSdkPath -Parent) -Parent
    $chartsRefPackRoot = Join-Path $chartsDotnetRoot 'packs/Microsoft.NETCore.App.Ref'
    $chartsRefPack = Get-ChildItem -LiteralPath $chartsRefPackRoot -Directory |
        Where-Object Name -like '10.*' | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    foreach ($chartsRef in Get-ChildItem -LiteralPath (Join-Path $chartsRefPack.FullName 'ref/net10.0') -Filter '*.dll') {
        $chartsResolverMap[$chartsRef.Name] = $chartsRef.FullName
    }
    # The restored assets describe dependency locations; they are not restored or executed here.
    foreach ($chartsAssetsRelative in @('src/NT.AvaloniaControl.Charts/obj/project.assets.json',
        'src/NT.AvaloniaControl.Charts.Themes/obj/project.assets.json', 'samples/NT.AvaloniaControl.Demo/obj/project.assets.json')) {
        $chartsAssetsPath = Join-Path $chartsRepo $chartsAssetsRelative
        if (!(Test-Path -LiteralPath $chartsAssetsPath)) { continue }
        $chartsAssets = Get-Content -LiteralPath $chartsAssetsPath -Raw | ConvertFrom-Json -AsHashtable
        foreach ($chartsTarget in $chartsAssets.targets.Values) {
            foreach ($chartsLibrary in $chartsTarget.GetEnumerator()) {
                if (!$chartsLibrary.Value.ContainsKey('runtime')) { continue }
                foreach ($chartsRuntimeAsset in $chartsLibrary.Value.runtime.Keys) {
                    foreach ($chartsPackageRoot in $chartsAssets.packageFolders.Keys) {
                        $chartsDependency = Join-Path $chartsPackageRoot ($chartsLibrary.Key.ToLowerInvariant() + '/' + $chartsRuntimeAsset)
                        if (!(Test-Path -LiteralPath $chartsDependency) -or !($chartsDependency.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase))) { continue }
                        $chartsDependencyName = [IO.Path]::GetFileName($chartsDependency)
                        if (!$chartsResolverMap.ContainsKey($chartsDependencyName)) { $chartsResolverMap[$chartsDependencyName] = $chartsDependency }
                    }
                }
            }
        }
    }
    $chartsApiPaths = @($chartsAssemblies | Where-Object status -eq 'captured' | ForEach-Object { Join-Path $chartsRepo $_.path })
    if ($chartsOfficialArchiveVerified -and (Test-Path -LiteralPath $chartsOfficialDll)) { $chartsApiPaths += $chartsOfficialDll }
    foreach ($chartsApiPath in $chartsApiPaths) { $chartsResolverMap[[IO.Path]::GetFileName($chartsApiPath)] = $chartsApiPath }
    $chartsResolverList = [Collections.Generic.List[string]]::new()
    foreach ($chartsResolverPath in $chartsResolverMap.Values) { $chartsResolverList.Add($chartsResolverPath) }
    $chartsMetadataContext = [Reflection.MetadataLoadContext]::new([Reflection.PathAssemblyResolver]::new($chartsResolverList), 'System.Runtime')
    try {
        foreach ($chartsAssembly in $chartsAssemblies) {
            $chartsApiResults.Add((Export-ChartsAssemblyApi $chartsMetadataContext (Join-Path $chartsRepo $chartsAssembly.path) 'local'))
        }
        if ($chartsOfficialArchiveVerified) {
            $chartsApiResults.Add((Export-ChartsAssemblyApi $chartsMetadataContext $chartsOfficialDll 'official-reference'))
        }
    }
    finally { $chartsMetadataContext.Dispose() }
}
else {
    $chartsApiResults.Add([ordered]@{ kind = 'all'; status = 'blocked-metadata-helper'; sdkVersion = $chartsSdkVersion })
}

[void]$chartsApiText.AppendLine()
[void]$chartsApiText.AppendLine('Local styled-property source registration snapshot (not observed runtime defaults):')
foreach ($chartsSource in Get-ChildItem -LiteralPath (Join-Path $chartsRepo 'src/NT.AvaloniaControl.Charts') -Filter '*.cs' | Sort-Object Name) {
    $chartsSourceText = [IO.File]::ReadAllText($chartsSource.FullName)
    foreach ($chartsRegistration in [regex]::Matches($chartsSourceText, 'public static readonly [\s\S]*?Property\s*=\s*AvaloniaProperty\.Register[\s\S]*?;')) {
        [void]$chartsApiText.AppendLine('  ' + (Get-ChartsRelative $chartsSource.FullName) + ': ' +
            [regex]::Replace($chartsRegistration.Value, '\s+', ' '))
    }
}
$chartsApiOutput = Join-Path $chartsOutput 'api.txt'
Write-ChartsText $chartsApiOutput $chartsApiText.ToString()

$chartsOfficialDefaults = @(
    @{ type = 'ChartBase'; member = 'Title'; value = 'null'; source = 'charts' },
    @{ type = 'ChartSeries'; member = 'ItemsSource'; value = 'null'; source = 'charts' },
    @{ type = 'ChartSeries'; member = 'ValuePath'; value = 'null'; source = 'charts' },
    @{ type = 'ChartSeries'; member = 'StrokeThickness'; value = '1.0; a default theme can override it'; source = 'charts' },
    @{ type = 'CartesianSeries'; member = 'EmptyPointMode'; value = 'Zero'; source = 'charts' },
    @{ type = 'ChartBase'; member = 'IsAnimationEnabled'; value = 'true'; source = 'charts' },
    @{ type = 'ChartBase'; member = 'AnimationDuration'; value = '00:00:01'; source = 'charts' },
    @{ type = 'ChartBase'; member = 'Easing'; value = 'CubicEaseOut'; source = 'charts' },
    @{ type = 'ChartSeries'; member = 'AnimationDelay'; value = '00:00:00'; source = 'charts' },
    @{ type = 'ChartSeries'; member = 'AnimationProgress'; value = '1.0'; source = 'charts' },
    @{ type = 'CartesianChart'; member = 'IsZoomEnabled'; value = 'false'; source = 'interactions-chart' },
    @{ type = 'CartesianChart'; member = 'IsPanEnabled'; value = 'false'; source = 'interactions-chart' },
    @{ type = 'CartesianChart'; member = 'ZoomMode'; value = 'XY'; source = 'interactions-chart' },
    @{ type = 'CartesianChart'; member = 'ZoomSensitivity'; value = '0.1'; source = 'interactions-chart' },
    @{ type = 'CartesianChart'; member = 'ShowRangeSelector'; value = 'true'; source = 'interactions-chart' },
    @{ type = 'CartesianChart'; member = 'ZoomHistoryLimit'; value = '20'; source = 'interactions-chart' },
    @{ type = 'CartesianChart'; member = 'CrosshairMode'; value = 'None'; source = 'crosshairs-chart' },
    @{ type = 'CartesianChart'; member = 'TrackballMode'; value = 'None'; source = 'interactions-chart' },
    @{ type = 'CartesianChart'; member = 'TrackballDisplayMode'; value = 'FloatAllPoints'; source = 'interactions-chart' },
    @{ type = 'ChartBase'; member = 'ShowLegend'; value = 'false'; source = 'legend-chart' },
    @{ type = 'ChartBase'; member = 'LegendPosition'; value = 'None'; source = 'legend-chart' },
    @{ type = 'ChartBase'; member = 'LegendAlignment'; value = 'Center'; source = 'legend-chart' },
    @{ type = 'ChartBase'; member = 'ToggleSeriesVisibility'; value = 'true; scale legends may coerce false'; source = 'legend-chart' },
    @{ type = 'ChartBase'; member = 'IsTooltipEnabled'; value = 'true'; source = 'tooltip-chart' },
    @{ type = 'ChartAxis'; member = 'IsVisible'; value = 'true'; source = 'axis-customization-chart' },
    @{ type = 'ChartAxis'; member = 'Title'; value = 'null'; source = 'axis-customization-chart' },
    @{ type = 'ChartAxis'; member = 'ShowLabels'; value = 'true'; source = 'axis-customization-chart' },
    @{ type = 'ChartAxis'; member = 'LabelFontSize'; value = '12.0'; source = 'axis-customization-chart' },
    @{ type = 'ChartAxis'; member = 'ShowGridLines'; value = 'true'; source = 'axis-customization-chart' },
    @{ type = 'ChartAxis'; member = 'ShowMinorGridLines'; value = 'false'; source = 'axis-customization-chart' },
    @{ type = 'ChartAxis'; member = 'LabelFitMode'; value = 'None'; source = 'axis-customization-chart' },
    @{ type = 'NumericalAxis'; member = 'Minimum'; value = 'null'; source = 'axis-customization-chart' },
    @{ type = 'NumericalAxis'; member = 'Maximum'; value = 'null'; source = 'axis-customization-chart' },
    @{ type = 'NumericalAxis'; member = 'MajorStep'; value = 'null'; source = 'axis-customization-chart' },
    @{ type = 'DateTimeAxis'; member = 'MajorStep'; value = '1.0'; source = 'axis-customization-chart' },
    @{ type = 'DateTimeAxis'; member = 'MajorStepUnit'; value = 'Day'; source = 'axis-customization-chart' },
    @{ type = 'LogarithmicAxis'; member = 'LogBase'; value = '10.0'; source = 'axis-customization-chart' },
    @{ type = 'LogarithmicAxis'; member = 'MajorStep'; value = 'null'; source = 'axis-customization-chart' },
    @{ type = 'CartesianSeries'; member = 'ShowMarkers'; value = 'series-dependent; line family false, some point series true'; source = 'markers-chart' },
    @{ type = 'CartesianSeries'; member = 'MarkerShape'; value = 'Circle'; source = 'markers-chart' },
    @{ type = 'CartesianSeries'; member = 'MarkerStrokeThickness'; value = 'NaN; falls back to series StrokeThickness'; source = 'markers-chart' }
) | ForEach-Object {
    $_.sourceUrl = if ($_.source -eq 'charts') { 'https://docs.avaloniaui.net/controls/data-display/charts' } else {
        'https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/' + $_.source
    }
    $_.evidence = 'rolling-public-docs-default; not-observed-on-12.3.0-runtime'
    $_.accessedDate = '2026-10-15'
    $_
}
$chartsEnvironment = [ordered]@{
    osDescription = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    osArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    powershell = $PSVersionTable.PSVersion.ToString()
    framework = [Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
    sdkRequired = $chartsSdkVersion; installedSdks = $chartsSdkList; installedRuntimes = $chartsRuntimeList
    culture = [Globalization.CultureInfo]::CurrentCulture.Name
    uiCulture = [Globalization.CultureInfo]::CurrentUICulture.Name
    timezone = [TimeZoneInfo]::Local.Id
    licenseEnvironmentPresent = Test-Path Env:AVALONIA_LICENSE_KEY
    licenseValueRead = $false
    requestedConfiguration = $Configuration
    nativeWindowRun = $false; actualPresentMeasured = $false; linuxAcceptance = 'not-run'
}
if ($IsWindows) {
    try {
        $chartsEnvironment['windows'] = Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, BuildNumber, OSArchitecture
        $chartsEnvironment['cpu'] = @(Get-CimInstance Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors)
        $chartsEnvironment['gpu'] = @(Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion)
    }
    catch { $chartsEnvironment['hardwareInventory'] = 'blocked-cim-access; use the native platform report for device evidence' }
}
else {
    if (Test-Path -LiteralPath '/etc/os-release') { $chartsEnvironment['osRelease'] = @(Get-Content -LiteralPath '/etc/os-release') }
    $chartsEnvironment['sessionType'] = [Environment]::GetEnvironmentVariable('XDG_SESSION_TYPE')
    $chartsEnvironment['displayPresent'] = Test-Path Env:DISPLAY
    $chartsEnvironment['waylandDisplayPresent'] = Test-Path Env:WAYLAND_DISPLAY
}
$chartsGitHead = (& git -C $chartsRepo rev-parse HEAD 2>$null | Out-String).Trim()
$chartsGitStatus = @(& git -C $chartsRepo status --porcelain=v1 --untracked-files=normal)
$chartsMissing = [Collections.Generic.List[string]]::new()
foreach ($chartsAssembly in $chartsAssemblies | Where-Object status -eq 'missing') { $chartsMissing.Add('local-assembly:' + $chartsAssembly.path) }
if (!$chartsOfficialArchiveVerified) { $chartsMissing.Add('official-pinned-package-not-available') }
if (!$chartsEnvironment.licenseEnvironmentPresent) { $chartsMissing.Add('official-license-environment-not-present') }
if (@($chartsApiResults | Where-Object status -ne 'captured-metadata-only').Count) { $chartsMissing.Add('one-or-more-api-snapshots-unavailable') }
if (@($chartsSourceSnapshots | Where-Object status -eq 'blocked-http').Count) { $chartsMissing.Add('one-or-more-documentation-http-snapshots-unavailable') }
if (@($chartsLocks | Where-Object { $_.path -like 'benchmarks/Charts.Reference/*' -and $_.status -eq 'missing' }).Count) {
    $chartsMissing.Add('official-reference-lock-not-generated')
}
$chartsMissing.Add('official-runtime-defaults-not-observed')
$chartsMissing.Add('official-reference-host-not-restored-built-or-run-by-capture')
$chartsMissing.Add('linux-native-and-performance-acceptance-not-run')
$chartsBaseline = [ordered]@{
    schemaVersion = 1; stage = 'C0'; runId = $chartsRunId
    capturedUtc = $chartsStarted.ToString('O'); accessedDate = '2026-10-15'
    captureKind = 'read-only-metadata-and-source'; executedRestore = $false; executedBuild = $false
    git = [ordered]@{ head = $chartsGitHead; workingTreeChanges = $chartsGitStatus }
    environment = $chartsEnvironment
    alignment = [ordered]@{
        file = Get-ChartsFileRecord $chartsMatrixPath; matrixItemCount = $chartsMatrixRows.Count
        rowsSha256 = Get-ChartsTextSha (($chartsMatrixRows -join "`n") + "`n")
        sourceListSha256 = Get-ChartsTextSha (($chartsSourceUrls -join "`n") + "`n")
        items = $chartsMatrixItems; sources = $chartsSourceSnapshots
    }
    local = [ordered]@{ sourceManifest = Get-ChartsFileRecord $chartsManifestPath; sourceFiles = $chartsSourceManifest
        assemblies = $chartsAssemblies; dependencyLocks = @($chartsLocks)
        api = Get-ChartsFileRecord $chartsApiOutput; apiResults = $chartsApiResults }
    officialReference = [ordered]@{
        packageId = 'Avalonia.Controls.Charts'; pinnedVersion = $chartsOfficialVersion
        packageVersionEvidence = 'nuget-v3-index-and-nuspec'
        versionIndex = Get-ChartsFileRecord (Join-Path $chartsOfficialDirectory 'versions.json')
        archive = $chartsOfficialArchive; pinnedArchiveSha256 = $chartsExpectedPackageSha; archiveVerified = $chartsOfficialArchiveVerified
        assembly = Get-ChartsFileRecord $chartsOfficialDll; xmlDocumentation = Get-ChartsFileRecord $chartsOfficialXml
        nuspec = Get-ChartsFileRecord (Join-Path $chartsOfficialDirectory 'Avalonia.Controls.Charts.nuspec')
        targetFramework = 'net9.0'; upstreamRepositoryCommit = 'af8225d971c05fb541086f6ab1e9c53ca10a7ee6'
        declaredDependencies = @(
            @{ id = 'Avalonia'; minimumVersion = '12.0.0' },
            @{ id = 'AvaloniaUI.Licensing'; minimumVersion = '3.0.7'; nuspecExcludes = 'Runtime,Compile,Build,Native,Analyzers' },
            @{ id = 'SkiaSharp'; minimumVersion = '3.119.3-preview.1.1' }
        )
        documentedDefaults = @($chartsOfficialDefaults)
        defaultsRuntimeVerified = $false
        nativeHostStatus = 'blocked-reference'
        reason = 'Reference build/runtime/entitlement and matching benchmark data must pass before any comparison; capture does not execute licensed controls.'
    }
    missingOrUnverified = $chartsMissing
}
$chartsBaselineOutput = Join-Path $chartsOutput 'baseline.json'
Write-ChartsJson $chartsBaselineOutput $chartsBaseline
if ($FreezeBaseline) {
    [IO.File]::Copy($chartsBaselineOutput, $chartsFreezeJson, $ReplaceFrozenBaseline.IsPresent)
    [IO.File]::Copy($chartsApiOutput, $chartsFreezeApi, $ReplaceFrozenBaseline.IsPresent)
}
[pscustomobject]@{ runId = $chartsRunId; baseline = $chartsBaselineOutput; api = $chartsApiOutput
    matrixItems = $chartsMatrixRows.Count; sourceUrls = $chartsSourceUrls.Count
    localApiStatus = @($chartsApiResults | Where-Object kind -eq 'local' | ForEach-Object status) -join ','
    officialApiStatus = @($chartsApiResults | Where-Object kind -eq 'official-reference' | ForEach-Object status) -join ','
    referenceRuntimeStatus = 'blocked-reference'; frozen = $FreezeBaseline.IsPresent }
