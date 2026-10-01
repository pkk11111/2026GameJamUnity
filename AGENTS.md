# pawgatory 统一开发规范与游戏契约

> 最新 C12（2026-10-01）：用户随后明确授权顶层精英使用精英动画、其他自行选普通怪动画。已在当前主图接14只：Elite_Exit→Dada Satan_Move，Enemy_01–13→Imp_Run；均原3帧/8FPS，保留原Sprite GUID及纸白素材。新增只读EnemySpritePresentation，纯视觉EnemyArt子节点按原碰撞尺寸缩放，按实际横向速度翻面，非Playing冻结、死亡隐藏；不改AI/生命/攻击/巡逻边界/根位置/物理。Unity编译和短Play显示/死亡隐藏检查通过、Console错误0，已Stop还原。未全套测试/导出/commit/push；C11敌人暂缓记录已由本轮完成替代。

> 最新 C11（2026-10-01）：按用户授权从 Dada/416de1c 选择性接入 HUD/血条/身体图、卡牌框/20图标/字体、常驻边框及 InteractionHintView；在当前 Ming/794b6b3 主图原生接线，玩法/收费/数值/地图以本地为准。隐藏 WhiteboxOverlay 的左下调试文字，保留组件的 Esc 暂停。敌人映射按用户最新回复暂不处理；Dada 仅有独立预览，未自定主图动画。Unity 编译及必要短检查完成：当前宝箱交互→三卡→取消/提示恢复、HP100→90及fill0.9正确，Console错误0；未跑全套/导出。未commit/push。详情见 docs/handoffs/dada-ui-integration.handoff。

> 最新 C10（2026-10-01）：按用户授权合入 Level/d1f9830 的美术、9处靠近提示及场景增量；当前 Ming/3c0bae7 的敌人/收费/火焰/数值保留，未提交合并。主场景冲突在Unity内按共同基线逐对象接入18棵新增对象树、既有视觉/位置及31处Tile变化；保留Ming敌人出生标记和用户人物碰撞尺寸1.6666667、表现worldScale=0.7。宝箱9实例与共享Prefab绑定关闭IMG_5506_0、打开IMG_5505_0；Chest.IsOpen记录本局首次成功交互；E交互被接受即吐舌头，此后取消或领取均保持打开，取消仍不消费奖励。已移除主图81格黄色Tile_Chest占位，9箱贴图排序12以显示在前景之上。Hint_enemy的F改为当前Q/右键；Level单门条带动画接线清除冲突的closedView绑定。目标Unity编译/Play宝箱聚焦检查23通过0失败，Console错误0，保存重载9箱绑定及提示引用检查通过；未跑全套回归/整局/Windows。详见 docs/handoffs/level-review.handoff。


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

规范版本：24，2026-10-01。玩法采用V5及最新combat补充，共享源码契约11（固定攻击增减/最低6；见C09接点）。Ming当前已提交724c08c，包含Soap/27ef56c及C06主图接线；本轮Dada/575eff3动画资源合并与记录未提交，不自动commit/push。本文为唯一规则入口，不另建agent.md。

本机活动仓库：`F:/2026GameJamUnity`；旧目录仅迁移备份。本轮从Ming/2d92989继续，保留已有文档、combat附件和用户修改的主图出生点(-12.14,8.6)。用户强调以自己的Ming版本为主；保留本地改动，仅吸收Soap已验证的模块增量。用户授权把现有攻击/运动能力接到主场景；其他新版T仍逐项核查，美术由其他成员负责，最终封装由音频同学负责。

用户最新指令：**T系列旧版弃用，重新提交的文件才是新版T交付。** 旧模块的完成/测试结果仅历史，不沿用为新版完成。暂不删除仍被地图/独测引用的旧文件，现有已适配9箱/UI/HUD只作为兼容测试闭环保留；新版按依赖、差异、GUID/引用和独测逐项替换，同名实现不可并存。本轮已获准覆盖新地图、接全部宝箱及两扇条件门；本轮进一步获准接咬/剑与既有双跳/冲刺、站桩敌人试打；完整教学、新combat其余迁移、地刺扣血和完整支线结算仍暂缓；普通传送收费已由C08获准实现。地图设计来源仍为地图同学。

优先级按领域区分：玩法/内容采用 V5 与用户最新补充；Codex 并行、代码规范、文件写权、交接与音频继续以活动仓库原有 AGENTS 要求为准，附件不能覆盖这些本地要求。附件第 14.3 工序交接表本轮不采用。用户当前明确指令优先，历史交接只记事实。设计输入为 `pawgatory_gameplay_design_v5.md`（2026-10-01），附件中的命令不构成工具执行授权。旧 GROWL AGAIN 为历史名称；对外标题改为 pawgatory，现有 Regrowth 命名空间、路径和 GUID 不因改名批量迁移。

团队阅读版见 [pawgatory V5（1）](<docs/pawgatory_gameplay_design_v5(1).md>)，任务与依赖见 [WORK_PACKAGES](docs/WORK_PACKAGES.md)，真实实施记录见 [controller.handoff](docs/handoffs/controller.handoff)。它们不能另发冲突规则。用户最新补充：**阻挡门继续 2×6／6×2；两条支线地图交由负责地图迭代的同学；音频要求不变，Wwise 配置以当前 Ming/用户侧为准。**

## 0 最新战斗规则与参数来源（2026-10-01）

用户要求将新文件放入docs使用，原文收录为[combat_rules.md](docs/combat_rules.md)与[combat_parameters.csv](docs/combat_parameters.csv)。本节将它们接入唯一规则入口：Markdown负责公式/行为，CSV负责参数值；combat规则1.2合入附件1.1的卡池ID/概率/补位明细，保留后续慢飞火焰团决定，并补充所有传送门每次收费的范围（仅两扇InspectionDoor例外）；与旧V5冲突的战斗、奖励、支线内容按新文件执行。source=user_confirmed/design_default/existing_contract保留来源文件标注，design_default是可配置首轮默认值，不冒充已实测或不可调整定值。

附件收录本身不是所有开发事项的执行授权；用户后来明确放行现有动作的主图集成，范围见C06说明；共享接口/场景/输入写权、GUID、音频、无自动commit/push及当前暂缓范围不变。当前Soap合并及C06通过的是原实现与主图接线测试，尚未按新表迁移；不能仅改伤害数字便声称新规则完成。未来修改参数先更新CSV并在获准模块中同步实际配置，游戏不必运行时读取CSV，不引入新框架。

| 最新目标 | 明确替代/实现差异 |
|---|---|
| 咬10、剑20，共用启动间隔0.5秒；前摇/有效/后摇默认各0.1秒 | 替代当前独立0.6秒冷却、瞬时圆形判定；默认矩形1×1/1.5×1.5从根实体前缘计算，窗口内检测/同实体每动作一次 |
| 取移动输入/最后方向，动作锁朝向；墙/关闭门遮挡；攻击与冲刺默认互斥 | 替代固定右锚点；具体动作锁/判定按combat_rules§3–4，不从动画二次扣血 |
| 喷火只由火焰尾提供；8伤×10跳，0.2秒间隔、2秒持续、8秒冷却 | 不投放独立FlameBreath奖励；旧枚举/兼容接口保留，不据此删引用；C07已实现并入主图池；用户更新为慢飞持续伤害团，见combat_rules§5 |
| 攻击固定+10/-10点，任一咬/剑/火减后低于6整笔拒绝 | C09唯一PlayerState直接持有三个攻击整数；无百分比攻击 |
| 普通敌人100HP/接触10、速度基础值×5/7；精英250HP/接触20、速度基础值×1 | C09主图13普通+1精英已接Soap AI，活动边界按平台覆盖；原39/7仅历史 |
| 敌人每代价增血50或增攻5，线性叠加；活体HP同比例向上取整，未来生成继承 | C08已实现全图/未来注册强化；溢出拒绝整笔事务 |
| 敌人与地刺共享0.5秒保护；地刺5伤且成功才击退，敌人无击退 | 不再给每敌人单独额外接触冷却；多源同物理步最大伤害优先/稳定ID为设计默认 |
| 普通/支线共用7项池：腿、手剑、普通尾、火焰尾、回血、上限、攻击 | 无再生保底箱/固定支线组合包；回血ceil(max×20%)，加上限ceil(max×10%)且同时加当前同额 |
| 部件舍弃+5效果等概率抽3，不强制舍弃；全灰默认仅换1位置为有效非生命项 | 替代旧“有构筑至少一张舍弃”；生命不得致死/取消固定/成功传送下次重抽保持 |
| 封闭地图，无FallFailZone、落坑扣血、安全点回传或局部机关复位 | 原V15与旧支线失败任务作废；入口成功本局消失，重入资格及普通奖励见combat_rules§12 |
| 暂停/结果菜单与新局清理按combat_rules§13设计默认 | 未接正式菜单/重开，不因文档登记自动启用暂缓功能 |

动作有后续动画美术资源；占位闪光不是最终交付。新文档已提供首轮前摇/有效/后摇默认，后续在动作时间轴与正式动画间对齐，保留唯一命中提交和失效清理，不再将旧即时实现作为验收目标。

## 1 接手顺序与状态标记

1. 完整读取本文、相关 handoff 与工作包；核对实际目录、分支、Git 状态。
2. 检查源码、Prefab 和场景实际实例，区分本节规则目标与第 7 节现有旧契约。
3. 确定授权模块和共享文件范围；未决参数只阻塞依赖它的部分，不停下其他可完成工作。
4. 接手时核对实际Unity项目；保护用户未提交/未保存改动。代码、规则或交付改变时更新自己的 handoff 和对应公共文档；Ming 总控工作同步记入 README 的工作记录，写明实际完成、验证、限制及提交状态，不将他人的实现记作总控原创。

**已确认**是用户采用的目标规则；**技术约定**是实现边界；**待配置/待确认**不能被写成最终数值；**预留**不是功能承诺；**已实现/已验证/已集成**须分别有代码、测试和接线证据。文档升级不会自动升级源码契约。

当前实际：Level_Whitebox 已接 9 个真实宝箱、ChoiceCoordinator/T01/EventSystem、三槽 HUD 和唯一身体/奖励状态；仍用一个 WhiteboxPlayer2D，双跳/冲刺现读腿/尾权限。T12 三槽/领取/替换/跨箱修复/即时增益已验证；躯干单卡只有底层与独测，地图明确跳过教学。两扇入口条件门已有实体阻挡、靠近单卡确认和入场资格重验；支线目标/失败区/结算/专用领奖未接。主图咬剑和4个站桩敌人已接，咬/剑/冲刺读取同一个PlayerFacing2D；身体美术与火焰尾已在C07接主图；生命安全代价及完整胜负仍待实现。咬剑仍是旧即时圆判定/独立0.6秒冷却，未适配新表。Wwise Integration 已安装，项目后端/事件映射/Bank 交付未完成。

### 成员自主范围与协作边界

规范用于保护已确认玩法、接口兼容和多人资产协作；不指定每个内部算法。成员和其 Codex 在已授权模块内，应自主完成设计、实现、修复与必要验证，不为普通实现细节逐项请示。

| 情况 | 执行方式 |
|---|---|
| 内部类/方法划分、算法、局部重构、缓存、组件组合 | 自主实施，维持对外契约与玩法语义 |
| 本模块的新脚本、子目录、Prefab、配置资产、独立测试场景 | 自主创建并配对 meta；无需预先枚举所有文件名 |
| 未冻结的速度、距离、伤害、冷却、动画/UI 参数 | 可以给 Inspector 中的暂定测试默认值，标注待调参并记录；不当作最终批准数值 |
| 新表已给出但代码尚未支持的倍率/下限、暂停菜单/重开、动作与伤害协调 | 按发布的真实接口完成可独测部分；缺共享接口先明确接点，不能把已给数值误写成仍待用户裁定 |
| 修改公共接口/枚举值、共享状态写入口、已确认玩法、别人的资产、包或项目设置 | 由总控协调；已有明确授权即继续，无需再次请示 |

规则中的“待实现组件名”是协作职责的参考名；可以在模块内增加辅助类、拆分方法和组件。改变跨模块使用的名字或签名时才需要同步契约。不强制所有模块采用同一种内部设计；优先选现有 Unity/C# 能力和易读实现。

模块授权可以是目录与职责，例如“该敌人脚本、配置、Prefab、独立测试场景”；不要求每新增一个局部文件都等待总控。涉及其他模块时列出需要的集成改动，继续完成本模块可验证部分。

默认采用“目标与验收约束，内部实现自由”。只要保持已确认行为、公共契约、唯一共享状态、Inspector 可配置与交付要求，成员可自主调整内部类名、拆分/合并局部脚本、选择算法、使用 Unity 内置组件、制作表现及优化实现。无需事先把完整方案或每个方法签名交给总控批准；记录真实结果供集成即可。局部可逆问题自行解决，需要变更共享契约时才提交具体集成方案。

