# Definitions only. Dot-sourcing never builds, starts a native window, or changes the clipboard.
function Get-MdNativeCanonicalFilePath([string]$Path) {
    $file = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if ($file.PSIsContainer) { throw 'Expected an executable/file, not a directory.' }
    $target = $file.ResolveLinkTarget($true)
    if ($null -ne $target) { return $target.FullName }
    return $file.FullName
}

function Resolve-MdNativeApplication {
    param([Parameter(Mandatory)][string]$Name)
    # PATH may contain both /usr/bin and /bin (or multiple SDKs). Never coerce multiple Source paths to one string.
    $command=Get-Command -Name $Name -CommandType Application -All -ErrorAction Stop|Select-Object -First 1
    if($null -eq $command){throw "Application was not found: $Name"}
    return Get-MdNativeCanonicalFilePath $command.Source
}

function Initialize-MdNativeCapture {
    if ('MarkdownNativeCI.BoundedCapture' -as [type]) { return }
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
namespace MarkdownNativeCI {
    public static class OwnedProcessImage {
        [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle process, int flags, StringBuilder path, ref int length);
        public static string GetWindowsPath(Process process) {
            var path = new StringBuilder(32768);
            int length = path.Capacity;
            if (!QueryFullProcessImageName(process.SafeHandle, 0, path, ref length))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return path.ToString(0, length);
        }
    }
    public sealed class CaptureResult {
        public string Path { get; set; }
        public long TotalBytes { get; set; }
        public long StoredBytes { get; set; }
        public bool Truncated { get; set; }
        public bool Incomplete { get; set; }
        public string Error { get; set; }
    }
    public static class BoundedCapture {
        public static async Task<CaptureResult> Drain(Stream input, string path, int limit, CancellationToken token) {
            var result = new CaptureResult { Path = path };
            try {
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) {
                    var buffer = new byte[8192];
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0) {
                        result.TotalBytes += read;
                        int write = (int)Math.Min(read, Math.Max(0L, limit - result.StoredBytes));
                        if (write != 0) {
                            await output.WriteAsync(buffer, 0, write, token).ConfigureAwait(false);
                            result.StoredBytes += write;
                        }
                    }
                    await output.FlushAsync(token).ConfigureAwait(false);
                }
            } catch (Exception error) { result.Incomplete = true; result.Error = error.GetType().Name; }
            result.Truncated = result.TotalBytes > result.StoredBytes;
            return result;
        }
    }
}
'@
}

function Get-MdNativeStartedModulePath([Diagnostics.Process]$Process) {
    if([OperatingSystem]::IsWindows()){
        # The initial module enumeration may contain only ntdll. Query the retained process image instead.
        Initialize-MdNativeCapture
        return [MarkdownNativeCI.OwnedProcessImage]::GetWindowsPath($Process)
    }
    $Process.Refresh()
    return $Process.MainModule.FileName
}

function Get-MdNativeStartedStartTime([Diagnostics.Process]$Process) {
    return $Process.StartTime.ToUniversalTime().Ticks
}

function Test-MdNativeExecutablePathEqual {
    param(
        [Parameter(Mandatory)][string]$Left,
        [Parameter(Mandatory)][string]$Right,
        [ValidateSet('Windows','Linux')][string]$Platform = $(if([OperatingSystem]::IsWindows()){'Windows'}else{'Linux'})
    )
    # FileInfo.FullName retains input casing; the OS image query may use different
    # casing for the same Windows executable. This applies only to image paths.
    $comparison = if($Platform -ceq 'Windows'){[StringComparison]::OrdinalIgnoreCase}else{[StringComparison]::Ordinal}
    return [string]::Equals($Left, $Right, $comparison)
}

