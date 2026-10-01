## 2026-10-01 卡牌正文居中与真实交互键提示修正

- 修复来源：CardVisual迁移器仍写入TopLeft，正文因此整体左偏。现在运行时与原生Prefab/ArtTest均使用水平居中，标题Center、正文Top Center，清零不对称margin；保持标题24/正文22、共享局部坐标和根旋转。
- PlayerHud新增InteractionHintView，显式只读唯一PlayerInteractor的IInteractionState/TargetChanged。底部中央显示32pt高对比`[E] Open chest`或当前目标Prompt；兼容现有`[E]`及`E - ...`前缀，避免重复键名。
- 提示只随正式可交互目标显示：无目标隐藏，E开Choosing隐藏，Esc取消恢复，离开范围隐藏。没有自行检测距离、消费输入或调用交互/奖励接口。旧白盒调试说明保留。
- 只做这两项的短Play：实际A走到Chest03，检查提示→E开卡→正文/标题居中→Esc恢复提示→D离开隐藏；未重复奖励、战斗或全图流程。首轮实拍发现源提示已自带[E]，修正去重后复核；最终10项通过、运行Error=0。自动输入不是人工验收。
- 更新ChoiceCard/PlayerHud、UI_ArtTest、Level中PlayerHud只读绑定、UI表现脚本。原生保存的Tilemap重排不带回正式项目；地形/敌人/事务不变。
- 原生截图：`docs/art-test/ui-preview/real-integration/Cards_CenteredText.png`、`InteractionHint_NearChest.png`；短检查日志：`docs/art-test/UI_ALIGNMENT_PROMPT_CHECKS.txt`。本轮未commit/push，未全局操控。

# Dada UI 集成交接

## 本轮结果

以现场 fetch 的 `origin/Ming 3c0bae7` 为业务基线，Dada 安全快进同步；原184个未提交文件已备份并保留。工作期间另一任务提交了敌人美术预览 `6f1761b`，该提交及其交接保留。本任务未 commit / push。

正式 ChoiceCard、ChoiceMenu 和现有 UI_ArtTest 共用正面卡框与布局。新增正式 MainMenu；PlayerHud 继续读取真实 PlayerState。没有改奖励、传送、敌人、玩家状态或攻击规则。

### 卡牌修正

- 唯一基础 Frame：`Assets/Art/UI/Cards/UI_Card_Frame_Center.png`。Left/Right PNG保留为参考，正式卡牌不引用。
- CardVisualStyle 不再提供每列独立框图、尺寸或文字偏移；只有根角度 `+4 / 0 / -4` 区分三列。
- 三列根节点同一高度。标题24pt、正文22pt，关闭Auto Size，卡内没有LayoutGroup或ContentSizeFitter。
- 固定布局：根415×614；TitleArea中心(0,-220)、280×58；DescriptionArea中心(0,-119)、280×108；IconArea中心(0,45)、280×430.5。各文本/图标在自己的Area中央，局部旋转均为0。

```text
ChoiceCard（CardRoot；原Button / ChoiceCardView保持）
├─ SelectedGlow
├─ Frame
└─ ContentArea
   ├─ IconArea / Icon
   ├─ DescriptionArea / DescriptionText
   └─ TitleArea / TitleBannerText
```

**像素证据**：复制正式Prefab，使用同一文案/图标、同一位置，三个根设rotation=0；三次1920×1080原生Unity渲染RGB数据逐字节完全相同。恢复左右±4°后，所有子节点RectTransform快照仍完全一致。证据见 `ui-preview/real-integration/Overlap_Zero_0.png`、`Overlap_Zero_1.png`、`Overlap_Zero_2.png`、`Overlap_RotationOnly.png`。

### 真实前端与边框

