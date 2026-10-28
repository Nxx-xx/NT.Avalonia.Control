# Charts official reference host

C0 固定官方 `Avalonia.Controls.Charts` **12.3.0**，Avalonia 桌面/Headless/Fluent **12.0.3**。项目不加入正常解决方案，不是自研库的依赖。官方公开 API 已从实际签名包通过 MetadataLoadContext 核验；原始版本、包/程序集哈希、默认值证据与完整 API 见 `docs/charts-c0-baseline.json` 和 `docs/charts-c0-api.txt`。

宿主直接链接 `Charts.Benchmarks/DatasetFactory.cs` 与纯 `ChartPoint.cs` 数据结构源文件，未引用自研 Charts 程序集。生成器、种子、逐系列 IEEE754 数据哈希与自研 benchmark 相同。这里使用真实对象属性绑定，显式关闭动画/图例/缩放、固定 1200×650、12px 字体、2px 线宽及 4px 散点，并固定 Gap 缺失值语义。

当前状态为 **blocked-reference**：C0 的公开 NuGet 还原已成功并生成独立锁文件；构建在官方许可检查处以 `AVLIC0001` 失败，提示没有适用于 Charts 的有效许可。2026-10-24 的 C7-03 工作中，用户确认暂无官方许可，先执行自研测量并保留参考阻塞。未绕过许可，宿主 C# 仍没有成功编译与运行的资格。缺少同样本循环、实际绑定点数、逐场景画质和双平台性能对照。数据相同并不代表绘制语义已经等同，尤其官方柱系列的水平分类兼容规则和实时场景完整范围必须先验证。任何 `initialized-*` 状态都只表示初始化，不表示性能对照通过。

显式选择参考项目后可使用：

```powershell
dotnet restore benchmarks/Charts.Reference/Charts.Reference.csproj --locked-mode --disable-parallel -p:EnableOfficialChartsReference=true
dotnet build benchmarks/Charts.Reference/Charts.Reference.csproj -c Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false -p:EnableOfficialChartsReference=true
dotnet run --project benchmarks/Charts.Reference/Charts.Reference.csproj -c Release --no-build -- --dataset-only --scenario S10-L --output artifacts/charts-reference/dataset.json
dotnet run --project benchmarks/Charts.Reference/Charts.Reference.csproj -c Release --no-build -- --headless --scenario S10-L --output artifacts/charts-reference/headless.json
```

省略 `--headless` 进入原生窗口。将来取得许可后，由调用进程的安全环境提供 `AVALONIA_LICENSE_KEY`；无需将密钥发到聊天中。项目仅在 `EnableOfficialChartsReference=true` 且环境变量非空时，把该环境值作为可执行项目的 `AvaloniaUILicenseKey` item 交给官方许可任务。此接入不包含任何密钥字面量，也不将密钥写入命令行、共享 props 或证据 JSON。不要用展开环境值或完整 MSBuild 属性转储来记录许可。没有变量时不生成该 item，仍由正常官方构建校验产生真实结果，不伪造授权或关闭任务。

宿主运行时只检查变量是否存在，不记录其值；环境变量存在不代表授权有效。正常还原、锁定还原、构建许可校验和运行初始化必须分别取得真实证据。初始化失败保留 `blocked-reference`，仅记录异常类型。即使将来合法构建通过，当前宿主也只是数据/布局初始化入口：还需实现并验证共同采样循环、实际绑定点数、原始命中身份、可比画质、后端与计时边界，才能加入性能倍率比较。自研的 Linux 字体隔离 API 不自动注入官方宿主，官方原生依赖或字体失败应另记。

生成的包缓存、文档 HTTP 原始快照及测量证据位于忽略的 `evidence/`；可由 `scripts/Capture-ChartsBaseline.ps1` 生成新的不可变捕获目录。冻结参数仅用于 C1 开始前，已经存在的 C0 文件不会被默认覆盖。

官方来源：[NuGet 12.3.0](https://www.nuget.org/packages/Avalonia.Controls.Charts/12.3.0) 为冻结版本；[数据/公共契约](https://docs.avaloniaui.net/controls/data-display/charts)与[安装及许可](https://docs.avaloniaui.net/tools/installing-avalonia-pro)于 2026-10-24 再次读取，二者都要求可执行项目配置 `AvaloniaUILicenseKey`，并说明可用环境变量或共享 props 提供密钥。当前在线文档不等于固定 12.3.0 的运行时或性能证据；[轴与水平兼容规则](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/axis-customization-chart)的冻结证据继续保留在 C0 记录中。
