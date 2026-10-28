# TreeDataGrid A6 活动 UIA 内存归因与通知热点

日期：2026-10-27（香港时间）。承接[上一轮混合耐久失败](treedatagrid-a6-automation-soak-status.md)，范围是 Windows 活动 UIA 的分组内存、通知/查询采样与已确认的产品热点。Fluent 构建 API 不在范围内；版本保持 `0.2.0-preview.2` / Avalonia 12.0.3 / net10.0。

本轮分组归因、三处产品热点修复及验证已完成。565 项控件测试、135 项独立校验测试、完整本地流程及无客户端 ABBA 通过；活动 UIA 的 10 分钟混合耐久仍未通过，不能用关闭 provider 后的零保留替代测量期间的护栏。当前功能 DLL 为 `065DE828D26F63860FAD15061B0E39A55AB9C3E39AD9C7EEF5447A40FE3DC577`。证据根为 `artifacts/treedatagrid-alignment/a6-uia-attribution-20261027/`，原始失败与中间候选均保留。

## 分组与内存口径

新增 `automationWorkloadVersion=1` 与 query / events / both；无客户端为 none。独立 Windows MTA 客户端继续使用当前 GridId 的真实 UIA bridge，每个请求包含进程、时间、完整初始树与退休消失校验。query 每帧读取但不订阅；events 只在 attach 读取初始树，帧边界不遍历，保留订阅；both 延续每帧完整值/选择核对及每个正式 soak / 导航 fixture 的属性事件增量校验。每条回复另外保留客户端原始托管堆估计和 Gen2 次数。

events 组收到结构事件，但本次基线的属性事件为 0；初始节点被虚拟化替换后，没有帧查询来连接新后代。该组只回答这种订阅负载的内存变化，不能认证新后代的属性公告。query 组必须保持订阅为空且事件计数为零。版本化校验没有放宽 both 的原有规则。

以下均为独立 60 秒 pilot，仅一轮预热，不作为正式耐久或同吞吐性能比较；实际完成轮数不同。基线产品为上一轮 `A5CD8453...`。每轮包括新表格、自动行高、默认/自定义工厂、同源刷新、编辑/换源/模板/主题及退休。

| 分组 | 完成轮数 | 私有内存稳态中位数增量 MiB | 采样峰值私有 MiB | 峰值时当前托管估计 MiB | 峰值时 last-Any GC committed MiB | 客户端托管估计峰值 MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| none | 30 | -7.20 | 197.53 | 42.42 | 86.14 | — |
| query | 18 | +382.82 | 677.76 | 391.20 | 501.56 | 16.22 |
| events | 30 | +359.45 | 609.89 | 417.87 | 458.42 | 9.97 |
| both | 17 | +380.45 | 679.76 | 382.09 | 479.53 | 15.37 |

只查询和只订阅都触发原 256 MiB 调查护栏，因此不能把增长全部归因于显式状态通知。当前托管估计、last-Any GC 与末次 FullBlocking 参考分开保留；基线 FullBlocking 常是更早的 GC，不可把它从私有字节扣除后就声称剩余全部是原生 UIA 内存。Any 也只是最后一次 GC 的快照。`memory-attribution.json` 保存各检查点的 live、committed、碎片、当前估计、进程和客户端指标，以及退休对象类别。私有字节减去 GC committed 只是不分类残差，包含运行时/JIT/栈、renderer、UIA 和采样时差。

无客户端 pilot 只等待 100ms，末次 299 个弱观察仍保留，独立校验失败；它不覆盖上一轮 30 分钟正式通过，也不作为新泄漏结论。客户端组在负载结束、客户端退出、12 秒排空后完整 GC，再保存 provider 关闭前后的快照；关闭后全部跟踪对象回收不覆盖活动期间的增长。测量循环没有加入强制 GC 或 provider 重置。

## 确认的热点与产品修改

基线 EventPipe 采样 `profile-baseline/`：310260 个 CPU 样本、28162 个 allocation ticks，EventsLost=0、missingStacks=0。UI 定时区的主要分配仍在行测量、布局、Clear/Prepare 与回收；区外 `TreeDataGridAutomationPeer.GetOrCreateChildrenCore` 的采样分配约 77.12 MB，调用链包含 Windows Navigate / ControlAutomationPeer.EnsureConnected / GetParentCore。单元格 Value/Name 查询还反复取得 RealizedRows / RealizedCells 数组并扫描视口。采样数量不是 CPU 毫秒，采样分配不是精确对象分配；inclusive 项相互重叠，不能相加为总量。

直接事件测试确认，不变状态仍会重复广播根结构/选择和每个行、单元格属性。对照旧 DLL 的四项回归全部失败，原始 XML 保留。产品修改为：

