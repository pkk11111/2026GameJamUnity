# pawgatory 工作包：当前结构、迁移顺序与验收

> 最新 C09（2026-10-01）：从 Soap/60f9a55 选择性接入敌人AI与100/10配置，保留 Ming/a78185f 的收费、火球和人物实现。主图13只普通怪+1只精英；最底层大红块按用户要求拆为3只独立普通怪。攻击奖励/代价改为固定+10/-10点，最低6，取消共享攻击倍率；生命比例规则按Soap原文保留。C09 Play 59/0，Unity编译与保存重载通过；用户本轮明确授权commit/push Ming。以下C08百分比及旧接线记录仅为历史，不覆盖本节。


## C09 接线与接口（2026-10-01）

本轮从干净Ming/a78185f开始，已取得Soap/60f9a55；只接敌人AI/config/prefab及原作者handoff，不整份覆盖主场景/PlayerState/战斗文档。用户授权主图AI、底层拆3只、采用Soap数值与完成后push。

- `WhiteboxPlayer2D`实现`IPlayerBaseMoveSpeedProvider`，`BaseMoveSpeed`直接只读唯一moveSpeed；主图7，普通AI5，精英AI7。没有测试反射provider进入主图。
- `Enemies_C09`含Enemy_01–13与Elite_Exit；Enemy_01–03是底层原大块。普通按平台大小1或1.5单位正方形，精英3单位；实体与可见方块同尺寸。原4个TrialEnemy替换，12组Tile_Enemy替换为无Sprite/无碰撞的Tile_EnemySpawnMarker，保留原格坐标记录；地形、玩家尺寸与运动参数不变。窄/悬空标记落到附近平整平台，绝不把±6直接跨过悬崖。
- `EnemyRoutes_C09`保存每只怪独立Left/Right世界边界，配置在敌人AI Inspector；不是移动角色的子节点。普通配置100HP/10/5索敌/1.5巡逻返回/5-7速度比；独立EnemyEliteConfig为250/20/7索敌/1速度比。三态不回血，受击无额外无敌，死亡停刚体并关闭视觉/碰撞。暂停/Choosing保留仇恨，巡逻遇墙/同伴反向，避免相互永久顶住。
- 每只EnemyBasic绑定唯一RunController、玩家和EnemyEnhancementService；全图增血+50/增攻+5含精英，未来注册仍继承。本轮没有出口胜负接线。
- `PlayerReward`恢复仅`AttackIncrease`固定整数，移除`AttackPercentIncrease`；`PlayerState`移除`AttackPercent`，Bite/Sword/Fire直接返回真实整数。奖励先预验三个数值溢出；减攻模拟全部三种攻击，任何结果<6拒绝，世界回调失败不扣费、不Clamp。`CanPayTeleportCost`/`TryCommitTeleportCost`的第三参数语义改为minimumAttackDamage。
- `ChestRewardConfig.AttackPointIncrease=10`、`PortalCostConfig.MinimumAttackDamage=6`，主图资产明确保存；旧序列化字段加FormerlySerializedAs，实际资产已迁移。卡片展示实际伤害点；生命20%/10%公式保留。C08检查仅适配新契约，不继承旧77/0为此次通过。
- `Tools/pawgatory/C09/Install Main Enemies (Edit)`是本次一次性作者工具，已安装时拒绝重建；后续直接Inspector调整。`Check Main Enemies (Play)`为临时DontSave短检查，使用真实状态、物理、宝箱卡片和服务，结束Stop还原；不进入正式运行组件或Windows构建。

验证结果：目标Unity6000.2.9f1编译通过；C09短Play 59/0，覆盖14只AI配置/实际巡逻/边界/平台、追击/暂停/Choosing/返回/不回血/死亡、全图含精英强化、真实宝箱固定+10、减10整笔校验/失败无副作用/溢出拒绝及生命公式。首轮55/4因测试夹具停用motor连带停用真实速度provider，改用冻结测试玩家刚体后59/0；生产逻辑未为此放宽依赖校验。保存重载发现Unity刷新会恢复Tile色，改独立无Sprite出生标记后再次核对。Console有两条既有Wwise Main Camera注销对象错误，未修改音频，未声称Console全零。未做整局、Windows导出或音频验收；近战时间轴/共享受伤保护（当前敌人仍独立0.6秒）不冒充本轮已完成。

> 历史 C08（2026-10-01）：四扇普通门已接 E → 传送代价三选一 → 物理步支付并传送，每次使用收费；InspectionDoor1/2保持原条件入口。与宝箱共用抽取器和卡片界面，各用独立卡池/缓存。修复火球移动转向边界，确认人物咬剑动画并移除主图青/金测试方块。C08 77/0、C07复跑30/0、三种百分比奖励实测通过；Unity编译无错误，未导出Windows。规范23、源码契约10；Ming/724c08c与Dada MERGE_HEAD/575eff3未变，无自动commit/push。

## C08（2026-10-01）普通门收费、朝向与攻击表现

用户确认另一对话已结束后串行实施，保留既有未提交的Dada合并、地图和资源。本次收费授权覆盖此前“收费暂缓”；完整教学、近战矩形/时间轴、AI、地刺扣血、支线结算及整局重开仍不在本次范围。

主场景四门Portal_A/B/C/D显式绑定同一PlayerState、RunController、ChoiceCoordinator、EnemyEnhancementService和MainPortalCosts配置。每次E打开三张代价；取消保持卡位且不扣费，已失去部件只局部补位，低生命/倍率下限卡禁用，全灰只换一张有效非生命代价。选择后恢复运行，在下个FixedUpdate再次校验落点和全部费用，整笔提交费用、迁移、全图强化并给予0.5秒抵达保护；失败无扣费、无移动，成功清本门缓存，下次重新抽且再次收费。InspectionDoor1/2未改。Portal_B仍使用原地面interactionPoint。

宝箱与门复用FixedChoiceDeck和ChoicePanel，各入口保留独立候选。主图两份宝箱配置启用combat百分比：回血ceil(maxHP×20%)、加上限ceil(maxHP×10%)且当前同额增加、攻击+10个百分点。共享攻击初始100%、代价-10个百分点、最低60%；咬/剑/火读同一倍率向上取整，主图基础剑伤同步20。既有独测固定值配置保留兼容开关，不作为主图池。

EnemyEnhancementService接4个现有EnemyBasic，增血+50/增攻+5线性叠加；活体增血同比例向上取整、死亡不复活，未来显式注册的敌人继承。禁用但存活的已注册敌人仍受全图效果；溢出拒绝整笔。不等于新增敌人AI或精英。

火球在成功启动动作锁之前从唯一输入刷新Facing，解决上一动作锁在Update与FixedUpdate之间结束时沿用旧方向的问题；锁内仍保持发射方向。人物真实咬/剑动作通知已绑定MainPlayerAnimationSet，删除主图PlayerBiteFlash/Bite Cyan Flash及Sword Gold Flash，清空剑slashVisual；保留真实判定、锚点、音频入口。两种无手火尾组合仍缺Bite资源；既有Dog2_FlameTail缺Fire也未伪造动画。

共享源码契约10新增/扩展（原有参数默认兼容）：