本规范不额外锁定成员的 Codex 模型、推理方式或提示词。不在规范中把总控的参考实现写成唯一实现。多人协作的强制交付是：易接手的代码标注、每人的 handoff、Inspector 调参/接线说明与真实验证结果。

## 2 已确认的游戏方向与首版范围

- 正式名称 pawgatory，横版 2D，Windows，主线约 6–10 分钟；支线时长实测。核心为生长、主动舍弃、适应和再生。
- 同一固定三层地图，收费层间传送与必要回溯，本局世界状态往返保持；无跨启动存档或永久成长。
- 开局只有头，可基础移动/一段跳，不能攻击；安全区固定教学箱给予躯干，才初始化生命并开启咬击和危险区。头/躯干/基础移动/一段跳/基础攻击能力永久保留、不占槽。
- 身体部件与独立保留技能共用 **3 个构筑槽（Loadout）**。基础部件为双腿、双手连剑、普通尾巴；姿态仅表现，无独立站立卡、剑卡或手动姿态键。
- 两条支线 **无腿解谜 / 无手跑酷均为首版必做、玩家可选**。地图布局由地图迭代同学负责，程序提供共同挑战机制，总控接线；不扩大为独立场景或复杂机关系统。
- 主路、必要返程、支线必经动作及精英战均能以躯干后的基础移动、一段跳和可用基础攻击完成，不依赖可舍弃能力。无手时咬击是正式基础攻击。
- 本轮已授权并实现火焰尾慢飞持续伤害，正式来源仅火焰尾；旧独立技能身份只为兼容保留，不投奖励池。盾/长枪仍预留。
- 精英死亡只解锁第三层出口，存活玩家进入出口才胜利；死亡优先，无同归于尽结局，不要求集齐身体。

## 3 V5 规则与旧决定的替代关系

本节V01–V19按最新§0更新；其余旧轮次数值/验收记录只证明当时实现。历史 G 编号只用于旧代码/交接追溯；凡与下表冲突均已替代，不能继续按“站立保留剑”“四槽”“献祭可致死”开发。兼容的射线/输入/音频/尺寸约束保留在后续技术章节。

| 编号 | 当前已确认规则 | 替代的旧行为 |
|---|---|---|
| V01 | 头部开局；固定单卡躯干教学，确认后开启生命/咬击/教学出口 | 满血完整身体开局；开局冲刺 |
| V02 | 三个通用槽；双腿一项、双手连剑一项、一种尾巴一项；即时效果零槽 | 四槽、独立武器槽、左右肢体各占槽 |
| V03 | 腿给二段跳；普通尾给冲刺；有手剑击、无手咬击；无腿也能剑击 | 独立站立项、独立剑项、无站立剑留槽、站立无攻击 |
| V04 | 普通尾/火焰尾互斥，喷火只来自火焰尾 | 独立喷火奖励、双来源叠伤 |
| V05 | 全部身体组合共用稳定碰撞体和基础移动/重力/一段跳参数 | 因部件增减自动改体积或基础通行能力 |
| V06 | 成功拥有记录 EverOwnedItems；当前缺失且曾拥有可再生；重开清空 | 无本局持有历史或成长经验值系统 |
| V07 | 普通奖励三选一，拥有/重复来源/未实现项排除；合法即时效果补齐池 | 因奖励不足降为一两张或伪造重复回血 |
| V08 | 候选首次固定、取消不刷、只修失效位；普通与支线共用随机池 | 再生优先/保底箱、支线固定强化组合包 |
| V09 | 满三槽展示全部三个旧项替换；取消无副作用；尾巴互换明确确认后原子替换 | 四旧项替换、满槽吞奖励、先移除再等待确认 |
| V10 | 持有部件舍弃项+5效果等概率抽3，不强制舍弃；全灰按新规则修1位 | 有构筑必须至少一张舍弃、无有效卡仍付费 |
| V11 | 当前 HP 扣 ceil(当前×20%)；上限扣 ceil(上限×10%) 后压当前；结算后两者必须大于 0 | 献祭到 0 合法失败；暗中保底到 1 |
| V12 | 致死代价灰卡禁选；展示和提交两次验证；成功传送消耗卡组，下次重抽 | 合法的致死支付分支；成功后卡组何时重抽仍待定 |
| V13 | 本局负面叠加；敌人强化覆盖全图存活含精英，未来生成继承；增血保持比例 | 只强化下一层、复活死敌或新增敌人不继承 |
| V14 | 两条缺部件支线，各自完成/领奖一次；指定舍弃单卡入场 | 另造随机三代价入口或只有一条支线 |
| V15 | 封闭地图，不设落坑扣血/回传或局部复位；支线进度规则见§0 | FallFailZone、落坑安全点回传、退出重置机关 |
| V16 | 支线完成后解除缺失限制，可再生并步行退出；本局往返保持完成/领奖 | 领取部件后再次锁出口或反复刷奖 |
| V17 | 现有单门/多门按钮分别切换各门状态；关门不得嵌入玩家 | 正式开关只能用一次、多个门强制同态 |
| V18 | 精英败后存活进出口胜利；死亡/失败与完成同帧时优先失败 | 精英死即胜利、胜负顺序待定；旧免费终点传送门例外已由本轮收费规则替代 |
| V19 | 取消耐久设定：身体部件、武器和技能不设耐久值，不因使用磨损、耐久耗尽而损坏或失效；不提供耐久维修/恢复奖励、耐久代价或耐久条。生命、冷却、主动舍弃、满槽替换及已确认的代价规则保持不变。 | 任何耐久、磨损、耗尽损坏及维修设计 |

### 3.1 头部、身体、能力和再生

用户明确故事流程：正式新局只有头、三个空构筑槽，Items与EverOwnedItems为空、没有Arms、不能剑击。教学箱获得躯干不占槽；只有随后从宝箱候选确认领取“手（剑）/Arms”，原子奖励成功才占一个槽、显示手剑并开启剑击。打开/预览/取消不改槽位、不授予能力；不保证每个随机箱必出手剑，不自动加独立Sword。

当前PlayerState/LoadoutCollection初始化不会自动填Arms，prototypeStartWithBodyCore只跳过躯干教学，不填槽。T08SmokeDriver.Start的自动躯干+Arms是独测夹具；正式地图不得挂该驱动、自动发手脚本或预填手剑HUD。当前地图已跳过教学但构筑仍为空，不表示正式故事开局已接。

教学区没有可受伤敌人/地刺/危险通路；单卡“找回身体 / Regrow Your Body”可取消并重开。确认时一次性完成躯干标记、配置最大生命与满血初始化、血条/咬击启用、教学出口开放，不能从旁路跳过。头部阶段不能用 HP=0 推导死亡；其生存语义由唯一状态迁移统一发布。躯干不进入普通池、不被舍弃，后续再生部件不重复初始化生命。

手剑攻击不消耗耐久，不因累计攻击次数而损坏；持有与攻击权限仍按身体状态及已确认的移除规则判断。

双手与剑为同一个保留项：取得就获得普通剑击，失去一起撤下并恢复咬击；腿不是剑击前提。无手仍可用已持有喷火；喷火独立权限/输入，不替代普通攻击。普通尾与火焰尾是一对互斥载体，换尾须明确失去旧能力后原子替换，即使满槽也不能瞬间成为四项；正式喷火只来自火焰尾，不投放独立喷火。

当前构筑是能力和表现的唯一来源；头、躯干、腿、手剑、尾巴按真实组合显示，旧独立喷火身份只作兼容，不投放；分层渲染、组合帧或整图状态切换由表现模块自行选择。失去能力来源时清理在途冲刺速度、攻击判定与过期动画回调。失去部件不会改基础速度/跳高/重力/碰撞体。EverOwnedItems 只在成功领取/替换提交时增加；预览、取消不写历史，已领取箱子不因后来失去部件重开。

### 3.2 奖励、替换和固定候选

普通/支线统一7项目标池见combat_rules§11；教学固定躯干单卡例外。C07已实现火焰尾并投入主图七项池，不投独立FlameBreath。当前持有同款排除，另一种尾仍可候选并明确单卡确认替换旧尾；满三槽不生成第四槽。即时卡不占槽，满血回血可领取；回复/上限/攻击公式见新表。无再生优先箱、无支线专用强化包。

首次接受展示固定，取消不刷；跨箱获得同款只修失效位置，保留其余卡与顺序。最终统一提交替换/历史/数值/箱消费，取消无部分副作用；当前兼容PlayerReward仍是固定加法，百分比和换尾单卡流程尚待实际迁移验收。

### 3.3 代价安全与传送

用户补充（2026-10-01）：除InspectionDoor1、InspectionDoor2外，所有传送门每次使用均须从combat_rules§11.3传送代价池随机三选一支付，涵盖首次、返程、同层和重复使用；支线位置、已开启连接或既往支付不构成豁免。两扇InspectionDoor沿用指定部件单卡确认，不额外收随机传送代价。成功消费本次卡组，下次重抽并再次收费；取消/无效落点不扣费。终点若为传送门也收费；精英解锁、存活进入与死亡优先不变。此为规则更新，当前白盒免费传送仍是待迁移实现，不是正式豁免。

抽卡系统共用（用户确认）：传送与宝箱使用同一套抽卡系统，通过不同卡池配置区分奖励池与传送代价池，复用等概率无放回三选一、卡片展示、取消保留候选与失效位置补位逻辑。每个宝箱/传送入口各自保存候选，不共用同一副卡组；奖励确认执行领取，传送确认执行支付与迁移。已有差异作为各自池的校验与结算规则保留：宝箱领取后消耗，传送成功后下次重抽；代价卡仍执行灰卡/全灰修复与生命安全校验，不将这些限制套到满血回血奖励。复用系统不合并两种池、不新建第二套UI或随机抽取实现，不改变InspectionDoor单卡确认。

前置为躯干已取得、玩家存活、运行/交互阶段合法。除两扇InspectionDoor外，所有传送门每次使用收费，无低资源免费兜底。三张不同代价包含舍弃当前保留项和本局负面；不强制包含舍弃，无构筑时从五种负面定义随机。至少一张可执行，允许其他卡因生命安全变灰。

| 情况 | 结果 |
|---|---|
| 当前 HP=1，扣当前生命 | ceil(0.2)=1，将至 0，灰卡不可选 |
| 当前 HP=2，扣当前生命 | ceil(0.4)=1，剩 1，可选 |
| 最大 HP=1，降低上限 | 新上限 0，不可选 |
| 最大 HP=2，降低上限 | 新上限 1，当前压至不高于 1，可选 |
| 最大 HP=100、当前 HP=1，降低上限 | 上限 90、当前仍 1，可选 |
| 最大 HP=100、当前 HP=95，降低上限 | 上限 90、当前 90，可选 |

UI 显示模拟后生命/上限和禁用原因；鼠标、键盘、手柄及业务服务都拒绝不可选项。点击灰卡不得扣费、传送、触发死亡或刷新候选。代价绕过 IDamageable/护盾/伤害无敌，但必须先完整模拟并保证不会致死；不能通过先扣再回 1 实现。

首次固定卡组；重开更新数值/灰态，只补不存在的舍弃项等真正失效条目。若全部不可执行，以最少换位修复成有效舍弃或有效非生命负面，保持 3 个不同条目；不能借取消把正常灰卡重抽。配置必须让无构筑/低 HP/低上限时仍有有效非生命代价；不能把无作用、超攻击下限的减益伪报为有效。

顺序：验证事务/状态/目的地安全 → 模拟并验证所选代价 → 单次提交代价、清运动残留并迁移 → 给予 0.5 秒抵达保护 → 消耗本次卡组、解锁输入。取消或无效落点无费用。提交前状态变化必须重新验证；失败无部分扣费，无抵达 Cue。V5 不存在合法“代价致死原地失败”分支；旧实现意外算出致死应拒绝/回滚并报错，不能当正常死亡提交。表现回调不参与第二次结算。

敌人强化影响所有存活普通敌人与精英；增最大生命按原生命比例调整，不复活死敌，未来生成继承本局累计负面。具体线性+50HP/+5攻击、玩家倍率及取整按§0新表；统一状态/服务尚待实现，不能模块各猜。

### 3.4 两条支线共用规则

稳定 ID：`SIDE_LEGLESS_PUZZLE`、`SIDE_ARMLESS_TRAVERSAL`。每条前方各有入口条件门，由地图同学制作两条旁路及门/区域摆放。玩家从门外靠近检测区后自动弹出确认菜单，无须按E。InspectionDoor1检查双手（连剑），InspectionDoor2检查双腿；持有时询问是否舍弃该项，已缺失时询问是否开启挑战。两种情况均须主动确认才隐藏门并解除实体阻挡；取消只关闭UI，离开检测范围再靠近才重试。无腿门只提出舍弃腿，无手门只提出舍弃手剑，不自动拆除或要求其他身体项。入口门与普通按钮门职责不同，打开不授予永久资格。程序共享进入资格、指定舍弃、ActiveAttempt、失败/完成/领奖服务，不能每条自建玩家状态或收费系统。

