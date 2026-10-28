# 图表原生采样解析

`charts_c7_native_profile.py` 是 C7-03 的纯 Python 诊断工具。它不启动 .NET、perf 或测试宿主，只解析已保存的 Linux 原生采样和时间窗口；这些结果不进入正式性能门禁。

先由独立采集流程保存以下输入：

- perf 叶样本导出：`perf script -G --ns -F tid,time,ip,sym,dso,period`。已验证的固定行格式是 `TID TIME: PERIOD IP SYMBOL (DSO)`；包含调用链的非 `-G` 导出不能直接作为输入。
- `windows.jsonl`：`charts-c703-native-circle-window/v1`，使用 Linux `CLOCK_MONOTONIC` 纳秒和原生 OS TID。
- `native-circle-probe.json`：终态 probe 报告，支持 `charts-c703-native-circle-probe/v1`、`/v2` 和 `/v3`。
- v3 还必须提供同目录的 `coordinate-comparison.json`，其指纹与报告一致；v1/v2 明确不具备生产坐标资格。
- 可选的失采证据文本：原始 perf record dump、统计或日志；同时保留各采集命令、退出码和原始文件。

```text
python scripts/charts_c7_native_profile.py --leaf <leaf.txt> --windows <run/windows.jsonl> --probe-report <run/native-circle-probe.json> --case paths-n10000-clip-plot --loss-file <loss-evidence.txt> --output <新的分析目录>
```

`--case` 必须与报告完全一致；v2 示例为 `paths-n10000-clip-plot-init-true`。脚本核对报告和窗口的 PID、完整启动时间、时钟、TID、case、完成批数及窗口不重叠条件。Windows QPC 窗口不适用于此 Linux 解析器。

v3 坐标校验要求报告和 sidecar 的 `passed=true`、点数相符、零差异、两套中心哈希相同。解析器核验 sidecar 的实际字节数/SHA，并按连续原始序号重建每个 X/Y 的 IEEE754 小端字节，重新计算 linked、production 和 source 哈希，逐位核对全部中心及 `equal` 标志；不只信任报告布尔值。linked 实现源码指纹仍需证据采集流程核对，坐标通过也不能代替产品画质或性能验收。

v3 的 `productionPathStructureQualification` 原样单列。坐标逐位一致不等于局部诊断路径结构与生产 native operation 相同；该字段失败不会被坐标结果覆盖，也不会被当作生产路径已通过。

主结果严格限定在指定 case 的 `measurement` + `draw` 半开时间窗口 `[start,end)`。测量期 Clear、warmup Draw/Clear、窗口外、其他线程和其他 case 分别保留。输出包含：

- `analysis.json`：输入和解析器指纹、窗口/报告核对结果、各类 leaf count、原始 period 分布、函数/未知 IP 聚合、源 probe 正确性和 native identity。
- `classified-leaf-samples.jsonl`：所有已解析样本，保留纳秒、TID、IP、完整函数名和 DSO，包括 `(deleted)`。
- `unparsed-lines.jsonl`：未能识别的行，不能静默丢弃。

未知符号仍计入样本总数，并按 IP 保留。源码 probe 的失败、像素拒绝和显式初始化的验证结果原样记录；解析成功不代表候选实现被接受。实际加载库的映射、SHA/Build-ID、符号来源和采集完整性仍需独立核验。

`LOST events`、`LOST_SAMPLES events` 及不同统计区段的值分别保留，不能把事件记录数、缺失样本数或重复导出的计数相加。没有失采文本不等于零失采。leaf count 和 period 分布均不能换算为精确 CPU 时间或 CPU 百分比，也不代表纯 P/Invoke、GPU 或窗口呈现耗时；此工具不计算 inclusive 调用链占比。

输出目录必须不存在。退出码 `0` 表示解析核对通过且有主窗口样本；`1` 表示已保存结果，但有未解析行或缺少主窗口样本；`2` 表示输入缺失/矛盾或输出路径冲突。`parsingQualified` 只说明解析条件成立，不能代替失采、符号质量、像素、性能或产品验收。

运行合成测试无需 .NET 或 perf：

```text
python scripts/test_charts_c7_native_profile.py
```

12 项测试覆盖真实导出格式、函数名和 DSO 括号、deleted 映射、未知符号、半开区间、矛盾/重叠窗口、失采统计、未解析行及 v1/v2 报告；v3 另含成功的完整坐标比较和点数、位值、序号、标志、哈希、sidecar 指纹等不一致拒绝用例。测试使用 `scripts` 下自动清理的临时目录，不读取真实 profile。
