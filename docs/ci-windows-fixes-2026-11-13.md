# Windows CI 修复证据（2026-11-13）

## 基线与边界

基于远端 `935c52eaf3c3b497fb8119fdddf27f5c2fae0300`。本轮仅本地提交，未推送或重跑 GitHub Actions。Linux 上的 PowerShell 和 Headless 验证不等于 Windows 原生验收。

- [TreeDataGrid Windows 作业](https://github.com/Nxx-xx/NT.Avalonia.Control/actions/runs/43395102122/job/131783999878)
- [Charts 运行及原始 artifact](https://github.com/Nxx-xx/NT.Avalonia.Control/actions/runs/43395102090)

## TreeDataGrid：已接受的 pending 退出码泄漏

原始 `results.json` 为 `passed=true/error=null`，自动化全部成功；`acceptance-windows.json` 为 `passed=false/errors=[]`，真实设备证据仍有七项 pending。脚本预期 release profile 返回 2，但最后没有成功出口，GitHub 默认 pwsh 包装器继续传播 `$LASTEXITCODE=2`。

仅在自动化、证据输出和 finally 成功结束后显式 `exit 0`。没有修改验收 JSON，没有将 pending 写成通过，也没有放宽既有 local/release profile 门禁。

新增真实脚本尾部与 GitHub 包装器回归：local/release `(0,2)` 成功，`(1,2)`、`(2,2)`、`(0,1)`、`(0,0)` 仍拒绝；移除修复的对照实际复现 evidence passed=true 但进程退出 2。PowerShell 7.5.4 来自微软官方发布，安装于临时目录，未修改系统设置。

## LOD/HIT：输入发送早于最终布局

最新事件证据中四次路由输入均已正确命中：LOD 是 `sample-23430/Index23430`，HIT 是 `front-1/Index1`。随后 LOD Bounds.Y 由 174 变为 212，HIT Width 由 520 变为 486，悬停才清空。四条记录均没有 PointerExited。

`CartesianChart` 在 Bounds 改变时调用 `CancelInput`，因此不能沿用旧坐标下的悬停。夹具新增有界就绪等待：布局有效、PlotArea 非空、没有 pending refresh，连续两次几何稳定后才发送输入；10 秒仍不满足则明确失败。不把固定延时当成功条件。保留原 ID/index 断言并新增悬停前后 Bounds 不变断言。

两个 Headless 回归分别故意制造待处理的位置和宽度变化：变化前命中正确，布局后旧悬停合法清空，稳定布局重新输入后原始身份保持。

## Analysis 导出：悬停强调边框泄漏

原实现只屏蔽导出中的 Tooltip，却仍以 `_active` 为 mark 绘制强调边框。Windows artifact 中 JPEG 差异集中在首层 Funnel 边界；PNG 相同不能证明 JPEG 报错无效，异步让出可能改变活跃状态。

新增回归在修复前实际失败：导出绘图命令含悬停 pen。修复仅在导出深度非零时令渲染局部 `active=null`，不改变控件真实活动点或选择，不放宽像素断言。回归同时验证实时悬停边框仍在、导出不改变 ActivePoint、持久 SelectedIndex 的边框保留且不随悬停改变。

## 验证

- PowerShell closeout：12/12，无跳过；Python acceptance：8/8。
- 新增 Analysis 导出回归：修前失败；修后通过。
- SharedTooltipPresentationTests 与 HoverLayoutReadinessTests 聚焦：62/62。
- 最终 Charts Headless Release 全量：3270/3270，0 失败、0 跳过（2 分 12 秒）。
- Demo Release 最终构建成功：0 错误、1 条既有 AVA3001 警告。
- Windows 原生 smoke、物理输入、真实设备验收及新 Actions：未执行，不能宣称 8 项 Windows 失败已经全部消失。

复现命令：

```sh
python -m unittest discover -s scripts/Tests -p 'test_treedatagrid_closeout.py' -v
python -m unittest discover -s scripts/Tests -p 'test_treedatagrid_acceptance.py' -v
dotnet test tests/Charts.Headless.Tests/Charts.Headless.Tests.csproj -c Release
dotnet build samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj -c Release
```

closeout 的 PowerShell 测试要求 `pwsh` 在 PATH；缺少时会跳过，不能将跳过记为通过。
