# GROWL AGAIN

Unity 6.2（6000.2.9f1）2D 项目，目标 Windows。活动仓库 `F:/2026GameJamUnity`，总控分支 Ming；已发布基线 `649451f`。当前还有未提交的 Level/Soap 选择性集成，不能把旧 main 当作最新基线。

先读 [AGENTS.md](AGENTS.md)、[总控交接](docs/handoffs/controller.handoff)、[WORK_PACKAGES](docs/WORK_PACKAGES.md)。玩法阅读版见 [GAME_DESIGN](docs/GAME_DESIGN.md)。用户下一轮将提供新规则；本轮暂不把可领取宝箱放入地图，不提前实施未知规则。

## 当前地图入口

打开 `Assets/WhiteBox/Scenes/Level_Whitebox.unity` 后 Play。A/D或方向键移动，Space基础跳跃，左右Shift冲刺，E交互，R回出生点，Escape测试暂停。当前用户配置二段跳关闭，可在 `02 Actors/Test_Player/WhiteboxPlayer2D` 调整白板开关。相机仍为玩家子物体。

这是试走场景：免费传送、按钮反复切换门、地刺击退、R只回位。HUD显示真实PlayerState的HP、四个构筑槽和姿态；试走能力不写构筑槽。7处宝箱仅为原地图标记。Portal_B交互区已下移到站立可用高度；地形、门2×6/6×2、地刺图块3×1/4×1比例保留。

Hierarchy：00 Runtime、01 World（地形/光照/传送/门/开关/危险）、02 Actors、03 UI、90 Validation；WwiseGlobal由插件维护，保留根节点。8扇门复用Soap WorldDoor，白板适配器只转发反复开关。T06基础运动保留独测，地图只有WhiteboxPlayer2D一套刚体运动组件。

`90 Validation/Level Whitebox Play Checks` 默认autoVerify=false，自动检查会进入Dead；退出/重进Play恢复试走。该历史全能力回归要求在Play临时打开二段跳，不需要保存改动。所有开发测试场景均未加入Build Settings，SampleScene仍是模板占位；本轮未导出Windows。

## 验证入口

| 场景（Assets/Scenes/Tests 下） | 用途 |
|---|---|
| C01/C01_Smoke.unity | 输入缓冲、运行阶段与启动清理 |
| C02/C02_Smoke.unity | 真实HP、构筑槽、姿态与死亡 |
| C03/C03_Smoke.unity | 交互排序、去重和选择事务 |
| T01/T01_Smoke.unity | 菜单生命周期、三项/四项显示 |
| Integration/T01_C03.unity | 真实运行组件与T01菜单接线 |
| T02/T02_Smoke.unity | HUD真实事件刷新、启停与换源 |
| T03/T03_Smoke.unity | 正式一次性开关门与输入/阶段锁 |
| T06/T06_Smoke.unity | 基础单跳、射线落地、碰墙与调参 |
| T12/T12_Smoke.unity | 宝箱领取/取消、满槽替换、已拥有过滤与缓存修复 |

T02自动检查，其余Soap入口用场景中的Run Checks。模拟键鼠验证前将Game视图置于焦点；最终结果及例外见总控交接。T12有隔离测试替身和仅供测试的回血配置，不能整套复制进地图或当最终奖励池。

## 目录与清理边界

正式接口/音频入口在 Scripts/Core、Scripts/Audio/Core；唯一状态与输入在 Scripts/Runtime；Soap模块在 Scripts/Gameplay 与 Scripts/UI；白板资源/适配在 WhiteBox；验证脚本、场景、Prefab、Editor工具按Tests保留。

本轮删除空Tested节点和白板门重复状态代码；未导入Soap的恢复场景和Wwise漂移。已有独测有独立覆盖价值，保留其源码、配置、Prefab及meta。Wwise工程、插件、URP/TMP配置及使用中的缓存不作为无用内容删除。没有自动commit/push；发布时需包含未跟踪的新资产及对应meta。