# GROWL AGAIN 音频接口与 Unity 接入交接

更新日期：2026-10-01。交接对象：程序、关卡、动画/UI、技术音频。
核对分支：AudioPipeline。本文件替代此前同名报告，以当前已保存的 Wwise 工程及生成元数据为依据。

**交付状态：Wwise 资产与接口已配置；Unity 业务接线与运行验收待完成。** 当前 Assets 下未发现 Wwise 以外的游戏 C# 脚本。本文中的区域检测、统一音频控制器及成功事实通知是接入要求，不代表已有实现。旧 AGENTS.md 仅作历史资料。

## 1. 最新约定与接入重点

- 三只普通小怪：一只飞行、两只地面，无主动攻击技能；Elite 同样依靠移动和身体碰撞伤害。
- 三层音乐使用 State/Event，**不使用高度 RTPC**。实际楼层 Event 名为 **Set_State_Level1、Set_State_Level2、Set_State_Level3**；不要使用聊天早期建议的 Set_Exploration_Level1 等名字。
- 音乐只启动一次，换层只发 State Event。Unity 判断楼层，Wwise 控制各轨音量。
- 地图总高度约 150 m，实际层界由关卡配置，不默认按 50/100 m 等分。
- 最新 SB_Main 已包含全部 **39 个 Event**，包括三个楼层事件；不再需要独立楼层 Event Bank。
- 石门动作固定 2 秒，声音在开始开启时播放一次。当前 Play_Door_Open Bank 元数据仍为 **2.58 秒**；需音频确认多出的部分是否为尾响，或裁切后重新生成，不要改门动作时长迁就素材。
- 音频响应真实成功事实，不在 Update、任意碰撞或按键按下时无条件播放。

## 2. 版本、路径与 Bank 清单

| 项目 | 当前配置 |
|---|---|
| Unity | 6000.2.9f1 |
| Wwise Authoring / SDK | 2025.1.10，Build 9233 |
| Unity Integration Bundle | 2025.1.10.4304 |
| Wwise 工程 | 2026GameJamUnity_WwiseProject/2026GameJamUnity_WwiseProject.wproj |
| Windows 生成目录 | 2026GameJamUnity_WwiseProject/GeneratedSoundBanks/Windows/ |
| Unity RootOutputPath | ../2026GameJamUnity_WwiseProject/GeneratedSoundBanks/，相对 Assets 解析 |
| Unity WwiseProjectPath | ../2026GameJamUnity_WwiseProject/2026GameJamUnity_WwiseProject.wproj，相对 Assets 解析 |
| 构建复制目标根目录 | Assets/StreamingAssets/Audio/GeneratedSoundBanks；平台子目录以 Integration 实际构建结果为准 |
| 构建选项 | CopySoundBanksAsPreBuildStep=true；GenerateSoundBanksAsPreBuildStep=false |
| 当前 Bank 策略 | 当前产物使用 SB_Main 承载全部 39 个 Event |

Authoring 安装路径保存了本机 E 盘位置，需要生成 Bank 的队友应改为自己的安装目录。构建不会自动替你更新 Bank。

### 2.1 当前 Windows 必需 Bank

以下路径相对 Windows 生成目录：

| Bank 文件 | 内容 | 加载要求 |
|---|---|---|
| Init.bnk | 初始化数据 | Integration 初始化流程管理；避免再重复加载 |
| SB_Main.bnk | 39 个 Event、51 个媒体条目，含三个楼层事件 | 音频启用前加载 |

相应 JSON/Integration 所需元数据及目录结构一起交付。最新 Windows SB_Main JSON 已确认包含 Set_State_Level1/2/3；此前的独立楼层 Bank 方案已被替代。程序加载 Init 与 SB_Main，并确认成功后再 Post Event。Picker 可见不等于运行时已经加载。

### 2.2 Unity 启动顺序与清理

