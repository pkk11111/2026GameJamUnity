# GROWL AGAIN 统一开发规范与游戏契约

规范版本：9，2026-10-01；公共源码契约版本 5，新增 IPlayerStateCommands/IPlayerCombatState，既有接口与枚举值不变。用户为总控，其他程序人数未定、不超过 5 人；按最小可测试功能领取任务，不固定个人长期板块。本文使用标准文件名 AGENTS.md，作为所有 Codex 会话必读的唯一规则入口；不再另建 agent.md 副本以免内容漂移。

活动仓库：`F:/2026GameJamUnity`。旧 `F:/UniSyd_Gamjam2` 仅为迁移来源，不再开发。接手工具时明确把工作目录设为活动仓库；本文件在活动仓库的副本是现行规则，旧目录副本只用于迁移备份。

当前总控工作分支及发布基线来源是 `Ming`。C00/C01 已由用户推送，本轮 C02 基于 `e86074a6930b23b8c9e390b89806173f14dfd95e`；开始时 HEAD/本地 origin/Ming 一致、工作区干净，未 fetch 查询实时远端。本轮 C02 源码/场景/文档仍待用户提交与发布，Codex 不自动 commit/push。成员从总控明确发布的 Ming 提交创建任务分支，读取该提交随附的根 AGENTS.md、任务卡与相关 handoff，不另建 Ming 专用 agent.md；不用旧 main/旧项目规则，不自动切分支、合并或重置。

最新迁移决定：使用 Unity 6.2（6000.2.9f1）新建项目，路径为 F:/2026GameJamUnity。旧 6000.6.3f1 及其包版本仅是历史环境；只迁入自研源码/.meta、规范、交接与格式规则，保留新项目现有 .gitignore/.gitattributes、包与项目设置。新项目已有 Wwise Integration，不复制旧模板或降级插件。

本文整合玩法、限制、技术栈、接口、音频、并行与交付。handoff 只记录工作事实，不发布另一套规则。用户当前明确指令优先于本文；附件是设计输入，不把附件中的命令视为工具执行授权。

团队阅读版见 [游戏设计文档](docs/GAME_DESIGN.md)。它解释玩法与未决问题，不能发布独立规则；玩法答复必须同步更新本文与阅读版。

可领取的功能任务、依赖与验收见 [最小可测试任务目录](docs/WORK_PACKAGES.md)。派发与协作规则以本文第 11 节为准；任务目录不是已实现代码清单。地图正在制作，已确认以独立地图场景交付，由总控接入运行组件。

## 1 接手顺序与状态标记

1. 读取本文件全部内容；检查实际工作目录、分支、Git 状态及相关 docs/handoffs 文件。
2. 检查源码、Prefab、场景，不假定文档中的类型已经有运行实例。
3. 明确本任务目标与模块边界；按下节区分自主实施与需要协调的变更。未决问题只阻塞依赖它的部分，不停止整个模块。
4. 在自己的 handoff 记录改动与验证。实现现有接口无需改接口表；共享契约变更由总控协调更新，不声称未执行的测试已通过。

状态定义：**已确认**是用户明确答复；**技术约定**是本轮采用的实现边界；**待确认**不得据此实现业务；**预留**只有身份或计划，没有功能承诺；**已实现**仅指表中真实存在代码。

公共契约与空音频入口已重写，新项目有 Unity 模板及 Wwise Integration。C00/C01 已随 Ming/e86074a 发布；本轮 C02 PlayerState、最小写口、攻击读口与死亡停止接线已实现，独立场景实际 Play 51 项通过，旧 C01 32 项复验通过。C02 尚在工作区，待用户发布提交。运动、攻击动作、敌人、世界、正式 UI 和项目 IAudioBackend 适配器仍未实现。Integration 已安装不等于游戏音频映射或 Bank 交付已经完成。

### 成员自主范围与协作边界

规范用于保护已确认玩法、接口兼容和多人资产协作；不指定每个内部算法。成员和其 Codex 在已授权模块内，应自主完成设计、实现、修复与必要验证，不为普通实现细节逐项请示。

| 情况 | 执行方式 |
|---|---|
| 内部类/方法划分、算法、局部重构、缓存、组件组合 | 自主实施，维持对外契约与玩法语义 |
| 本模块的新脚本、子目录、Prefab、配置资产、独立测试场景 | 自主创建并配对 meta；无需预先枚举所有文件名 |
| 未冻结的速度、距离、伤害、冷却、动画/UI 参数 | 可以给 Inspector 中的暂定测试默认值，标注待调参并记录；不当作最终批准数值 |
| 未确认的扣费策略、叠加/下限、卡组刷新、暂停/重开、胜负顺序 | 先完成无依赖部分；可提交方案或隔离试验，不将猜测接入正式业务 |
| 修改公共接口/枚举值、共享状态写入口、已确认玩法、别人的资产、包或项目设置 | 由总控协调；已有明确授权即继续，无需再次请示 |

规则中的“待实现组件名”是协作职责的参考名；可以在模块内增加辅助类、拆分方法和组件。改变跨模块使用的名字或签名时才需要同步契约。不强制所有模块采用同一种内部设计；优先选现有 Unity/C# 能力和易读实现。

模块授权可以是目录与职责，例如“该敌人脚本、配置、Prefab、独立测试场景”；不要求每新增一个局部文件都等待总控。涉及其他模块时列出需要的集成改动，继续完成本模块可验证部分。

默认采用“目标与验收约束，内部实现自由”。只要保持已确认行为、公共契约、唯一共享状态、Inspector 可配置与交付要求，成员可自主调整内部类名、拆分/合并局部脚本、选择算法、使用 Unity 内置组件、制作表现及优化实现。无需事先把完整方案或每个方法签名交给总控批准；记录真实结果供集成即可。局部可逆问题自行解决，需要变更共享契约时才提交具体集成方案。

本规范不额外锁定成员的 Codex 模型、推理方式或提示词。不在规范中把总控的参考实现写成唯一实现。多人协作的强制交付是：易接手的代码标注、每人的 handoff、Inspector 调参/接线说明与真实验证结果。

