[CmdletBinding()]
param([Parameter(Mandatory)][string]$SourceRoot,[Parameter(Mandatory)][string]$OutputDirectory,[string]$PackageVersion='0.2.0-preview.3',[switch]$IncludeDemo,[switch]$SkipConsumers)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 3
$root=[IO.Path]::GetFullPath($SourceRoot);$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Fresh evidence directory required'}
[IO.Directory]::CreateDirectory($output)|Out-Null
. (Join-Path $root 'scripts/ChartsPerformanceGate.Process.ps1')
$stages=[Collections.Generic.List[object]]::new()
$state=[ordered]@{schema='charts-preview-build/v1';platform=$(if($IsWindows){'windows'}else{'linux'});version=$PackageVersion;sourceRoot=$root;startedUtc=[DateTimeOffset]::UtcNow.ToString('O');finishedUtc=$null;passed=$false;stages=$stages;packages=@();error=$null}
function Save-State {$state|ConvertTo-Json -Depth 25|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8}
function Run-Stage([string]$Id,[string[]]$StageArguments,[int]$Timeout=900){
 $r=Invoke-ChartsGateProcess -Id $Id -Executable dotnet -Arguments $StageArguments -WorkingDirectory $root -OutputDirectory (Join-Path $output $Id) -TimeoutSeconds $Timeout
 $stages.Add($r);Save-State;if(!$r.passed){throw "Stage failed: $Id"}
}
Save-State
try{
 $version="-p:Version=$PackageVersion"
 Run-Stage 'sdk' @('--info')
 $tests='tests/Charts.Headless.Tests/Charts.Headless.Tests.csproj'
 Run-Stage 'restore-tests' @('restore',$tests,'--locked-mode','--disable-parallel','-m:1','-p:BuildInParallel=false',$version)
 Run-Stage 'build-tests' @('build',$tests,'-c','Release','--no-restore','--no-incremental','--disable-build-servers','-m:1','-nodeReuse:false','-p:UseSharedCompilation=false',$version)
 Run-Stage 'tests' @('test',$tests,'-c','Release','--no-restore','--no-build','--logger','trx;LogFileName=charts.trx','--results-directory',(Join-Path $output 'test-results'),$version) 1800
 $usage='samples/Charts.Usage/Charts.Usage.csproj'
 Run-Stage 'restore-usage' @('restore',$usage,'--locked-mode','--disable-parallel','-m:1','-p:BuildInParallel=false',$version)
 Run-Stage 'build-usage' @('build',$usage,'-c','Release','--no-restore','--no-incremental','--disable-build-servers','-m:1','-nodeReuse:false','-p:UseSharedCompilation=false',$version)
 $feed=Join-Path $output 'feed';[IO.Directory]::CreateDirectory($feed)|Out-Null
 $packages=[Collections.Generic.List[object]]::new()
 foreach($name in @('NT.AvaloniaControl.Charts','NT.AvaloniaControl.Charts.Themes','NT.AvaloniaControl.Charts.Skia')){
  Run-Stage "pack-$name" @('pack',"src/$name/$name.csproj",'-c','Release','--no-build','--no-restore','--disable-build-servers','-m:1',$version,'-o',$feed)
  $path=Join-Path $feed "$name.$PackageVersion.nupkg"
  if(!(Test-Path -LiteralPath $path)){throw 'Required package missing'}
  $packages.Add([ordered]@{id=$name;version=$PackageVersion;path=$path;bytes=(Get-Item -LiteralPath $path).Length;sha256=(Get-FileHash -LiteralPath $path).Hash})
 }
 $state.packages=@($packages);Save-State
 if(!$SkipConsumers){
  $r=Invoke-ChartsGateProcess -Id 'headless-package-consumers' -Executable pwsh -Arguments @('-NoProfile','-File',(Join-Path $root 'scripts/Test-ChartsPackageConsumer.ps1'),'-NoPack','-Feed',$feed,'-PackageVersion',$PackageVersion,'-OutputRoot',(Join-Path $output 'clean-consumers')) -WorkingDirectory $root -OutputDirectory (Join-Path $output 'headless-consumer-process') -TimeoutSeconds 3600
  $stages.Add($r);Save-State;if(!$r.passed){throw 'Clean package consumers failed'}
 }
 if($IncludeDemo){
  $demo='samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj'
  Run-Stage 'restore-demo' @('restore',$demo,'--locked-mode','--disable-parallel','-m:1','-p:BuildInParallel=false',$version) 1800
  Run-Stage 'build-demo' @('build',$demo,'-c','Release','--no-restore','--disable-build-servers','-m:1','-nodeReuse:false','-p:UseSharedCompilation=false',$version) 1800
 }
 $state.passed=$true
}catch{$state.error=$_.Exception.ToString()}
finally{$state.finishedUtc=[DateTimeOffset]::UtcNow.ToString('O');Save-State}
if(!$state.passed){throw $state.error}