| 接点 | 语义 |
|---|---|
| ChoiceOption(..., bool isEnabled=true) / IsEnabled | Panel、Card、Coordinator均拒绝灰卡；文字承载生命和倍率预览 |
| PlayerReward(..., int attackPercentIncrease=0) / AttackPercentIncrease | 百分点奖励与原子奖励一并预验/结算，旧固定attackIncrease仍兼容 |
| FixedChoiceDeck.Select<T>(cached,pool,count,id,available,randomIndex,copy) | 保留合法卡位，等概率无放回补缺；不足返回null，不伪造卡 |
| TeleportCostKind | 1–9：ShedLegs/ShedArms/ShedTail/ShedFlameTail/CurrentHealth/MaximumHealth/Attack/EnemyHealth/EnemyAttack |
| PlayerState.AttackPercent / HasDamageProtection / CostItem | 唯一倍率、抵达保护与舍弃身份映射；不另建真实状态 |
| PlayerState.CanPayTeleportCost(kind,amount,minAttack,out reason) | 只读验证；致死生命代价和低于倍率下限均拒绝 |
| PlayerState.TryCommitTeleportCost(kind,amount,minAttack,protectionSeconds,Func<bool> tryCommitWorld,Action onCommitted) | 物理步主线程事务；世界回调false必须无副作用，true必须完成无通知世界写入；之后统一通知，拒绝重入 |
| WhiteboxPlayer2D.CanLandAt / TryQueuePaidTeleport / CancelPaidTeleport | 显式安全落点及物理队列；中断不扣费；旧TryTeleportTo只供既有测试/调试，不是普通门路径 |
| EnemyEnhancementService.Register/Unregister/CanIncrease/TryCommit/PublishCommitted | 显式注册、整体预验与静默世界结算，玩家提交完成后才发布敌人通知 |
| PlayerFacing2D.RefreshFromInput() | 同一输入刷新并返回方向，动作锁期间不改变 |

验证：C08首次71/1暴露测试把Portal_B根坐标当交互点，夹具改读原interactionPoint后77/0；包含四门真实模拟E、固定取消/重抽、九费用、灰卡与全灰修复、重复确认、落点迟变、暂停/禁用中断、全图/未来敌人、抵达保护、连续移动Q和旧锁到期边界。C07首次28/2为60ms采样的咬/剑联合断言，补充诊断后全新Play复跑30/0（咬1000→990、剑990→970，Bite/Attack动画均正确）；没有为让测试通过更改攻击逻辑，也不将首轮失败隐去。额外真实Chest→Panel确认实测：60/100→70/110（上限）、→92/110（回血）、100%→110%（伤害11/22/9）。GameView抽卡截图已查看，三卡完整显示。

保存状态：非Play，主图无missing script、四门全部接线；出生点(-12.14,8.6)及原运动/根碰撞保持。C07/C08测试夹具未保存；地图原LevelWhiteboxPlayChecks保留。日志Logs/C08为忽略产物。未重建Windows、未人工整局/音频验收。测试入口Tools/pawgatory/C08/Run Portal And Facing Checks (Play)，需全新主图Play，结束后Stop还原。

> 历史 C07（2026-10-01）：Dada 资源已接到 `Level_Whitebox/Test_Player`，角色取代根白方块；咬/剑动画读取真实成功动作，火焰尾卡已投放并实现慢飞持续伤害。新增 C07 30/0、C06 回归39/0，Unity编译无错误；未重建Windows。Ming HEAD仍724c08c，Dada MERGE_HEAD仍575eff3，合并和本轮完善均待用户提交；没有自动commit/push。


## C07（2026-10-01）主场景角色与火焰尾

状态：已接主图、Editor验证通过，未提交。基线Ming/724c08c；本地Dada合并575eff3仍待提交。用户明确角色替换白块，咬/剑/火在主图可用；追加确认火为持续扣血的慢飞团。规范22、源码契约9；其他未授权完整教学/AI/地刺扣血/收费/结算继续暂缓。

总控新增Production Presentation/Fire、可选IPlayerFireInput、PlayerActionGate与C07验收；在原PlayerState原子数值中增加FireDamage并同步旧固定攻击增益。现有咬剑增加互斥门与Sword成功通知，Whitebox运动只接互斥门、参数未改；PlayerInputReader独立Fire缓冲。Dada原PNG/Clip和本人handoff未改，不把Art Test挂主玩家。

Unity中持久接线：Test_Player保留根碰撞/运动/出生点，新Character、Fire orb/trail，根白Sprite隐藏。MainPlayerAnimationSet持有13身体组合/52动作引用；原始Sprite与普通Sprite材质保留白纸底、PlayerFlame临时程序火焰材质。输入资产增Q/右键/右肩，Overlay说明同步；HUD不再把FlameTail标为不可用。两份主图宝箱配置加入FlameTail，关闭再生优先，成为7项普通等概率池；空槽不自动赠手或火尾。

实际验证：C07 30/0，覆盖真实宝箱卡片取消/确认Arms与FlameTail、移动跳跃/方向、咬剑动画与敌HP、火焰逐实体去重/10跳/8秒冷却/暂停/墙阻挡/失尾/死亡/缺图降级/根碰撞不变；C06复验39/0，含原移动、二段跳、左右冲刺、菜单与敌人接触死亡。C07初次临时驱动因Editor-only程序集不能AddComponent启动失败，已改同C06的UNITY_EDITOR编译方式，之后完整通过。测试夹具用真实EnemyBasic临时1000HP/宽触发区验证完整十跳，额外火焰用临时重置冷却测试独立边界；没有改保存的敌人39HP/7参数。测试日志Logs/C07，截图Builds/C07；均忽略、不作为源码交付。

手动入口/调参/限制详见docs/C07_PLAYER_CHARACTER.md；火焰移动语义及默认参数更新combat_rules§5、combat_parameters.csv（原7列结构保留，4项新增，其他参数不变）。两种缺Bite及Dog2_FlameTail缺Fire仍需Dada补图；仅当前正确身体与攻击效果降级显示。动作伤害不由动画回调触发。原近战时间轴/共用冷却/矩形遮挡、百分比倍率、AI等仍待迁移，火焰音频未映射，未重建Windows或声称整局验收。

最后保存的主图为非Play、无C07临时驱动，玩家空槽开局。没有自动commit/push；用户需要将Dada合并与本轮新增脚本/meta/资产和文档一并提交后push。Wwise/Packages/ProjectSettings不纳入本轮修改。

更新：2026-10-01；规范22，共享源码契约9。规则入口[AGENTS.md](../AGENTS.md)，当前行为/公式[combat_rules.md](combat_rules.md)，数值[combat_parameters.csv](combat_parameters.csv)。本文登记职责、依赖与测试，不另发玩法规则。

活动仓库`F:/2026GameJamUnity`，Ming；HEAD `724c08c`已包含C06和Soap朝向增量。本轮Dada MERGE_HEAD `575eff3`及C07尚未提交；无自动commit/push。过程见[controller.handoff](handoffs/controller.handoff)。