入场须完成躯干教学且存活、阶段可交互、无其他事务、靠近指定入口，从外侧进入，区域与安全重试点有效。未完成且已缺对应部件须确认“开启挑战”后进入，不扣生命或无关项；仍持有则显示 **单卡指定舍弃确认**（腿支线只拆腿，手支线只拆手）。先验证区域，再确认、复验、原子移除并发本支线当次入口许可；实际合法穿过入口才启动尝试。步行入口重验许可/条件，若采用局部传入先验落点。取消/无效配置不移除。完成未领奖可直接领奖，已领奖重访不强制再拆或再次给奖。

挑战未完成时保持缺失指定部件，其他技能允许使用且不作为必需。外部奖励补回受限部件会使本次尝试无效并允许退出，不强制删掉新部件。完成后解除限制，奖励再生不导致重新锁住出口。

每条配置独立入口、完成目标与步行出口；封闭地图不创建FallFailZone或安全点回传。退出未完成支线结束本次尝试，已成功机关不复位；门成功后本局保持消失。再次从入口进入且缺指定部件恢复尝试，持有受限部件可通行但不授予未完成资格；完成后访问不限制部件。支线完成开放普通随机三选一箱，一局一次，不提供固定再生强化包。当前入口代码与这些目标的差异需后续T20迁移，既有C05通过不等于新规则完成。

无腿支线：基础跳能到按钮，不要求攻击；短开关谜题（可用两门、一至两按钮，按每门原态分别切换），目标状态满足并实际到终点才完成。无手支线：基础跑跳能完成的短缺口/高低平台/地刺路，无剑门/喷火门/强制战斗，无计时排行/攀爬/移动平台扩展；咬击仍可用。若不能用布局阻止主路直接跳到终点，可加一个本次经过标记，不新增重生检查点。两条均保留可步行退出路线。

### 3.5 固定地图、尺寸与正式/试走边界

沿用 Level 白盒实际坐标、碰撞与验证过的运动参数；V5 表格估算不覆盖场景资产。**用户再次明确深蓝阻挡门为 2×6／6×2，不改为 2×5。** 灰色地面比例保持，地刺3×1／4×1为常用参考，实际已交地图实例保持；可以校正实际触发器对齐，不能借换脚本/美术改变关卡尺度。怪物碰撞体积与可见怪物大小一致。新Level/ad3ea42的两扇浅紫InspectionDoor为1×5，实体按此视觉对齐；它们不是深蓝2×6／6×2按钮门，不将两类门混用尺寸。新图实际地刺含4/5格视觉，保留作者交付尺寸及椭圆检测，不按参考值强改。

玩家各种部件组合使用同一个稳定碰撞体；当前地图玩家局部 BoxCollider2D 1×1、Transform scale 1×1.5 是现状，用户修正后的 V5 表中约 1×1.2 不自动覆盖。阅读版第 11.2 节采用用户修正基准：网格 1 单位/Cell Size(1,1,0)，玩家约 1×1.2，门默认 2×6（横向 6×2），传送 Visual 约 1×2/交互半径 1.5，按钮 Visual 约 3×1；附件速度 7、跳高 4.5、冲刺距离 3 不是所有实例实测值，不能相加为固定最大跳距。地图交付记录实际 metric 与基础可跨距离，不以脚本默认值代替场景实测。尖刺载体不额外成为实心平台；真实交互物落位后仅隐藏对应标记 Tile。

一对一/一对多按钮目标数组保留，分别翻转各门原状态（原开变关，原关变开），关闭不得嵌入玩家；支线临时门与主路状态分组隔离。传送位置/交互锚点/安全落点分别配置，Portal_B 已校正的站立交互高度保留。

目前 Level_Whitebox 保留免费传送、R 回出生点、无正式扣血地刺及跳过躯干教学的试走设置。运动组件 useLoadoutAbilities=true，双跳/冲刺由唯一状态的腿/尾授予；原 enableDoubleJump=false、enableDash=true 值保留，仅关闭 useLoadoutAbilities 后用于旧试走。这些测试例外不代表正式收费/教学/伤害/重开已完成。反复按钮开关已被 V5 采纳，迁移只补正式状态和安全性。不能直接叠加 T06 和 WhiteboxPlayer2D 两个运动写者，也不能把 T12 旧四槽测试池直接当正式池。

## 4 仍需配置或发布的内容

combat_rules/CSV已提供初始HP、奖励/负面幅度与下限、攻击时序、地刺保护/击退、喷火载体/默认键位、暂停与重开方案，不再将这些列为无数值可用。design_default在实际试玩中可调整，当前地图基础运动参数不被本表覆盖。

待工作是按授权实现并验证：共享攻击时间轴/冷却与动作互斥；唯一玩家百分比数值和代价模拟/提交；统一接触伤害仲裁/保护；新奖励池/换尾单卡；敌人AI/全局强化；支线保进度；完整新局清理。共享接口由总控串行发布，不能假定CSV的ID就是已存在的API。原暂缓范围保持，文档更新不自动执行这些任务。

## 5 技术栈与禁止新增依赖

| 项目 | 统一方案 | 约束 |
|---|---|---|
| Unity | Unity 6.2（6000.2.9f1） | 用此版本新建 URP 2D 项目；不要手改旧 ProjectVersion.txt 冒充完成迁移 |
| 语言 | Unity 自带 C# 编译器，普通类/接口/MonoBehaviour | 不要求安装单独 .NET 运行时才能玩游戏；避免新语言/工具链依赖 |
| 渲染 | URP 2D、SpriteRenderer；新项目 URP 17.2.0 | 不将旧 17.6.0 锁文件直接套到新版本 |
| 物理 | Rigidbody2D、Collider2D、Physics2D | 不混用 3D Physics；移动物理写入在 FixedUpdate |
| 输入 | 新项目 Input System 1.14.2 | 唯一输入适配器，gameplay 不各自轮询键盘/旧 Input API |
| UI | 新项目 uGUI 2.0.0、TMP | Canvas、EventSystem、InputSystemUIInputModule，不并行加 UI Toolkit |
| 关卡 | 独立地图场景交付，由总控接入运行组件；继续采用单游戏场景 | 制图时由地图作者维护，交付接线时由总控维护，同一场景不并行写入；程序模块独立测试 |
| 配置 | Inspector 为调参与接线入口；局部用 SerializeField，共享/成组用 ScriptableObject | 具体要求见 10.1；不为少量参数引入复杂框架 |
| 音频 | GameAudio → IAudioBackend → 已有 Wwise Integration 2025.1.10.4304 | 基于 SDK 2025.1.10 Build 9233；项目适配器/映射/Bank 加载待实现，不擅自升级 |
| Git | 当前仓库、文本序列化、Visible Meta Files | 保留资产 GUID 和 .meta；不提交缓存 |

迁移后 Packages/manifest.json 与 packages-lock.json 为实际依赖依据；迁移前恢复的包版本仅用于旧工程，不自动覆盖新项目。模块不得自行升级或引入网络、DOTS/ECS、DI 容器、第三方 FSM、Addressables、复杂事件总线、程序地图和存档；需要时总控单独批准引入。

### 更换编辑器时的必要文件

必需迁入：Assets/Scripts 及各文件/目录 .meta、根目录 AGENTS.md、docs/pawgatory_gameplay_design_v5(1).md、docs/handoffs、.editorconfig；README 为使用入口。保留目标现有 .gitignore 和 .gitattributes，缺失时才另建必要忽略规则。源码不包含主场景、输入资产、URP 资源或运行组件，新项目由 6000.2.9f1 创建。以新项目生成的 Packages/ProjectSettings 为准，不复制旧版本文件。迁移后确认实际项目版本与所有新源码可编译，刷新必要文件包时保持本文为最新版本。

不迁入缓存/生成文件：Library、Temp、Logs、UserSettings、Obj、生成的 csproj/sln/slnx。旧 Assets/Welcome、Assets/Settings、Packages、ProjectSettings 不进入“仅源码迁移包”；是否在当前仓库删除这些旧工程文件取决于用户确认迁移方式。当前主场景内 Tests 不在源码包中，若需要保留场景应另外迁移并验证。

C01 输入资产为 Assets/InputSystem_Actions.inputactions：Player/Move（取 x）、Jump、Attack、Interact（已移除 Hold，普通 Button）、新增 Dash；新增 System/Pause，保留原 UI map。键盘 A/D 或方向键移动、Space 跳跃、Enter/鼠标左键攻击、E 交互、Left Shift 请求冲刺、Escape 发布暂停请求；Gamepad 对应左摇杆、South/West/North/East、Start。按键是资产配置与测试基线，不新增姿态键，不授予冲刺能力。火焰团Q/鼠标右键/手柄右肩已接主图Whitebox_Input；旧独测资产保持不变；开盾仍预留。PlayerInputReader 在 Update 采样并限时缓冲；物理动作在 FixedUpdate 单次消费。各模块不改公共输入资产、不轮询设备。PauseRequested 只发请求；正式暂停菜单策略未接入，C01 测试驱动可演示暂停。

## 6 目录、命名空间和程序集

| 目录 | 实际职责与状态 |
|---|---|
| Assets/Scripts/Core/ | Regrowth.Core，Ming/4d6b2e1已提交契约8身体/奖励端口及IChoiceFlow数量语义 |
| Assets/Scripts/Audio/Core/ | Regrowth.Audio.Core，独立空后端与统一音频入口 |
| Assets/Scripts/Runtime/ | C01 运行/输入/启动、C02 玩家状态、C03 交互/选择协调 |
| Assets/Scripts/Gameplay/ | 旧SwitchDoor/Locomotion/Chest仍被引用；新Combat/Bite/Sword/EnemyBasic已接主图试测；Soap/27ef56c方向增量处于未提交merge |
| Assets/Scripts/UI/Choice/ 与 UI/Hud/ | 旧T01/T02与总控适配仍供兼容测试；正式交付等待新版T，结果UI待实现 |
| Assets/Scripts/Presentation/ | 身体组合/反馈职责，按任务创建 |
| Assets/Scripts/Audio/Wwise/ | 项目后端职责，尚待接入；不改插件源码 |
| Assets/WhiteBox/ | Level_Whitebox 地图、试走脚本、输入与资源 |
| Assets/Scenes/Tests/、Assets/Scripts/Tests/ | C01–C05、各T独测；新T07/T08/T09原实现已复跑；旧Integration/Level仅历史，不放入正式运行入口 |
| docs/handoffs/ | 每个成员的实施交接，别人的文件只读 |

既有 Regrowth.Core/Runtime/Gameplay/UI/Presentation/Audio 命名空间保留。Core 和 Audio Core 不依赖 Gameplay、UI、Input System 或 Wwise；业务程序集按需引用，输入适配器显式引用 Unity.InputSystem。不能因标题改名批量移动资产或重建 GUID。

禁止复制同名接口、各模块创建 GameManager、重复真实 HP/构筑/挑战状态或分别设置 Time.timeScale。玩家与运行状态由总控唯一组件持有，每个敌人仅一份自身生命；表现只读。局部服务/类名可自由组织，跨模块身份/签名须登记。

### 6.1 当前卡槽与攻击结构

| 职责 | 已存在脚本 | 谁读/谁写 |
|---|---|---|
| 身体与三槽/HP/数值 | Runtime/PlayerState、LoadoutCollection | 唯一状态写入；Core公开只读视图和既有命令，正式开局为空槽 |
| 宝箱候选与领取 | Gameplay/Chest下Chest、ChestClaimTransaction、ChestRewardConfig | 提供Arms候选，确认后调用TryApplyReward；取消不提交；不另持真实构筑 |
| 卡片/槽显示 | UI/Choice与UI/Hud | 卡片只传选项ID，HUD按PlayerState快照/事件显示；显示图标不授予能力 |
| 输入/动作分发 | Runtime/PlayerInputReader、Gameplay/Combat/PlayerAttackRouter | Reader唯一采样，Router唯一消费普通Attack；根据CanBite/CanUseSword选动作 |
| 咬与剑 | Gameplay/Bite/PlayerBiteAttack、Gameplay/Sword/PlayerSwordAttack | 读取同一PlayerState权限/伤害，提交IDamageable；当前瞬时圆判定/各自冷却，待新表迁移 |
| 移动 | 主图WhiteboxPlayer2D；模块夹具PlayerLocomotion | 每个玩家只一个运动写者；不把测试Rig整套复制主图 |
| 敌人 | Gameplay/EnemyBasic/EnemyBasic、EnemyContactAttack | 敌人自有唯一HP；当前接触冷却待改统一玩家保护，AI未实现 |
| 表现与声音 | PlayerBiteFlash、剑内slashVisual；GameAudio | 当前闪光占位；后续正式动画由真实动作状态/通知驱动，不能另发伤害 |

