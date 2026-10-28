[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('development','formal')][string]$Phase='development',
    [string]$PythonExecutable,
    [string]$FontDirectory,
    [switch]$FirstUseOnly
)
$ErrorActionPreference='Stop'
$c7Root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$c7Output=[IO.Path]::GetFullPath($OutputDirectory)
if($FirstUseOnly -and $Phase -eq 'formal'){throw 'Formal campaigns require interaction and first-use coverage together; FirstUseOnly is for development.'}
if(Test-Path -LiteralPath $c7Output){throw 'Use a new C7 campaign output directory; failures are never overwritten.'}
if($FontDirectory -and !(Test-Path -LiteralPath $FontDirectory -PathType Container)){throw 'The explicit font directory does not exist.'}
if(!$PythonExecutable){$PythonExecutable=if($IsWindows){'python'}else{'python3'}}
[IO.Directory]::CreateDirectory($c7Output)|Out-Null
. (Join-Path $PSScriptRoot 'ChartsPerformanceGate.Process.ps1')
$c7ProtocolPath=Join-Path $PSScriptRoot 'charts-c7-performance-protocol.json'
$c7Protocol=Get-Content -LiteralPath $c7ProtocolPath -Raw|ConvertFrom-Json
$c7Processes=[Collections.Generic.List[object]]::new()
$c7Runs=[Collections.Generic.List[object]]::new()
$c7FailedNative=[Collections.Generic.List[object]]::new()
$c7Started=[DateTimeOffset]::UtcNow
$c7Error=$null
$c7Passed=$false
$c7Platform=if($IsWindows){'windows'}elseif($IsLinux){'linux'}else{throw 'This campaign supports Windows and Linux only.'}
function Get-C7Inputs {
 $paths=@(& rg --files --hidden src/NT.AvaloniaControl.Charts src/NT.AvaloniaControl.Charts.Skia src/NT.AvaloniaControl.Charts.Themes benchmarks/Charts.NativeBenchmarks -g '!**/bin/**' -g '!**/obj/**')+@('benchmarks/Charts.Benchmarks/DatasetFactory.cs','Directory.Build.props','Directory.Packages.props','global.json','NuGet.Config','docs/charts-performance-budgets.json','scripts/Invoke-ChartsC7Performance.ps1','scripts/ChartsPerformanceGate.Process.ps1','scripts/charts-c7-performance-protocol.json','scripts/charts_c7_performance_oracle.py','scripts/charts_png_oracle.py')
 if($LASTEXITCODE -ne 0){throw 'C7 input enumeration failed.'}
 @(foreach($relative in @($paths|Sort-Object -CaseSensitive -Unique)){$p=Join-Path $c7Root $relative;[ordered]@{path=$relative.Replace('\','/');bytes=(Get-Item -LiteralPath $p -Force).Length;sha256=(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}})
}
function Save-C7([string]$Name,$Value){$Value|ConvertTo-Json -Depth 50|Set-Content -LiteralPath (Join-Path $c7Output $Name) -Encoding utf8}
function Stage-C7([string]$Id,[string]$Executable,[string[]]$Arguments,[int]$Timeout=900){
 $record=Invoke-ChartsGateProcess -Id $Id -Executable $Executable -Arguments $Arguments -WorkingDirectory $c7Root -OutputDirectory (Join-Path $c7Output "processes/$Id") -TimeoutSeconds $Timeout
 $c7Processes.Add($record)
 return $record
}
function Native-C7([string]$Id,[string]$Kind,[string]$Role,[string[]]$Tracks,[double]$Warmup,[double]$Measure){
 $hostOutput=Join-Path $c7Output "runs/$Id"
 $argsC7=@($script:c7Host,'--c7-performance','--output',$hostOutput,'--source-root',$c7Root,'--c7-renderer','software','--c7-warmup-seconds',$Warmup.ToString([Globalization.CultureInfo]::InvariantCulture),'--c7-measure-seconds',$Measure.ToString([Globalization.CultureInfo]::InvariantCulture),'--c7-scenarios',($Tracks -join ','))
 if($Kind -eq 'first-use'){$argsC7+='--c7-first-use-only'}
 $envNames=@('XDG_CACHE_HOME','XDG_CONFIG_HOME','DOTNET_CLI_HOME','TMPDIR','FONTCONFIG_FILE','AVALONIA_GLOBAL_SCALE_FACTOR','AVALONIA_SCREEN_SCALE_FACTORS')
 $before=@{}
 foreach($name in $envNames){$before[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
 try{
  $cache=Join-Path $c7Output "private-caches/$Id"
  foreach($name in @('XDG_CACHE_HOME','XDG_CONFIG_HOME','DOTNET_CLI_HOME','TMPDIR')){$dir=Join-Path $cache $name;[IO.Directory]::CreateDirectory($dir)|Out-Null;[Environment]::SetEnvironmentVariable($name,$dir,'Process')}
  [Environment]::SetEnvironmentVariable('AVALONIA_GLOBAL_SCALE_FACTOR',$null,'Process');[Environment]::SetEnvironmentVariable('AVALONIA_SCREEN_SCALE_FACTORS',$null,'Process')
  if($IsLinux){
   $privateFontCache=Join-Path $cache 'font-cache';[IO.Directory]::CreateDirectory($privateFontCache)|Out-Null
   $fontFile=Join-Path $cache 'fonts.conf';$extra=if($FontDirectory){'<dir>'+[Security.SecurityElement]::Escape([IO.Path]::GetFullPath($FontDirectory))+'</dir>'}else{''}
   [IO.File]::WriteAllText($fontFile,"<?xml version='1.0'?><!DOCTYPE fontconfig SYSTEM 'urn:fontconfig:fonts.dtd'><fontconfig><cachedir>$([Security.SecurityElement]::Escape($privateFontCache))</cachedir><include ignore_missing='no'>/etc/fonts/fonts.conf</include>$extra</fontconfig>")
   $env:FONTCONFIG_FILE=$fontFile
  }
  $row=[ordered]@{kind=$Kind;role=$Role;raw="runs/$Id/raw.json";receipt="processes/$Id/process.json"}
  $c7Runs.Add($row)
  Save-C7 'campaign.json' (Campaign-C7)
  $receipt=Stage-C7 $Id 'dotnet' $argsC7 900
  if(!$receipt.passed){$c7FailedNative.Add([ordered]@{id=$Id;exitCode=$receipt.exitCode;timedOut=$receipt.timedOut;terminalRawPresent=(Test-Path -LiteralPath (Join-Path $hostOutput 'raw.json'))})}
 }finally{foreach($name in $envNames){[Environment]::SetEnvironmentVariable($name,$before[$name],'Process')}}
}
function Campaign-C7 {
 [ordered]@{schemaVersion='charts-c7-performance-campaign/v1';platform=$c7Platform;phase=$Phase;protocolSha256=(Get-FileHash -LiteralPath $c7ProtocolPath -Algorithm SHA256).Hash;expectedSources=$script:c7Expected;sourceManifest=@{path='source-before.json';sha256=(Get-FileHash -LiteralPath (Join-Path $c7Output 'source-before.json') -Algorithm SHA256).Hash};runs=@($c7Runs);reference=@{status='blocked-reference';measurements=$null;ratios=$null;reason='User confirmed no valid official Charts license.'};present=@{status='blocked-present';measurements=$null};physicalValidation=@{status='deferred-by-user';scope=@('target physical desktops','hardware GPU','physical touch')};cacheQualification='New process-private caches; system-wide caches are not purged.'}
}
Push-Location $c7Root
try{
 $c7Before=@(Get-C7Inputs);Save-C7 'source-before.json' $c7Before
 $script:c7Expected=@{}
 Save-C7 'campaign.json' (Campaign-C7)
 $sdk=Stage-C7 'sdk' 'dotnet' @('--info') 30
 if(!$sdk.passed){throw 'C7 SDK inventory failed.'}
 $project='benchmarks/Charts.NativeBenchmarks/Charts.NativeBenchmarks.csproj'
 foreach($build in @(@{id='restore';args=@('restore',$project,'--locked-mode','--disable-parallel','-p:EnableC7Performance=true')},@{id='build';args=@('build',$project,'-c','Release','--no-restore','--no-incremental','--disable-build-servers','-m:1','-p:UseSharedCompilation=false','-p:EnableC7Performance=true')})){
  $r=Stage-C7 $build.id 'dotnet' $build.args
  if(!$r.passed){throw "C7 $($build.id) failed."}
 }
 $bin=Join-Path $c7Root 'benchmarks/Charts.NativeBenchmarks/bin/Release/net10.0'
 $script:c7Host=Join-Path $bin 'Charts.NativeBenchmarks.dll'
 $script:c7Expected=@{chartsSha256=(Get-FileHash -LiteralPath (Join-Path $bin 'NT.AvaloniaControl.Charts.dll') -Algorithm SHA256).Hash;harnessSha256=(Get-FileHash -LiteralPath $script:c7Host -Algorithm SHA256).Hash;nativeBackendSha256=(Get-FileHash -LiteralPath (Join-Path $bin 'NT.AvaloniaControl.Charts.Skia.dll') -Algorithm SHA256).Hash}
 $archive=Join-Path $c7Output 'measured-assemblies';[IO.Directory]::CreateDirectory($archive)|Out-Null
 $assemblyProof=@(foreach($name in @('Charts.NativeBenchmarks.dll','Charts.NativeBenchmarks.runtimeconfig.json','Charts.NativeBenchmarks.deps.json','NT.AvaloniaControl.Charts.dll','NT.AvaloniaControl.Charts.Skia.dll','NT.AvaloniaControl.Charts.Themes.dll','Avalonia.Base.dll')){
  $original=Join-Path $bin $name;$copy=Join-Path $archive $name
  Copy-Item -LiteralPath $original -Destination $copy
  $hash=(Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
  if((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $hash){throw 'Measured assembly archive mismatch.'}
  [ordered]@{name=$name;path="measured-assemblies/$name";sha256=$hash;bytes=(Get-Item -LiteralPath $copy).Length}
 })
 Save-C7 'measured-assemblies.json' $assemblyProof
 $formal=$Phase -eq 'formal'
 if(!$FirstUseOnly){
  foreach($i in 1..$(if($formal){5}else{1})){
   Native-C7 "interaction-$i" 'interaction' $(if($formal){'formal'}else{'development'}) @($c7Protocol.formal.interactionTracks) $(if($formal){10}else{2}) $(if($formal){30}else{3})
  }
 }
 if($formal){
  foreach($i in 1..2){Native-C7 "first-calibration-$i" 'first-use' 'calibration' @('S1M-L-minmax') 10 30}
  foreach($i in 1..100){Native-C7 "first-$i" 'first-use' 'formal' @('S1M-L-minmax') 10 30}
 }else{Native-C7 'first-development' 'first-use' 'development' @('S1M-L-minmax') 2 3}
 $c7After=@(Get-C7Inputs);Save-C7 'source-after.json' $c7After
 if(($c7Before|ConvertTo-Json -Depth 8 -Compress) -cne ($c7After|ConvertTo-Json -Depth 8 -Compress)){throw 'C7 measured inputs changed.'}
 Save-C7 'campaign.json' (Campaign-C7)
 $guard=Stage-C7 'independent-oracle' $PythonExecutable @((Join-Path $PSScriptRoot 'charts_c7_performance_oracle.py'),'--campaign',(Join-Path $c7Output 'campaign.json'),'--protocol',$c7ProtocolPath,'--output',(Join-Path $c7Output 'oracle.json')) 300
 if(!$guard.passed){throw 'Independent C7 campaign validation failed.'}
 $c7Passed=$c7FailedNative.Count -eq 0
 if(!$c7Passed){$c7Error='One or more planned native processes failed. All planned replicas and independent evidence checks are retained; see failedNativeProcesses.'}
}catch{$c7Error=$_.Exception.ToString()}
finally{
 if(Test-Path -LiteralPath (Join-Path $c7Output 'source-before.json')){Save-C7 'campaign.json' (Campaign-C7)}
 Save-C7 'results.json' ([ordered]@{schema='charts-c703-execution/v1';startedUtc=$c7Started.ToString('o');finishedUtc=[DateTimeOffset]::UtcNow.ToString('o');platform=$c7Platform;phase=$Phase;passed=$c7Passed;error=$c7Error;failedNativeProcesses=@($c7FailedNative);processes=@($c7Processes);scope='Controlled software raster/source/hit measurements. Overall integrity success is not formal frame/present/official/hardware acceptance.';officialComparisonPassed=$false;presentQualified=$false;physicalHardwareValidated=$false})
 Pop-Location
}
if(!$c7Passed){throw "C7 campaign did not pass: $c7Error"}