function Stop-MdNativeOwnedProcess {
    param([Diagnostics.Process]$Process, [int]$ExpectedId, [long]$ExpectedStartTimeUtcTicks, [string]$ExpectedExecutable)
    if ($Process.HasExited) { return $true }
    try {
        # Use the retained instance returned by our own Start, never reacquire a reused PID.
        $actualStart=Get-MdNativeStartedStartTime $Process
        $actual=Get-MdNativeCanonicalFilePath (Get-MdNativeStartedModulePath $Process)
    }
    catch { if($Process.HasExited){return $true};throw }
    if($Process.HasExited){return $true}
    if($Process.Id -ne $ExpectedId -or ($ExpectedStartTimeUtcTicks -gt 0 -and $actualStart -ne $ExpectedStartTimeUtcTicks) -or
        -not (Test-MdNativeExecutablePathEqual $actual $ExpectedExecutable)){throw 'Owned process executable/start identity differs; refusing termination.'}
    try { $Process.Kill($true) }
    catch [InvalidOperationException] { if (-not $Process.HasExited) { throw } }
    catch [ComponentModel.Win32Exception] { if (-not $Process.HasExited) { throw } }
    return $Process.WaitForExit(5000)
}

function Invoke-MdNativeProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Executable,
        [string[]]$ArgumentList = @(),
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][string]$OutputDirectory,
        [ValidateRange(1,7200)][int]$TimeoutSeconds = 120,
        [ValidateRange(1024,16777216)][int]$MaximumLogBytes = 1048576
    )
    Initialize-MdNativeCapture
    if (Test-Path -LiteralPath $OutputDirectory) { throw 'Process evidence directory exists; preserve it and use a fresh path.' }
    $exe = Get-MdNativeCanonicalFilePath $Executable
    $working = (Get-Item -LiteralPath $WorkingDirectory -Force -ErrorAction Stop).FullName
    $directory = [IO.Path]::GetFullPath($OutputDirectory)
    $null = New-Item -ItemType Directory -Path $directory
    $result = [ordered]@{
        StartedUtc=[DateTimeOffset]::UtcNow.ToString('O'); CompletedUtc=$null; Started=$false;
        ProcessId=$null; StartTimeUtcTicks=$null; Executable=$exe; Arguments=@($ArgumentList); WorkingDirectory=$working;
        TimeoutSeconds=$TimeoutSeconds; MaximumLogBytesPerStream=$MaximumLogBytes;
        ExitCode=$null; ExitObserved=$false; TimedOut=$false; OwnedIdentityVerified=$false; TerminationVerified=$false;
        RetainedProcessOwnershipVerified=$false; StartTimeObserved=$false; ModuleObserved=$false; ObservedExecutable=$null;
        ImagePathObservationMethod=if([OperatingSystem]::IsWindows()){'Win32.QueryFullProcessImageName'}else{'Process.MainModule'};
        IdentityObservation='Pending'; IdentityMismatch=$false;
        StreamsComplete=$false; Stdout=$null; Stderr=$null; Failure=$null; Outcome='Failed'
    }
    $process = [Diagnostics.Process]::new()
    $cancel = [Threading.CancellationTokenSource]::new()
    $stdoutTask = $null; $stderrTask = $null
    try {
        $process.StartInfo.FileName = $exe
        $process.StartInfo.WorkingDirectory = $working
        $process.StartInfo.UseShellExecute = $false
        $process.StartInfo.CreateNoWindow = $true
        $process.StartInfo.RedirectStandardOutput = $true
        $process.StartInfo.RedirectStandardError = $true
        foreach ($argument in $ArgumentList) { $process.StartInfo.ArgumentList.Add($argument) }
        $deadline=[Diagnostics.Stopwatch]::StartNew()
        if (-not $process.Start()) { throw 'The owned child process did not start.' }
        $result.Started=$true; $result.ProcessId=$process.Id; $result.RetainedProcessOwnershipVerified=$true
        # Drain before observing optional process metadata: a console child may already be exiting.
        $stdoutTask=[MarkdownNativeCI.BoundedCapture]::Drain($process.StandardOutput.BaseStream,(Join-Path $directory 'runtime-stdout.log'),$MaximumLogBytes,$cancel.Token)
        $stderrTask=[MarkdownNativeCI.BoundedCapture]::Drain($process.StandardError.BaseStream,(Join-Path $directory 'runtime-stderr.log'),$MaximumLogBytes,$cancel.Token)
        while($true){
            try{
                $start=Get-MdNativeStartedStartTime $process
                if($null -ne $start){$result.StartTimeUtcTicks=$start;$result.StartTimeObserved=$true}
            }catch{if(-not $process.HasExited -and $deadline.ElapsedMilliseconds -ge 1000){throw}}
            try{
                $modulePath=Get-MdNativeStartedModulePath $process
                if(-not [string]::IsNullOrWhiteSpace($modulePath)){
                    $result.ObservedExecutable=Get-MdNativeCanonicalFilePath $modulePath
                    $result.ModuleObserved=$true
                    if(-not (Test-MdNativeExecutablePathEqual $result.ObservedExecutable $exe)){$result.IdentityMismatch=$true;throw 'The started process executable differs from the requested executable.'}
                }
            }catch{if($result.IdentityMismatch -or (-not $process.HasExited -and $deadline.ElapsedMilliseconds -ge 1000)){throw}}
            if($result.StartTimeObserved -and $result.ModuleObserved){$result.OwnedIdentityVerified=$true;$result.IdentityObservation='Observed';break}
            if($process.HasExited){$result.IdentityObservation='ExitedBeforeCompleteObservation';break}
            if($deadline.ElapsedMilliseconds -ge [Math]::Min(1000,$TimeoutSeconds*1000)){
                $result.IdentityObservation='StartupObservationTimedOut';throw 'The live owned process identity was not observable within its bounded startup deadline.'
            }
            Start-Sleep -Milliseconds 10
        }
        $remaining=[int][Math]::Max(0,$TimeoutSeconds*1000-$deadline.ElapsedMilliseconds)
        if (-not $process.WaitForExit($remaining)) {
            $result.TimedOut=$true
            $result.TerminationVerified=Stop-MdNativeOwnedProcess $process $result.ProcessId $result.StartTimeUtcTicks $exe
            if (-not $result.TerminationVerified) { throw 'Timed-out owned child did not exit within the termination deadline.' }
        }
        else { $result.TerminationVerified=$true }
        # Keep the producer's real exit, including nonzero values; a timeout is always failure.
        if ($process.HasExited) { $result.ExitCode=$process.ExitCode; $result.ExitObserved=$true }
    }
    catch { $result.Failure=$_.Exception.Message }
    finally {
        if ($result.Started) {
            try {
                if (-not $process.HasExited) {
                    $result.TerminationVerified=Stop-MdNativeOwnedProcess $process $result.ProcessId $result.StartTimeUtcTicks $exe
                    if (-not $result.TerminationVerified) { throw 'Owned process cleanup could not verify exit.' }
                }
                if ($process.HasExited) { $result.ExitCode=$process.ExitCode; $result.ExitObserved=$true }
            }
            catch { $result.Failure=(@($result.Failure,$_.Exception.Message)|Where-Object {$_}) -join '; ' }
        }
        if ($null -ne $stdoutTask -and $null -ne $stderrTask) {
            $both=[Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($stdoutTask,$stderrTask))
            if (-not $both.Wait(5000)) {
                $cancel.Cancel()
                $process.StandardOutput.Close(); $process.StandardError.Close()
                $null=$both.Wait(1000)
            }
            if ($stdoutTask.IsCompletedSuccessfully) { $result.Stdout=$stdoutTask.Result }
            if ($stderrTask.IsCompletedSuccessfully) { $result.Stderr=$stderrTask.Result }
            $result.StreamsComplete=$null -ne $result.Stdout -and $null -ne $result.Stderr -and
                -not $result.Stdout.Incomplete -and -not $result.Stderr.Incomplete
            if (-not $result.StreamsComplete -and -not $result.Failure) { $result.Failure='Output streams did not finish within their bounded deadline.' }
        }
        $cancel.Cancel(); $cancel.Dispose(); $process.Dispose()
        $result.CompletedUtc=[DateTimeOffset]::UtcNow.ToString('O')
        $completedOwnership=$result.OwnedIdentityVerified -or ($result.RetainedProcessOwnershipVerified -and $result.ExitObserved -and -not $result.IdentityMismatch -and $result.IdentityObservation -ceq 'ExitedBeforeCompleteObservation')
        $result.Outcome=if ($result.TimedOut) { 'TimedOut' } elseif ($result.Started -and $completedOwnership -and -not $result.Failure -and $result.TerminationVerified -and $result.StreamsComplete) { 'Completed' } else { 'Failed' }
        $result|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $directory 'supervisor-result.json') -Encoding utf8
    }
    return [pscustomobject]$result
}