> 历史C06增量（已由724c08c收录）：以用户Ming为主，SOAP/27ef56c只补模块朝向和测试资产；HEAD2d92989未变、MERGE_HEAD27ef56c，merge尚未提交。T08 77/0、T09 50/0，最终主图C06 39/0；C05统一朝向前69/0，按用户要求不再续查独测。WhiteboxPlayer2D保留原运动，仅新增同源朝向引用；不采用队友交接作为玩法规则。

## 1 当前能测到哪一步

| 对象 | 当前事实 | 不能据此声称 |
|---|---|---|
| 当前地图 | 9个真实宝箱、选择UI、三槽HUD、2个条件入口门；唯一PlayerState/Input/Run，唯一WhiteboxPlayer2D；现已接人物动画/咬/剑/慢飞持续火焰/左右朝向/4站桩敌人 | 完整教学、正式近战时序/AI、地刺扣血、支线目标/领奖或整局完成 |
| 新T07/T08/T09 | Soap/29e59ef已由用户合并为2d92989；原独测42/52/50，本轮主图C06 39/0 | 已采用最新矩形/时序/共享冷却/AI规则或完整敌人/精英战 |
| 新规则/数值 | docs现有122项参数（本轮补4项）及计算语义，任务正文按此更新 | 运行时自动读取CSV，或当前Inspector已同步 |
| 美术/音频 | Dada原图白纸底人物/咬剑已接，3种动作缺图及正式火焰特效待补；音频后端/映射/Bank仍待交 | 占位就是最终动画、Cue记录就是实际声音验收 |
| Windows | 05c082e对应C05普通包/自动检查包有历史结果 | 本地新战斗模块已导出或异机可玩 |

保留原T编号；状态须分别写“代码存在、独测范围、主图接线、新规则迁移、已提交来源”。旧T完成记录不继承为新版验收；已依赖的旧代码/资产保留，替换时审查GUID/引用，不另造同名实现。

## 2 现有脚本怎么分工

| 层/已有路径 | 真实职责 | 后续改动归属 |
|---|---|---|
| Core/、Runtime/PlayerState.cs | 唯一HP、躯干、Arms/Legs/Tail等三槽、历史、正面奖励事务；读出CanBite/CanUseSword及伤害 | C02串行迁移攻击百分点/百分比奖励/伤害保护/安全代价；不是卡牌或攻击脚本的第二份状态 |
| Gameplay/Chest/Chest.cs、ChestClaimTransaction.cs、ChestRewardConfig.cs | 候选、固定缓存、确认/取消/替换、调用真实奖励命令、一次消费 | T12改新统一池与配置映射，复用现有事务 |
| UI/Choice/ 与 UI/Hud/ | 选择展示/提交ID，读取真实状态显示三个槽/HP | T01/T02；UI不直接授予手臂、不决定攻击方式、不扣血 |
| Runtime/PlayerInputReader.cs、Gameplay/Combat/PlayerAttackRouter.cs | 唯一输入适配、唯一普通Attack消费；按权限分发同一状态的动作 | C01+T07/T08协调共享冷却/动作锁；只允许一个Attack消费者 |
| Gameplay/Bite/ 与 Gameplay/Sword/ | 真实动作判定、向IDamageable提交伤害、成功动作Cue；目前独立冷却/即时圆查询 | T07/T08迁移矩形持续窗口、锁朝向/遮挡/中断清理；不得维护自己的构筑/伤害倍率 |
| WhiteBox/Scripts/WhiteboxPlayer2D.cs；Gameplay/Locomotion/PlayerLocomotion.cs | 前者是主地图唯一运动；后者用于模块独测 | T06/T10/T11与总控接动作协调；禁止把两个运动器同时挂在主玩家 |
| Gameplay/EnemyBasic/ | 敌人唯一HP、接触攻击、死亡关闭碰撞/视觉、局部注册接点 | T09补AI与统一伤害仲裁；T17实现全局强化；接触攻击Trigger不应意外定义受击体积 |
| Gameplay/Challenge/InspectionDoor2D.cs | 当前门状态、指定部件确认/移除、入口许可与跨入检测 | T20迁移“门永久消失、尝试独立、成功机关保留、普通随机领奖” |
| Audio/Core/ 与未来表现适配 | GameAudio/IAudioBackend统一声音请求；动画读取真实身体/动作 | T05/T18与美术；不凭动画回调再扣一次血 |

已存在的领取链：`交互 → Chest事务 → ChoiceCoordinator/ChoicePanel → PlayerState奖励提交 → Loadout/Body通知 → HUD`。已存在的攻击独测链：`InputReader → PlayerAttackRouter → Bite或Sword → EnemyBasic.TryTakeDamage`。两条链已在主图接通：C06检查真实箱卡确认后Arms槽/剑击联动，以及取消/移除回咬。

用户明确故事流程：正式新局只有头、三个空构筑槽，Items与EverOwnedItems为空、没有Arms、不能剑击。教学箱获得躯干不占槽；只有随后从宝箱候选确认领取“手（剑）/Arms”，原子奖励成功才占一个槽、显示手剑并开启剑击。打开/预览/取消不改槽位、不授予能力；不保证每个随机箱必出手剑，不自动加独立Sword。

剑（手臂）不是新增独立剑卡：有躯干无Arms咬击，有Arms剑击，无腿也可剑击；移除Arms恢复咬击。卡牌层、持有状态、攻击动作、动画表现分别维护，已有Arms身份/领取/槽位不重做。

## 3 实施顺序与授权边界

### 3.1 下一批依赖顺序

1. **C04/C02/C01先发布必要接点**：新参数映射到真实配置；同一PlayerState的攻击倍率/伤害及奖励模拟；普通攻击共用冷却、动作锁、移动朝向、冲刺协调与统一接触伤害的唯一归属。签名尚未发布的部分明确待实现，不让成员猜API。
2. **T07/T08一起迁移动作机制**：复用Router和两个动作组件，改矩形/窗口/方向/墙遮挡；0.5秒共享冷却不能因增删手臂或切动作而刷新。迁移旧独测并补新边界，暂用占位表现即可测逻辑。
3. **T09依赖伤害/运动接点迁移**：先150/10、受击体积、共享保护与死亡顺序，再做有界追击/巡逻/返回；精英与全局强化接T15/T17。T09的站桩原测试不等于AI完成。
4. **T12/T01/T02复用原卡槽链适配新奖励语义**：先验证Arms领取/取消/替换仍正确；百分比奖励、火焰尾与统一池按真实可用功能接入。C07已接慢飞火焰并投7项池；剩余百分比奖励继续按新契约迁移。
5. **美术接动作事件/状态并对齐时间轴**：正式动画资源到齐后替换闪光，验证左右/失去部件/暂停/死亡清理；判定不依赖Sprite大小、不重复结算。美术局部准备可提前，不要求等待全部逻辑完成。
6. **C06已接原模块，新规则迁移后回归**：在现有唯一玩家上接生产动作和真实敌人，验证“全新三个空槽→教学获躯干仍空槽→宝箱确认领取Arms→占一槽/剑击→舍弃或替换→手剑撤下/恢复咬击（舍弃留空槽，替换显示新项）”；不复制含Bootstrap/运动器的整套SmokeRig。最后再验完整路线、Windows与音频。

