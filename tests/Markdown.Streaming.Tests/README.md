# Markdown 流式纯数据测试

```powershell
dotnet restore tests/Markdown.Streaming.Tests --locked-mode -p:NuGetAudit=false
dotnet run --project tests/Markdown.Streaming.Tests -c Release --no-restore -- -reporter verbose
```

`NuGetAudit=false` 仅用于当前受限网络无法访问漏洞源时使用已锁定的本机包；不改变中央版本和锁文件。集成环境能够访问源时使用仓库常规 locked restore。

当前 41 项测试覆盖 MT07–MT11 及 MT23/MT24 的纯调度部分。慢解析假服务故意忽略取消，通过手动完成顺序证明代次守卫、单解析/单待处理请求、同会话前缀单调提交与最终等待。9 份对抗语料对所有两块分割点、12 个随机种子多块验证完整快照（除迁移身份外）、链接目标和复制投影；额外测试实际提交不完整 CRLF/代理对/实体/ZWJ 后的纠正。

宿主通过 `MarkdownUpdate<T>.TryCommit` 在自己的 UI 调度回调中提交短同步修改，之后可以等待最终布局；`MarkdownApplyResult.LayoutPending` 表示仍挂载但隐藏，`NoPresentationEnvironment` 表示不能等待的错误边界。调度器不等待图片/高亮。取消/卸载保留已接收源文本，新完整替换优先。`Dispose()` 请求正常结束，`Completion` 可观察结果；`DisposeAsync()` 等待该结果。

这些测试不声明真实 AXAML、视觉布局、字体、剪贴板或平台验收通过；对应集成证据见 `Markdown.Headless.Tests` 与平台文档。
