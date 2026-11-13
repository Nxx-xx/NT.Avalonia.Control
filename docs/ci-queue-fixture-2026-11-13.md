# 无障碍通知队列夹具修复（2026-11-13）

## 范围

继续修复前一轮记录的 `Pending_scene_still_announces_unavailable_if_no_scene_is_published_in_the_dispatcher_turn` 独立运行失败。按用户最新要求，本轮不处理 Windows 原生 hover/export，不推送远端。

## 根因

测试的前提是“Background 通知回调执行之前，失效场景仍未重新发布”。普通 Window 自动渲染也会消费 dispatcher；Render 可先发布场景，生产代码随后合法合并中间 unavailable 通知。此时通知列表为空、pending=false，并不表示生产队列丢失消息。之前整类或全量执行通过，不能消除这一顺序依赖。

## 修复

仅目标用例启用 `EmbeddableControlRoot` 测试根，通过公开 `Prepare` / `StopRendering` 生命周期暂停该根的自动渲染。嵌入根注入公开 `OffscreenTopLevelImplBase` 的测试实现，不依赖 Headless 不支持的默认嵌入工厂。该自动化专用宿主没有原生图面或鼠标输入，意外访问鼠标会显式失败；销毁时检查宿主已释放。其余用例仍使用普通 Window；没有更改生产源码、公开 API、全局渲染时钟或 dispatcher 优先级，也没有反射访问私有实现。

用例明确检查控件有效可见、附属于 TopLevel、调度 IsActive、初始点有效，以及失效后确实进入 pending 状态。随后仍由真实 `Dispatcher.UIThread.RunJobs()` 执行 Background 投递，保留 unavailable、CurrentPoint为空、显式发布后恢复数据值等原始断言。没有用直接调用内部 Flush 方法替代队列测试，也没有接受空通知来放宽断言。

## 验证

- 修前相同用例单独运行失败，通知列表为空，见上一轮诊断记录。
- 修后独立运行重复10/10通过；整类8/8通过。
- 最终 Charts Headless Release 全量3267/3267通过，0失败、0跳过，耗时1分49秒。
- 主工程测试编译及Git空白检查通过；无新增生产源码变更。
- 本轮没有Windows操作，也未推送或运行新的GitHub Actions。