这是后续任务顺序，不是本轮自动派工。最新授权增加主图现有攻击/双跳/冲刺与站桩敌人集成；已实施C06。完整教学、新combat全量迁移、地刺扣血、收费、支线结算继续暂缓。新表已取消落坑失败/回传/机关复位，取消项不再作为待恢复任务。美术由其他同学负责，最终音频与封装由音频同学负责。

### 3.2 新版来源登记

| 模块 | 来源/当前状态 | 接收要求 |
|---|---|---|
| T07/T08/T09 | 29e59ef已在2d92989；最新27ef56c方向增量no-commit试合并，T08/T09本轮实测77/50，T07保留历史42/0，主图C06 39/0 | 下一版明确本表新验收及源码/资产/meta/handoff；不得把42/52/50当新规则通过 |
| T01/T02/T03/T06/T12 | 旧Soap交付与总控V5兼容适配仍被引用；本次未取得这些模块的新修订证明 | 等对应真实提交单独审查，不自动把整分支模块标完成；历史来源见controller交接 |
| T10/T11 | 主地图已有Legs/Tail权限与在途冲刺清理；独立正式迁移未登记 | 与唯一运动器和动作协调接点一起验收 |
| 其他T | 以任务表实际代码与依赖为准 | 不因文件名/预留枚举/旧完成记录推定已实现 |

接收按共同祖先比较队友新增，保护Ming已有适配与Wwise配置；同一目录只能一个写会话。共享接口/场景/输入变更串行，提交后才可将确切基线发给队友。2d92989是本地已提交merge；本次远端未见该提交，本轮未提交接线也不能由队友pull取得。

## 4 总控C系列工作包

| ID | 现有基础 | 新规则下待交付与验收 |
|---|---|---|
| C00 | 规则入口、契约9、版本/来源登记 | C07同步规范22与可选Fire输入/PlayerState火伤读口；旧接口保持兼容 |
| C01 | RunController/InputReader/Bootstrap，Playing/Choosing/Paused/Dead、输入缓冲 | 串行发布动作协调/接触伤害结算顺序所需接点；胜负/正式重开/喷火输入按范围推进；唯一Time.timeScale |
| C02 | PlayerState三槽/躯干/历史、IPlayerCombatState、原子正面命令 | 咬10/剑20/火8基础值，共用整数百分点与60%下限；百分比回血/上限同额回血，溢出/取消无部分写；统一受伤保护与安全代价 |
| C03 | 单卡/三卡/三旧项事务、取消/替换、输入锁 | 灰卡/预览数据与业务拒绝；换尾明确单卡；保持重复/迟到/重入保护 |
| C04 | 只读Bootstrap审查工具、当前数值来源表 | 将122参数映射到实际Inspector/配置，区分新局快照与运行值；暂未映射不伪造同名字段；不引入CSV运行框架 |
| C05 | 9箱/2入口、原69项回归、Editor菜单与Windows试测构建 | 原动作已由C06接入，新规则迁移后逐项回归；更新与新规则冲突的门/奖励断言，再测卡槽攻击闭环/保存重载/导出 |
| C06 | 主图咬/剑、左右朝向、4站桩敌人与既有腿/尾；Editor显式菜单 | 已测39/0，C05再回归69/0；接线已保存但未提交，后续新规则替换后重验，不将旧值当新平衡 |

C07新增：真实主图角色/咬剑动画/慢飞火焰集成与30项验收，C06回归39/0；操作、绑定及缺图见[C07](C07_PLAYER_CHARACTER.md)。

## 5 T01–T06：展示、世界与运动

| ID / 范围 | 当前情况 | 新交付目标/验收 |
|---|---|---|
| T01 UI/Choice | 已接图，缺灰卡/生命预览 | 单/三卡及三旧项替换；UI与服务都拒绝灰卡，确认一次、取消一次、不自行写状态/时间；等C03真实数据 |
| T02 UI/Hud | 已接真实三槽/HP，身体完整表现待交 | 头部隐血条、躯干启用、Arms得失/有无腿攻击提示、启停快照；通用槽不固定成腿手尾格；不做耐久UI |
| T03 Gameplay/SwitchDoor | 地图WorldDoor配合白盒按钮，模块WorldSwitch仍有旧一次性行为 | 主路逐门独立翻转、不夹人；支线按钮成功障碍消失、按钮保留且无重复收益；不做退出/失败复位 |
| T04 既有相机/任务独测 | 主地图沿用原相机 | 看清落点/危险/交互，传送后跟随；不改玩家碰撞/运动，不强加新相机包 |
| T05 Audio/Wwise及映射/Prefab | 项目后端/Bank交付待验 | 单Cue实际出声、空后端安全、缺Bank可定位、卸载保护、异机依赖；保持Ming Wwise，不动Authoring职责 |
| T06 Gameplay/Locomotion与授权运动适配 | PlayerLocomotion独测存在，主图WhiteboxPlayer2D | 单一刚体写者；保留基础速度/跳高/碰撞与射线grounded；朝向读取输入而非击退速度，动作/击退仲裁；脚步按原契约另接 |

模块内部可自主拆类与调参；主地图、共享玩家Prefab、公共状态/输入仅按总控授权串行修改。T06不能私自替换主地图控制器。

## 6 T07–T18：战斗、奖励与结局

### T07：咬击与普通攻击共同机制

范围：Gameplay/Combat、Gameplay/Bite及对应Config/Prefab/Tests/T07；与T08共用的文件由同一任务批次/指定维护者串行修改。现有42项只证明旧圆形即时咬击及输入链。

新验收：有躯干无手，10基础伤、前缘矩形1×1；默认前摇/有效/后摇各0.1秒，有效窗口持续查询，后进入目标可命中；同实体多Collider每动作一次、多目标可分别命中；实心墙/关闭门遮挡。起手锁方向、位置随玩家走，静止沿最后输入方向，击退不改朝向。普通Attack唯一消费，拒绝忙碌请求不补发，共用0.5秒间隔；换Arms/启停不能刷新本局冷却。实际开始一次PlayerBite，表现/音频异常不二次扣血。

### T08：手臂与剑击

范围：Gameplay/Sword及Config/Prefab/Tests/T08；复用Arms身份、PlayerState和Router，卡槽归T12/T02。现有52项证明真实Arms命令切换/实时伤害/剑咬分发，原T08未经过宝箱UI；本轮C06已补主图真实卡确认→HUD→剑击。

新验收：剑20基础伤、前缘矩形1.5×1.5，节奏同咬击；无腿可用，有手不能补咬击；移除/替换Arms清理该剑动作后续命中/过期表现，下一次合法普通攻击回咬击且不绕过共用冷却。补前摇/有效/后摇各阶段失去手、暂停/死亡/禁用的边界；暂停只冻结时间、不能补打积压输入。动画有且仅有一个伤害源，实际开始一次PlayerWeaponAttack，无耐久。

两模块共同修复夹具：Manual按钮左键与Attack重叠、固定向右、旧日志“T08 unimplemented”的误导；受击范围按明确Collider策略，不能因为敌人攻击Trigger变大而无意扩大受击体积。原覆盖继续保留，和新规则冲突的即时/独立冷却断言替换为新行为，不以减少检查数掩盖失败。

