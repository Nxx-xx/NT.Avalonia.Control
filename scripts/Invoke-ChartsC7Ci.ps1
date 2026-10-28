# Manual hosted-CI adapter only. The original C7 producer/oracle/protocol are unchanged.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidateSet('HostedWindowsSoftware','XvfbSoftware')][string]$NativeMode,
    [string]$PythonExecutable,
    [string]$FontDirectory,
    [ValidateRange(300,6600)][int]$CampaignTimeoutSeconds = 6000
)
$ErrorActionPreference = 'Stop'
$ciRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$ciOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $ciOutput) { throw 'Use a fresh CI evidence directory; failed campaigns are never replaced.' }
[IO.Directory]::CreateDirectory($ciOutput) | Out-Null
if (-not $PythonExecutable) { $PythonExecutable = if ($IsWindows) { 'python' } else { 'python3' } }
. (Join-Path $PSScriptRoot 'ChartsPerformanceGate.Process.ps1')
$ciCampaign = Join-Path $ciOutput 'campaign'
$ciReceipt = $null; $ciError = $null; $ciEvidenceError = $null; $ciEvidenceExit = $null; $ciPassed = $false
$ciStarted = [DateTimeOffset]::UtcNow
try {
    if (($NativeMode -eq 'HostedWindowsSoftware' -and -not $IsWindows) -or ($NativeMode -eq 'XvfbSoftware' -and -not $IsLinux)) { throw 'The requested CI mode does not match this OS.' }
    if ($NativeMode -eq 'XvfbSoftware' -and -not $env:DISPLAY) { throw 'Xvfb mode requires the workflow-owned X11 DISPLAY.' }
    foreach ($ciCommand in @('rg','dotnet',$PythonExecutable)) { if (-not (Get-Command $ciCommand -ErrorAction SilentlyContinue)) { throw "Missing prerequisite: $ciCommand" } }
    [ordered]@{
        schema = 'charts-c7-ci-wrapper-start/v1'; startedUtc = $ciStarted.ToString('O')
        nativeMode = $NativeMode; sourceRoot = $ciRoot; output = $ciOutput
        runnerEnvironment = $env:RUNNER_ENVIRONMENT; runnerOs = $env:RUNNER_OS
        imageOs = $env:ImageOS; imageVersion = $env:ImageVersion
        display = $env:DISPLAY; displayKind = $env:CHARTS_DISPLAY_KIND; waylandDisplay = $env:WAYLAND_DISPLAY
        officialReference = 'blocked-reference'; officialMeasurements = $null; officialRatios = $null
        physicalHardwareQualified = $false; presentMeasurements = $null; peakMemoryMeasurements = $null
        campaignTimeoutSeconds = $CampaignTimeoutSeconds
        timeoutScope = 'External CI watchdog only; partial evidence remains failed. Original per-run timeouts and numeric gates are unchanged.'
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ciOutput 'wrapper-start.json') -Encoding utf8
    $ciPowerShell = (Get-Process -Id $PID).Path
    $ciArguments = @('-NoLogo','-NoProfile','-File',(Join-Path $PSScriptRoot 'Invoke-ChartsC7Performance.ps1'),'-OutputDirectory',$ciCampaign,'-Phase','formal','-PythonExecutable',$PythonExecutable)
    if ($FontDirectory) { $ciArguments += @('-FontDirectory',$FontDirectory) }
    $ciReceipt = Invoke-ChartsGateProcess -Id 'original-c7-formal' -Executable $ciPowerShell -Arguments $ciArguments -WorkingDirectory $ciRoot -OutputDirectory (Join-Path $ciOutput 'supervisor') -TimeoutSeconds $CampaignTimeoutSeconds
    if (-not $ciReceipt.passed) { $ciError = $ciReceipt.error }
} catch { $ciError = $_.Exception.ToString() }
finally {
    try {
        $ciExit = if ($null -ne $ciReceipt -and $null -ne $ciReceipt.exitCode) { [int]$ciReceipt.exitCode } else { -1 }
        & $PythonExecutable (Join-Path $PSScriptRoot 'charts_c7_ci_evidence.py') --repo $ciRoot --root $ciOutput --native-mode $NativeMode --campaign-exit-code $ciExit
        $ciEvidenceExit = $LASTEXITCODE
        if ($ciEvidenceExit -ne 0) { $ciEvidenceError = "CI evidence/gate exit $ciEvidenceExit; see ci-evidence.json and all original campaign artifacts." }
    } catch { $ciEvidenceError = $_.Exception.ToString() }
    $ciPassed = $null -ne $ciReceipt -and $ciReceipt.passed -and $ciEvidenceExit -eq 0 -and -not $ciError -and -not $ciEvidenceError
    [ordered]@{
        schema = 'charts-c7-ci-wrapper-result/v1'; startedUtc = $ciStarted.ToString('O'); finishedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        nativeMode = $NativeMode; passed = $ciPassed; campaign = 'campaign'; supervisor = $ciReceipt
        error = $ciError; evidenceError = $ciEvidenceError; evidenceExitCode = $ciEvidenceExit
        officialReference = 'blocked-reference'; officialMeasurements = $null; officialRatios = $null
        presentMeasurements = $null; peakMemoryMeasurements = $null; physicalHardwareQualified = $false
        scope = 'Hosted Windows software or X11 Xvfb self-developed original formal gate only; no official license/reference, real desktop, physical GPU/touch or present/peak qualification.'
    } | ConvertTo-Json -Depth 25 | Set-Content -LiteralPath (Join-Path $ciOutput 'wrapper-result.json') -Encoding utf8
}
if (-not $ciPassed) { throw "Original C7 CI gate failed. $ciError $ciEvidenceError All available evidence is retained at $ciOutput" }
