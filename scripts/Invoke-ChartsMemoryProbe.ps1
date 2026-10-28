[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$PythonExecutable,
    [string]$DotnetExecutable = 'dotnet',
    [string]$HostAssembly,
    [string]$FontDirectory,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$memoryRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$memoryOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $memoryOutput) { throw 'Use a new memory diagnostic output directory; attempts are never overwritten.' }
if (!$PythonExecutable) { $PythonExecutable = if ($IsWindows) { 'python' } else { 'python3' } }
if (!$IsWindows -and !$IsLinux) { throw 'Only Windows and Linux are supported.' }
if ($FontDirectory -and !(Test-Path -LiteralPath $FontDirectory -PathType Container)) { throw 'Explicit font directory is missing.' }
[IO.Directory]::CreateDirectory($memoryOutput) | Out-Null
. (Join-Path $PSScriptRoot 'ChartsPerformanceGate.Process.ps1')
$memoryRuns = [Collections.Generic.List[object]]::new()
$memoryStages = [Collections.Generic.List[object]]::new()
$memoryStarted = [DateTimeOffset]::UtcNow.ToString('o')
$memoryError = $null; $memoryPassed = $false; $memoryEvidenceIntegrityPassed = $false; $memoryDiagnosticTargetPassed = $null; $memorySources = @()
$memoryContract = Join-Path $PSScriptRoot 'ChartsMemoryProbe.Contract.json'