### T09：普通敌人

范围：Gameplay/EnemyBasic、对应配置/Prefab/独测。现有EnemyBasic/EnemyContactAttack是39HP/7接触伤、每敌人0.6秒间隔的站桩测试版，50项原测试通过。

迁移目标：普通150HP/10接触伤；按玩家基础水平速度×1追击，配置索敌/出生边界/巡逻/返回，越界脱战、返回不回血、死怪不复活；实体阻挡与视觉一致。接触首个合法物理步提交，后续受玩家统一0.5秒保护限制，移除独立额外敌人攻击冷却。多来源仲裁依赖总控接点，不自行绕过。保留一次死亡/注销/关闭碰撞和视觉，T17对活体比例更新、未来生成继承另验；伤害通过既有DamageRequest。

### T10/T11：腿二段跳、普通尾冲刺

范围分别为授权的DoubleJump/Dash模块或既有唯一运动器中的对应职责，不强制新建类。现图已读Legs/Tail权限，本轮C06真实输入验证空槽无双跳/冲刺、取得后可用、移除尾后禁用。腿整体一槽，落地重置和空中得失按统一约定验收；无腿仍保留基础跳。尾冲刺失去/换火焰尾立即清在途速度；墙不穿越，不自加无敌。按新默认攻击动作与冲刺互斥，同步动作协调顺序；地刺击退可以终止冲刺，不能两个组件争写速度。参数保持实际地图手感。

### T12：统一宝箱池与原子领奖

范围：Gameplay/Chest、Configs/Chest、对应Prefab/独测；Core/PlayerState/Choice共享接点由总控发布。现地图卡槽链复用，不重做Arms卡。

领取链验收：新局槽全空；预览/取消Arms不填槽；确认成功才Items含Arms一次、HUD占一槽、CanUseSword为真；重复确认不重复占槽/消费；重开回空槽。随机测试可用隔离固定候选夹具保证本次含Arms，不改变正式等概率池。

目标池7项：腿、手剑、普通尾、火焰尾、回血、上限、攻击；普通/支线共用，教学单卡例外，无再生保底、无固定支线强化包。正式投放依赖对应能力已经可用；当前六项试走池不是完整7项验收。

回血ceil(max×20%)；上限ceil(max×10%)且当前同加该量；攻击+10个百分点由唯一状态计算。保留固定候选/跨箱失效只修原位、满血无收益可领取、三槽替换、尾互换单卡、取消/停用/重入/提交溢出无部分效果。参数来自统一配置，不让Chest另持倍率或真实HP。删除目标中的favorRegrowth优先规则，旧资产按授权迁移，不能仅改文案。

### T13：收费传送

范围：未来Portal业务/候选/独测及总控发布的安全代价接点；当前地图PrototypePortal2D免费，不算此模块。

持有部件舍弃项+5效果等概率抽3，不强制舍弃；生命须>0、减攻结果≥60%，展示/服务复验；全灰只修1位为有效未显示非生命项。取消固定、只补失效舍弃项，成功消耗下次重抽。目的地→模拟/复验→原子扣费迁移/清旧运动→0.5秒保护，无部分扣费、无免费兜底。依赖C02/C03/T17，不用IDamageable付代价。

### T14：地刺与共用伤害保护

范围：未来Hazard及配置/独测，跨运动/玩家状态由总控协调；现PrototypeSpike2D只有试走击退。地刺Terrain5、成功伤害才一次击退、敌人无击退；玩家根实体收集敌人/地刺多源，同步选最大伤害再稳定ID，单步最多一次，共用0.5秒保护。无敌不扣血不击退；击退4水平/4向上/0.15控制锁为设计默认，当前攻击继续、冲刺可终止。保留地图Trigger/尺度，无实心承托、无落坑回传；测试重叠/退出/暂停/死亡/抵达保护。

### T15/T16/T17：精英出口、结果新局、全图强化

- T15依赖T09/T17/C01：精英250HP/20接触伤，复用有界接触AI；死只解锁出口，存活进入免费胜利；按同物理步死亡优先，基础身体可完成。
- T16依赖唯一运行/新局入口：暂停继续/重开/退出，结果重开/退出；新局清身体/历史/HP/倍率/强化/箱门/敌死/挑战/冷却/候选/输入锁。Esc取消选择不同时暂停；UI不自行Time.timeScale或靠Bootstrap启停复活。
- T17依赖C02与EnemyBasic注册：全局线性+50最大HP或+5接触伤；存活同比例向上取整、死亡不复活、未来生成继承；溢出原子拒绝，强化数不存共享配置资产。现IEnemyRegistrationAdapter只是局部接点，不是已交付服务。

### T18：动作音频收尾

接T05与真实成功动作，GameAudio/Cue身份沿用；素材、混音、Bank和最终封装归音频同学。脚步按真实grounded/实际移动与独立节奏，0.02秒射线扫描不等于播放频率；脚步稳定Cue尚待串行发布，不伪造Event字符串。取消/失败不播成功，新局/暂停无残留，空后端可玩，有后端异机Windows实际试听。

## 7 T19–T21与两条地图

| ID / 实际范围 | 当前状态 | 新目标/验收 |
|---|---|---|
| T19 教学业务+独测，复用Chest/PlayerState | 躯干一次获取底层已有，地图跳过教学 | 安全头部移动单跳；固定单卡确认100/100/咬击/血条/出口一次启用；取消不改，头部不因0HP死，后续再生不满血 |
| T20 Gameplay/Challenge及独测 | InspectionDoor2D仅入口，当前失效后会恢复门，完整目标/领奖未接 | 门1无手、门2无腿；有/无部件均确认，取消离开再重试；成功本局门永久消失。门开与尝试分离，缺部件合法重入才恢复尝试，成功机关不复位；完成后开放普通随机箱，一局一次 |
| T21 火焰尾慢飞持续伤害（用户已授权，C07已集成） | PlayerFireAttack/输入/七项卡池/表现已接主图，C07 30/0；未提交 | 8伤×10跳、持续2秒、起手8秒冷却；默认1.5单位/秒、半径.6；暂停冻结/失尾取消/墙阻挡/锁方向。百分比倍率、正式特效与音频仍待补 |
| L01 无腿解谜地图 | 地图迭代同学负责 | 基础单跳可达按钮/终点，有步行退出；明确入口、目标、障碍关联与普通奖励点；不建失败坑/安全回传点 |
| L02 无手跑酷地图 | 地图迭代同学负责 | 基础跑跳可完成，地刺/缺口可辨认；无剑/喷火门/强制战斗，防绕过目标；成功机关保持，步行退出 |

T20不新增FallFailZone、落坑伤害/回传、退出机关复位；这部分已取消，不是等待恢复的欠项。当前ResetAdmission等接口的旧效果仍须迁移，C05旧断言不能决定新门规则。

## 8 地图、文件写权与交付边界

程序在模块独测场景工作；地图作者制作/迭代，交付后总控接线。同一unity/prefab/asset串行，保留meta/GUID与现有资源，不盲拼YAML，不另建长期主地图。

