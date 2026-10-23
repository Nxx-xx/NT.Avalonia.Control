param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$testRepo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$testSourcePath = $PSCommandPath
$testSourceHash = (Get-FileHash -LiteralPath $testSourcePath -Algorithm SHA256).Hash
$helperSource = Join-Path $testRepo 'scripts/Test-MarkdownNative.Helpers.ps1'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $testRepo ('artifacts/guard-tests/markdown-native-' + [guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Native contract output directory must be fresh.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$helper = Join-Path $OutputDirectory 'helper-under-test.ps1'
Copy-Item -LiteralPath $helperSource -Destination $helper
$helperHash = (Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash
. $helper
$shell = (Get-Process -Id $PID).Path
$results = [Collections.Generic.List[object]]::new()
$windowsPathCaseAliasProcessExecuted = $false

function Assert-Contract([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-ContractThrows([scriptblock]$Action, [string]$Message) {
    $threw = $false
    try { & $Action | Out-Null } catch { $threw = $true }
    Assert-Contract $threw $Message
}
function Test-Contract([string]$Name, [scriptblock]$Body) {
    try { & $Body; $results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Error = $null }) }
    catch { $results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Error = $_.Exception.Message }) }
}
function New-CompletedProcess {
    [pscustomobject]@{ Started = $true; ProcessId = 101; ExitCode = 0; TimedOut = $false; OwnedIdentityVerified = $true; TerminationVerified = $true; StreamsComplete = $true; Failure = $null; Outcome = 'Completed' }
}
function New-SmokeReport {
    $names = @(
        '原生窗口已创建','Markdown页面已选择','模板部件可用','跨段落代码表格全文选择','真实剪贴板文本复制','剪贴板测试已执行',
        '跨线程流式最终提交','流式同步保留MVVM绑定和选区','宿主外部绑定替换优先','链接键盘激活交给宿主','换肤保留全文和选区',
        '图片增长前占位可见','图片增高保持阅读锚点','用户上滚暂停底部跟随','回到底部恢复跟随','页面停用取消会话','页面重新激活不恢复旧会话',
        '自定义节点使用原生文本几何','单图片服务局部覆盖与显式禁用','单代码块高亮覆盖与根继承','自定义节点与局部服务保持全文投影',
        '原生提示NOTE图标与复制','原生提示TIP图标与复制','原生提示IMPORTANT图标与复制','原生提示WARNING图标与复制','原生提示CAUTION图标与复制',
        '五类提示使用不同矢量图形','默认图片file原生PNG解码与尺寸','默认图片loader释放保留调用方租约','默认图片独立租约与重复释放',
        '默认图片file租约释放原生bitmap','默认图片host avares原生PNG解码与尺寸','默认图片avares租约释放原生bitmap',
        '默认图片file编码超限拒绝并关闭输入','默认图片avares编码超限拒绝','默认图片解码后6pixel超过4pixel拒绝',
        '默认图片回环HTTP原生PNG解码与尺寸','默认图片HTTP保留宿主client和已有租约','默认图片HTTP租约释放原生bitmap',
        '默认图片HTTP声明编码超限拒绝','默认图片HTTP分块编码超限拒绝','默认图片回环服务已取消并关闭','剪贴板原文本已恢复'
    )
    $checks = [ordered]@{}
    foreach ($name in $names) { $checks[$name] = $true }
    [pscustomobject]@{ 通过 = $true; 错误 = $null; 检查 = [pscustomobject]$checks }
}
function New-PendingSmokeReport {
    $report = New-SmokeReport
    $report.检查.PSObject.Properties.Remove('真实剪贴板文本复制')
    $report.检查.PSObject.Properties.Remove('剪贴板原文本已恢复')
    $report.检查.剪贴板测试已执行 = $false
    $report.通过 = $false
    return $report
}
function Assert-SmokeFailed($Report, $Process = (New-CompletedProcess), [bool]$ImagePresent = $true) {
    $verdict = Get-MdNativeSmokeVerdict -Report $Report -ProcessResult $Process -ImagePresent $ImagePresent
    Assert-Contract ($verdict.Status -eq 'Failed' -and $verdict.Passed -is [bool] -and -not $verdict.Passed) 'Invalid smoke must be Failed, never Passed/Pending.'
}

# These reports are synthetic contract fixtures, never native rendering evidence.
Test-Contract 'executable-path-windows-compares-ordinal-ignore-case-only' {
    Assert-Contract (Test-MdNativeExecutablePathEqual -Left 'C:\Windows\system32\wsl.exe' -Right 'C:\Windows\System32\wsl.exe' -Platform Windows) 'Windows OS image casing cannot turn the same executable into an identity mismatch.'
    Assert-Contract (-not (Test-MdNativeExecutablePathEqual -Left 'C:\Windows\System32\wsl.exe' -Right 'C:\Other\System32\wsl.exe' -Platform Windows)) 'A genuinely different Windows path must still be rejected.'
}
Test-Contract 'executable-path-linux-retains-ordinal-case-sensitive-identity' {
    Assert-Contract (Test-MdNativeExecutablePathEqual -Left '/usr/bin/pwsh' -Right '/usr/bin/pwsh' -Platform Linux) 'The exact Linux executable must match.'
    Assert-Contract (-not (Test-MdNativeExecutablePathEqual -Left '/usr/bin/pwsh' -Right '/usr/bin/PWSH' -Platform Linux)) 'Linux case-only executable differences must remain distinct.'
}
Test-Contract 'policy-platform-budgets-remain-explicit' {
    Assert-Contract ((Get-MdNativePolicyBudget -Platform Windows) -eq 50) 'Windows must retain the stricter original 50 ms policy.'
    Assert-Contract ((Get-MdNativePolicyBudget -Platform Linux) -eq 51) 'Linux must use only the explicitly authorized 51 ms long UI policy.'
    Assert-ContractThrows { Get-MdNativePolicyBudget -Platform Other } 'Unknown platform must not select a policy.'
}
Test-Contract 'build-state-default-is-requested-but-never-prematurely-performed' {
    $state = New-MdNativeBuildState
    Assert-Contract ($state.SourceBuildRequested -is [bool] -and $state.SourceBuildRequested -and -not $state.SourceBuildPerformed -and -not $state.BuildStarted -and -not $state.BuildSucceeded) 'Requesting a source build must not claim the command was started or succeeded.'
}
Test-Contract 'build-state-no-build-never-accepts-a-fake-source-build' {
    $state = New-MdNativeBuildState -NoBuild $true
    Assert-Contract (-not $state.SourceBuildRequested -and -not $state.SourceBuildPerformed -and -not $state.BuildStarted -and -not $state.BuildSucceeded) 'NoBuild must begin with every source-build state false.'
    Assert-ContractThrows { Set-MdNativeBuildState -State $state -ProcessResult (New-CompletedProcess) } 'NoBuild cannot be promoted to a source-build proof.'
}
Test-Contract 'build-state-command-not-started-preserves-performed-false' {
    $state = New-MdNativeBuildState
    $process = New-CompletedProcess; $process.Started = $false; $process.ExitCode = $null; $process.Outcome = 'Failed'
    Set-MdNativeBuildState -State $state -ProcessResult $process
    Assert-Contract (-not $state.SourceBuildPerformed -and -not $state.BuildStarted -and -not $state.BuildSucceeded) 'A pre-start failure cannot count as a performed build.'
}
Test-Contract 'build-state-started-failure-is-performed-but-unsuccessful' {
    $state = New-MdNativeBuildState
    $process = New-CompletedProcess; $process.ExitCode = 7
    Set-MdNativeBuildState -State $state -ProcessResult $process
    Assert-Contract ($state.SourceBuildPerformed -and $state.BuildStarted -and -not $state.BuildSucceeded) 'An actually started failed command must be distinguished from both no build and a successful build.'
}
Test-Contract 'build-state-success-requires-completed-numeric-zero-exit' {
    $state = New-MdNativeBuildState
    Set-MdNativeBuildState -State $state -ProcessResult (New-CompletedProcess)
    Assert-Contract ($state.SourceBuildRequested -and $state.SourceBuildPerformed -and $state.BuildStarted -and $state.BuildSucceeded) 'A real completed zero exit is a successful performed build.'
}
Test-Contract 'build-state-string-started-and-string-or-boolean-exits-cannot-forge-success' {
    $state = New-MdNativeBuildState
    $process = New-CompletedProcess; $process.Started = 'true'
    Set-MdNativeBuildState -State $state -ProcessResult $process
    Assert-Contract (-not $state.BuildStarted -and -not $state.SourceBuildPerformed -and -not $state.BuildSucceeded) 'String true is not proof that the process started.'
    foreach ($exit in @('0',$false,$null)) {
        $state = New-MdNativeBuildState
        $process = New-CompletedProcess; $process.ExitCode = $exit
        Set-MdNativeBuildState -State $state -ProcessResult $process
        Assert-Contract ($state.BuildStarted -and -not $state.BuildSucceeded) 'A string, boolean or missing exit cannot be a numeric-zero build success.'
    }
}
Test-Contract 'formal-arguments-full-100-with-literal-output-path' {
    $path = Join-Path $OutputDirectory 'path with spaces 中文 $dollar/report.json'
    $actual = @(Get-MdNativeFormalArguments -Round 3 -OutputPath $path)
    $expected = @('--round','3','--samples','100','--stream-sessions','100','--long-stream-sessions','100','--output',$path)
    Assert-Contract ($actual.Count -eq $expected.Count) 'Formal arguments must contain exactly the full measurement arguments.'
    for ($i = 0; $i -lt $expected.Count; $i++) { Assert-Contract ($actual[$i] -ceq $expected[$i]) "Formal argument $i differs." }
    Assert-Contract (-not ($actual -contains '--pilot') -and -not ($actual -contains '--only')) 'A formal run cannot use pilot/only.'
}
Test-Contract 'smoke-current-43-checks-and-real-text-restoration-pass' {
    $verdict = Get-MdNativeSmokeVerdict -Report (New-SmokeReport) -ProcessResult (New-CompletedProcess) -ImagePresent $true
    Assert-Contract ($verdict.Status -eq 'Passed' -and $verdict.Passed -eq $true -and $verdict.ClipboardStatus -eq 'Passed' -and $verdict.CheckCount -eq 43) 'All 43 genuine booleans, completed process and image are required.'
}
Test-Contract 'smoke-empty-clipboard-restoration-also-pass' {
    $report = New-SmokeReport
    $report.检查.PSObject.Properties.Remove('剪贴板原文本已恢复')
    $report.检查 | Add-Member NoteProperty '空剪贴板状态已恢复' $true
    $verdict = Get-MdNativeSmokeVerdict -Report $report -ProcessResult (New-CompletedProcess) -ImagePresent $true
    Assert-Contract ($verdict.Passed -eq $true -and $verdict.CheckCount -eq 43) 'Original empty state restoration is a valid clipboard contract.'
}
Test-Contract 'smoke-unavailable-clipboard-is-pending-never-passed' {
    $process = New-CompletedProcess; $process.ExitCode = 1
    $verdict = Get-MdNativeSmokeVerdict -Report (New-PendingSmokeReport) -ProcessResult $process -ImagePresent $true
    Assert-Contract ($verdict.Status -eq 'Pending' -and $verdict.ClipboardStatus -eq 'Pending' -and $verdict.FunctionalChecksPassed -eq $true -and $verdict.Passed -eq $false -and $verdict.CheckCount -eq 41) 'Unknown clipboard with 40 passing functional checks must remain Pending.'
}
Test-Contract 'smoke-unavailable-clipboard-does-not-hide-functional-failure' {
    $report = New-PendingSmokeReport; $report.检查.模板部件可用 = $false
    $process = New-CompletedProcess; $process.ExitCode = 1
    Assert-SmokeFailed $report $process
}
Test-Contract 'smoke-unavailable-clipboard-cannot-forge-overall-true' {
    $report = New-PendingSmokeReport; $report.通过 = $true
    Assert-SmokeFailed $report
}
Test-Contract 'smoke-copy-false-is-failed-not-pending' {
    $report = New-SmokeReport; $report.检查.真实剪贴板文本复制 = $false; $report.通过 = $false
    Assert-SmokeFailed $report
}
Test-Contract 'smoke-string-boolean-rejected' {
    $report = New-SmokeReport; $report.检查.真实剪贴板文本复制 = 'true'
    Assert-SmokeFailed $report
}
Test-Contract 'smoke-overall-string-boolean-rejected' {
    $report = New-SmokeReport; $report.通过 = 'true'
    Assert-SmokeFailed $report
}
Test-Contract 'smoke-non-null-error-rejected' {
    $report = New-SmokeReport; $report.错误 = 'synthetic error'
    Assert-SmokeFailed $report
}
Test-Contract 'smoke-missing-one-check-rejected' {
    $report = New-SmokeReport; $report.检查.PSObject.Properties.Remove('换肤保留全文和选区')
    Assert-SmokeFailed $report
}
Test-Contract 'smoke-extra-check-rejected-until-contract-reviewed' {
    $report = New-SmokeReport; $report.检查 | Add-Member NoteProperty 'unexpected' $true
    Assert-SmokeFailed $report
}
Test-Contract 'smoke-same-count-renamed-functional-check-rejected' {
    $report = New-SmokeReport
    $report.检查.PSObject.Properties.Remove('模板部件可用')
    $report.检查 | Add-Member NoteProperty 'unrelated replacement check' $true
    Assert-SmokeFailed $report
}
Test-Contract 'smoke-image-is-required' { Assert-SmokeFailed (New-SmokeReport) (New-CompletedProcess) $false }
foreach ($field in @('Started','TimedOut','StreamsComplete','OwnedIdentityVerified','TerminationVerified')) {
    Test-Contract "smoke-supervisor-$field-invalid-rejected" {
        $process = New-CompletedProcess; $process.$field = ($field -eq 'TimedOut')
        Assert-SmokeFailed (New-SmokeReport) $process
    }
}
Test-Contract 'smoke-supervisor-boolean-string-rejected' {
    $process = New-CompletedProcess; $process.StreamsComplete = 'true'
    Assert-SmokeFailed (New-SmokeReport) $process
}
Test-Contract 'smoke-supervisor-nonnull-failure-rejected' {
    $process = New-CompletedProcess; $process.Failure = 'synthetic supervision failure'
    Assert-SmokeFailed (New-SmokeReport) $process
}
Test-Contract 'smoke-nonzero-exit-rejected' {
    $process = New-CompletedProcess; $process.ExitCode = 7
    Assert-SmokeFailed (New-SmokeReport) $process
}

Test-Contract 'four-product-hashes-include-all-files-and-detect-tamper' {
    $folder = Join-Path $OutputDirectory 'hash inputs with spaces'
    New-Item -ItemType Directory -Path $folder | Out-Null
    $names = @('NT.AvaloniaControl.Markdown.dll','NT.AvaloniaControl.Markdown.Themes.dll','NT.AvaloniaControl.Markdown.TextMate.dll','Markdown.NativeBenchmarks.dll')
    foreach ($name in $names) { [IO.File]::WriteAllText((Join-Path $folder $name), "synthetic bytes: $name") }
    $hashes = Get-MdNativeFileHashes -Directory $folder -RelativePaths $names
    Assert-Contract ($hashes.Count -eq 4) 'All four product/host file hashes must be present.'
    Assert-MdNativeFileHashes -Directory $folder -Expected $hashes
    [IO.File]::AppendAllText((Join-Path $folder $names[2]), 'changed')
    Assert-ContractThrows { Assert-MdNativeFileHashes -Directory $folder -Expected $hashes } 'One changed DLL must reject the source binding.'
}
Test-Contract 'four-product-hashes-reject-missing-file' {
    $folder = Join-Path $OutputDirectory 'missing hash input'
    New-Item -ItemType Directory -Path $folder | Out-Null
    Assert-ContractThrows { Get-MdNativeFileHashes -Directory $folder -RelativePaths @('missing.dll') } 'Missing source cannot have an invented hash.'
}
Test-Contract 'hash-path-cannot-escape-frozen-directory' {
    $folder = Join-Path $OutputDirectory 'path boundary'
    New-Item -ItemType Directory -Path $folder | Out-Null
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'outside.dll'), 'outside fixture')
    Assert-ContractThrows { Get-MdNativeFileHashes -Directory $folder -RelativePaths @('../outside.dll') } 'Relative paths must remain inside the frozen source directory.'
}
Test-Contract 'application-resolution-two-path-matches-return-one-canonical-first-file' {
    $root = Join-Path $OutputDirectory 'application path fixtures'
    $first = Join-Path $root 'first path with spaces'
    $second = Join-Path $root 'second path with spaces'
    [IO.Directory]::CreateDirectory($first) | Out-Null
    [IO.Directory]::CreateDirectory($second) | Out-Null
    $name = 'md-native-resolve-' + [guid]::NewGuid().ToString('N')
    $filename = if ([OperatingSystem]::IsWindows()) { $name + '.exe' } else { $name }
    foreach ($folder in @($first,$second)) {
        $file = Join-Path $folder $filename
        [IO.File]::WriteAllText($file, 'Inert command-resolution fixture. Never execute this file.')
        if (-not [OperatingSystem]::IsWindows()) {
            [IO.File]::SetUnixFileMode($file, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute)
        }
    }
    $originalPath = $env:PATH
    try {
        $env:PATH = $first + [IO.Path]::PathSeparator + $second + [IO.Path]::PathSeparator + $originalPath
        $all = @(Get-Command -Name $name -CommandType Application -All -ErrorAction Stop)
        Assert-Contract ($all.Count -eq 2) 'This fixture must exercise two real Application command matches.'
        $resolved = Resolve-MdNativeApplication -Name $name
        $expected = (Get-Item -LiteralPath (Join-Path $first $filename) -Force).FullName
        Assert-Contract ($resolved -is [string] -and @($resolved).Count -eq 1 -and $resolved -ceq $expected) 'Multiple PATH matches must resolve to one canonical first executable path, never a space-joined array.'
    }
    finally { $env:PATH = $originalPath }
    Assert-Contract ($env:PATH -ceq $originalPath) 'The process-local PATH fixture must restore its exact original value.'
}
Test-Contract 'application-resolution-missing-name-rejected' {
    $missing = 'md-native-absent-' + [guid]::NewGuid().ToString('N')
    Assert-ContractThrows { Resolve-MdNativeApplication -Name $missing } 'A missing command cannot synthesize a usable executable path.'
}