function Write-MemoryJson([string]$Path, $Value) {
    $temporary = $Path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    [IO.File]::WriteAllText($temporary, ($Value | ConvertTo-Json -Depth 40))
    [IO.File]::Move($temporary, $Path, $true)
}
function Read-MemoryJson([string]$Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -DateKind String }
function Save-MemoryCampaign {
    Write-MemoryJson (Join-Path $memoryOutput 'campaign.json') ([ordered]@{
        schema = 'charts-memory-campaign/v1'; diagnosticOnly = $true; startedUtc = $memoryStarted
        platform = $(if ($IsWindows) { 'windows' } else { 'linux' })
        pairOrder = @(@('empty','full'), @('full','empty'), @('empty','full'))
        nominalIncrementBytes = 536870912; intervalMs = 20; smapsIntervalMs = 100
        contract = @{ path = [IO.Path]::GetRelativePath($memoryOutput, $memoryContract).Replace('\','/'); sha256 = (Get-FileHash -LiteralPath $memoryContract -Algorithm SHA256).Hash }
        runs = @($memoryRuns); sources = $memorySources
        scope = 'Matched same-OS sampled process-memory diagnostic. No original C7 oracle, instantaneous peak, exact native/GPU, present or physical-hardware qualification.'
        nativeBytes = $null; gpuBytes = $null; originalC7PeakQualification = $null
    })
}
function Stage-Memory([string]$Id, [string]$Executable, [string[]]$Arguments) {
    $receipt = Invoke-ChartsGateProcess -Id $Id -Executable $Executable -Arguments $Arguments -WorkingDirectory $memoryRoot -OutputDirectory (Join-Path $memoryOutput "processes/$Id") -TimeoutSeconds 600
    $memoryStages.Add($receipt)
    return $receipt
}
function Get-MemoryNativeProcessIdentity([int]$ProcessId) {
    if ($ProcessId -le 0) { throw 'Native process identity requires a positive PID.' }
    $begin = [Diagnostics.Stopwatch]::GetTimestamp(); $beginUtc = [DateTimeOffset]::UtcNow.ToString('o')
    $windowsCreation = $null; $linuxStart = $null; $linuxBoot = $null
    if ($IsWindows) {
        $observed = [Diagnostics.Process]::GetProcessById($ProcessId)
        try {
            if ($observed.HasExited) { throw 'Process exited before native creation identity could be observed.' }
            $windowsCreation = $observed.StartTime.ToUniversalTime().ToFileTimeUtc().ToString([Globalization.CultureInfo]::InvariantCulture)
        } finally { $observed.Dispose() }
    } elseif ($IsLinux) {
        $stat = [IO.File]::ReadAllText("/proc/$ProcessId/stat")
        $separator = $stat.IndexOf(' '); $close = $stat.LastIndexOf(')')
        if ($separator -le 0 -or $close -le $separator -or
            [int]::Parse($stat.Substring(0, $separator), [Globalization.CultureInfo]::InvariantCulture) -ne $ProcessId) {
            throw 'Linux stat did not identify the requested process.'
        }
        $fields = $stat.Substring($close + 2).Split([char]' ', [StringSplitOptions]::RemoveEmptyEntries)
        if ($fields.Length -lt 20 -or $fields[0] -in @('Z','X','x')) { throw 'Linux process is exited or has incomplete stat fields.' }
        $linuxStart = [long]::Parse($fields[19], [Globalization.CultureInfo]::InvariantCulture)
        $linuxBoot = [IO.File]::ReadAllText('/proc/sys/kernel/random/boot_id').Trim()
        if ($linuxStart -le 0 -or $linuxBoot -notmatch '^[a-fA-F0-9]{8}(?:-[a-fA-F0-9]{4}){3}-[a-fA-F0-9]{12}$') {
            throw 'Linux process start ticks or boot identity are invalid.'
        }
    } else { throw 'Native process identity is unavailable on this OS.' }
    return [ordered]@{ processId = $ProcessId; windowsCreationFileTimeUtc = $windowsCreation; linuxProcStartTicks = $linuxStart; linuxBootId = $linuxBoot
        readStartedUtc = $beginUtc; readFinishedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        readStartQpc = $begin; readEndQpc = [Diagnostics.Stopwatch]::GetTimestamp(); qpcFrequency = [Diagnostics.Stopwatch]::Frequency }
}
function Assert-MemoryHostReady($Ready, $HostChild, [string]$Token, [string]$Mode) {
    if ($Ready.schema -cne 'charts-memory-probe/v1' -or $Ready.runToken -cne $Token -or $Ready.mode -cne $Mode -or
        $Ready.processId -ne $HostChild.Record.processId -or $Ready.processStartedUtc -notmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$') {
        throw 'Host ready token, PID, mode or canonical UTC format mismatch.'
    }
    $nativeReady = Get-MemoryNativeProcessIdentity $HostChild.Record.processId
    $HostChild.Record.nativeIdentityAtReady = $nativeReady
    Write-MemoryJson $HostChild.ReceiptPath $HostChild.Record
    $nativeLaunch = $HostChild.Record.nativeIdentityAtLaunch
    if ($nativeLaunch.processId -ne $Ready.processId -or $nativeReady.processId -ne $Ready.processId) { throw 'Host native PID changed.' }
    foreach ($key in @('windowsCreationFileTimeUtc','linuxProcStartTicks','linuxBootId')) {
        if ($nativeLaunch[$key] -cne $nativeReady[$key] -or $nativeLaunch[$key] -cne $Ready.$key) {
            throw "Host native creation identity mismatch: $key"
        }
    }
    # Linux .NET StartTime UTC is a per-process conversion of kernel jiffies.
    # Keep both UTC observations; exact PID/start-jiffies/boot identity is primary.
    if ($IsWindows -and $HostChild.Record.parentObservedProcessStartedUtc -cne $Ready.processStartedUtc) {
        throw 'Windows exact creation FILETIME/UTC observations disagree.'
    }
    $HostChild.Record.canonicalProcessStartedUtc = $Ready.processStartedUtc
    $HostChild.Record.hostReadyIdentityValidated = $true
    Write-MemoryJson $HostChild.ReceiptPath $HostChild.Record
}
function Start-MemoryChild([string]$Id, [string]$Executable, [string[]]$Arguments, [string]$Directory, $EnvironmentOverrides) {
    [IO.Directory]::CreateDirectory($Directory) | Out-Null
    $record = [ordered]@{ schemaVersion = 'charts-memory-process-v2'; id = $Id; executable = $Executable; arguments = $Arguments
        workingDirectory = $memoryRoot; processId = $null; processStartedUtc = $null; startedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        parentObservedProcessStartedUtc = $null; canonicalProcessStartedUtc = $null; hostReadyIdentityValidated = $false
        nativeIdentityAtLaunch = $null; nativeIdentityAtReady = $null
        finishedUtc = $null; timeoutSeconds = 120; timedOut = $false; exitCode = $null; passed = $false; error = $null
        stdoutPath = 'stdout.log'; stderrPath = 'stderr.log'; logCapture = 'redirected-base-stream-copy-to-disk-during-process; no retry' }
    $receiptPath = Join-Path $Directory 'process.json'; Write-MemoryJson $receiptPath $record
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $Executable; $info.WorkingDirectory = $memoryRoot; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    if ($EnvironmentOverrides) { foreach ($key in $EnvironmentOverrides.Keys) {
        if ($null -eq $EnvironmentOverrides[$key]) { [void]$info.Environment.Remove($key) }
        else { $info.Environment[$key] = $EnvironmentOverrides[$key] }
    } }
    $stdout = [IO.File]::Open((Join-Path $Directory 'stdout.log'), [IO.FileMode]::CreateNew)
    $stderr = [IO.File]::Open((Join-Path $Directory 'stderr.log'), [IO.FileMode]::CreateNew)
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    try {
        if (!$process.Start()) { throw 'Child did not start.' }
        $record.processId = $process.Id
        $record.nativeIdentityAtLaunch = Get-MemoryNativeProcessIdentity $process.Id
        $record.parentObservedProcessStartedUtc = $process.StartTime.ToUniversalTime().ToString('O')
        $record.processStartedUtc = $record.parentObservedProcessStartedUtc
        Write-MemoryJson $receiptPath $record
        return [pscustomobject]@{ Process = $process; Record = $record; ReceiptPath = $receiptPath; Stdout = $stdout; Stderr = $stderr
            StdoutTask = $process.StandardOutput.BaseStream.CopyToAsync($stdout); StderrTask = $process.StandardError.BaseStream.CopyToAsync($stderr); Closed = $false }
    } catch {
        $record.error = $_.Exception.ToString()
        # A failed native-identity read must not orphan an already started host.
        try {
            if ($null -ne $record.processId -and !$process.HasExited) { $process.Kill($true); [void]$process.WaitForExit(10000) }
            if ($null -ne $record.processId -and $process.HasExited) { $record.exitCode = $process.ExitCode }
        } catch { $record.error += "`nOwned-child cleanup: $($_.Exception.Message)" }
        $record.finishedUtc = [DateTimeOffset]::UtcNow.ToString('o'); Write-MemoryJson $receiptPath $record
        $stdout.Dispose(); $stderr.Dispose(); $process.Dispose(); throw
    }
}
function Finish-MemoryChild($Child, [bool]$Terminate = $false) {
    if ($null -eq $Child -or $Child.Closed) { return }
    try {
        if ($Terminate -and !$Child.Process.HasExited) { $Child.Record.timedOut = $true; $Child.Process.Kill($true) }
        if (!$Child.Process.WaitForExit(10000)) {
            $Child.Record.timedOut = $true; $Child.Process.Kill($true)
            if (!$Child.Process.WaitForExit(10000)) { throw 'Owned child did not exit after termination.' }
        }
        [void]$Child.StdoutTask.GetAwaiter().GetResult(); [void]$Child.StderrTask.GetAwaiter().GetResult()
        $Child.Record.exitCode = $Child.Process.ExitCode
        $Child.Record.passed = !$Child.Record.timedOut -and $Child.Record.exitCode -eq 0
        if (!$Child.Record.passed) { $Child.Record.error = 'Nonzero exit or owned-process timeout; evidence retained without retry.' }
    } catch { $Child.Record.error = $_.Exception.ToString(); $Child.Record.passed = $false }
    finally {
        $Child.Record.finishedUtc = [DateTimeOffset]::UtcNow.ToString('o'); Write-MemoryJson $Child.ReceiptPath $Child.Record
        $Child.Stdout.Dispose(); $Child.Stderr.Dispose(); $Child.Process.Dispose(); $Child.Closed = $true
    }
}
function Wait-MemoryFile([string]$Path, $HostChild, $SamplerChild = $null) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        if ($HostChild.Process.HasExited) { throw "Host exited before handshake: $Path" }
        if ($null -ne $SamplerChild -and $SamplerChild.Process.HasExited) { throw "Sampler exited before handshake: $Path" }
        if ($clock.Elapsed.TotalSeconds -gt 90) { throw "Handshake timeout: $Path" }
        Start-Sleep -Milliseconds 20
    }
    if ((Get-Item -LiteralPath $Path).Length -gt 1048576) { throw 'Handshake exceeded its bounded JSON size.' }
    return Read-MemoryJson $Path
}
function Assert-MemoryControl($Control, $Ready, [string]$Status) {
    if ($Control.runToken -cne $Ready.runToken -or $Control.processId -ne $Ready.processId -or
        $Control.processStartedUtc -cne $Ready.processStartedUtc -or $Control.status -cne $Status) { throw 'Control identity or status mismatch.' }
}
function Get-MemorySources {
    $paths = @('benchmarks/Charts.MemoryProbe/Program.cs','benchmarks/Charts.MemoryProbe/Charts.MemoryProbe.csproj',
        'benchmarks/Charts.Benchmarks/DatasetFactory.cs','scripts/Invoke-ChartsMemoryProbe.ps1','scripts/ChartsMemoryProbe.Contract.json',
        'scripts/charts_memory_sampler.py','scripts/charts_memory_oracle.py','scripts/ChartsPerformanceGate.Process.ps1',
        'Directory.Build.props','Directory.Packages.props','global.json','NuGet.Config')
    if (Test-Path -LiteralPath (Join-Path $memoryRoot 'benchmarks/Charts.MemoryProbe/packages.lock.json')) { $paths += 'benchmarks/Charts.MemoryProbe/packages.lock.json' }
    @(foreach ($relative in $paths) {
        $path = Join-Path $memoryRoot $relative
        [ordered]@{ path = $relative; bytes = (Get-Item -LiteralPath $path).Length; sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    })
}

try {
    if (!$SkipBuild) {
        $project = 'benchmarks/Charts.MemoryProbe/Charts.MemoryProbe.csproj'
        $restoreArgs = @('restore', $project, '--disable-parallel')
        if (Test-Path -LiteralPath (Join-Path $memoryRoot 'benchmarks/Charts.MemoryProbe/packages.lock.json')) { $restoreArgs += '--locked-mode' }
        $restore = Stage-Memory 'restore' $DotnetExecutable $restoreArgs
        if (!$restore.passed) { throw 'Memory diagnostic restore failed.' }
        $build = Stage-Memory 'build' $DotnetExecutable @('build', $project, '-c', 'Release', '--no-restore', '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false')
        if (!$build.passed) { throw 'Memory diagnostic build failed.' }
    }
    if (!$HostAssembly) { $HostAssembly = Join-Path $memoryRoot 'benchmarks/Charts.MemoryProbe/bin/Release/net10.0/Charts.MemoryProbe.dll' }
    $HostAssembly = [IO.Path]::GetFullPath($HostAssembly)
    if (!(Test-Path -LiteralPath $HostAssembly -PathType Leaf)) { throw 'Built host assembly is missing.' }
    $memorySources = @(Get-MemorySources); Write-MemoryJson (Join-Path $memoryOutput 'source-before.json') $memorySources
    $orders = @(@('empty','full'), @('full','empty'), @('empty','full'))
    for ($pair = 0; $pair -lt $orders.Count; $pair++) {
        for ($position = 0; $position -lt 2; $position++) {
            $mode = $orders[$pair][$position]; $id = "pair-$($pair + 1)-$mode"
            $runRelative = "runs/$id"; $runRoot = Join-Path $memoryOutput $runRelative
            [IO.Directory]::CreateDirectory($runRoot) | Out-Null
            $controlDir = Join-Path $runRoot 'control'; [IO.Directory]::CreateDirectory($controlDir) | Out-Null
            $token = [Guid]::NewGuid().ToString('N'); $hostChild = $null; $samplerChild = $null
            $row = [ordered]@{ pairIndex = $pair + 1; position = $position + 1; mode = $mode; runToken = $token
                host = "$runRelative/host"; sampler = "$runRelative/sampler"; control = "$runRelative/control"
                hostReceipt = "$runRelative/host-process/process.json"; samplerReceipt = "$runRelative/sampler-process/process.json"
                passed = $false; error = $null }
            $memoryRuns.Add($row); Save-MemoryCampaign
            try {
                $overrides = @{}
                foreach ($name in @('XDG_CACHE_HOME','XDG_CONFIG_HOME','DOTNET_CLI_HOME','TMPDIR')) {
                    $path = Join-Path $runRoot "private-caches/$name"; [IO.Directory]::CreateDirectory($path) | Out-Null; $overrides[$name] = $path
                }
                $overrides['AVALONIA_GLOBAL_SCALE_FACTOR'] = $null; $overrides['AVALONIA_SCREEN_SCALE_FACTORS'] = $null
                if ($IsLinux) {
                    $fontCache = Join-Path $runRoot 'private-caches/font-cache'; [IO.Directory]::CreateDirectory($fontCache) | Out-Null
                    $fontFile = Join-Path $runRoot 'private-caches/fonts.conf'
                    $extra = if ($FontDirectory) { '<dir>' + [Security.SecurityElement]::Escape([IO.Path]::GetFullPath($FontDirectory)) + '</dir>' } else { '' }
                    [IO.File]::WriteAllText($fontFile, "<?xml version='1.0'?><!DOCTYPE fontconfig SYSTEM 'urn:fontconfig:fonts.dtd'><fontconfig><cachedir>$([Security.SecurityElement]::Escape($fontCache))</cachedir><include ignore_missing='no'>/etc/fonts/fonts.conf</include>$extra</fontconfig>")
                    $overrides['FONTCONFIG_FILE'] = $fontFile
                }
                $hostDir = Join-Path $runRoot 'host'
                $hostArgs = @($HostAssembly, '--mode', $mode, '--output', $hostDir, '--control-dir', $controlDir, '--run-token', $token)
                $hostChild = Start-MemoryChild "$id-host" $DotnetExecutable $hostArgs (Join-Path $runRoot 'host-process') $overrides
                $ready = Wait-MemoryFile (Join-Path $hostDir 'ready.json') $hostChild
                Assert-MemoryHostReady $ready $hostChild $token $mode
                $samplerArgs = @((Join-Path $PSScriptRoot 'charts_memory_sampler.py'), '--ready-file', (Join-Path $hostDir 'ready.json'),
                    '--control-dir', $controlDir, '--output', (Join-Path $runRoot 'sampler'), '--interval-ms', '20', '--smaps-interval-ms', '100', '--timeout-seconds', '90')
                $samplerChild = Start-MemoryChild "$id-sampler" $PythonExecutable $samplerArgs (Join-Path $runRoot 'sampler-process') @{}
                $samplerReady = Wait-MemoryFile (Join-Path $controlDir 'sampler-ready.json') $hostChild $samplerChild
                Assert-MemoryControl $samplerReady $ready 'ready'
                [void](Wait-MemoryFile (Join-Path $hostDir 'work-complete.json') $hostChild $samplerChild)
                $complete = Wait-MemoryFile (Join-Path $controlDir 'sampler-complete.json') $hostChild $samplerChild
                Assert-MemoryControl $complete $ready 'completed'
                Finish-MemoryChild $samplerChild
                if (!$samplerChild.Record.passed) { throw 'Sampler failed; host was not released as a successful run.' }
                Write-MemoryJson (Join-Path $controlDir 'release.json') ([ordered]@{ runToken = $token; processId = $ready.processId; processStartedUtc = $ready.processStartedUtc; status = 'release' })
                Finish-MemoryChild $hostChild
                $row.passed = $hostChild.Record.passed
                if (!$row.passed) { throw 'Host failed after release.' }
            } catch { $row.error = $_.Exception.ToString() }
            finally { Finish-MemoryChild $samplerChild $true; Finish-MemoryChild $hostChild $true; Save-MemoryCampaign }
        }
    }
    $after = @(Get-MemorySources); Write-MemoryJson (Join-Path $memoryOutput 'source-after.json') $after
    if (($memorySources | ConvertTo-Json -Depth 8 -Compress) -cne ($after | ConvertTo-Json -Depth 8 -Compress)) { throw 'Memory diagnostic sources changed during the campaign.' }
    $oracle = Stage-Memory 'independent-oracle' $PythonExecutable @((Join-Path $PSScriptRoot 'charts_memory_oracle.py'), '--campaign', (Join-Path $memoryOutput 'campaign.json'), '--output', (Join-Path $memoryOutput 'oracle.json'))
    $oracleResult = Read-MemoryJson (Join-Path $memoryOutput 'oracle.json')
    $memoryEvidenceIntegrityPassed = $oracleResult.evidenceIntegrityPassed -eq $true -and @($memoryRuns | Where-Object { !$_.passed }).Count -eq 0
    $memoryDiagnosticTargetPassed = $oracleResult.diagnosticIncrementTargetPassed
    if ($oracle.exitCode -eq 0 -and (!$memoryEvidenceIntegrityPassed -or $memoryDiagnosticTargetPassed -ne $true)) { throw 'Oracle exit0 contradicts its evidence/diagnostic result.' }
    if ($oracle.exitCode -eq 1 -and (!$memoryEvidenceIntegrityPassed -or $memoryDiagnosticTargetPassed -ne $false)) { throw 'Oracle exit1 is reserved for an intact failed nominal diagnostic target.' }
    if ($oracle.exitCode -notin @(0,1)) { $memoryEvidenceIntegrityPassed = $false }
    $memoryPassed = $memoryEvidenceIntegrityPassed -and $memoryDiagnosticTargetPassed -eq $true -and $oracle.exitCode -eq 0
    if (!$memoryPassed) {
        $memoryError = if ($memoryEvidenceIntegrityPassed) { 'Sampled full-minus-empty nominal diagnostic target failed; evidence integrity passed. Original C7 qualifications are unchanged.' }
            else { 'One or more attempts or independent evidence guards failed; retained without retry.' }
    }
} catch { $memoryEvidenceIntegrityPassed = $false; $memoryError = $_.Exception.ToString() }
finally {
    Save-MemoryCampaign
    Write-MemoryJson (Join-Path $memoryOutput 'results.json') ([ordered]@{
        schema = 'charts-memory-execution/v1'; diagnosticOnly = $true; startedUtc = $memoryStarted; finishedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        passed = $memoryPassed; error = $memoryError; runs = @($memoryRuns); stages = @($memoryStages)
        evidenceIntegrityPassed = $memoryEvidenceIntegrityPassed; diagnosticIncrementTargetPassed = $memoryDiagnosticTargetPassed
        nativeBytes = $null; gpuBytes = $null; originalC7PeakQualification = $null
    })
}
if (!$memoryPassed) { throw "Memory diagnostic did not pass: $memoryError" }
