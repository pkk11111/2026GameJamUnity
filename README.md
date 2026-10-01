# GROWL AGAIN

Unity 2D Game Jam，主题 Regrowth。必要文件已迁入 Unity 6.2 新项目，当前正在冻结玩法并建设运行底层。

当前总控工作与最新已发布基线为 Ming/e7ef271（发布/测试资源文档整理，公共契约6；业务/资产仍为349ba93）；2026-10-01再次查询GitHub确认已推送。不要默认 main 已包含最新规范/代码。新会话工作目录为 F:/2026GameJamUnity，先读 AGENTS.md 和总控交接，再核对 Git 与 Unity MCP 的实际目标。

## Development guidelines

- [统一玩法、技术栈、接口与 Codex 协作规范](AGENTS.md)
- [完整游戏设计与待确认问题](docs/GAME_DESIGN.md)
- [最小可测试功能任务与领取顺序](docs/WORK_PACKAGES.md)
- [总控新会话入口、当前状态与下一步](docs/handoffs/controller.handoff)

所有规则集中于 AGENTS.md；handoff 只记录实际工作。现有基础代码不等于游戏功能已经完成。

## Unity 验证入口与目录用途

使用Unity6000.2.9f1；以下场景直接在编辑器打开，未加入正式Build Settings。

| 场景 | 检查用途与保留原因 | 此前实际通过项数 |
|---|---|---|
| Assets/Scenes/Tests/C01/C01_Smoke.unity | 全套输入采样/缓冲/消费、运行阶段与启动清理；集成场景不覆盖全部按键 | 32 |
| Assets/Scenes/Tests/C02/C02_Smoke.unity | 真实生命、四槽容器、姿态、写口及死亡停止；集成场景仅做部分状态写入 | 51 |
| Assets/Scenes/Tests/C03/C03_Smoke.unity | 交互类别/距离/稳定ID排序、去重与选择事务；多个测试目标不能被单目标集成替代 | 57 |
| Assets/Scenes/Tests/T01/T01_Smoke.unity | Soap菜单与卡片回调的独立验收，不依赖总控事务 | 41 |
| Assets/Scenes/Tests/Integration/T01_C03.unity | 真实输入→交互→选择事务→Soap菜单→PlayerState接线；手动体验优先打开此入口 | 31 |

集成场景的T01 C03 Integration Checks.autoVerify=true时运行检查并进入Dead；关闭后可用E或TEST Choice手动打开菜单。各场景的自动检查是验收入口，不是整局重开。

C03与集成场景的Hierarchy分为Runtime、Test World、Test UI、Test Checks、Environment，WwiseGlobal由插件管理。测试代码集中Assets/Scripts/Tests，正式菜单在Assets/Scripts/UI/Choice及Assets/Prefabs/Choice；主地图不携带验收驱动。

SampleScene仍被项目构建与模板配置引用，保留为占位场景；正式地图由组员独立场景交付。URP/Wwise/TMP配置和旧验收场景保留，根目录无用的一次性logRunSetup.txt已清理。美术素材和外观资源本轮不调整，由组员后续适配。

Soap T01与总控集成/整理已由用户提交并推送到Ming/349ba93；详情见总控交接。上述项数为此前Unity实际验证，本次只做Git/源码/序列化引用检查，Unity MCP不可连接，未重跑Play。

## 项目内容与清理结论

| 位置 | 内容与维护用途 |
|---|---|
| Assets/Scripts/Core、Audio/Core | 已发布的公共契约与音频入口 |
| Assets/Scripts/Runtime | 总控输入、唯一运行阶段、玩家状态、交互、选择与启动清理 |
| Assets/Scripts/UI/Choice、Assets/Prefabs/Choice | Soap正式选择菜单及卡片 |
| Assets/Scripts/Tests、Assets/Scenes/Tests、Assets/Prefabs/Tests | 开发回归验收；保留，正式地图不挂驱动 |
| docs/handoffs | 各成员接线与验证事实；总控维护controller.handoff，其他人的交接只读 |
| Assets/Wwise、2026GameJamUnity_WwiseProject、根Wwise源码zip | 已有插件/音频交付；程序清理不改、不删 |
| Library、Temp、Logs、UserSettings、.vs、根csproj/slnx | 本机生成且已忽略，目录里可见不代表进入Git；不在Unity使用时删除缓存 |

本次核对5个测试场景、9个测试脚本，没有可退役的旧测试。C03ProbeTarget同时被C03及Integration场景引用，Integration程序集依赖Tests.C03；ChoiceMenuPlayChecks由驱动调用；T01SceneSetup虽不挂场景，却提供编辑器创建/重载入口。不能只看“无场景引用”或编号较早就删除脚本。未来有替代覆盖后，成组检查场景/Prefab/代码/asmdef/meta再退役。

736个Assets meta无重复GUID、孤立meta或普通已跟踪文件缺meta；Wwise Mac.bundle内文件属于插件载荷。没有已跟踪缓存、临时或备份文件；旧一次性logRunSetup.txt已在前次发布删除。此次不移动/删除测试资源，也不调整美术素材。

新对话从本仓库AGENTS.md、docs/handoffs/controller.handoff及docs/WORK_PACKAGES.md接手。Unity MCP复查已恢复项目/编辑器/活动场景查询；当前T01_C03非Play且已保存。一项深层execute_code读取超时，未重跑测试；接手时仍须核对实际Git与Unity状态。
