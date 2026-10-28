# TreeDataGrid 本地预览版收尾

日期：2026-10-27。交付版本固定为 `0.2.0-preview.2`，依赖 Avalonia 12.0.3 / .NET 10。收尾目标是当前 Windows 本地预览版可交付；真实平台验收沿用独立门禁。最终结果由交付目录 `release.json`、`SHA256.json` 及当次回归 `results.json` 给出。

本次完整回归通过：Core153 / Headless379 / Themes33，共 565 项，零失败、零跳过；11 项收尾工具测试和全部本地步骤通过，源码在验证期间不变。独立示例已通过发布与原生窗口启动检查。30 分钟 / 899 轮受控 UIA 耐久按相同产品构建复用，私有内存稳态增量 5939200 B（5.66 MiB）；正式原始数据由交付工具再次独立校验。

本次目录：`artifacts/treedatagrid-alignment/a6-closeout-20261027/`。交付目录：`artifacts/releases/treedatagrid/0.2.0-preview.2-local-20261027/`。

## 冻结范围

冻结当前功能/主题、公开 API 与默认值，以及现有身份、选择事务、编辑写回、批量移动、工厂清理和宿主复用行为。本轮改动只涉及验证脚本、交付工具和说明文档。产品功能 DLL 沿用 `065DE828D26F63860FAD15061B0E39A55AB9C3E39AD9C7EEF5447A40FE3DC577`；版本保持不变。后续功能和进一步性能优化另行排期。

当前已实现的平面/层级数据、列配置、排序过滤、选择与编辑、冻结/显隐、列重排、行移动、定位、元素工厂、自动行高和默认宿主复用作为本地预览范围。跨平台/真实输入支持状态依下表记录。

## 本轮交付内容

- 功能包 `NT.AvaloniaControl.TreeDataGrid.0.2.0-preview.2.nupkg`。
- 主题包 `NT.AvaloniaControl.TreeDataGrid.Themes.0.2.0-preview.2.nupkg`。
- 独立 `TreeDataGrid.Usage` 的 framework-dependent 发布目录。
- 使用、绑定、行移动、工厂和无障碍说明。
- 本次完整回归、包消费、发布原生检查和证据索引，以及既有正式原生报告和原始数据引用。
- 全部交付文件的 SHA256/长度清单、ZIP 和 ZIP 校验回执。

示例要求 Windows x64 和 .NET 10，进入解压目录执行：

```powershell
dotnet sample/TreeDataGrid.Usage.dll
```

本机发布目录和启动验证不能代表干净机器部署通过。

## 包接入

将交付的 `packages/` 加到应用的本地 NuGet 源；Avalonia 等依赖由应用已有源提供。项目配置：

```xml
<PackageReference Include="NT.AvaloniaControl.TreeDataGrid" Version="0.2.0-preview.2" />
<PackageReference Include="NT.AvaloniaControl.TreeDataGrid.Themes" Version="0.2.0-preview.2" />
```

在已有 Avalonia Fluent 主题之后加载：

```xml
<StyleInclude Source="avares://NT.AvaloniaControl.TreeDataGrid.Themes/Themes/Generic.axaml" />
```

功能包可单独配合自定义模板使用。绑定、编辑与模板部件规则分别见 `treedatagrid-usage.md`、`treedatagrid-binding-support.md`、`treedatagrid-element-factory.md`。

## 验证与失败记录

`Test-TreeDataGridAlignment.ps1` 在构建前对证据目录、工作区、临时目录和 NuGet 缓存所在卷执行空间预检。每卷按 6 GiB 新增占用规划、另留 2 GiB 余量，相同卷去重；不足或无法读取容量时明确失败并保存 `disk-preflight.json`。该值是规划预算，不承诺磁盘占用硬上限，也不替代运行期间的空间监控。

`Test-TreeDataGridNativePerformance.ps1` 的每卷规划预算为 `2 + 2 × max(1, DurationSeconds/3600)` GiB，另留 2 GiB 余量。`Export-TreeDataGridPreview.ps1` 按不少于 2 GiB、且不少于示例体量三倍的新增占用预算预检，再留 2 GiB。更长负载需事先根据证据体量调整计划。

上述入口结束时保留 `results.json`、简要 `summary.json` 和带哈希的 `evidence-index.json`。失败摘要含阶段、日志路径和错误类别；完整错误保留于日志。构建/应用副本由各自二进制清单记录。

交付工具核对三组控件测试、零跳过、源码一致性、本地验收门禁、功能/主题包身份和包内 DLL、示例 DLL，以及复用的原生记录。正式原生读取/事件批次和 30 分钟耐久使用原始记录重新校验；复用范围以相同功能/主题 DLL 和未变的测量执行代码为依据。脚本记账和 README 差异单独记录，不能将不同客户端后端的耗时解释成产品版本提速。

交付目录可在仓库中校验：

```powershell
python scripts/Export-TreeDataGridPreview.py --verify <交付目录或ZIP解压目录>
```

`SHA256.json` 覆盖包、示例、文档和已复制报告。完整原生 JSONL/图像仍存放在原证据目录，由 `evidence/external-native-evidence-index.json` 给出路径、长度和哈希；交付包仅携带原生报告摘要。历史失败记录保留。

## 验收边界与后续清单

| 范围 | 收尾状态 |
| --- | --- |
| Windows 本地控件、包消费、发布目录程序化检查 | 按本次回归结果关闭本地范围 |
| 受控显式 COM UIA 客户端、正式读取/事件与 30 分钟耐久 | 复用相同产品构建的合格证据 |
| 真实读屏、IME、拖放、系统焦点/剪贴板、DPI | 待验 |
| 干净部署、实际 Windows CI、目标麒麟 | 待验 |
| 业务性能预算和更长真实混合负载 | 后续验收 |

A3.1 任意不透明原生 CompiledBinding 的通用自动安全写回继续开放，可使用已有受控适配器或显式 writer。原生 Grid/Table pattern 未实现；现有 Selection/Value/Expand/Scroll 等结果不扩大为该能力。Fluent 构建 API 明确不在本轮范围。

受控客户端的接口释放修复不能认证任意第三方客户端的持有策略。上一轮长耐久在 provider 关闭前仍有各 10 个行 peer/行宿主残余，关闭 provider 后跟踪对象全部回收；原始报告保留该边界。真实读屏下的焦点/公告及持续连接保留上界继续待验。

整体平台 A6 保持开放，版本调整与公共包发布属于独立发布任务。本轮交付仅生成本地产物。

## 仓库内复跑入口

```powershell
./scripts/Test-TreeDataGridAlignment.ps1 -OutputDirectory artifacts/treedatagrid-alignment/<新的运行目录>
dotnet msbuild samples/TreeDataGrid.Usage/TreeDataGrid.Usage.csproj -t:Publish -p:NoBuild=true -p:Configuration=Release -p:PublishDir=<新的示例目录的绝对路径>/ -m:1 -nr:false
./scripts/Export-TreeDataGridPreview.ps1 -RegressionDirectory artifacts/treedatagrid-alignment/<合格回归目录> -NativeEvidenceDirectory artifacts/treedatagrid-alignment/a6-uia-retirement-20261027 -SampleDirectory artifacts/treedatagrid-alignment/<示例目录> -OutputDirectory artifacts/releases/treedatagrid/<新的交付目录>
```

示例目录须与合格回归使用相同产品 DLL；代码变化后必须重跑受影响验证，不能自动套用本轮原生证据。