1. 根 peer 对比弱引用子宿主及 NodeId / ColumnId，只有宿主/身份/次序变化才通知结构；选择只比较选中集合及选择单位。不缓存强引用的退休控件。
2. 根、行、单元格只对变化后的属性广播，缓存只持有标量/字符串及身份快照，不保存模型、源或工厂。验证错误仍公告，重复错误不重发。
3. 默认纯文本由单元格的 Name/Value 表示，省去内部 TextBlock/ContentPresenter/Border 自动化子树；默认文本与临时空内容切换不产生伪结构通知。编辑器、CheckBox、层级展开器及自定义交互内容保留原有视觉自动化树。直接返回自定义内容 peer 的中间候选破坏父级关系，已撤回，失败测试保留。
4. 可视单元格 peer 使用自己的活动宿主解析 Value/Name，先核验 grid/row/column 身份，避免重复扫描整个视口；逻辑离屏 peer 继续使用原有查找与失效边界。目标对象是临时对象，不跨回收保存。

默认宿主复用、自动行高几何缓存、自定义工厂备用池及回调协议均保持。公共 API、产品依赖和版本没有变化。上一轮单元格显式父级和 COM 线程选择能力探测修复保留。

## 验证与边界

`regression-final-2/results.json` 完整通过，输入前后一致：Core153 / Headless379 / Themes33，共 **565 项**，无跳过；新增五项覆盖重复状态抑制、选择差量、验证错误、默认文本/自定义交互内容和活动编辑器的子树/父级及取消恢复。独立功能/主题包消费、Usage/Demo、数据/UI/40 轮回收、发布目录原生 161/29/36 项及 Gallery 通过。记录校验为原生73 / 自动化45 / 几何9 / 验收8，共 **135 项**。

短候选 `candidate-3-pilot/` 完成 26 轮，结构事件 24414；相较基线 both 的 34123/17 轮，每轮约 2007→939，约减少 53%。该比值是诊断观察，吞吐和事件时序不同，不能替代正式同场景对照。其私有内存仍增长 +559.94 MiB，保持失败。

正式 `soak-after-uia-10m/` 已完成 **600.9457972 秒、194 轮、1632/1632 请求**，包括十轮预热，测量期间没有强制 GC。39 个资源交叉快照全部有效，无当前估计负值或 Any 不一致。私有字节稳态中位数增量 **3804323840 B（3628.09 MiB，约 3.54 GiB）**，句柄 -11；违反原 256 MiB 护栏，独立校验明确失败。吞吐比上一轮不同，不能把较少的每轮增量包装为相同时间内的总内存改善。

测量检查点峰值私有约 4591.34 MiB，对应当前托管估计约 3430.02 MiB、last-Any GC committed 3865.29 MiB。客户端仍连接、未诱发 GC 的末次观察中，194 个表格、94319 个跟踪 peer 尚存；这不是完整 GC 后的泄漏证明。客户端退出、排空和完整 GC 后，关闭 provider 前仍有 2 个表格、774 个 peer、4 个 presenter、30 行/270 单元格/40 列头，模型/源/工厂/上下文均已回收。显式 provider 关闭后 **869352 个跟踪退休对象全部回收**。

最终当前托管估计约 31.15 MiB、last-GC live 32.89 MiB，但 GC committed 仍约 1975.45 MiB、进程 private 2209.69 MiB。容量未回落和活动根的寿命需要分开诊断；该终点不覆盖稳态增长，也不支持把所有私有增量直接称为原生泄漏。原始校验只有 report/checks 与 drift 调查失败，其余请求、事件/条目、生命周期、交叉指标和关闭顺序校验通过。

`paired-binary-proof.json` 固定正式应用，只有功能 DLL/PDB 不同；宿主、主题和客户端字节相同。正式 native 请求/渲染完成、完整 UIA 条目、六场景图像分别核对；无客户端以 before→after→after→before 顺序运行。采样、堆图和正式性能在独立进程中顺序运行。

工具最初把宿主的诊断端口暂停环境继承给客户端，客户端启动文字污染 JSON，`profile-before/` 保留失败。修正为仅移除子进程的 `DOTNET_DefaultDiagnosticPortSuspend`，新 `profile-baseline/` 成功采集；它的宿主仍因原内存护栏退出 1，采样成功并不代表耐久通过。`candidate-2-pilot/` 因构建失败后错误使用 NoBuild，实际使用第一候选；按实际 DLL 哈希标识，不能当作第三候选证据。最终候选重新成功构建后才发布及运行。

A6 继续开放。下一步围绕已观察到的 COM/peer/合成根验证退休资源的安全释放边界，同时区分客户端 SafeHandle 寿命和托管堆容量；不能从进程私有字节直接断言原生泄漏。实际 Windows 输入/读屏/DPI、持续连接的缓存上界、真实 CI、干净部署、麒麟与业务预算仍是独立范围。

## 无客户端保护与活动 UIA 覆盖

同宿主无客户端 ABBA (`none-before-1 → none-after-1 → none-after-2 → none-before-2`) 共 **6000 请求 / 3600 正式测量帧 / 120 个 fixture**。初始产品 peer 全部未创建，全部原始快照和六种图像一致。UI / 渲染完成 P95 均未触发同时增加超过 20% 和 5ms 的调查规则。每场景/每版 300 个正式帧，默认复用收益保留；这不是业务 SLA。

