# TreeDataGrid P6 可重复性能与耐久记录

更新日期：2026-10-13。[首轮数据 / 几何基线](../artifacts/benchmarks/p6-baseline.json)为 10 个场景 × 5 次共 50 个样本，[后测](../artifacts/benchmarks/p6-after.json)为 12 个场景 × 5 次共 60 个样本，不变量全部通过。AddRange 配对收益、10 个共同场景比较、Headless 滚动和最终耐久验证均已完成；原生帧率、输入延迟、长期耐久与产品预算仍须真实测量。

这些 JSON 按各自测量时间描述该次实验。[交付材料哈希清单](../artifacts/platform/treedatagrid-p6/evidence-manifest.json)链接它们以便复核，不代表基准、UI profile 或扩展耐久测量已使用最后命令通知修复后的 DLL 重跑。最终完整功能回归、原生检查与包消费者另见[执行记录](treedatagrid-p6-status.md)，不将两个时间范围混用。

已增加 `TreeDataGrid.Benchmarks --p6` 和 `scripts/Measure-TreeDataGrid.ps1`。先由统一构建生成 Release 输出，再执行：

```powershell
./scripts/Measure-TreeDataGrid.ps1 -Output artifacts/benchmarks/p6-baseline.json
./scripts/Measure-TreeDataGrid.ps1 -Output artifacts/benchmarks/p6-comparison-run.json -CompareTo artifacts/benchmarks/p6-baseline.json
```

每项至少五个独立 fixture 样本，在同一次进程运行内执行，不是五个独立进程；固定种子 1729、几何视口 1200 × 700。场景覆盖十万行创建 / 刷新、十万行与百列混合行高布局、两万次纯几何滚动位置定位 / 命中、宽树十万子节点、1000 条深度 100 的链共十万节点、以及一万初始行追加 200 项的逐条通知 / DeferRefresh 比较。追加对象在测量前创建，结构更新和投影发布计入测量。`DeferRefresh` 目前合并发布，仍执行图结构重建，不能按名称推定其为结构批处理优化。

结果保存机器、系统、运行时、架构、处理器、构建配置、种子、视口和测量模式，包含每次耗时、累计分配、各代 GC 次数、测量后管理内存 / 工作集及进程累计峰值工作集。汇总 min / median / nearest-rank P95 / max；五次样本的 P95 等于 max。准备、每次前的 full GC、结果验证和清理不计入操作耗时。分配并非保留内存；进程峰值包含准备和之前的场景，不是每个场景的精确峰值。

数据与几何入口不使用字体或渲染器，JSON 明确记录这一点。Headless 的完整控件测量另行记录实际字体、视口、自动行高、持续滚动、列显隐、模板 / 源替换与卸载。Headless 运行器的离屏布局和耗时不代表桌面真实帧率、输入延迟、IME、DPI 或平台支持。

可通过 `TREE_DATAGRID_P6_UI_PROFILE_OUTPUT` 指定 Headless probe JSON 的绝对输出路径，并运行 `P6DurabilityTests`。该模式使用五次 10000 行 × 100 列自动行高窗口，每次连续 100 个程序化滚动位置，记录实际字体和视口；普通回归每次运行 12 个滚动位置。`TREE_DATAGRID_P6_DURABILITY_OUTPUT` 可指定 20 轮滚动 / 显隐 / 换肤 / 模板 / 换源 / 关闭测试的对象保留与管理内存趋势 JSON。两个环境变量只启用材料归档，不扩大设备验收声明。

比较脚本要求同机器、运行时、系统、架构、Release、种子、视口和测量模式一致。耗时中位数同时增加超过 20% 和 5 ms 时标为调查项，不作为产品合格线。JSON 的 Passed 只描述业务 / 身份 / 几何不变量是否通过，不承诺任何耗时预算。

首轮环境：机器 NXX，Windows 10.0.26200，X64，.NET 10.0.12，16 个逻辑处理器，Release；完整处理器描述和原始分布见 JSON。

| 场景 | 中位耗时 ms | 中位累计分配 bytes |
| --- | ---: | ---: |
| 十万平面行创建 | 52.54 | 65,510,040 |
| 十万平面行刷新 | 69.81 | 80,361,376 |
| 十万行 / 百列混合行高几何 | 0.86 | 803,360 |
| 两万滚动位置的 FindRow / HitTest | 6.03 | 672 |
| 十万子节点宽树创建 | 42.17 | 57,360,888 |
| 宽树展开 | 15.68 | 43,909,096 |
| 十万节点 / 深度 100 创建 | 104.55 | 105,398,736 |
| 深树全展开 | 59.42 | 90,283,048 |
| 一万基底逐条追加 200 项 | 1,337.40 | 1,388,468,360 |
| 同基底 DeferRefresh 追加 200 项 | 723.35 | 791,670,528 |