主地图继续唯一WhiteboxPlayer2D和稳定根碰撞体；深蓝普通门2×6/6×2、浅紫入口1×5保持，地刺按实际交付尺寸/Trigger。基础速度7、跳高4.5、冲刺距离3是当前实例参数，按实际可达性验收，不由新表重配手感。

地图handoff给出生点/安全教学、地形、箱/敌/精英/出口、传送安全落点、按钮目标、两支线入口/目标/奖励/步行出口、相机边界；不要求已取消的失败区/安全回传点。运行引用显式绑定，不靠名字全局搜索。

## 9 每项提交与验证要求

交付含来源提交、契约版本、允许目录、源码职责/依赖注释、中文Inspector参数/单位/生效时机、Prefab/配置/meta、本人handoff与可复现独测。保持模块自主实现空间，不预先冻结每个内部类名；改变共享签名/状态/输入须串行发布。每人一个开发中任务，不自动启动子代理或commit/push。

测试分层：①编译/引用；②独立场景真实组件逻辑；③正式动画与人工输入/手感；④主地图真实卡槽攻击闭环；⑤整局/Windows/音频。只记录实际达到的层级，不将模拟键盘当人工验收、音频Spy当Bank出声、原测试计数当新规则完成。

以下§10–15保留历史证据，描述当时配置/实现，不作为当前玩法或待办；§16是最近实际本地测试及复跑说明，§17是新表迁移验收差距。


## 10 历史记录：现有T12兼容测试与可复验入口（旧交付不再作为新版完成）

- 场景：`Assets/WhiteBox/Scenes/Level_Whitebox.unity`，01 World/Chests/Chest_01–09；最下面第一箱中心(32.5,-11.5)，玩家可站其左侧约(30,-12.2)按E。菜单鼠标或键盘选择，Esc/Cancel取消，R只回出生点。
- 地图以已获得躯干的白板状态测试；身体外观/实际攻击/收费/完整支线结算尚未接；两入口条件门已接。取得腿/尾会改变真实运动权限；旧开关仅在useLoadoutAbilities=false时生效。
- 配置：`Assets/Configs/Chest/V5_WhiteboxRewards.asset`与`V5_RegrowthRewards.asset`，暂定回血25、上限+15不附带回血、攻击+3；Chest_03/05再生优先。只有三基础部件，没有自然第四种保留奖励；满槽替换用隔离测试验证，不能把未实现喷火投入地图池。
- 新回归：Play后菜单 **Tools/pawgatory/T12/Run V5 Checks (Play)**，实际结果33 passed / 0 failed。测试只建临时状态/菜单替身，销毁后不写地图；真实UI另已在地图操作验证。
- C02/C03已迁移到V5并实际69/63项通过；旧T02/T12与Integration/Level断言、旧奖励配置弃用，等待本人新版文件重新验收；现有T12适配33项仅兼容回归，不能把历史47项结果算新版通过。
- Wwise两文件保持逐字/哈希一致；原轮新增7箱和菜单/输入UI接点；本轮从新图补为9箱，地形与非宝箱标记和来源一致。最终Unity无编译错误，地图无Missing Script；完整导出/整局/手柄实机尚未验证。

## 11 历史记录：历史C系列适配与新T接点（4d6b2e1轮结果，不代表本轮C05）

| 工作包 | 本轮交付 | 实际验证 / 仍未完成 |
|---|---|---|
| C00 | 规范15、源码契约8及旧T弃用/新版交付登记 | C系列提交4d6b2e1，C04后提交b2498d6；此表为历史 |
| C01 | 不改签名/输入资产，复验唯一输入/运行/启动 | C01_Smoke 32/0；Won/整场景重开仍待后续 |
| C02-V5 | 原C02独测迁移三槽/Arms/Legs，新增头部安全、躯干一次、成功历史、原子正面包 | C02_Smoke 69/0；保留生命/通知/重入/启停/死亡，未做负面 |
| C03-V5 | IChoiceFlow/Coordinator均限定1或3；原C03独测迁移三旧项与V5身份，补单卡生命周期 | C03_Smoke 63/0；拒绝2/4，不关闭当前阶段，灰卡/预览仍无接口 |
| C04 | 本轮只读配置审查与唯一来源清单 | Level_Whitebox零错误、隔离检查17/0；实值/边界见AGENTS§7与本节下方 |
| C05 | 现有地图底层兼容回归，不更新布局/保存场景 | T12适配33/0；7箱组件链路三卡开/取消、全部未领；不当新版T验收 |

独测入口沿用Assets/Scenes/Tests/C01/C01_Smoke.unity、C02/C02_Smoke.unity、C03/C03_Smoke.unity，自动检查结束C02/C03为Dead，退出重进Play才是新测试生命周期，不能用Bootstrap启停当重开。本轮没有新增场景/Prefab/meta或修改Build Settings。

新T接线使用AGENTS§7真实签名：IPlayerBodyState / ILoadoutState / IPlayerCombatState只读同一PlayerState；IPlayerRewardCommands.TryApplyReward与TryAcquireBodyCore供服务提交；IChoiceFlow只受理1/3项，IChoicePresenter仍保持确认false保留/同ID换阶段/取消一次生命周期。未发布接点不得由模块自行猜或建立第二套状态。

## 12 历史记录：C04配置与新版T放行依据（已提交b2498d6）

Editor入口：选中场景GameBootstrap，Tools/pawgatory/C04/Validate Selected Bootstrap。源码Assets/Scripts/Tests/C04/Editor/C04SceneValidator.cs，仅Editor程序集；默认正式PlayerState必填，C01纯输入独测用Validate(bootstrap,false)。工具返回Errors/Notes，只读、不修接线、不改资产、不跑Play；通过不代表模块内部/跨场景/整局验收通过。

配置逐项实值、单位、生效时机及唯一来源见AGENTS§7的C04表：初始HP100、基础咬击10/剑15、白盒跳过教学、倍率1、缓冲0.15真实秒、统一扫描半径2/Default/Trigger、兼容奖励25/+15/+3、运动7/4.5/3。它们是现有实例/配置，不额外冻结数值、不另建共享状态。

| 新版模块 | 当前真实可读/可提交接点 | 仍需交付或总控后续发布 |
|---|---|---|
| T02身体/三槽HUD | 同一PlayerState的IHealth/ILoadoutState/IPlayerBodyState/IFormState，现有通知及启用快照 | 新版文件/独测；IPlayerCombatState无变化事件，不伪造事件接口 |
| T06/T10/T11移动 | 唯一IPlayerInput与IRunContext、HasBodyCore和Legs/Tail持有状态 | 新版唯一运动入口、模块配置与独测；不可与WhiteboxPlayer2D同时写刚体 |
| T01/T12当前奖励链 | IChoiceFlow仅1/3、IChoicePresenter生命周期、TryApplyReward/TryAcquireBodyCore正面事务 | 新版代码/Prefab/meta/独测；灰卡/HP预览缺口不由UI私建 |
| T19教学、T07/T08战斗 | 单卡/躯干获取与CanBite/CanUseSword/BiteDamage/SwordDamage读口 | 完整接线本轮暂缓；安全地图/实际攻击组件不能当已有实现 |
| T13/T17收费/敌人强化 | 现有展示/健康只读，正面奖励不是负面接口 | 安全代价模拟/提交/灰卡/敌人注册后续串行发布，本轮暂缓 |
| T20及支线专用奖励/地图 | 三槽/单卡和本轮InspectionDoor2D入口许可/跨入/撤销接点 | 完整目标/失败/专用领奖仍未接；入口已完成部分见§13，L01/L02由地图同学做 |
| T15/T16胜利/重开 | RunPhase身份与现有死亡阶段 | 完整胜利/世界重置/新局接口尚无，本轮暂缓 |