$four = [ordered]@{
    'NT.AvaloniaControl.MarkdownSHA256' = 'A' * 64
    'NT.AvaloniaControl.Markdown.ThemesSHA256' = 'B' * 64
    'NT.AvaloniaControl.Markdown.TextMateSHA256' = 'C' * 64
    'Markdown.NativeBenchmarksSHA256' = 'D' * 64
}
function New-FormalReport([int]$Round = 1, [int]$ProcessId = 101) {
    $environment = [ordered]@{}
    foreach ($name in $four.Keys) { $environment[$name] = $four[$name] }
    [pscustomobject]@{
        模式 = 'markdown-native-benchmark'; 正式采样 = $true; 轮次 = $Round; 进程ID = $ProcessId; 全部通过 = $true; 错误 = $null
        环境 = [pscustomobject]$environment
        场景 = @(1..11 | ForEach-Object { [pscustomobject]@{ 名称 = "synthetic-group-$_"; 预热次数 = 10; 样本 = @(1..100 | ForEach-Object { [pscustomobject]@{ 序号 = $_; 是否通过 = $true } }) } })
        流式完成 = @(1..100 | ForEach-Object { [pscustomobject]@{ 会话序号 = $_ } })
        长会话流式完成 = @(1..100 | ForEach-Object { [pscustomobject]@{ 会话序号 = $_ } })
        虚拟化 = @(1..100 | ForEach-Object { [pscustomobject]@{ 序号 = $_ } })
    }
}
function Assert-FormalInvalid($Report, $Process = (New-CompletedProcess), [int]$Round = 1) {
    $verdict = Get-MdNativeRoundVerdict -Report $Report -ProcessResult $Process -ExpectedRound $Round -ExpectedFour $four
    Assert-Contract ($verdict.Valid -is [bool] -and -not $verdict.Valid -and $verdict.Failures.Count -gt 0) 'Invalid formal metadata must be rejected before the independent raw guard.'
}
Test-Contract 'formal-complete-metadata-accepted-without-claiming-timing-validation' {
    $verdict = Get-MdNativeRoundVerdict -Report (New-FormalReport) -ProcessResult (New-CompletedProcess) -ExpectedRound 1 -ExpectedFour $four
    Assert-Contract ($verdict.Valid -eq $true -and $verdict.OriginalProducerPassed -eq $true) 'A full formal metadata fixture must pass the metadata boundary; timing remains the independent guard responsibility.'
}
Test-Contract 'formal-original-producer-false-exit1-retained-for-policy-guard' {
    $report = New-FormalReport; $report.全部通过 = $false
    $process = New-CompletedProcess; $process.ExitCode = 1
    $verdict = Get-MdNativeRoundVerdict -Report $report -ProcessResult $process -ExpectedRound 1 -ExpectedFour $four
    Assert-Contract ($verdict.Valid -eq $true -and $verdict.OriginalProducerPassed -is [bool] -and -not $verdict.OriginalProducerPassed) 'A truthful producer false/exit1 must be preserved for explicit policy evaluation, never rewritten to true.'
}
foreach ($case in @(
    @{ Name = 'pilot'; Change = { param($r) $r.正式采样 = $false } },
    @{ Name = 'formal-string'; Change = { param($r) $r.正式采样 = 'true' } },
    @{ Name = 'wrong-round'; Change = { param($r) $r.轮次 = 2 } },
    @{ Name = 'wrong-process'; Change = { param($r) $r.进程ID = 102 } },
    @{ Name = 'nonnull-error'; Change = { param($r) $r.错误 = 'synthetic' } },
    @{ Name = 'producer-string'; Change = { param($r) $r.全部通过 = 'true' } },
    @{ Name = 'only-ten-groups'; Change = { param($r) $r.场景 = @($r.场景 | Select-Object -First 10) } },
    @{ Name = 'short-warmup'; Change = { param($r) $r.场景[0].预热次数 = 1 } },
    @{ Name = 'short-group'; Change = { param($r) $r.场景[0].样本 = @($r.场景[0].样本 | Select-Object -First 99) } },
    @{ Name = 'missing-short-completion'; Change = { param($r) $r.流式完成 = @($r.流式完成 | Select-Object -First 99) } },
    @{ Name = 'missing-long-completion'; Change = { param($r) $r.长会话流式完成 = @($r.长会话流式完成 | Select-Object -First 99) } },
    @{ Name = 'missing-resource-cycle'; Change = { param($r) $r.虚拟化 = @($r.虚拟化 | Select-Object -First 99) } },
    @{ Name = 'different-core-sha'; Change = { param($r) $r.环境.'NT.AvaloniaControl.MarkdownSHA256' = 'E' * 64 } },
    @{ Name = 'different-theme-sha'; Change = { param($r) $r.环境.'NT.AvaloniaControl.Markdown.ThemesSHA256' = 'E' * 64 } },
    @{ Name = 'different-textmate-sha'; Change = { param($r) $r.环境.'NT.AvaloniaControl.Markdown.TextMateSHA256' = 'E' * 64 } },
    @{ Name = 'different-host-sha'; Change = { param($r) $r.环境.'Markdown.NativeBenchmarksSHA256' = 'E' * 64 } }
)) {
    Test-Contract "formal-$($case.Name)-rejected" {
        $report = New-FormalReport
        & $case.Change $report
        Assert-FormalInvalid $report
    }
}
Test-Contract 'formal-false-producer-with-exit0-rejected' {
    $report = New-FormalReport; $report.全部通过 = $false
    Assert-FormalInvalid $report
}
Test-Contract 'formal-true-producer-with-exit1-rejected' {
    $process = New-CompletedProcess; $process.ExitCode = 1
    Assert-FormalInvalid (New-FormalReport) $process
}
foreach ($field in @('Started','TimedOut','StreamsComplete','OwnedIdentityVerified','TerminationVerified')) {
    Test-Contract "formal-supervisor-$field-invalid-rejected" {
        $process = New-CompletedProcess; $process.$field = ($field -eq 'TimedOut')
        Assert-FormalInvalid (New-FormalReport) $process
    }
}
Test-Contract 'five-raw-reports-remain-byte-identical-through-metadata-checks' {
    $folder = Join-Path $OutputDirectory 'five original raw fixtures'
    New-Item -ItemType Directory -Path $folder | Out-Null
    $paths = @(1..5 | ForEach-Object {
        $path = "report-$_.json"
        New-FormalReport -Round $_ -ProcessId (100 + $_) | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $folder $path) -Encoding utf8
        $path
    })
    $before = Get-MdNativeFileHashes -Directory $folder -RelativePaths $paths
    $pids = [Collections.Generic.HashSet[int]]::new()
    for ($i = 0; $i -lt 5; $i++) {
        $report = Get-Content -LiteralPath (Join-Path $folder $paths[$i]) -Raw | ConvertFrom-Json
        $process = New-CompletedProcess; $process.ProcessId = 101 + $i
        $verdict = Get-MdNativeRoundVerdict -Report $report -ProcessResult $process -ExpectedRound ($i + 1) -ExpectedFour $four
        Assert-Contract ($verdict.Valid -and $pids.Add($report.进程ID)) 'Five fixtures must have distinct authenticated process identifiers.'
    }
    Assert-MdNativeFileHashes -Directory $folder -Expected $before
    Assert-Contract ($before.Count -eq 5) 'All five original raw hashes must be retained.'
}