1. 初始化引擎，确认 Init 就绪。
2. 加载 SB_Main，等待成功；失败提示具体文件，不阻塞玩法。
3. 准备持久全局音乐/UI emitter 和有效默认 Listener。
4. 按实际出生位置发 Set_State_Level1/2/3，再设置当前 Music_Group（普通开局为 Set_State_Exploration）。
5. Post Play_Music_State 一次，记录音乐实例/playing ID。
6. 后续换层只发楼层 Event；进入/退出 Elite 战斗只切 Music_Group。
7. 退出本局停止音乐和世界声音，清理订阅、playing ID 和楼层缓存；确认无声音使用后卸载业务 Bank。重开重新确定状态。

当前 SampleScene 有 WwiseGlobal/AkInitializer、相机 AkAudioListener/AkGameObj，但未发现完整游戏接线或场景 AkBank 加载配置。启动工作由一个控制器负责，不让每个模块自行初始化。

## 3. 三层探索音乐（已核对保存配置）

### State 分工

| State Group | 状态 | 用途 |
|---|---|---|
| Music_Group | Exploration、Elite、默认 None | 探索/战斗分支；None 当前也映射 Exploration，不是静音 |
| Exploration_Level | Level1、Level2、Level3、默认 None | 控制探索五轨混音；None 目前与 Level1 同音量配置 |

运行时显式使用 Level1，不依赖默认 None。Level3 不等于 Elite；进入第三层不自动进入战斗。
已保存的楼层默认过渡时间是 **1.5 秒**，不是此前讨论示例的 2 秒，Unity 不再叠加音量插值。

### 五轨 State 音量偏移（dB）

| 轨道 | None | Level1 | Level2 | Level3 |
|---|---:|---:|---:|---:|
| 环境床 Ambience | 0 | 0 | -108 | -108 |
| 5 Keyboard | -108 | -108 | 0 | 0 |
| 6 Bass | -108 | -108 | 0 | 0 |
| 3 Synth | -108 | -108 | -108 | 0 |
| 7 Backing_Vocals | -108 | -108 | -108 | 0 |

第一层只有黑暗环境床；第二层切 Keyboard + Bass；第三层保留第二层并加入 Synth + Backing Vocals。
Hope Segment 基础音量 -6 dB，环境床轨基础音量 -8 dB；State 的 0 表示不额外衰减。
五轨共用时间线，新层从当前时间位置淡入，不保证从第一句开始；人声空白处暂时无声属于素材本身。

当前 Exploration 播放列表引用 Hope，保存了无限循环设置（LoopCount=0）；环境床片段已延展到约 183.833 秒，与 Hope Exit Cue 对齐。仍需试听短素材重复与整段循环接缝。不要再单独启动旧环境床。
Elite 播放列表未见同样的显式无限循环设置，需播放超过曲长验证，不能承诺已无限循环。

### Unity 楼层检测要求

- 每层创建音乐区域，使用 BoxCollider2D，启用 Is Trigger；配置楼层 ID 1/2/3。
- 玩家具备 Collider2D/Rigidbody2D，Physics2D Layer Collision Matrix 允许区域检测；只识别玩家根对象，多个碰撞体去重。
- 区域通知统一音频控制器，仅在楼层真正改变时发对应 Event。
- 区域尽量不重叠；必须重叠时设唯一优先级，边界跳跃不可反复切层。
- 不在任意 OnTriggerExit2D 中重置 Level1，因为离开第二层可能进入第三层。
- 出生、传送成功、重开时主动查询当前区域，不只依靠走入触发器。返回下层也切回对应状态。
- Elite 战斗期间仍记录当前楼层；结束战斗切回 Exploration 时恢复当前区域层次。

## 4. 接入边界、对象配置与 Git

### 4.1 统一音频入口

Gameplay 只通知真实成功事实；Bank 加载、Wwise 类型与 Event 映射集中在音频适配层。旧文档中的 GameAudio/IAudioBackend 在当前分支未发现实现；团队如果已有封装，应扩展已有映射，不新建第二套同名接口。