## 2 已确认的游戏方向

- Unity 2D，主要通过 Codex 开发；交付 Windows。最新人员范围：用户为总控，其他程序人数未知、不超过 5 人，替代此前固定四人的排班假设。剩余制作窗口约 38 小时是此前估计，不能当作每人有效工时或新的倒计时。
- 用户作为总控：逐项发布接口和运行基线，成员按最小可测试功能领取任务。已有接口支持的局部任务可以先独测，不需要所有玩法细节确定后才开工；依赖真实写接口的任务等待对应接口发布。
- 横版小狗 GROWL AGAIN，核心为获得、主动舍弃、适应和再生。单局目标 6–10 分钟。
- 保留同地图多层探索、付费传送和付费回溯。允许减少技能、地图范围和战斗复杂度；本轮补充明确了三选一，覆盖此前允许 1–3 项代价的建议。
- 永久保留基础移动和基础跳跃；四足基础咬击不可被直接献祭，但站立姿态禁用咬击。主线/返程不能依赖可舍弃的移动技能；只用四足基础操作可完成路线和战斗。玩家主动选择站立却无可用攻击时，可以陷入无法攻击的构筑，不能因此擅自给站立补咬击。
- 无永久成长和跨启动存档。布局固定，本轮世界状态在往返时保持。

## 3 最新补充决定与旧建议的替代关系

以用户补充稿第 5 节“决定说明”为新决定；同一行旧推荐被替代。空白说明尚未确认。其他章节保留的旧文字不能覆盖明确的新决定。

| 编号 | 已确认决定 | 被替代的旧建议 |
|---|---|---|
| G01 | 满血开局，仅移动/跳跃/咬击；冲刺是后续获取能力 | 开局持有冲刺或三能力 |
| G02 | 盾牌可获取，只挡敌人、不挡地形；有次数或冷却限制 | 默认所有伤害都挡；具体刷新方式仍待确认 |
| G03 | 奖励随机三选一；库可包含技能、生命/攻击效果、站立后可用武器 | 优先补缺能力、只三技能等尚未经批准的删减 |
| G04 | 允许满血回血等无即时收益的效果由玩家取舍；已拥有技能/装备必须排除 | 满血必定排除回血；重复技能仍可抽到 |
| G05 | 代价随机三选一，可失去已持有技能/装备或选择负面效果 | 有技能必须舍弃技能 |
| G06 | 无技能/装备时仍提供三个负面效果；没有低资源免费兜底；扣血到 0 失败 | 1 HP 保底不死、免费应急返回、暂时战斗计时减益 |
| G07 | 不实现旧 Bone Guard 兜底分支 | 通过骨盾消耗处理另一套应急费用 |
| G08 | 交互采用固定规则，现以 G14 的优先级为准 | 任意模块自选距离或优先级 |
| G09 | 地面陷阱持续扣血并弹开，不允许无风险烧血强行穿越 | 统一扣 10 点回安全点 |
| G10 | 必须击败 Boss 后到达终点门才胜利，不设同归于尽结局 | Boss 一死立刻胜利 |
| G11 | 暂定三层，第三层终点前是 Boss/精英怪，阻挡出口 | 两层＋独立封闭 Boss 房不是已批准范围 |
| G12 | 首版二段跳、冲刺、站立、武器；喷火和盾仅预留 | 首版自动骨盾；将站立/武器直接删为扩展 |
| G13 | 盾是按键开启、带冷却，优先级靠后；仍只挡敌人 | 自动挡一次或宝箱回充不是当前决定 |
| G14 | 传送门→开关→宝箱为交互优先级 | 按距离跨类别排序 |
| G15 | 已作废，现以 G19 为准 | 旧“失去站立自动丢武器”不得实现 |
| G16 | 负面池包含多项，例如扣 20% 血量、扣 10% 上限、减玩家攻击、增敌人血量/攻击；无技能/装备从中随机三选一 | 只有固定三种负面或免费兜底 |
| G17 | 扣血为 ceil(当前 HP × 20%)；扣上限为 ceil(当前最大 HP × 10%)，然后当前 HP 压到新上限；永久负面只持续本局，重开清空 | 按最大 HP 扣当前血量、舍弃保底留 1 HP |
| G18 | 敌人强化作用本局全图所有存活敌人，包括终点精英；死亡敌人不复活；增最大 HP 时按原 HP 比例调整当前 HP；负面永久叠加到本局结束 | 仅下一层、回血到满、覆盖旧负面 |
| G19 | 站立姿态没有咬击；失去站立能力后武器仍保留，暂时不能使用；占槽结构以 G23 为准 | G15 的自动丢武器规则明确作废 |
| G20 | Unity 6.2（6000.2.9f1）新建项目，迁入必要文件 | 在旧 6000.6.3f1 工程原地降版本、沿用旧包锁 |
| G21 | 姿态只能通过选择奖励/代价改变；不能主动按键切回四足。站立无武器不能攻击，未来有喷火可用喷火 | 自由切换姿态；站立保留咬击 |
| G22 | 首版只有剑；长枪保留身份/接口。恢复站立后原保留武器恢复使用 | 首版同时做剑和枪 |
| G23 | 所有可保留技能/装备共用最多 4 项的槽；正面/负面效果即时结算、不占槽 | 独立武器槽、另计技能容量 |
| G24 | 三张奖励排除已拥有武器/装备；G27 进一步确认技能同样排除 | 已有同款武器仍可随机抽到 |
| G25 | 武器是占槽保留项；四足暂不可用时仍可作为失去武器代价抽取并真正移除 | 仅可舍弃当前可用武器 |
| G26 | 满 4 项时所有新保留项均先选旧项替换；也可直接选增益；取消不领取、不消耗箱子 | 独立武器槽替换、满槽无收益消耗箱子 |
| G27 | 技能和装备都排除已拥有项，不重复、不升级 | 已拥有技能无收益仍可抽到 |
| G28 | 选择站立项立即站立；舍弃/替换站立项立即回四足；剑保留且仍占位，重新获得站立可恢复使用；四足可领取剑但不能用剑攻击 | 自由变身、取得站立后另按变身键、四足无法拿剑 |
| G29 | 打开传送选择暂停；先验证目的地，再确认扣代价；致死原地失败不移动，存活才移动；抵达后 0.5 秒免疫；取消/无效落点不扣费，无免费兜底 | 先传送再死亡、取消收费、致死仍播放到达 |
| G30 | 池内等概率、不重复抽取 3 个不同条目，不保证类别均衡；第一次打开后卡组固定，取消不能刷新 | 每次打开重新抽、保证技能/装备/效果各一个 |
| G31 | 重新打开只补无法执行的代价，例如已失去的技能；满血回血可出；已拥有技能/装备不可出 | 同一资源换文案凑三项、允许舍弃并不存在的项 |
| G32 | 玩家生命归零立即进入 Dead，停止游戏物理与玩家 gameplay 输入；死亡界面/UI/按钮由其他组员负责 | 只扣到 0 而继续操作；总控在 C02 包办结局 UI。重开、胜利及同帧仲裁仍待确认 |
| G33 | 玩家运动、落地与脚步使用 Physics2D.Raycast 地面检测，默认每 0.02 游戏秒扫描；按键仍由唯一 Input System 适配器读取 | 用射线读键盘、模块各自轮询输入或每帧各算一份落地状态 |
| G34 | 当前脚步没有材质 Switch Group；脚步音只按实际落地移动及播放节奏触发，必须避免机枪效应 | 每次地面扫描/按住移动键都播放脚步音；擅自引入材质切换组 |

