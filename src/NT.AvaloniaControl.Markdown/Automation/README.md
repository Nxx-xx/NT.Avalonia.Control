# Markdown automation 支持级别

`MarkdownAutomationPeer` 的平台 provider 支持只读 `IValueProvider`：`Value` 是完整逻辑复制投影，包含离屏块；`IsReadOnly=true`，`SetValue` 明确拒绝。宿主 `AutomationProperties.Name` 作为文档名称；未设置时名称为 `Markdown`，不把整份长文塞入名称。

逻辑树由不可变文档产生，查询某个父级时才创建它的直接子 peers；表格/列表/引用等保留层级，标题暴露 heading level。块和链接身份由快照节点身份与内联路径约束，移除的节点不能调用另一份同位置的新链接。离屏链接具有 Hyperlink role、URL help、`IInvokeProvider`、焦点与 BringIntoView；调用交给控件的宿主链接事件/脚注定位规则。只读任务具有 CheckBox role、Value 中的 checked 布尔字符串及 `ItemStatus`，不暴露可操作的 Toggle/Invoke provider。

语义角色和动作依据快照的 `SemanticKind`。自定义 converter 的 `Text=null` 保留内置内容时，其标题、链接和任务仍通过原有语义暴露；提供新文本的真正自定义节点不自动冒充内置节点。

Avalonia **12.0.3** 公开的 automation provider 没有 `ITextProvider` 或 `ITextRangeProvider`。此边界由固定包的导出类型和 XML 核验；[固定版本 AutomationPeer 源码](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.0.3/src/Avalonia.Controls/Automation/Peers/AutomationPeer.cs)及[ControlAutomationPeer 源码](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.0.3/src/Avalonia.Controls/Automation/Peers/ControlAutomationPeer.cs)说明 provider 分发、逻辑子树和事件接口。本库因此**不声明原生 UIA TextPattern 或平台文本范围已实现**，也不把用于列表条目的 SelectionPattern 冒充文字选区。

本库 C# `GetTextRange(start,length)` / `GetSelectionRange()` 返回 `MarkdownAutomationTextRange`。范围使用 UTF-16，规范到字素边界，保留捕获的不可变文本。`Select()` / `BringIntoView()` 必须在 UI 线程；文档代次或快照已改变时拒绝旧范围（完整替换开始即失效），避免把旧坐标应用到新文档。它是本库适配接口，不能替代屏幕阅读器所需的平台 TextPattern。文档 Value、逻辑树和选区状态变化会通过 Avalonia peer 事件通知；新增订阅使用弱目标，缓存裁剪到仍存活的节点。

Headless automation tests 验证公开 providers、逻辑结构、离屏访问、身份失效、字素范围和缓存释放；卸载后等待本地解析/UI 队列结束，再检查 reader/peer 可回收，反复重新挂载不增加事件通知。该证据不包含仍被非合作外部服务持有的资源。Windows/Linux/麒麟实际辅助工具、原生桥接的消费体验和平台 TextPattern 扩展仍需独立验收。
