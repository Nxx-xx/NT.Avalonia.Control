# C7-03 记录分配、柱形缓存与同协议复测

更新日期：2026-10-27。

C7-03 本轮定位公开 DrawingGroup 记录分配，拒绝3个不保持语义/画质的输入几何替换；已按明确审核决定接纳柱形源替换/布局优化，完成双平台各107进程同协议正式复测。按原协议预热后，柱形源替换请求至受控栅格完成的P95稳定降低，全体请求P95表现混合，不能扩展为所有请求或图族统一提速。原 Render、画质、协议和门槛保持，C7-03 不结项。见[记录分配与柱形复测](charts-c7-record-allocations.md)和[验证记录](charts-c7-record-allocations-validation.json)。

本报告记录当前桌面软件证据；柱形源替换子集收益、全体请求P95及完整C7资格分别判断。B17与运行时/Picture原记录保持历史身份，不改写数值。

## 定位与被拒绝方案

Windows B12冻结 S10-S-exact 的分配诊断定位属性存储、通知和SDK几何/绘图包装路径；它不是后来柱形候选的测量。采样ticks不是对象数，区间权重不是精确分配、保留/native/GPU内存，也不代表SDK必要最小成本或预计可省比例。
measured Record ticks=14692，近似权重=1571755368 bytes；时钟资格=empirically-qualified-for-this-Windows-capture。
本次10MHz QPC/UTC只经该捕获经验锚点核验，残差约+1.793/−0.975/−0.596ms；读取未括定、不拟合偏移，不宣称严格亚毫秒或通用对齐资格。

| 几何候选 | 实查case / 首失败index | native结构 | Bounds | 像素 |
| --- | --- | --- | --- | --- |
| rect-stream | 2 / 1 | 通过 | 未通过 | 通过 |
| circle-stream-double | 1 / 0 | 未通过 | 未通过 | 未通过 |
| circle-stream-floatbounds | 1 / 0 | 未通过 | 未通过 | 未通过 |

每项计划320例，实际首失败即停并跳过候选性能。矩形Stream丢失原double Bounds；两种4-Arc圆形出现结构及像素差异。未放宽门槛、未接入这些替换。

## 实现与验证

BarHistory使用精确Rect位键和单所有者共享；LRU容量按活跃期满足共享条件的最大条数两倍预留，上限16384项，只增不减，退役归零；小场景保持local slots，不污染dense历史。构造时完整预留shared与local权重，拒绝/驱逐/Trim/detach/多owner及跨图族均退役；旧帧和空旧slot不能复活历史。
仅参与预算准入流程的retained入口申请共享；普通direct/visible入口、导出绕过路径和零预算路径均不申请。SDK每次仍创建独立可变输出包装，活brush、原RectangleGeometry、普通native DrawRectangle和marker顺序保持。Layout全有效时直接填最终Entry[]，过滤才resize，ordinal/shape/命中身份不变。

| 平台 | 全量 executed / passed / failed | 专项图对 / 编码一致 / 像素一致 |
| --- | --- | --- |
| windows | 3267 / 3267 / 0 | 232 / 232 / 232 |
| linux | 3267 / 3267 / 0 | 232 / 232 / 232 |

v1 Windows保留3263通过/2失败：custom Projection不裁剪bar，以及把已归属旧chart的series转挂detached chart。v2分别改为100点small source、新建detached series；增加dense markers双分支和Bar→Scatter→Bar退役/旧帧回归。v1失败及Linux配对旧路径解析失败均保留，不重跑.NET掩盖。

## 同host、同质量柱形配对

每端AB/BA/AB三对新进程，2×5000原数据、10s预热/30s测量、60Hz、15s排空；完整原oracle及100+1000命中/8ms、JSONL=raw、身份和同状态图片另行审计。以下只列sourceReplace成功子集的对内P95，不跨进程合并。请求指标为requestToControlledRasterMs，不含队列等待、后续命中或真实present。