满血开局的具体数值、负面叠加/下限及具体攻击参数未冻结。技能/装备不可重复、所有保留项满槽替换及姿态联动已确认。

技术命名统一为“构筑槽（Loadout）”，对应用户所说能力槽：包含技能与装备，容量固定 4；即时效果不是构筑项。武器和站立分别占一项，失去站立不移除剑、不释放剑的项位；失去剑才移除剑并释放一个项位。未批准背包、耐久或武器专用槽，不自动添加。

已确认生命公式示例：当前 HP 40，献祭扣 ceil(8)=8，剩 32；当前 HP 1，献祭扣 ceil(0.2)=1，归零失败。最大 HP 100、当前 HP 95，降上限后最大 HP 90、当前 HP 90；最大 HP 1 再降会得到 0，是否过滤这一选项或允许致死仍需确认下限，不能擅自保底。

## 4 首版范围与需要回答的问题

当前采用用户暂定三层；每层空间尺度、宝箱数量尚未确认。第三层精英挡在出口前，不默认加独立 Boss 场景、封闭战斗区或额外付费 PB。一组下层开关控制上层门、短返程路线是一致的设计意图，但主线锁门或可选奖励联动需最后确认。

首版确定类别：移动/跳跃/咬击、二段跳、冲刺、站立、武器、三层探索、奖励/代价三选一、付费回溯、终点前精英与出口。普通敌人种数、精英招式、美术数量、箱数和机关细节尚待确认。喷火与主动冷却盾只预留端口/身份，不实现本轮 gameplay。

必须继续确认：

- Q01：三层路线长度、每层箱数、主线机关位置；不默认加 PB，若需要另行确认。
- Q02：盾已确定主动冷却，首版不实现；后续再确认持续时间、冷却恢复和输入。
- Q03：构筑槽/替换/姿态/首版单剑已确认；不同宝箱之间导致旧候选变为已拥有时怎样修复，以及传送成功后何时重抽尚需确认。
- Q04：负面池方向、永久叠加与全图精英作用范围已确认；攻击变化幅度、叠加按基础值或当前值、取整/下限与未来生成敌人如何继承仍待确认。
- Q05：百分比公式已确认见 G17，收费死亡顺序已确认见 G29；最大生命下限/归零是否允许、攻击下限与危险提示形式仍待确认。
- Q06：跨类别优先级已确认；同优先级距离/稳定 ID 排序、门不可用时是否允许较低优先级对象交互尚待确认。
- Q07：陷阱 tick 间隔/弹开方向、敌人无敌帧是否阻挡陷阱、坑洞怎样处理？弹开并不自动保证不能穿越，需要灰盒验证。
- Q08：Boss 死亡与玩家死亡同帧顺序；建议玩家死亡优先，因为尚未进入出口。必须明确才能发布胜负控制器。
- Q09：传送暂停、取消不收费、固定卡组、0.5 秒抵达保护已确认；奖励界面暂停、整场景重开仍需确认。Windows 已确认。
- Q10：初始 HP、奖励/负面完整池、合法池不足三项的处理、冲刺方向/冷却/无敌、二段跳重置、攻击参数与姿态碰撞体尚未冻结。正式池必须满足恰好三项；不得伪造条目补位或自行减少选项。

回答前可实现已确认行为与不依赖未决规则的模块逻辑。形态/装备按 G21–G28 执行；未确认的收费细节与护盾策略只作隔离方案，不把代码反过来当作用户决定。未冻结数值可用 Inspector 暂定值测试，不能由此改变已确认规则。

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
| 编辑器操作 | 已安装 MCP for Unity，manifest 固定为 v10.2.0 | 用于编辑器检查、场景/Prefab 接线、Console 与验收；每个会话先核对目标实例，见下文 |

迁移后 Packages/manifest.json 与 packages-lock.json 为实际依赖依据；迁移前恢复的包版本仅用于旧工程，不自动覆盖新项目。模块不得自行升级或引入网络、DOTS/ECS、DI 容器、第三方 FSM、Addressables、复杂事件总线、程序地图和存档；需要时总控单独批准引入。

### Unity MCP 操作与验证

