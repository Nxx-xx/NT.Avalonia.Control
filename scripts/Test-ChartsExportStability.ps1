[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(1,128)][int]$Rounds = 16,
    [ValidateRange(8191,1000000)][int]$Points = 100000,
    [ValidateSet('DesktopSynthetic','NativeSynthetic','XvfbSynthetic')][string]$NativeMode = $(if ($IsWindows) { 'DesktopSynthetic' } else { 'XvfbSynthetic' }),
    [ValidateRange(30,1800)][int]$NativeTimeoutSeconds = 600,
    [string]$PythonExecutable = 'python',
    [switch]$SkipBuild,
    [string]$HostDirectory
)
$ErrorActionPreference = 'Stop'
$exportRepo = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$exportOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $exportOutput) { throw 'Use a fresh immutable export-stability output directory.' }
if ($HostDirectory -and -not $SkipBuild) { throw '-HostDirectory requires -SkipBuild; the caller owns its frozen build provenance.' }
if ($IsWindows -and $NativeMode -eq 'XvfbSynthetic') { throw 'Xvfb mode requires Linux.' }
[IO.Directory]::CreateDirectory($exportOutput) | Out-Null
. (Join-Path $PSScriptRoot 'ChartsPerformanceGate.Process.ps1')
function Save-ExportStabilityJson($Value, [string]$Name) {
    $Value | ConvertTo-Json -Depth 35 | Set-Content -LiteralPath (Join-Path $exportOutput $Name) -Encoding utf8
}
function Get-ExportStabilityInputs {
    $roots=@('src/NT.AvaloniaControl.Charts','src/NT.AvaloniaControl.Charts.Themes','benchmarks/Charts.ExportStability')
    if (Get-Command rg -ErrorAction SilentlyContinue) {
        $paths = @(& rg --files --hidden @roots -g '!**/bin/**' -g '!**/obj/**')
        if ($LASTEXITCODE -ne 0) { throw 'Source inventory failed.' }
    } else {
        $paths=@($roots | ForEach-Object { Get-ChildItem -LiteralPath (Join-Path $exportRepo $_) -Recurse -File | Where-Object FullName -NotMatch '[\\/](bin|obj)[\\/]' | ForEach-Object { [IO.Path]::GetRelativePath($exportRepo,$_.FullName) } })
    }
    $paths += @('Directory.Build.props','Directory.Packages.props','global.json','NuGet.Config','.github/workflows/charts.yml',
        'scripts/Test-ChartsExportStability.ps1','scripts/ChartsPerformanceGate.Process.ps1','scripts/charts_export_stability_oracle.py','scripts/charts_png_oracle.py','scripts/Tests/test_charts_export_stability_oracle.py')
    @($paths | Sort-Object -Unique | ForEach-Object {
        $path = Join-Path $exportRepo $_
        [ordered]@{path=$_.Replace('\','/');sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;bytes=(Get-Item -LiteralPath $path).Length}
    })
}
function Get-ExportStabilityHostManifest([string]$Directory) {
    @(Get-ChildItem -LiteralPath $Directory -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{path=[IO.Path]::GetRelativePath($Directory,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;bytes=$_.Length}
    })
}
$processes = [Collections.Generic.List[object]]::new()
$started = [DateTimeOffset]::UtcNow
$passed = $false; $failure = $null; $failedStage = $null
$before = @(); $hostBefore = @(); $hostAfter = @(); $after = @(); $jitEnvironment = @()
$defaultJitConfirmed=$false; $builtCurrentSource=$false
$hostRoot = if ($HostDirectory) { [IO.Path]::GetFullPath($HostDirectory) } else { Join-Path $exportRepo 'benchmarks/Charts.ExportStability/bin/Release/net10.0' }
Push-Location $exportRepo
try {
    $before = Get-ExportStabilityInputs
    Save-ExportStabilityJson $before 'source-inputs-before.json'
    $keys = @('DOTNET_TieredCompilation','COMPlus_TieredCompilation','DOTNET_TieredPGO','COMPlus_TieredPGO','DOTNET_ReadyToRun','COMPlus_ReadyToRun',
        'DOTNET_TC_QuickJit','COMPlus_TC_QuickJit','DOTNET_TC_QuickJitForLoops','COMPlus_TC_QuickJitForLoops','DOTNET_TC_CallCountingDelayMs','COMPlus_TC_CallCountingDelayMs',
        'DOTNET_gcServer','COMPlus_gcServer','DOTNET_GCStress','COMPlus_GCStress')
    $jitEnvironment = @($keys | ForEach-Object { [ordered]@{key=$_;value=[Environment]::GetEnvironmentVariable($_,'Process')} })
    if (@($jitEnvironment | Where-Object { $_.value -and $_.key -match 'Tiered|ReadyToRun|TC_' }).Count -ne 0) { throw 'Default-JIT gate rejects ambient JIT overrides; none are silently removed.' }
    if (-not $SkipBuild) {
        foreach ($stage in @(
            @{id='restore';args=@('restore','benchmarks/Charts.ExportStability/Charts.ExportStability.csproj','--locked-mode','--disable-parallel','--disable-build-servers','-m:1','-p:UseSharedCompilation=false')},
            @{id='build';args=@('build','benchmarks/Charts.ExportStability/Charts.ExportStability.csproj','-c','Release','--no-restore','--disable-build-servers','-m:1','-p:UseSharedCompilation=false')}
        )) {
            $failedStage=$stage.id
            $receipt=Invoke-ChartsGateProcess -Id $stage.id -Executable 'dotnet' -Arguments $stage.args -WorkingDirectory $exportRepo -OutputDirectory (Join-Path $exportOutput $stage.id) -TimeoutSeconds 600
            $processes.Add($receipt)
            if (-not $receipt.passed) { throw "Export stability $($stage.id) failed; original evidence retained." }
        }
        $builtCurrentSource=$true
    }
    $hostDll=Join-Path $hostRoot 'Charts.ExportStability.dll'
    if (-not (Test-Path -LiteralPath $hostDll)) { throw 'Export stability host assembly missing.' }
    $runtimePath=Join-Path $hostRoot 'Charts.ExportStability.runtimeconfig.json'
    $runtime=Get-Content -LiteralPath $runtimePath -Raw | ConvertFrom-Json
    $tiered=@($runtime.runtimeOptions.configProperties.PSObject.Properties | Where-Object Name -eq 'System.Runtime.TieredCompilation')
    if ($tiered.Count -gt 0 -and $tiered[0].Value -eq $false) { throw 'The native export host must preserve default tiered JIT.' }
    $defaultJitConfirmed=$true
    $hostBefore=Get-ExportStabilityHostManifest $hostRoot
    Save-ExportStabilityJson $hostBefore 'host-inputs-before.json'
    $arguments=@($hostDll,'--export-stability','--rounds',[string]$Rounds,'--force-gc','--points',[string]$Points,'--output',(Join-Path $exportOutput 'producer'))
    $executable='dotnet'
    if ($NativeMode -eq 'XvfbSynthetic') { $executable='xvfb-run';$arguments=@('-a','dotnet')+$arguments }
    $failedStage='native'
    $nativeWorkingDirectory=if ($HostDirectory) { $hostRoot } else { $exportRepo }
    $native=Invoke-ChartsGateProcess -Id 'native' -Executable $executable -Arguments $arguments -WorkingDirectory $nativeWorkingDirectory -OutputDirectory (Join-Path $exportOutput 'native') -TimeoutSeconds $NativeTimeoutSeconds
    $processes.Add($native)
    # Run the independent oracle even after a fatal native exit, so missing final
    # receipts and partial frames stay an explicit failure in uploaded evidence.
    $failedStage='oracle'
    $oracle=Invoke-ChartsGateProcess -Id 'oracle' -Executable $PythonExecutable -Arguments @(
        (Join-Path $PSScriptRoot 'charts_export_stability_oracle.py'),'--input',(Join-Path $exportOutput 'producer'),
        '--output',(Join-Path $exportOutput 'independent-oracle.json'),'--expected-rounds',[string]$Rounds,'--expected-points',[string]$Points,'--require-force-gc'
    ) -WorkingDirectory $exportRepo -OutputDirectory (Join-Path $exportOutput 'oracle') -TimeoutSeconds 600
    $processes.Add($oracle)
    if (-not $native.passed) { $failedStage='native';throw 'Native export stability failed or timed out; no automatic retry.' }
    if (-not $oracle.passed) { throw 'Independent PNG/protocol oracle failed; producer flags cannot override it.' }
    $report=Get-Content -LiteralPath (Join-Path $exportOutput 'producer/results.json') -Raw | ConvertFrom-Json
    $executed=@($report.assembliesAfter | Where-Object name -eq 'NT.AvaloniaControl.Charts')
    $core=Join-Path $hostRoot 'NT.AvaloniaControl.Charts.dll'
    if ($executed.Count -ne 1 -or $executed[0].sha256 -cne (Get-FileHash -LiteralPath $core -Algorithm SHA256).Hash) { throw 'Executed product DLL does not match pinned host input.' }
    $passed=$true; $failedStage=$null
}
catch { $failure=$_.Exception.ToString() }
finally {
    try {
        $after=Get-ExportStabilityInputs
        $sourcesStable=($before | ConvertTo-Json -Depth 7 -Compress) -ceq ($after | ConvertTo-Json -Depth 7 -Compress)
        if ($hostBefore.Count -gt 0) { $hostAfter=Get-ExportStabilityHostManifest $hostRoot }
        $assembliesStable=($hostBefore | ConvertTo-Json -Depth 7 -Compress) -ceq ($hostAfter | ConvertTo-Json -Depth 7 -Compress)
        $passed=$passed -and $sourcesStable -and $assembliesStable
        Save-ExportStabilityJson $after 'source-inputs-after.json'
        Save-ExportStabilityJson $hostAfter 'host-inputs-after.json'
        Save-ExportStabilityJson ([ordered]@{
            schema='charts-native-export-stability-gate/v1';passed=[bool]$passed;startedUtc=$started.ToString('o');completedUtc=[DateTimeOffset]::UtcNow.ToString('o')
            failedStage=$failedStage;error=$failure;processes=$processes;rounds=$Rounds;points=$Points;forcedGc=$true;nativeMode=$NativeMode
            sourceInputsUnchanged=$sourcesStable;hostFilesUnchanged=$assembliesStable;jitEnvironmentWhitelist=$jitEnvironment
            defaultJitPreserved=$defaultJitConfirmed;tier1PromotionIndependentlyProved=$false;callerProvidedFrozenHost=[bool]$HostDirectory
            currentSourceBuildEstablished=$builtCurrentSource;fullDemoRegressionRun=$false;officialRuntimeCompared=$false
            physicalInputValidated=$false;compositorPresentValidated=$false;performanceMeasured=$false;eightHourSoakCompleted=$false
            noAutomaticRetry=$true;nugetAuditDisabled=$false
        }) 'results.json'
    } finally { Pop-Location }
}
if (-not $passed) { throw "Export stability gate failed; evidence retained at $exportOutput" }