本轮Unity6000.2.9f1编译零错误，当前地图静态审查零错误，隔离Preview Scene 17项通过；没有重跑C01/C02/C03/T12 Play，没有保存场景或改变地图/输入/音频。前轮32/69/63/33结果仍为历史。新版T按来源提交+契约8+真实依赖逐项审查，不要求全部T同时完成。

## 13 历史记录：C05本轮新地图适配与复验

- 来源：Level/ad3ea42，只取Assets/WhiteBox/Scenes/Level_Whitebox.unity；未取SampleScene、没有整分支merge。保留地图GUID。将02 Actors从Chests下移回根，保存后6根、Missing Script=0、C04审查0错误。
- Chest_01–09覆盖全部9簇标记；新增08中心(-32.5,-10.5)、09中心(-39.5,66.5)，普通池；03/05仍再生优先。81个宝箱标记格仅透明，实体未领视图黄色。两新增箱并未绑定支线终点，不当专用奖励。
- InspectionDoor1中心(-22,49.5)，Arms，SIDE_ARMLESS_TRAVERSAL，外侧+X；Door2中心(63.4,63.5)，Legs，SIDE_LEGLESS_PUZZLE，外侧-X。实体1×5、外侧Trigger4×6、偏移2.25、越门余量0.1；玩家/状态/运动/选择/视图均显式Inspector引用。
- C05新Play检查Assets/Scripts/Tests/C05/NewMapPlayChecks.cs：临时添加组件并以Begin注入实际地图的PlayerState、RunController、ChoiceCoordinator、ChoicePanel、按门名排序的2门和按箱名排序的9箱。检查会修改本次Play的身体/箱门/阶段并最终Dead，只用于测试；退出Play后销毁，不能保存进主场景。完整调用例在controller.handoff。
- 实际69 passed / 0 failed，覆盖9箱真实交互三卡/取消、领取一次、两门实体/单项舍弃/缺部件确认/取消重试/补回失效/防夹人/退出/R/死亡撤销；完成钩子仅隔离调用验证。另用真实键盘E打开新增箱、Esc取消到Playing，无暂停残留。
- 保存重载：地面5808个非空Tile和非宝箱标记339格内容哈希与Level来源一致；9箱、2门、6根、无测试驱动。免费传送4个、普通门/按钮各8个、地刺7个试走组件保留，敌人仍红标记。全图基础可达性、完整战斗/支线/收费、Bank和Windows未验证。
- 美术资源准备本轮只更新规范，尚未批量导入。后续按AGENTS§10.4登记资产→状态→目标引用、导入参数、动画触发、碰撞尺度和可读性；同步总控README。旧T重交付规则不变，不能因换美术把模块标为完成。

## 14 历史记录：美术模块自主迭代与异机交付（规范18）

美术由负责成员在自己的电脑/分支提供和接入，无须先上传总控电脑。任务授权写目标、模块范围、现有接口与验收结果即可；不逐个规定切片参数、动画结构、文件名和操作顺序。AGENTS§10.4为实际边界。

成员Agent可在本模块自主选择表现方案、加工素材、调参、创建辅助脚本/配置/预览工具，并反复预览、修复和优化。素材清单边做边补；缺资源/共享接点时先完成可独测部分。稳定业务ID到图标的本地映射无需等公共接口改版，真实状态和公共签名不能私改。只有影响已确认玩法/尺度、共享资产写权、公共契约或超出任务范围时才协调；原音频/并行/发布要求不变。

成员交付仓库相对资源路径、Prefab/预览入口、必要参数及本人handoff与实际验证；总控取得对应提交后再记录主场景集成。成员电脑独测完成与Ming已集成分开登记，不因总控尚无素材判定成员不能开工或迭代。本轮没有导入素材或派发新任务。

## 15 历史记录：C05复验与Windows测试构建（当前轮）

已提交基线0d892a4；C05无需等全部T。新增测试工具只负责当前已实现程序链，音频最终封装、美术及暂缓玩法不扩入本轮。入口与可复制操作见[C05_TEST_BUILD.md](C05_TEST_BUILD.md)。

| 交付 | 实际状态 | 仍待完成 |
|---|---|---|
| Editor复跑入口 | Tools/pawgatory/C05/Run New Map Checks (Play)，实际69/0；全新地图校验、重复拒绝、结果Current可读 | 不替代完整路线/最终UI实机输入 |
| Windows Trial | 显式仅构建Level_Whitebox、Development、普通包无C05运行检查类型；构建0错误/491警告，启动已查 | 未做Windows完整手动试玩/正常菜单退出 |
| Windows Automated Checks | 单次C05_PLAYER_CHECKS定义，自动检查69/0、进程退出0；构建0错误/5警告 | 只证明专用包当前逻辑；不是最终游戏包 |
| 构建副作用核对 | 38个Unity/Wwise自动变更文件已备份还原，暂存meta移出Assets，源图/公共配置无diff | 音频同学自己的构建后仍核对真实改动 |
| 最终封装交接 | 音频同学负责，本文不改变音频契约；已提供场景选择/测试包区分/依赖清单 | 等正式交付范围和相关模块资源齐备，再做整局/Bank/异机Windows验收 |

本轮Windows路径为Builds/C05下20261001-081520-trial-1423f2与20261001-081829-checks-7ad855；均为忽略的本机产物，不提交到源码。最终封装不要直接使用自动检查包或默认SampleScene场景列表。新版T依赖接收表继续使用§3.1/§12，当时尚未登记新T；最新Soap/29e59ef来源与测试见§3.2/§16。


## 16 本地测试具体内容与复跑入口

来源Soap/29e59ef试合并到Ming/05c082e；上轮在Unity6000.2.9f1中进入Play执行，未重新构建Windows。本轮只核对源码与更新文档，没有重新运行或增加通过数。此前独立编译9程序集/静态检查15资产是辅助证据，不替代下表Play。