- 新会话先发现可用 MCP 工具与资源，按实际 server 名读取 `mcpforunity://custom-tools`、`mcpforunity://instances`、`mcpforunity://project/info`、`mcpforunity://editor/state`。确认项目路径与 Unity 版本；多个实例时显式绑定目标，不沿用上个会话的实例 ID 或连接状态。
- 同一 Unity 编辑器实例同一时间只有一个写入会话。工作树分离仍须各自连接正确编辑器实例；文件隔离不能解决两个 MCP 会话控制同一编辑器的问题。
- 场景、Prefab、组件、Inspector 引用与资产创建优先用 Unity MCP；C# 和 Markdown 可用普通文件工具。操作路径以对应工具当前 schema 为准，使用相对项目路径和正斜杠，不猜工具参数或实例 ID。
- 修改源码后刷新/等待编译与域重载结束，检查 Console，再添加新组件/运行测试。连接成功、工具返回成功、历史独立编译通过都不能代替当前 Unity 编译或 Play 验收。
- 加载/创建/保存前检查当前场景路径、脏状态和 Play 状态；保存明确目标，保护未保存改动。结构/配置应在非 Play 模式保存；Play 中创建成功不等于已持久化。地图场景仍遵循第 11.2 节的维护权。
- Console 检查区分已有日志与本轮新错误；记录已有问题，不靠清空日志宣称通过。工具断线/超时后先查询实际状态再重试，避免重复生成对象、重复挂组件或重复提交事务。
- 必要时发现并启用测试/反射/文档工具组，使用当前项目的实际 API 与素材；构建和 Play 都记录场景、操作、结果。MCP 未可用时可继续文件工作，明确标记尚未进行的编辑器验证。

实际会话状态与已有日志记在 controller.handoff，不把临时实例信息或日志当成长期规则。

### 更换编辑器时的必要文件

必需迁入：Assets/Scripts 及各文件/目录 .meta、根目录 AGENTS.md、docs/GAME_DESIGN.md、docs/handoffs、.editorconfig；README 为使用入口。保留目标现有 .gitignore 和 .gitattributes，缺失时才另建必要忽略规则。源码不包含主场景、输入资产、URP 资源或运行组件，新项目由 6000.2.9f1 创建。以新项目生成的 Packages/ProjectSettings 为准，不复制旧版本文件。迁移后确认实际项目版本与所有新源码可编译，刷新必要文件包时保持本文为最新版本。

不迁入缓存/生成文件：Library、Temp、Logs、UserSettings、Obj、生成的 csproj/sln/slnx。旧 Assets/Welcome、Assets/Settings、Packages、ProjectSettings 不进入“仅源码迁移包”；是否在当前仓库删除这些旧工程文件取决于用户确认迁移方式。当前主场景内 Tests 不在源码包中，若需要保留场景应另外迁移并验证。

C01 输入资产为 Assets/InputSystem_Actions.inputactions：Player/Move（取 x）、Jump、Attack、Interact（已移除 Hold，普通 Button）、新增 Dash；新增 System/Pause，保留原 UI map。键盘 A/D 或方向键移动、Space 跳跃、Enter/鼠标左键攻击、E 交互、Left Shift 请求冲刺、Escape 发布暂停请求；Gamepad 对应左摇杆、South/West/North/East、Start。按键是资产配置与测试基线，不新增姿态键，不授予冲刺能力。未来喷火/开盾按键待确认。PlayerInputReader 在 Update 采样并限时缓冲；物理动作在 FixedUpdate 单次消费。各模块不改公共输入资产、不轮询设备。PauseRequested 只发请求；正式暂停菜单策略未接入，C01 测试驱动可演示暂停。

## 6 目录、命名空间和程序集

```text
Assets/Scripts/Core/             Regrowth.Core，已重建的共享身份/数据/端口
Assets/Scripts/Audio/Core/       Regrowth.Audio.Core，已重建的无 Wwise 音频入口
Assets/Scripts/Runtime/          Regrowth.Runtime，C01 阶段/输入/启动，C02 玩家状态/死亡停止
Assets/Scripts/Gameplay/         玩家、世界、敌人业务，待实现
Assets/Scripts/UI/               HUD 与 ChoicePanel，待实现
Assets/Scripts/Presentation/     动画与反馈，待实现
Assets/Scripts/Audio/Wwise/      Wwise 后端，接入时创建
Assets/Scenes/Tests/C01/         C01_Smoke.unity，已接线并独测通过，非主场景
Assets/Scripts/Tests/C01/        Regrowth.Tests.C01，测试驱动，非正式运动/重开组件
Assets/Scenes/Tests/C02/         C02_Smoke.unity，真实状态/死亡独测，非正式结果 UI
Assets/Scripts/Tests/C02/        Regrowth.Tests.C02，独测驱动/录音替身，不进正式构建
docs/handoffs/                   用户名.handoff，只记工作事实
```

命名空间分别为 Regrowth.Core、Regrowth.Runtime、Regrowth.Gameplay、Regrowth.UI、Regrowth.Presentation、Regrowth.Audio。Core 与 Audio Core 不依赖 gameplay、UI、输入包或 Wwise；业务程序集按需引用它们。输入适配器显式引用 Unity.InputSystem。Wwise 实际依赖可获取后才建后端程序集。

禁止复制同名接口到不同命名空间、各模块创建同名 GameManager、重复持有同一玩家的真实 HP、各自控制 Time.timeScale。玩家与全局共享状态由总控发布的唯一组件持有；每个敌人可有自己的唯一生命实例，不能多组件各算同一实体的生命。表现层只读。

## 7 实际存在的接口及名字

契约版本 5。以下对应实际源码；数据类无业务副作用。IRunContext/IPlayerInput 已有 C01 实现；生命/构筑/姿态及新增写口/攻击读口由 C02 的唯一 PlayerState 实现。不要根据表格自动创建第二个实现。

