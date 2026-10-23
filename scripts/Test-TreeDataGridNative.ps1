param([string]$OutputDirectory = 'artifacts/platform/treedatagrid-p6', [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    $outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    $windows = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)
    $capabilities = [ordered]@{
        TimestampUtc = [DateTimeOffset]::UtcNow.ToString('o')
        Windows = $windows
        UserInteractive = [Environment]::UserInteractive
        NativeWindowCreated = $null
        Status = 'Pending'
    }
    if (-not $windows) {
        $capabilities.Status = 'EnvironmentUnavailable'
        $capabilities | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'native-capabilities.json') -Encoding utf8
        throw 'TreeDataGrid native checks require a Windows desktop; this environment is unavailable.'
    }
    if (-not $NoBuild) {
        dotnet restore NT.AvaloniaControl.slnx --locked-mode
        if ($LASTEXITCODE -ne 0) { throw 'Native check restore failed.' }
        dotnet build samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj -c Release --no-restore -m:1
        if ($LASTEXITCODE -ne 0) { throw 'Native check build failed.' }
    }
    & (Join-Path $PSScriptRoot 'Collect-Platform.ps1') -OutputPath (Join-Path $outputRoot 'environment.json')
    $dll = Join-Path $repoRoot 'samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.dll'
    foreach ($scenario in @(@{Name='demo';Flag='--demo-smoke'}, @{Name='mvvm';Flag='--mvvm-smoke'}, @{Name='regression';Flag='--smoke'})) {
        $json = Join-Path $outputRoot ($scenario.Name + '.json')
        $png = Join-Path $outputRoot ($scenario.Name + '.png')
        $started = [DateTimeOffset]::UtcNow
        dotnet $dll --page treedatagrid $scenario.Flag --smoke-output $json --smoke-image $png
        $exitCode = $LASTEXITCODE
        if (-not (Test-Path -LiteralPath $json)) { throw "$($scenario.Name) produced no result (exit $exitCode)." }
        $result = Get-Content -LiteralPath $json -Raw | ConvertFrom-Json
        $failed = @($result.Checks.PSObject.Properties | Where-Object { $_.Value -ne $true })
        $checks = @($result.Checks.PSObject.Properties)
        if ($scenario.Name -eq 'demo') {
            $capabilities.NativeWindowCreated = $result.Checks.nativeWindowCreated
            $capabilities.Status = if ($capabilities.NativeWindowCreated -eq $true) { 'NativeWindowAvailable' } else { 'EnvironmentUnavailable' }
            $capabilities | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'native-capabilities.json') -Encoding utf8
        }
        if ($exitCode -ne 0 -or $result.Passed -ne $true -or $checks.Count -eq 0 -or $failed.Count -gt 0 -or $result.Error -or
            -not $result.TimestampUtc -or [DateTimeOffset]$result.TimestampUtc -lt $started -or -not (Test-Path -LiteralPath $png)) {
            throw "$($scenario.Name) failed: exit=$exitCode, Passed=$($result.Passed), failed=$($failed.Name -join ', '), Error=$($result.Error)."
        }
        Write-Output "$($scenario.Name): exit=0, Passed=true, $($checks.Count) checks passed."
    }
    Write-Output 'Native programmatic checks completed. Physical input, IME, DPI and screen reader evidence remains separate.'
}
finally { Pop-Location }