| 检查 | 入口（均相对项目根） | 上轮结果 | 实际证明 |
|---|---|---|---|
| T07 咬击 | Assets/Scenes/Tests/T07/T07_Smoke.unity；Run T07 Checks | 首轮32/10，聚焦GameView全新Play复跑42/0 | 真实InputReader→Router→Bite，躯干/Arms权限、命中/落空/冷却、多Collider去重、自伤排除、配置过滤、音频故障/权限复验、暂停/选择/死亡、短移动跳跃 |
| T08 剑击 | Assets/Scenes/Tests/T08/T08_Smoke.unity；Run T08 Auto Checks | 52/0 | 真实状态加/删Arms（无腿可剑）、咬剑分发、范围差异、实时伤害、冷却/去重、暂停/选择/死亡、真实敌人死亡清理与占位表现；没有经过宝箱UI |
| T09 普通敌人 | Assets/Scenes/Tests/T09/T09_Smoke.unity；Run T09 Auto Checks | 50/0 | 真实咬击杀敌、唯一HP/死亡通知、碰撞/视觉关闭、接触伤害、多Collider/每敌间隔、暂停、启停不复活、局部注册/快照；没有巡逻追击AI |
| C05 现图回归 | Assets/WhiteBox/Scenes/Level_Whitebox.unity；Tools/pawgatory/C05/Run New Map Checks (Play) | 69/0 | 合并后现有9箱交互/三卡/领取取消、三槽及2入口门旧流程未被破坏；完整挑战完成钩子仅隔离调用 |

这些驱动会临时修改本次Play状态，且最后可进入Dead；退出Play恢复。T08/T09按钮创建新Auto夹具并停用Manual根，避免重复Bootstrap；T07在全新Play中开始检查。上轮工具调用按钮对应方法，输入由InputSystem注入，不是人工逐项按键。T07首轮失焦时后台键盘被禁用，未修改断言/输入设置；复跑全过，首轮失败仍保留记录。

人工查看时：先打开对应场景→Play→点Game视图聚焦。T08默认由测试驱动自动获取躯干+手臂、无腿，完全绕过宝箱，仅用于直接测剑；这不是故事开局，也不能复制到主地图。A/D移动、Space跳、Enter攻击，Remove Arms/Add Arms切换。当前朝右、初始敌人在仅剑可达距离；贴近后咬也可中。左键也绑定Attack，点击控制按钮可能顺带出招，不能用混合点击测伤害/冷却；下轮夹具需隔离此输入。每组人工操作后Stop再Play才做独测，勿保存运行时临时对象。

**未验证**：最新10/20数值、前缘矩形/持续窗口/遮挡/共用冷却；共享伤害无敌/多源仲裁、AI/喷火/新奖励公式；主地图宝箱→手臂槽→攻击完整联动；正式动画/真实音效、整局平衡或合并版Windows。

现地图咬10/剑15；T07/T08/T09夹具刻意咬13/剑15，敌人配置39HP/7伤；咬/剑半径1.2/1.8、各0.6秒冷却。测试值用来证明读实际配置，不是新平衡值。音频Recorder只验证Cue及次数，不是声音。T07旧日志“T08 unimplemented”仅表示该夹具没绑定剑，不代表新T08文件缺失。

## 17 最新规则迁移验收差距

combat_rules.md/combat_parameters.csv为用户提供原文件，118个唯一参数ID/7列已检查，复制哈希一致；来源标签保留51个user_confirmed、50个design_default、17个existing_contract。文件收录不等于118项已经写进Inspector；默认值需实际试玩。

| 迁移项 | 现实现 | 新验收重点 |
|---|---|---|
| 攻击时序 | 即时OverlapCircle，各自冷却 | 咬10剑20、共用0.5；0.1/0.1/0.1窗口；左右/墙体/窗口内进出/同实体去重；Arms变化/暂停/死亡不残留或绕冷却 |
| 玩家数值/奖励 | 固定整数加法，25回血/15上限不回血/3攻击 | 唯一倍率100±10、下限60；逐次ceil；80/100加上限→90/110，max110回血22；取消/溢出原子拒绝 |
| 敌人/地刺 | 站桩39/7、每敌间隔；地图地刺仅击退 | 普通150/10、精英250/20、有界AI；统一0.5保护/单步伤害选择；地刺5伤才击退、在途攻击不被普通受伤打断 |
| 敌人强化 | 仅局部注册/数值写口 | 75/150+50上限→100/200；全图存活与未来生成继承，死亡不复活；+5伤线性，溢出整笔拒绝 |
| 奖励/代价 | 六项试走池、部分箱favorRegrowth；正式收费未接 | 普通/支线7项池、无保底/固定包、另一尾单卡换尾；随机代价不强制舍弃、全灰修1位、生命不致死 |
| 支线 | 入口许可失效后可能重新关门，目标/领奖未接 | 门成功永久消失，缺部件合法重入才有资格；机关保进度，普通箱一局一次；不实现旧落坑/回传/复位 |
| 火焰尾/动画 | 只有身份兼容/占位闪光 | t=0…1.8恰好10跳、80基础总伤，逐跳ceil；2秒锁动作/8秒起手冷却，失去/重得不刷新；正式动画单一伤害源 |

次序见§3，范围见§4–7。新验收应随实现同步改测试，不预写“通过”或固定要求维持旧检查总数。当前共享接口/主图接线授权范围、音频分工与未提交Git状态保持。

## 18 C06主图动作集成与复验（2026-10-01，当前）

用户授权把现有模块调到主场景。本轮在2d92989上接线、保留用户出生点(-12.14,8.6)与原地形/门/机关参数，复用Soap/27ef56c的PlayerFacing2D（删除本轮临时独立朝向适配）；没有复制SmokeRig、第二运动器、自动授予Arms或更改Wwise。原Instant攻击接线通过，不宣称新表迁移。

Unity6000.2.9f1 / Windows64：T08全新Manual注入D约0.3秒位移1.5，FreezeRotation、Playing；主图C06 39/0（实际输入/卡片按钮/HP/朝向/槽位/腿尾/阶段/敌人死亡），C05 69/0。C06为新独立集成验收，不重标旧T；Windows本轮未构建。自动检查结束进入Dead，重新Play才是可手动模式。

操作、位置、参数和准确的测试边界见[C06_MAIN_ACTIONS.md](C06_MAIN_ACTIONS.md)。§16是此前独测结果；其中主图联动“未验证”已由本节更新，新combat、正式动画/音效、AI/精英和整局依然未验。下一步先迁移共享普攻时序/矩形/遮挡及伤害协调，再调数值；保留已接卡槽/运动链，不重复造组件。
## 19 Dada动画资源接收（2026-10-01，当前）

Ming已由用户提交724c08c；本轮no-commit合并Dada/575eff3，252新增路径、无冲突、未commit/push。主角39PNG/95帧/52Clip（13临时Idle），13组合；另3敌人PNG/Clip实际已随分支带入，与Dada文字交接“未交敌人”不符，按真实文件登记。新增资产全部配meta、GUID无重复、42PNG已下载完整，39主角清单哈希一致；Unity6000.2.9f1资源检查1140/0。无Play、无Windows；不继承Dada本人的890项为总控结果。

状态为“资源已本地合入，正式表现待接”，不是T07/T08/主图动画完成。入口Assets/Scenes/Tests/Art/PlayerVisual_ArtTest.unity；Library位于Art/Characters/Player/Controllers。仅预览脚本在Tests/Art，后续正式表现适配读真实PlayerState/运动/朝向/攻击，不能直接复制预览Harness。无手火尾两种Bite和有手有腿火尾Fire缺图，Idle临时复用；不擅自补绘、借其他姿势填缺项或扩大玩法。主地图、共享状态、输入和Wwise保持Ming。