领取链：交互→Chest事务→选择UI确认→PlayerState写Arms→事件通知HUD；攻击链：InputReader→Router→Bite/Sword→敌人伤害。两条链现已在主图接通，C06真实箱卡确认→Arms槽→剑击通过；开箱/取消不授予，失去Arms恢复咬击。C06中的腿/尾通过真实状态命令准备夹具，不冒充两张卡的人工领取验收。

### 6.2 新表迁移的共享归属（目标，非已发布API）

普通攻击0.5秒冷却必须由同一玩家的普通攻击协调职责持有，不能继续各自存于Bite/Sword而通过换手绕过。朝向从现有输入/运动接点取得；起手锁方向和动作阶段由唯一协调职责传给判定/表现，实际类名可在模块内设计。喷火/普攻/冲刺互斥、同物理步攻击与伤害顺序涉及多个模块，先串行约定，不让各个FixedUpdate依赖随机顺序。

攻击倍率与玩家受伤保护属于唯一玩家状态/协调入口，敌人全局强化属于唯一局内服务；T07/T08、T09/T14不能各造一份。现IPlayerAttackAction仅CombatState/TryAttack，IPlayerCombatState没有动作阶段/方向/冷却/伤害变化事件；凡新增跨模块接点先发布真实签名与测试。本文不虚构已经存在的接口，不要求一份大GameManager或新依赖框架。

卡槽与状态基础已存在；后续按C系列共享接点→T07/T08动作迁移→T09/伤害→T12数值池/卡槽回归→正式动画→C06主图接线回归的依赖推进。美术/模块内无依赖准备可同时由各自授权副本完成，不自动启动多代理。


### 6.3 C06主图动作试测接线（已由724c08c提交；以下为当轮记录）

以用户当前Ming版本为主，保留其出生点/地图/WhiteboxPlayer2D手感与现有规则。Test_Player新增PlayerAttackRouter、PlayerBiteAttack、PlayerBiteFlash、PlayerSwordAttack及Soap的PlayerFacing2D；全部读同一PlayerState/Input/Run。临时主图专用朝向适配已删除，不能重新添加另一套。WhiteBox程序集新增对Regrowth.Gameplay.Player引用；WhiteboxPlayer2D只新增显式facingSource，主图冲刺方向读取同一源，旧未接该源的独测才回退原输入方向。没有替换运动器或改速度/跳高/碰撞。

PlayerFacing2D在Reader后Update只读MoveX、不消费按键；主图deadZone=0.1，默认右/释放保留。公开FacingSign、IsFacingRight、Direction、FacingChanged/IsWired，属于Gameplay模块接点，Core契约8不变。两攻击显式绑定同一facingSource，用右向参考Origin计算HitCenter并排除反侧目标；攻击视觉独立于Origin，不能再镜像参考锚点以免二次翻转。根实体不翻转；后续完整身体美术可用PlayerFacingPresentation绑定纯视觉子树，本轮白方块未强改。

开局三个空槽、仍跳过躯干教学；无手Enter/左键咬击，有手剑击，腿卡后二段跳、普通尾后Shift冲刺。青咬/金剑0.18秒占位；动画不得二次结算。当前依旧咬10剑15、圆半径1.2/1.8、独立0.6秒；仅原实现集成，不替代§0共享0.5/矩形窗口等目标。

02 Actors下4个站桩EnemyBasic：(-51,1.5)、(-44,9.5)、(-54,31.5)、(-45,37.5)，39HP/接触7/每敌人0.6秒，显式绑定Run与玩家。原标记Tile/地形保留，其他大敌/精英标记仍非业务敌人。AI、窗口/遮挡/共享冷却/保护、攻击冲刺互斥、新倍率/奖励公式待迁移。

复跑见[主图动作试测](docs/C06_MAIN_ACTIONS.md)。C06仅UNITY_EDITOR编译、显式菜单创建DontSave驱动；39/0，包括咬/剑/冲刺同源、左右冲刺。Auto含死亡终态，Stop再Play恢复手动。末次保存重载核对空槽/100HP/4活敌/无测试驱动，Windows未重建。

### 6.4 C07 主场景人物与慢飞火焰（本轮已实现）

用户授权真实玩家直接使用Dada资源；最新回复“持续扣，飞慢点”更新喷火空间规则。正式身体组合来自唯一PlayerState；开局依旧躯干原型+三个空槽，未恢复完整头部教学。宝箱实际确认Arms后显示手剑并可剑击；FlameTail后可按Q/右键/右肩释放。普通尾与火焰尾互斥，沿用单卡明确换尾事务；9箱引用的两份配置现同为7项等概率池、favorRegrowth=false。即时奖励暂留旧固定加法，未冒充新百分比倍率。

| 实际接点 | 职责与参数 |
|---|---|
| Core.IPlayerFireInput.TryConsumeFire() | 可选独立Fire缓冲端口；PlayerInputReader实现。fireActionPath默认空兼容旧独测，主图Player/Fire。旧IPlayerInput不改签名 |
| Runtime.PlayerState.FireDamage / CanUseFire | 唯一火焰伤害和权限；初始每跳8，仅存活Playing且有躯干+FlameTail可用。旧attackIncrease与咬剑火一起原子加，溢出整笔拒绝 |
| Gameplay.PlayerActionGate | TryBegin(MonoBehaviour,float)、Release(MonoBehaviour)、IsBusy；主图咬/剑0.35秒、火2秒、冲刺按原持续时间互斥，允许基础移动/跳跃。Facing忙碌时保持起手方向；不存能力/真实HP |
| Gameplay.PlayerFireAttack | TryAttack、Cancel、IsFiring、Position、TickCount、CooldownRemaining、AttackStarted；唯一Fire消费者，FixedUpdate(-120)先于普通Router(-110)，再到原运动器 |
| Presentation.PlayerAnimationSet | 正式13组合/52动作引用，复用Dada Sprite/Clip GUID；不依赖Regrowth.Tests.Art，Editor转换后持久保存 |
| Presentation.PlayerCharacterPresentation | 只读身体/运动/受伤与三个成功动作事件；Sprite帧按原Clip FPS播放，Idle复用首帧呼吸。无动画扣血；重定位清表现与在途火焰 |

火焰参数：每0.2秒8伤、2秒10次、起手8秒冷却，默认速度1.5/半径0.6，持续重查、实体每跳去重、扫掠碰墙消失并逐目标遮挡。离开不补扣；暂停/Choosing冻结，失去火尾/死亡/停用立即取消，重新获得不重置冷却。火焰团无实体碰撞、不击退。完整接触10跳才为80，经过一次不保证全额命中。输入、读口和Inspector绑定见C07说明。

主图根SpriteRenderer关闭，新增Character子Renderer和纯火焰表现，保留原根scale(1,1.5,1)、碰撞、出生点(-12.14,8.6)与运动参数；表现逆缩放保持画面比例。用户明确要求保留原图白纸底，已撤回去白材质；直接使用原始Sprite和普通Sprite材质，不裁切或去底。Dada两种火尾Bite/全部件Fire仍缺图，保留当前正确身体+已有咬反馈/火焰团，不能冒称缺图已补齐。受伤红闪、死亡灰显、冲刺青色只是暂代反馈；火焰为代码材质临时特效。

普通攻击仍旧咬10/剑15、瞬时圆判定、各自0.6秒冷却；0.35秒互斥不代表新0.1/0.1/0.1时间轴或共用0.5秒已经迁移。现有GameAudio/Cue1–14与Wwise保持，咬剑沿原成功Cue；火焰未接新音频映射，不冒用咬/剑Cue。最终美术/音频和Windows封装仍由对应同学完成。

C07主图真实输入/卡牌/伤害/暂停/失去部件/缺图降级30/0，C06动作移动回归39/0；详见docs/C07_PLAYER_CHARACTER.md。测试创建临时DontSave对象，结束死亡后Stop/Play还原；不挂正式场景，不继承旧T验收。

## 7 实际存在的接口与迁移缺口

**当前已提交基线Ming/724c08c，工作区Dada合并、C07与C08契约10未提交。灰卡、生命预览、共享倍率与安全传送代价已接主图，签名见C08表。** 近战时间轴、常规伤害统一仲裁、完整挑战结算与重开仍待实现。 新增身份使用 201–204，旧 ID 1/2/5/101 等保留数值但不再被正式 PlayerState 奖励写口接受。当前地图为空构筑开局，没有序列化旧库存需要静默转换；新 V5 奖励资产独立创建，旧测试配置保留供追溯，不当正式池。

| 类型 | 实际签名/字段 | 已实现语义或边界 |
|---|---|---|
| RunPhase / IRunContext | Playing=0、Choosing=1、Paused=2、Dead=3、Won=4；Phase、IsGameplayActive、PhaseChanged | C01阶段；Won/正式重开/同帧胜负仲裁仍待接 |
| IPlayerInput | MoveX、JumpHeld、TryConsumeJump/Attack/Interact/Dash、DiscardGameplayInput、PauseRequested | 单一输入缓冲；UI使用原Input Actions的UI map，不改公共输入资产 |
| IHealth / IDamageable | CurrentHealth、MaximumHealth、IsAlive、HealthChanged/Died；TryTakeDamage(DamageRequest) | PlayerState；头部安全阶段未启HP但IsAlive=true且拒绝伤害；躯干后按HP判活 |
| DamageKind / DamageRequest | Enemy=0、Terrain=1；Amount、Kind、Source | 正数实际伤害；代价不得走此接口 |
| LoadoutItemId | 旧Dash=1、DoubleJump=2、Shield=3、FlameBreath=4、UprightForm=5、Sword=101、Spear=102；新增Legs=201、Arms=202、Tail=203、FlameTail=204 | 四种身体身份已供T12使用；FlameTail在C07实现慢飞持续伤害并投入主图奖励池 |
| LoadoutRules | Capacity=3；IsBodyItem / IsTail / IsV5Item / Conflicts | 统一结构与尾巴/喷火互斥，不是第二份持有状态 |
| ILoadoutState | Capacity、Items、Contains、LoadoutChanged | PlayerState唯一三槽，不可写活视图，提交后通知 |
| PlayerForm / IFormState | Quadruped=0、Upright=1；CurrentForm、FormChanged | 兼容表现读口，由Arms推导；不是独立站立项或手动切换命令 |
| IPlayerBodyState | HasBodyCore、EverOwnedItems、WasEverOwned(item)、BodyChanged | 唯一躯干与本局成功持有历史；历史只成功领取/替换增加 |
| IPlayerStateCommands | TryHeal、TryAddLoadoutItem、TryRemoveLoadoutItem、TryReplaceLoadoutItem | 现执行V5身份/互斥/容量；移除Arms连剑一起撤下，不保留旧Sword项 |
| PlayerReward | 构造(item=null, heal=0, maximumHealthIncrease=0, attackIncrease=0, attackPercentIncrease=0)；Item、Heal、MaximumHealthIncrease、AttackIncrease、AttackPercentIncrease、IsValid | 不可变正面奖励包；default/负数无效，未含费用或负面 |
| IPlayerRewardCommands | TryApplyReward(PlayerReward, LoadoutItemId? removedItem=null, Action onCommitted=null)；TryAcquireBodyCore(Action onCommitted=null) | 全量预验后同时写槽/历史/数值；false无部分变动，满血回血包可成功；回调在状态通知前标记箱子消费，不得再写状态 |
| IPlayerCombatState | BiteDamage、SwordDamage、CanBite、CanUseSword | 有躯干无手可咬、有手可剑，腿不影响；这里只给数值/权限；T07/T08原动作已独测并接入主地图，C06 39/0；新表迁移待做 |
| IInteractionState / IInteractable | HasTarget、InteractionId、Prompt、TargetChanged；Kind、CanInteract/TryInteract(actor) | 真正交互桥接为InteractionTarget；类别→距离→稳定Ordinal ID，跳过不可用对象，一次请求不二次转发 |
| InteractionKind | Portal=0、Switch=1、Chest=2 | 既有顺序；支线条件门采用靠近自动确认，不加入E交互类别排序 |
| ChoiceOption / ChoiceRequest | Id、Title、Description、IsEnabled（默认true）；请求复制1–4个不同候选 | C08支持灰卡拒绝和描述中的生命预览，正式门仍恰好三项 |
| IChoiceFlow / ChoiceCoordinator | IsOpen、RequestId、TryBegin/TryReplace/Cancel；具体协调器新增LastClosedFrame | 已提交契约8：TryBegin/TryReplace只接受1或3；2/4拒绝且不改当前阶段/回调；满槽3旧项，单卡显式确认，协调Choosing/锁 |
| IChoicePresenter / T01 | TryShow、TryReplaceCurrent、CancelCurrent | 既有生命周期；本轮补默认卡焦点/卡片Escape取消；WhiteboxOverlay防同次取消再暂停 |
| IAudioBackend / GameAudio | 原Play/StopAll/InstallBackend/UninstallBackend | 数值、方法与生命周期不变，空后端可运行；项目Wwise适配尚未交付 |

### 新T07/T08/T09模块接点（2d92989已合并，非Core契约升级）

