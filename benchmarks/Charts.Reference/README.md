# Charts official reference host

C0 固定官方 `Avalonia.Controls.Charts` **12.3.0**，Avalonia 桌面/Headless/Fluent **12.0.3**。项目不加入正常解决方案，不是自研库的依赖。官方公开 API 已从实际签名包通过 MetadataLoadContext 核验；原始版本、包/程序集哈希、默认值证据与完整 API 见 `docs/charts-c0-baseline.json` 和 `docs/charts-c0-api.txt`。

宿主直接链接 `Charts.Benchmarks/DatasetFactory.cs` 与纯 `ChartPoint.cs` 数据结构源文件，未引用自研 Charts 程序集。生成器、种子、逐系列 IEEE754 数据哈希与自研 benchmark 相同。这里使用真实对象属性绑定，显式关闭动画/图例/缩放、固定 1200×650、12px 字体、2px 线宽及 4px 散点，并固定 Gap 缺失值语义。

当前状态为 **blocked-reference**：公开 NuGet 还原已成功并生成独立锁文件；构建在官方许可检查处以 `AVLIC0001` 失败，提示没有适用于 Charts 的有效许可。未绕过许可，宿主 C# 尚未通过编译器验证，也未运行。缺少同样本循环、实际绑定点数、逐场景画质和双平台性能对照。数据相同并不代表绘制语义已经等同，尤其官方柱系列的水平分类兼容规则和实时场景完整范围必须先验证。任何 `initialized-*` 状态都只表示初始化，不表示性能对照通过。

显式选择参考项目后可使用：

```powershell
dotnet restore benchmarks/Charts.Reference/Charts.Reference.csproj -p:EnableOfficialChartsReference=true
dotnet build benchmarks/Charts.Reference/Charts.Reference.csproj -c Release --no-restore -p:EnableOfficialChartsReference=true
dotnet run --project benchmarks/Charts.Reference/Charts.Reference.csproj -c Release --no-build -- --dataset-only --scenario S10-L --output artifacts/charts-reference/dataset.json
dotnet run --project benchmarks/Charts.Reference/Charts.Reference.csproj -c Release --no-build -- --headless --scenario S10-L --output artifacts/charts-reference/headless.json
```

省略 `--headless` 进入原生窗口。运行前由外部安全环境配置符合官方要求的许可；宿主仅检查 `AVALONIA_LICENSE_KEY` 是否存在，不记录许可值、不把它放入参数、项目文件或日志，也不绕过许可验证。环境变量存在不代表授权有效；初始化失败保留 `blocked-reference`，仅记录异常类型。项目未添加含密钥的 `AvaloniaUILicenseKey` 项，若目标许可流程必须生成密钥文件，先调整安全接入方式并保留阻塞状态，不直接写入仓库。

生成的包缓存、文档 HTTP 原始快照及测量证据位于忽略的 `evidence/`；可由 `scripts/Capture-ChartsBaseline.ps1` 生成新的不可变捕获目录。冻结参数仅用于 C1 开始前，已经存在的 C0 文件不会被默认覆盖。

官方来源（访问：2026-10-15）：[NuGet 12.3.0](https://www.nuget.org/packages/Avalonia.Controls.Charts/12.3.0)、[数据/公共契约](https://docs.avaloniaui.net/controls/data-display/charts)、[安装与许可](https://docs.avaloniaui.net/tools/installing-avalonia-pro)、[轴与水平兼容规则](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/axis-customization-chart)。