function Get-MdNativePolicyBudget {
    param([Parameter(Mandatory)][ValidateSet('Windows','Linux')][string]$Platform)
    if ($Platform -eq 'Linux') { return 51 }; return 50
}

function Test-MdNativeCompletedProcess($Process) {
    $completedOwnership=($Process.OwnedIdentityVerified -is [bool] -and $Process.OwnedIdentityVerified) -or
        ($Process.RetainedProcessOwnershipVerified -is [bool] -and $Process.RetainedProcessOwnershipVerified -and
            $Process.ExitObserved -is [bool] -and $Process.ExitObserved -and
            $Process.IdentityMismatch -is [bool] -and -not $Process.IdentityMismatch -and
            $Process.IdentityObservation -ceq 'ExitedBeforeCompleteObservation')
    return $Process.Outcome -ceq 'Completed' -and $Process.Started -is [bool] -and $Process.Started -and
        $Process.TimedOut -is [bool] -and -not $Process.TimedOut -and
        $completedOwnership -and
        $Process.TerminationVerified -is [bool] -and $Process.TerminationVerified -and
        $Process.StreamsComplete -is [bool] -and $Process.StreamsComplete -and $null -eq $Process.Failure
}

function New-MdNativeBuildState {
    param([bool]$NoBuild=$false)
    return [ordered]@{SourceBuildRequested=(-not $NoBuild);SourceBuildPerformed=$false;BuildStarted=$false;BuildSucceeded=$false}
}