可在 Inspector 绑定 AK.Wwise.Event，然后在音频层通过 eventReference.Post(emitter) 调用。启动/Bank 就绪、空引用、返回值与去重由控制器处理。楼层统一使用 Event，业务不再同时直接 SetState。
当前没有业务 RTPC 和怪物类型 Switch，不要求填写 Speed、Hope、EnemyType 等不存在的参数。

### 4.2 emitter 与生命周期

玩家动作用玩家 emitter；怪物移动/受伤用各实例 emitter；命中音用目标/命中点；门、开关、箱子和传送源/目的地各有自己的位置。UI/音乐用持久全局对象。
死亡先停止移动/振翅触发，再播死亡音；如果实体当帧销毁，使用临时 emitter 保留尾音。对象池回收停止本实例声音并清理 playing ID/节拍；不要全局 StopAll。
暂停由唯一运行控制器通知音频后端。当前没有 Pause/Resume Event，需后端实现世界音暂停与恢复，UI 保持可发声；不假定 Time.timeScale=0 自动暂停 Wwise。

### 4.3 衰减与 Listener

已保存 ShareSet 真实名称为 **ATT_Enermies**，挂在 Enemies 容器上；RadiusMax=35，VolumeDry 由 0 m / 0 dB 到 35 m / -200 dB，段形状 Log2。
这不是先前聊天示例的多点曲线，不能承诺 27 m 正好为 -20 dB。相机中心到边缘约 27 m，需游戏内试听边缘与画外声音。
2D 场景建议 Listener 跟随相机 XY、Z 与发声平面一致；若相机 Z=-10，直接挂相机可能引入额外距离。使用独立 Listener 时避免重复默认监听。
当前只核实 Enemies 引用了该衰减；不假设所有世界音效都已配置空间化。UI/音乐不应受敌人距离衰减。

### 4.4 Git 交付

音频负责人决定不向共享 Git 提交 Originals。Wwise 工程 .gitignore 已添加 /Originals/，本地 WAV 保留供生成 Bank，队友使用预生成运行资产。
.gitattributes 已将 *.bnk 和 *.wem 配置为 Git LFS；WAV 的既有 LFS 属性保留，但 Originals 被忽略，不进入本次提交。
提交工作单元、Init/SB_Main Bank、配套元数据及本文；不提交缓存。两个平台的 SB_Main 当前各约 171 MiB，必须以 LFS 指针入库，不能作为普通 Git 大文件提交。
队友拉取后执行 git lfs pull，确认 Bank 是真实二进制文件。没有 Originals 的 checkout 无法完整重生成 Authoring 工程，但不影响现有 Event/State 调用与已生成音频播放。
构建配置继续使用“复制 Bank、不自动生成 Bank”；即使不再修改 WAV，改变 Event/State/混音后仍由音频负责人本地重新生成并交付。
本次为修复大文件提交问题，暂存了 .gitattributes、Wwise .gitignore 与四个已有 Bank；未 commit/push，其他用户改动保留。

## 5. 完整 Event 清单与触发逻辑（39 个）

名称逐项取自 `Events/Default Work Unit.wwu`，保持大小写与下划线。除特别注明外，要求一次状态变化只发一次；表中 emitter 是接入建议。

### 玩家（12 个）

