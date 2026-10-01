> 2026-10-01 最新真实UI集成与卡牌统一布局见 [UI_REAL_INTEGRATION.md](UI_REAL_INTEGRATION.md)。以下保留历史记录；当前Ming规则、真实入口和验证范围以最新交接为准。

# 卡牌图标与真实 UI 接入

2026-10-01，Dada。没有 commit / push；本轮不改玩法、奖励配置、事务、地图、输入资产、HUD 或边缘动画生命周期。

## 查看入口

- 纯视觉：`Assets/Scenes/Tests/Art/UI_ArtTest.unity` → Play → **Cards**。
- **TEST CONTROLS** → **Prev icons / Next icons**：循环预览全部 20 张图标，每次前后移动 3 项，循环后每张都能出现在左、中、右。
- **Live copy**：恢复腿、手剑、尾巴的真实短文案；**4-line sample**：两行标题、四行正文的最长样例。
- 原 Login / HUD / Cards、Left / Center / Right / None、1 card / 3 cards、Cards enabled / disabled、Walk play / pause、HP、身体状态、边缘与 Overlay 控件全部保留。收起 TEST CONTROLS 看完整右卡。
- 真实功能：`Assets/WhiteBox/Scenes/Level_Whitebox.unity` → Play。A/D 移动、Space 跳跃，靠近未领取的 Chest 后按 **E**。Chest_01 约在世界坐标 `(32.5, -11.5)`。
- 鼠标点击；键盘方向键 + Enter；手柄方向输入 + South；Cancel / Esc 沿用原选择流程。菜单使用原 ChoicePanel、Button 和 ChoiceCardView 回调，不直接发放奖励。
- ArtTest 的 Start Game 仍仅切换测试页面；图库中的未接入效果不会发放奖励。

## 原图导入

源目录：`C:\Users\95799\Documents\Codex\2026-10-01\d-01study-2026gamejam-body-ui-x20\outputs\Card_Icons_Export`。

实际扫描 20 张 PNG，**全部 320×492，RGBA 8-bit**；逐张 SHA-256 与源 manifest 一致。原始透明画布、内容位置、文件名和字节完整保留，没有裁切或重绘。

项目路径：`Assets/Art/UI/Cards/Icons/`。每张及目录都有 Unity 生成的 `.meta`；Sprite Single / FullRect / Bilinear / Uncompressed / No Mip。运行时和 Prefab 只引用项目内 Sprite GUID，不依赖本机路径。完整尺寸/校验值见 `CARD_ICON_INVENTORY.json`。

## 明确映射

以下文件名均为 **`UI_Card_Icon_<关键词>.png`**。关键词是美术目录键，**不是新增的 gameplay ID**。

| 关键词 | 真实 option ID | 当前状态 |
|---|---|---|
| RestoreBody | 未配置 | 图库可看；白板跳过身体教学，没有正式单卡发放入口/稳定ID |
| GrowLegs | `legs` | 当前七箱奖励池已接入；再生沿用相同图标 |
| GrowTail | `tail` | 当前七箱奖励池已接入；普通尾给予冲刺 |
| GrowArms | `arms` | 当前七箱奖励池已接入；手剑共同获得 |
| GrowFlameTail | 未配置 | 图库可看；身体身份存在，白板无火尾奖励/实际喷火入口 |
| RestoreHP | `heal` | 当前七箱奖励池已接入 |
| MaxHPUp | `max-health` | 当前七箱奖励池已接入 |
| AttackUp | `attack` | 当前七箱奖励池已接入 |
| LoseLegs | `201` | 正式旧项替换阶段的 Legs ID 已映射；不是收费/舍弃业务入口 |
| LoseTail | `203` | 正式旧项替换阶段的 Tail ID 已映射 |
| LoseFlameTail | `204` | 正式旧项替换阶段的 FlameTail ID 已映射；当前池无火尾 |
| LoseArms | `202` | 正式旧项替换阶段的 Arms ID 已映射 |
| PayHP | 未配置 | 图库可看；正式代价选择ID/安全预览/提交入口缺失 |
| MaxHPDown | 未配置 | 图库可看；正式负面选择ID/效果入口缺失 |
| AttackDown | 未配置 | 图库可看；正式负面选择ID/效果入口缺失 |
| EnemyHPUp | 未配置 | 图库可看；全局敌人强化事件/选项入口缺失 |
| EnemyAttackUp | 未配置 | 图库可看；全局敌人强化事件/选项入口缺失 |
| RegrowLegsAndHeal | 未配置 | 图库可看；底层正面组合奖励可表达，缺支线奖励配置/入口/稳定ID |
| MaxHPUpEnhanced | 未配置 | 图库可看；缺支线强化奖励配置/入口/稳定ID |
| AttackUpEnhanced | 未配置 | 图库可看；缺支线强化奖励配置/入口/稳定ID |