| 类型 | 实际签名/字段 | 语义与状态 |
|---|---|---|
| RunPhase | Playing=0、Choosing=1、Paused=2、Dead=3、Won=4 | 稳定枚举；C01/C02 支持 Playing/Choosing/Paused/Dead；Won、重开与同帧仲裁未实现 |
| IRunContext | Phase、IsGameplayActive；event Action<RunPhase> PhaseChanged | C01 RunController 实现；初始化且 Playing 时活跃 |
| IPlayerInput | float MoveX；bool JumpHeld；bool TryConsumeJump/Attack/Interact/Dash()；void DiscardGameplayInput()；event Action PauseRequested | C01 集中输入；每类最多一个限时请求；离开 Playing 清空，暂停后仍按住按钮须释放再按 |
| IHealth | int CurrentHealth / MaximumHealth；bool IsAlive；event Action HealthChanged / Died | PlayerState 实现；真实修改后通知；死亡先 HealthChanged 再 Died，每个生命周期一次 |
| DamageKind | Enemy=0、Terrain=1 | 区分盾可挡与不可挡；献祭不属于本接口 |
| DamageRequest | Amount、Kind、Source；构造(int amount, DamageKind kind, GameObject source=null) | 正数伤害、合法 kind，不可变；来源可空；default struct 仍需接收端拒绝 |
| IDamageable | bool TryTakeDamage(DamageRequest request) | 实际扣血才 true；死亡/无敌/无效请求 false；护盾完全挡住 false；具体策略待确认 |
| LoadoutItemId | Dash=1、DoubleJump=2、Shield=3、FlameBreath=4、UprightForm=5、Sword=101、Spear=102 | 技能/装备统一身份；Shield/FlameBreath/Spear 预留；即时效果不入枚举 |
| ILoadoutState | int Capacity；IReadOnlyList<LoadoutItemId> Items；bool Contains(LoadoutItemId item)；event Action LoadoutChanged | PlayerState 实现；Capacity 固定 4，稳定的不可写活视图；原子提交后通知 |
| PlayerForm | Quadruped=0、Upright=1 | 当前姿态身份，不提供手动切换 |
| IFormState | PlayerForm CurrentForm；event Action<PlayerForm> FormChanged | PlayerState 实现；根据站立项即时联动，只在真实变化后通知 |
| IPlayerStateCommands | bool TryHeal(int amount)；bool TryAddLoadoutItem(LoadoutItemId item) / TryRemoveLoadoutItem(LoadoutItemId item)；bool TryReplaceLoadoutItem(LoadoutItemId removedItem, LoadoutItemId addedItem) | C02 最小写口；主线程 Playing/Choosing 且存活/已绑定；false 无修改。UI 不直接结算 |
| IPlayerCombatState | int BiteDamage / SwordDamage；bool CanBite / CanUseSword | C02 只读攻击配置/权限；仅存活、已绑定、Playing 可攻击；不执行动作/命中 |
| InteractionKind | Portal=0、Switch=1、Chest=2 | 已确认的跨类别顺序；同类别排序待确认 |
| IInteractable | string InteractionId / Prompt；InteractionKind Kind；bool CanInteract(GameObject actor) / TryInteract(GameObject actor) | 稳定场景唯一 ID，不能用 GetInstanceID；true 是接受请求，不等于付费/领奖成功 |
| ChoiceOption | Id、Title、Description；构造(string id,string title,string description) | 不可变展示项，Id 非空，Title 非空；不执行效果 |
| ChoiceRequest | Id、Title、IReadOnlyList<ChoiceOption> Options；构造(string id,string title,IEnumerable<ChoiceOption> options) | 复制候选，拒绝空/重复 ID；展示支持 1–4 项；奖励/代价必须恰好 3 项，满槽替换展示全部 4 个旧项 |
| IChoicePresenter | bool IsOpen；bool TryShow / TryReplaceCurrent(ChoiceRequest request,Func<string,bool> tryConfirm,Action onCancel)；void CancelCurrent() | 只有端口，真实 UI 尚未实现；同请求内可切换替换阶段，生命周期见下文 |
| IAudioBackend | void Play(AudioCue cue,GameObject emitter) / StopAll(GameObject emitter) | 后端端口，Integration 已安装，项目后端适配器未实现 |
| GameAudio | Play(AudioCue cue,GameObject emitter=null)；StopAll(GameObject emitter)；InstallBackend(IAudioBackend)；UninstallBackend(IAudioBackend expectedBackend) | 已实现转发与默认空后端；调用限定 Unity 主线程 |

### C01/C02 实际运行接线（契约 5）

| 组件/入口 | 实际使用方式 | 实现边界 |
|---|---|---|
| RunController : IRunContext | bool TryPause() / TryResume()；bool TryBeginChoosing(object owner) / TryEndChoosing(object owner)；bool IsInitialized | 主线程；重复/阶段不符/错误 owner 返回 false。选择锁不允许普通 Resume 绕过；回调中拒绝重入命令 |
| PlayerInputReader : IPlayerInput | bool IsInitialized；输入读取/消费见上表 | 输入资产及六个动作路径在 Inspector；缓冲暂定 0.15 真实秒、实时生效；初始化创建私有输入副本 |
| GameBootstrap | bool IsStarted；OnEnable 启动、OnDisable 清理 | Inspector 必填 RunController、PlayerInputReader；正式玩家还须绑定 PlayerState（旧 C01 输入独测允许为空）；可选 IChoicePresenter 组件和发声对象数组。重复入口拒绝，不自动查找/创建对象 |

T06 接入：Inspector 用 MonoBehaviour 引用并验证 IPlayerInput/IRunContext，或显式引用已发布的具体组件。FixedUpdate 先检查 IsGameplayActive，再读取 MoveX、单次 TryConsumeJump；只有实际跳跃成功才发 PlayerJump。同一 Rigidbody2D 的运动写入只由运动模块持有，输入适配器不写速度。启用时读阶段快照，PhaseChanged 注册/退订遵循统一生命周期。

暂停/选择期间 Time.timeScale=0，输入立即清空，物理冻结；恢复后保留原物理速度继续模拟，运动模块不得在暂停期间积累动作。传送成功后，未来 T06/T13 的运动入口负责在迁移事务中清零线性/角速度，并调用 DiscardGameplayInput；C01 不提供假传送或费用结算，实际运动/迁移 API 由后续总控串行登记。

测试场景的暂停按钮及 Escape 接收者只是隔离演示；正式暂停菜单/奖励暂停未确认，不在 Bootstrap 自动订阅 PauseRequested。C02 由 Bootstrap 根据真实生命归零接入 Dead，停止 gameplay 输入和物理；不发布任意 SetPhase、Won 或 Restart，不以测试入口启停冒充整局重开。退出先取消可选菜单，再卸载输入副本、释放锁并恢复启动前时间倍率。