| Event | 唯一触发点 | emitter / 去重要求 |
|---|---|---|
| `Play_Footstep` | 玩家着地移动中的有效落脚帧，或统一步距计时器 | 玩家；动画与脚本二选一，停下/离地/死亡不发 |
| `Play_Jump` | 第一段跳跃成功执行 | 玩家；按键但跳跃被拒绝不发 |
| `Play_DoubleJump` | 第二段跳跃成功执行 | 玩家；本次不再叠发 Play_Jump |
| `Play_Dash` | 冲刺成功开始 | 玩家；冷却/权限失败不发 |
| `Play_Land` | 从空中变为有效着地 | 玩家；过滤地面检测抖动，不每帧发 |
| `Play_Player_Bite` | 四足咬击动作实际开始 | 玩家；空咬也发；站立禁用咬击 |
| `Play_Player_SwordSwing` | 站立且能用剑时剑击动作开始 | 玩家；挥空也发 |
| `Play_PlayerBiteHit_NPC` | 咬击对普通怪造成有效伤害 | 命中点/目标；同一攻击对同一目标去重 |
| `Play_PlayerSwordHit_NPC` | 剑击对普通怪造成有效伤害 | 同上；包括飞行怪与两种地面怪 |
| `Play_PlayerBiteHit_Elite` | 咬击对 Elite 造成有效伤害 | 命中点/Elite；不再发 NPC 版 |
| `Play_PlayerSwordHit_Elite` | 剑击对 Elite 造成有效伤害 | 命中点/Elite；不再发 NPC 版 |
| `Play_Player_Hurt` | 敌人接触/地形伤害实际扣血且玩家存活 | 玩家；无敌、完全挡住、无效伤害不发；献祭扣血不自动复用 |

### 普通怪、Elite 与死亡（8 个）

| Event | 唯一触发点 | emitter / 去重要求 |
|---|---|---|
| `Play_Player_Death` | 玩家首次进入死亡 | 玩家或临时 emitter；每生命周期一次；致死伤害优先死亡音，抑制同次 Hurt |
| `Play_NPC_Fly` | 飞行小怪有效振翅节拍 | 飞行怪；当前为含 4 个声音的 Random Container，未见显式连续循环设置。先按短音节拍接入并试听确定节拍，不能只因名称 Fly 就当永久 loop |
| `Play_NPC_Footsteps` | 两种地面小怪移动落脚帧 | 对应怪物；共用 Event，停止移动/离地/死亡不发；飞行怪不用 |
| `Play_NPC_Hurt` | 任一普通怪实际受伤且存活 | 受伤怪；不是“它碰到玩家”时发 |
| `Play_NPC_Death` | 普通怪首次死亡 | 怪物/临时 emitter；每生命周期一次；同次不再发 NPC_Hurt |
| `Play_Elite_Hurt` | Elite 实际受伤且存活 | Elite；不用 NPC_Hurt |
| `Play_Elite_Death` | Elite 首次死亡 | Elite/临时 emitter；停止移动音与接触伤害；不直接判游戏胜利 |
| `Play_Elite_Attack` | **已存在，当前不接主动攻击逻辑** | 内容是猫叫 Random Container。若保留为“成功碰撞造成伤害时的发声”，必须由音频负责人确认语义；确认前不调用，也不在追逐 Update 中调用 |

Elite 目前没有专用脚步 Event；建议先复用 `Play_NPC_Footsteps` 作为其移动声，需试听确认是否符合体型。专用 Elite 移动音属于后续新增需求，不假装已有。飞行声若后续改成连续 loop，应改为“进入飞行状态只启动一次、离开/死亡/回收停止对应 playing ID”，并同步更新本文。

### 世界交互（5 个）

| Event | 唯一触发点 | emitter / 去重要求 |
|---|---|---|
| `Play_Door_Open` | 门状态 Closed → Opening，动作开始时 | 门；2 秒开门过程只发一次，不在第 2 秒又发；重复交互不重播 |
| `Play_SwitchActivate` | 开关首次有效激活 | 开关；失败/重复激活不发；联动门各自发自己的开门音 |
| `Play_Box_Open` | 领取事务成功且箱子变为已领取 | 宝箱；延续旧契约的“成功领取”语义；仅打开选择 UI/取消不发 |
| `Play_Portal_In` | 已验证落点、代价提交后玩家仍存活，实际开始离开 | 源传送门；本文约定 In=进入源门。取消、无效落点、付费致死均不发 |
| `Play_Portal_Out` | 位置迁移成功、实际抵达 | 目的传送门；本文约定 Out=走出目的门；一次成功迁移只发一次 |

