# P4/P5 实施与集成记录

本轮按三条子线程并行开发，产品版本 `0.2.0-preview.1`。父线程维护排序列头桥接、公共 API 审阅、跨模块验收、独立包消费与性能记录。

| 工作线 | 所有权 | 本轮目标 |
| --- | --- | --- |
| P4 选择编辑 | State / Controls / Primitives / 专项测试 | 行和单元格选择、单矩形范围、键盘导航/搜索、暂存编辑、验证和提交取消、回收与隐藏联动 |
| P5 排序过滤 | Source / Core 投影 / 专项测试 | 同级稳定单列排序、三态清除、逐节点谓词过滤、动态更新和异常/批处理契约 |
| 主题与样例验证 | Themes / Demo / 组合测试 / Windows 探针 | 选区/当前/编辑/错误/排序状态、可操作样例及跨模块回归 |

共享规则：源 IndexPath 与 NodeId 不随排序/过滤改变；隐藏不是删除；模型写回前必须转换和校验；编辑会话的暂存值不依附回收控件。行选择保留身份；矩形单元格选择保留原节点和列集合，投影变化导致可见集合不再构成单矩形时，收敛到仍可见的当前单元格，不能按旧端点补选别的对象。

P1 和 P2/P3 历史 API 快照分别保存在 `validation/p1-public-api.txt` 与 `validation/p2-p3-public-api.txt`；完整旧 API 兼容测试本轮继续执行。排序过滤属于投影操作，新增基类方法保持 virtual，不增加派生类必须实现的 abstract 成员。

当前状态：**Windows 与核心模型范围的 P4/P5 已集成验收，版本 0.2.0-preview.1**。本轮没有推进 P6/P7，也没有上传公共包源。

## 验收结果

2026-10-12，本机 Windows x64，SDK 10.0.401 / .NET 10.0.12 / Avalonia 12.0.3。最终执行 `scripts/Verify.ps1` 退出 0：锁定还原成功、Release 构建零警告零错误、160 项测试全部通过、双 NuGet 包生成及全新缓存的外部 XAML 消费通过。

| 验证 | 结果 | 覆盖 |
| --- | ---: | --- |
| Core | 92/92 | 旧源/树/状态回归；21 项排序过滤；7 项 P4 状态；4 项 API/依赖契约 |
| Headless | 50/50 | 旧布局回收；14 项 P4 交互；9 项排序、绑定和跨模块整合 |
| Themes | 18/18 | 旧主题回归；6 项实际编辑器、亮暗、模板、选择与排序组合 |
| 原生 Windows 发布目录 | 36/36 | 原 21 项 + 15 项编辑选择/校验/排序过滤/回收组合 |
| 独立 NuGet 消费 | 通过 | 两程序集、五种列、公开主题 URI、编译 XAML、按需展开、可空布尔/模板编辑、矩形选区和排序过滤 |
| Release 性能诊断 | 18 场景 × 5 轮通过 | 保留原 12 场景，新增十万行/节点排序、过滤及批量清除 |

完整 API 快照已更新，P1 与 P2/P3 的公开及受保护成员兼容检查均通过。新增 Source 方法为 virtual，不要求已有自定义 Source 增加抽象成员。最终包消费目录为 `artifacts/consumer/0794238beb7c4bc584309e2beb36111f`，没有引用仓库源码项目。

集成中修复了：验证/错误通知回调更换 Source 或移除目标后的旧对象写回；提交使末行被过滤后的键盘越界；提交期间选择通知漏发；列头调宽与排序冲突；绑定默认 UnsetValue 与显式 null 的区分；程序更新草稿与内置编辑器不同步；28 DIP 行中的复选框裁切。相关回归全部纳入上述测试。

## 可运行产物与记录

- `artifacts/packages/NT.AvaloniaControl.0.2.0-preview.1.nupkg`
- `artifacts/packages/NT.AvaloniaControl.Themes.0.2.0-preview.1.nupkg`
- `artifacts/publish/windows/TreeDataGrid.Demo.exe`：默认打开可编辑示例，顶部提供选择、编辑、过滤、排序和亮暗切换。
- [公共接口](public-api.txt)、[P4 契约](p4-interaction.md)、[P5 契约](p5-projection.md)、[示例操作](p4-p5-examples.md)。
- [Windows 原始记录](validation/windows-smoke-p4-p5.json)、[五轮性能记录](validation/performance-p4-p5.json)、[产物核对](validation/p4-p5-verification.json)。
- [矩形选区](validation/p4-p5-selection.png)、[错误草稿](validation/p4-p5-editing.png)、[暗色排序过滤](validation/p4-p5-sort-filter.png)：Avalonia 离屏控件渲染，已查看，不是桌面截图。

最终原生窗口为 1180×780 DIP、100% 缩放、Inter 14。十万行 × 百列滚动后实现 16 行、128 单元格、8 列头；可编辑样例实现 14 行、70 单元格。五轮核心诊断中，十万行升序排序中位 66.02 ms，降序 67.93 ms，已排序数据过滤一半 72.14 ms；这不表示输入延迟或滚动帧率。

## 保留边界

真实中文输入法、麒麟、多 DPI 和无障碍仍是单独的平台验收项。Headless 预编辑代理测试和原生 TextBox 赋值不能替代真实输入法选词、候选框及焦点切换验收。用户尚无麒麟设备，本轮没有据 Windows 结果宣称跨平台通过。

源仍提前索引全部已加载节点，最大深度 256；模型通知会完整重建图，排序过滤同步执行。过滤不自动保留祖先；矩形身份重排不连续时收敛当前单格。任意用户 setter 的外部副作用无法回滚。列重排、行拖放、双入口补齐、完整无障碍、持续滚动统计和完整发布支持矩阵留在 P6/P7。