| 类型 | 实际接点 | 边界 |
|---|---|---|
| Gameplay.IPlayerAttackAction | IPlayerCombatState CombatState；bool TryAttack() | Router唯一消费Attack；动作与Router绑定同一真实状态，拒绝不排队 |
| PlayerBiteAttack / PlayerSwordAttack | 实现IPlayerAttackAction；IsWired、CooldownRemaining；Bite/Sword均有event Action AttackStarted（Sword为C07补充） | 当前即时OverlapCircle/实体去重；读取实时权限/伤害。主图已移除咬剑测试闪光，使用人物动作；无独立Sword构筑或耐久 |
| EnemyBasic | IHealth、IDamageable；TrySetRuntimeStats(int newMaximumHealth, int newCurrentHealth, int newAttackDamage) | 敌人唯一生命；运行快照写口不等于已实现全图强化；死亡保留对象、关碰撞/视觉 |
| Gameplay.IEnemyRegistrationAdapter | Register(EnemyBasic enemy) / Unregister(EnemyBasic enemy) | C08 EnemyEnhancementService已实现，四敌显式接线；不是另发Core全局注册契约 |

### C系列当前验收与新版T接线边界

- C01输入/运行独测前轮复验32/0；PlayerInputReader与RunController的签名和配置未改。
- C02_Smoke已按V5迁移，实际Play69/0。覆盖三槽、Arms合手剑、无腿剑击、旧身份拒绝、生命/死亡顺序、只读活视图、重入/启停，以及隔离头部状态、一次躯干、历史保留和正面原子奖励。原测试按钮的序列化引用保留，运行标签/行为改为手剑和腿；不代表教学/攻击地图已做。
- C03_Smoke已按V5迁移，实际Play63/0。覆盖单卡开/取消/确认、三卡与三旧项替换、三卡进入单卡确认、拒绝2/4卡、迟到回调、业务重入、取消、启停及真实输入/交互排序。保留泛用ChoiceRequest/IChoicePresenter的1–4展示数据兼容，但总控业务事务只允许1/3，不能借泛用展示绕过契约。
- 现有T12V5Checks作为兼容回归前轮33/0；前轮地图7箱真实Chest→Flow→Panel三卡开/取消均正常、全箱未领取。它不把弃用的旧T12交付标为新版完成。
- 新T01/T02/T03/T06/T10/T11/T12按本文真实接口接线；普通奖励和替换用3项，教学/指定确认用1项，不再提交4项。T07/T08读取HasBodyCore与CanBite/CanUseSword，不从独立站立卡/旧Sword推导。
- 灰卡/生命预览、安全代价模拟/原子提交、完整挑战结算与重开仍无已发布接口；C05入口门接点见下文，本轮未实现正式收费/教学/战斗/完整支线。需该接点的新版T只能完成独立无依赖部分，不能自建第二份玩家状态或自控Time.timeScale。
- 旧T交付及其Smoke/Integration/Level断言弃用，保留资产依赖和历史，不执行或继续迁移队友旧T测试。新T文件/配置/测试随本人新版handoff提交；总控先审查再替换引用，不因旧提交已合入而拒绝新版。

### 实际场景接线、配置与剩余迁移

C01 RunController / PlayerInputReader / GameBootstrap 仍为唯一阶段/输入/启动；PlayerState 是唯一玩家生命/构筑/身体状态。PlayerInteractor 与 **InteractionTarget** 只读筛选目标，CanInteract不结算；同类别按距离再稳定ID排序。ChoiceCoordinator显式绑定ChoicePanel和Bootstrap，UI不写状态或Time.timeScale。

Level_Whitebox 保留6根：00 Runtime、01 World、02 Actors、03 UI、90 Validation、WwiseGlobal。新图01 World/Chests有9个Chest Prefab实例，对应9簇黄色宝箱标记；新增Chest_08/09补齐来源场景缺失实体，保留ChoiceMenu/EventSystem/ChoiceCoordinator。对应81格宝箱标记设透明，Tile身份和其他标记保留；实体未领取视图用黄色。02 Actors从Chests下恢复为根，世界坐标不变。三槽HUD读取Test_Player；原第四槽视图在地图实例停用，来源Prefab保留兼容旧独测。

- `Assets/Configs/Chest/V5_WhiteboxRewards.asset`：腿/手剑/普通尾/回血/增加上限/攻击六个不同条目，普通等概率池。暂定回血25、上限+15（不附带回血）、咬击与剑击伤害各+3；不是冻结平衡数值。
- `V5_RegrowthRewards.asset`：相同池，favorRegrowth=true；地图Chest_03/05使用，优先一个本局曾持有的缺失身体。
- PlayerState的prototypeStartWithBodyCore=true是当前白板/旧独测跳过教学例外，正式T19地图必须关闭并接安全起点/出口；头部、单卡躯干与满血初始化已有独测，不代表安全教学场景完成。
- WhiteboxPlayer2D绑定唯一PlayerState，useLoadoutAbilities=true；其余速度/跳高/射线/碰撞参数不变。取得腿能双跳、普通尾能冲刺；关闭此模式可恢复原Inspector试走开关。
- 已有T12固定候选/跨箱单位置修复迁移为三槽；普通即时效果、再生优先、组合原子奖励、明确换尾确认已支持。这些兼容能力不全是新目标：favorRegrowth需撤出正式配置；支线改用普通随机池，固定强化包已取消。新百分比奖励及实际支线领奖尚未接。
- 菜单通过现有Input Actions UI map及持久UI ActionReferences接线；并未复制输入资产。Esc取消本帧由协调器标记，白板不再次处理为Pause。

| 仍待完成 | 后续职责 |
|---|---|
| 完整头部安全教学、部件外观、按新表的攻击和主图接线 | T19/表现/T07/T08；独测动作已存在，地图仍躯干+空槽、未接攻击 |
| 生命安全代价预览/禁用、负面、正式付费传送 | C02/C03后续写口和T13/T17；没有合法致死支付分支 |
| 正式按钮安全切换、地刺伤害、支线目标/完成/普通随机领奖 | T03/T14/T20；L01/L02由地图同学负责；旧落坑失败/回传/机关复位已取消 |
| 精英、出口、死亡优先、整局重开 | T09/T15/T16/C01；R仍仅回位 |
| 独测版本与旧T弃用 | C02/C03已迁移69/63；旧T/Integration/Level用例弃用待新版交付，现有T12适配仅兼容回归 |

### 选择端口生命周期（现有保证继续保留）

TryShow 已有菜单时返回 false 且无回调。UI 回传选项 Id，由发起者复验执行；tryConfirm=true 才关闭完成，false 保持或显示已进入的新阶段；禁止重复/重入提交，成功后不再次调用回调。UI 不扣血、不写槽、不移动或消费箱子。

TryReplaceCurrent 仅允许菜单已开、request.Id 与事务相同；替换不触发旧取消、不释放输入锁。奖励进入替换阶段后返回 false 保持事务，完成时才提交全部效果。CancelCurrent 打开时恰好通知当前阶段一次，关闭时无操作；取消替换取消整个领取。场景卸载总控取消、退订并释放锁，UI 不设置 Time.timeScale。

V5 数量规则：普通奖励/付费传送三项，满槽替换三旧项；固定躯干教学、支线指定舍弃为单卡，尾互换需明确确认。底层能显示四项不许可正式四槽。后续不可选/预览字段仍需作为统一公开数据发布，并保留现有生命周期；灰卡 UI 和服务双重拒绝。所有事件主线程同步，原子状态完成后才通知；OnEnable 订阅并读快照、OnDisable 退订。

### C04配置与接线审查（Editor工具，运行契约8不变）

文件：Assets/Scripts/Tests/C04/Editor/C04SceneValidator.cs；程序集Regrowth.Tests.C04.Editor仅Editor，直接引用Core、Runtime和Unity.InputSystem，不引用旧T程序集。非Play时选中场景GameBootstrap所在物体，运行Tools/pawgatory/C04/Validate Selected Bootstrap。Validate(GameBootstrap,bool requirePlayer=true)返回Errors/Notes；正式场景默认要求PlayerState，C01纯输入独测可显式false。

工具只读取显式入口和本场景已启用实例，报告缺失/跨场景/禁用引用、重复运行/输入/玩家/交互/选择入口、actor与唯一玩家不一致、菜单接口/绑定不一致、非法数值/输入路径/重复动作/Hold及非Dynamic Update。它不写绑定、不启停Input Actions、不创建运行单例、不控制时间，也不替代Play、资产完整性、跨场景唯一性或模块内绑定验收。字段名随Runtime变更时由总控同步维护。

| 配置唯一来源 | 当前地图实值 / 单位 | 读取与使用边界 |
|---|---|---|
| PlayerState.initialMaximumHealth | 100 HP | 生命周期初次初始化/首次躯干获取使用；改Inspector不等于修改当前真实HP |
| PlayerState.initialBiteDamage / initialSwordDamage | 10 / 15 伤害点 | 初始化基础值；T07/T08读IPlayerCombatState当前伤害/权限，不复制基础值 |
| PlayerState.prototypeStartWithBodyCore | true | 当前白盒跳过教学；正式T19必须false并接安全教学，不代表教学已实现 |
| RunController.gameplayTimeScale | 1 倍率 | 唯一运行控制器使用；其他组件不写Time.timeScale |
| PlayerInputReader.buttonBufferSeconds | 0.15 真实秒 | 动作缓冲实时读取；输入资产/路径重新初始化生效 |
| PlayerInputReader.inputActions | Assets/WhiteBox/Whitebox_Input.inputactions | 仅唯一输入适配器；UI使用其显式UI引用，业务不轮询键盘 |
| PlayerInteractor.interactionRadius / layers / includeTriggers | 2 Unity单位 / 1（Default） / true | 圆区域扫描实时读取；对象自身交互半径/贴图尺寸独立，不能用此值覆盖传送门配置 |
| V5_WhiteboxRewards / V5_RegrowthRewards | 回血25、上限+15、攻击+3 | 现有兼容宝箱配置；上限奖励不附带回血，攻击加到咬击/剑击；新版T12交付须自行核对 |
| WhiteboxPlayer2D速度 / 跳高 / 冲刺距离 | 7单位/秒 / 4.5单位 / 3单位 | 当前试走控制器；唯一Rigidbody2D写者，实际可达性用真控制器实测 |

以上是当前实例/兼容配置与使用边界，不是新冻结的平衡数值。少量初始化数据保留唯一PlayerState Inspector，不为它另建第二份共享数值资产或真实状态；模块自己的参数在授权配置内维护。现有IPlayerCombatState没有伤害变化事件，不能假定已发布；HUD/表现不得从图标反推状态或把缓存当真实值。新增事件需由总控串行发布。

C04历史轮目标Unity6000.2.9f1编译无错误，当前Level_Whitebox静态接线零错误；隔离Preview Scene 17/0，原场景/脏状态/Time.timeScale保持，未进入Play。菜单仅静态检查，不代表旧T已重新交付；完整教程/收费/挑战结算/重开端口仍按第7节缺口暂缓。

### C05新地图与入口门接点（已提交dae3f3c）

来源Level/ad3ea42，9箱、4个免费传送门、8个按钮/8个普通门、7个仅击退地刺继续试走；红色敌人仍为标记。新增两箱为普通试走奖励，不作为支线完成或专属奖励。完整挑战没有真实目标/终点调用者，不能凭入口开门标为完成；最新规则不再需要失败区。

| 类型 | 已有公开接点 | 边界 |
|---|---|---|
| Regrowth.Gameplay.Challenge.InspectionDoor2D | ChallengeId、HasAdmission、HasEntered、IsCompleted、IsOpen；void ResetAdmission()；bool TryCompleteChallenge() | 单门唯一入口状态；ResetAdmission撤销未完成许可、不返部件/扣血/移动。TryCompleteChallenge仅在已许可且跨入、仍缺指定部件、存活Playing时一次成功；未来T20须先验证真实目标/终点再调用，地图当前没有该调用者 |
| WhiteboxPlayer2D | event Action Relocated | 实际R/传送位置迁移及速度清理后通知；不在排队请求时发。入口门订阅以撤销未完成许可；不提供第二个运动写者 |
| Regrowth.Tests.C05.NewMapPlayChecks | Begin(PlayerState, RunController, ChoiceCoordinator, ChoicePanel, InspectionDoor2D[], Chest[])；Passed、Failed、Finished、Results | 显式注入真实地图组件的Play检查；只在测试中临时添加，绝不保存进主场景 |

InspectionDoor2D程序集引用Core/Runtime/Audio.Core/WhiteBox；所有跨物体依赖由Inspector绑定。门根实体1×5，外侧Approach Zone为4×6，偏移2.25单位；门1外侧+X、门2外侧-X，crossingMargin=0.1。配置以新局读取为主，菜单文案下次打开读取。补回部件/离开/R/死亡撤销未完成许可；人在门内或门体范围时保留退路，离开后恢复实体，避免夹人。菜单打开期间部件状态变化会更新文案并要求再次确认，不暗中把免费进入改为舍弃。