程序必须对照音频试听确认 Portal In/Out 的听感与上述语义对应。源端声音不要挂在会瞬移的玩家上，否则声音会跟着跳到目的地。声音完成回调不负责扣费、移动或解锁门。

### UI 与构筑反馈（7 个）

| Event | 唯一触发点 | 去重要求 |
|---|---|---|
| `Play_UI_Click` | 普通按钮的有效确认被接受 | 全局 UI；失效按钮不发；卡片确认走专用 Event |
| `Play_UI_Hover` | 普通按钮首次获得悬停/导航焦点 | 焦点未改变不重发；卡片走专用 Event |
| `Play_Card_Hover` | 卡片获得悬停/导航焦点 | 不叠加通用 UI_Hover |
| `Play_Card_Selected` | 卡片选择被系统接受 | 不叠加 UI_Click；进入满槽替换步骤可作为一次被接受的 UI 选择，最终业务音等事务真正完成后才发 |
| `Play_Card_PopOut` | 一组卡片面板实际展示 | 建议每次面板出现一次，不对布局刷新逐卡重复；若需逐卡动效节拍再统一调整 |
| `Play_Player_AbilityGain` | 构筑项由未持有变为持有，事务完成 | 不能只因为点击卡片就发；即时回血等增益不自动按“能力获得”处理 |
| `Play_Player_AbilityLoss` | 构筑项实际移除，事务完成 | 取消/替换失败不发；姿态导致武器暂不可用但仍持有，不发武器丢失音 |

目前没有独立的武器获得/丢失 Event。建议技能与装备统一按构筑项的真实增删复用 AbilityGain/Loss，由程序与音频负责人在映射层明确。满槽替换成功可各发一次 Loss 与 Gain；不可在选择旧项前就发 Loss。

### 音乐（7 个）

| Event | 触发条件 | 注意 |
|---|---|---|
| `Set_State_Exploration` | 新局初始化、Elite 战斗结束后回探索 | 设置 Music_Group=Exploration；初始化时先设 State 再 Play |
| `Play_Music_State` | 引擎/Bank 就绪且当前没有音乐实例 | 持久全局对象，一次启动；切状态不重复 Post |
| `Set_State_Elite` | 运行控制器确认首次进入 Elite 战斗 | Music_Group=Elite；进入触发区需防重复，普通怪不切 Elite |
| `Stop_Music_State` | 退出关卡/终止本局或需停止背景音乐 | 在同一音乐 emitter 上停止，避免停止对象作用域不匹配 |
| `Set_State_Level1` | 出生在第一层，或进入/返回第一层 | Exploration_Level=Level1；已包含于 SB_Main |
| `Set_State_Level2` | 进入/返回第二层 | Exploration_Level=Level2；已包含于 SB_Main |
| `Set_State_Level3` | 进入第三层 | Exploration_Level=Level3；已包含于 SB_Main |

`Music_Group` 有 `None / Exploration / Elite`。**当前 None 也映射 Exploration，不能用 None 表示静音。** 当前没有 Victory、Dead、Pause 音乐状态，也没有独立胜利 Event。建议首版死亡/胜利时停止背景音乐，独立结算音乐待音频补充；胜利条件仍由游戏逻辑决定。

已核实三个 Set_State_Level Event 均为对应 Set State Action，不含重播音乐 Action。音乐轨道、1.5 秒过渡、循环与楼层区域配置见第 3 节。

## 6. 程序接入规则与旧接口迁移

推荐继续沿用旧设计的单一音频入口：业务发成功事实，音频适配层将事实映射到 Event。Wwise 类型、字符串、Bank 加载集中在音频层。**当前分支没有 GameAudio/AudioCue 实现；以下是移植时的映射要求，不能直接当作已可编译 API。**