| 场景 | UI P95 前→后 ms | UI 分配变化 |
| --- | ---: | ---: |
| 自动行高默认 | 37.5331→36.8286 | +0.000257% |
| 自动行高自定义 | 35.6983→36.1877 | -0.001410% |
| 十万行平面 | 17.6281→17.2587 | +0.000004% |
| 导航编辑 | 43.7707→43.3416 | +0.000266% |
| 宽树 | 17.2096→17.6032 | +0.000216% |

修复后正式 `paired-after-uia/` 完成 **1500 请求 / 900 正式帧 / 1560 次外部查询**，完整独立校验通过；收到 70410 个结构事件与 43760 个属性事件。属性总数并非优化目标：新差量通知携带实际 ItemStatus 等值，而旧实现部分是空值通知；不能把不同类型/不同状态变化的总数直接解释成重复广播减少。真实逐项公告及屏幕阅读器仍另验。

首个旧版 `paired-before-uia/` 在 1000 秒监督上限内未完成，保留缺失终报告的超时失败；延长基线的准备队列随后因磁盘问题中止，没有形成合格批次。本轮**没有正式活动 UIA 修复前后性能收益结论**，不拼接未完成批次，也不以旧宿主历史数字代替同宿主对照。

## 活动堆根与通知分类

`connected-heap/` 是隔离诊断。第 12 个测量 cycle 的第八帧 UIA 读取完成后、Detach 前暂停，`NativeFixture.14` 仍活动且订阅存在；外部 collector 诱发宿主 GC，再继续负载。因此该进程不能作为耐久或性能合格证据。通知分类只读取收到的 event args，不额外访问 UIA 树或属性。

堆图 EventsLost=0、2705559 个节点、6306382 条边，与声明边数一致；节点浅尺寸合计 256555593 B，不是包括原生内存的完整占用。主要浅尺寸类别包括 ServerCompositionDrawListVisual、ReadbackData、CompositionDrawListVisual、ServerBorderVisual、属性事件委托和属性值存储。产品对象明细中有 14 个表格、5180 个单元格宿主和 5160 个单元格 peer；该时刻只有一个活动 fixture，因此其余表格已经退休。多条引用计数根包含：

`ManagedObjectWrapperHolder → Avalonia.Win32.Automation.AutomationNode → TreeDataGridCellPresenterAutomationPeer → TreeDataGridCellPresenter → ContentPresenter / Border → CompositionBorderVisual / CompositionDrawListVisual → TreeDataGridRowsPresenter → TreeDataGrid`。

诱发 GC 前后，当前托管估计约 311.99→244.98 MiB、私有约 488.37→477.61 MiB。它确认了桥接根可以通过模板/合成对象连接到表格，不能仅靠清空模型解释活动图的寿命；它没有量化每个根独占的保留大小。默认文本内部 peer 减少后，外部必须保留的单元格 peer 仍能形成此类路径。

该诊断客户端收到 23310 个 ChildrenInvalidated，624 个属性事件中 ItemStatus364、IsSelected260。类别和计数独立核对；本负载未覆盖所有公告类型，不将计数为零当作其已认证。后续采样 `profile-after/` EventsLost=0、missingStacks=0、216 个 UI 区间；与同宿主旧版诊断的 136 个区间相比，根子树构建及 RealizedCells 数组的每区间采样分配明显减少，全视口 RealizedCell 扫描未在该样本中命中。旧版该次进程受磁盘失败影响，故仅作诊断，不进入正式性能分位数。

## 环境恢复与证据限制

D 盘一度只余约 2 MiB，导致首次堆诊断构建失败、第一次完整回归中止，并影响同宿主旧版采样进程的结束。原目录和日志保留。仅对本轮及上一轮 TreeDataGrid 证据目录做 NTFS 无损压缩，保留路径与字节，没有删除历史证据；压缩日志、哈希复核及最终清单另存。本轮逻辑约 5.62 GB 压至约 2.53 GB，上一轮约 7.53 GB 压至约 4.19 GB。恢复后隔离堆诊断和新目录 `regression-final-2/` 重新执行完成。

退休模板的隔离试验 `template-retirement-pilot/` 只对已脱离且 DataContext 为空的退休宿主，用公开 Template / ApplyTemplate 暂拆并恢复模板配置，不碰活动/备用池宿主，也不在循环内强制 GC。60 秒试验实际约 61 秒/30 轮，拆解 7243 次；客户端退出、排空和完整 GC 后、关闭 provider 前，30 个表格/所有单元格均已回收，但还有 10 个 peer / 10 个行宿主。私有内存稳态仍增长 **578.63 MiB**，继续违反原护栏。该试验可缩短模板保留链，却没有解决当前内存护栏，并涉及用户工厂/模板配置的释放语义，因此**没有加入产品**；报告带强制失败的 diagnostic-only 检查，不能被误认证为正式耐久通过。

本轮没有重新运行新产品的无客户端 30 分钟耐久；上一轮 A5CD 的 30 分钟证据保留其原有构建范围。本轮默认保护依据新产品的 ABBA、完整生命周期/包消费回归，以及无客户端路径未创建 product peer 的检查，不将旧构建的长耐久冒充新构建。
