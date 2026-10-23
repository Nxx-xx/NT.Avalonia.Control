# 当前进度与下一步

更新日期：2026-10-23。当前版本：`0.2.0-preview.2`。

## 当前进度

五个控件均提供独立功能包、默认主题、统一 Demo 和 Usage 示例。构建、测试、打包及独立包消费脚本已建立。

| 控件 | 当前进度 | 下一步 |
| --- | --- | --- |
| TreeDataGrid | 平面与树数据、MVVM、编辑校验、排序过滤、虚拟化、列管理及受控行移动已实现 | 异步加载产品化，补复制与布局持久化，并完成真实输入和无障碍验收 |
| VirtualKeyboard | 数字与英文输入、原生编辑、长按重复和生命周期管理已实现 | 中文输入原型、自动焦点与显隐，以及触屏和 IME 验收 |
| Markdown | 原生只读阅读、全文选择复制、流式更新、图片扩展和 TextMate 高亮已实现 | 目标设备、读屏、DPI 与长期运行验证，完善复杂内容组合 |
| Charts | 常用图族、C4 性能引擎、C5/C6 基础图族及七类统计补充已实现 | C7-01 全功能集成，再推进实机、性能和发布验收 |
| PdfViewer | 完整阅读功能及 P3-01～P3-03 的大文件读取、缓存预算和渲染质量分层已实现 | P3-04 长文档优化、P3-05 性能指标验收，再推进保存与编辑 |

## 下一步

1. **Charts**：推进 C7-01，统一图族的主题、模板、选择、交互与生命周期组合；处理 LOD 原生崩溃和地图暗色标签等集成问题，再开展性能与平台验收。
2. **PdfViewer**：继续 P3-04 长文档优化与 P3-05 同画质性能验收，随后推进保存、书签持久化和编辑能力。
3. **TreeDataGrid**：完成异步 loader 的取消、生命周期、加载与错误反馈，接入远端数据示例。
4. **VirtualKeyboard**：完成中文输入引擎原型，明确预编辑、候选、提交取消及系统 IME 互斥；推进自动显隐与焦点管理。
5. **Markdown**：补目标设备、真实读屏、DPI 和长期运行验证，完善自定义呈现器、图片及超长内容的组合场景。
6. **发布准备**：运行当前源码的完整 CI 与包消费检查，补齐 Windows/Linux 实机、输入设备、长期稳定性和支持矩阵。

## 相关计划

- [Charts 开发计划](charts-development-plan.md)
- [Charts 功能覆盖矩阵](charts-official-alignment.md)
- [Charts 性能与平台计划](charts-performance-platform-plan.md)
- [PdfViewer 开发计划](pdfviewer-development-plan.md)
- [TreeDataGrid 后续计划](treedatagrid-next-development-plan.md)
- [VirtualKeyboard 开发计划](virtualkeyboard-development-plan.md)
- [Markdown 执行计划](markdown-next-execution-plan-20261015.md)