旧版 IDamageable(int amount, GameObject source) 不是本次规范；统一 DamageRequest，避免地形伤害被盾错误挡住。版本 3 已移除旧 AbilityId/IAbilityState/EquipmentId/IEquipmentState，统一使用 LoadoutItemId/ILoadoutState，不保留第二套真实容量。旧类型此前没有业务引用或序列化资产；后续发布后的枚举值不可重排/复用，也不能因为 UI 排序改变。

所有事件主线程同步触发。订阅者 OnEnable 注册、OnDisable 退订，启用时读取当前快照。回调只通知，不用音频/UI 回调决定 gameplay 成败。

### 卡片端口生命周期

TryShow 在已有菜单打开时返回 false，不调用任何回调。UI 传回选项 Id，发起者重新验证并执行；tryConfirm 返回 true 才完成关闭，false 保持菜单并提示。UI 不能执行扣血、改能力、移动或标记箱子。确认期间禁止重复/重入提交；成功后回调不能再调用。

TryReplaceCurrent 只允许在菜单已打开且 request.Id 等于原事务 Id 时替换展示/回调，否则 false 且无回调。它不关闭、不触发旧 onCancel、不释放输入锁。奖励确认回调可进入替换阶段并返回 false，使事务保持未完成；新阶段完成时才提交领取/移除。取消替换阶段取消整个领取，不先移除旧项。UI 不直接写槽位。

CancelCurrent 在关闭时无操作，在打开时关闭并恰好通知当前阶段取消回调一次。场景卸载前总控取消/退订并释放输入锁。UI 自己不设置 Time.timeScale。展示支持 1–4 项不构成奖励/代价降为 1–2 项或增为 4 项的许可；4 项只用于满槽替换。

奖励与满槽替换允许取消且不领取/不消耗；传送按 G29 取消不收费。总控在提交前验证目的地，然后应用代价；若致死，结束原地事务、进入 Dead，不迁移位置、不发 Teleported。否则应用迁移并抵达保护。表现动画不能再次扣费，也不能靠淡出完成事件决定业务是否支付。

随机卡组按 G30–G31 生成。抽取不保证能力/装备/效果各一项，不能因为“三选一”而伪造缺失的技能代价。奖励候选跨宝箱失效和门成功后卡组更新仍待确认。

### C02 最小状态写口与队友接点

IHealth、ILoadoutState、IFormState、IPlayerCombatState 是同一 PlayerState 的读视图，IPlayerStateCommands 是总控服务/独测驱动使用的受控写口。初始化由 Bootstrap 显式负责：满血、空构筑、四足；暂定 Inspector 默认 HP100、咬击10、剑15，首次初始化读取，临时入口启停不重置。攻击0可作为测试配置，攻击模块须跳过0伤害请求；这不批准负面攻击下限。

- TryTakeDamage 仅 Playing 接受正数 Enemy/Terrain 实际伤害，default/死亡/暂停拒绝；扣到0允许致死，无护盾/无敌帧策略。TryHeal 仅实际正数回血返回true，压到上限、不复活；满血false不代表业务不能领取满血回血奖励，奖励服务另行判断。
- 构筑只接受 Dash/DoubleJump/UprightForm/Sword；拒绝重复、预留/非法身份和第五项。替换先验证旧项存在、新项未持有，再一次提交，不先删旧项；真实改变后一次 LoadoutChanged，必要时 FormChanged。
- 姿态从站立项得出；四足持剑仍占槽但不可剑击；失去站立保留剑；站立无剑不能咬击。CanBite/CanUseSword 还包含生命、初始化/绑定及运行阶段权限。
- 写命令仅存活、已绑定、Playing/Choosing 接受；暂停/死亡/解绑或状态通知回调中拒绝，事件订阅者只读已提交快照，不重入结算。
- T02 的生命/构筑/姿态引用都指向同一 PlayerState，不复制真实状态。T07/T08 用 IPlayerCombatState，动作开始与命中重新校验，实际伤害发给目标 IDamageable，不把玩家 PlayerState 作为敌人状态容器。
- T16/队友死亡 UI 订阅 IRunContext.PhaseChanged，在 OnEnable 读取 Phase 快照，Dead 时显示结果；也可订阅 IHealth。UI 不改 Time.timeScale，不调用普通 Resume 绕过 Dead，不把 Bootstrap 启停当复活。
- PlayerState 发实际受伤 PlayerHurt、技能状态改变 AbilityGained/Lost；Bootstrap 只在本生命周期首次死亡接线时发 PlayerDied，UI/攻击模块不重复发这些 Cue。剑获取/移除没有独立 Cue，不冒用攻击音效。

C02 没有生命上限修改、攻击增减/叠加、百分比献祭、候选池或新局复位写口；未确认的代价下限和叠加规则不接入。禁止模块另建 SetHealth/扣费系统，献祭不得走 IDamageable。首版恰好四种保留项，全部持有时没有第五个合法新项；满槽原子替换容器用隔离测试数据验收，不给真实玩家授予预留技能。

## 8 组件实现边界与依赖

下表标明 C01/C02 已建立的最小实现与其他待实现职责。源码存在不等于场景已验收或基线已发布；验证状态见 controller.handoff。模块内组件与辅助类名可自主调整，跨模块签名由总控登记。

| 计划组件 | 职责 | 必须先决定 |
|---|---|---|
| RunController（C01/C02） | 唯一 Playing/Paused/Choosing/Dead、时间倍率、选择锁、死亡停止 | 正式暂停菜单、Won/同帧仲裁与重开待确认 |
| PlayerState（C02 最小版） | 唯一生命/构筑槽/姿态，治疗/增删/原子替换与攻击读口 | 初始值可配置；上限/攻击变化、负面叠加/下限尚未接入 |
| PlayerInputReader（C01） | 实现 IPlayerInput，集中采样/缓冲/清理 | 已有输入资产；技能权限仍由 gameplay 验证 |
| PlayerInteractor | 按固定优先级选对象、单次请求锁 | 优先级/距离/不可用对象处理 |
| ChoicePanel | IChoicePresenter 的展示实现 | 总控运行阶段接线 |
| RewardService / SacrificeService | 固定三候选、校验、替换事务与实际应用 | 候选失效修复、真实数值池与下限 |
| Portal / Chest / WorldSwitch / WorldDoor | 交互和本轮状态 | 成功后卡组更新、地图联动；收费顺序已确认 |
| GameBootstrap（C01/C02） | 显式引用 RunController/InputReader/PlayerState，死亡接线、可选菜单取消与音频停止，唯一入口 | 不自动建对象，不实现新局重开 |

