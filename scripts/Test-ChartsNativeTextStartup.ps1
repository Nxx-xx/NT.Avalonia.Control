[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Feed,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$PackageVersion,
    [string]$SystemHarfBuzzPath,
    [string]$FontDirectory,
    [string[]]$DependencyFeed = @(),
    [string]$PythonExecutable,
    [ValidateRange(30,1800)][int]$TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -and -not $IsLinux) { throw 'This startup acceptance script currently targets Windows and Linux.' }
$startupRepository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$startupOutput = [IO.Path]::GetFullPath($OutputDirectory)
$startupFeed = [IO.Path]::GetFullPath($Feed)
if (Test-Path -LiteralPath $startupOutput) { throw 'Use a new startup evidence directory; existing evidence is never overwritten.' }
if (-not (Test-Path -LiteralPath $startupFeed -PathType Container)) { throw 'The caller must supply an existing local package feed.' }
if ($IsLinux -and (-not $SystemHarfBuzzPath -or -not [IO.Path]::IsPathRooted($SystemHarfBuzzPath) -or -not (Test-Path -LiteralPath $SystemHarfBuzzPath -PathType Leaf))) { throw 'Linux controls require an explicit existing absolute -SystemHarfBuzzPath.' }
if ($FontDirectory -and (-not [IO.Path]::IsPathRooted($FontDirectory) -or -not (Test-Path -LiteralPath $FontDirectory -PathType Container))) { throw '-FontDirectory must be an existing absolute directory.' }
foreach($dependency in $DependencyFeed){if(-not (Test-Path -LiteralPath $dependency -PathType Container)){throw 'A dependency feed directory does not exist.'}}
if (-not $PythonExecutable) { $PythonExecutable = (Get-Command $(if($IsWindows){'python'}else{'python3'}) -ErrorAction Stop).Source }
if (-not $PackageVersion) { $PackageVersion = [string]([xml](Get-Content -LiteralPath (Join-Path $startupRepository 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version }
$startupAvalonia = [string](([xml](Get-Content -LiteralPath (Join-Path $startupRepository 'Directory.Packages.props') -Raw)).Project.ItemGroup.PackageVersion | Where-Object Include -eq 'Avalonia').Version
. (Join-Path $PSScriptRoot 'ChartsPerformanceGate.Process.ps1')
[IO.Directory]::CreateDirectory($startupOutput) | Out-Null
$startupStarted = [DateTimeOffset]::UtcNow
$startupProcesses = [Collections.Generic.List[object]]::new()
$startupCases = [Collections.Generic.List[object]]::new()
$startupError = $null
$startupPassed = $false
$startupConsumer = Join-Path $startupOutput 'consumer'
$startupPayloadSha = $null
$startupPackage = Join-Path $startupFeed "NT.AvaloniaControl.Charts.$PackageVersion.nupkg"

function Save-StartupJson($Value,[string]$Path) { $Value | ConvertTo-Json -Depth 60 | Set-Content -LiteralPath $Path -Encoding utf8 }
function Invoke-StartupStage([string]$Id,[string]$Executable,[string[]]$Arguments,[string]$WorkingDirectory,[hashtable]$Environment = @{}) {
    $old = @{}
    try {
        foreach($name in $Environment.Keys) { $old[$name]=[Environment]::GetEnvironmentVariable($name,'Process'); [Environment]::SetEnvironmentVariable($name,[string]$Environment[$name],'Process') }
        $receipt=Invoke-ChartsGateProcess -Id $Id -Executable $Executable -Arguments $Arguments -WorkingDirectory $WorkingDirectory -OutputDirectory (Join-Path $startupOutput "processes/$Id") -TimeoutSeconds $TimeoutSeconds
        $startupProcesses.Add($receipt)
        return $receipt
    } finally { foreach($name in $old.Keys) { [Environment]::SetEnvironmentVariable($name,$old[$name],'Process') } }
}
function Assert-StartupStage($Receipt) { if(-not $Receipt.passed){throw "Required stage failed: $($Receipt.id)"} }
function Get-StartupInputs {
    return @(foreach($path in @((Join-Path $PSScriptRoot 'Test-ChartsNativeTextStartup.ps1')) + @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Consumers/ChartsNativeTextStartup') -Force -File | ForEach-Object FullName)) {
        $file=Get-Item -LiteralPath $path -Force
        [ordered]@{path=$file.FullName;sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash;bytes=$file.Length}
    })
}
try {
    $startupInputs = @(Get-StartupInputs)
    Save-StartupJson $startupInputs (Join-Path $startupOutput 'inputs-before.json')
    if(-not (Test-Path -LiteralPath $startupPackage -PathType Leaf)){throw 'The requested Charts nupkg is absent from the explicit local feed.'}
    $zip=[IO.Compression.ZipFile]::OpenRead($startupPackage)
    try {
        $entry=$zip.GetEntry('lib/net10.0/NT.AvaloniaControl.Charts.dll')
        if($null -eq $entry){throw 'Core package contains no net10.0 assembly.'}
        $stream=$entry.Open()
        try {$startupPayloadSha=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))} finally {$stream.Dispose()}
    } finally {$zip.Dispose()}
    [IO.Directory]::CreateDirectory($startupConsumer) | Out-Null
    foreach($file in @('Directory.Build.props','Directory.Build.targets','Directory.Packages.props')) {'<Project />' | Set-Content -LiteralPath (Join-Path $startupConsumer $file) -Encoding utf8}
    Copy-Item -LiteralPath (Join-Path $startupRepository 'global.json') -Destination (Join-Path $startupConsumer 'global.json')
    $project=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Consumers/ChartsNativeTextStartup/Consumer.csproj.template') -Raw).Replace('__CHARTS_VERSION__',$PackageVersion).Replace('__AVALONIA_VERSION__',$startupAvalonia)
    $project | Set-Content -LiteralPath (Join-Path $startupConsumer 'Consumer.csproj') -Encoding utf8
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Consumers/ChartsNativeTextStartup/Program.cs') -Destination (Join-Path $startupConsumer 'Program.cs')
    $feedXml=[Security.SecurityElement]::Escape($startupFeed)
    $dependencySources='';$dependencyMappings='';$dependencyIndex=0
    foreach($dependency in $DependencyFeed){
        $dependencyPath=[Security.SecurityElement]::Escape([IO.Path]::GetFullPath($dependency))
        $dependencySources+="<add key='dependency-$dependencyIndex' value='$dependencyPath'/>"
        $dependencyMappings+="<packageSource key='dependency-$dependencyIndex'><package pattern='*'/></packageSource>"
        $dependencyIndex++
    }
    "<configuration><packageSources><clear/><add key='local' value='$feedXml'/>$dependencySources<add key='nuget.org' value='https://api.nuget.org/v3/index.json'/></packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders><packageSourceMapping><packageSource key='local'><package pattern='NT.AvaloniaControl.Charts'/></packageSource>$dependencyMappings<packageSource key='nuget.org'><package pattern='*'/></packageSource></packageSourceMapping></configuration>" | Set-Content -LiteralPath (Join-Path $startupConsumer 'NuGet.Config') -Encoding utf8
    $packages=Join-Path $startupConsumer 'packages'
    $environment=@{NUGET_PACKAGES=$packages;NUGET_HTTP_CACHE_PATH=(Join-Path $startupConsumer 'http-cache');DOTNET_CLI_HOME=(Join-Path $startupConsumer 'dotnet-home');DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'}
    foreach($stage in @(
        @{id='sdk';args=@('--version')},
        @{id='restore';args=@('restore','Consumer.csproj','--configfile','NuGet.Config','--packages',$packages,'--disable-parallel')},
        @{id='locked-restore';args=@('restore','Consumer.csproj','--locked-mode','--configfile','NuGet.Config','--packages',$packages,'--disable-parallel')},
        @{id='build';args=@('build','Consumer.csproj','-c','Release','--no-restore','--no-incremental','--disable-build-servers','-m:1','-p:UseSharedCompilation=false')}
    )) { Assert-StartupStage (Invoke-StartupStage $stage.id 'dotnet' $stage.args $startupConsumer $environment) }
    $assets=Get-Content -LiteralPath (Join-Path $startupConsumer 'obj/project.assets.json') -Raw | ConvertFrom-Json
    if(@($assets.libraries.PSObject.Properties | Where-Object {$_.Value.type -eq 'project'}).Count){throw 'Package consumer resolved a source project reference.'}
    $bin=Join-Path $startupConsumer 'bin/Release/net10.0'
    $core=Join-Path $bin 'NT.AvaloniaControl.Charts.dll'
    if((Get-FileHash -LiteralPath $core -Algorithm SHA256).Hash -ne $startupPayloadSha){throw 'Consumer core differs from the local nupkg payload.'}
    $plan=@(@{id='normal';mode='normal';preload=$false;diagnostic=$false},@{id='idempotent';mode='idempotent';preload=$false;diagnostic=$false})
    if($IsLinux){$plan+=@(
        @{id='preloaded-system-api';mode='normal';preload=$true;diagnostic=$false},
        @{id='missing';mode='missing';preload=$false;diagnostic=$false},
        @{id='late';mode='late';preload=$false;diagnostic=$false},
        @{id='native-loaded';mode='native-loaded';preload=$false;diagnostic=$false},
        @{id='resolver-conflict';mode='resolver-conflict';preload=$false;diagnostic=$false},
        @{id='baseline-system';mode='baseline';preload=$true;diagnostic=$true}
    )}
    Save-StartupJson $plan (Join-Path $startupOutput 'plan.json')
    foreach($case in $plan){
        $caseRoot=Join-Path $startupOutput "cases/$($case.id)"
        [IO.Directory]::CreateDirectory($caseRoot) | Out-Null
        $caseBin=$bin
        if($case.mode -eq 'missing'){
            # Only this owned cloned output can be temporarily moved by the fixture.
            $caseBin=Join-Path $caseRoot 'owned-bin'
            Copy-Item -LiteralPath $bin -Destination $caseBin -Recurse
        }
        $evidence=Join-Path $caseRoot 'evidence'
        $arguments=@((Join-Path $caseBin 'Consumer.dll'),'--output',$evidence,'--mode',$case.mode)
        if($case.preload){$arguments+=@('--preload-system-harfbuzz',$SystemHarfBuzzPath)}
        $caseEnvironment=@{}
        foreach($name in $environment.Keys){$caseEnvironment[$name]=$environment[$name]}
        $caseEnvironment['XDG_CACHE_HOME']=Join-Path $caseRoot 'fresh-xdg-cache'
        $caseEnvironment['XDG_CONFIG_HOME']=Join-Path $caseRoot 'fresh-xdg-config'
        $caseEnvironment['DOTNET_CLI_HOME']=Join-Path $caseRoot 'fresh-dotnet-home'
        $caseEnvironment['TMPDIR']=Join-Path $caseRoot 'fresh-tmp'
        if($IsWindows){$caseEnvironment['TEMP']=$caseEnvironment['TMPDIR'];$caseEnvironment['TMP']=$caseEnvironment['TMPDIR']}
        foreach($name in @('XDG_CACHE_HOME','XDG_CONFIG_HOME','DOTNET_CLI_HOME','TMPDIR')){[IO.Directory]::CreateDirectory($caseEnvironment[$name]) | Out-Null}
        $fontEvidence=$null
        if($IsLinux){
            $privateFontCache=Join-Path $caseRoot 'fresh-private-font-cache'
            [IO.Directory]::CreateDirectory($privateFontCache) | Out-Null
            $fontCacheBefore=@(Get-ChildItem -LiteralPath $privateFontCache -Force -Recurse -File).Count
            if($fontCacheBefore -ne 0){throw 'The private font cache must begin empty.'}
            $fontFile=Join-Path $caseRoot 'fonts.conf'
            $fontDirXml=if($FontDirectory){'<dir>'+[Security.SecurityElement]::Escape($FontDirectory)+'</dir>'}else{''}
            $fontCacheXml=[Security.SecurityElement]::Escape($privateFontCache)
            "<?xml version='1.0'?><!DOCTYPE fontconfig SYSTEM 'urn:fontconfig:fonts.dtd'><fontconfig><include ignore_missing='no'>/etc/fonts/fonts.conf</include>$fontDirXml<cachedir>$fontCacheXml</cachedir></fontconfig>" | Set-Content -LiteralPath $fontFile -Encoding utf8
            $caseEnvironment['FONTCONFIG_FILE']=$fontFile
            $caseEnvironment['CHARTS_STARTUP_FONT_CACHE']=$privateFontCache
            $fontEvidence=[ordered]@{fontconfigFile=$fontFile;fontconfigSha256=(Get-FileHash -LiteralPath $fontFile -Algorithm SHA256).Hash;privateCache=$privateFontCache;preexistingFiles=$fontCacheBefore;systemCachesPurged=$false;inheritedProjectFontConfigUsed=$false}
        }
        $receipt=Invoke-StartupStage "case-$($case.id)" 'dotnet' $arguments $caseBin $caseEnvironment
        $rawPath=Join-Path $evidence 'raw.json';$initialPath=Join-Path $evidence 'raw.initial.json'
        $row=[ordered]@{id=$case.id;mode=$case.mode;diagnosticOnly=$case.diagnostic;process=$receipt;passed=$false;rawAvailable=(Test-Path -LiteralPath $rawPath);nativeWindowValidated=$false;fontCache=$fontEvidence;error=$null}
        if($case.diagnostic){
            $preloadedPath=Join-Path $evidence 'raw.system-preloaded.json'
            $row['controlPreloadObserved']=$false
            if(Test-Path -LiteralPath $preloadedPath){
                $control=Get-Content -LiteralPath $preloadedPath -Raw | ConvertFrom-Json
                $row.controlPreloadObserved=($control.processId -eq $receipt.processId -and $control.apiRequested -eq $false -and $control.systemPreload.flags -eq 258 -and $control.systemPreload.actualVersion -ne '8.3.1' -and $control.systemPreload.path -eq $SystemHarfBuzzPath -and $control.systemPreload.symbolProviderPath -eq $SystemHarfBuzzPath -and $control.systemPreload.sha256 -eq $control.systemPreload.symbolProviderSha256 -and $control.systemPreload.sha256 -eq (Get-FileHash -LiteralPath $SystemHarfBuzzPath -Algorithm SHA256).Hash)
            }
            $row['outcome']='Diagnostic control only. A nonzero exit or missing final raw is preserved, not rewritten as a fix. Graphics/bootstrap failures do not alone identify a font cause.'
            $row.passed=$null
        } elseif($receipt.passed -and $row.rawAvailable){
            $oraclePath=Join-Path $caseRoot 'independent-oracle.json'
            $oracleReceipt=Invoke-StartupStage "oracle-$($case.id)" $PythonExecutable @((Join-Path $PSScriptRoot 'Consumers/ChartsNativeTextStartup/oracle.py'),'--directory',$evidence,'--scripts',$PSScriptRoot,'--output',$oraclePath,'--expected-platform',$(if($IsWindows){'windows'}else{'linux'}),'--expected-process-id',[string]$receipt.processId) $startupRepository
            $raw=Get-Content -LiteralPath $rawPath -Raw | ConvertFrom-Json
            $loadedCore=@($raw.loadedAssemblies | Where-Object name -eq 'NT.AvaloniaControl.Charts')
            $row.passed=($oracleReceipt.passed -and $raw.processId -eq $receipt.processId -and $loadedCore.Count -eq 1 -and $loadedCore[0].sha256 -eq $startupPayloadSha)
            if($IsLinux){$row.passed=($row.passed -and $raw.environment.fontconfigFile -eq $fontEvidence.fontconfigFile -and $raw.environment.fontconfigSha256 -eq $fontEvidence.fontconfigSha256 -and $raw.environment.privateFontCache -eq $fontEvidence.privateCache -and $fontEvidence.preexistingFiles -eq 0)}
            $row.nativeWindowValidated=($row.passed -and $case.mode -in @('normal','idempotent'))
            $row['oracle']=$oraclePath
        } else {$row.error='Required process or terminal raw failed; partial evidence retained without retry.'}
        $startupCases.Add($row)
    }
    $after=@(Get-StartupInputs);Save-StartupJson $after (Join-Path $startupOutput 'inputs-after.json')
    $sameInputs=($startupInputs | ConvertTo-Json -Depth 8 -Compress) -ceq ($after | ConvertTo-Json -Depth 8 -Compress)
    $required=@($startupCases | Where-Object {-not $_.diagnosticOnly})
    $controls=@($startupCases | Where-Object diagnosticOnly)
    $startupPassed=($sameInputs -and $required.Count -gt 0 -and @($required | Where-Object {-not $_.passed}).Count -eq 0 -and @($controls | Where-Object {-not $_.controlPreloadObserved}).Count -eq 0)
} catch {$startupError=$_.Exception.ToString()}
finally {
    Save-StartupJson ([ordered]@{schema='charts-native-text-package-startup-results/v1';startedUtc=$startupStarted;finishedUtc=[DateTimeOffset]::UtcNow;
        passed=$startupPassed;error=$startupError;platform=$(if($IsWindows){'windows'}elseif($IsLinux){'linux'}else{'other'});
        packagePath=$startupPackage;packageVersion=$PackageVersion;packageSha256=$(if(Test-Path -LiteralPath $startupPackage){(Get-FileHash -LiteralPath $startupPackage -Algorithm SHA256).Hash}else{$null});corePayloadSha256=$startupPayloadSha;
        cases=@($startupCases);processes=@($startupProcesses);noProjectReferences=$true;isolatedPackageCache=$true;
        physicalInputValidated=$false;performanceMeasured=$false;systemFontCachePurged=$false;ciUploadEnabled=$false;
        scope='Independent ordinary native Desktop package consumer. Fresh per-process XDG/DOTNET/TMP directories do not purge system-wide font caches. Baseline failures remain diagnostic observations, not successful runs.'}) (Join-Path $startupOutput 'results.json')
}
if(-not $startupPassed){throw "Charts native-text package startup verification failed; see $startupOutput"}
