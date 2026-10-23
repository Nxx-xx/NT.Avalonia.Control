param(
    [Parameter(Mandatory)][string[]]$ReportPaths,
    [string]$OutputPath,
    [ValidateSet(50,51)][int]$LongActualUiP95BudgetMilliseconds = 50,
    [ValidateSet(50)][int]$ProducerLongActualUiP95BudgetMilliseconds = 50
)
$ErrorActionPreference = 'Stop'
$performanceRepo = Split-Path $PSScriptRoot -Parent
if (-not $OutputPath) { $OutputPath = Join-Path $performanceRepo 'artifacts/benchmarks/markdown/native-guard.json' }
function Required($Object, [string]$Name) {
    if ($Object -isnot [pscustomobject] -or $null -eq $Object.PSObject.Properties[$Name]) { throw "Missing field $Name." }
    return ,$Object.PSObject.Properties[$Name].Value
}
function Number($Object, [string]$Name, [switch]$Integer, [switch]$Signed) {
    $value = Required $Object $Name
    if ($null -eq $value -or $value -is [string] -or $value -is [bool] -or $value -isnot [ValueType]) { throw "$Name must be JSON number." }
    $numeric = [double]$value
    if ([double]::IsNaN($numeric) -or [double]::IsInfinity($numeric) -or (-not $Signed -and $numeric -lt 0) -or ($Integer -and [Math]::Truncate($numeric) -ne $numeric)) { throw "$Name must be finite nonnegative number (signed only for heap delta)." }
    return $numeric
}
function True($Object, [string]$Name) { $value=Required $Object $Name; if ($value -isnot [bool] -or -not $value) { throw "$Name must be true JSON boolean." } }
function JsonBoolean($Object, [string]$Name) { $value=Required $Object $Name; if ($value -isnot [bool]) { throw "$Name must be JSON boolean." }; return $value }
function Text($Object, [string]$Name) { $value=Required $Object $Name; if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) { throw "$Name must be nonempty string." }; return $value }
function Array($Object, [string]$Name) { $value=Required $Object $Name; if ($value -isnot [array]) { throw "$Name must be JSON array." }; return ,$value }
function Pct([double[]]$Values, [double]$Percentile) { $ordered=@($Values | Sort-Object); return $ordered[[Math]::Max(0, [int][Math]::Ceiling($ordered.Count*$Percentile)-1)] }
function Same([double]$Actual,[double]$Expected,[string]$Name) { if ([Math]::Abs($Actual-$Expected) -gt [Math]::Max(1e-8,[Math]::Abs($Expected)*1e-9)) { throw "$Name differs from raw samples." } }
# Stopwatch.GetElapsedTime on .NET 10.0.12 converts first to TimeSpan ticks (100 ns).
# Match the producer's truncation exactly; floating-point tolerance and budgets stay unchanged.
# https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Stopwatch.cs
function StopwatchElapsedMilliseconds([long]$StartTicks, [long]$EndTicks, [double]$Frequency) {
    if ($EndTicks -lt $StartTicks -or $Frequency -le 0) { throw 'Invalid stopwatch interval or frequency.' }
    $tickFrequency = [double][TimeSpan]::TicksPerSecond / $Frequency
    $timeSpanTicks = [Math]::Truncate(($EndTicks - $StartTicks) * $tickFrequency)
    return [TimeSpan]::FromTicks([long]$timeSpanTicks).TotalMilliseconds
}
function ValidateReceipts($Report,[string]$ReceiptField,[string]$BatchField,[string]$CompletionField,[int]$FinalBytes,[string]$CommitField) {
    $receipts=Array $Report $ReceiptField; $batches=Array $Report $BatchField; $completions=Array $Report $CompletionField
    $bySession=@{}; foreach($group in @($receipts|Group-Object 会话序号)){$bySession[$group.Name]=@($group.Group)}
    $batchSessions=@{}; foreach($group in @($batches|Group-Object 会话序号)){$batchSessions[$group.Name]=@($group.Group)}
    $commitSessions=@{}; if($CommitField){foreach($group in @((Array $Report $CommitField)|Group-Object 会话序号)){$commitSessions[$group.Name]=@($group.Group)}}
    if($bySession.Count -ne $completions.Count -or $batchSessions.Count -ne $completions.Count){throw 'Receipt/session count differs.'}
    $completedSessions=[Collections.Generic.HashSet[int]]::new()
    foreach($completion in $completions){
        $session=[int](Number $completion '会话序号' -Integer)
        if(-not $completedSessions.Add($session)){throw 'Duplicate receipt completion session.'}
        $key=[string]$session; $sessionInput=@($bySession[$key]); $observations=@($batchSessions[$key])
        if($sessionInput.Count -eq 0 -or $observations.Count -eq 0){throw 'Receipt session has missing input or frames.'}
        $versions=@{}; $previousEnd=0; $previousBytes=0; $previousTicks=0; $previousVersion=0
        $baseVersion=Number $completion 'BaseSourceVersion' -Integer
        for($index=0;$index -lt $sessionInput.Count;$index++){
            $receipt=$sessionInput[$index]
            if((Number $receipt '序号' -Integer) -ne $index -or (Number $receipt '会话序号' -Integer) -ne $session){throw 'Receipt sequence differs.'}
            $version=Number $receipt 'SourceVersion' -Integer; $end=Number $receipt '累计UTF16结束位置' -Integer; $bytes=Number $receipt '累计UTF8结束位置' -Integer; $ticks=Number $receipt '接收Ticks' -Integer
            if($version -le 0 -or ($index -eq 0 -and $version -ne $baseVersion+1) -or ($index -gt 0 -and $version -ne $previousVersion+1) -or $ticks -lt $previousTicks -or $end -le $previousEnd -or $bytes -le $previousBytes){throw 'Receipt version, timestamp or source interval differs.'}
            Same (Number $receipt 'ChunkUTF16长度' -Integer) ($end-$previousEnd) 'receipt UTF16 delta'
            Same (Number $receipt 'ChunkUTF8字节数' -Integer) ($bytes-$previousBytes) 'receipt UTF8 delta'
            $versions[[string]$version]=$index
            $previousEnd=$end; $previousBytes=$bytes; $previousTicks=$ticks; $previousVersion=$version
        }
        if($previousBytes -ne $FinalBytes -or $previousEnd -ne (Number $completion '源长度' -Integer) -or (Number $completion 'SourceVersion' -Integer) -ne $previousVersion -or (Number $completion '接收结束Ticks' -Integer) -lt $previousTicks){throw 'Receipt final source/version/receive-end differs.'}
        if($CommitField){
            $sessionCommits=@($commitSessions[$key]); if($sessionCommits.Count -eq 0){throw 'Receipt session has no actual commits.'}
            for($commitIndex=0;$commitIndex -lt $sessionCommits.Count;$commitIndex++){
                $commit=$sessionCommits[$commitIndex];$commitVersion=[string](Number $commit 'SourceVersion' -Integer)
                if((Number $commit '序号' -Integer) -ne $commitIndex -or -not $versions.ContainsKey($commitVersion)){throw 'Actual commit sequence/version has no receipt.'}
                $commitReceipt=$sessionInput[$versions[$commitVersion]]
                Same (Number $commit 'SourceLength' -Integer) $commitReceipt.累计UTF16结束位置 'actual commit receipt length'
                Same (Number $commit '源UTF8字节数' -Integer) $commitReceipt.累计UTF8结束位置 'actual commit receipt bytes'
            }
        }
        $lastCovered=-1
        for($ordinal=0;$ordinal -lt $observations.Count;$ordinal++){
            $batch=$observations[$ordinal]
            if((Number $batch '序号' -Integer) -ne $ordinal){throw 'Covered batch sequence differs.'}
            $versionKey=[string](Number $batch 'SourceVersion' -Integer)
            if(-not $versions.ContainsKey($versionKey)){throw 'Frame source version has no received chunk.'}
            $targetIndex=$versions[$versionKey]; $target=$sessionInput[$targetIndex]
            if($targetIndex -le $lastCovered){throw 'Frame covers an old chunk again.'}
            Same (Number $batch 'SourceLength' -Integer) $target.累计UTF16结束位置 'receipt frame source length'
            Same (Number $batch '累计已绘制UTF16' -Integer) $target.累计UTF16结束位置 'receipt frame UTF16 end'
            Same (Number $batch '累计已绘制UTF8' -Integer) $target.累计UTF8结束位置 'receipt frame UTF8 end'
            Same (Number $batch '覆盖Chunk数' -Integer) ($targetIndex-$lastCovered) 'receipt covered count'
            $priorBytes=if($lastCovered -lt 0){0}else{$sessionInput[$lastCovered].累计UTF8结束位置}
            Same (Number $batch '覆盖ChunkUTF8字节数' -Integer) ($target.累计UTF8结束位置-$priorBytes) 'receipt covered UTF8 bytes'
            if($CommitField){
                $matches=@($sessionCommits|Where-Object {([string]$_.SourceVersion -eq $versionKey) -and $_.SourceLength -eq $batch.SourceLength -and $_.Parse毫秒 -eq $batch.Parse毫秒 -and $_.UI应用毫秒 -eq $batch.UI应用毫秒 -and $_.布局毫秒 -eq $batch.布局毫秒})
                if($matches.Count -eq 0){throw 'Rendered metric has no matching actual commit.'}
            }
            $covered=@($sessionInput[($lastCovered+1)..$targetIndex])
            $firstTicks=($covered|Measure-Object 接收Ticks -Minimum).Minimum
            $lastTicks=($covered|Measure-Object 接收Ticks -Maximum).Maximum
            Same (Number $batch '最早Chunk接收Ticks' -Integer) $firstTicks 'earliest uncovered receipt'
            if($lastTicks -gt (Number $batch '绘制完成Ticks' -Integer)){throw 'Frame covers a future receipt.'}
            $lastCovered=$targetIndex
        }
        if($lastCovered -ne $sessionInput.Count-1){throw 'Final frame omitted a received chunk.'}
    }
}
$expectedGroups = [ordered]@{
    '100KiB混合首屏-纯文本' = @(300,$null)
    '1024KiB混合首屏-纯文本' = @(1500,50)
    '100KiB混合首屏-TextMate' = @(300,$null)
    '1024KiB混合首屏-TextMate' = @(1500,50)
    '100KiB单代码长行' = @(300,$null,150,150,150,134217728)
    '100KiB单段落' = @(300,$null,150,150,150,134217728)
    '30x30宽表' = @(1500,$null,500,150,150,134217728)
    '100KiB-20chunk每秒最早未显示至原生帧' = @(150,$null)
    '100KiB流式Complete至最终提交' = @(500,$null)
    '1MiB长会话-20chunk每秒最早未显示至原生帧' = @($null,50)
    '1MiB长会话Complete至最终提交' = @(1500,$null)
}
$results = @()
$seenProcesses = [Collections.Generic.HashSet[int]]::new()
$seenPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$firstFixture = $null
$firstCorpora = @{}
$reassessLongActualUi = $LongActualUiP95BudgetMilliseconds -eq 51
foreach ($path in $ReportPaths) {
    $errorText=$null; $metrics=@()
    $rawReportSha256=$null; $producerOverall=$null; $producerLongActualUiCheck=$null
    $producerLongActualUiExpected=$null; $producerConsistencyVerified=$null
    $commitP95=$null; $policyLongActualUiPassed=$null
    $producerLongRenderedUiCheck=$null; $producerLongRenderedUiExpected=$null
    $longRenderedUiP95=$null; $policyLongRenderedUiPassed=$null
    try {
        if (-not $seenPaths.Add([IO.Path]::GetFullPath($path))) { throw 'Duplicate report path.' }
        $report=Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
        $rawReportSha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        True $report '正式采样'
        if($reassessLongActualUi){$producerOverall=JsonBoolean $report '全部通过'}
        else{True $report '全部通过';$producerOverall=$true}
        if ((Text $report '模式') -ne 'markdown-native-benchmark') { throw 'Wrong native mode.' }
        if ($null -ne (Required $report '错误')) { throw 'Report has error.' }
        if (-not $seenProcesses.Add([int](Number $report '进程ID' -Integer))) { throw 'Reports must use independent processes.' }
        $fixture=Required $report '环境'
        $checks=Required $report '检查'
        foreach ($check in $checks.PSObject.Properties) {
            if($reassessLongActualUi -and $check.Name -eq '长会话提交UI预算'){$null=JsonBoolean $checks $check.Name}
            else{True $checks $check.Name}
        }
        foreach ($checkName in @('Release且无调试器','原生窗口句柄','固定缩放1','固定1200x700视口','无诊断错误','流式100有效批次','流式100完成样本','长会话100有效批次','长会话100完成样本','长会话100提交样本','长会话提交UI预算','长会话资源队列有界','10000块回收守卫','资源队列有界','共享服务未被控件释放','并发共享服务卸载隔离','卸载后旧更新不提交','旧资源请求和订阅已结束','旧Reader可回收','旧Lease可回收')) {
            if($reassessLongActualUi -and $checkName -eq '长会话提交UI预算'){$null=JsonBoolean $checks $checkName}
            else{True $checks $checkName}
        }
        $producerLongActualUiCheck=JsonBoolean $checks '长会话提交UI预算'
        foreach ($key in @('操作系统','进程架构','运行时','配置','NativeHandle类型','NativeBackend','字体','字符字体U+41','字符字体U+4E2D','字符字体U+1F600','NT.AvaloniaControl.MarkdownSHA256','NT.AvaloniaControl.Markdown.ThemesSHA256','NT.AvaloniaControl.Markdown.TextMateSHA256','Markdown.NativeBenchmarksSHA256')) { $null=Text $fixture $key }
        if ($fixture.配置 -ne 'Release' -or $fixture.NativeBackend -notin 'Avalonia.Win32','Avalonia.X11') { throw 'Native Release fixture required.' }
        Same (Number $fixture '窗口宽DIP') 1200 'viewport width'; Same (Number $fixture '窗口高DIP') 700 'viewport height'; Same (Number $fixture '缩放') 1 'scaling'
        $null=Number $fixture '字体大小'
        $frequency=Number $fixture 'StopwatchFrequencyHz' -Integer; if ($frequency -le 0) { throw 'Stopwatch frequency must be positive.' }
        foreach ($key in @('NT.AvaloniaControl.MarkdownSHA256','NT.AvaloniaControl.Markdown.ThemesSHA256','NT.AvaloniaControl.Markdown.TextMateSHA256','Markdown.NativeBenchmarksSHA256')) { if ($fixture.$key -notmatch '^[0-9A-Fa-f]{64}$') { throw 'Invalid assembly hash.' } }
        if ($null -eq $firstFixture) { $firstFixture=$fixture } else {
            foreach ($key in @('操作系统','进程架构','运行时','配置','NativeBackend','字体','字体大小','字符字体U+41','字符字体U+4E2D','字符字体U+1F600','缩放','NT.AvaloniaControl.MarkdownSHA256','NT.AvaloniaControl.Markdown.ThemesSHA256','NT.AvaloniaControl.Markdown.TextMateSHA256','Markdown.NativeBenchmarksSHA256')) { if ($fixture.$key -cne $firstFixture.$key) { throw "Fixture mismatch: $key" } }
        }
        $groups=Array $report '场景'; if ($groups.Count -ne $expectedGroups.Count) { throw 'Eleven native groups required.' }
        $seenGroups=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($group in $groups) {
            $name=Text $group '名称'
            if (-not $expectedGroups.Contains($name) -or -not $seenGroups.Add($name)) { throw 'Unknown or duplicate native group.' }
            $isReassessedLongRenderedGroup=$reassessLongActualUi -and $name -eq '1MiB长会话-20chunk每秒最早未显示至原生帧'
            if($isReassessedLongRenderedGroup){$producerLongRenderedUiCheck=JsonBoolean $group '是否通过'}
            else{True $group '是否通过'}
            if ((Text $group '语料SHA256') -notmatch '^[0-9A-Fa-f]{64}$') { throw 'Invalid corpus hash.' }
            if ($firstCorpora.ContainsKey($name)) { if ($group.语料SHA256 -cne $firstCorpora[$name]) { throw 'Corpus hash differs between rounds.' } } else { $firstCorpora[$name]=$group.语料SHA256 }
            $bytes=Number $group 'UTF8字节数' -Integer
            if ($bytes -le 0 -or ($name -like '100KiB*' -and $bytes -ne 102400) -or (($name -like '1024KiB*' -or $name -like '1MiB*') -and $bytes -ne 1048576)) { throw 'Corpus UTF8 byte count differs.' }
            if ((Number $group '预热次数' -Integer) -lt 10) { throw 'At least 10 warmup iterations required.' }
            $samples=Array $group '样本'; if ($samples.Count -lt 100 -or (Number $group '样本数' -Integer) -ne $samples.Count) { throw 'At least 100 raw samples required.' }
            $total=[Collections.Generic.List[double]]::new(); $ui=[Collections.Generic.List[double]]::new(); $layout=[Collections.Generic.List[double]]::new(); $allocation=[Collections.Generic.List[double]]::new(); $select=[Collections.Generic.List[double]]::new(); $scroll=[Collections.Generic.List[double]]::new()
            $interactions=$name -in '100KiB单代码长行','100KiB单段落','30x30宽表'
            for ($index=0;$index -lt $samples.Count;$index++) {
                $sample=$samples[$index]; True $sample '是否通过'
                if ((Number $sample '序号' -Integer) -ne $index) { throw 'Raw sample sequence differs.' }
                $total.Add((Number $sample '总耗时毫秒'))
                $ui.Add((Number $sample 'UI应用毫秒')+(Number $sample '布局毫秒'))
                $null=Number $sample 'Parse毫秒'; $allocation.Add((Number $sample '分配字节数' -Integer)); $layout.Add((Number $sample '布局毫秒'))
                if ($name -like '*TextMate') { $highlight=Number $sample '高亮完成至绘制总毫秒'; if ($highlight -lt $sample.总耗时毫秒) { throw 'Highlight timestamp precedes first frame.' } }
                if ($interactions) { $select.Add((Number $sample '选择至绘制毫秒')); $scroll.Add((Number $sample '滚动至绘制毫秒')) }
            }
            $p50=Pct $total.ToArray() .5; $p95=Pct $total.ToArray() .95; $ui95=Pct $ui.ToArray() .95
            Same (Number $group '总耗时P50毫秒') $p50 'P50'; Same (Number $group '总耗时P95毫秒') $p95 'P95'; Same (Number $group 'UI批P95毫秒') $ui95 'UI-P95'
            Same (Number $group '布局P95毫秒') (Pct $layout.ToArray() .95) 'layout P95'
            Same (Number $group '单文档累计分配最大字节数' -Integer) (($allocation | Measure-Object -Maximum).Maximum) 'allocation max'
            $budget=$expectedGroups[$name][0]; $uiBudget=$expectedGroups[$name][1]
            $budgetApplicable=Required $group '延迟预算适用'
            if ($budgetApplicable -isnot [bool] -or $budgetApplicable -ne ($null -ne $budget -or $null -ne $uiBudget)) { throw 'Latency budget applicability differs.' }
            if ($null -ne $budget) { Same (Number $group '总耗时P95预算毫秒') $budget 'budget'; if ($p95 -gt $budget) { throw "$name P95 budget failed: $p95 > $budget" } }
            elseif ($null -ne (Required $group '总耗时P95预算毫秒')) { throw 'No frozen latency budget exists for this diagnostic group.' }
            if ($null -ne $uiBudget) {
                Same (Number $group 'UI批P95预算毫秒') $uiBudget 'UI budget'
                if($isReassessedLongRenderedGroup){
                    # This group's samples remain mandatory true semantic results.
                    # Its other producer budgets are null and verified below; only
                    # the original declared UI budget may account for its false.
                    $longRenderedUiP95=$ui95
                    $producerLongRenderedUiExpected=$ui95 -le $ProducerLongActualUiP95BudgetMilliseconds
                    $policyLongRenderedUiPassed=$ui95 -le $LongActualUiP95BudgetMilliseconds
                    if($producerLongRenderedUiCheck -ne $producerLongRenderedUiExpected){throw 'Original producer long-rendered UI group verdict differs from raw samples.'}
                }
                elseif ($ui95 -gt $uiBudget) { throw "$name UI budget failed: $ui95 > $uiBudget" }
            }
            elseif ($null -ne (Required $group 'UI批P95预算毫秒')) { throw 'No frozen UI budget exists for this group.' }
            foreach ($limit in @(@('布局P95预算毫秒',2),@('选择P95预算毫秒',3),@('滚动P95预算毫秒',4),@('单文档累计分配上限字节数',5))) {
                $expected=if($interactions){$expectedGroups[$name][$limit[1]]}else{$null}
                if($null -eq $expected){if($null -ne (Required $group $limit[0])){throw 'No frozen interaction budget exists for this group.'}}
                else{Same (Number $group $limit[0]) $expected $limit[0]}
            }
            if($interactions){
                $layout95=Pct $layout.ToArray() .95; $select95=Pct $select.ToArray() .95; $scroll95=Pct $scroll.ToArray() .95
                Same (Number $group '选择P95毫秒') $select95 'selection P95'; Same (Number $group '滚动P95毫秒') $scroll95 'scroll P95'
                if($layout95 -gt $expectedGroups[$name][2]){throw "$name layout budget failed."}
                if($select95 -gt 150){throw "$name selection budget failed."}
                if($scroll95 -gt 150){throw "$name scroll budget failed."}
                if(($allocation|Measure-Object -Maximum).Maximum -gt 134217728){throw "$name per-document allocation budget failed."}
            }
            $metrics += [pscustomobject]@{ 名称=$name; 样本数=$samples.Count; P50毫秒=$p50; P95毫秒=$p95; UI批P95毫秒=$ui95; 语料SHA256=$group.语料SHA256 }
        }
        $batches=Array $report '流式批次'; if ($batches.Count -lt 100) { throw '100 stream batches required.' }
        $streamGroup=@($groups | Where-Object 名称 -eq '100KiB-20chunk每秒最早未显示至原生帧')[0]
        if ($batches.Count -ne $streamGroup.样本.Count) { throw 'Stream batch/raw sample count differs.' }
        $batchIndex=0
        foreach ($batch in $batches) {
            True $batch '是否通过'; $start=Number $batch '最早Chunk接收Ticks' -Integer; $end=Number $batch '绘制完成Ticks' -Integer
            if ($end -lt $start -or (Number $batch '覆盖Chunk数' -Integer) -lt 1) { throw 'Invalid earliest stream receipt interval.' }
            True $batch '接收版本对应长度'; $null=Number $batch 'SourceVersion' -Integer
            if ((Number $batch 'SourceLength' -Integer) -gt (Number $batch '累计已绘制UTF16' -Integer)) { throw 'Metric source exceeds rendered source.' }
            Same (Number $batch '延迟毫秒') (StopwatchElapsedMilliseconds $start $end $frequency) 'stream elapsed'
            Same $batch.延迟毫秒 $streamGroup.样本[$batchIndex++].总耗时毫秒 'stream/raw elapsed'
        }
        $completions=Array $report '流式完成'; if ($completions.Count -lt 100) { throw '100 completions required.' }
        $completionGroup=@($groups | Where-Object 名称 -eq '100KiB流式Complete至最终提交')[0]
        if ($completions.Count -ne $completionGroup.样本.Count) { throw 'Completion/raw sample count differs.' }
        $completionIndex=0
        foreach ($completion in $completions) {
            True $completion '是否通过'; True $completion '全文选择完整'; if ((Number $completion '完成毫秒') -gt 500 -or (Number $completion '源长度' -Integer) -ne (Number $completion '已绘制长度' -Integer)) { throw 'Final completion budget or source failed.' }
            $start=Number $completion '接收结束Ticks' -Integer; $end=Number $completion '最终应用Ticks' -Integer
            if ($end -lt $start) { throw 'Invalid completion tick interval.' }
            Same $completion.完成毫秒 (StopwatchElapsedMilliseconds $start $end $frequency) 'completion elapsed'
            Same $completion.完成毫秒 $completionGroup.样本[$completionIndex++].总耗时毫秒 'completion/raw elapsed'
        }
        $longBatches=Array $report '长会话流式批次'; $longCompletions=Array $report '长会话流式完成'
        $longGroup=@($groups|Where-Object 名称 -eq '1MiB长会话-20chunk每秒最早未显示至原生帧')[0]
        $longCompleteGroup=@($groups|Where-Object 名称 -eq '1MiB长会话Complete至最终提交')[0]
        if ($longBatches.Count -lt 100 -or $longBatches.Count -ne $longGroup.样本.Count -or $longCompletions.Count -lt 100 -or $longCompletions.Count -ne $longCompleteGroup.样本.Count) { throw 'Long-session 100 raw batches/completions required.' }
        $longIndex=0
        foreach($batch in $longBatches) {
            True $batch '是否通过'; True $batch '接收版本对应长度'; $null=Number $batch 'SourceVersion' -Integer
            $start=Number $batch '最早Chunk接收Ticks' -Integer; $end=Number $batch '绘制完成Ticks' -Integer
            if($end -lt $start -or (Number $batch '覆盖Chunk数' -Integer) -lt 1 -or (Number $batch 'SourceLength' -Integer) -gt (Number $batch '累计已绘制UTF16' -Integer)) { throw 'Long-session receipt or source interval differs.' }
            Same (Number $batch '延迟毫秒') (StopwatchElapsedMilliseconds $start $end $frequency) 'long stream elapsed'
            $sample=$longGroup.样本[$longIndex++]
            Same $batch.延迟毫秒 $sample.总耗时毫秒 'long stream/raw elapsed'
            $renderedBytes=Number $batch '累计已绘制UTF8' -Integer; $finalBytes=Number $batch '最终源UTF8字节数' -Integer
            if($finalBytes -ne 1048576 -or $renderedBytes -le 0 -or $renderedBytes -gt $finalBytes -or (Number $batch '覆盖ChunkUTF8字节数' -Integer) -gt $renderedBytes){throw 'Long-session UTF8 progress differs.'}
            Same (Number $batch '最终大小进度') ($renderedBytes/$finalBytes) 'long source progress'
            foreach($field in @('Parse毫秒','UI应用毫秒','布局毫秒')){Same (Number $batch $field) (Number $sample $field) ('long '+$field)}
            Same (Number $batch '累计分配字节数' -Integer) (Number $sample '分配字节数' -Integer) 'long allocation'
            Same (Number $batch '累计分配每已绘制源字节') ($batch.累计分配字节数/$renderedBytes) 'long normalized allocation'
            $null=Number $batch '托管堆估算字节数' -Integer
        }
        $longCompleteIndex=0
        foreach($completion in $longCompletions) {
            True $completion '是否通过'; True $completion '全文选择完整'
            if((Number $completion '源长度' -Integer) -ne (Number $completion '已绘制长度' -Integer) -or (Number $completion '完成毫秒') -gt 1500) { throw 'Long-session final source or completion budget failed.' }
            $start=Number $completion '接收结束Ticks' -Integer; $end=Number $completion '最终应用Ticks' -Integer
            if($end -lt $start) { throw 'Long-session completion interval differs.' }
            Same (Number $completion '完成毫秒') (StopwatchElapsedMilliseconds $start $end $frequency) 'long completion elapsed'
            Same $completion.完成毫秒 $longCompleteGroup.样本[$longCompleteIndex++].总耗时毫秒 'long completion/raw elapsed'
            if((Number $completion '最终ActiveResourceRequestCount' -Integer) -ne 0){throw 'Long-session active resources did not settle.'}
        }
        $commits=Array $report '长会话提交'; if($commits.Count -lt 100){throw 'Long-session 100 actual commits required.'}
        $commitUi=@(foreach($commit in $commits){
            True $commit '接收版本对应长度'; $null=Number $commit 'SourceVersion' -Integer; $null=Number $commit 'SourceLength' -Integer; $null=Number $commit 'Parse毫秒'
            foreach($bound in @(@('ActiveParseCount',1),@('PendingRequestCount',1),@('ExecutingResourceRequestCount',4),@('PendingResourceRequestCount',256))){if((Number $commit $bound[0] -Integer) -gt $bound[1]){throw 'Long-session diagnostic queue bound failed.'}}
            if((Number $commit '源UTF8字节数' -Integer) -le 0 -or $commit.源UTF8字节数 -gt 1048576){throw 'Long-session commit UTF8 size differs.'}
            (Number $commit 'UI应用毫秒')+(Number $commit '布局毫秒')
        })
        $commitP95=Pct $commitUi .95; Same (Number $report '长会话提交UI批P95毫秒') $commitP95 'long all-commit UI P95'
        $producerLongActualUiExpected=$commitP95 -le $ProducerLongActualUiP95BudgetMilliseconds
        $policyLongActualUiPassed=$commitP95 -le $LongActualUiP95BudgetMilliseconds
        if($reassessLongActualUi){
            # Re-evaluate only a truthful original producer verdict. All groups,
            # other checks, semantic samples, receipts and resource limits retain
            # their original validation. Raw JSON and its 50ms flags are untouched.
            if($producerLongActualUiCheck -ne $producerLongActualUiExpected){throw 'Original producer long-actual UI check differs from raw commits.'}
            $expectedProducerOverall=(@($checks.PSObject.Properties|Where-Object {$_.Value -ne $true}).Count -eq 0) -and (@($groups|Where-Object {$_.是否通过 -ne $true}).Count -eq 0)
            if($producerOverall -ne $expectedProducerOverall){throw 'Original producer overall verdict differs from original checks and groups.'}
            $producerConsistencyVerified=$true
        }
        elseif(-not $policyLongActualUiPassed){throw 'Long-session actual-commit UI budget failed.'}
        $growths=Array $report '长会话退化'; if($growths.Count -ne $longCompletions.Count){throw 'Long-session growth/session count differs.'}
        $growthSessions=[Collections.Generic.HashSet[int]]::new()
        foreach($growth in $growths){
            $session=[int](Number $growth '会话序号' -Integer); if(-not $growthSessions.Add($session)){throw 'Duplicate growth session.'}
            $sessionBatches=@($longBatches|Where-Object 会话序号 -eq $session)
            if($sessionBatches.Count -eq 0){throw 'Growth session has no raw batches.'}
            $size=Number $growth '最终源UTF8字节数' -Integer; if($size -ne 1048576){throw 'Growth final source differs.'}
            $allocated=Number $growth '会话累计分配字节数' -Integer
            $startHeap=Number $growth '开始托管堆估算字节数' -Integer; $finalHeap=Number $growth '最终托管堆估算字节数' -Integer
            Same (Number $growth '托管堆估算变化字节数' -Integer -Signed) ($finalHeap-$startHeap) 'heap delta'
            Same (Number $growth '会话累计分配每最终源字节') ($allocated/$size) 'session normalized allocation'
            foreach($quarterName in @('首四分之一','第二四分之一','第三四分之一','末四分之一')){
                $quarter=Required $growth $quarterName
                $quarterIndex=@('首四分之一','第二四分之一','第三四分之一','末四分之一').IndexOf($quarterName)
                $raw=@($sessionBatches|Where-Object {$_.累计已绘制UTF8 -gt $size*$quarterIndex/4 -and $_.累计已绘制UTF8 -le $size*($quarterIndex+1)/4})
                Same (Number $quarter '样本数' -Integer) $raw.Count 'quarter sample count'
                foreach($field in @('ParseP95毫秒','UI批P50毫秒','UI批P95毫秒','累计分配每已绘制源字节P95','托管堆估算P50字节数','托管堆估算P95字节数')){
                    if($raw.Count -eq 0){if($null -ne (Required $quarter $field)){throw 'Missing quarter must retain null observations.'};continue}
                    $values=@(foreach($entry in $raw){switch($field){
                        'ParseP95毫秒'{$entry.Parse毫秒}
                        {$_ -like 'UI批*'}{$entry.UI应用毫秒+$entry.布局毫秒}
                        '累计分配每已绘制源字节P95'{$entry.累计分配每已绘制源字节}
                        default{$entry.托管堆估算字节数}
                    }})
                    Same (Number $quarter $field) (Pct $values $(if($field -like '*P50*'){.5}else{.95})) ('quarter '+$field)
                }
            }
            $ratioAvailable=$growth.首四分之一.样本数 -gt 0 -and $growth.末四分之一.样本数 -gt 0 -and $growth.首四分之一.UI批P95毫秒 -gt 0 -and $growth.首四分之一.累计分配每已绘制源字节P95 -gt 0
            $ratioFlag=Required $growth '比例可计算'; if($ratioFlag -isnot [bool] -or $ratioFlag -ne $ratioAvailable){throw 'Growth ratio availability differs.'}
            foreach($field in @('UI批P95末首比例','归一累计分配P95末首比例')){
                if(-not $ratioAvailable){if($null -ne (Required $growth $field)){throw 'Unavailable growth ratio must be null.'};continue}
                $ratio=if($field -eq 'UI批P95末首比例'){$growth.末四分之一.UI批P95毫秒/$growth.首四分之一.UI批P95毫秒}else{$growth.末四分之一.累计分配每已绘制源字节P95/$growth.首四分之一.累计分配每已绘制源字节P95}
                Same (Number $growth $field) $ratio 'growth ratio'
            }
            $frozen=Required $growth '退化预算已冻结'; if($frozen -isnot [bool] -or $frozen){throw 'Quarter growth ratio is diagnostic, without a frozen ratio budget.'}
        }
        $virtual=Array $report '虚拟化'; if ($virtual.Count -lt 100) { throw '100 scroll observations required.' }
        foreach ($entry in $virtual) { True $entry '是否通过'; if ((Number $entry 'Realized' -Integer) -gt (Number $entry 'Visible' -Integer)+16 -or (Number $entry 'Retained' -Integer) -gt 16) { throw 'Virtualization bound failed.' } }
        $resources=Required $report '资源'; if ((Number $resources '替换卸载次数' -Integer) -lt 100) { throw '100 replacements required.' }
        foreach ($key in @('活动服务请求','当前失效订阅','已卸载Reader存活数','旧Lease存活数','卸载后旧更新回调','当前Reader活动资源请求')) { if ((Number $resources $key -Integer) -ne 0) { throw "Resource remains: $key" } }
        foreach ($key in @('观察ActiveParse峰值','观察Pending峰值')) { if ((Number $resources $key -Integer) -gt 1) { throw 'Background queue bound failed.' } }
        foreach($bound in @(@('观察ExecutingResource峰值',4),@('观察PendingResource峰值',256))){if((Number $resources $bound[0] -Integer) -gt $bound[1]){throw 'Resource executing/pending queue bound failed.'}}
        ValidateReceipts $report '流式接收' '流式批次' '流式完成' 102400
        ValidateReceipts $report '长会话流式接收' '长会话流式批次' '长会话流式完成' 1048576 '长会话提交'
        if($reassessLongActualUi -and -not $policyLongActualUiPassed){throw 'Long-session actual-commit UI policy budget failed (51ms).'}
        if($reassessLongActualUi -and -not $policyLongRenderedUiPassed){throw 'Long-session rendered UI policy budget failed (51ms).'}
    } catch { $errorText=$_.Exception.Message }
    $results += [pscustomobject]@{
        报告=[IO.Path]::GetFullPath($path); 原始报告SHA256=$rawReportSha256
        是否通过=($null -eq $errorText); 错误=$errorText; 场景=$metrics
        原生产者全部通过=$producerOverall; 原生产者长会话提交UI检查=$producerLongActualUiCheck
        全部实际提交UI重算P95毫秒=$commitP95
        原生产者预算毫秒=$ProducerLongActualUiP95BudgetMilliseconds
        原生产者长会话提交UI重算通过=$producerLongActualUiExpected
        原生产者判定一致性已验证=$producerConsistencyVerified
        本次政策预算毫秒=$LongActualUiP95BudgetMilliseconds
        本次政策长会话提交UI通过=$policyLongActualUiPassed
        原生产者长会话已绘制UI场景通过=$producerLongRenderedUiCheck
        长会话已绘制UI重算P95毫秒=$longRenderedUiP95
        原生产者长会话已绘制UI重算通过=$producerLongRenderedUiExpected
        本次政策长会话已绘制UI通过=$policyLongRenderedUiPassed
    }
}
$passed=$ReportPaths.Count -eq 5 -and @($results | Where-Object { -not $_.是否通过 }).Count -eq 0
$fullOutput=[IO.Path]::GetFullPath($OutputPath); New-Item -ItemType Directory -Path (Split-Path $fullOutput -Parent) -Force | Out-Null
[pscustomobject]@{
    创建时间UTC=[DateTimeOffset]::UtcNow; 全部通过=$passed; 独立进程数=$seenProcesses.Count; 五轮要求=5
    显式长会话实际提交UI政策复评=$reassessLongActualUi
    原生产者长会话实际提交UI预算毫秒=$ProducerLongActualUiP95BudgetMilliseconds
    本次长会话实际提交UI政策预算毫秒=$LongActualUiP95BudgetMilliseconds
    结果=$results
    说明='原生render-thread性能预算与原始数据一致性守卫；默认50ms兼容。显式51ms仅复评长会话UI P95的全部实际提交与已绘制子集两处；原生产者50ms声明、两处原判定与raw SHA保留，其余门槛不变。不认证GPU scan-out、麒麟或物理输入。'
} | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $fullOutput -Encoding utf8
if (-not $passed) { throw "Markdown performance FAIL: see $fullOutput" }
Write-Output 'PASS: Markdown native performance, five validated independent reports.'