function Set-MdNativeBuildState {
    param([Parameter(Mandatory)][Collections.IDictionary]$State,[Parameter(Mandatory)]$ProcessResult)
    if($State['SourceBuildRequested'] -isnot [bool] -or -not $State['SourceBuildRequested']){throw 'NoBuild cannot record a requested source build.'}
    $started=$ProcessResult.Started -is [bool] -and $ProcessResult.Started
    # Performed means the owned build command actually started, not that restore was requested or succeeded.
    $State['BuildStarted']=$started
    $State['SourceBuildPerformed']=$started
    $zero=$null -ne $ProcessResult.ExitCode -and $ProcessResult.ExitCode -is [ValueType] -and
        $ProcessResult.ExitCode -isnot [bool] -and [double]$ProcessResult.ExitCode -eq 0
    $State['BuildSucceeded']=$started -and $zero -and (Test-MdNativeCompletedProcess $ProcessResult)
}

function Get-MdNativeFormalArguments {
    param([Parameter(Mandatory)][ValidateRange(1,5)][int]$Round,[Parameter(Mandatory)][string]$OutputPath)
    # NativeBenchmarkRunner fixes formal warmup=10. No pilot, partial --only, or sample override is offered.
    return @('--round',[string]$Round,'--samples','100','--stream-sessions','100','--long-stream-sessions','100','--output',$OutputPath)
}

function Get-MdNativeFileHashes {
    param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][string[]]$RelativePaths)
    $root=(Get-Item -LiteralPath $Directory -Force).FullName
    $hashes=[ordered]@{}
    foreach($relative in $RelativePaths) {
        $path=[IO.Path]::GetFullPath((Join-Path $root $relative))
        if (-not $path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::Ordinal)) { throw 'Relative evidence path escapes the directory.' }
        $hashes[$relative]=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
    return $hashes
}