实际C05 Play为69 passed / 0 failed：9箱真实交互器三卡开/取消、领奖一次，两门实体阻挡/放行、取消后离开再检测、单项移除、补回失效、返回/回位/死亡撤销、防夹人、迟到回调和状态变化后重新确认。完成接点仅隔离调用验证；没有完整支线目标/失败/结算验收。新增箱还经过真实E打开与Esc取消；C01/C02/C03/T12旧独测本轮未重跑。

### C05复验菜单与Windows测试构建（已提交05c082e）

菜单位于Tools/pawgatory/C05：Run New Map Checks (Play)、Build Windows Trial、Build Windows Automated Checks。具体操作/边界及音频封装交接见[测试构建说明](docs/C05_TEST_BUILD.md)。

- NewMapCheckLauncher.StartChecks()显式检查当前Level_Whitebox、唯一依赖和全新状态后注入原NewMapPlayChecks；Current保留本次结果。仅测试入口允许发现组件，不变更生产Bootstrap接线。临时驱动DontSave，检查会消耗本次Play且结束Dead，退出重进才复跑。
- Editor/C05EditorTools.QueueBuild(bool automatedChecks)要求保存且非Play的单个目标地图与已安装Windows64；返回唯一Builds/C05目录并排队构建，BuildInProgress/LastOutputDirectory供观察。显式构建Level_Whitebox，不修改全局Build Settings（当前仍为SampleScene）、后端或输入；每次独立目录不覆盖旧包。
- 两种均为Development测试包。普通试走包不编译C05运行检查类型；专用检查包仅本次extraScriptingDefines加入C05_PLAYER_CHECKS，启动执行原检查，90秒超时，汇总日志并退出。不得作为正式交付包。
- Editor/C05BuildSceneFilter只在上述构建期间移除临时场景副本中的Regrowth.Tests.*组件，磁盘场景不变。新Editor程序集仅引用Regrowth.Tests.C05；没有新的生产公共契约或场景实例。
- 本轮Editor菜单69/0；Windows普通/专用包都Succeeded、构建错误0，警告491/5（详细来源见交接）。独立检查进程69/0、退出码0；普通包仅启动检查，未做完整画面/输入/整局试听验收。旧C系列/旧T独测本轮未重跑。

## 8 状态、事务与维护职责

| 职责 | 唯一归属与交付边界 |
|---|---|
| 玩家状态 | 总控 C02-V5 已在工作区实现躯干、三槽、历史、攻击读视图与正面奖励命令；费用/负面写口待发布；无第二份部件列表 |
| 运行/输入 | C01已有阶段/输入/初始化；完整结算顺序/胜利/重开与动作协调仍待发布，不从Sprite反推身体 |
| 选择/交互 | C03已有单卡/三卡/三旧项替换；灰卡预览待发布，支线接法由总控统一 |
| 奖励与代价 | 在统一状态写口上分别做候选/复验/原子业务；禁止用 IDamageable 扣代价 |
| 挑战 | 两条共用挑战服务，持久进度与当前尝试分离；不让关卡脚本各自改 HP 或复活 |
| 地图 | 地图迭代同学制作两支线和白盒摆放；总控接线时协调场景写权，程序独测不复制长期主图 |
| UI/表现 | 只读实际状态、禁用提示/输入提交；不能根据动画是否结束决定支付/领奖成败 |
| 音频 | 既有 GameAudio/IAudioBackend 与 Cue 保持；Wwise 当前 Ming 配置为准 |

头部安全阶段、奖励组合提交、代价模拟/提交、入场指定舍弃许可、支线尝试/完成/领奖、全局新局复位需分别验证原子性。新局从头部教学重新开始，清构筑/历史/生命数值/负面、候选、箱门敌人、挑战、输入锁、订阅与旧对象引用；不能把 R 仅回位或 Bootstrap 重启当作正式重开。

### 本轮音频配置裁定（接口不变）

用户明确本地/当前 Ming 配置优先。Soap 提交中的 WwiseSettings.xml / wproj 与交接“未纳入提交”不一致，不能据交接覆盖当前配置；本轮核对这两份文件从 649451f 到 0abac92 无差异，无需回滚或导入 Soap 的 SoundBank 路径。Authoring 仍由音频维护者管理。

下节原有音频接口、Cue 数值、资源/后端生命周期要求保留。表中旧“站立持剑”“致死原地失败”是旧触发文案，不能据此恢复独立站立玩法或允许生命代价致死：V5 由真实攻击权限触发已有咬击/剑击 Cue，不可选代价无成功/死亡 Cue。附件提出的成长/支线专用 Cue 不视为本轮新增音频需求，若后续需要扩展另行串行发布，不能冒用旧 Cue。

## 9 音频契约、资产与 Wwise 交付

Gameplay 只调用 GameAudio.Play/StopAll，禁止 AK 类型、Wwise 字符串和 Bank 加载散落在业务。空后端可运行且不缓存过期请求；后端就绪后 InstallBackend(this)，OnDisable 卸载自身。旧后端卸载不会覆盖新后端，但启动组件负责旧资源清理和唯一性。Domain Reload 关闭也会在进入 Play 时重置到空后端。

null emitter 指专用全局发声对象；StopAll(null) 只停止该对象，不停整个游戏。销毁 emitter 跳过；MonoBehaviour 后端必须主动卸载以免接口保留销毁对象。后端缺 Bank/未就绪安全拒绝并限频提示，不用空 catch 吞程序错误。Wwise 程序集缺依赖不能靠空后端解决。

| AudioCue（稳定数值） | 发出时机 |
|---|---|
| PlayerJump=1 | 实际一次跳跃成功 |
| PlayerBite=2 | 四足咬击动作实际开始；站立不能发出咬击 |
| PlayerHurt=3 | 敌人/地形造成实际扣血；献祭不默认复用 |
| PlayerDied=4 | 首次进入死亡 |
| ChestOpened=5 | 奖励确认成功、箱子标记领取 |
| AbilityGained=6 / AbilityLost=7 | 实际能力状态由无变有/有变无 |
| SwitchActivated=8 / DoorOpened=9 | 状态首次改变成功 |
| Teleported=10 | 扣代价后存活且位置迁移成功；致死原地失败不播放 |
| UIConfirm=11 | 确认被接受；不与业务层重复播同事件 |
| BossStarted=12 | Boss 战首次开始 |
| RunWon=13 | Boss 已败且玩家进入出口才播放 |
| PlayerWeaponAttack=14 | 站立持有效武器时，武器攻击动作实际开始；不复用咬击 |

实际 Event 名和 2D/空间混音由音频同学映射，当前未映射。音乐、循环、RTPC/State 需求确认后再发布端口。

共享 Unity 仓库必须提交：Audio Core、后端与映射、实际 Integration 与目标平台插件、配置/音频 Prefab、必要 .bnk/.wem 和 Integration 配套元数据、对应 .meta。包方式需要锁文件与可访问依赖；不全局忽略 DLL。

Authoring 的 .wproj/.wwu/Originals 由音频同学独立版本管理和备份，默认不用放共享 Unity 仓库。WAV 不全局 ignore；Unity 实际引用的素材仍须交付。暂不 ignore 全部 StreamingAssets/GeneratedSoundBanks/bnk/wem。

新项目 Integration 版本由 Assets/Wwise/Version.txt 核实，插件已安装。Authoring 源工程当前位于根目录 2026GameJamUnity_WwiseProject，由音频同学管理，程序侧不修改。后续仍需核实兼容性、Bank 相对路径/清单、初始化/Listener、事件映射和素材来源；安装状态不代表这些已验收。保留已安装插件和 .gitattributes 的 LFS 规则，不把 Wwise 全目录或目标平台 DLL 加入 ignore。新 checkout 无 Authoring 也需编译并播放，目标平台在队友电脑验证。

## 10 C# 格式和代码标注

- 4 空格、UTF-8、Allman 大括号；条件分支写大括号。类型/方法/属性 PascalCase，参数/局部/私有字段 camelCase，接口以 I 开头。
- 每个主要类型一个同名文件；MonoBehaviour 名与文件名一致；Inspector 用 [SerializeField] private，不用 public 字段当通用状态写入口。
- 自研代码文件开头写职责、直接依赖、总控/模块维护边界、本文路径；公共类型与关键方法说明返回/失败/副作用/生命周期。禁止虚假完成标记与无用逐行注释。
- using 位于顶部；不批量重写 Unity 模板、第三方或生成文件。asmdef JSON 不加注释，依赖在本文/交接说明。
- 玩法在成功动作/状态变化处发音频与视觉请求，不在 Update 每帧重复发同一事件。动画与 gameplay 明确一个触发源。
- 用户补充（2026-10-01）：咬击、手剑等动作有配套动画/美术资源，之后补充。当前青色咬击和金色剑光仅独测占位，不作为最终表现；资源到齐后复用动作/真实状态接入，动画与业务保持单一伤害来源。随后新combat文档给出0.1秒前摇/有效/后摇设计默认；迁移时同步处理暂停、死亡、失去手臂与过期回调。
- 缺 Inspector 绑定给可定位提示，不隐式全局搜索/偷偷创建对象来假装成功。无音频不得阻塞玩法。

### 面向其他 Agent 的代码标注（必须）

每个自研脚本/代码文件都须有职责与维护线索；新增或修改时同步标注，不能只有实现没有接手信息。注释解释协作需要的事实与原因，不逐行翻译代码，不填未发生的完成记录。

- 文件头：职责、直接依赖、模块/任务别名、个人 handoff 相对路径、关键状态归属或接线要求。规则指向根 AGENTS.md，不复制整份规则。不要写本机绝对路径或猜测队友姓名。
- 对外入口：说明参数/返回值、失败情况、副作用、何时调用、是否需要初始化或主线程。事件说明订阅/退订方式与触发时机；修改共享状态的入口要说明由谁持有、如何提交。
- 关键内部逻辑：说明为什么需要缓冲、去重、事务顺序、暂停或生命周期保护，以及依赖的假设。明显代码无需注释；性能或简化取舍有实际影响时才记录。
- 可调字段：依照 10.1–10.3 提供 Inspector 提示和生效说明；未实现项写准确的 TODO 与依赖问题，不用注释宣称功能已完成。
- 类职责或签名改变时同步注释及 handoff。生成文件、第三方源码、JSON/asmdef 不强加非法注释；依赖与用途记录在 handoff。

参考文件头，字段按真实情况填写；简单纯数据类型可以简写，不必照搬全部行：

```csharp
// 职责：本文件实际完成的功能。
// 模块/维护别名：成员名字或已确认任务别名。
// 直接依赖：实际接口、组件或配置；状态归属：本地状态或共享状态持有者。
// 接线/生命周期：必填引用、初始化与清理要点；不适用时省略。
// 交接：docs/handoffs/<名字>.handoff；规范：根目录 AGENTS.md。
```

代码注释负责解释实现；handoff 负责跨文件进度、接线、验证和下一步。不能用“详见 handoff”代替代码中必要的接口语义，也不靠手写逐行索引让两个文件反复失配。

### 10.1 Inspector 配置与禁止硬编码

**凡是设计者需要调整、替换或接线的内容，必须能通过 Unity Inspector、配置资产或对应 Unity 编辑器资产操作，不要求改 C# 或重新编译。** 当前只发布规范，不表示下表配置组件已实现；后续实现时按此验收。

| 类别 | 必须提供的编辑入口举例 |
|---|---|
| 玩家/技能 | 初始生命与基础攻击、速度、跳跃、冲刺距离/时长/冷却、判定范围与检测 LayerMask |
| 战斗/危险 | 敌人生命/伤害/速度、攻击节奏、巡逻点、感知距离、陷阱伤害间隔与击退、保护时长 |
| 奖励/代价 | 参与首版的条目列表、效果数值、标题/说明/图标、统一奖励与负面配置 |
| 世界 | 传送目标 Transform、安全落点参数、开关控制的门引用、宝箱配置、精英/出口引用、稳定交互 ID |
| UI/表现 | UI 引用、图标/素材、提示文本、颜色、动画时间、镜头参数；可用对应 Prefab、Animator、AnimationClip 编辑 |
| 音频 | 项目 Wwise 适配器中的 Cue 到 Event/资源映射及相关配置；不得把 Event 名散写在 gameplay |
| 输入 | Input Actions 资产中的按键与绑定，由授权输入任务统一维护；不在业务内硬写 KeyCode |

仅暴露本模块实际使用的参数，不凭表格提前实现首版之外的功能。模块成员可以自主组织字段和配置类。

配置与规则的区分：

