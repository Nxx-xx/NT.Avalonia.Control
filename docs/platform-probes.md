# Windows 与麒麟平台探针

P0/P1 基线：.NET SDK 10.0.401、`net10.0`、Avalonia 12.0.3。日期按香港时区记录。用户已确认暂时没有麒麟设备，当前开发与证据覆盖 Windows 和核心模型；麒麟仍为**未验证**。

最新 `0.2.0-preview.2` 于 2026-10-12 13:54 在最终 Release 发布目录完成 MVVM 21/21 和既有 36/36 原生探针。绑定、编辑命令、实际 AXAML 与独立包验证结果见 [MVVM 集成验收](mvvm-status.md)。下文保留各阶段历史证据。

当前探针入口为统一 `NT.AvaloniaControl.Demo`：`--smoke` 验证 TreeDataGrid 核心交互，`--demo-smoke` 验证界面与自定义列模板，`--mvvm-smoke` 验证该页面的 MVVM Tab。下面的重跑命令使用当前项目和输出名称，历史验收时间、数量与环境记录保留原值。

## Windows 自动探针

在仓库根目录执行：

```powershell
./scripts/Collect-Platform.ps1
dotnet build samples/NT.AvaloniaControl.Demo -c Release
dotnet samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.dll --smoke --smoke-output artifacts/platform/windows-smoke-release.json
```

验证发布目录可以独立运行：

```powershell
dotnet publish samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj -c Release --no-restore -o artifacts/publish/windows -m:1 -p:UseSharedCompilation=false
dotnet artifacts/publish/windows/NT.AvaloniaControl.Demo.dll --smoke --smoke-output artifacts/platform/windows-smoke-published-release.json
```

这是依赖已安装 .NET 10 运行时的发布探针，不等于干净机器的自包含安装包验收。`-m:1 -p:UseSharedCompilation=false` 避免共享开发目录下并发构建争用；命令不修改用户级构建配置。

`--smoke` 创建真实桌面窗口，在布局完成后验证原生窗口句柄、控件布局、10 万行的源路径映射、视口范围内的行容器数量、双向滚动与回收、冻结参数、可变行高、亮暗主题切换及树投影，写入 JSON 后自动退出。退出码 0 表示全部自动检查通过，1 表示失败。该进程需要可用的交互式桌面，窗口短暂可见。

JSON 中的 `FirstLayoutMilliseconds` 含主动等待 350 ms 的时间，用于启动诊断，**不是首屏性能基准**。性能基准由专门的 Benchmark 场景测量。快照记录真实可见行容器数和视觉控件数量；P1 按行回收，每个实现行仍创建所有列单元格，横向单元格虚拟化尚未完成。

环境采集脚本只记录系统、CPU、图形驱动、.NET、中文字体与输入语言；不采集主机名、用户名、序列号或网络信息。生成结果默认放在 Git 忽略的 `artifacts/platform`，验证摘要保存在本文件。

## 当前执行证据

2026-10-12 10:53（香港时间），Windows 原生 Debug 探针通过全部 9 项检查：

| 项目 | 记录 |
| --- | --- |
| 系统 / 架构 | Windows NT 10.0.26200 / X64 |
| 运行时 / Avalonia | .NET 10.0.12 / Avalonia 12.0.3.0 |
| 客户区 / 缩放 | 1180 × 780 DIP / 100% |
| 表格字体 | FontSize 14 DIP；未显式指定 FontFamily，继承默认主题 / 平台字体；上述历史 JSON 未记录有效字体名称或中文逐字形回退 |
| 100,000 行 × 20 列首屏 | 18 个实现行，460 个视觉控件 |
| 横纵滚动 + 冻结 2 列 | 20 个实现行，累计复用 19 次 |
| 可变行高 | 18 个实现行，累计复用 37 次 |
| 树样例 | 900 总节点、124 可见行、16 个实现行 |
| 中文 TextBox | 程序化中文字符串赋值 / 读取一致 |
| 原始记录 | `artifacts/platform/windows-smoke-debug.json`（本地生成） |

