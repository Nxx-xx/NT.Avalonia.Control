[CmdletBinding()]
param(
    [ValidateSet('Smoke','Formal')][string]$Mode='Smoke',
    [string]$OutputDirectory,
    [string]$SourceRoot=(Split-Path $PSScriptRoot -Parent),
    [string]$SourceHostPath,
    [string]$DotnetPath,
    [switch]$NoBuild
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Test-MarkdownNative.Helpers.ps1')
$source=(Get-Item -LiteralPath $SourceRoot -Force).FullName
$platform=if([OperatingSystem]::IsWindows()){'Windows'}elseif([OperatingSystem]::IsLinux()){'Linux'}else{throw 'Only native Windows and Linux are supported by this runner.'}
$budget=Get-MdNativePolicyBudget $platform
if(-not $OutputDirectory){$OutputDirectory=Join-Path $source ('artifacts/markdown-native/'+$Mode.ToLowerInvariant()+'-'+[guid]::NewGuid().ToString('N'))}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Preserve existing native evidence; select a fresh output directory.'}
foreach($name in @('src','samples','tests','scripts','benchmarks','docs','.github')){
    $protected=Join-Path $source $name
    if($output -eq $protected -or $output.StartsWith($protected+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Native evidence must not be written into a source directory.'}
}
$null=New-Item -ItemType Directory -Path $output
$stages=[Collections.Generic.List[object]]::new()
$rounds=[Collections.Generic.List[object]]::new()
$buildState=New-MdNativeBuildState -NoBuild ([bool]$NoBuild)
$entry=[ordered]@{
    Mode='markdown-native-ci';Action=$Mode;Platform=$platform;StartedUtc=[DateTimeOffset]::UtcNow.ToString('O');CompletedUtc=$null;
    Status='Failed';Passed=$false;SourceRoot=$source;SourceFilesUnchanged=$false;
    SourceBuildRequested=$buildState.SourceBuildRequested;SourceBuildPerformed=$buildState.SourceBuildPerformed;
    BuildStarted=$buildState.BuildStarted;BuildSucceeded=$buildState.BuildSucceeded;
    ProducerLongUiP95BudgetMilliseconds=50;AcceptedLongUiP95BudgetMilliseconds=$budget;
    PolicyScope='Linux51ms applies only to long ALLactual and rendered-subset UI P95. Windows50ms and every other producer/guard threshold are unchanged.';
    FormalRoundsRequired=if($Mode -eq 'Formal'){5}else{0};FormalSamples=100;ShortSessions=100;LongSessions=100;FormalWarmups=10;
    Processes=$stages;Rounds=$rounds;Smoke=$null;Guard=$null;FourDLLs=$null;Failure=$null;
    PhysicalInputVerified=$false;AssistiveToolsVerified=$false;DpiMatrixVerified=$false;KylinVerified=$false;
    Boundary='A native Window/CompositionBatch result is not physical input, scan-out, all devices, or a remote GitHub execution claim. NoBuild records supplied binaries and current source separately; it does not prove they were built from that source.'
}

function Save-NativeJson($Value,[string]$Path){$Value|ConvertTo-Json -Depth 18|Set-Content -LiteralPath $Path -Encoding utf8}
function Source-Files {
    return @(Get-MdNativeSourceFiles -SourceRoot $source)
}
function Assert-NativeSource($Expected){
    $current=@(Source-Files)
    if($current.Count -ne $Expected.Count){throw 'Native source input file count changed.'}
    for($i=0;$i -lt $Expected.Count;$i++){if($current[$i].Path -cne $Expected[$i].Path -or $current[$i].SHA256 -cne $Expected[$i].SHA256){throw 'Native source inputs changed during execution.'}}
    return ,$current
}
function Run-NativeChild([string]$Name,[string]$Executable,[string[]]$Arguments,[int]$Seconds,[string]$Working=$source){
    $result=Invoke-MdNativeProcess -Executable $Executable -ArgumentList $Arguments -WorkingDirectory $Working -OutputDirectory (Join-Path $output $Name) -TimeoutSeconds $Seconds
    $stages.Add([pscustomobject]@{Name=$Name;Evidence=(Join-Path $output ($Name+'/supervisor-result.json'));Result=$result})
    return $result
}
function Require-NativeSuccess($Result,[string]$Name){if(-not (Test-MdNativeCompletedProcess $Result) -or $Result.ExitCode -ne 0){throw "$Name failed; see its owned-process supervisor/log evidence."}}
function Read-NativeReport([string]$Path,$Process){
    if(-not(Test-Path -LiteralPath $Path)){throw 'The owned native process produced no report.'}
    $sha=(Get-FileHash -LiteralPath $Path).Hash
    $value=Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json
    $stamp=if($Mode -eq 'Smoke'){$value.时间UTC}else{$value.创建时间UTC}
    if(-not $stamp -or [DateTimeOffset]$stamp -lt [DateTimeOffset]$Process.StartedUtc -or [DateTimeOffset]$stamp -gt [DateTimeOffset]$Process.CompletedUtc){throw 'Native report timestamp does not belong to the owned process window.'}
    if((Get-FileHash -LiteralPath $Path).Hash -cne $sha){throw 'Native report changed during read.'}
    return [pscustomobject]@{Path=$Path;SHA256=$sha;Value=$value}
}

$before=$null;$frozenHashes=$null;$app=$null
try{
    if(-not $DotnetPath){$DotnetPath=Resolve-MdNativeApplication -Name 'dotnet'}
    $dotnet=Get-MdNativeCanonicalFilePath $DotnetPath
    $shell=Get-MdNativeCanonicalFilePath (Get-Process -Id $PID).Path
    $before=@(Source-Files)
    Save-NativeJson ([pscustomobject]@{CapturedUtc=[DateTimeOffset]::UtcNow.ToString('O');Files=$before}) (Join-Path $output 'source-before.json')
    $scriptHashes=Get-MdNativeFileHashes -Directory $PSScriptRoot -RelativePaths @('Test-MarkdownNative.ps1','Test-MarkdownNative.Helpers.ps1')
    $environment=[ordered]@{
        CapturedUtc=[DateTimeOffset]::UtcNow.ToString('O');Platform=$platform;OS=[Runtime.InteropServices.RuntimeInformation]::OSDescription;
        ProcessArchitecture=[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString();LogicalCpuCount=[Environment]::ProcessorCount;
        CpuIdentifier=$env:PROCESSOR_IDENTIFIER;DotnetExecutable=$dotnet;DotnetSHA256=(Get-FileHash -LiteralPath $dotnet).Hash;
        PowerShellExecutable=$shell;PowerShellVersion=$PSVersionTable.PSVersion.ToString();RunnerOS=$env:RUNNER_OS;RunnerName=$env:RUNNER_NAME;
        GitHubActions=$env:GITHUB_ACTIONS;GitHubSha=$env:GITHUB_SHA;GitHubRunId=$env:GITHUB_RUN_ID;
        DISPLAY=$env:DISPLAY;WAYLAND_DISPLAY=$env:WAYLAND_DISPLAY;XDG_SESSION_TYPE=$env:XDG_SESSION_TYPE;
        FONTCONFIG_FILE=$env:FONTCONFIG_FILE;LANG=$env:LANG;LC_ALL=$env:LC_ALL;LD_LIBRARY_PATH=$env:LD_LIBRARY_PATH;
        Scripts=$scriptHashes;NoBuild=[bool]$NoBuild;SourceBeforeSHA256=(Get-FileHash -LiteralPath (Join-Path $output 'source-before.json')).Hash
    }
    if($platform -eq 'Linux'){
        if(Test-Path -LiteralPath '/etc/os-release'){$environment.LinuxRelease=@(Get-Content -LiteralPath '/etc/os-release')}
        if(Test-Path -LiteralPath '/proc/cpuinfo'){$environment.CpuIdentifier=(@(Get-Content -LiteralPath '/proc/cpuinfo'|Where-Object {$_ -match '^(model name|Hardware)\s*:'}|Select-Object -First 1) -join '')}
        Save-NativeJson $environment (Join-Path $output 'environment.json')
        if(-not $env:DISPLAY){throw 'The current Avalonia Linux host requires an X11 desktop/DISPLAY (PR smoke may use Xvfb); WSL is not invoked.'}
        $fontCommand=Get-Command -Name 'fc-list' -CommandType Application -All -ErrorAction SilentlyContinue|Select-Object -First 1
        if($fontCommand){$fontPath=Resolve-MdNativeApplication -Name 'fc-list';$fontResult=Run-NativeChild 'font-environment' $fontPath @() 30;Require-NativeSuccess $fontResult 'Font inventory'}
    }
    Save-NativeJson $environment (Join-Path $output 'environment.json')
    $info=Run-NativeChild 'dotnet-info' $dotnet @('--info') 30;Require-NativeSuccess $info 'dotnet --info'
    $project=if($Mode -eq 'Smoke'){'samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj'}else{'benchmarks/Markdown.Benchmarks/NativeHost/Markdown.NativeBenchmarks.csproj'}
    $defaultHost=[IO.Path]::GetFullPath((Join-Path $source ((Split-Path $project -Parent)+'/bin/Release/net10.0')))
    $pathComparison=if($platform -eq 'Windows'){[StringComparison]::OrdinalIgnoreCase}else{[StringComparison]::Ordinal}
    if(-not $NoBuild -and $SourceHostPath -and -not [string]::Equals([IO.Path]::GetFullPath($SourceHostPath),$defaultHost,$pathComparison)){
        throw 'An external SourceHostPath requires -NoBuild; this runner cannot bind unrelated supplied binaries to its own build.'
    }
    if(-not $NoBuild){
        $restore=Run-NativeChild 'restore' $dotnet @('restore',(Join-Path $source $project),'--locked-mode') 600;Require-NativeSuccess $restore 'Locked restore'
        $build=Run-NativeChild 'build' $dotnet @('build',(Join-Path $source $project),'-c','Release','--no-restore','-m:1') 600
        Set-MdNativeBuildState -State $entry -ProcessResult $build
        Require-NativeSuccess $build 'Release build'
    }
    if(-not $SourceHostPath){$SourceHostPath=$defaultHost}
    $sourceHost=(Get-Item -LiteralPath $SourceHostPath -Force).FullName
    $hostDll=if($Mode -eq 'Smoke'){'NT.AvaloniaControl.Demo.dll'}else{'Markdown.NativeBenchmarks.dll'}
    $dllNames=@('NT.AvaloniaControl.Markdown.dll','NT.AvaloniaControl.Markdown.Themes.dll','NT.AvaloniaControl.Markdown.TextMate.dll',$hostDll)
    $originalFour=Get-MdNativeFileHashes -Directory $sourceHost -RelativePaths $dllNames
    $app=Join-Path $output 'app';$null=New-Item -ItemType Directory -Path $app
    foreach($item in Get-ChildItem -LiteralPath $sourceHost -Force){Copy-Item -LiteralPath $item.FullName -Destination $app -Recurse -Force}
    Assert-MdNativeFileHashes -Directory $sourceHost -Expected $originalFour
    Assert-MdNativeFileHashes -Directory $app -Expected $originalFour
    $frozenHashes=Get-MdNativeFileHashes -Directory $app -RelativePaths @((Get-ChildItem -LiteralPath $app -File -Recurse -Force)|ForEach-Object {[IO.Path]::GetRelativePath($app,$_.FullName)})
    $four=[ordered]@{}
    foreach($name in $dllNames){$four[([IO.Path]::GetFileNameWithoutExtension($name)+'SHA256')]=$originalFour[$name]}
    $entry.FourDLLs=$four
    $null=Assert-NativeSource $before
    Save-NativeJson ([pscustomobject]@{CapturedUtc=[DateTimeOffset]::UtcNow.ToString('O');SourceHostPath=$sourceHost;
        SourceBuildRequested=$entry.SourceBuildRequested;SourceBuildPerformed=$entry.SourceBuildPerformed;BuildStarted=$entry.BuildStarted;BuildSucceeded=$entry.BuildSucceeded;
        BuiltFromCapturedSource=$entry.BuildSucceeded;FourDLLs=$four;AllRuntimeFiles=$frozenHashes}) (Join-Path $output 'source-snapshot.json')
    if($Mode -eq 'Smoke'){
        $path=Join-Path $output 'report.json';$image=Join-Path $output 'preview.png'
        $process=Run-NativeChild 'smoke' $dotnet @((Join-Path $app $hostDll),'--page','markdown','--markdown-smoke','--smoke-output',$path,'--smoke-image',$image) 120 $app
        $report=Read-NativeReport $path $process
        $verdict=Get-MdNativeSmokeVerdict -Report $report.Value -ProcessResult $process -ImagePresent (Test-Path -LiteralPath $image)
        $entry.Smoke=[pscustomobject]@{Report=$report.Path;ReportSHA256=$report.SHA256;Image=$image;Verdict=$verdict}
        $entry.Status=$verdict.Status;$entry.Passed=$verdict.Passed
        if($verdict.Status -eq 'Failed'){throw ($verdict.Failures -join '; ')}
    }
    else{
        $guardSource=Join-Path $source 'scripts/Test-MarkdownPerformance.ps1'
        $guardScript=Join-Path $output 'guard-under-test.ps1'
        Copy-Item -LiteralPath $guardSource -Destination $guardScript
        $guardHash=(Get-FileHash -LiteralPath $guardScript).Hash
        if($guardHash -cne (Get-FileHash -LiteralPath $guardSource).Hash){throw 'Performance guard changed while freezing.'}
        $seen=[Collections.Generic.HashSet[int]]::new()
        foreach($round in 1..5){
            Assert-MdNativeFileHashes -Directory $app -Expected $frozenHashes
            $path=Join-Path $output "report-$round.json"
            $arguments=@((Join-Path $app $hostDll)) + @(Get-MdNativeFormalArguments -Round $round -OutputPath $path)
            $process=Run-NativeChild "round-$round" $dotnet $arguments 1200 $app
            $report=Read-NativeReport $path $process
            $verdict=Get-MdNativeRoundVerdict -Report $report.Value -ProcessResult $process -ExpectedRound $round -ExpectedFour $four
            $rounds.Add([pscustomobject]@{Round=$round;ProcessId=$process.ProcessId;ProducerExitCode=$process.ExitCode;Report=$path;RawSHA256=$report.SHA256;Envelope=$verdict})
            if(-not $verdict.Valid -or -not $seen.Add($process.ProcessId)){throw 'Formal native round is incomplete, repeated, or has a different source; preserve all collected evidence.'}
            # A complete producer Exit1 is recorded unchanged. Collect all five, then use the independent guard.
            # This includes the Linux50..51ms case; never rewrite its original producer50 result or retry a sample.
        }
        foreach($round in $rounds){if((Get-FileHash -LiteralPath $round.Report).Hash -cne $round.RawSHA256){throw 'An original raw report changed before guard execution.'}}
        $reportList=Join-Path $output 'report-paths.json';Save-NativeJson @($rounds|ForEach-Object Report) $reportList
        $guardInvoker=Join-Path $output 'invoke-frozen-guard.ps1'
        @'
param([string]$Guard,[string]$Reports,[string]$Output,[int]$Budget)
$ErrorActionPreference='Stop'
$paths=@(Get-Content -LiteralPath $Reports -Raw|ConvertFrom-Json)
& $Guard -ReportPaths $paths -OutputPath $Output -LongActualUiP95BudgetMilliseconds $Budget -ProducerLongActualUiP95BudgetMilliseconds 50
'@|Set-Content -LiteralPath $guardInvoker -Encoding utf8
        $guardPath=Join-Path $output 'guard.json'
        $guardProcess=Run-NativeChild 'guard' $shell @('-NoLogo','-NoProfile','-File',$guardInvoker,'-Guard',$guardScript,'-Reports',$reportList,'-Output',$guardPath,'-Budget',[string]$budget) 300
        $guard=if(Test-Path -LiteralPath $guardPath){Get-Content -LiteralPath $guardPath -Raw|ConvertFrom-Json}else{$null}
        $entry.Guard=[pscustomobject]@{Path=$guardPath;ExecutedSourcePath=$guardScript;ExecutedSourceSHA256=$guardHash;OriginalSourcePath=$guardSource;ProducerBudget=50;AcceptedBudget=$budget;Result=$guard}
        Require-NativeSuccess $guardProcess 'Independent five-process guard'
        if($guard.全部通过 -isnot [bool] -or -not $guard.全部通过 -or $guard.独立进程数 -ne 5 -or $guard.五轮要求 -ne 5 -or @($guard.结果).Count -ne 5 -or
            $guard.原生产者长会话实际提交UI预算毫秒 -ne 50 -or $guard.本次长会话实际提交UI政策预算毫秒 -ne $budget){throw 'Independent native guard result/policy is incomplete.'}
        foreach($round in $rounds){
            if((Get-FileHash -LiteralPath $round.Report).Hash -cne $round.RawSHA256){throw 'An original raw report changed during guard execution.'}
            $matching=@($guard.结果|Where-Object {$_.报告 -eq $round.Report})
            if($matching.Count -ne 1 -or $matching[0].是否通过 -ne $true -or $null -ne $matching[0].错误 -or $matching[0].原始报告SHA256 -cne $round.RawSHA256){throw 'Independent guard does not cover each unchanged original raw report.'}
        }
        if((Get-FileHash -LiteralPath $guardScript).Hash -cne $guardHash -or (Get-FileHash -LiteralPath $guardSource).Hash -cne $guardHash){throw 'Frozen guard source changed.'}
        $entry.Status='Passed';$entry.Passed=$true
    }
}
catch{$entry.Status='Failed';$entry.Passed=$false;$entry.Failure=$_.Exception.Message}
finally{
    try{
        if($null -ne $before){$after=Assert-NativeSource $before;Save-NativeJson ([pscustomobject]@{CapturedUtc=[DateTimeOffset]::UtcNow.ToString('O');Files=$after}) (Join-Path $output 'source-after.json');$entry.SourceFilesUnchanged=$true}
        if($null -ne $frozenHashes){Assert-MdNativeFileHashes -Directory $app -Expected $frozenHashes}
    }
    catch{$entry.Status='Failed';$entry.Passed=$false;$entry.Failure=(@($entry.Failure,$_.Exception.Message)|Where-Object {$_}) -join '; '}
    $entry.CompletedUtc=[DateTimeOffset]::UtcNow.ToString('O')
    Save-NativeJson $entry (Join-Path $output 'result.json')
}
Write-Output "Markdown native evidence: $output; status=$($entry.Status)"
if($entry.Status -eq 'Pending'){Write-Warning 'Clipboard remains Pending; the original producer failed verdict/exit is retained. No full smoke pass is claimed.';exit 2}
if(-not $entry.Passed){Write-Error "Markdown native validation failed: $($entry.Failure)" -ErrorAction Continue;exit 1}
exit 0