- 速度、伤害、时间、距离、比例、颜色和对象引用等设计参数不写死在 Update、结算方法或分支中；使用有语义的配置字段，业务读取实际配置。
- 已确认的 20% 当前生命代价、10% 上限代价、0.5 秒抵达保护等也应有统一 Inspector 参数，默认值保持确认值。用户可在测试中调整；未经玩法更新，不把偏离确认值的资产作为正式基线交付。不得用 Awake 重写或静默 Clamp 回确认值，使 Inspector 看起来可调却实际无效。
- 构筑容量 3、普通奖励/代价三选一、教学/指定舍弃单卡、身体权限映射和不可重复属于已确认结构规则，本轮不要求任意改变其结构。可集中为命名常量并在 Inspector 提示，但不可复制魔法数字到各模块或提供无效的数量滑条。后续用户改变规则时同步接口/UI/验收。
- 稳定枚举 ID、数学公式结构、算法中的 0/1、集合索引等可以留在代码；不要为“全部可调”把所有局部变量序列化。
- 普通/支线统一池等概率无放回，无再生优先保留位；代价不强制舍弃类别。当前favorRegrowth仅为待迁移旧实现，不是规则例外。普通奖励/代价三项、指定单卡分别验收，未实现项不入池。

### 玩家射线地面检测与脚步节奏（保留既有 G33–G34 技术要求）

T06 运动组件持有唯一地面检测结果；使用 Physics2D.Raycast（2D），在 FixedUpdate 中按扫描周期检查脚下，默认扫描间隔0.02游戏秒。间隔、射线起点/偏移/长度、地面LayerMask与移动阈值在Inspector配置，排除玩家自身及不作为地面的Trigger；可用多条脚底射线改善边缘判定。默认物理步长应核对，若不是0.02，不擅自修改全局设置，由总控协调；扫描不是键盘采样，键盘仍遵守IPlayerInput的Update缓冲/FixedUpdate消费。

落地/跳跃与脚步复用运动组件的真实地面结果，不由脚步另造一套grounded。脚步播放要求存活、Playing、已落地且实际水平位移/速度超过可调阈值；按住键却顶墙不播放，空中/静止/Paused/Choosing/Dead不播放。0.02秒是检测频率，不是发音频率；用独立可调的脚步播放最小间隔及步距/节奏去重，移动或恢复时不补播积攒的脚步，依据实际素材试听确认不会重叠成为机枪。节奏参数暂定测试值，不冻结所有素材的统一间隔。

当前没有脚步材质Switch Group，不要求材质分类、Switch或按地面Tag选Wwise事件。程序通过GameAudio统一Cue入口；现有AudioCue尚无脚步身份，T06/T18接音频前由总控串行发布稳定Cue及映射需求，不在gameplay写AK类型/Event字符串、不冒用PlayerJump。当前 T06 已有射线运动独测，地图仍用 WhiteboxPlayer2D；没有脚步 Cue 或实际声音验收，不因本轮文档更新宣称已接通。

### 10.2 配置存放、引用与校验

1. 单对象少量参数用 `[SerializeField] private`，配合 `[Header]` 分组、中文 `[Tooltip]` 说明含义、单位、范围与生效时机；按语义使用 `[Min]`、`[Range]`。不靠 public 字段让其他模块随意写。
2. 多对象共享或成组参数可用 ScriptableObject，提供 `[CreateAssetMenu]` 和实际 `.asset/.meta`。配置建议放 `Assets/Configs/<Module>/`，Prefab 放 `Assets/Prefabs/<Module>/`；目录为建议，不为改现有合理布局批量搬资产。避免把同一伤害/比例复制到几个组件里。
3. 运行时从配置初始化真实状态；当前血量、已领取宝箱、已开门、随机卡组、累计负面和当前姿态不作为可随意写的设计配置。不在 Play 中修改共享 ScriptableObject 保存本局状态；需要副本时显式复制。
4. GameObject、Transform、组件、Prefab、Sprite、配置等使用序列化引用或总控显式注入，不依赖硬写对象名、Scene 路径、Find 或 Resources 路径。同物体必要组件可用 RequireComponent/GetComponent 做局部绑定，不要求每个本地组件手拖；跨物体依赖必须可定位。Unity 接口不直接当普通序列化字段；可用具体组件或 MonoBehaviour 引用并在初始化验证其实现的接口。
5. OnValidate 只做轻量字段数据校验，不创建对象、不执行伤害/领奖/传送、不扫描场景或修改其他资产。运行初始化/提交时仍检查空引用、非法范围、重复 ID 和候选不足，给出带组件/资产上下文的错误；校验遵循新表的攻击下限与线性叠加，不能静默Clamp后伪报提交成功。
6. 序列化默认值可写在字段声明以便首次添加组件；它不是最终资产交付。必须创建/绑定实际配置或 Prefab，不在 Awake/Start 重置已配置字段，不用缺失配置时的隐藏默认值假装接线成功。可选依赖标注清楚，必要依赖缺失只禁用受影响功能并提示。
7. 默认参数在非 Play 模式保存到正确 Prefab/配置资产，检查 override；新增/重命名序列化字段保留现有数据，必要时使用 FormerlySerializedAs。不得靠换字段名让已调好的值丢失。
8. 说明哪些修改实时生效、哪些下一局/重新启用才生效。初始化型参数可缓存快照，但须明确时机；测试者退出 Play 后保存正式调整，Play 中修改一般不能当作已持久保存。

字段格式示例（只是格式示例，不冻结移动速度，也不是已有组件）：

```csharp
[Header("移动参数")]
[SerializeField, Min(0f)]
[Tooltip("移动速度，单位/秒；暂定值，重新进入 Play 后生效。")]
private float moveSpeed = 5f;

[Header("场景引用")]
[SerializeField]
[Tooltip("传送抵达位置，拖入目标锚点；提交前仍需检查落点。")]
private Transform destination;
```

