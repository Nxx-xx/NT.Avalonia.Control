# CI 后续修复与验证（2026-11-13）

## 基线与范围

基于与远端 `acd6b06712129c283aa8fd0b57b8c33f7045de35` 相同的源码树继续修复。此轮仅本地提交，不推送；不把 Linux 的测试结果视为 Windows 原生验收。

原始失败：
- [TreeDataGrid Windows 作业](https://github.com/Nxx-xx/NT.Avalonia.Control/actions/runs/43385644238/job/131753338575)
- [Charts Windows native 作业](https://github.com/Nxx-xx/NT.Avalonia.Control/actions/runs/43385644234/job/131753338907)
- [Charts Linux 作业](https://github.com/Nxx-xx/NT.Avalonia.Control/actions/runs/43385644234/job/131753338939)

## 已确认的原因与修复

### TreeDataGrid 跨线程测试

`Task.Run` 不保证与创建集合的线程不同：异步测试让出线程后，线程池可以再次使用同一线程。精确产品源码独立实验中，1000 次 Task.Run 有 959 次复用创建线程；其余 41 次正常抛出跨线程异常，未观察到产品保护失效。显式 Thread 的 100 次实验全部正常拒绝 Count/Add/MoveNext。

回归改用独立 Thread、有界 Join，并明确校验线程不同。保留三项 InvalidOperationException 断言，增加集合未被修改及创建线程可正常枚举检查。异常后的原枚举器不要求可复用（C# iterator 异常会结束枚举状态），使用新枚举器验证创建线程契约。

### Charts artifact

隔离 NuGet HTTP 缓存路径含冒号，upload-artifact 拒绝该路径。四处 charts-platform 上传一致排除 http-cache 与 packages 子目录；不忽略上传失败、不删除结果和诊断证据。使用 @actions/glob 对实际工作流模式进行夹具验证：含冒号缓存及包排除，results.json、packages.lock.json、PNG、stdout.log、source-manifest.json 均保留。

### MultiChart 原生测试夹具

Windows 证据中的每图宽323 DIP、plot宽223.787，所有图仍为 IsActive=true。窗口请求宽度会被宿主工作区约束，原组合断言不能据此认定控件未激活。固定测试内容宽1200 DIP，保留原 plot>250 和高度>=240 断言。

RefreshAsync 后刷新状态文本及原生布局可能触发新一代后台任务；旧 await 不代表最终一代已发布。测试改为有界等待当前 generation 连续稳定、无 busy/refresh pending，保留成功发布、无失败和像素一致性断言，并记录初始 bounds。

## 尚未确认修复的 Windows 项目

LOD/HIT hover 共4项、professional tooltip export 共4项仍需要 Windows native 重跑。证据显示 PNG baseline 相同、JPEG 差异集中于首个数据图形区域，但不足以将原因定论为原生 PointerExited 或控件导出缺陷。

新增只观察的输入事件及状态诊断：合成 pointer ID，分派前后 hover，Move/Exit/CaptureLost 时间戳，导出前后 active 身份。未吞掉原生输入，未放宽身份或像素断言，未更改这些控件输入/导出行为。若重跑失败，可据此区分输入干扰与控件回归，不能把“增加诊断”称为这8项已修复。

## 全量测试新发现：无障碍通知回调测试的调度依赖

首次 Charts Headless 全量3266/3267，失败目标是重入回调发布场景后恢复通知。独跑10次均失败，而整类8项一起执行可通过；诊断表明 dispatcher 执行后场景已经发布、pending=false、通知列表为空，自动Render提前完成后的合法合并绕过了测试试图覆盖的延期分支。

将已存在的延期回调最小提取为internal方法，实际Dispatcher.Post优先级和弱引用语义不变，未增加public API。重入测试显式断言待发布状态后调用真实回调方法，保留unavailable与恢复通知断言。原 `Pending_scene_still_announces_unavailable_if_no_scene_is_published_in_the_dispatcher_turn` 仍保留真实dispatcher路径。但进一步单独运行发现它也会被Render抢先发布而失败（首次全量通过），因此不能声称队列场景已经稳定覆盖；本轮未改它，也未新增测试计数器或屏蔽渲染。不是通过接受两种结果来绕过回归。

## 验证记录

- TreeDataGrid.Core.Tests Release：153/153通过。
- 跨线程回归独立重复运行：5/5通过；另有独立专用线程实验100/100通过。
- 工作流 YAML 解析及四组 @actions/glob 排除/保留夹具通过。
- Demo Release 最终源码编译通过：0 errors，1 条既有 AVA3001 警告。
- Charts Headless 首次全量：3266通过/1失败（总3267）。失败为 `AutomationNotificationCoalescingTests.Deferred_availability_callback_can_publish_a_scene_and_still_announce_restoration`，聚焦复跑亦失败，诊断见上一节，最终状态见下列记录。
- 最终 Charts Headless 全量：3267/3267通过；无障碍聚焦类8/8、修复后callback独跑5/5通过。注意前述未修改queue测试仍存在已观察到的单跑失败，全量通过不等于完全稳定。
- Windows native、实际 GitHub artifact 上传、GitHub Actions 重跑：未执行。无此次推送授权，未更新远端分支。
