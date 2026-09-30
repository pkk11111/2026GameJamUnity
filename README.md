# GROWL AGAIN

Unity 2D Game Jam，主题 Regrowth。必要文件已迁入 Unity 6.2 新项目，当前正在冻结玩法并建设运行底层。

当前总控工作与已发布基线来源为 Ming；不要默认 main 已包含最新规范/代码。新会话工作目录为 F:/2026GameJamUnity，先读 AGENTS.md 和总控交接，再核对 Git 与 Unity MCP 的实际目标。

## Development guidelines

- [统一玩法、技术栈、接口与 Codex 协作规范](AGENTS.md)
- [完整游戏设计与待确认问题](docs/GAME_DESIGN.md)
- [最小可测试功能任务与领取顺序](docs/WORK_PACKAGES.md)
- [总控新会话入口、当前状态与下一步](docs/handoffs/controller.handoff)

所有规则集中于 AGENTS.md；handoff 只记录实际工作。现有基础代码不等于游戏功能已经完成。

## Unity 验证入口与目录用途

使用Unity6000.2.9f1；以下场景直接在编辑器打开，未加入正式Build Settings。

| 场景 | 用途 |
|---|---|
| Assets/Scenes/Tests/C01/C01_Smoke.unity | 集中输入、运行阶段与启动清理 |
| Assets/Scenes/Tests/C02/C02_Smoke.unity | 真实生命、构筑、姿态、死亡停止 |
| Assets/Scenes/Tests/C03/C03_Smoke.unity | 交互排序及选择事务，整理后57项 |
| Assets/Scenes/Tests/T01/T01_Smoke.unity | Soap菜单独测，本机41项 |
| Assets/Scenes/Tests/Integration/T01_C03.unity | Soap真实菜单与总控接线，31项 |

集成场景的T01 C03 Integration Checks.autoVerify=true时运行检查并进入Dead；关闭后可用E或TEST Choice手动打开菜单。各场景的自动检查是验收入口，不是整局重开。

C03与集成场景的Hierarchy分为Runtime、Test World、Test UI、Test Checks、Environment，WwiseGlobal由插件管理。测试代码集中Assets/Scripts/Tests，正式菜单在Assets/Scripts/UI/Choice及Assets/Prefabs/Choice；主地图不携带验收驱动。

SampleScene仍被项目构建与模板配置引用，保留为占位场景；正式地图由组员独立场景交付。URP/Wwise/TMP配置和旧验收场景保留，根目录无用的一次性logRunSetup.txt已清理。美术素材和外观资源本轮不调整，由组员后续适配。

当前Soap T01合并到Ming的候选已通过上述菜单/接线验证，但尚未提交或推送；详情见总控交接。
