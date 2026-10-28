[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$Feed,[Parameter(Mandatory)][string]$OutputDirectory,
 [ValidateSet('core','themes','skia')][string]$Variant='themes',[string]$PackageVersion='0.2.0-preview.3',
 [ValidateSet('smoke','lifecycle','soak')][string]$Mode='smoke',[ValidateRange(1,30000)][int]$Seconds=10,
 [ValidateRange(1,1000)][int]$Cycles=1,[string[]]$DependencyFeed=@(),[string]$FontDirectory,
 [string]$ExistingConsumer,[string]$QualificationFile,[switch]$AllowPreparedConsumer
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 3
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent));$out=[IO.Path]::GetFullPath($OutputDirectory);$feedRoot=[IO.Path]::GetFullPath($Feed)
if(Test-Path -LiteralPath $out){throw 'Fresh output required'}
[IO.Directory]::CreateDirectory($out)|Out-Null
. (Join-Path $root 'scripts/ChartsPerformanceGate.Process.ps1')
$stages=[Collections.Generic.List[object]]::new()
$rid=if($IsWindows){'win-x64'}else{'linux-x64'}
$state=[ordered]@{schema='charts-preview-clean-consumer/v2';platform=$(if($IsWindows){'windows'}else{'linux'});variant=$Variant;version=$PackageVersion;mode=$Mode;seconds=$Seconds;cycles=$Cycles;freshCache=(!$ExistingConsumer);consumer=$null;runtimeIdentifier=$rid;publishedDirectory=$null;packageCacheRemovedBeforeExecution=$false;startedUtc=[DateTimeOffset]::UtcNow.ToString('O');finishedUtc=$null;passed=$false;stages=$stages;payloads=@();error=$null}
function Save-State {$state|ConvertTo-Json -Depth 25|Set-Content -LiteralPath (Join-Path $out 'execution.json') -Encoding utf8}
function Stage([string]$Id,[string[]]$Arguments,[string]$Work,[int]$Timeout=900){
 $r=Invoke-ChartsGateProcess -Id $Id -Executable dotnet -Arguments $Arguments -WorkingDirectory $Work -OutputDirectory (Join-Path $out $Id) -TimeoutSeconds $Timeout
 $stages.Add($r);Save-State;if(!$r.passed){throw "Stage failed: $Id"}
}
function Run-Long([string]$Assembly,[string]$Work){
 $dir=Join-Path $out 'native-process';[IO.Directory]::CreateDirectory($dir)|Out-Null
 $receipt=[ordered]@{schema='charts-preview-long-process/v1';startedUtc=[DateTimeOffset]::UtcNow.ToString('O');finishedUtc=$null;processId=$null;exitCode=$null;passed=$false;timeoutSeconds=($Seconds+1800);timedOut=$false;arguments=@($Assembly,'--output',(Join-Path $out 'run'),'--mode',$Mode,'--seconds',"$Seconds",'--cycles',"$Cycles");error=$null}
 $info=[Diagnostics.ProcessStartInfo]::new();$info.FileName='dotnet';$info.WorkingDirectory=$Work;$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
 foreach($arg in $receipt.arguments){$info.ArgumentList.Add($arg)}
 $child=[Diagnostics.Process]::new();$child.StartInfo=$info
 $stdout=[IO.File]::Open((Join-Path $dir 'stdout.log'),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
 $stderr=[IO.File]::Open((Join-Path $dir 'stderr.log'),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
 try{
  if(!$child.Start()){throw 'Process did not start'};$receipt.processId=$child.Id;$receipt.processStartedUtc=$child.StartTime.ToUniversalTime().ToString('O');$deadline=[DateTimeOffset]::UtcNow.AddSeconds($receipt.timeoutSeconds)
  $a=$child.StandardOutput.BaseStream.CopyToAsync($stdout);$b=$child.StandardError.BaseStream.CopyToAsync($stderr)
  do{
   $receipt.heartbeatUtc=[DateTimeOffset]::UtcNow.ToString('O');$receipt|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $dir 'process.json') -Encoding utf8
   if([DateTimeOffset]::UtcNow -gt $deadline){$receipt.timedOut=$true;$child.Kill($true);[void]$child.WaitForExit(10000);break}
  }while(!$child.WaitForExit(5000))
  [void]$a.GetAwaiter().GetResult();[void]$b.GetAwaiter().GetResult();$receipt.exitCode=$child.ExitCode;$receipt.passed=(!$receipt.timedOut -and $child.ExitCode -eq 0)
 }catch{$receipt.error=$_.Exception.ToString();try{if(!$child.HasExited){$child.Kill($true)}}catch{}}
 finally{$stdout.Dispose();$stderr.Dispose();$child.Dispose();$receipt.finishedUtc=[DateTimeOffset]::UtcNow.ToString('O');$receipt|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $dir 'process.json') -Encoding utf8}
 $stages.Add($receipt);Save-State;if(!$receipt.passed){throw 'Native long process did not pass'}
}
$environmentBefore=@{}
Save-State
try{
 $packageIds=switch($Variant){'core'{@('NT.AvaloniaControl.Charts')};'themes'{@('NT.AvaloniaControl.Charts.Themes')};'skia'{@('NT.AvaloniaControl.Charts.Themes','NT.AvaloniaControl.Charts.Skia')}}
 if($ExistingConsumer){
  $consumer=[IO.Path]::GetFullPath($ExistingConsumer)
  $proof=if($QualificationFile){[IO.Path]::GetFullPath($QualificationFile)}else{Join-Path (Split-Path $consumer -Parent) 'execution.json'}
  $qualified=Get-Content -LiteralPath $proof -Raw|ConvertFrom-Json
  $prepared=$AllowPreparedConsumer -and @($qualified.stages|Where-Object {!$_.passed}).Count -eq 0 -and $qualified.publishedDirectory -and (Test-Path -LiteralPath $qualified.publishedDirectory)
  if((!$qualified.passed -and !$prepared) -or $qualified.variant -cne $Variant -or $qualified.version -cne $PackageVersion -or [IO.Path]::GetFullPath($qualified.consumer) -cne $consumer){throw 'Existing consumer must have passed matching smoke or have explicitly qualified successful build/publish stages'}
  $state.preparedConsumerEvidence=[ordered]@{path=$proof;sha256=(Get-FileHash -LiteralPath $proof).Hash;freshCache=$qualified.freshCache;preparedBuildOnly=[bool]$prepared;previousPassed=$qualified.passed}
 }else{
  $consumer=Join-Path $out 'consumer';[IO.Directory]::CreateDirectory($consumer)|Out-Null
  foreach($n in @('Directory.Build.props','Directory.Build.targets','Directory.Packages.props')){'<Project />'|Set-Content -LiteralPath (Join-Path $consumer $n) -Encoding utf8}
  Copy-Item -LiteralPath (Join-Path $root 'global.json') -Destination $consumer
  $refs=($packageIds|ForEach-Object {'<PackageReference Include="'+$_+'" Version="'+$PackageVersion+'" />'}) -join "`n"
  $symbols=if($Variant -eq 'core'){'CHARTS_PREVIEW_CORE'}elseif($Variant -eq 'themes'){'CHARTS_PREVIEW_THEME'}else{'CHARTS_PREVIEW_THEME;CHARTS_PREVIEW_SKIA'}
  $project=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Consumers/ChartsPreview/Consumer.csproj.template')).Replace('__PACKAGES__',$refs).Replace('__SYMBOLS__',$symbols).Replace('__RID__',$rid)
  [IO.File]::WriteAllText((Join-Path $consumer 'Consumer.csproj'),$project)
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Consumers/ChartsPreview/Program.cs') -Destination $consumer
  $sources='<add key="release" value="'+[Security.SecurityElement]::Escape($feedRoot)+'"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/>'
  $maps='<packageSource key="release"><package pattern="NT.AvaloniaControl.Charts*"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource>'
  for($i=0;$i -lt $DependencyFeed.Count;$i++){$sources+='<add key="dependency-'+$i+'" value="'+[Security.SecurityElement]::Escape([IO.Path]::GetFullPath($DependencyFeed[$i]))+'"/>';$maps+='<packageSource key="dependency-'+$i+'"><package pattern="*"/></packageSource>'}
  '<configuration><packageSources><clear/>'+ $sources+'</packageSources><fallbackPackageFolders><clear/></fallbackPackageFolders><packageSourceMapping>'+$maps+'</packageSourceMapping></configuration>'|Set-Content -LiteralPath (Join-Path $consumer 'NuGet.Config') -Encoding utf8
 }
 $state.consumer=$consumer
 $envs=@{NUGET_PACKAGES=(Join-Path $consumer 'packages');NUGET_HTTP_CACHE_PATH=(Join-Path $consumer 'http-cache');DOTNET_CLI_HOME=(Join-Path $consumer 'dotnet-home');DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'}
 foreach($key in $envs.Keys){$environmentBefore[$key]=[Environment]::GetEnvironmentVariable($key,'Process');[Environment]::SetEnvironmentVariable($key,$envs[$key],'Process')}
 if(!$ExistingConsumer){
  if(Test-Path -LiteralPath $envs.NUGET_PACKAGES){throw 'Package cache must begin absent'}
  Stage 'restore' @('restore','Consumer.csproj','--configfile','NuGet.Config','--disable-parallel') $consumer
  Stage 'locked-restore' @('restore','Consumer.csproj','--locked-mode','--configfile','NuGet.Config','--disable-parallel') $consumer
  Stage 'build' @('build','Consumer.csproj','-c','Release','--no-restore','--no-incremental','--disable-build-servers','-m:1','-nodeReuse:false','-p:UseSharedCompilation=false') $consumer
  Stage 'publish' @('publish','Consumer.csproj','-c','Release','--no-build','--no-restore','--self-contained','false','-o',(Join-Path $consumer 'published')) $consumer
 }
 $assets=Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw|ConvertFrom-Json
 if(@($assets.libraries.PSObject.Properties|Where-Object {$_.Value.type -eq 'project'}).Count){throw 'Source project reference found'}
 $bin=Join-Path $consumer 'published';if($ExistingConsumer -and $qualified.publishedDirectory){$bin=$qualified.publishedDirectory}
 $state.publishedDirectory=$bin
 $payloads=[Collections.Generic.List[object]]::new()
 foreach($id in @('NT.AvaloniaControl.Charts')+@($packageIds|Where-Object {$_ -ne 'NT.AvaloniaControl.Charts'})){
  $package=Join-Path $feedRoot "$id.$PackageVersion.nupkg";$zip=[IO.Compression.ZipFile]::OpenRead($package)
  try{$entry=$zip.GetEntry("lib/net10.0/$id.dll");if(!$entry){throw 'Package DLL missing'};$stream=$entry.Open();try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}}finally{$zip.Dispose()}
  if((Get-FileHash -LiteralPath (Join-Path $bin "$id.dll")).Hash -cne $hash){throw 'Consumer DLL does not match package'}
  $payloads.Add([ordered]@{id=$id;package=$package;dllSha256=$hash;packageSha256=(Get-FileHash -LiteralPath $package).Hash})
 }
 $state.payloads=@($payloads);Save-State
 $cache=$envs.NUGET_PACKAGES
 if(!$ExistingConsumer -and (Test-Path -LiteralPath $cache)){
  $cacheResolved=(Resolve-Path -LiteralPath $cache).ProviderPath
  $consumerResolved=(Resolve-Path -LiteralPath $consumer).ProviderPath
  if(!$cacheResolved.StartsWith($consumerResolved+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $cacheResolved -Leaf) -cne 'packages'){throw 'Generated cache cleanup target outside this consumer refused'}
  $metadata=@(Get-ChildItem -LiteralPath $cacheResolved -File -Filter '*.nupkg.sha512' -Recurse|ForEach-Object {[ordered]@{relative=[IO.Path]::GetRelativePath($cacheResolved,$_.FullName);contentHash=[IO.File]::ReadAllText($_.FullName).Trim()}})
  $metadata|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $out 'restored-package-content-hashes.json') -Encoding utf8
  $cleanupErrors=@()
  Remove-Item -LiteralPath $cacheResolved -Recurse -Force -ErrorAction SilentlyContinue -ErrorVariable cleanupErrors
  if($cleanupErrors.Count){$state.cacheCleanupWarning=($cleanupErrors|ForEach-Object ToString)-join "`n"}
  $state.packageCacheRemovedBeforeExecution=!(Test-Path -LiteralPath $cacheResolved);Save-State
 }
 $httpCache=Join-Path $consumer 'http-cache'
 if(!$ExistingConsumer -and (Test-Path -LiteralPath $httpCache)){
  $resolvedHttp=(Resolve-Path -LiteralPath $httpCache).ProviderPath
  if(!$resolvedHttp.StartsWith((Resolve-Path -LiteralPath $consumer).ProviderPath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolvedHttp -Leaf) -cne 'http-cache'){throw 'HTTP cache cleanup outside this consumer refused'}
  Remove-Item -LiteralPath $resolvedHttp -Recurse -Force -ErrorAction SilentlyContinue
 }
 # The deployed directory is executed with an unused package-cache location.
 # A build-services collector can briefly hold a generated tool DLL on Windows.
 [Environment]::SetEnvironmentVariable('NUGET_PACKAGES',(Join-Path $out 'runtime-unused-package-cache'),'Process')
 $environmentBefore.XDG_CACHE_HOME=[Environment]::GetEnvironmentVariable('XDG_CACHE_HOME','Process');[Environment]::SetEnvironmentVariable('XDG_CACHE_HOME',(Join-Path $out 'font-cache'),'Process')
 if($IsLinux -and $FontDirectory){
  $environmentBefore.FONTCONFIG_FILE=[Environment]::GetEnvironmentVariable('FONTCONFIG_FILE','Process');$config=Join-Path $out 'fontconfig.xml'
  '<fontconfig><cachedir>'+[Security.SecurityElement]::Escape((Join-Path $out 'font-cache'))+'</cachedir><include ignore_missing="yes">/etc/fonts/fonts.conf</include><dir>'+[Security.SecurityElement]::Escape($FontDirectory)+'</dir></fontconfig>'|Set-Content -LiteralPath $config -Encoding utf8
  [Environment]::SetEnvironmentVariable('FONTCONFIG_FILE',$config,'Process')
 }
 if($Seconds -gt 5400){Run-Long (Join-Path $bin 'Charts.PreviewConsumer.dll') $bin}else{Stage 'native-process' @((Join-Path $bin 'Charts.PreviewConsumer.dll'),'--output',(Join-Path $out 'run'),'--mode',$Mode,'--seconds',"$Seconds",'--cycles',"$Cycles") $bin ([Math]::Max(300,$Seconds+120))}
 $raw=Get-Content -LiteralPath (Join-Path $out 'run/result.json') -Raw|ConvertFrom-Json
 if(!$raw.passed -or $raw.status -cne 'completed' -or $raw.completedCycles -lt $Cycles -or $raw.elapsedSeconds -lt $Seconds){throw 'Raw native checks incomplete'}
 $state.passed=$true
}catch{$state.error=$_.Exception.ToString()}
finally{foreach($key in $environmentBefore.Keys){[Environment]::SetEnvironmentVariable($key,$environmentBefore[$key],'Process')};$state.finishedUtc=[DateTimeOffset]::UtcNow.ToString('O');Save-State}
if(!$state.passed){throw $state.error}