- 正式入口：`Assets/Scenes/Frontend/MainMenu.unity`。Start按钮实际加载 `Assets/WhiteBox/Scenes/Level_Whitebox.unity`。
- 标题/背景/走路角色复用现有资源；走路使用Dog2_Move_Sword三帧动画。
- Editor中未登记Build Settings时使用Editor场景加载。**正式打包前仍需总控将MainMenu和Level_Whitebox登记到构建场景列表，并选择MainMenu为入口**；本轮未修改共享构建设置。
- `PlayerHud/GameplayEdgeOverlay` 属于常驻Gameplay Canvas，独立于ChoiceMenu.viewRoot。复用现有 `UI_Login_Edge_01/02/03` 三张实际边框纹理；`UI_HUD_Edge.png`静态旧资源保留。Login与Gameplay各有自己的动画实例和时钟。
- 每帧1.2秒，8秒轻微呼吸，使用unscaledDeltaTime，RaycastTarget=false。E开卡/Choosing/timeScale0/Esc取消均不重启时钟。
- 旧白盒操作/交互提示移到右下方，避开左下身体HUD；保留原读状态和暂停逻辑，未另造提示数据。

## 图标与精简文案

20张PNG均在 `Assets/Art/UI/Cards/Icons/`，保留原文件名和GUID。下表是明确映射，未按关键词模糊猜图。动态数值从真实ChoiceOption.Description提取，不在UI计算奖励或费用。

| 正式option ID | 图标关键词 | 卡面短文案要点 |
|---|---|---|
| legs | GrowLegs | Double jump. / Uses 1 slot. |
| arms | GrowArms | Arms and sword. / Slash attack. / Uses 1 slot. |
| tail | GrowTail | Gain a dash. / Uses 1 slot. |
| flame-tail | GrowFlameTail | Fire orb (Q / RMB). / Replaces tail / dash. / Uses 1 slot. |
| heal | RestoreHP | Restore N HP.；满血时明确Already at full HP |
| max-health | MaxHPUp | Max and current HP / +N. |
| attack | AttackUp | All attacks +N. / Includes fire ticks. |
| 201 / 202 / 203 / 204 | LoseLegs / LoseArms / LoseTail / LoseFlameTail | Replace对应部件，Lose… / Gain chosen reward. |
| COST_SHED_LEGS | LoseLegs | Lose legs / and double jump. |
| COST_SHED_ARMS | LoseArms | Lose arms and sword. / Return to bite. |
| COST_SHED_TAIL | LoseTail | Lose tail / and dash. |
| COST_SHED_FLAME_TAIL | LoseFlameTail | Lose flame tail / and fire breath. |
| COST_CURRENT_HP | PayHP | 实际扣血值、百分比、HP前后值 |
| COST_MAX_HP | MaxHPDown | 最大HP与当前HP前后值 |
| COST_ATTACK | AttackDown | Bite / Sword / Fire每跳前后值；保留最低伤害禁用原因 |
| COST_ENEMY_HP | EnemyHPUp | 全部当前/未来敌人增血，保持当前HP比例 |
| COST_ENEMY_ATTACK | EnemyAttackUp | 全部当前/未来敌人接触伤害增加 |
| enter（现有精确舍弃文案） | LoseArms / LoseLegs | Enter Challenge，明确舍弃部件、保留基础动作 |
| enter（无需舍弃） | 无 | 通用进入确认，没有据此猜一张部件图 |
| swap-tail | 无 | 明确换尾单卡；现有数据未提供唯一图标身份，保持无图 |

RestoreBody：素材已有，但主图没有正式头部教学入口/对应正式请求映射，保留ArtTest预览。

RegrowLegsAndHeal、MaxHPUpEnhanced、AttackUpEnhanced：素材保留在VISUAL ONLY图标图库；最新Ming已取消固定强化组合包，未加入正式卡池。

当前主图回血为ceil(maxHP×20%)，上限增长为ceil(maxHP×10%)并增加同额当前HP，攻击固定+10。运行时显示真正算出的N。旧固定25回血/+15上限请求若仍由历史测试提供，保留其自身语义，不改成百分比效果。未知ID/未审核的新描述保留原文，需下次审查文案是否仍适配安全区。

## 实际验证与限制

本轮全部为隔离Unity项目中的原生Play和Unity虚拟输入，未全局操控电脑。**自动输入的真实链路通过，不冒充用户已经人工验收，也不是整图通关。**