**特殊情况 `swap-tail`**：源请求是“移除旧尾能力、取得选中新尾”的确认，ID 不区分旧尾类型。因此精简为 **Swap Tail / Lose current tail. / Gain selected tail.**，图标明确留空。需要程序提供旧尾身份的明确表现数据，或另提供通用换尾图标。没有凭标题子串猜图。

当前白板只有三种身体项及三种即时奖励，持有全部三身体项后没有第四个合法新部件，所以自然游玩不会进入满槽旧项替换页。四个数字ID已通过真实 ChoiceCard Prefab 的显示/点击绑定检查，但不代表当前地图存在该发放场景。独立 FlameBreath 的替换ID `4` 无对应本批图标，不用 LoseFlameTail 冒充。

## 已落地的精简文案

斜线表示手动换行。标题 24 pt、1–2 行；正文 22 pt、最多 4 行；关闭 Auto Size。没有 tooltip，也没有卡面占位说明。

| 关键词 | 标题 | 正文 |
|---|---|---|
| RestoreBody | Restore Body | Regrow your body. / Unlock HP and bite. |
| GrowLegs | Grow Legs | Double jump. / Uses 1 slot. |
| GrowTail | Grow Tail | Gain a dash. / Uses 1 slot. |
| GrowArms | Grow Arms | Arms and sword. / Slash attack. / Uses 1 slot. |
| GrowFlameTail | Grow Flame Tail | Regrow flame tail. / Gain fire breath. / Uses 1 slot. |
| RestoreHP | Restore HP | Restore 25 HP. / Full HP: no healing. |
| MaxHPUp | Max HP Up | Max HP +15. / No healing. |
| AttackUp | Attack Up | Bite and slash +3. / Uses no slot. |
| LoseLegs | Lose Legs | Lose legs / and double jump. |
| LoseTail | Lose Tail | Lose tail / and dash. |
| LoseFlameTail | Lose Flame Tail | Lose flame tail / and fire breath. |
| LoseArms | Lose Arms | Lose arms and sword. / Return to bite. |
| PayHP | Pay HP | Pay HP. |
| MaxHPDown | Max HP Down | Max HP down. |
| AttackDown | Attack Down | Attack down. |
| EnemyHPUp | Enemy HP Up | Enemies gain HP. |
| EnemyAttackUp | Enemy Attack Up | Enemies hit harder. |
| RegrowLegsAndHeal | Regrow Legs / + Heal | Regrow legs. / Gain a double jump. / Restore HP. / Uses 1 slot. |
| MaxHPUpEnhanced | Greater Max HP | Gain more max HP. |
| AttackUpEnhanced | Greater Attack | Gain more attack. |

真实六项已核对 `V5_WhiteboxRewards` / `V5_RegrowthRewards`：25回血、+15上限且不回血、咬击/剑击+3。腿给予双跳、普通尾给予冲刺；没有写成改变移动速度。再生标题分别是 Regrow Legs / Arms / Tail。旧项替换页使用 **Replace Legs / Arms / Tail / Flame Tail**，正文说明移除旧项、取得选中奖励；不是单独支付代价。