| 旧 AudioCue | 当前 Event/迁移处理 |
|---|---|
| PlayerJump | 首跳 → Play_Jump；二段跳需增加区分信息 → Play_DoubleJump |
| PlayerBite | Play_Player_Bite，只对应动作开始；命中音另走有效伤害通知 |
| PlayerWeaponAttack | Play_Player_SwordSwing，只对应挥剑；不兼任命中音 |
| PlayerHurt / PlayerDied | Play_Player_Hurt / Play_Player_Death |
| ChestOpened | Play_Box_Open，确认领取成功后 |
| AbilityGained / AbilityLost | Play_Player_AbilityGain / Play_Player_AbilityLoss |
| SwitchActivated | Play_SwitchActivate |
| DoorOpened | Play_Door_Open，但触发时机必须明确为 Opening 开始，不能沿用“完全打开后” |
| Teleported | 只能覆盖抵达 → Play_Portal_Out；源端 Play_Portal_In 需额外成功离开通知 |
| UIConfirm | 普通确认 → Play_UI_Click；卡片 → Play_Card_Selected，不能二者叠加 |
| BossStarted | 若确指本次 Elite 战斗开始 → Set_State_Elite；不触发 Elite_Attack |
| RunWon | 当前无匹配胜利音 Event；可按首版策略停音乐，不虚构 Play_RunWon |

新增接入事实包括：脚步、振翅、二段跳、冲刺、落地、命中（攻击类型+目标类型）、NPC/Elite 受伤与死亡、UI 焦点、卡片展开、音乐生命周期。若团队已有稳定枚举，只追加身份，不重排旧数值。停止指定播放实例与暂停/恢复能力需要由音频后端提供，旧 Play/StopAll 端口本身不足以表达所有细粒度控制。

### 身体碰撞伤害的顺序（逻辑示例，不是现有 API）

```text
怪物接触玩家
  → 由 gameplay 依据伤害间隔/无敌状态尝试结算
  → 未实际扣血：不发 Hurt，也不发所谓 Attack
  → 实际扣血且玩家存活：Player_Hurt 一次
  → 玩家死亡：Player_Death 一次，同次抑制 Player_Hurt
```

持续接触可按 gameplay 的真实伤害 tick 产生后续受伤音；不要用 OnCollisionStay2D/OnTriggerStay2D 每帧直接 Post Event。音频只消费结算结果，不额外扣血或改变伤害冷却。

```text
玩家攻击动作开始 → Bite 或 SwordSwing 一次
  → 命中且伤害成功 → 按 NPC/Elite、咬/剑选唯一 Hit Event
  → 目标存活 → 对应 Hurt；目标死亡 → 对应 Death（同次抑制 Hurt）
```

攻击动作音、命中材质音和目标受伤声是不同层次，可以共同存在；不得由攻击者和被攻击者各重复播同一个 Hit Event。使用“攻击实例+目标实例”去重，避免多 Collider 重复触发。

## 7. 组员需要配置的对象

| 对象/模块 | 必须提供的绑定/通知 | 责任 |
|---|---|---|
| 三层音乐区域 | BoxCollider2D Trigger、楼层 ID、玩家过滤、去重；出生/传送/重开主动刷新 | 关卡+程序 |
| 启动/运行控制器 | Bank 就绪、音乐全局对象、暂停/恢复、重开/退出、Elite 战斗与胜负通知 | 程序/总控 |
| 玩家 | emitter；落脚、首跳/二段跳、冲刺、落地、动作开始、真实伤害与死亡通知 | 玩家程序+动画 |
| 飞行小怪 | emitter；振翅节拍、实际伤害/死亡、回收清理 | 敌人程序+动画 |
| 两种地面小怪 | 每实例 emitter；有效落脚、实际伤害/死亡、回收清理 | 敌人程序+动画 |
| Elite | emitter；移动落脚、真实伤害/死亡、战斗进入/结束通知；不加主动攻击技能 | 敌人程序 |
| 石门 | emitter；Closed/Opening/Open 状态；2 秒动作开始通知；重复开门锁 | 关卡程序+动画 |
| 开关/宝箱 | emitter；成功激活/成功领取通知 | 交互程序 |
| 传送门 | 源/目标 emitter；代价提交且存活、迁移成功两个阶段通知 | 传送程序 |
| UI | 全局 emitter；焦点变更、有效确认、卡片展开通知；与通用按钮音互斥 | UI 程序 |
| Wwise | Event/素材映射、Bank、循环、门时长、Bus/空间化、音量与限声 | 技术音频 |

