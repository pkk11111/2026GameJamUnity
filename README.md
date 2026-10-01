# pawgatory

Unity 6.2（6000.2.9f1）2D，目标Windows。活动仓库 `F:/2026GameJamUnity`，分支Ming；已提交基点 `0abac92`。本轮工作区为规范13/源码契约7，**未提交/未发布**。

先读 [AGENTS.md](AGENTS.md)、[总控交接](docs/handoffs/controller.handoff)、[WORK_PACKAGES](docs/WORK_PACKAGES.md)。新版 [pawgatory_gameplay_design_v5(1).md](<docs/pawgatory_gameplay_design_v5(1).md>) 已取代旧GAME_DESIGN，只提供玩法/内容；Codex并行、代码、交接与音频要求以本地AGENTS为准。

## 当前测试入口

打开 `Assets/WhiteBox/Scenes/Level_Whitebox.unity`，Play后可测试7个宝箱、三选一领取、重复拥有过滤、即时增益、三槽HUD和替换事务。01 World/Chests下为Chest_01–07；第一个箱中心(32.5,-11.5)，从左侧约(30,-12.2)可按E。

A/D或方向键移动、Space基础跳、E交互；有腿后可二段跳，有普通尾后Shift冲刺。卡片可鼠标选择或键盘导航/Enter确认，Esc或Cancel取消。R只回出生点，Esc在正常游玩时测试暂停。

地图目前跳过头部教学，以满血躯干测试状态开始。免费传送、反复按钮开门、地刺仅击退仍是试走；实际攻击、身体美术、正式收费、两支线、整局胜负/重开待下一轮。两条支线地图由地图迭代同学制作；入口条件门方案见新版规则。

原速度7/跳高4.5、地面、门2×6／6×2、地刺3×1／4×1及Portal_B交互锚点保持。运动仍只有WhiteboxPlayer2D；其useLoadoutAbilities=true读取真实状态，关闭后才恢复旧Inspector试走技能开关。

## 奖励配置和替换验证

`Assets/Configs/Chest/V5_WhiteboxRewards.asset`含腿、手剑、普通尾、回血、上限、攻击；暂定回血25、上限+15不附带回血、攻击+3。Chest_03/05使用`V5_RegrowthRewards.asset`优先曾拥有且当前缺失的身体部件。Inspector修改配置，不在运行时改共享资产存局内数据。

只有三种基础部件时，满三槽不会自然再抽到第四种不同保留项。替换已用独立测试及Play临时独立技能夹具验证；未实现喷火没有加入地图奖励池。

Play后菜单 **Tools/pawgatory/T12/Run V5 Checks (Play)** 可运行33项新回归。它只创建/销毁临时测试状态，不修改地图配置。地图7箱的打开/取消、真实E打开、鼠标领取、Esc取消及键盘替换已另行验证，场景保存后退出Play保持全箱未领取。

## 历史测试与剩余工作

C01/C02/C03、T01/T02/T03/T06/T12、Integration/Level独测资源继续保留。旧C02/T02/T12里的四槽/站立独立剑断言和旧奖励池属于迁移前版本，后续迁移后再作新规则验收；当前T12使用上述V5入口。90 Validation的旧Level全能力检查默认关闭，不能直接作为本轮三槽回归。

Hierarchy保留00 Runtime、01 World、02 Actors、03 UI、90 Validation和插件WwiseGlobal根；新增箱子按原标记中心放置，仅隐藏对应宝箱Tile，不删其他标记/地形。测试驱动不进入正式Build Settings。

WwiseSettings.xml和Authoring工程保持用户当前Ming版本，未导入Soap的SoundBank路径变更。音频接口和Cue要求不变；Bank播放、完整整局及Windows导出尚未验收。