其余图库文案是视觉建议，未配置的费用、强化数值没有编造。它们需要正式语义/数值确定后再接到相应ID。

## 卡牌结构与参数

```text
ChoiceCard（原Prefab根，也是完整CardRoot；保留原Button/ChoiceCardView）
├─ SelectedGlow
├─ Frame
├─ Icon
├─ DescriptionText
└─ TitleBannerText
```

- 三列统一复用 `UI_Card_Frame_Center.png`，原左右两张PNG仍保留。用根节点 +4° / 0° / −4° 实现倾斜；五个子节点局部旋转全部为0，没有 Frame 反向旋转或标题单独偏移。
- 卡根 415×614；标题中心 `(0,-220)` / 尺寸280×58；正文中心 `(0,-119)` / 尺寸280×108；图标中心 `(0,45)` / 尺寸280×430.5，保留原320:492画布比例。全部固定中心anchor/pivot，无Layout/ContentSizeFitter。
- `Assets/Art/UI/Cards/CardVisualStyle.asset` 是共用布局入口；`CardPresentationCatalog.asset` 是20关键词/10图标ID映射/换尾无图确认和精简文案入口。
- ChoiceCardView 新增可选 presentation 字段，仅在 Bind 填显示。回传的仍是原 option.Id，监听、确认、Cancel不变。ChoicePanel、奖励配置、ChestClaimTransaction、Core、输入和状态源字节不变。
- 精简描述需要与目录里的 **sourceDescription 完全匹配**。程序改了原描述后保留新原文，不用旧短文案遮盖新数值。新/未映射选项留空图标；后续应补映射并重新验收排版。长未知文案以固定区域截断兜底，不把它当已审核文案。
- ChoiceMenu 仍通过原 cardPrefab GUID 实例化该组件，因此真实场景自动得到同样视觉。本轮不需要重复修改/覆盖 ChoiceMenu 或 Level 实例。
- Gameplay HUD/Edge独立常驻；原慢速边缘、登录walk和状态显示本轮保留。

## 验证和人工确认

使用隔离 Unity 6000.2.9f1 副本保存/重开/Play，未操作全局键鼠，未关闭用户编辑器。检查记录见 `UI_CARD_ICON_CHECKS.txt`。

覆盖：20图标×左/中/右、固定标题/正文边界及行数、两配置六项短文案、源文案变化回退、未知ID、10个图标ID及swap-tail显示/原ID点击回传、图标翻页按钮持久接线、7种分辨率；真实E→Chest→Coordinator→Choice；鼠标点击/键盘/虚拟手柄导航确认；Cancel/Esc无奖励副作用且缓存候选不变；7箱；HUD/Edge连续播放。

预览在 `ui-preview/cards-icons/`，都是实际Unity Canvas渲染。RealChoice画面省略世界渲染，只用于UI检查。既有Unity.Collections测试DLL/许可客户端启动日志单列，不代表全项目Console零错误。

需要人工确认：

1. 正中卡框统一后的左右倾斜、图标保留原画布位置的大小与留白、24/22文字的视觉感受。
2. 本批20图标资源齐全；新增费用/教学/支线入口需要程序给稳定ID、真实数值、可选状态及禁用原因，才能在正式卡面显示。不要让图标文件名反向定义玩法。
3. `swap-tail` 补旧尾类型或通用换尾图；独立FlameBreath如上线需独立的舍弃图标。
4. 当前选中Glow仍用已有一张倾斜素材。它跟随整卡旋转；是否补正面Glow由美术判断。
5. 手柄检查用Unity虚拟设备；实体手柄手感由用户在真实编辑器复核。

## 修改范围

新增20 PNG及meta、Icons目录meta、CardPresentationCatalog脚本/资产及meta、UICardIconsRevision编辑器脚本/meta；更新ChoiceCard.prefab、CardVisual/Style及共享Style资产、ChoiceCardView/Choice程序集引用、原UIArtPreview/场景、迁移兼容入口和验证器、Dada.handoff及本说明。已有资源GUID保留；没有项目外绝对路径写进运行组件。