技能和剑共享 ILoadoutState，不维护单独的真实武器槽。丢 UprightForm 不从 Items 删除 Sword；必须区分保留项持有、当前姿态和实际攻击权限，不允许从角色 Sprite/图标反推真实状态。PlayerState 已实现构筑变更与姿态联动，没有自由切姿态命令；奖励/舍弃事务服务仍由后续总控统一接入。

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
- 构筑容量 4、正式三选一、不可重复和姿态联动属于已确认结构规则，本轮不要求任意改变其结构。可集中为命名常量并在 Inspector 提示，但不可复制魔法数字到各模块或提供无效的数量滑条。后续用户改变规则时同步接口/UI/验收。
- 稳定枚举 ID、数学公式结构、算法中的 0/1、集合索引等可以留在代码；不要为“全部可调”把所有局部变量序列化。
- 等概率抽取是已确认规则，不添加可任意改概率的权重字段。候选池可配置，执行时仍须过滤已拥有项、保证恰好三项且不包含预留技能。

### 玩家射线地面检测与脚步节奏（G33–G34）

T06 运动组件持有唯一地面检测结果；使用 Physics2D.Raycast（2D），在 FixedUpdate 中按扫描周期检查脚下，默认扫描间隔0.02游戏秒。间隔、射线起点/偏移/长度、地面LayerMask与移动阈值在Inspector配置，排除玩家自身及不作为地面的Trigger；可用多条脚底射线改善边缘判定。默认物理步长应核对，若不是0.02，不擅自修改全局设置，由总控协调；扫描不是键盘采样，键盘仍遵守IPlayerInput的Update缓冲/FixedUpdate消费。

落地/跳跃与脚步复用运动组件的真实地面结果，不由脚步另造一套grounded。脚步播放要求存活、Playing、已落地且实际水平位移/速度超过可调阈值；按住键却顶墙不播放，空中/静止/Paused/Choosing/Dead不播放。0.02秒是检测频率，不是发音频率；用独立可调的脚步播放最小间隔及步距/节奏去重，移动或恢复时不补播积攒的脚步，依据实际素材试听确认不会重叠成为机枪。节奏参数暂定测试值，不冻结所有素材的统一间隔。

当前没有脚步材质Switch Group，不要求材质分类、Switch或按地面Tag选Wwise事件。程序通过GameAudio统一Cue入口；现有AudioCue尚无脚步身份，T06/T18接音频前由总控串行发布稳定Cue及映射需求，不在gameplay写AK类型/Event字符串、不冒用PlayerJump。本轮仅更新规范/任务依赖，C02状态独测不声称已实现走路/射线/脚步播放。

### 10.2 配置存放、引用与校验

1. 单对象少量参数用 `[SerializeField] private`，配合 `[Header]` 分组、中文 `[Tooltip]` 说明含义、单位、范围与生效时机；按语义使用 `[Min]`、`[Range]`。不靠 public 字段让其他模块随意写。
2. 多对象共享或成组参数可用 ScriptableObject，提供 `[CreateAssetMenu]` 和实际 `.asset/.meta`。配置建议放 `Assets/Configs/<Module>/`，Prefab 放 `Assets/Prefabs/<Module>/`；目录为建议，不为改现有合理布局批量搬资产。避免把同一伤害/比例复制到几个组件里。
3. 运行时从配置初始化真实状态；当前血量、已领取宝箱、已开门、随机卡组、累计负面和当前姿态不作为可随意写的设计配置。不在 Play 中修改共享 ScriptableObject 保存本局状态；需要副本时显式复制。
4. GameObject、Transform、组件、Prefab、Sprite、配置等使用序列化引用或总控显式注入，不依赖硬写对象名、Scene 路径、Find 或 Resources 路径。同物体必要组件可用 RequireComponent/GetComponent 做局部绑定，不要求每个本地组件手拖；跨物体依赖必须可定位。Unity 接口不直接当普通序列化字段；可用具体组件或 MonoBehaviour 引用并在初始化验证其实现的接口。
5. OnValidate 只做轻量字段数据校验，不创建对象、不执行伤害/领奖/传送、不扫描场景或修改其他资产。运行初始化/提交时仍检查空引用、非法范围、重复 ID 和候选不足，给出带组件/资产上下文的错误；校验不能偷偷决定尚未确认的生命/攻击下限。
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

每个成员按所领取的任务卡逐项验收，交付可复现操作与个人 handoff；只有编译或脚本不足以标独测通过。总控维护单项验收/集成记录，全部第一阶段门槛通过才标阶段通过。第二阶段核心玩法与后续整局任务先保留候选，第一阶段通过后依据实际代码、人数与规则重新拆分和派发，不提前固定成员。具体门槛与登记表见 docs/WORK_PACKAGES.md。

### 11.2 独立地图场景交付

地图正在制作，用户已确认以独立地图场景交付。沿用地图作者的场景，不要求改成地图 Prefab；无需因此引入多场景加载框架。总控以交付场景接入运行组件，具体路径由地图 handoff 登记。

制作期间地图作者维护地形、碰撞和摆放；程序只在独立测试场景验证。交付后由总控接线；之后还需改图时协调交还编辑权，同一个 .unity 文件不能两人同时改。需要提前试走时可使用测试副本，但不将副本变成第二份长期主地图。

地图交付说明至少标明：出生点、地面/阻挡、传送入口与安全落点、开关对应门、宝箱/敌人/陷阱位置、精英/出口、相机边界。程序使用 Inspector 引用或明确注入；不依赖对象名字搜索、不由地图另写 HP/奖励/扣费系统。Layers 与物理设置由总控统一发布。