1. MainMenu的实际Start按钮进入Level；动画角色读取三个不同帧。
2. 从出生点按A，玩家实际走到(-15.22,7.77)，交互器选中whitebox-chest-03；按E经过正式输入/交互器/Chest/Coordinator打开三卡。
3. 键盘、手柄导航和Esc取消实际通过；Cancel保持生命和领取状态。原鼠标PointerClick进入Button/ChoiceCardView，按原ID提交真实奖励。
4. 边框实例及时间不重置：Choosing中t=2.530→6.397，帧2→0→1→2，timeScale=0；取消后t=7.883→9.640继续，timeScale=1。完整日志在 `UI_REAL_FLOW_FIRST.txt`。
5. 远处箱子用**位置夹具**覆盖接线：9个实际Chest通过E打开，真实卡片依次取得Tail、Legs、Arms、FlameTail；换尾进入真实swap-tail单卡。四种身体状态从实际奖励事件刷新HUD，无授予部件命令或测试Toggle。不是正常走遍9箱的人工路线。
6. 实际Bite/Sword动作事件已观察。首轮Fire测试紧接剑击被已有动作锁拒绝，修正测试等待时间；补测从实际宝箱领取FlameTail后Q触发Fire AttackStarted=1且IsFiring=true。
7. 定位到实际Enemy_06接触范围：真实EnemyContactAttack使PlayerState HP100→90，HealthChanged=1，黑色数字与Fill同时下降。未直接调用伤害接口伪造此证据。
8. 三卡归零渲染逐像素相同；恢复左右旋转，子节点参数相同。
9. 初轮截图清理顺序产生13条测试工具RenderTexture错误，已解除Camera.targetTexture后再释放。补测22项关键检查通过、运行Error=0；最终编译/保存成功。编辑器启动另有既有长路径PackageCache DLL与账户授权诊断，未修改第三方或把环境信息隐去。
10. 依用户要求停止多分辨率/全图标遍历与重复链路测试。传送九费用及条件门本轮只有代码/映射核对，未逐门实际Play验收；键盘Enter/手柄确认仍保留原绑定，本轮新增实测覆盖导航与Esc，不重跑旧全套事务测试。

## UI完整状态表

等级：REAL STATE=读取正式状态；VISUAL ONLY=测试数据；BACKEND MISSING=真实闭环缺失。REAL INTERACTION人工验收留给用户，自动真实链证据在上一节列明。

| 功能 | UI是否存在 | Backend是否存在 | 真实入口 | 本轮验证级别 | 缺什么 |
|---|---|---|---|---|---|
| Main Menu | 新正式场景 | 场景加载有 | Start Game | 自动实际点击通过；人工待验 | 构建场景登记 |
| Gameplay Edge | 常驻三帧 | unscaled动画 | 进入Level | 真实E/取消期间连续通过 | 用户确认1.2秒节奏 |
| HP | 骨头框/Fill/黑字 | PlayerState有 | 实际敌人接触 | REAL STATE；伤害链通过 | 用户视觉确认 |
| Body status | 左下身体图 | Body/Loadout有 | 实际Chest领奖 | REAL STATE；四部件领取通过 | 用户组合视觉确认 |
| 3 Loadout | 旧三槽在LegacyReadout隐藏；身体图可见 | 三通用槽有 | Chest增删 | 身体REAL STATE；独立槽条未显示 | 决定是否展示独立三槽条及位置 |
| Chest cards | 新正式三卡 | 正式事务有 | Chest附近E | 自动走到Chest03按E通过 | 人工鼠标/键盘/手柄复核 |
| Full-slot replace | 正式卡Prefab支持 | 三旧项后端有 | 非互斥新项且满槽 | 本轮未人为造第四项；真实换尾单卡通过 | 当前7项池自然覆盖换尾；通用满槽另待适用内容 |
| Portal cost cards | 同一正式Prefab、9映射 | 安全代价事务有 | Portal A/B/C/D附近E | 映射/禁用代码核对；本轮未实测支付 | 人工检查灰卡和数值预览 |
| InspectionDoor确认 | 单卡有，精简现有描述 | 入口许可有 | 门外靠近，非E | 代码核对；本轮未实测 | 通用无需舍弃确认图标/人工复核 |
| FlameTail | 奖励卡和身体火焰标记 | 真实火球有 | Chest→Q/右键 | REAL STATE；实际Q通过 | 原角色部分动作资源缺失仍保留 |
| Head-only tutorial | RestoreBody仅预览 | 身体核心接口有；地图教程无 | 主图仍跳过教程 | VISUAL ONLY / BACKEND MISSING完整入口 | 教学安全区、箱、出口闭环 |
| Damage feedback | 已有角色红闪；新HP刷新 | 真实伤害有 | Enemy接触 | REAL STATE；HP实测 | 正式反馈美术；未额外设计屏幕闪红 |
| Death UI | 无正式结果面板 | Dead阶段有；完整新局无 | HP归零 | BACKEND MISSING完整重开 | 结果美术与Restart/返回菜单流程 |
| Win UI | 无 | 精英实体有；出口胜利闭环无 | 尚无 | BACKEND MISSING | 出口判定/胜利通知/结果UI |
| Pause UI | 旧白盒阶段提示，无正式菜单 | 暂停/恢复有 | Playing时Esc | 现有白盒功能，未新做菜单 | 正式菜单布局与按钮要求 |
| Interaction E prompt | 旧提示保留并移右下 | 唯一Interactor有 | 靠近实际交互物 | REAL STATE；Chest03目标实测 | 需确认后续是否改为物件旁浮动提示 |
| Side challenge status | 无正式进度UI | 入口状态有，完整目标/领奖缺 | 两条件门 | BACKEND MISSING完整挑战 | 目标/完成事件及显示设计 |

