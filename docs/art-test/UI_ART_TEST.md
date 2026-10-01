> 2026-10-01 最新真实UI集成与卡牌统一布局见 [UI_REAL_INTEGRATION.md](UI_REAL_INTEGRATION.md)。以下保留历史记录；当前Ming规则、真实入口和验证范围以最新交接为准。

# PAWGATORY UI 测试与真实接入

当前版本：2026-10-01，卡牌图标、精简文案和完整CardRoot整体旋转。没有 commit / push。
本轮详细20项映射、文案、缺项、Inspector参数和截图说明见 [CARD_ICONS_INTEGRATION.md](CARD_ICONS_INTEGRATION.md)。

## 纯视觉测试

打开 `Assets/Scenes/Tests/Art/UI_ArtTest.unity` → Play。也可用菜单 `PAWGATORY > UI Art > Open test scene`。

| TEST CONTROLS 控件 | 功能 |
|---|---|
| Login / HUD / Cards | 登录、游戏HUD、叠加卡牌弹窗 |
| Prev icons / Next icons | 循环查看全部20图标和精简文案；每次移动3项 |
| Live copy | 腿、手剑、普通尾三张真实奖励的短文案 |
| 4-line sample | 两行标题、四行正文的最长样例 |
| Left / Center / Right / None | 选择高光 |
| 1 card / 3 cards | 单卡居中或三卡 |
| Cards enabled / disabled | 可用/灰态视觉，不模拟支付资格 |
| Walk play / pause | 主界面已有带剑角色walk动画 |
| HP Slider | 0–100%视觉测试，参考上限200 |
| 身体部件Toggle | 身体组合，手剑共同变化，普通尾/火尾互斥 |
| Edge 01/02/03、Edge animation | 登录帧图、登录及Gameplay动效播放/暂停 |
| PNG overlay | 原PNG与80%黑遮罩比较 |

收起右上 TEST CONTROLS 查看完整右卡。ArtTest 的 Start Game 只切测试页，图库不改变真实游戏状态。

## 真实游戏测试

打开 `Assets/WhiteBox/Scenes/Level_Whitebox.unity` → Play。靠近未领取Chest按 **E**；鼠标点击确认，键盘方向键/Enter或手柄方向输入/South选择，Cancel/Esc取消。

当前七箱池的 `legs / arms / tail / heal / max-health / attack` 都已显示对应图标和短文案。ChoiceMenu仍使用原ChoiceCard引用；无需重新生成场景。当前地图尚无教学、收费、喷火或支线专用发放入口。

原 PlayerHud/BodyHudArt 读取同一个PlayerState。HP骨头条数字黑色，手剑同时显示/消失，普通尾显示Dash，火尾状态显示Fire。GameplayEdge属于常驻Gameplay HUD，Choice为叠加Modal，开关时相位持续；LoginEdge单独生命周期。

## 当前视觉与结构

- 20张原PNG已导入 `Assets/Art/UI/Cards/Icons/`，均320×492，配meta，不使用本机绝对路径。
- 正式 `Assets/Prefabs/Choice/ChoiceCard.prefab` 的根就是CardRoot，保留原Button/ChoiceCardView；子节点为SelectedGlow、Frame、Icon、DescriptionText、TitleBannerText。三列统一正面框，由根+4/0/-4度带动全部内容，无Frame反向旋转。
- 共用 `CardVisualStyle.asset` 和 `CardPresentationCatalog.asset`。标题24pt固定牌匾，正文22pt放卡面；不启用AutoSize/内容驱动布局/Tooltip。20份文案均不超过2行标题、4行正文。
- 真实ChoiceCardView只新增显示映射；回传原ID、监听和取消行为不变。ChoicePanel、Gameplay、奖励数值、事务和Level本轮未修改。
- Login原三边框0.8秒切换、呼吸8秒；Gameplay原UI_HUD_Edge单图8秒呼吸。主界面使用现有Dog2_Move_Sword三帧8fps。

## 真实与预览的边界

6个现有奖励图标已能从真实箱触发。4种Lose图标已绑定旧项替换ID201/202/203/204，但当前地图池不能自然触发这条满槽换新部件分支。其他10种图标已有完整视觉样例，仍缺正式ID/配置/事件入口。

`swap-tail`已有通用简短确认文案，缺旧尾类型数据，图标留空；独立FlameBreath没有本批对应的舍弃图标。后续不能将预览关键词直接当成已存在的选项ID。

## 本轮验证

Unity6000.2.9f1隔离副本保存/重开/实际Play，**3884项断言通过，新增运行错误0**。覆盖20图标三列、7种分辨率、文本边界、图标翻页按钮、真实宝箱输入/取消/点击提交/键盘和虚拟手柄确认、缓存候选和连续Edge。详见 [UI_CARD_ICON_CHECKS.txt](UI_CARD_ICON_CHECKS.txt)。

当前截图：`ui-preview/cards-icons/`。RealChoice截图是UI Canvas单独渲染，世界画面省略。没有全局键鼠操作。用户侧视觉和实体手柄仍请人工检查；既有包DLL/许可启动日志不代表新增模块运行错误。