| 平台 / 对 | source样本 before / after | 请求P95 before / after ms | 请求变化% | record分配变化% |
| --- | --- | --- | --- | --- |
| windows / 1 | 180 / 180 | 47.4923 / 35.2086 | -25.864614 | -46.123563 |
| windows / 2 | 180 / 180 | 47.8106 / 35.6038 | -25.531577 | -46.122948 |
| windows / 3 | 180 / 180 | 48.598 / 35.6752 | -26.591218 | -46.123482 |
| linux / 1 | 180 / 180 | 51.323811 / 38.227285 | -25.517446 | -46.1237 |
| linux / 2 | 180 / 180 | 50.562482 / 39.124787 | -22.620913 | -46.12425 |
| linux / 3 | 180 / 180 | 51.78865 / 38.372819 | -25.904964 | -46.1237 |

全体measured请求P95表现混合，完整allMeasured原值保存在验证JSON；不能把源替换子集分配下降比例等同全链路或所有轨道加速。保留全部慢轮/失败/pending，不因CPU位置或净值筛掉轮次。

## 完整正式复测

正式表与历史B8活动比较，B17另保留；稳定源替换净益来自上面的同harness配对，不从跨批次formal P95单独推导因果。

| 平台 | 进程 | 正式首次可用 / 校准排除 | 首次可用软件P95 ms | 完整性 / 命中 / 负载 |
| --- | --- | --- | --- | --- |
| windows | 107 | 100 / 2 | 1175.4179 | 通过 / 通过 / 未通过 |
| linux | 107 | 100 / 2 | 1307.015489 | 通过 / 通过 / 未通过 |

| 平台 / 轨道 | 测量轮通过/5 | planned / completed / 排空后pending | 计划截止pending |
| --- | --- | --- | --- |
| windows / S10-L-exact | 5/5 | 9000 / 9000 / 0 | 0 |
| windows / S10-S-exact | 4/5 | 9000 / 8839 / 161 | 3013 |
| windows / S10-B-exact | 5/5 | 9000 / 9000 / 0 | 0 |
| windows / S100-L-minmax | 5/5 | 9000 / 9000 / 0 | 0 |
| linux / S10-L-exact | 5/5 | 9000 / 9000 / 0 | 0 |
| linux / S10-S-exact | 0/5 | 9000 / 7960 / 1040 | 3552 |
| linux / S10-B-exact | 5/5 | 9000 / 9000 / 0 | 47 |
| linux / S100-L-minmax | 5/5 | 9000 / 9000 / 0 | 0 |

| 平台 | formal图比较 | 同状态/首图 | 其一致门禁 | 不同末态 |
| --- | --- | --- | --- | --- |
| windows | 142 | 140 | 通过 | 2 |
| linux | 142 | 140 | 通过 | 2 |

全局完整性=通过，负载=未通过，命中=通过，可比图=通过，受控活动门禁=未通过。完整证据与全部门槛通过分开报告。

## 剩余问题与资格边界

未通过轨道、尾部波动及公开DrawingGroup逐点记录开销按上表继续处理；本轮不把scatter视为该bar候选已解决。百万点首次可用为3000ms名义软件诊断，非真实render/present资格。
官方无许可保持blocked-reference，测量/倍率=null；真实present、匹配峰值内存未取得。Ubuntu22.04/24.04实机、硬件GPU、物理触控、百万点命中尾延迟及UIA/AT-SPI原生选中项桥接按用户决定后置。C7 milestoneComplete=false。
并行原型/Picture未接入，原调用与画质门槛保持；远程CI、长稳、包装消费者、Demo/Usage等资格只采用各自实际批次证据，不因本次测试或formal自动补记。

机器结果、源/脚本指纹、所有失败与外部原始证据索引见[验证JSON](charts-c7-record-allocations-validation.json)。历史[B17](charts-c7-scatter-followup.md)及[运行时/Picture](charts-c7-runtime-picture.md)记录保持原数值和候选身份。