这组结果证明当前原型创建了真实 Windows 窗口，并在该视口下保持有限行容器。它不证明真实 IME、稳定帧率、完整冻结交互、所有 DPI 或所有平台通过。

2026-10-12 10:56（香港时间），上述 Release 发布命令成功，发布目录原生探针再次 **9 / 9 通过，退出码 0**。快照的实现行、控件数和复用次数与 Debug 记录一致，原始记录为 `artifacts/platform/windows-smoke-published-release.json`。主题回归为 **6 / 6 通过**。

最终于2026-10-12 11:08（香港时间）重新构建并运行发布目录，原生探针9/9通过，退出0。记录表格FontFamily=Inter、平台默认FontFamily=Segoe UI、字号14；实际中文字形回退仍未验证。主题回归最终8/8通过，另含独立NuGet消费者的公开URI编译/运行验证。

`Collect-Platform.ps1` 已生成 `artifacts/platform/windows-environment.json`。受限shell初次无法查询CIM，后续在允许本地诊断的执行环境补齐：Windows11家庭版中文版Build26200、Intel Core i5-14400F（10核16逻辑处理器）、NVIDIA GeForce RTX4060驱动32.0.15.9649；同时存在虚拟显示适配器，不能据列表推断本次实际渲染设备。`dotnet --info`最终退出0。脚本仍会对无法采集的字段标记Available=false，CPU名也支持注册表回退。

## 手工中文输入与系统交互

不带探针参数启动统一 Demo，进入 TreeDataGrid 页的普通模式，展开“诊断与输入测试”。其中的中文 TextBox 用于记录实际输入法版本、输入模式和操作结果：

1. 连续输入拼音或其他组合输入，观察预编辑文本与候选框定位。
2. 分别用 Enter、空格选词，Esc 取消预编辑，Tab 与鼠标切换焦点。
3. 组合输入中滚动、改变窗口尺寸、切换主题，观察字符是否丢失或重复。
4. 检查粘贴中文与混合字符、中文字体回退和 100% / 125% / 150% / 200% 缩放。

P0 TextBox 探针只验证最小系统输入路径。表格编辑器在 P4 接入后，需要重新验证输入法选词不会被误识别为单元格提交。**当前原生 IME、剪贴板、无障碍、拖放和 DPI 矩阵均为待人工验证。**

## 麒麟接入

取得设备后先执行：

```sh
sh scripts/collect-platform.sh artifacts/platform/kylin-environment.txt
dotnet build samples/NT.AvaloniaControl.Demo -c Release
dotnet samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.dll --smoke --smoke-output artifacts/platform/kylin-smoke.json
```

需补齐发行版与版本、实际 CPU、.NET 和原生依赖、X11/Wayland、显卡驱动、中文输入法与字体。脚本采集并不等于兼容认证，通用 Linux 结果也不能替代麒麟真机结果。发布支持列表只纳入已验收的组合。

## 主题与模板自动回归

```powershell
dotnet test tests/TreeDataGrid.Themes.Tests
```

测试覆盖功能程序集不引用主题程序集、默认主题加载、`:empty` 状态、亮暗颜色变化、自定义模板可独立使用、缺少必需部件的清晰诊断，以及切换模板 / 主题后节点身份、展开状态和源版本不变。

后续补齐状态保留与公开 URI 的跨程序集 XAML 编译测试后，主题回归扩展为 **8 / 8 通过**。本次通过命令为 `dotnet tests/TreeDataGrid.Themes.Tests/bin/Debug/net10.0/TreeDataGrid.Themes.Tests.dll`，使用 xUnit v3 自带运行器；对应项目已先行构建成功。