Q3 状态：基线显示 200 次结构追加有明显累计分配和重建成本；DeferRefresh 合并投影后仍有较大分配。新增可选事务集合的 AddRange 路径后，测量入口增加 `row-move-collection-add-200-base-10000` 与 `row-move-collection-addrange-200-base-10000` 配对场景。两者使用相同集合类型、源、模型和配置，分别追加 200 次与一次 range 通知，仍各执行五次；这评估可选结构批导入，不代表更改 ObservableCollection 或原 DeferRefresh 算法。

同机[后测](../artifacts/benchmarks/p6-after.json)的 12 场景 × 5 次共 60 样本全部通过。其中配对结果如下：

| 同一事务集合追加方式 | 中位耗时 ms | P95 ms | 中位累计分配 bytes | 投影通知 |
| --- | ---: | ---: | ---: | ---: |
| 逐条 Add 200 次 | 1,210.6002 | 1,224.3947 | 1,388,576,920 | 200 |
| 一次 AddRange 200 项 | 1.9491 | 2.0333 | 7,058,000 | 1 |

在这一万基底 / 200 项追加场景中，AddRange 的中位耗时降低约 99.84%，累计分配降低约 99.49%。收益来自一次已索引 range 通知和一次源图重建；不将这一收益推及一般更新、其它集合或 UI 帧率。原基线没有这两个新增场景，[比较 JSON](../artifacts/benchmarks/p6-after.comparison.json)明确标记 `NewScenario=true`，其历史差值、百分比和调查结果均为空，不能制造历史比值。10 个共同场景均通过严格同环境比较，调查阈值超出项为 0；阈值要求中位耗时同时增加超过 20% 与 5 ms，普通波动不作为其它算法的优化收益。

进一步优化必须保留前后同场景原始样本与分配比较，并回归 NodeId、通知顺序、选区、草稿和订阅。行高前缀更新与一般增量图维护尚无本轮测量支持，不扩大实现范围。

Q5 当前环境的生命周期回归已完成。首轮[未排空工作队列的历史记录](../artifacts/benchmarks/p6-durability.json)中，20000 个退休模型保留数为 0，模型订阅已解除；但 full-GC 后管理内存从第 5 轮约 30.8 MB 增至第 20 轮约 104.9 MB。该记录保留为诊断起点，不能据模型释放宣称内存稳定，也不能用它替代下述最终观测边界。

后续[排队工作诊断](../artifacts/benchmarks/p6-durability-diagnostics.json)执行 `Dispatcher.UIThread.RunJobs` 和 layout 更新，降低了排空前保留的视觉对象和托管内存；额外三个 Headless render timer tick 没有消除剩余单个 RowsPresenter / ScrollViewer，未据此认定 renderer tick 是原因。最终测试将 exercise helper 设为 NoInlining，在它的栈帧返回后再 full-GC，避免循环局部变量被误计为产品持有。

[最终20轮复测](../artifacts/benchmarks/p6-durability-final.json)已执行，Passed=true：

| 已跟踪退休对象 | 总数 | 外帧返回后保留 |
| --- | ---: | ---: |
| 模型 | 20,000 | 0 |
| Source | 20 | 0 |
| RowsPresenter | 20 | 0 |
| RowPresenter | 156 | 0 |
| CellPresenter | 1,384 | 0 |
| ScrollViewer | 20 | 0 |

第5 / 10 / 15 / 20轮排空后的托管内存记录分别为 6,253,584 / 6,477,128 / 6,616,832 / 6,877,496 bytes，按十进制 MB 为约6.25 / 6.48 / 6.62 / 6.88。weak-reference 列表及测量设施本身随样本数增长，不能把较小内存差异直接归因于产品对象。循环帧内诊断仍可能看到一个 part；最终零保留断言是在 NoInlining 外帧返回后执行，不能混用两个观测时点。

上述排空操作模拟 UI 工作得到执行后的观测边界。结果证明本轮已跟踪旧模型、源和视觉对象可回收，不涵盖所有可能保留的对象；约6.25–6.88 MB 是测量值，不是长期 native 使用的内存预算，也不能代替真实桌面耐久试验。

首轮自动行高 UI probe 已完成 5 × 100 个程序化滚动位置，记录于 `artifacts/benchmarks/p6-ui-profile.json`；实际字体为 `compositefont:fonts:Inter#Inter, $Default#Inter, $Default`、14 DIP，视口 640 × 360。此材料覆盖控件布局与容器边界，仍不证明 native FPS 或真实输入延迟。真实持续滚动与窗口交互的长期趋势需桌面环境记录；管理对象趋势和订阅解除分别验证，进程工作集的瞬时变化不能单独证明泄漏或无泄漏。