先用短区段验证角色尺寸与基础跳跃可达性，避免地图完成后才发现必须持有可舍弃技能才能通行。已开始制图不自动确认旧提案中的箱数、门关系和收费连接；按实际交付与用户规则核对。

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

## 13 验收与发布基线

当前可检查：公共程序集独立编译、数据不可变/非法输入、空后端和后端切换、源文件/meta 配对与 GUID 唯一性。后续组件完成再检查 Unity 导入、Play、Prefab 接线与目标平台导出。独立 .NET 编译不等于 Unity 场景运行。

版本 2 的 16 项检查属于历史结果，不覆盖当前契约。版本 3 已用目标 Unity 6000.2.9f1 自带 Roslyn、真实 UnityEngine/NetStandard 引用分别编译 Core 和 Audio，通过；新的 16 项断言涵盖四项展示接受/第五项拒绝、候选复制与只读、重复/空项拒绝、伤害分类、构筑身份及音频转发/卸载保护，全部通过。

C00/C01已随Ming/e86074a发布，历史32项C01与16项Core断言不覆盖新业务。本轮C02经Unity刷新编译、独立场景保存重载、真实Play51项通过；HP120/咬击13配置保存重载生效并恢复100/10，实际测试按钮与非法初始HP拒绝验证通过，旧C01兼容复验32项通过。698个Assets meta无重复GUID/孤立meta，新增源码/场景meta配对；场景零Missing Script，唯一状态/输入/阶段/入口且全部引用有效。物理fixedDeltaTime实际约0.02，本轮未修改。第一阶段、运动/射线/脚步试听、整局与Windows导出未验收。C02仍待用户发布，历史Wwise/网络与预期拒绝日志另记交接，不继承未执行的验证。

整局最终验收必须包含：

1. 无强化能力走完主路/返程，Boss 攻击有基础操作躲避窗口。
2. 箱/门正式业务恰好三张；玩家可看到无收益奖励；无技能/装备仍有三种真实负面选项。
3. 可致死代价明确警告；选择导致 0 HP 进入失败，不再采用旧“1 HP 安全通行”验收。
4. 取消/无效落点/重复确认是否收费按冻结规则测试，不重复应用效果。
5. 机关、箱子、敌人和玩家状态在往返时保持；盾不挡地形伤害。
6. 击败 Boss 只解锁出口，存活玩家进入出口才胜利；死亡重开无旧引用/输入锁/暂停残留。
7. 无 Wwise 后端可以完成 gameplay；有后端的导出在队友电脑运行。
8. 构筑槽不超过 4 项，技能/装备共用容量，即时效果不占项位；失去站立保留剑，四足不可剑击；舍弃剑才移除剑。站立无可用武器/喷火不能攻击，也不能按键切回四足。
9. 玩法参数、素材/世界引用与映射可在 Inspector 或 Unity 配套资产中调整，业务实际读取配置；运行状态不绕过统一写入口。模块附参数入口与生效时机，保存后 checkout 可复现。
10. 自研代码标注足以理解职责、依赖、对外语义与关键假设；成员个人 handoff 已更新，接手 Agent 能定位入口、完成接线并知道未验证内容。

## 14 本轮记录与下一轮

已确认：新版 G01–G10、总控先建 main、规则统一单文件、脚本标注和 handoff。已恢复 61 个 Git 保存的缺失文件，保留尚存的 ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json 改动。

本轮已确认 G21–G31：统一 4 项构筑槽、技能/装备不重复、所有保留项满槽替换、姿态即时联动、四足持剑、传送致死原地失败和固定等概率三选一。地图/箱数、数值下限、陷阱及跨交互的候选更新等仍待确认，不擅自实施。每轮把明确答复移到已确认部分，删除互斥旧建议，不让多个 Codex 分别猜规则。

不要把本文“待实现文件”或“预留枚举”标为已完成功能。当前尚未具备完整运行底层，但已有接口支持的任务可先独测；总控逐项发布依赖与基线后解锁后续任务，不再要求等所有功能都准备好才分发。

规范版本 4 放宽了模块内部实现与文件创建自由，增加 Inspector 配置、共享配置与本局状态分离、参数接线及验收规范。未修改已确认玩法、现有公共签名或枚举 ID；本轮为文档更新，不能视为已实现了所有参数组件。

规范版本 5 进一步采用目标/验收约束与内部实现自由，参考组件名不锁定内部结构；明确自研代码标注及个人 docs/handoffs/<名字>.handoff 为强制交付。仍未改变公共源码契约或已确认玩法。

规范版本 6：用户总控＋其他不超过 5 名程序的人数待定，改用最小可测试任务池；地图独立场景交付、总控接入。只新增任务结构和协作规则，没有实现任务内功能、分配具体成员或修改地图。

规范版本 7：用户已将此前内容推送至 Ming；更新实际分支基线、Unity MCP 操作要求与新会话交接。总控下一步先补齐 C00 编辑器验证，再实现 C01 最小输入/运行阶段/启动接线，随后推进 C02；不因连接 MCP 就宣称底层或整局已经验收。

规范版本 8：新增契约 4 的 IPlayerInput 和 C01 实际运行接线；明确 Ming 分支随附规范与 T01/T02/T03/T06/T07 最小合入批次。C00 编辑器检查完成，C01 编译/32 项独立 Play 检查通过，最终修复与场景待用户发布；不标第一阶段/正式地图/Windows 导出完成。

规范版本 9 / 契约 5：基于已发布 Ming/e86074a 完成 C02 最小真实状态、受控写口和攻击读口，按 G32 接入死亡停止；正式死亡 UI/按钮归队友。C02 独立 Play 51 项、参数保存重载、C01 32 项兼容复验通过；C02 尚待用户发布。三份总控协作文档随代码和 Unity 编辑同步维护，未接入代价下限、叠加或新局/胜利。

同轮用户新增 G33–G34：运动/脚步共用0.02秒地面射线扫描、输入仍集中读取，脚步单事件节奏限频且不依赖材质Switch Group。已登记T06/T05/T18对接；实际射线运动、脚步Cue/映射及试听尚未实现。
