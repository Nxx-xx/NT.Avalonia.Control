# Supervised child processes used by the performance gate. No product work runs when imported.
function Invoke-ChartsGateProcess {
    param(
        [Parameter(Mandatory)][string]$Id,
        [Parameter(Mandatory)][string]$Executable,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][string]$OutputDirectory,
        [ValidateRange(1,7200)][int]$TimeoutSeconds = 600
    )
    if (Test-Path -LiteralPath $OutputDirectory) { throw "Stage output already exists: $OutputDirectory" }
    [IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
    $receipt = [ordered]@{
        schemaVersion = 'charts-ci-process-v1'; id = $Id; executable = $Executable
        arguments = @($Arguments); workingDirectory = $WorkingDirectory
        startedUtc = [DateTimeOffset]::UtcNow.ToString('O'); finishedUtc = $null
        timeoutSeconds = $TimeoutSeconds; timedOut = $false; exitCode = $null
        processId = $null; passed = $false; error = $null
        stdoutPath = 'stdout.log'; stderrPath = 'stderr.log'
        logCapture = 'redirected-base-stream-copy-to-disk-during-process; no retry'
    }
    $receiptPath = Join-Path $OutputDirectory 'process.json'
    $receipt | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $receiptPath -Encoding utf8
    $process = $null; $stdoutFile = $null; $stderrFile = $null
    try {
        $start = [Diagnostics.ProcessStartInfo]::new()
        $start.FileName = $Executable; $start.WorkingDirectory = $WorkingDirectory
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
        foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
        $stdoutFile = [IO.File]::Open((Join-Path $OutputDirectory 'stdout.log'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $stderrFile = [IO.File]::Open((Join-Path $OutputDirectory 'stderr.log'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
        $process = [Diagnostics.Process]::new(); $process.StartInfo = $start
        if (-not $process.Start()) { throw 'Child process failed to start.' }
        $receipt.processId = $process.Id
        $receipt | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $receiptPath -Encoding utf8
        $stdoutTask = $process.StandardOutput.BaseStream.CopyToAsync($stdoutFile)
        $stderrTask = $process.StandardError.BaseStream.CopyToAsync($stderrFile)
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $receipt.timedOut = $true
            $process.Kill($true)
            if (-not $process.WaitForExit(10000)) { throw 'Child process tree did not exit after kill.' }
        }
        [void]$stdoutTask.GetAwaiter().GetResult(); [void]$stderrTask.GetAwaiter().GetResult()
        $receipt.exitCode = $process.ExitCode
        $receipt.passed = -not $receipt.timedOut -and $receipt.exitCode -eq 0
        if ($receipt.timedOut) { $receipt.error = 'Wall timeout; process tree killed; partial evidence retained.' }
        elseif ($receipt.exitCode -ne 0) { $receipt.error = 'Nonzero child exit; all output retained without retry.' }
    } catch {
        $receipt.error = $_.Exception.ToString()
        if ($null -ne $process) {
            try { if (-not $process.HasExited) { $process.Kill($true); [void]$process.WaitForExit(10000) } } catch { }
        }
    }
    finally {
        if ($null -ne $stdoutFile) { $stdoutFile.Dispose() }
        if ($null -ne $stderrFile) { $stderrFile.Dispose() }
        if ($null -ne $process) { $process.Dispose() }
        $receipt.finishedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        $receipt | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $receiptPath -Encoding utf8
    }
    return [pscustomobject]$receipt
}
