[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$CasesPath,
    [ValidateSet('NativeSynthetic','DesktopSynthetic','XvfbSynthetic')][string]$NativeMode = 'DesktopSynthetic',
    [ValidateRange(30,1800)][int]$CaseTimeoutSeconds = 180,
    [ValidateRange(30,1800)][int]$BuildTimeoutSeconds = 900,
    [string]$PythonExecutable,
    [ValidateSet('not-specified','user-confirmed-access-pending','connected-not-executed')][string]$TargetLinuxEquipmentStatus = 'not-specified',
    [switch]$LinuxNativeTextIsolation,
    [switch]$PlanOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$acceptanceRepository = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
. (Join-Path $PSScriptRoot 'ChartsPerformanceGate.Process.ps1')
if ($LinuxNativeTextIsolation -and -not $IsLinux) { throw 'Native text isolation is an explicit Linux acceptance-host option.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $acceptanceRepository ('artifacts/charts-platform-acceptance/' + [guid]::NewGuid().ToString('N')) }
$acceptanceOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $acceptanceOutput) { throw 'Use a fresh output directory; existing evidence is never replaced.' }
[IO.Directory]::CreateDirectory($acceptanceOutput) | Out-Null
$started = [DateTimeOffset]::UtcNow
$stages = [Collections.Generic.List[object]]::new()
$cases = [Collections.Generic.List[object]]::new()
$runError = $null
$sourceUnchanged = $false
$assembliesUnchanged = $false
$qualification = [ordered]@{
    physicalInputValidated = $false; compositorPresentValidated = $false
    physicalMultiMonitorValidated = $false; targetDistributionCertified = $false
    nativeSelectedItemBridgeCertified = $false; officialRuntimeCompared = $false
    performanceSlaEvaluated = $false; longSoakValidated = $false
    hostOnlyNativeTextIsolationRequested = [bool]$LinuxNativeTextIsolation
    ordinaryConsumerNativeTextCertified = $false
    scope = 'Automatic native-window and raster evidence. External system-input/compositor and physical-device evidence are separate.'
}
function Save-AcceptanceJson($Value, [string]$Path) {
    $Value | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $Path -Encoding utf8
}
function Invoke-AcceptanceStage([string]$Id, [string]$Executable, [string[]]$Arguments, [int]$TimeoutSeconds) {
    $receipt = Invoke-ChartsGateProcess -Id $Id -Executable $Executable -Arguments $Arguments -WorkingDirectory $acceptanceRepository -OutputDirectory (Join-Path $acceptanceOutput "processes/$Id") -TimeoutSeconds $TimeoutSeconds
    $stages.Add($receipt)
    return $receipt
}
function Get-AcceptanceSources {
    $paths = [Collections.Generic.List[string]]::new()
    foreach ($relative in @('src/NT.AvaloniaControl.Charts','src/NT.AvaloniaControl.Charts.Themes','benchmarks/Charts.PlatformAcceptance')) {
        $directory = Join-Path $acceptanceRepository $relative
        if (Test-Path -LiteralPath $directory) {
            foreach ($file in Get-ChildItem -LiteralPath $directory -Force -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -ne '.md' }) { $paths.Add($file.FullName) }
        }
    }
    foreach ($relative in @('Directory.Build.props','Directory.Packages.props','global.json','NuGet.Config','.editorconfig','scripts/ChartsPerformanceGate.Process.ps1','scripts/Invoke-ChartsPlatformAcceptance.ps1','scripts/Invoke-ChartsPlatformAcceptance-Linux.sh','scripts/charts_platform_acceptance_oracle.py','scripts/charts_platform_acceptance_contract.json','scripts/charts_png_oracle.py','scripts/Tests/test_charts_platform_acceptance.py')) {
        $file = Join-Path $acceptanceRepository $relative
        if (Test-Path -LiteralPath $file) { $paths.Add($file) }
    }
    return @(foreach ($path in $paths | Sort-Object -CaseSensitive -Unique) { $file = Get-Item -LiteralPath $path -Force; [ordered]@{path=[IO.Path]::GetRelativePath($acceptanceRepository,$file.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash;bytes=$file.Length} })
}
function Get-AcceptanceAssemblies([string]$Directory) {
    $files = @(Get-ChildItem -LiteralPath $Directory -Force -File | Where-Object { $_.Extension -eq '.dll' -or $_.Name -match '\.(deps|runtimeconfig)\.json$' })
    $files += @(Get-ChildItem -LiteralPath $Directory -Force -File -Recurse -Filter 'libHarfBuzzSharp.so')
    return @($files | Sort-Object FullName -CaseSensitive -Unique | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($Directory,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;bytes=$_.Length} })
}
function Add-DefaultAcceptanceCase([string]$Id,[string]$Renderer,[string]$Culture,[string]$Theme,$Scale=$null) {
    $cases.Add([pscustomobject][ordered]@{id=$Id;renderer=$Renderer;culture=$Culture;theme=$Theme;scenario='desktop';expectedScale=$Scale;processScreenScale=$Scale})
}
$finalError = $null
$finalPassed = $false
try {
    if ($CasesPath) {
        $resolvedCases = [IO.Path]::GetFullPath($CasesPath)
        $custom = Get-Content -LiteralPath $resolvedCases -Raw | ConvertFrom-Json
        if ($null -ne $custom.PSObject.Properties['cases']) { $custom = $custom.cases }
        foreach ($case in @($custom)) { $cases.Add($case) }
        Save-AcceptanceJson ([ordered]@{path=$resolvedCases;sha256=(Get-FileHash -LiteralPath $resolvedCases -Algorithm SHA256).Hash}) (Join-Path $acceptanceOutput 'case-plan-source.json')
    } else {
        Add-DefaultAcceptanceCase 'default-en-US-light' 'default' 'en-US' 'light'
        Add-DefaultAcceptanceCase 'software-en-US-light' 'software' 'en-US' 'light'
        Add-DefaultAcceptanceCase 'gpu-en-US-light' 'gpu' 'en-US' 'light'
        Add-DefaultAcceptanceCase 'default-zh-CN-dark' 'default' 'zh-CN' 'dark'
        Add-DefaultAcceptanceCase 'default-ar-SA-hc-light' 'default' 'ar-SA' 'hc-light'
        Add-DefaultAcceptanceCase 'software-zh-CN-hc-dark' 'software' 'zh-CN' 'hc-dark'
        if ($IsLinux) {
            foreach ($scale in @(1.0,1.25,1.5,2.0)) {
                $tag = $scale.ToString('0.##',[Globalization.CultureInfo]::InvariantCulture).Replace('.','-')
                Add-DefaultAcceptanceCase "software-zh-CN-scale-$tag" 'software' 'zh-CN' 'light' $scale
            }
        }
    }
    if ($cases.Count -lt 1 -or $cases.Count -gt 96) { throw 'A plan must contain 1..96 bounded native processes.' }
    $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($case in $cases) {
        if ($null -ne $case.PSObject.Properties['isolateNativeText']) {
            if ($case.isolateNativeText -isnot [bool] -or $case.isolateNativeText -ne [bool]$LinuxNativeTextIsolation) { throw 'Native text isolation must match the explicit runner switch.' }
        } else { $case | Add-Member -NotePropertyName isolateNativeText -NotePropertyValue ([bool]$LinuxNativeTextIsolation) }
        foreach ($required in @('id','renderer','culture','theme','scenario','expectedScale','processScreenScale')) {
            if ($null -eq $case.PSObject.Properties[$required]) { throw "Case misses required field: $required" }
        }
        if ($case.id -notmatch '^[a-zA-Z0-9][a-zA-Z0-9._-]{0,99}$' -or -not $ids.Add([string]$case.id)) { throw 'Case IDs must be unique safe directory names.' }
        if ($case.renderer -notin @('default','software','gpu') -or $case.theme -notin @('light','dark','hc-light','hc-dark') -or $case.scenario -ne 'desktop') { throw 'Unsupported renderer, theme or scenario.' }
        $case.culture = [Globalization.CultureInfo]::GetCultureInfo([string]$case.culture).Name
        foreach ($field in @('expectedScale','processScreenScale')) {
            if ($null -ne $case.$field) {
                if ($case.$field -is [bool] -or -not [double]::IsFinite([double]$case.$field) -or [double]$case.$field -lt .5 -or [double]$case.$field -gt 4) { throw 'Scale must be finite in 0.5..4, or null for current desktop scaling.' }
                $case.$field = [double]$case.$field
            }
        }
        if ($null -ne $case.processScreenScale -and -not $IsLinux) { throw 'Process screen-scale injection is only available to Linux X11 cases; Windows system DPI is never modified.' }
    }
    Save-AcceptanceJson ([ordered]@{schema='charts-platform-acceptance-plan/v1';cases=@($cases);nativeMode=$NativeMode;displayKind=$env:CHARTS_DISPLAY_KIND;scope='Six orthogonal defaults plus four Linux software scale cases; not the complete Cartesian product of renderer/culture/theme/DPI.';scaleQualification='expectedScale is checked against actual before/after RenderScaling; processScreenScale sets the child AVALONIA_GLOBAL_SCALE_FACTOR multiplier and clears child indexed screen overrides. Defaults assume observed base scale 1; use an explicit expectedScale plan on other base scales. This does not certify physical DPI.'}) (Join-Path $acceptanceOutput 'plan.json')
    $before = @(Get-AcceptanceSources)
    Save-AcceptanceJson $before (Join-Path $acceptanceOutput 'source-before.json')
    if (-not $PlanOnly) {
        $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
        if (-not $PythonExecutable) { $PythonExecutable = (Get-Command $(if ($IsWindows) {'python'} else {'python3'}) -ErrorAction Stop).Source }
        $project = 'benchmarks/Charts.PlatformAcceptance/Charts.PlatformAcceptance.csproj'
        $restore = Invoke-AcceptanceStage 'restore' $dotnet @('restore',$project,'--locked-mode','--disable-parallel') $BuildTimeoutSeconds
        if (-not $restore.passed) { throw 'Locked restore failed; normal NuGet audit is retained. See receipt.' }
        $build = Invoke-AcceptanceStage 'build' $dotnet @('build',$project,'-c','Release','--no-restore','--no-incremental','--disable-build-servers','-m:1','-p:UseSharedCompilation=false') $BuildTimeoutSeconds
        if (-not $build.passed) { throw 'Host source rebuild failed; see supervised receipt.' }
        $hostRoot = Join-Path $acceptanceRepository 'benchmarks/Charts.PlatformAcceptance/bin/Release/net10.0'
        $hostDll = Join-Path $hostRoot 'Charts.PlatformAcceptance.dll'
        if (-not (Test-Path -LiteralPath $hostDll)) { throw 'Acceptance host assembly does not exist.' }
        $assembliesBefore = @(Get-AcceptanceAssemblies $hostRoot)
        foreach ($required in @('Charts.PlatformAcceptance.dll','NT.AvaloniaControl.Charts.dll','NT.AvaloniaControl.Charts.Themes.dll','Avalonia.Base.dll','Avalonia.Skia.dll')) {
            if ($required -notin @($assembliesBefore.path)) { throw "Missing required host module: $required" }
        }
        Save-AcceptanceJson $assembliesBefore (Join-Path $acceptanceOutput 'assemblies-before.json')
        foreach ($case in $cases) {
            $caseOutput = Join-Path $acceptanceOutput "cases/$($case.id)"
            $arguments = @($hostDll,'--output',$caseOutput,'--renderer',[string]$case.renderer,'--culture',[string]$case.culture,'--theme',[string]$case.theme,'--scenario',[string]$case.scenario)
            if ($LinuxNativeTextIsolation) { $arguments += '--isolate-native-text' }
            if ($null -ne $case.expectedScale) { $arguments += @('--scale',([double]$case.expectedScale).ToString('R',[Globalization.CultureInfo]::InvariantCulture)) }
            $previousScale = [Environment]::GetEnvironmentVariable('AVALONIA_SCREEN_SCALE_FACTORS','Process')
            $previousGlobalScale = [Environment]::GetEnvironmentVariable('AVALONIA_GLOBAL_SCALE_FACTOR','Process')
            try {
                if ($null -ne $case.processScreenScale) {
                    [Environment]::SetEnvironmentVariable('AVALONIA_SCREEN_SCALE_FACTORS',$null,'Process')
                    [Environment]::SetEnvironmentVariable('AVALONIA_GLOBAL_SCALE_FACTOR',([double]$case.processScreenScale).ToString('R',[Globalization.CultureInfo]::InvariantCulture),'Process')
                }
                [void](Invoke-AcceptanceStage "case-$($case.id)" $dotnet $arguments $CaseTimeoutSeconds)
            } finally {
                [Environment]::SetEnvironmentVariable('AVALONIA_SCREEN_SCALE_FACTORS',$previousScale,'Process')
                [Environment]::SetEnvironmentVariable('AVALONIA_GLOBAL_SCALE_FACTOR',$previousGlobalScale,'Process')
            }
        }
        $assembliesAfter = @(Get-AcceptanceAssemblies $hostRoot)
        Save-AcceptanceJson $assembliesAfter (Join-Path $acceptanceOutput 'assemblies-after.json')
        $assembliesUnchanged = (ConvertTo-Json -InputObject $assembliesBefore -Depth 8 -Compress) -ceq (ConvertTo-Json -InputObject $assembliesAfter -Depth 8 -Compress)
    }
    $after = @(Get-AcceptanceSources)
    Save-AcceptanceJson $after (Join-Path $acceptanceOutput 'source-after.json')
    $sourceUnchanged = (ConvertTo-Json -InputObject $before -Depth 8 -Compress) -ceq (ConvertTo-Json -InputObject $after -Depth 8 -Compress)
    if (-not $sourceUnchanged) { throw 'Declared source input set/hash changed during execution.' }
    if (-not $PlanOnly -and -not $assembliesUnchanged) { throw 'Host output module set/hash changed during execution.' }
} catch { $runError = $_.Exception.ToString() }
$run = [ordered]@{
    schema='charts-platform-acceptance-run/v1';startedUtc=$started.ToString('o');finishedUtc=[DateTimeOffset]::UtcNow.ToString('o')
    platform=$(if($IsWindows){'windows'}elseif($IsLinux){'linux'}else{'other'});osDescription=[Runtime.InteropServices.RuntimeInformation]::OSDescription
    architecture=[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString();powershell=$PSVersionTable.PSVersion.ToString()
    nativeMode=$NativeMode;displayKind=$env:CHARTS_DISPLAY_KIND;display=$env:DISPLAY;waylandDisplay=$env:WAYLAND_DISPLAY
    targetLinuxEquipmentStatus=$TargetLinuxEquipmentStatus
    linuxNativeTextIsolationRequested=[bool]$LinuxNativeTextIsolation
    planOnly=[bool]$PlanOnly;sourceUnchanged=$sourceUnchanged;assembliesUnchanged=$assembliesUnchanged
    sourceAssociation=@{method='same-run-locked-restore-and-nonincremental-source-build';hostProject='benchmarks/Charts.PlatformAcceptance/Charts.PlatformAcceptance.csproj';binaryOnlyQualificationAllowed=$false}
    processExecutions=@($stages);error=$runError;qualification=$qualification
    nativeProcessesPassed=($null -eq $runError -and -not $PlanOnly -and @($stages | Where-Object {-not $_.passed}).Count -eq 0)
    passed=$false;status=$(if($PlanOnly -and $null -eq $runError){'planned'}else{'pending-independent-oracle'})
    pendingAcceptance=@($(if($TargetLinuxEquipmentStatus -eq 'user-confirmed-access-pending'){'User confirmed Ubuntu22.04/24.04 physical equipment exists; connection and target-session execution pending'}else{'Target physical Windows/Linux distribution and desktop qualification'}),'Physical multi-monitor DPI transitions and hardware input','Native selected-item accessibility bridge deferred by user','Official licensed runtime/performance comparison and long soak')
}
Save-AcceptanceJson $run (Join-Path $acceptanceOutput 'run.json')
if ($PlanOnly) { if ($runError) { throw $runError }; Write-Output "Planned: $acceptanceOutput"; return }
try {
    if (-not $PythonExecutable) { $PythonExecutable = (Get-Command $(if ($IsWindows) {'python'} else {'python3'}) -ErrorAction Stop).Source }
    $oracle = Invoke-AcceptanceStage 'independent-oracle' $PythonExecutable @((Join-Path $PSScriptRoot 'charts_platform_acceptance_oracle.py'),'--run-directory',$acceptanceOutput,'--expected-platform',$(if($IsWindows){'windows'}else{'linux'}),'--expected-architecture',[Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString(),'--output',(Join-Path $acceptanceOutput 'independent-oracle.json')) $BuildTimeoutSeconds
    if (-not $oracle.passed) { throw 'Independent acceptance oracle rejected evidence; all raw output is preserved.' }
    if ($runError) { throw $runError }
    $finalPassed = $true
} catch {
    $finalError = $_.Exception.ToString()
}
Save-AcceptanceJson ([ordered]@{
    schema='charts-platform-acceptance-results/v1';passed=$finalPassed;status=$(if($finalPassed){'selected-software-matrix-passed-physical-acceptance-open'}else{'failed'})
    startedUtc=$started.ToString('o');finishedUtc=[DateTimeOffset]::UtcNow.ToString('o');error=$finalError
    rawRun=@{path='run.json';sha256=(Get-FileHash -LiteralPath (Join-Path $acceptanceOutput 'run.json') -Algorithm SHA256).Hash}
    independentOracle=$(if(Test-Path -LiteralPath (Join-Path $acceptanceOutput 'independent-oracle.json')){@{path='independent-oracle.json';sha256=(Get-FileHash -LiteralPath (Join-Path $acceptanceOutput 'independent-oracle.json') -Algorithm SHA256).Hash}}else{$null})
    processExecutions=@($stages);qualification=$qualification;targetLinuxEquipmentStatus=$TargetLinuxEquipmentStatus
}) (Join-Path $acceptanceOutput 'results.json')
if (-not $finalPassed) { Write-Error $finalError; exit 1 }
Write-Output "Native software acceptance completed: $acceptanceOutput"
