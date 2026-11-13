# 虚拟键盘 M1/M2 重建验证

日期：2026-11-13。本轮从公开远端 `9e626da290d7299a03bbc31976de4e111d50b003` 重新克隆；此前云端 M1/M2 工作区不可用。本轮实现、测试和提交重新执行，不把历史测试成绩作为本次结果。

## 交付范围

- 可选窗口级 Scope、三种显示策略、焦点关联、同会话关闭抑制、显式 Target 接管、无效目标和生命周期清理、重入与触摸来源保护。
- Text/Integer/Decimal/Email/Url/Phone 用途与便利布局；原有显式 Target 不变。无格式强制转换、输入历史、系统级注入或中文引擎。
- 44×44 DIP 默认触点、现有动态尺寸/间距/字号资源、窄窗横向滚动、显式高对比资源覆盖。
- 自动示例、金额业务校验与有限 BringIntoView 协作；不预览密码。
- 密码/模态窗口回归与无需 PowerShell 的独立 NuGet 消费脚本。

## 验证结果

环境：Debian 13 linux-x64，官方 SDK 10.0.402，运行时10.0.13，Avalonia12.0.3；独立可写CLI/NuGet目录，单节点MSBuild。

| 检查 | 本轮结果 |
| --- | --- |
| 独立远端基线 Core / Headless / Themes | 25 / 52 / 11 全通过 |
| 重建 Core（含正常API快照校验） | 32通过 |
| 重建 Headless | 77通过，0失败/跳过 |
| 重建 Themes | 19通过 |
| Usage Release build | 成功，0警告/错误 |
| 统一 Demo Release build | 成功，0错误；Charts.Skia既有AVA3001警告1项 |
| 新鲜NuGet FunctionOnly / DefaultTheme | 两模式还原、编译、运行成功，0警告/错误 |

主题测试覆盖320/400/560/760/960 DIP宽度及末端按键可达性，五种布局400×700下有限滚动避让、标签测量与高对比启用/移除。包消费验证实际包内AXAML、原生输入/撤销/绑定、Scope、用途和高对比资源，不把pack成功代替消费运行。Python语法、Git空白检查通过。

## 可复现入口

```sh
for name in Core Headless Themes; do
  dotnet test tests/VirtualKeyboard.$name.Tests -c Release -m:1
done
dotnet build samples/VirtualKeyboard.Usage -c Release -m:1
dotnet build samples/NT.AvaloniaControl.Demo -c Release -m:1
python3 scripts/test-virtualkeyboard-package-consumer.py
```

## 范围与未验收项

Headless 合成触摸、鼠标、模态窗口和布局测量不是 Windows/Linux 真机触屏、DPI/多屏、屏幕阅读器、原生窗口管理器或系统 IME 验收。系统组合期间并用虚拟键盘继续排除。极矮窗口仍需宿主调整布局。没有 pwsh，不执行全仓库 Verify.ps1 与原生性能脚本。M4真实设备/发布门槛未完成，不发布 NuGet、不推送远端。

## 恢复策略

远端基线首先生成完整 Git bundle 并校验，保存到 Library；重建中间源码另存标为 WIP 的快照。交付提交和日志提交后再生成完整 bundle，校验并用独立 clone 验证 HEAD，保存 Library 后才报告持久备份完成。旧日志中的 SHA 保留历史信息，不伪造其对象仍存在。

收到交付 bundle 后可恢复到新目录，不覆盖现有工作区：

```sh
git clone NT.Avalonia.Control-M1-M2-rebuilt.bundle NT.Avalonia.Control-restored
cd NT.Avalonia.Control-restored
git log -4 --oneline
git status --short
```

bundle 包含 Git 历史、源码、测试、开发文档及离线日志，不包含 SDK、NuGet 缓存或运行产物。恢复后按 global.json 安装官方 SDK，再运行上面的测试入口；不需要向远端推送。