## 你自己检查的路线

1. 打开 `Assets/Scenes/Frontend/MainMenu.unity` → Play → 点击Start Game。预期进入真实白盒地图，左上HP、左下身体与黑色动态边框出现。
2. 点击Game View取得焦点，从出生点向左走到Chest_03（箱中心-15.5,8.5）→ E。随机三卡出现，底图仍是实际Gameplay；不保证首箱有手剑。
3. 用左右键/手柄方向切卡，Esc或Cancel退出；边框持续播放。再E，鼠标点击或Enter/手柄确认；只有成功领取才改变身体与能力。
4. Arms后普通攻击为剑；无Arms为咬。Legs后二段跳，普通Tail后Shift冲刺；FlameTail取代Tail后Q/右键火球。
5. 测试真实敌人 `02 Actors/Enemies_C09/Enemy_06`，巡逻在约(-44…-39,13.5)附近；或Enemy_05约(-51,1.5)。地图现在有13只普通怪+1只精英，旧4站桩坐标清单已过时。红色可移动方块是带EnemyBasic的真实敌人，旧Tile_EnemySpawnMarker没有可见Sprite/碰撞。
6. 接触后HP应下降、Fill同步缩短。Fire和普通攻击受已有动作锁/冷却限制，剑击后等动作结束再按Q。

纯视觉入口仍是 `Assets/Scenes/Tests/Art/UI_ArtTest.unity`。页面、三卡/单卡、左右中高光、普通/长文案、Prev/Next图标、禁用态、HP滑条、身体开关、边框开关/定帧、走路播放/暂停全部保留。它们均为VISUAL ONLY，不能作为领取或伤害成功的证据。

## 请与程序/美术确认的位置

| 功能 | 当前能否真实触发 | 建议位置/方式 | 原因 |
|---|---|---|---|
| 独立3槽 | 状态有，槽条隐藏 | 左下身体图上方一行（待确认，未新做） | 同时看身体与容量，不重复占角落 |
| E提示 | 能 | 目前右下；可确认后移交互物旁 | 避开身体图，靠近目标更容易关联 |
| 支线进度 | 完整完成事件缺失 | 上方简短目标条（待确认） | 不遮动作与地图落点 |
| Pause/Death/Win | Pause与Dead阶段有，Win/重开不完整 | 中央Modal，复用暗底（待确认） | 清晰区分停止操作与Gameplay；需先完成相应接口 |

修改重点：ChoiceCard.prefab、PlayerHud.prefab、Level_Whitebox.unity、UI_ArtTest.unity、CardVisualStyle/Catalog及UI表现脚本；ChoiceMenu保留原正式Button/Panel接线。新增MainMenu场景/Controller与显式Editor迁移和验证入口。现有PNG、动作、碰撞、输入资产、Packages、ProjectSettings和其他成员交接未被此UI任务覆盖。

同步时保留420个Tilemap单元的原始序列化数据；原生保存产生的颜色索引重排经逐格解析确认等价后排除。主场景仅接入四个UI组件块，不改敌人/地形/业务接线。最终右下提示位置已编译和原生保存；截图取自位置调整前，未为此重复Play。补测原始记录见 `UI_REAL_REMAINING.txt`。