function New-SourceFixture([string]$Name) {
    $root = Join-Path $OutputDirectory ('source-fixtures/' + $Name)
    [IO.Directory]::CreateDirectory($root) | Out-Null
    return $root
}
function Write-SourceFixtureFile([string]$Root, [string]$RelativePath, [string]$Content = 'synthetic source') {
    $path = Join-Path $Root $RelativePath
    [IO.Directory]::CreateDirectory((Split-Path $path -Parent)) | Out-Null
    [IO.File]::WriteAllText($path, $Content, [Text.UTF8Encoding]::new($false))
}
function Assert-SourceRows([string]$Root, $Rows, [string[]]$ExpectedPaths) {
    Assert-Contract (@($Rows).Count -eq $ExpectedPaths.Count) 'Source enumeration must include exactly the allowed fixture files.'
    $actualPaths = @($Rows | ForEach-Object Path)
    foreach ($path in $ExpectedPaths) { Assert-Contract ($actualPaths -ccontains $path) "Source enumeration omitted $path." }
    $sorted = @($actualPaths | Sort-Object)
    for ($i = 0; $i -lt $actualPaths.Count; $i++) {
        Assert-Contract ($actualPaths[$i] -ceq $sorted[$i]) 'Source manifest paths must have stable sorted order.'
    }
    foreach ($row in $Rows) {
        Assert-Contract (-not [IO.Path]::IsPathRooted($row.Path) -and -not $row.Path.Contains('\')) 'Source manifest uses relative slash paths.'
        $file = Get-Item -LiteralPath (Join-Path $Root $row.Path) -Force
        Assert-Contract ($row.Length -eq $file.Length -and $row.SHA256 -ceq (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) 'Source metadata must hash the actual file bytes.'
    }
}
Test-Contract 'source-enumeration-empty-is-an-empty-manifest' {
    $root = New-SourceFixture 'empty'
    $rows = @(Get-MdNativeSourceFiles -SourceRoot $root)
    Assert-Contract ($rows.Count -eq 0) 'An empty source root must not emit a null placeholder or wrapper object.'
}
Test-Contract 'source-enumeration-single-file-is-not-unwrapped-incorrectly' {
    $root = New-SourceFixture 'one'
    Write-SourceFixtureFile $root 'README.md' 'UTF8 fixture 中文 😀'
    $rows = @(Get-MdNativeSourceFiles -SourceRoot $root)
    Assert-SourceRows $root $rows @('README.md')
}
Test-Contract 'source-enumeration-seven-root-configurations-all-hashed' {
    $root = New-SourceFixture 'root-configurations'
    $names = @('Directory.Build.props','Directory.Packages.props','NuGet.Config','global.json','NT.AvaloniaControl.slnx','.editorconfig','README.md')
    foreach ($name in $names) { Write-SourceFixtureFile $root $name "root fixture: $name" }
    $rows = @(Get-MdNativeSourceFiles -SourceRoot $root)
    Assert-SourceRows $root $rows $names
}
Test-Contract 'source-enumeration-hidden-root-github-and-nested-files-are-not-lost' {
    $root = New-SourceFixture 'hidden-files'
    $names = @('.editorconfig','.github/workflows/check.yml','.github/.hidden.yml','src/.hidden/Hidden.cs','src/Visible.cs')
    foreach ($name in $names) { Write-SourceFixtureFile $root $name "hidden fixture: $name" }
    $attributes = [Collections.Generic.List[object]]::new()
    try {
        if ([OperatingSystem]::IsWindows()) {
            foreach ($relative in @('.editorconfig','.github','.github/.hidden.yml','src/.hidden','src/.hidden/Hidden.cs')) {
                $path = Join-Path $root $relative
                $before = [IO.File]::GetAttributes($path)
                $attributes.Add([pscustomobject]@{ Path = $path; Before = $before })
                [IO.File]::SetAttributes($path, $before -bor [IO.FileAttributes]::Hidden)
            }
        }
        $rows = @(Get-MdNativeSourceFiles -SourceRoot $root)
        Assert-SourceRows $root $rows $names
    }
    finally {
        foreach ($entry in $attributes) { [IO.File]::SetAttributes($entry.Path, $entry.Before) }
    }
}
Test-Contract 'source-enumeration-each-top-level-source-tree-is-included' {
    $root = New-SourceFixture 'source-trees'
    $names = @('src/a.cs','samples/b.cs','tests/c.cs','scripts/d.ps1','benchmarks/e.cs','docs/f.md','.github/g.yml')
    foreach ($name in $names) { Write-SourceFixtureFile $root $name }
    Assert-SourceRows $root @(Get-MdNativeSourceFiles -SourceRoot $root) $names
}
Test-Contract 'source-enumeration-generated-directories-excluded-before-descent' {
    $root = New-SourceFixture 'generated-exclusions'
    $keep = @('src/keep.cs','src/deep/.keep.cs','.github/workflows/keep.yml')
    foreach ($name in $keep) { Write-SourceFixtureFile $root $name }
    foreach ($directory in @('bin','obj','artifacts','.git','.packages','node_modules','.nuget')) {
        foreach ($parent in @('src','src/deep','.github')) { Write-SourceFixtureFile $root "$parent/$directory/must-not-be-hashed.cs" }
    }
    Assert-SourceRows $root @(Get-MdNativeSourceFiles -SourceRoot $root) $keep
}

$childScript = Join-Path $OutputDirectory 'owned tiny process.ps1'
@'
param([string]$Mode, [string]$Value)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
switch ($Mode) {
    'echo' { [Console]::Out.Write($Value); [Console]::Error.Write('stderr marker'); exit 7 }
    'fast' { exit 0 }
    'delayed-fast' { Start-Sleep -Milliseconds 400; exit 0 }
    'brief-live' { Start-Sleep -Milliseconds 1500; exit 0 }
    'flood' { [Console]::Out.Write(('中文😀' * 8192)); [Console]::Error.Write(('error' * 8192)); exit 0 }
    'sleep' { Start-Sleep -Seconds 30; exit 0 }
    default { exit 91 }
}
'@ | Set-Content -LiteralPath $childScript -Encoding utf8
function Invoke-TinyProcess([string]$Name, [string]$Mode, [int]$TimeoutSeconds = 10, [int]$MaximumLogBytes = 4096, [string]$Value = '') {
    Invoke-MdNativeProcess -Executable $shell -ArgumentList @('-NoProfile','-File',$childScript,'-Mode',$Mode,'-Value',$Value) -WorkingDirectory $OutputDirectory -OutputDirectory (Join-Path $OutputDirectory $Name) -TimeoutSeconds $TimeoutSeconds -MaximumLogBytes $MaximumLogBytes
}
Test-Contract 'owned-process-existing-evidence-is-not-overwritten' {
    $folder = Join-Path $OutputDirectory 'preserved process evidence'
    New-Item -ItemType Directory -Path $folder | Out-Null
    $marker = Join-Path $folder 'original.json'
    [IO.File]::WriteAllText($marker, 'preserve this original evidence')
    $before = (Get-FileHash -LiteralPath $marker -Algorithm SHA256).Hash
    Assert-ContractThrows { Invoke-MdNativeProcess -Executable $shell -ArgumentList @('-NoProfile','-File',$childScript,'-Mode','fast') -WorkingDirectory $OutputDirectory -OutputDirectory $folder } 'Existing evidence directory must reject before starting another process.'
    Assert-Contract ((Get-FileHash -LiteralPath $marker -Algorithm SHA256).Hash -ceq $before) 'An existing report cannot be overwritten.'
}
Test-Contract 'owned-process-zero-timeout-rejected-before-start' {
    $folder = Join-Path $OutputDirectory 'invalid timeout process'
    Assert-ContractThrows { Invoke-MdNativeProcess -Executable $shell -ArgumentList @('-NoProfile','-File',$childScript,'-Mode','fast') -WorkingDirectory $OutputDirectory -OutputDirectory $folder -TimeoutSeconds 0 } 'Timeout zero cannot create an unbounded native process.'
    Assert-Contract (-not (Test-Path -LiteralPath $folder)) 'Invalid supervisor parameters must not claim a started process.'
}
Test-Contract 'owned-process-missing-executable-rejected-before-start' {
    $folder = Join-Path $OutputDirectory 'missing executable process'
    Assert-ContractThrows { Invoke-MdNativeProcess -Executable (Join-Path $OutputDirectory 'missing.exe') -WorkingDirectory $OutputDirectory -OutputDirectory $folder } 'Missing executable must not synthesize successful supervision.'
    Assert-Contract (-not (Test-Path -LiteralPath $folder)) 'A missing executable cannot claim started process evidence.'
}
function Assert-ObservedIdentityHonest($Actual) {
    if (-not $Actual.ModuleObserved) { Assert-Contract ($null -eq $Actual.ObservedExecutable) 'An unobserved module path must remain null.' }
    if (-not $Actual.StartTimeObserved) { Assert-Contract ($null -eq $Actual.StartTimeUtcTicks) 'An unobserved process start time must remain null.' }
    if ($Actual.OwnedIdentityVerified) { Assert-Contract ($Actual.ModuleObserved -and $Actual.StartTimeObserved) 'Full module/start identity cannot be inferred from a retained Process alone.' }
    if ($Actual.IdentityObservation -ceq 'ExitedBeforeCompleteObservation') {
        Assert-Contract (-not $Actual.OwnedIdentityVerified -and $Actual.RetainedProcessOwnershipVerified -and $Actual.ExitObserved -and -not $Actual.IdentityMismatch) 'Retained early exit must be distinguished from observed module/start identity.'
    }
}
Test-Contract 'owned-process-fast-exit-preserves-real-zero-code' {
    $actual = Invoke-TinyProcess 'process fast' 'fast'
    Assert-Contract ((Test-MdNativeCompletedProcess $actual) -and $actual.ExitCode -eq 0) 'Fast exit must retain the started Process handle and real exit code, without inventing optional metadata.'
    Assert-ObservedIdentityHonest $actual
    $method = if ([OperatingSystem]::IsWindows()) { 'Win32.QueryFullProcessImageName' } else { 'Process.MainModule' }
    Assert-Contract ($actual.ImagePathObservationMethod -ceq $method) 'The recorded image observation method must match the real platform implementation.'
    if ($actual.ModuleObserved) { Assert-Contract (Test-MdNativeExecutablePathEqual $actual.ObservedExecutable (Get-MdNativeCanonicalFilePath $shell)) 'Observed process image must be the actual PowerShell executable, not a transient loader module.' }
    Assert-Contract (Test-Path -LiteralPath (Join-Path $OutputDirectory 'process fast/supervisor-result.json')) 'Fast exit must always write supervisor evidence.'
}
Test-Contract 'owned-process-module-first-two-observations-empty-then-visible' {
    $script:mdNativeDelayedModuleReads = 0
    $script:mdNativeOriginalImageReader = ${function:Get-MdNativeStartedModulePath}
    function local:Get-MdNativeStartedModulePath([Diagnostics.Process]$Process) {
        $script:mdNativeDelayedModuleReads++
        if ($script:mdNativeDelayedModuleReads -le 2) { return $null }
        return & $script:mdNativeOriginalImageReader $Process
    }
    $actual = Invoke-TinyProcess 'process delayed module observation' 'delayed-fast'
    Assert-Contract ($script:mdNativeDelayedModuleReads -ge 3 -and (Test-MdNativeCompletedProcess $actual) -and $actual.ExitCode -eq 0 -and $actual.OwnedIdentityVerified -and $actual.ModuleObserved) 'One retained child must survive two empty metadata observations and later expose its real module.'
    Assert-ObservedIdentityHonest $actual
}
Test-Contract 'owned-process-exits-before-both-identity-properties-visible' {
    function local:Get-MdNativeStartedModulePath([Diagnostics.Process]$Process) { return $null }
    function local:Get-MdNativeStartedStartTime([Diagnostics.Process]$Process) { return $null }
    $actual = Invoke-TinyProcess 'process unobserved identity early exit' 'echo' -Value 'retained child output'
    Assert-Contract ((Test-MdNativeCompletedProcess $actual) -and $actual.ExitCode -eq 7 -and $actual.ExitObserved -and $actual.RetainedProcessOwnershipVerified) 'The retained child real exit7 and complete streams must survive unobservable optional metadata.'
    Assert-Contract (-not $actual.ModuleObserved -and -not $actual.StartTimeObserved -and -not $actual.OwnedIdentityVerified -and $actual.IdentityObservation -ceq 'ExitedBeforeCompleteObservation') 'Missing identity observations must remain explicit, never fabricated.'
    Assert-Contract ([IO.File]::ReadAllText($actual.Stdout.Path) -ceq 'retained child output' -and [IO.File]::ReadAllText($actual.Stderr.Path) -ceq 'stderr marker') 'Both streams must be drained before an early-exit result is complete.'
    Assert-ObservedIdentityHonest $actual
}
Test-Contract 'owned-process-unobserved-identity-while-alive-cannot-pass' {
    function local:Get-MdNativeStartedModulePath([Diagnostics.Process]$Process) { return $null }
    function local:Get-MdNativeStartedStartTime([Diagnostics.Process]$Process) { return $null }
    $actual = Invoke-TinyProcess 'process live but identity unavailable' 'brief-live'
    Assert-Contract (-not (Test-MdNativeCompletedProcess $actual) -and $actual.Outcome -ceq 'Failed' -and $actual.IdentityObservation -ceq 'StartupObservationTimedOut' -and -not $actual.OwnedIdentityVerified -and $null -ne $actual.Failure) 'A live process with unobservable identity cannot use the safe already-exited ownership path.'
    Assert-ObservedIdentityHonest $actual
}
foreach ($exitCode in @(0,7)) {
    foreach ($iteration in @(1,2)) {
        Test-Contract "owned-process-immediate-exit$exitCode-fixed-sample$iteration" {
            $mode = if ($exitCode -eq 0) { 'fast' } else { 'echo' }
            $actual = Invoke-TinyProcess "process immediate $exitCode sample $iteration" $mode
            Assert-Contract ((Test-MdNativeCompletedProcess $actual) -and $actual.ExitCode -eq $exitCode -and $actual.ExitObserved -and $actual.StreamsComplete) 'Each of the fixed immediate exit0/exit7 samples must independently retain its real exit and streams.'
            Assert-ObservedIdentityHonest $actual
        }
    }
}
Test-Contract 'owned-process-nonzero-and-literal-unicode-argument-preserved' {
    $value = 'path with spaces 中文 $literal $(not-a-command) "quoted"'
    $actual = Invoke-TinyProcess 'process argument' 'echo' -Value $value
    Assert-Contract ($actual.ExitCode -eq 7 -and -not $actual.TimedOut -and $actual.StreamsComplete) 'Nonzero exit must be observed without an exit race or timeout.'
    Assert-Contract ([IO.File]::ReadAllText($actual.Stdout.Path) -ceq $value) 'Arguments must remain one literal argv entry.'
    Assert-Contract ([IO.File]::ReadAllText($actual.Stderr.Path) -ceq 'stderr marker') 'Both redirected streams must be drained.'
}
Test-Contract 'owned-process-both-logs-capped-with-full-drain' {
    $actual = Invoke-TinyProcess 'process logs' 'flood' -MaximumLogBytes 1024
    Assert-Contract ($actual.ExitCode -eq 0 -and $actual.StreamsComplete) 'Log caps must not block either stream or change exit code.'
    foreach ($log in @($actual.Stdout,$actual.Stderr)) {
        Assert-Contract ($log.Truncated -eq $true -and $log.TotalBytes -gt 1024 -and $log.StoredBytes -le 1024 -and -not $log.Incomplete) 'Log length must be capped, truncation explicit, total bytes fully observed.'
        Assert-Contract ((Get-Item -LiteralPath $log.Path).Length -le 1024) 'Stored log must obey the byte cap, including multibyte UTF8.'
    }
}
Test-Contract 'owned-process-timeout-terminates-and-records-only-owned-process' {
    $actual = Invoke-TinyProcess 'process timeout' 'sleep' -TimeoutSeconds 1
    Assert-Contract ($actual.Started -and $actual.TimedOut -and $actual.OwnedIdentityVerified -and $actual.TerminationVerified -and $actual.ProcessId -ne $PID) 'A timeout must authenticate and terminate only the launched child.'
    Assert-Contract ($actual.Outcome -ne 'Completed') 'A timed out child cannot be Completed.'
    Assert-Contract (Test-Path -LiteralPath (Join-Path $OutputDirectory 'process timeout/supervisor-result.json')) 'Timeout must retain supervisor evidence.'
}
Test-Contract 'owned-process-platform-path-case-policy-covers-start-and-cleanup' {
    if ([OperatingSystem]::IsWindows()) {
        $script:mdNativeCaseOriginalImageReader = ${function:Get-MdNativeStartedModulePath}
        function local:Get-MdNativeStartedModulePath([Diagnostics.Process]$Process) {
            # Return a case alias of the real owned child's queried image. This is
            # a valid Windows file, not a substituted executable or fabricated PID.
            return (& $script:mdNativeCaseOriginalImageReader $Process).ToUpperInvariant()
        }
        $actual = Invoke-MdNativeProcess -Executable $shell.ToLowerInvariant() -ArgumentList @('-NoProfile','-File',$childScript,'-Mode','sleep') -WorkingDirectory $OutputDirectory -OutputDirectory (Join-Path $OutputDirectory 'process Windows case alias timeout') -TimeoutSeconds 1
        $script:windowsPathCaseAliasProcessExecuted = $true
        Assert-Contract ($actual.Started -and $actual.ModuleObserved -and $actual.OwnedIdentityVerified -and -not $actual.IdentityMismatch -and $actual.TimedOut -and $actual.TerminationVerified -and $actual.StreamsComplete -and $null -eq $actual.Failure) 'Case aliases must pass initial image identity and authenticate cleanup of the same timed-out owned child.'
        Assert-Contract ($actual.Executable -cne $actual.ObservedExecutable -and (Test-MdNativeExecutablePathEqual $actual.Executable $actual.ObservedExecutable)) 'The real process fixture must exercise differing recorded Windows path casing.'
    }
    else {
        Assert-Contract (-not (Test-MdNativeExecutablePathEqual '/usr/bin/pwsh' '/usr/bin/PWSH')) 'The production default on Linux must reject the synthetic case-only path counterexample.'
    }
}

$entrySource = Join-Path $testRepo 'scripts/Test-MarkdownNative.ps1'
$entryDirectory = Join-Path $OutputDirectory 'entry-under-test'
[IO.Directory]::CreateDirectory($entryDirectory) | Out-Null
$entryCopy = Join-Path $entryDirectory 'Test-MarkdownNative.ps1'
Copy-Item -LiteralPath $entrySource -Destination $entryCopy
Copy-Item -LiteralPath $helper -Destination (Join-Path $entryDirectory 'Test-MarkdownNative.Helpers.ps1')
$entryHash = (Get-FileHash -LiteralPath $entryCopy -Algorithm SHA256).Hash
foreach ($noBuild in @($false,$true)) {
    Test-Contract "entry-missing-dotnet-no-build-$noBuild-never-claims-performed-build" {
        $name = if ($noBuild) { 'no-build' } else { 'build-requested' }
        $sourceRoot = New-SourceFixture ('entry-empty-' + $name)
        $entryOutput = Join-Path $OutputDirectory ('entry-result-' + $name)
        $arguments = @('-NoProfile','-File',$entryCopy,'-Mode','Smoke','-SourceRoot',$sourceRoot,'-DotnetPath',(Join-Path $OutputDirectory 'missing-dotnet-executable'),'-OutputDirectory',$entryOutput)
        if ($noBuild) { $arguments += '-NoBuild' }
        $actual = Invoke-MdNativeProcess -Executable $shell -ArgumentList $arguments -WorkingDirectory $OutputDirectory -OutputDirectory (Join-Path $OutputDirectory ('entry-supervision-' + $name)) -TimeoutSeconds 15
        Assert-Contract ((Test-MdNativeCompletedProcess $actual) -and $actual.ExitCode -eq 1) 'The real entry must exit1 before starting dotnet/native/build.'
        $result = Get-Content -LiteralPath (Join-Path $entryOutput 'result.json') -Raw | ConvertFrom-Json
        Assert-Contract ($result.Status -ceq 'Failed' -and $result.Passed -is [bool] -and -not $result.Passed) 'A missing dotnet executable must retain an actual failed entry verdict.'
        Assert-Contract ($result.SourceBuildRequested -is [bool] -and $result.SourceBuildRequested -eq (-not $noBuild)) 'Build requested metadata must reflect only the real requested mode.'
        foreach ($field in @('SourceBuildPerformed','BuildStarted','BuildSucceeded')) { Assert-Contract ($result.$field -is [bool] -and -not $result.$field) 'Early entry failure cannot claim any performed or successful source build.' }
        Assert-Contract (@($result.Processes).Count -eq 0 -and -not (Test-Path -LiteralPath (Join-Path $entryOutput 'source-snapshot.json'))) 'No dotnet child or source-bound binary snapshot may be claimed before resolving dotnet.'
    }
}
Assert-Contract ((Get-FileHash -LiteralPath $entryCopy -Algorithm SHA256).Hash -ceq $entryHash) 'The real entry copy changed during contracts.'

$helperAfter = (Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash
Assert-Contract ($helperAfter -ceq $helperHash) 'The helper under test changed during contract execution.'
$testSourceAfter = (Get-FileHash -LiteralPath $testSourcePath -Algorithm SHA256).Hash
Assert-Contract ($testSourceAfter -ceq $testSourceHash) 'The contract test source changed during execution.'
$passed = @($results | Where-Object { -not $_.Passed }).Count -eq 0
[pscustomobject]@{
    AllPassed = $passed
    TestOnly = $true
    NativeExecutionPerformed = $false
    DotnetBuildPerformed = $false
    WindowsPathCaseAliasProcessExecuted = $windowsPathCaseAliasProcessExecuted
    ReparsePointFixtureExecuted = $false
    ReparsePointFixtureBoundary = 'Junction creation is denied by the restricted workspace; dotfiles and generated-directory traversal are tested, reparse-point creation is not counted as a passing case.'
    Description = 'Synthetic reports, inert hash fixtures and owned tiny PowerShell processes validate native tooling contracts only; these are not native rendering/performance evidence.'
    CaseCount = $results.Count
    HelperSource = $helperSource
    HelperSHA256 = $helperHash
    HelperCopyUnchanged = ($helperAfter -ceq $helperHash)
    EntrySource = $entrySource
    EntrySHA256 = $entryHash
    TestSource = $testSourcePath
    TestSourceSHA256 = $testSourceHash
    TestSourceUnchanged = ($testSourceAfter -ceq $testSourceHash)
    PowerShellVersion = $PSVersionTable.PSVersion.ToString()
    OperatingSystem = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    Results = @($results | ForEach-Object { $_ })
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'results.json') -Encoding utf8
if (-not $passed) {
    $failures = @($results | Where-Object { -not $_.Passed } | ForEach-Object { "$($_.Name): $($_.Error)" }) -join "`n"
    throw "Markdown native helper contracts failed:`n$failures"
}
Write-Output "PASS: Markdown native helper $($results.Count) contracts. Evidence: $OutputDirectory"