P1 的 `TreeDataGridState` 由应用创建并持有，主控件当前只持有 `Source`。新增换肤回归验证：程序选择的 `SelectedNodes`、`CurrentCell`、同一 `EditSession` 实例及此前已提交的模型值，在亮暗切换与模板替换后保留，且不产生额外模型写入或状态事件。这是**独立状态模型的保留契约**；P4 的实际单元格编辑器、焦点、IME 预编辑和编辑器视觉状态尚未实现，需届时另行验证。

后续 `--smoke` JSON 增补 `GridFontSize`、`GridFontFamily` 和 `PlatformDefaultFontFamily`，用于固定字体环境。它们记录字体选择属性，不证明每个中文字符实际使用的回退字形；字体回退仍需视觉检查。

样式入口为 `avares://NT.AvaloniaControl.TreeDataGrid.Themes/Themes/Generic.axaml`，也可以 `Application.Styles.Add(new TreeDataGridTheme())`。应用需要显式加载基本控件主题（如 FluentTheme），用于 ScrollViewer、Button 和 TextBox 等 Avalonia 控件。

`Generic.axaml` 是独立的公共 Styles 资源，`TreeDataGridTheme` 通过同一 URI 加载它。测试程序集内另有真正的 AXAML `StyleInclude` 消费入口，确保资源既可运行时加载，也能被引用程序集的 XAML 编译器发现。

## P2/P3 原生窗口探针

本轮探针扩为 21 项，包括树批量展开、动态增删、同步按需加载、模型展开绑定、自定义模板、Auto/Star、自动换行行高、插入时的身份锚点以及百列滚动回收。执行：

```powershell
dotnet publish samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj -c Release --no-restore -o artifacts/publish/windows -m:1
dotnet artifacts/publish/windows/NT.AvaloniaControl.Demo.dll --smoke --smoke-output artifacts/platform/windows-smoke-p2-p3-release.json --smoke-image artifacts/platform/windows-smoke-p2-p3-release.png
```

`--smoke-image` 额外生成宽表、树表（`-tree.png`）、换行列（`-columns.png`）三张 Avalonia 离屏渲染，辅助检查排版；这些图像不是桌面截图。展开按钮使用路由 Click 事件验证 UI 处理链；Headless 另用指针事件验证列头拖动。真实 IME、多 DPI、系统拖放与无障碍仍需原生人工验收。

最终结果归档与统计见 [P2/P3 集成验收](p2-p3-status.md)。P1 的 JSON 与历史结果保留，不由本轮覆盖。

## P4/P5 原生发布版验收

2026-10-12，`0.2.0-preview.1` 发布目录的原生 Windows 探针 **36/36 通过、退出0**。最终 JSON 归档为 [windows-smoke-p4-p5.json](validation/windows-smoke-p4-p5.json)，执行环境与产物核对见 [P4/P5 集成记录](p4-p5-status.md)。

新增检查覆盖矩形选择、F2、Enter/Esc、类型校验、无效编辑阻止排序、可空布尔与自定义模板写回、回收及换肤后的草稿、过滤隐藏取消、排序后的身份写回和 Source 替换。Headless 另验证真实指针事件与 TextInputMethodClient 预编辑路由；原生探针使用路由键盘事件和编辑器赋值，不替代操作系统输入法验收。

发布命令与 P2/P3 相同，探针输出参数改为 `artifacts/platform/windows-smoke-p4-p5-release.json` 和 `artifacts/platform/windows-smoke-p4-p5-release.png`。六张离屏图包含既有宽表/树/换行，以及新增 [选区](validation/p4-p5-selection.png)、[错误编辑](validation/p4-p5-editing.png)、[排序过滤](validation/p4-p5-sort-filter.png)。后者三张已复核，亮暗状态、箭头、草稿红框和三态框没有裁切。

应用现在可通过 `grid.State` 使用控件拥有的选择和编辑状态；历史 P1 章节中“主控件只持有 Source”的表述仅适用于当时版本。真实中文 IME、麒麟、多 DPI、剪贴板、系统拖放及无障碍依旧不在当前通过清单中。