function Get-MdNativeSourceFiles {
    param([Parameter(Mandatory)][string]$SourceRoot)
    $root=Get-Item -LiteralPath $SourceRoot -Force -ErrorAction Stop
    if(-not $root.PSIsContainer){throw 'SourceRoot must be a directory.'}
    $queue=[Collections.Generic.Queue[string]]::new()
    foreach($name in @('src','samples','tests','scripts','benchmarks','docs','.github')){
        $path=Join-Path $root.FullName $name
        if(Test-Path -LiteralPath $path){$queue.Enqueue($path)}
    }
    $files=[Collections.Generic.List[object]]::new()
    foreach($name in @('Directory.Build.props','Directory.Packages.props','NuGet.Config','global.json','NT.AvaloniaControl.slnx','.editorconfig','README.md')){
        $path=Join-Path $root.FullName $name
        # Literal hidden files require -Force on Linux as well as Windows providers.
        if(Test-Path -LiteralPath $path){$files.Add((Get-Item -LiteralPath $path -Force -ErrorAction Stop))}
    }
    while($queue.Count){
        foreach($file in Get-ChildItem -LiteralPath $queue.Dequeue() -Force -ErrorAction Stop){
            if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Source provenance does not follow symbolic links/reparse points.'}
            if($file.PSIsContainer){
                if($file.Name -notin @('bin','obj','artifacts','.git','.packages','node_modules','.nuget')){$queue.Enqueue($file.FullName)}
            }
            else{$files.Add($file)}
        }
    }
    return @($files|Sort-Object FullName|ForEach-Object {
        [pscustomobject]@{Path=[IO.Path]::GetRelativePath($root.FullName,$_.FullName).Replace('\','/');Length=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
}

function Assert-MdNativeFileHashes {
    param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][Collections.IDictionary]$Expected)
    $actual=Get-MdNativeFileHashes -Directory $Directory -RelativePaths @($Expected.Keys)
    foreach($name in $Expected.Keys) { if($actual[$name] -cne $Expected[$name]) { throw "Frozen file changed: $name" } }
}

function Get-MdNativeSmokeVerdict {
    param([Parameter(Mandatory)]$Report,[Parameter(Mandatory)]$ProcessResult,[Parameter(Mandatory)][bool]$ImagePresent)
    $failures=[Collections.Generic.List[string]]::new()
    $checks=@($Report.检查.PSObject.Properties)
    $clipboard=$Report.检查.剪贴板测试已执行
    $pending=$clipboard -is [bool] -and -not $clipboard
    $restore=@('空剪贴板状态已恢复','剪贴板原文本已恢复')|Where-Object {$null -ne $Report.检查.PSObject.Properties[$_]}
    $functional=@($checks|Where-Object {$_.Name -ne '剪贴板测试已执行' -and $_.Name -ne '真实剪贴板文本复制' -and $_.Name -notin @('空剪贴板状态已恢复','剪贴板原文本已恢复')})
    $expected=@(
        '原生窗口已创建','Markdown页面已选择','模板部件可用','跨段落代码表格全文选择',
        '跨线程流式最终提交','流式同步保留MVVM绑定和选区','宿主外部绑定替换优先','链接键盘激活交给宿主','换肤保留全文和选区',
        '图片增长前占位可见','图片增高保持阅读锚点','用户上滚暂停底部跟随','回到底部恢复跟随','页面停用取消会话','页面重新激活不恢复旧会话',
        '自定义节点使用原生文本几何','单图片服务局部覆盖与显式禁用','单代码块高亮覆盖与根继承','自定义节点与局部服务保持全文投影',
        '原生提示NOTE图标与复制','原生提示TIP图标与复制','原生提示IMPORTANT图标与复制','原生提示WARNING图标与复制','原生提示CAUTION图标与复制',
        '五类提示使用不同矢量图形','默认图片file原生PNG解码与尺寸','默认图片loader释放保留调用方租约','默认图片独立租约与重复释放',
        '默认图片file租约释放原生bitmap','默认图片host avares原生PNG解码与尺寸','默认图片avares租约释放原生bitmap',
        '默认图片file编码超限拒绝并关闭输入','默认图片avares编码超限拒绝','默认图片解码后6pixel超过4pixel拒绝',
        '默认图片回环HTTP原生PNG解码与尺寸','默认图片HTTP保留宿主client和已有租约','默认图片HTTP租约释放原生bitmap',
        '默认图片HTTP声明编码超限拒绝','默认图片HTTP分块编码超限拒绝','默认图片回环服务已取消并关闭'
    )
    $functionalPassed=$functional.Count -eq $expected.Count -and @($functional|Where-Object {$_.Name -cnotin $expected -or $_.Value -isnot [bool] -or -not $_.Value}).Count -eq 0
    if (-not $functionalPassed) { $failures.Add('Expected all40 non-clipboard native checks to pass.') }
    if ($Report.通过 -isnot [bool] -or $null -ne $Report.错误) { $failures.Add('Smoke declared result/error is invalid.') }
    if (-not (Test-MdNativeCompletedProcess $ProcessResult) -or -not $ImagePresent) { $failures.Add('Native process, image, or supervision is incomplete.') }
    if ($pending) {
        if ($checks.Count -ne 41 -or $restore.Count -ne 0 -or $null -ne $Report.检查.PSObject.Properties['真实剪贴板文本复制'] -or
            $Report.通过 -ne $false -or $ProcessResult.ExitCode -ne 1) { $failures.Add('Clipboard Pending must preserve an unexecuted test and original failed exit/verdict.') }
    }
    else {
        if ($clipboard -isnot [bool] -or -not $clipboard -or $restore.Count -ne 1 -or $checks.Count -ne 43 -or
            @($checks|Where-Object {$_.Value -isnot [bool] -or -not $_.Value}).Count -ne 0 -or $Report.通过 -ne $true -or $ProcessResult.ExitCode -ne 0) { $failures.Add('Full smoke requires43 exact true checks, real copy/restore and producer exit0.') }
    }
    $status=if($failures.Count){'Failed'}elseif($pending){'Pending'}else{'Passed'}
    return [pscustomobject]@{Status=$status;Passed=$status -eq 'Passed';FunctionalChecksPassed=$functionalPassed;ClipboardStatus=if($status -eq 'Failed'){'Failed'}elseif($pending){'Pending'}else{'Passed'};CheckCount=$checks.Count;Failures=@($failures|ForEach-Object {$_})}
}

function Get-MdNativeRoundVerdict {
    param([Parameter(Mandatory)]$Report,[Parameter(Mandatory)]$ProcessResult,[Parameter(Mandatory)][int]$ExpectedRound,[Parameter(Mandatory)][Collections.IDictionary]$ExpectedFour)
    $failures=[Collections.Generic.List[string]]::new()
    if(-not (Test-MdNativeCompletedProcess $ProcessResult)) { $failures.Add('Owned native process did not complete.') }
    if($Report.正式采样 -isnot [bool] -or -not $Report.正式采样 -or $Report.轮次 -ne $ExpectedRound -or
        $Report.进程ID -ne $ProcessResult.ProcessId -or $null -ne $Report.错误) { $failures.Add('Formal report process/round/completion differs.') }
    if($Report.全部通过 -isnot [bool] -or $ProcessResult.ExitCode -ne $(if($Report.全部通过 -eq $true){0}else{1})) { $failures.Add('Original producer exit/verdict differs.') }
    if(@($Report.场景).Count -ne 11 -or @($Report.场景|Where-Object {$_.预热次数 -ne 10 -or @($_.样本).Count -lt 100}).Count -ne 0 -or
        @($Report.流式完成).Count -ne 100 -or @($Report.长会话流式完成).Count -ne 100 -or @($Report.虚拟化).Count -ne 100) { $failures.Add('All11 formal groups,100 samples/sessions and10 warmups are required.') }
    foreach($name in $ExpectedFour.Keys) { if($Report.环境.$name -cne $ExpectedFour[$name]) { $failures.Add("Native four-DLL identity differs: $name") } }
    return [pscustomobject]@{Valid=$failures.Count -eq 0;OriginalProducerPassed=$Report.全部通过;Failures=@($failures|ForEach-Object {$_})}
}