首版不要求 RTPC 或怪物类型 Switch：当前这两个工作单元均为空。不要给组员配置不存在的 `Speed`、`Health`、`EnemyType` 等参数。以后有新增需求先创建并更新映射清单。

## 8. 合并前验收清单

- [ ] 加载 SB_Main，覆盖全部 39 个 Event；引擎日志无 Event/Bank/media 缺失。
- [ ] 分别从三层出生，音乐正确；Level1→Level2→Level3→Level2→Level1 按 1.5 秒过渡，音乐不重复启动。
- [ ] 区域边界/多 Collider 无抖动；传送、返回下层、重开能刷新楼层；第一层环境床超过 30 秒不中断。
- [ ] 队友 fresh checkout + LFS 下载后无需 Authoring 也能运行已交付音频；导出包在另一台电脑播放正常。
- [ ] 游戏开始只有一份音乐；探索/Elite 切换不叠播；播放超过 Elite 曲长验证循环；重开无遗留状态。
- [ ] 首跳、二段跳、冲刺仅成功时各发正确音；落地无抖动连发；脚步只在移动落脚时发。
- [ ] 三种普通怪无主动攻击音；飞行怪不发地面脚步；Elite 不因每帧追逐重复发 Attack。
- [ ] 碰撞但无敌不发 Hurt；持续碰撞只按成功伤害 tick 发声；一次致死只发一次 Death。
- [ ] 咬击/剑击挥空有动作音无命中音；命中 NPC/Elite 正确区分；多 Collider 不重复播同一命中。
- [ ] 怪物死亡与回收后移动声停止，死亡尾音不被当帧销毁截断。
- [ ] 石门 t=0 动作与声音同时开始，t=2.00 秒动作完成；重复操作不重播；尾音与暂停行为按约定验收。
- [ ] 传送取消/无效落点/付费致死无 In/Out；成功一次分别在源与目的地播放；声音不随玩家瞬移错位。
- [ ] 宝箱取消不发已领取音；构筑替换失败不发 Gain/Loss；失去站立但保留剑不误报丢剑。
- [ ] UI 悬停不每帧重播；卡片专用音与通用 UI 音不叠发；暂停时 UI 可操作并发声。
- [ ] 世界声音远近、多人声同时播放的音量/限声策略经过试听，无全图脚步同音量堆积。

## 9. 本次核查与待验证

已静态核对：39 个 Event 名称/楼层 Action 目标、五轨 State 音量、1.5 秒过渡、Exploration→Hope 循环及环境床延展、Windows SB_Main 的 39 个 Event/51 个媒体、51 个素材引用无缺失、Integration 版本/路径、Git/LFS。
本轮未执行 Wwise GUI 试听、Unity 编译/Play、Windows/Mac 构建或其他电脑测试。静态配置正确不等于运行已验收。
待音频确认：石门 Event 仍为 2.58 秒；Elite 曲末循环；环境床与五轨整段接缝；35 m 衰减的实际听感。
生成 TXT 的尾随空白属于生成文件格式，不手工重写。本文只更新 Markdown，未修改音频/工程配置。

证据入口：Wwise Events/Containers/States/SoundBanks/Attenuations 工作单元、GeneratedSoundBanks/Windows/SB_Main.json 和 Event/*.json、Assets/WwiseSettings.xml、Assets/Wwise/Version.txt、ProjectSettings/ProjectVersion.txt。

