param([string]$OutputPath = "artifacts/platform/windows-environment.json")

$ErrorActionPreference = "Stop"
if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    throw "This probe is for Windows. Use scripts/collect-platform.sh for Linux."
}

# Collect reproducible platform facts, never hostnames, usernames, serial numbers or network settings.
# Restricted developer shells may not expose CIM. Keep the remaining facts usable and mark gaps.
function Get-PlatformCimData([string]$ClassName, [string[]]$Properties) {
    try {
        $platformData = @(Get-CimInstance -ClassName $ClassName -ErrorAction Stop | Select-Object -Property $Properties)
        return [ordered]@{ Available = $true; Data = $platformData }
    } catch {
        return [ordered]@{ Available = $false; Note = "CIM query unavailable in this shell; collect this field on the target machine manually." }
    }
}
$platformOs = Get-PlatformCimData "Win32_OperatingSystem" @("Caption", "Version", "BuildNumber", "OSArchitecture")
$platformCpu = Get-PlatformCimData "Win32_Processor" @("Name", "Architecture", "NumberOfCores", "NumberOfLogicalProcessors")
$platformCpuName = try {
    (Get-ItemProperty -LiteralPath 'HKLM:\HARDWARE\DESCRIPTION\System\CentralProcessor\0' -Name ProcessorNameString -ErrorAction Stop).ProcessorNameString.Trim()
} catch { $null }
$platformGpu = Get-PlatformCimData "Win32_VideoController" @("Name", "DriverVersion", "CurrentHorizontalResolution", "CurrentVerticalResolution")
$platformDotnet = (& dotnet --info 2>&1 | Out-String).Replace($env:USERPROFILE, "<USERPROFILE>")
$platformDotnetExitCode = $LASTEXITCODE
$platformFonts = @("msyh.ttc", "msyhbd.ttc", "simsun.ttc", "simhei.ttf") | Where-Object { Test-Path -LiteralPath (Join-Path $env:WINDIR "Fonts/$_") }
$platformLanguages = try {
    if (Get-Command Get-WinUserLanguageList -ErrorAction SilentlyContinue) {
        @(Get-WinUserLanguageList -ErrorAction Stop | Where-Object { $_.LanguageTag } | Select-Object LanguageTag, InputMethodTips)
    } else { @() }
} catch { @() }
$platformLanguages = @($platformLanguages | Where-Object { -not [string]::IsNullOrWhiteSpace($_.LanguageTag) })
$platformResult = [ordered]@{
    CollectedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    OSDescription = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    ProcessorIdentifier = $env:PROCESSOR_IDENTIFIER
    OperatingSystem = $platformOs
    Cpu = $platformCpu
    CpuNameFromRegistry = $platformCpuName
    Graphics = $platformGpu
    ProcessArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    DotnetInfo = $platformDotnet.Trim()
    DotnetInfoExitCode = $platformDotnetExitCode
    ChineseFontFiles = @($platformFonts)
    InputLanguages = [ordered]@{ Available = @($platformLanguages).Count -gt 0; Data = @($platformLanguages) }
    NativeImeValidated = $false
    KylinValidated = $false
    Notes = "Environment collection only. Run the native window smoke probe separately; test actual IME, DPI, clipboard and accessibility manually."
}
$platformFullPath = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($platformFullPath)) | Out-Null
$platformResult | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $platformFullPath -Encoding utf8
Write-Output "Environment report saved to $platformFullPath"