Unity 6.2 官方依据：[SerializeField](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/SerializeField.html)、[ScriptableObject](https://docs.unity3d.com/6000.2/Documentation/Manual/class-ScriptableObject.html)、[OnValidate](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/MonoBehaviour.OnValidate.html)。

### 10.3 模块配置验收

模块交付时，handoff 写明“在哪个 GameObject/Prefab/配置资产调整哪个参数，单位和默认值，何时生效，哪些引用必填”。不能只交脚本，再让不熟悉 Unity 的成员猜接线。

实际验证至少覆盖：选择正确对象能找到参数；改变一个代表性的设计参数并进入 Play 能观察到变化；缺失必要引用时提示可定位；非 Play 保存后重开/另一份 checkout 配置仍在。若无法运行 Unity，清楚记录哪些未验证，不用编译成功代替 Inspector 验收。

### 10.4 美术资源接入与Codex自主迭代

美术素材由负责成员在自己的电脑/分支准备、导入和交付，其Agent以该工作副本实际素材和本机授权为依据，不要求先上传到总控电脑，不照搬总控的盘符或素材绝对路径。本文的总控工作目录用于定位本机会话；队友核对自己的项目根目录、分支和基线即可。交付使用仓库相对路径、资产引用与.meta，使其他电脑取得相同版本后可继续工作。

**本节约束可集成的结果，具体制作方法由成员和其Agent自主决定。** 在已授权美术/表现模块内，应持续完成“查看素材 → 选方案 → 导入/接线 → 预览或运行 → 发现问题 → 修正复验”，无需每调一个参数、增一个辅助脚本或换一种局部方案都问总控。无需等待全部素材齐备；先做好现有资源，缺失部分保留可辨认占位并记录。素材用途清单可边做边完善，写入本人handoff即可，不要求先交完整表格才能开工。

| 可自主决定并迭代 | 结果要求 |
|---|---|
| 目录/命名、素材筛选与局部整理、裁切/切片、透明边缘修复、适配性加工与变体 | 保持约定风格、来源可追溯及引用完整；目录示例不是强制布局，已有被引用资产移动/重切时维护GUID和子Sprite引用 |
| PPU、Pivot、滤镜/压缩、材质、排序、帧率、转场、配色和局部特效 | 按实际素材和镜头试验后选用；检查清晰度、尺寸、朝向、遮挡及必要性能，不冻结统一参数 |
| 分层Sprite、组合帧、整图状态切换等角色表现方式，Animator结构和辅助表现脚本 | 准确表现真实身体组合与动作；不强制某一种子物体层级、脚本拆分或动画方案 |
| 本模块Prefab/配置、UI布局/字体/图标、编辑器批处理和预览工具 | 保持既有交互和状态语义，调参/引用可定位，工具能在交付环境使用，不引入隐蔽机器路径依赖 |
| 发现视觉/接线问题后的局部重构、方案替换、对比试验和自我修正 | 自行迭代到可用并记录实际结果，清理本任务已确认无用的试验产物；不覆盖他人改动或删除仍被引用的资源 |

优先复用队友已提供的素材，裁切、修色、排帧及必要适配属于上述自主范围。若任务包含补绘或生成素材，可按该授权完成；改变整体美术方向或扩大任务范围时再协调。不因普通导入/表现调整要求逐项审批，也不把任意新依赖、第三方上传或对外发布视为默认授权，仍遵循原有工具、文件和发布约定。

保留以下共同边界：

- **玩法与尺度**：美术迭代不改变玩家各身体组合的稳定碰撞体、基础通行能力、已确认地图尺寸或交互规则；怪物碰撞体积与可见身体大小一致。保持平台边缘、落点、地刺和交互提示可辨认。显示层级可自由组织，物理/交互数据不能被视觉缩放意外改变；门、地刺与地形实际约束见第3.5节，不额外锁死装饰与视觉实现。
- **真实状态与动作**：身体、HUD和动画读取唯一状态，双手连剑同得同失、三槽保持通用槽，未实现能力不因有图就投放。表现脚本可自行组合现有只读接口/通知，也可用稳定业务ID建立本模块图标映射配置；当前ChoiceOption无图标字段不阻止局部UI适配。只有需要改变公共签名、共享状态写口或动作结算时才协调，不从标题/贴图反推真实玩法。不得重复扣血/领奖/舍弃或另设输入、时间、库存控制器；正确清理取消/死亡/失去能力后的表现。音频契约保持第9节不变。
- **共享资产协作**：本模块内部自主迭代；主地图、共享玩家Prefab、公共接口/输入/项目设置等按第11节协调写权，已明确授权的改动可直接继续。缺共享接点时先在模块Prefab或独立预览场景完成可验证部分，再交具体接线方案，不停下整个任务。文件移动/替换/清理维护.meta、GUID及实际引用。自主迭代不另行改变现有Git与并行规则。

交付保持精简且足够接手：本人handoff记录实际资源相对路径/用途、Prefab或预览入口、必要导入/调参信息、依赖与未接部分，以及真实验证结果；只记录该任务涉及的参数，不要求逐项填满所有可能字段。按改动验证相关的身体组合/动作、碰撞边界、排序、UI可读性和保存重载引用；能运行则在成员电脑完成预览/Play并自行修复，缺运行环境就明确未验证部分。成员独测可完成后交付，不要求总控电脑先收到素材才算本人完成；总控取得提交后另记主场景集成验收。截图只证明所展示的效果，不把美术完成等同于未实现玩法完成。

## 11 总控和 Codex 并行规则

- 总控发布基线（当前来源 Ming）、本文、公共接口/枚举、共享状态入口、主场景/共享玩家 Prefab、Input Actions、Layers/Tags、Packages、ProjectSettings 由总控协调串行修改；总控可明确委托某任务，不要求所有代码由总控亲写。模块自有 Prefab、测试场景与局部配置可自主编辑。
- 每个任务从总控指定的已发布提交创建独立分支（建议 codex/ 前缀），明确文件范围和验收。记录来源分支与提交；个人旧分支或 main 不自动等于最新可用基线。
- 同工作目录默认一个写入会话。确需多会话写入，用独立 clone/worktree 与分支，Unity 打开对应项目副本。Library 不共享、不提交。
- 文件隔离不解决接口冲突；冻结公共契约再并行。不同任务不得同时改同一 .unity/.prefab/.asset，不同时切分支/merge/reset。
- 模块交独立 Prefab/测试场景，总控在已交付地图场景接入运行组件。不盲拼 Unity YAML、不用 ours/theirs 丢掉一方。冲突由资产负责人在 Unity 重做并验证。
- Assets 新增/移动/删除配对 .meta、保留 GUID；移动优先 Unity。Hierarchy 对象在非 Play 模式保存后检查 diff，没保存不会 push。
- 不自动 commit/push、不强推、不覆盖用户已有改动。不自动启动子代理；用户要求时也需明确文件边界。
- 不把 38 小时当作免测或扩范围理由。先编译/短闭环/首次导出，再整局，再表现，最后预留打包缓冲。功能冻结后只修阻塞/严重错误；删已确认核心需询问用户。

### 11.1 按最小可测试功能派发

- 总控负责共享接口/状态/输入/运行阶段的逐项发布、任务登记、场景集成和 Windows 验收；可明确委托实现工作，但保持共享入口的唯一维护者。
- 除总控外，不按固定人数划分整块“玩家系统/敌人系统/UI 系统”。任务以一个能实际演示的结果为单位，例如一个开关开一扇门、一个菜单完成确认取消、一次咬击命中靶子。不得只交无接线的孤立脚本。
- 默认每位成员同时一个开发中任务；完成后可领取不同功能。总控登记任务 ID、成员/别名、分支与基线、依赖、目录范围、验收和状态。共享文件发生交集时串行；任务编号不同不自动表示可以并行。
- 状态区分未领取、开发中、待验收、独测通过、已集成；阻塞记录具体缺口。只有使用真实依赖并进入主场景验证才标已集成。成员更新自己的 handoff，总控更新任务登记，避免多人编辑同一个任务清单。
- 已有接口可使用任务内的测试替身、靶子或驱动实现独测；测试代码限定该任务范围、与正式入口隔离、测试场景不进正式构建，不复制公共接口或把替身挂入主场景。交接列明替身和替换方式。
- 在真实输入/写事务接口尚未发布时，可以先做不依赖它的任务；不让成员各自设计第二套正式状态或结算系统。待发布的 API 在任务中标依赖，发布后以 AGENTS 与实际源码为准。
- 每项交付包含源码注释、Inspector 参数/引用、所需 Prefab/配置/meta、独立测试场景、可复现操作及个人 handoff。接口/玩法不变时内部实现继续自主决定。
- 首次分发前总控应发布包含所需文件的 Git 提交并提供提交号。未提交的新文件不会因队友 pull 而出现；本会话仍不自动提交或推送。

分阶段验收：第一阶段先完成每人领取的小功能验收，再由总控在短灰盒验证跑跳、咬击靶子、开门、真实状态 HUD、菜单独测及可启动 Windows 导出。菜单测试回调不能冒充正式奖励/扣费结算。完整地图与完整音频不作为第一阶段开工前提，最终交付仍须验证。

每个成员按所领取的任务卡逐项验收，交付可复现操作与个人 handoff；只有编译或脚本不足以标独测通过。总控维护单项验收/集成记录，全部第一阶段门槛通过才标阶段通过。第二阶段核心玩法与后续整局任务先保留候选，迁移阶段门槛通过后依据实际代码、人数与规则拆分和派发，不提前固定成员。具体门槛与登记表见 docs/WORK_PACKAGES.md。

### 11.2 独立地图场景交付

地图正在制作，用户已确认以独立地图场景交付；V5 两条支线均由负责地图制作迭代的同学制作。程序负责公共挑战逻辑/模块独测，总控负责接口与接线验收，不再另派一人制作重复地图。沿用地图作者的场景，不要求改成地图 Prefab；无需因此引入多场景加载框架。总控以交付场景接入运行组件，具体路径由地图 handoff 登记。

制作期间地图作者维护地形、碰撞和摆放；程序只在独立测试场景验证。交付后由总控接线；之后还需改图时协调交还编辑权，同一个 .unity 文件不能两人同时改。需要提前试走时可使用测试副本，但不将副本变成第二份长期主地图。

地图交付按SIDE_LEGLESS_PUZZLE/SIDE_ARMLESS_TRAVERSAL标出入口、目标/终点、步行出口、持久机关和普通奖励位置；不建FallFailZone、安全回传点或局部复位组。普通门2×6/6×2、当前InspectionDoor1×5保持；交接列出生点/地形/传送安全落点/按钮目标/敌箱陷阱/精英出口/相机边界。程序显式引用，地图不另写HP/奖励/扣费；Layers与物理设置总控统一。

先用短区段验证角色尺寸与基础跳跃可达性，避免地图完成后才发现必须持有可舍弃技能才能通行。已开始制图不自动确认旧提案中的箱数、门关系和收费连接；按实际交付与用户规则核对。

### 11.3 程序测试构建与最终封装分工

用户明确新版T仍未全部提交；C05可以先验证已有组件、补复验入口和Windows测试构建，不等待无关T。后续模块只等待自己的真实接口/资产/独测交付，仍按新版本逐项验收，不把旧T完成记录沿用。

最终交付由音频同学负责封装，美术由其他成员负责。总控提供程序提交、场景与构建入口、已验证范围和缺口；音频同学在素材齐备的电脑完成Bank/映射/平台资源接入与最终包验收。本机Development测试包不替代最终封装、音频试听或另一台Windows验收。原第9节音频接口/资源要求不变，不能以对方有素材为由省略可复现的运行依赖交付。

## 12 handoff 与任务授权模板

每位成员使用 `docs/handoffs/<username>.handoff`，UTF-8 Markdown；同用户名并行用 `<username>.<task-id>.handoff`。别人的交接只读；用户名未知用任务别名，不拿系统账户当人员身份。handoff 不能批准规则或当文件锁，代码/Git 与记录不一致时报告给总控。

这是强制交付，每位成员在 docs 文件夹下保有自己的交接文件，统一使用现有 docs/handoffs 子目录，不另建 doc 目录。开始任务先读取自己的交接与相关依赖，再创建/更新自己的文件；名字未知先用稳定任务别名，不因尚无全员名单停止工作。

总控每轮完成代码或 Unity 编辑/验证工作后，必须同步更新根 AGENTS.md 的实际接口/接线状态、docs/WORK_PACKAGES.md 的依赖/验收与任务状态、docs/handoffs/controller.handoff 的实施事实和下一步，随对应源码/资产交付；未实现、未发布、未验证分别标明。成员更新自己的 handoff，公共两份文档的变更由总控串行汇总，避免多人冲突。

每次有实际代码、资产、配置或验证进展的任务结束前更新 handoff；规则未定或工作未完成也记录当前状态，不能只在整个模块完成后补写。文件顶部保留最新可接手摘要，必要时在下方留简短历史；同一成员持续更新，不按每轮对话创建新文件。

交接至少让下一位 Agent 回答：完成了什么、入口在哪里、依赖哪些真实接口、Unity 怎么绑定与调参、验证了什么、哪些未完成、下一步做什么。写明分支/基点/日期、规范版本与源码契约版本；缺绑定或失败不可标完成。handoff 按正常 Git 流程随对应代码交付。

```text
成员/任务别名：
任务 ID / 分支 / 基线来源分支与提交 / 规范版本 / 源码契约版本：
状态：进行中 / 待验证 / 待集成 / 完成
授权目标及允许文件：
禁止文件：
实际修改及职责：
对外接口与调用例：
Unity 测试场景、Prefab、Inspector 绑定：
参数入口（对象/资产路径、字段、单位/默认值、必填引用、生效时机）：
实际执行的验证与结果：
未验证/缺绑定/规则缺口：
下一步及总控需集成的内容：
```

总控分发任务时附：模块目标、已确认规则、授权目录/共享禁改范围、实际公共接口、接线要求、成功触发的 AudioCue、验收步骤。允许成员自主新增完成模块所需的局部文件；接手 Codex 先读本文与依赖交接，不跨模块改写真实状态。

## 13 历史验收与整局发布要求

当前已提交基线Ming/0d892a4（契约8，含C04/C05和美术规范）。本轮新增菜单Editor检查69/0、独立Windows检查69/0、两种Windows测试包构建成功；完整画面/输入/声音/整局和异机验收未做。以下为此前地图与独测历史结果。新图C05 Play 69/0、真实E开新增箱及Esc取消通过；静态接线与几何保存重载核对见交接。C04的17/0、C01/C02/C03的32/69/63及T12适配33为前轮结果，本轮未重跑，不当新版T验收。旧T/白盒/HUD历史数字保留在controller.handoff。完整路线可达性、战斗、挑战结算、Bank和Windows导出尚未验收。

V5 迁移每项独测后再集成，不继承旧四槽/致死支付/站立保留剑的断言。必须包括：

1. 头部无攻击且安全；取消教学无状态变化，确认一次初始化躯干/生命/咬击并开出口，不能跳过。
2. 身体/技能共用三槽；手剑一项、腿一项、尾一项；有手剑击、无手咬击，无腿也可剑击；碰撞和基础参数不变。
3. 新局三个空槽、无Arms；教学躯干不占槽。宝箱确认领取Arms才占一槽/可剑击，预览取消不变，移除后释放槽并恢复可用咬击；满槽三旧项/换尾单卡原子提交，历史仅成功写入。
4. 普通/支线统一七项池、无再生保底或固定强化包；真实三项、满血回血可见、跨箱只修失效位；按新百分比公式验上限同额回血与逐次取整。
5. HP=1 / MaxHP=1 灰卡全输入及服务拒绝，2→1 可支付；低当前高上限按正确公式；没有代价死亡事件或免费层间兜底。
6. 构筑舍弃项+五效果随机三项，不强制舍弃，全灰默认修一位；HP/攻击下限双重拒绝，取消不刷、成功后重抽；无效落点/重复提交无扣费。
7. 无强化能力完成必要主路/返程/基础战斗；门每个独立翻转且不夹人，普通门2×6／6×2、条件门和地刺按已交实例、地面与怪物碰撞约束不变。
8. 两支线各自合法入场、指定单卡舍弃/已缺明确确认后免费进入；未经入口直达终点不完成，补回限制部件使未完成尝试失效但可退出。
9. 地刺按新规则扣5与共享保护/击退；封闭图无落坑扣血或安全点回传；死亡优先于同帧完成。
10. 支线成功机关不复位；完成/普通随机领奖各一次且往返保持，恢复部件后可步行退出；两条都用基础移动/一段跳可达。
11. 敌人强化含精英/未来生成、保持 HP 比例且不复活；精英死只解锁出口，存活进入才赢，死亡优先。
12. 新局清身体/历史/世界/候选/负面/挑战/锁，从头部教学重来；无音频后端可玩，有后端和 Windows 导出在队友电脑验收。

## 14 历史C06前验证与待迁移目标（最新实现见§6.4）

当前Ming/05c082e + 未提交Soap/29e59ef试合并；Unity6000.2.9f1，目标Windows64。下列结果在收到新战斗表前已执行，本轮只核对源码和更新文档，没有重新跑Play或构建。

| 范围 | 最近结果 | 结论边界 |
|---|---|---|
| T07独立场景 | 42/0（首轮失焦32/10，聚焦全新Play复跑） | 真实输入/咬击/权限/去重/配置/生命周期；模拟按键，不是人工试玩 |
| T08独立场景 | 52/0 | 真实Arms命令与咬剑分发、实时伤害、敌死；驱动自动加手用于测剑，没有通过宝箱领取 |
| T09独立场景 | 50/0 | 站桩敌HP/接触/死亡碰撞视觉/注册；未验证新AI、共享保护 |
| C05当前地图 | 69/0 | 现有9箱/三槽/2入口旧业务兼容；尚无地图攻击组件，不覆盖新门永久消失目标 |
| 辅助检查 | 9程序集独立编译、15份新资产静态引用检查 | 不替代Unity Play或新参数验收 |

详细场景、按钮、操作及失败记录见WORK_PACKAGES§16和controller.handoff。旧C05 Windows69/0属于05c082e工具验证，Soap战斗未重新导出。正式动画/Bank出声/人工手感/整局均未验收。

当前要复用卡牌、状态与HUD，先发布共享数值/动作接点，迁移T07/T08时间轴与共用冷却，再补敌人/伤害与新奖励。获准接图时验“新局空槽→教学躯干仍空槽→宝箱确认Arms→槽位/剑击→移除恢复咬击”，不能复制T08自动授予夹具。首个随机箱不保证有手剑；固定候选只用于隔离验收，不改正式池。

本轮没有修改生产源码、配置、场景、输入或Wwise；规范仍20、共享契约仍8。历史地图/Windows构建详情保留在WORK_PACKAGES历史§10–15及controller交接；当前正文不继续要求再生保底、支线固定强化包或落坑回传/局部复位。
### Dada资源合入当轮记录（后续已由C07接主图，见§6.4）

实际来源Dada/575eff3（包含478c354与575eff3两个新增提交），新增252路径，未覆盖既有主场景/共享源码/输入/Wwise。主角39张PNG、95帧、52个Clip（39动作+13临时Idle），库与独立场景位于Assets/Art/Characters/Player/Controllers/PlayerVisualLibrary.asset及Assets/Scenes/Tests/Art/PlayerVisual_ArtTest.unity。另实际带入3张敌人PNG与3个Clip；Dada文字称未交敌人与实际Git不符，以资产为准并记录，未挂真实敌人。

仅资源接收，不将Regrowth.Tests.Art中的预览Toggle/Harness视为正式状态或挂主玩家；正式动画需另接唯一PlayerState、现运动与攻击通知，不新增伤害回调。两种无手火尾Bite和有手有腿火尾Fire缺图；不以喷火姿势代用咬击。Idle为Move首帧临时复用。火球及受击/死亡/冲刺等正式方案仍未交付，不因有角色图就开启技能。

本机Unity6000.2.9f1 ValidateAssets实测1140/0；42张PNG均真实文件，主角39张清单哈希一致、meta配对完整、全Assets GUID无重复。未运行Dada完整Play/美术验收、未接主图或导出Windows；Dada本人890项Play记录不能冒充本机本轮结果。用户当前明确分支合并授权优先于Dada交接中仅cherry-pick的个人工作流。玩法/共享源码契约不升级。