param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$testRepo=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$guard=Join-Path $testRepo 'scripts/Test-MarkdownPerformance.ps1'
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $testRepo ('artifacts/guard-tests/markdown-'+[guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$guardSource=$guard
$guard=Join-Path $OutputDirectory 'guard-under-test.ps1'
Copy-Item -LiteralPath $guardSource -Destination $guard -Force
$guardHash=(Get-FileHash -LiteralPath $guard).Hash
$shell=(Get-Process -Id $PID).Path
$runner=Join-Path $OutputDirectory 'invoke-guard.ps1'
@'
param([string]$Guard,[string]$Paths,[string]$Output,[int]$PolicyBudget=50,[int]$ProducerBudget=50)
$ErrorActionPreference='Stop'
$inputs=@(Get-Content -Raw -LiteralPath $Paths | ConvertFrom-Json)
& $Guard -ReportPaths $inputs -OutputPath $Output -LongActualUiP95BudgetMilliseconds $PolicyBudget -ProducerLongActualUiP95BudgetMilliseconds $ProducerBudget
'@ | Set-Content -LiteralPath $runner -Encoding utf8
# Synthetic data test the real guard's acceptance/rejection contract only.
# They are not measured performance and must never enter docs/validation baselines.
$names=@('100KiB混合首屏-纯文本','1024KiB混合首屏-纯文本','100KiB混合首屏-TextMate','1024KiB混合首屏-TextMate','100KiB单代码长行','100KiB单段落','30x30宽表','100KiB-20chunk每秒最早未显示至原生帧','100KiB流式Complete至最终提交','1MiB长会话-20chunk每秒最早未显示至原生帧','1MiB长会话Complete至最终提交')
$checkNames=@('Release且无调试器','原生窗口句柄','固定缩放1','固定1200x700视口','无诊断错误','流式100有效批次','流式100完成样本','长会话100有效批次','长会话100完成样本','长会话100提交样本','长会话提交UI预算','长会话资源队列有界','10000块回收守卫','资源队列有界','共享服务未被控件释放','并发共享服务卸载隔离','卸载后旧更新不提交','旧资源请求和订阅已结束','旧Reader可回收','旧Lease可回收')
function EmptyQuarter { return [pscustomobject]@{样本数=0;ParseP95毫秒=$null;UI批P50毫秒=$null;UI批P95毫秒=$null;累计分配每已绘制源字节P95=$null;托管堆估算P50字节数=$null;托管堆估算P95字节数=$null} }
function Fixture([int]$ProcessId) {
    $groups=@()
    foreach($name in $names) {
        $interaction=$name -in '100KiB单代码长行','100KiB单段落','30x30宽表'
        $count=if($name -eq '1MiB长会话-20chunk每秒最早未显示至原生帧'){200}else{100}
        $samples=@(for($i=0;$i -lt $count;$i++) { [pscustomobject]@{序号=$i;总耗时毫秒=10;Parse毫秒=1;UI应用毫秒=2;布局毫秒=3;分配字节数=20;是否通过=$true;高亮完成至绘制总毫秒=12;选择至绘制毫秒=$(if($interaction){4}else{$null});滚动至绘制毫秒=$(if($interaction){5}else{$null})} })
        $budget=if($name -eq '1MiB长会话Complete至最终提交' -or $name -like '1024KiB*' -or $name -eq '30x30宽表'){1500}elseif($name -like '1MiB*'){$null}elseif($name -like '*chunk*'){150}elseif($name -like '*Complete*'){500}else{300}
        $uiBudget=if($name -like '1024KiB*' -or $name -eq '1MiB长会话-20chunk每秒最早未显示至原生帧'){50}else{$null}
        $groups += [pscustomobject]@{名称=$name;语料SHA256=('A'*64);UTF8字节数=$(if($name -like '1024KiB*' -or $name -like '1MiB*'){1048576}elseif($name -eq '30x30宽表'){20000}else{102400});预热次数=10;总耗时P95预算毫秒=$budget;UI批P95预算毫秒=$uiBudget;延迟预算适用=($null -ne $budget -or $null -ne $uiBudget);布局P95预算毫秒=$(if($interaction){if($name -eq '30x30宽表'){500}else{150}}else{$null});选择P95预算毫秒=$(if($interaction){150}else{$null});滚动P95预算毫秒=$(if($interaction){150}else{$null});单文档累计分配上限字节数=$(if($interaction){134217728}else{$null});样本=$samples;样本数=$count;总耗时P50毫秒=10;总耗时P95毫秒=10;UI批P95毫秒=5;布局P95毫秒=3;选择P95毫秒=$(if($interaction){4}else{$null});滚动P95毫秒=$(if($interaction){5}else{$null});单文档累计分配最大字节数=20;是否通过=$true}
    }
    $checks=[ordered]@{}; foreach($name in $checkNames){$checks[$name]=$true}
    return [pscustomobject]@{
        测试专用=$true;正式采样=$true;全部通过=$true;模式='markdown-native-benchmark';错误=$null;进程ID=$ProcessId
        环境=[pscustomobject]@{操作系统='unit-fixture';进程架构='X64';运行时='.NET unit';配置='Release';NativeHandle类型='HWND';NativeBackend='Avalonia.Win32';字体='unit-font';字体大小=16;窗口宽DIP=1200;窗口高DIP=700;缩放=1;StopwatchFrequencyHz=1000;'字符字体U+41'='unit-font';'字符字体U+4E2D'='unit-font';'字符字体U+1F600'='unit-emoji';'NT.AvaloniaControl.MarkdownSHA256'=('B'*64);'NT.AvaloniaControl.Markdown.ThemesSHA256'=('F'*64);'NT.AvaloniaControl.Markdown.TextMateSHA256'=('C'*64);'Markdown.NativeBenchmarksSHA256'=('D'*64)}
        检查=[pscustomobject]$checks;场景=$groups
        流式批次=@(for($i=0;$i -lt 100;$i++){[pscustomobject]@{会话序号=$i;序号=0;最早Chunk接收Ticks=(1000+$i*1000);绘制完成Ticks=(1010+$i*1000);累计已绘制UTF16=100000;累计已绘制UTF8=102400;覆盖ChunkUTF8字节数=102400;覆盖Chunk数=1;SourceVersion=($i+1);SourceLength=100000;接收版本对应长度=$true;延迟毫秒=10;是否通过=$true}})
        流式完成=@(for($i=0;$i -lt 100;$i++){[pscustomobject]@{会话序号=$i;接收结束Ticks=(2000+$i*1000);最终应用Ticks=(2010+$i*1000);完成毫秒=10;SourceVersion=($i+1);BaseSourceVersion=$i;源长度=100000;已绘制长度=100000;全文选择完整=$true;最终ActiveResourceRequestCount=0;是否通过=$true}})
        流式接收=@(for($i=0;$i -lt 100;$i++){[pscustomobject]@{会话序号=$i;序号=0;SourceVersion=($i+1);累计UTF16结束位置=100000;累计UTF8结束位置=102400;ChunkUTF16长度=100000;ChunkUTF8字节数=102400;接收Ticks=(1000+$i*1000)}})
        长会话流式批次=@(for($i=0;$i -lt 100;$i++){foreach($part in 0..1){$bytes=if($part -eq 0){262144}else{1048576};$utf16=if($part -eq 0){250000}else{1000000};[pscustomobject]@{会话序号=$i;序号=$part;最早Chunk接收Ticks=(1000+$i*2000+$part*1000);绘制完成Ticks=(1010+$i*2000+$part*1000);累计已绘制UTF16=$utf16;累计已绘制UTF8=$bytes;最终源UTF8字节数=1048576;覆盖Chunk数=1;覆盖ChunkUTF8字节数=$(if($part -eq 0){262144}else{786432});SourceVersion=($i*2+$part+1);SourceLength=$utf16;接收版本对应长度=$true;延迟毫秒=10;Parse毫秒=1;UI应用毫秒=2;布局毫秒=3;累计分配字节数=20;托管堆估算字节数=$(if($part -eq 0){1000}else{2000});最终大小进度=($bytes/1048576);累计分配每已绘制源字节=(20/$bytes);是否通过=$true}}})
        长会话流式完成=@(for($i=0;$i -lt 100;$i++){[pscustomobject]@{会话序号=$i;接收结束Ticks=(3000+$i*2000);最终应用Ticks=(3010+$i*2000);完成毫秒=10;SourceVersion=($i*2+2);BaseSourceVersion=($i*2);源长度=1000000;已绘制长度=1000000;全文选择完整=$true;最终ActiveResourceRequestCount=0;是否通过=$true}})
        长会话提交=@(for($i=0;$i -lt 100;$i++){foreach($part in 0..1){[pscustomobject]@{会话序号=$i;序号=$part;SourceVersion=($i*2+$part+1);SourceLength=$(if($part -eq 0){250000}else{1000000});源UTF8字节数=$(if($part -eq 0){262144}else{1048576});Parse毫秒=1;UI应用毫秒=2;布局毫秒=3;ActiveParseCount=1;PendingRequestCount=1;ExecutingResourceRequestCount=4;PendingResourceRequestCount=256;接收版本对应长度=$true}}})
        长会话提交UI批P95毫秒=5
        长会话流式接收=@(for($i=0;$i -lt 100;$i++){foreach($part in 0..1){[pscustomobject]@{会话序号=$i;序号=$part;SourceVersion=($i*2+$part+1);累计UTF16结束位置=$(if($part -eq 0){250000}else{1000000});累计UTF8结束位置=$(if($part -eq 0){262144}else{1048576});ChunkUTF16长度=$(if($part -eq 0){250000}else{750000});ChunkUTF8字节数=$(if($part -eq 0){262144}else{786432});接收Ticks=(1000+$i*2000+$part*1000)}}})
        长会话退化=@(for($i=0;$i -lt 100;$i++){[pscustomobject]@{会话序号=$i;最终源UTF8字节数=1048576;会话累计分配字节数=40;开始托管堆估算字节数=900;最终托管堆估算字节数=2100;托管堆估算变化字节数=1200;会话累计分配每最终源字节=(40/1048576);首四分之一=[pscustomobject]@{样本数=1;ParseP95毫秒=1;UI批P50毫秒=5;UI批P95毫秒=5;累计分配每已绘制源字节P95=(20/262144);托管堆估算P50字节数=1000;托管堆估算P95字节数=1000};第二四分之一=(EmptyQuarter);第三四分之一=(EmptyQuarter);末四分之一=[pscustomobject]@{样本数=1;ParseP95毫秒=1;UI批P50毫秒=5;UI批P95毫秒=5;累计分配每已绘制源字节P95=(20/1048576);托管堆估算P50字节数=2000;托管堆估算P95字节数=2000};比例可计算=$true;UI批P95末首比例=1;归一累计分配P95末首比例=.25;退化预算已冻结=$false}})
        虚拟化=@(for($i=0;$i -lt 100;$i++){[pscustomobject]@{序号=$i;Visible=20;Realized=36;Retained=16;OffsetY=$i;是否通过=$true}})
        资源=[pscustomobject]@{替换卸载次数=100;活动服务请求=0;当前失效订阅=0;已卸载Reader存活数=0;旧Lease存活数=0;观察ActiveParse峰值=1;观察Pending峰值=1;卸载后旧更新回调=0;观察ExecutingResource峰值=4;观察PendingResource峰值=256;当前Reader活动资源请求=0}
    }
}
function MergeFirstLongBatch($Report) {
    $Report.长会话流式批次=@($Report.长会话流式批次|Select-Object -Skip 1)
    $batch=$Report.长会话流式批次[0];$batch.序号=0;$batch.最早Chunk接收Ticks=1000;$batch.覆盖Chunk数=2;$batch.覆盖ChunkUTF8字节数=1048576;$batch.延迟毫秒=1010
    $group=$Report.场景[9];$group.样本=@($group.样本|Select-Object -Skip 1);$group.样本数=199
    for($i=0;$i -lt $group.样本.Count;$i++){$group.样本[$i].序号=$i};$group.样本[0].总耗时毫秒=1010
    $growth=$Report.长会话退化[0];$growth.首四分之一=EmptyQuarter;$growth.比例可计算=$false;$growth.UI批P95末首比例=$null;$growth.归一累计分配P95末首比例=$null
}
function SetStopwatchFixtureFrequency($Reports, [long]$Frequency, [long]$RemainderNanoseconds = 0) {
    foreach ($report in $Reports) {
        $multiplier = [double]$Frequency / [double]$report.环境.StopwatchFrequencyHz
        $report.环境.StopwatchFrequencyHz = $Frequency
        foreach ($field in @('流式批次','流式完成','流式接收','长会话流式批次','长会话流式完成','长会话流式接收')) {
            foreach ($row in $report.$field) {
                foreach ($property in @($row.PSObject.Properties)) {
                    if ($property.Name.EndsWith('Ticks', [StringComparison]::Ordinal)) {
                        $row.($property.Name) = [long]([long]$property.Value * $multiplier)
                    }
                }
            }
        }
        # Add only a sub-TimeSpan-tick remainder to observation/application ends.
        # Source receipt identities, earliest-covered timestamps and native samples remain unchanged.
        foreach ($field in @('流式批次','长会话流式批次')) {
            foreach ($row in $report.$field) { $row.绘制完成Ticks += $RemainderNanoseconds }
        }
        foreach ($field in @('流式完成','长会话流式完成')) {
            foreach ($row in $report.$field) { $row.最终应用Ticks += $RemainderNanoseconds }
        }
    }
}
$cases=@(
    @{Name='valid-synthetic-contract';Pass=$true;Mutate={param($r)}},
    @{Name='linux-clock-1ghz-truncates-18ns';Pass=$true;Mutate={param($r)SetStopwatchFixtureFrequency $r 1000000000 18}},
    @{Name='linux-clock-1ghz-truncates-99ns';Pass=$true;Mutate={param($r)SetStopwatchFixtureFrequency $r 1000000000 99}},
    @{Name='windows-clock-10mhz-exact';Pass=$true;Mutate={param($r)SetStopwatchFixtureFrequency $r 10000000}},
    @{Name='linux-short-batch-one-quantum-drift';Error='stream elapsed';Mutate={param($r)SetStopwatchFixtureFrequency $r 1000000000 18;$r[0].流式批次[0].绘制完成Ticks+=100}},
    @{Name='linux-long-batch-one-quantum-drift';Error='long stream elapsed';Mutate={param($r)SetStopwatchFixtureFrequency $r 1000000000 18;$r[0].长会话流式批次[0].绘制完成Ticks+=100}},
    @{Name='linux-short-complete-one-quantum-drift';Error='completion elapsed';Mutate={param($r)SetStopwatchFixtureFrequency $r 1000000000 18;$r[0].流式完成[0].最终应用Ticks+=100}},
    @{Name='linux-long-complete-one-quantum-drift';Error='long completion elapsed';Mutate={param($r)SetStopwatchFixtureFrequency $r 1000000000 18;$r[0].长会话流式完成[0].最终应用Ticks+=100}},
    @{Name='linux-short-batch-backward-timestamp';Error='Invalid earliest stream receipt interval';Mutate={param($r)SetStopwatchFixtureFrequency $r 1000000000 18;$r[0].流式批次[0].绘制完成Ticks=$r[0].流式批次[0].最早Chunk接收Ticks-1}},
    @{Name='linux-long-batch-backward-timestamp';Error='Long-session receipt or source interval differs';Mutate={param($r)SetStopwatchFixtureFrequency $r 1000000000 18;$r[0].长会话流式批次[0].绘制完成Ticks=$r[0].长会话流式批次[0].最早Chunk接收Ticks-1}},
    @{Name='linux-short-complete-backward-timestamp';Error='Invalid completion tick interval';Mutate={param($r)SetStopwatchFixtureFrequency $r 1000000000 18;$r[0].流式完成[0].最终应用Ticks=$r[0].流式完成[0].接收结束Ticks-1}},
    @{Name='linux-long-complete-backward-timestamp';Error='Long-session completion interval differs';Mutate={param($r)SetStopwatchFixtureFrequency $r 1000000000 18;$r[0].长会话流式完成[0].最终应用Ticks=$r[0].长会话流式完成[0].接收结束Ticks-1}},
    @{Name='json-bool-string';Error='JSON boolean';Mutate={param($r)$r[0].全部通过='true'}},
    @{Name='numeric-string';Error='JSON number';Mutate={param($r)$r[0].场景[0].样本[0].Parse毫秒='1'}},
    @{Name='negative-timing';Error='finite nonnegative';Mutate={param($r)$r[0].场景[0].样本[0].Parse毫秒=-1}},
    @{Name='negative-allocation';Error='finite nonnegative';Mutate={param($r)$r[0].场景[0].样本[0].分配字节数=-1}},
    @{Name='missing-raw-field';Error='Missing field Parse';Mutate={param($r)$r[0].场景[0].样本[0].PSObject.Properties.Remove('Parse毫秒')}},
    @{Name='forged-summary';Error='differs from raw';Mutate={param($r)$r[0].场景[0].总耗时P95毫秒=0}},
    @{Name='consistent-budget-failure';Error='P95 budget failed';Mutate={param($r)foreach($s in $r[0].场景[0].样本){$s.总耗时毫秒=301};$r[0].场景[0].总耗时P50毫秒=301;$r[0].场景[0].总耗时P95毫秒=301}},
    @{Name='ui-budget-failure';Error='UI budget failed';Mutate={param($r)foreach($s in $r[0].场景[1].样本){$s.布局毫秒=49};$r[0].场景[1].UI批P95毫秒=51;$r[0].场景[1].布局P95毫秒=49}},
    @{Name='changed-budget';Error='differs from raw';Mutate={param($r)$r[0].场景[0].总耗时P95预算毫秒=999}},
    @{Name='insufficient-samples';Error='100 raw';Mutate={param($r)$r[0].场景[0].样本=@($r[0].场景[0].样本[0])}},
    @{Name='wrong-sample-count';Error='100 raw';Mutate={param($r)$r[0].场景[0].样本数=101}},
    @{Name='broken-sequence';Error='sequence';Mutate={param($r)$r[0].场景[0].样本[1].序号=0}},
    @{Name='false-raw-semantic';Error='true JSON boolean';Mutate={param($r)$r[0].场景[0].样本[0].是否通过=$false}},
    @{Name='duplicate-process';Error='independent processes';Mutate={param($r)$r[1].进程ID=$r[0].进程ID}},
    @{Name='only-four-processes';Pass=$false;PathsCount=4;Mutate={param($r)}},
    @{Name='missing-group';Error='Eleven native groups';Mutate={param($r)$r[0].场景=@($r[0].场景[0])}},
    @{Name='duplicate-group';Error='Unknown or duplicate';Mutate={param($r)$r[0].场景[1].名称=$r[0].场景[0].名称}},
    @{Name='headless-backend';Error='Native Release fixture';Mutate={param($r)$r[0].环境.NativeBackend='Avalonia.Headless'}},
    @{Name='debug-config';Error='Native Release fixture';Mutate={param($r)$r[0].环境.配置='Debug'}},
    @{Name='wrong-scaling';Error='differs from raw';Mutate={param($r)$r[0].环境.缩放=1.25}},
    @{Name='changed-binary';Error='Fixture mismatch';Mutate={param($r)$r[1].环境.'NT.AvaloniaControl.MarkdownSHA256'=('E'*64)}},
    @{Name='changed-theme-binary';Error='Fixture mismatch';Mutate={param($r)$r[1].环境.'NT.AvaloniaControl.Markdown.ThemesSHA256'=('E'*64)}},
    @{Name='missing-theme-binary-hash';Error='Missing field';Mutate={param($r)$r[0].环境.PSObject.Properties.Remove('NT.AvaloniaControl.Markdown.ThemesSHA256')}},
    @{Name='invalid-binary-hash';Error='Invalid assembly hash';Mutate={param($r)$r[0].环境.'NT.AvaloniaControl.MarkdownSHA256'='abc'}},
    @{Name='changed-actual-fallback-font';Error='Fixture mismatch';Mutate={param($r)$r[1].环境.'字符字体U+4E2D'='another-font'}},
    @{Name='changed-corpus';Error='Corpus hash differs';Mutate={param($r)$r[1].场景[0].语料SHA256=('F'*64)}},
    @{Name='wrong-corpus-size';Error='UTF8 byte count';Mutate={param($r)$r[0].场景[0].UTF8字节数=102401}},
    @{Name='missing-warmup';Error='warmup';Mutate={param($r)$r[0].场景[0].预热次数=0}},
    @{Name='missing-stream-warmup';Error='warmup';Mutate={param($r)$r[0].场景[7].预热次数=0}},
    @{Name='highlighter-before-first-frame';Error='precedes first frame';Mutate={param($r)$r[0].场景[2].样本[0].高亮完成至绘制总毫秒=9}},
    @{Name='missing-selection-timing';Error='Missing field';Mutate={param($r)$r[0].场景[4].样本[0].PSObject.Properties.Remove('选择至绘制毫秒')}},
    @{Name='invalid-stream-receipt';Error='stream elapsed';Mutate={param($r)$r[0].流式批次[0].最早Chunk接收Ticks=1001}},
    @{Name='wrong-stream-source-version';Error='true JSON boolean';Mutate={param($r)$r[0].流式批次[0].接收版本对应长度=$false}},
    @{Name='stream-raw-mismatch';Error='stream/raw elapsed';Mutate={param($r)$r[0].流式批次[0].绘制完成Ticks=1011;$r[0].流式批次[0].延迟毫秒=11}},
    @{Name='missing-stream-batch';Error='100 stream batches';Mutate={param($r)$r[0].流式批次=@($r[0].流式批次[0])}},
    @{Name='completion-tick-mismatch';Error='completion elapsed';Mutate={param($r)$r[0].流式完成[0].最终应用Ticks=2011}},
    @{Name='completion-source-mismatch';Error='Final completion';Mutate={param($r)$r[0].流式完成[0].已绘制长度=99999}},
    @{Name='incomplete-final-selection';Error='true JSON boolean';Mutate={param($r)$r[0].流式完成[0].全文选择完整=$false}},
    @{Name='long-missing-samples';Error='Long-session 100';Mutate={param($r)$r[0].长会话流式批次=@($r[0].长会话流式批次[0])}},
    @{Name='long-tick-mismatch';Error='long stream elapsed';Mutate={param($r)$r[0].长会话流式批次[0].绘制完成Ticks=1011}},
    @{Name='long-final-selection';Error='true JSON boolean';Mutate={param($r)$r[0].长会话流式完成[0].全文选择完整=$false}},
    @{Name='long-invented-budget';Error='No frozen latency';Mutate={param($r)$r[0].场景[9].总耗时P95预算毫秒=150}},
    @{Name='long-no-fake-frozen-budget';Pass=$true;Mutate={param($r)
        foreach($g in @($r[0].场景[9],$r[0].场景[10])){foreach($s in $g.样本){$s.总耗时毫秒=900};$g.总耗时P50毫秒=900;$g.总耗时P95毫秒=900}
        foreach($b in $r[0].长会话流式批次){$b.绘制完成Ticks=$b.最早Chunk接收Ticks+900;$b.延迟毫秒=900}
        foreach($c in $r[0].长会话流式完成){$c.最终应用Ticks=$c.接收结束Ticks+900;$c.完成毫秒=900}
    }},
    @{Name='code-first-frame-budget';Error='P95 budget failed';Mutate={param($r)$g=$r[0].场景[4];foreach($s in $g.样本){$s.总耗时毫秒=301};$g.总耗时P50毫秒=301;$g.总耗时P95毫秒=301}},
    @{Name='paragraph-first-frame-budget';Error='P95 budget failed';Mutate={param($r)$g=$r[0].场景[5];foreach($s in $g.样本){$s.总耗时毫秒=301};$g.总耗时P50毫秒=301;$g.总耗时P95毫秒=301}},
    @{Name='wide-first-frame-budget';Error='P95 budget failed';Mutate={param($r)$g=$r[0].场景[6];foreach($s in $g.样本){$s.总耗时毫秒=1501};$g.总耗时P50毫秒=1501;$g.总耗时P95毫秒=1501}},
    @{Name='code-layout-budget';Error='layout budget failed';Mutate={param($r)$g=$r[0].场景[4];foreach($s in $g.样本){$s.布局毫秒=151};$g.布局P95毫秒=151;$g.UI批P95毫秒=153}},
    @{Name='paragraph-layout-budget';Error='layout budget failed';Mutate={param($r)$g=$r[0].场景[5];foreach($s in $g.样本){$s.布局毫秒=151};$g.布局P95毫秒=151;$g.UI批P95毫秒=153}},
    @{Name='wide-layout-budget';Error='layout budget failed';Mutate={param($r)$g=$r[0].场景[6];foreach($s in $g.样本){$s.布局毫秒=501};$g.布局P95毫秒=501;$g.UI批P95毫秒=503}},
    @{Name='long-block-selection-budget';Error='selection budget failed';Mutate={param($r)$g=$r[0].场景[4];foreach($s in $g.样本){$s.选择至绘制毫秒=151};$g.选择P95毫秒=151}},
    @{Name='long-block-scroll-budget';Error='scroll budget failed';Mutate={param($r)$g=$r[0].场景[5];foreach($s in $g.样本){$s.滚动至绘制毫秒=151};$g.滚动P95毫秒=151}},
    @{Name='wide-selection-budget';Error='selection budget failed';Mutate={param($r)$g=$r[0].场景[6];foreach($s in $g.样本){$s.选择至绘制毫秒=151};$g.选择P95毫秒=151}},
    @{Name='wide-scroll-budget';Error='scroll budget failed';Mutate={param($r)$g=$r[0].场景[6];foreach($s in $g.样本){$s.滚动至绘制毫秒=151};$g.滚动P95毫秒=151}},
    @{Name='single-doc-allocation-budget';Error='per-document allocation budget failed';Mutate={param($r)$g=$r[0].场景[6];$g.样本[0].分配字节数=134217729;$g.单文档累计分配最大字节数=134217729}},
    @{Name='changed-long-block-layout-budget';Error='differs from raw';Mutate={param($r)$r[0].场景[4].布局P95预算毫秒=999}},
    @{Name='changed-long-block-allocation-budget';Error='differs from raw';Mutate={param($r)$r[0].场景[4].单文档累计分配上限字节数=999999999}},
    @{Name='changed-long-completion-budget';Error='differs from raw';Mutate={param($r)$r[0].场景[10].总耗时P95预算毫秒=500}},
    @{Name='forged-long-block-selection-summary';Error='differs from raw';Mutate={param($r)$r[0].场景[4].选择P95毫秒=0}},
    @{Name='long-completion-budget';Error='P95 budget failed';Mutate={param($r)$g=$r[0].场景[10];foreach($s in $g.样本){$s.总耗时毫秒=1501};$g.总耗时P50毫秒=1501;$g.总耗时P95毫秒=1501}},
    @{Name='long-actual-commit-ui-budget';Error='actual-commit UI budget failed';Mutate={param($r)foreach($c in $r[0].长会话提交){$c.布局毫秒=49};$r[0].长会话提交UI批P95毫秒=51}},
    @{Name='long-missing-actual-commits';Error='100 actual commits';Mutate={param($r)$r[0].长会话提交=@($r[0].长会话提交[0])}},
    @{Name='long-forged-source-progress';Error='long source progress';Mutate={param($r)$r[0].长会话流式批次[0].最终大小进度=.5}},
    @{Name='long-forged-quarter-summary';Error='quarter';Mutate={param($r)$r[0].长会话退化[0].末四分之一.UI批P95毫秒=0}},
    @{Name='long-forged-growth-ratio';Error='growth ratio';Mutate={param($r)$r[0].长会话退化[0].UI批P95末首比例=0}},
    @{Name='long-ratio-cannot-freeze-budget';Error='ratio is diagnostic';Mutate={param($r)$r[0].长会话退化[0].退化预算已冻结=$true}},
    @{Name='resource-executing-bound';Error='executing/pending queue bound';Mutate={param($r)$r[0].资源.观察ExecutingResource峰值=5}},
    @{Name='resource-pending-bound';Error='executing/pending queue bound';Mutate={param($r)$r[0].资源.观察PendingResource峰值=257}},
    @{Name='long-resource-executing-bound';Error='diagnostic queue bound';Mutate={param($r)$r[0].长会话提交[0].ExecutingResourceRequestCount=5}},
    @{Name='long-resource-pending-bound';Error='diagnostic queue bound';Mutate={param($r)$r[0].长会话提交[0].PendingResourceRequestCount=257}},
    @{Name='long-active-resource-not-settled';Error='active resources did not settle';Mutate={param($r)$r[0].长会话流式完成[0].最终ActiveResourceRequestCount=1}},
    @{Name='long-missing-middle-quarter';Error='Missing field';Mutate={param($r)$r[0].长会话退化[0].PSObject.Properties.Remove('第二四分之一')}},
    @{Name='long-forged-empty-quarter';Error='quarter sample count';Mutate={param($r)$r[0].长会话退化[0].第二四分之一.样本数=1}},
    @{Name='long-empty-quarter-cannot-fake-zero';Error='null observations';Mutate={param($r)$r[0].长会话退化[0].第三四分之一.UI批P95毫秒=0}},
    @{Name='long-negative-heap-change-accepted';Pass=$true;Mutate={param($r)$r[0].长会话退化[0].最终托管堆估算字节数=800;$r[0].长会话退化[0].托管堆估算变化字节数=-100}},
    @{Name='long-zero-first-allocation-has-no-ratio';Pass=$true;Mutate={param($r)
        $r[0].场景[9].样本[0].分配字节数=0;$r[0].长会话流式批次[0].累计分配字节数=0;$r[0].长会话流式批次[0].累计分配每已绘制源字节=0
        $g=$r[0].长会话退化[0];$g.首四分之一.累计分配每已绘制源字节P95=0;$g.比例可计算=$false;$g.UI批P95末首比例=$null;$g.归一累计分配P95末首比例=$null
    }},
    @{Name='merged-receipts-use-earliest';Pass=$true;Mutate={param($r)MergeFirstLongBatch $r[0]}},
    @{Name='consistent-latest-replaces-earliest';Error='earliest uncovered receipt';Mutate={param($r)
        MergeFirstLongBatch $r[0];$r[0].长会话流式批次[0].最早Chunk接收Ticks=2000;$r[0].长会话流式批次[0].延迟毫秒=10;$r[0].场景[9].样本[0].总耗时毫秒=10
    }},
    @{Name='missing-session-receipts';Error='Receipt/session count';Mutate={param($r)$r[0].长会话流式接收=@($r[0].长会话流式接收|Where-Object 会话序号 -ne 0)}},
    @{Name='missing-initial-receipt';Error='Receipt sequence';Mutate={param($r)$r[0].长会话流式接收=@($r[0].长会话流式接收|Select-Object -Skip 1)}},
    @{Name='consistent-missing-initial-receipt';Error='Receipt version';Mutate={param($r)
        MergeFirstLongBatch $r[0];$r[0].长会话流式接收=@($r[0].长会话流式接收|Select-Object -Skip 1);$r[0].长会话流式接收[0].序号=0
    }},
    @{Name='cover-old-chunk-again';Error='old chunk again';Mutate={param($r)
        $first=$r[0].长会话流式批次[0];$second=$r[0].长会话流式批次[1]
        foreach($field in @('SourceVersion','SourceLength','累计已绘制UTF16','累计已绘制UTF8','覆盖ChunkUTF8字节数','最终大小进度','累计分配每已绘制源字节','托管堆估算字节数')){$second.$field=$first.$field}
        $g=$r[0].长会话退化[0];$g.首四分之一.样本数=2;$g.末四分之一=EmptyQuarter;$g.比例可计算=$false;$g.UI批P95末首比例=$null;$g.归一累计分配P95末首比例=$null
    }},
    @{Name='future-covered-receipt';Error='future receipt';Mutate={param($r)MergeFirstLongBatch $r[0];$r[0].长会话流式接收[1].接收Ticks=2500}},
    @{Name='covered-source-version-has-no-receipt';Error='has no received chunk';Mutate={param($r)$r[0].长会话流式批次[0].SourceVersion=999}},
    @{Name='covered-count-forged';Error='receipt covered count';Mutate={param($r)$r[0].长会话流式批次[0].覆盖Chunk数=2}},
    @{Name='covered-bytes-forged';Error='receipt covered UTF8';Mutate={param($r)$r[0].长会话流式批次[0].覆盖ChunkUTF8字节数=262143}},
    @{Name='receipt-source-length-forged';Error='receipt frame source length';Mutate={param($r)$r[0].长会话流式批次[0].SourceLength=249999}},
    @{Name='completion-precedes-last-receipt';Error='receive-end';Mutate={param($r)$r[0].长会话流式完成[0].接收结束Ticks=1990;$r[0].长会话流式完成[0].最终应用Ticks=2000}},
    @{Name='frame-metric-without-actual-commit';Error='no matching actual commit';Mutate={param($r)
        $r[0].长会话提交=@($r[0].长会话提交|Select-Object -Skip 1);$r[0].长会话提交[0].序号=0
    }},
    @{Name='actual-commit-without-receipt';Error='Actual commit sequence/version';Mutate={param($r)$r[0].长会话提交[0].SourceVersion=999}},
    @{Name='virtualization-off-by-one';Error='Virtualization bound';Mutate={param($r)$r[0].虚拟化[0].Realized=37}},
    @{Name='retained-over-limit';Error='Virtualization bound';Mutate={param($r)$r[0].虚拟化[0].Retained=17}},
    @{Name='live-retired-reader';Error='Resource remains';Mutate={param($r)$r[0].资源.已卸载Reader存活数=1}},
    @{Name='late-old-update';Error='Resource remains';Mutate={param($r)$r[0].资源.卸载后旧更新回调=1}},
    @{Name='unbounded-queue';Error='Background queue';Mutate={param($r)$r[0].资源.观察Pending峰值=2}},
    @{Name='missing-native-check';Error='Missing field';Mutate={param($r)$r[0].检查.PSObject.Properties.Remove('原生窗口句柄')}},
    @{Name='pilot-cannot-pass';Error='true JSON boolean';Mutate={param($r)$r[0].正式采样=$false}},
    @{Name='nonfinite-timing';Error='finite nonnegative';Mutate={param($r)};RawTransform={param($j)[regex]::Replace($j,'"Parse毫秒":\s*1','"Parse毫秒": 1e999')}}
)
$originalCaseCount=$cases.Count
# Revision fixtures preserve semantic sample booleans and all receipt identities.
# Only UI timings/derived summaries and the truthful original producer verdicts
# change. They are synthetic contract tests, never native budget evidence.
function SetLongUiRevisionFixture($Report,[double]$UiMilliseconds) {
    $layoutMilliseconds=$UiMilliseconds-2
    foreach($commit in $Report.长会话提交){$commit.布局毫秒=$layoutMilliseconds}
    foreach($batch in $Report.长会话流式批次){$batch.布局毫秒=$layoutMilliseconds}
    $group=$Report.场景[9]
    foreach($sample in $group.样本){$sample.布局毫秒=$layoutMilliseconds}
    $group.UI批P95毫秒=$UiMilliseconds;$group.布局P95毫秒=$layoutMilliseconds
    $group.是否通过=$UiMilliseconds -le 50
    $Report.长会话提交UI批P95毫秒=$UiMilliseconds
    $Report.检查.长会话提交UI预算=$UiMilliseconds -le 50
    $Report.全部通过=$UiMilliseconds -le 50
    foreach($growth in $Report.长会话退化){
        $growth.首四分之一.UI批P50毫秒=$UiMilliseconds;$growth.首四分之一.UI批P95毫秒=$UiMilliseconds
        $growth.末四分之一.UI批P50毫秒=$UiMilliseconds;$growth.末四分之一.UI批P95毫秒=$UiMilliseconds
    }
}
function SetUnrenderedLongUiRevisionFixture($Report,[double]$UiMilliseconds) {
    # Every first prefix is committed but only the final prefix has a confirmed
    # frame. This separates ALL-commit UI from the rendered subset lawfully.
    foreach($commit in $Report.长会话提交){if($commit.序号 -eq 0){$commit.布局毫秒=$UiMilliseconds-2}}
    $Report.长会话提交UI批P95毫秒=$UiMilliseconds
    $Report.检查.长会话提交UI预算=$UiMilliseconds -le 50
    $Report.全部通过=$UiMilliseconds -le 50
    $Report.长会话流式批次=@($Report.长会话流式批次|Where-Object 序号 -eq 1)
    foreach($batch in $Report.长会话流式批次){
        $batch.序号=0;$batch.最早Chunk接收Ticks-=1000;$batch.覆盖Chunk数=2
        $batch.覆盖ChunkUTF8字节数=1048576;$batch.延迟毫秒=1010
    }
    $group=$Report.场景[9];$group.样本=@($group.样本|Select-Object -First 100);$group.样本数=100
    for($i=0;$i -lt $group.样本.Count;$i++){$group.样本[$i].序号=$i;$group.样本[$i].总耗时毫秒=1010}
    $group.总耗时P50毫秒=1010;$group.总耗时P95毫秒=1010
    foreach($growth in $Report.长会话退化){
        $growth.首四分之一=EmptyQuarter;$growth.比例可计算=$false
        $growth.UI批P95末首比例=$null;$growth.归一累计分配P95末首比例=$null
    }
}
function SetRenderedSubsetUiRevisionFixture($Report,[double]$UiMilliseconds) {
    SetUnrenderedLongUiRevisionFixture $Report 5
    # Nine of 200 actual commits are costly (ALL P95 remains 5ms), while
    # nine of 100 confirmed frames are costly (rendered P95 is the new value).
    for($i=0;$i -lt 9;$i++){
        $Report.长会话提交[$i*2+1].布局毫秒=$UiMilliseconds-2
        $Report.长会话流式批次[$i].布局毫秒=$UiMilliseconds-2
        $Report.场景[9].样本[$i].布局毫秒=$UiMilliseconds-2
        $Report.长会话退化[$i].末四分之一.UI批P50毫秒=$UiMilliseconds
        $Report.长会话退化[$i].末四分之一.UI批P95毫秒=$UiMilliseconds
    }
    $Report.场景[9].UI批P95毫秒=$UiMilliseconds;$Report.场景[9].布局P95毫秒=$UiMilliseconds-2
    $Report.场景[9].是否通过=$UiMilliseconds -le 50
    $Report.全部通过=$UiMilliseconds -le 50
}
$revisionCases=@(
    @{Name='revision-default50-rejects-truthful-long-ui';Error='true JSON boolean';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962}},
    @{Name='revision51-accepts-truthful-long-ui';PolicyBudget=51;Pass=$true;Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962}},
    @{Name='revision51-exact-boundary';PolicyBudget=51;Pass=$true;Mutate={param($r)SetLongUiRevisionFixture $r[0] 51}},
    @{Name='revision51-exceeds-boundary';PolicyBudget=51;Error='actual-commit UI policy budget failed';Mutate={param($r)SetLongUiRevisionFixture $r[0] 51.0001}},
    @{Name='revision51-accepts-all-only-failure';PolicyBudget=51;Pass=$true;Mutate={param($r)SetUnrenderedLongUiRevisionFixture $r[0] 50.6962}},
    @{Name='revision51-accepts-rendered-only-failure';PolicyBudget=51;Pass=$true;Mutate={param($r)SetRenderedSubsetUiRevisionFixture $r[0] 50.3239}},
    @{Name='revision51-rendered-exact-boundary';PolicyBudget=51;Pass=$true;Mutate={param($r)SetRenderedSubsetUiRevisionFixture $r[0] 51}},
    @{Name='revision51-rendered-exceeds-boundary';PolicyBudget=51;Error='rendered UI policy budget failed';Mutate={param($r)SetRenderedSubsetUiRevisionFixture $r[0] 51.0001}},
    @{Name='revision51-at-original50-boundary';PolicyBudget=51;Pass=$true;Mutate={param($r)SetLongUiRevisionFixture $r[0] 50}},
    @{Name='revision51-false-producer-check-below50';PolicyBudget=51;Error='producer long-actual UI check differs';Mutate={param($r)$r[0].检查.长会话提交UI预算=$false;$r[0].全部通过=$false}},
    @{Name='revision51-true-producer-check-above50';PolicyBudget=51;Error='producer long-actual UI check differs';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$r[0].检查.长会话提交UI预算=$true}},
    @{Name='revision51-false-overall-with-all-original-pass';PolicyBudget=51;Error='producer overall verdict differs';Mutate={param($r)$r[0].全部通过=$false}},
    @{Name='revision51-true-overall-with-original-failure';PolicyBudget=51;Error='producer overall verdict differs';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$r[0].全部通过=$true}},
    @{Name='revision51-nonboolean-producer-check';PolicyBudget=51;Error='JSON boolean';Mutate={param($r)$r[0].检查.长会话提交UI预算='false'}},
    @{Name='revision51-true-rendered-group-above50';PolicyBudget=51;Error='producer long-rendered UI group verdict differs';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$r[0].场景[9].是否通过=$true}},
    @{Name='revision51-false-rendered-group-below50';PolicyBudget=51;Error='producer long-rendered UI group verdict differs';Mutate={param($r)$r[0].场景[9].是否通过=$false;$r[0].全部通过=$false}},
    @{Name='revision51-raw-rendered-budget-remains50';PolicyBudget=51;Error='differs from raw';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$r[0].场景[9].UI批P95预算毫秒=51}},
    @{Name='revision51-rendered-sample-semantic-false';PolicyBudget=51;Error='true JSON boolean';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$r[0].场景[9].样本[0].是否通过=$false}},
    @{Name='revision51-other-check-false';PolicyBudget=51;Error='true JSON boolean';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$r[0].检查.旧Reader可回收=$false}},
    @{Name='revision51-other-group-false';PolicyBudget=51;Error='true JSON boolean';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$r[0].场景[0].是否通过=$false}},
    @{Name='revision51-reading-ui-still50';PolicyBudget=51;Error='UI budget failed';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$g=$r[0].场景[1];foreach($sample in $g.样本){$sample.布局毫秒=48.5};$g.UI批P95毫秒=50.5;$g.布局P95毫秒=48.5}},
    @{Name='revision51-nonnull-error';PolicyBudget=51;Error='Report has error';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$r[0].错误='synthetic-error'}},
    @{Name='revision51-forged-actual-ui-summary';PolicyBudget=51;Error='differs from raw';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$r[0].长会话提交UI批P95毫秒=50}},
    @{Name='revision51-still-rejects-forged-receipt';PolicyBudget=51;Error='receipt covered count';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$r[0].长会话流式批次[0].覆盖Chunk数=2}},
    @{Name='revision51-still-rejects-resource-bound';PolicyBudget=51;Error='diagnostic queue bound';Mutate={param($r)SetLongUiRevisionFixture $r[0] 50.6962;$r[0].长会话提交[0].PendingRequestCount=2}}
)
$cases+=$revisionCases
$results=@()
foreach($case in $cases){
    $folder=Join-Path $OutputDirectory $case.Name;New-Item -ItemType Directory -Path $folder -Force|Out-Null
    $reports=@(1..5|ForEach-Object{Fixture (100+$_)})
    & $case.Mutate $reports
    $paths=@(for($i=0;$i -lt $(if($case.PathsCount){$case.PathsCount}else{5});$i++){
        $path=Join-Path $folder "round-$i.json";$json=$reports[$i]|ConvertTo-Json -Depth 20
        if($case.RawTransform -and $i -eq 0){$json=& $case.RawTransform $json}
        $json|Set-Content -LiteralPath $path -Encoding utf8;$path
    })
    $pathsFile=Join-Path $folder 'paths.json';$paths|ConvertTo-Json|Set-Content -LiteralPath $pathsFile -Encoding utf8
    $output=Join-Path $folder 'guard.json'
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$shell;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($arg in @('-NoProfile','-File',$runner,'-Guard',$guard,'-Paths',$pathsFile,'-Output',$output)){$start.ArgumentList.Add($arg)}
    if($case.PolicyBudget){$start.ArgumentList.Add('-PolicyBudget');$start.ArgumentList.Add([string]$case.PolicyBudget)}
    $child=[Diagnostics.Process]::Start($start)
    try{
        $stdout=$child.StandardOutput.ReadToEndAsync();$stderr=$child.StandardError.ReadToEndAsync()
        if(-not $child.WaitForExit(60000)){$child.Kill();throw 'Guard test timeout.'}
        $outcome=Get-Content -LiteralPath $output -Raw|ConvertFrom-Json
        $errors=@($outcome.结果|ForEach-Object 错误)-join "`n"
        $expectedPass=$case.Pass -eq $true
        $valid=($child.ExitCode -eq 0) -eq $expectedPass
        $valid=$valid -and ($outcome.全部通过 -is [bool]) -and ($outcome.全部通过 -eq $expectedPass)
        if($case.PolicyBudget -eq 51){
            $valid=$valid -and $outcome.显式长会话实际提交UI政策复评 -eq $true
            $valid=$valid -and $outcome.原生产者长会话实际提交UI预算毫秒 -eq 50 -and $outcome.本次长会话实际提交UI政策预算毫秒 -eq 51
            if($expectedPass){
                foreach($entry in $outcome.结果){
                    $valid=$valid -and $entry.原生产者判定一致性已验证 -eq $true -and $entry.本次政策长会话提交UI通过 -eq $true -and $entry.本次政策长会话已绘制UI通过 -eq $true
                    $valid=$valid -and $entry.原始报告SHA256 -eq (Get-FileHash -LiteralPath $entry.报告 -Algorithm SHA256).Hash
                }
            }
        }
        if($case.Error){$valid=$valid -and $errors.Contains($case.Error)}
        $results+=[pscustomobject]@{名称=$case.Name;是否通过=$valid;预期接受=$expectedPass;子进程ExitCode=$child.ExitCode;守卫错误=$errors}
        if(-not $valid){$stdout.GetAwaiter().GetResult();$stderr.GetAwaiter().GetResult();throw "Guard contract test failed: $($case.Name), $errors"}
    }finally{$child.Dispose()}
}
[pscustomobject]@{全部通过=$true;用例数=$results.Count;原默认兼容用例数=$originalCaseCount;政策修订用例数=$revisionCases.Count;测试专用=$true;守卫来源=$guardSource;守卫SHA256=$guardHash;说明='合成数据只验证真实脚本的固定副本拒绝行为，不作为渲染或预算证据。';结果=$results}|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $OutputDirectory 'results.json') -Encoding utf8
Write-Output "PASS: Markdown native performance guard $($results.Count) contract tests. Evidence: $OutputDirectory"
