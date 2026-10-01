# Opening Story Intro 集成交接

## 已接通

MainMenu → 实际Start按钮 → 当前Level_Whitebox → 首帧黑幕 → 原第一段 → “I want to go home.” → 黑幕淡出 → Gameplay。

- 本轮fetch：Ming `794b6b30b7e2f1c0f39b1b8d5ed3cb80d337aa11`，Soap `68a96cd414248226300b1bf99f482a9960a0187b`。Dada开始为 `f3fa49f`，工作区干净，之前UI已在提交中。
- Ming用no-commit同步，唯一冲突是右下说明RectTransform与紧邻新增Hint_chest；保留Dada三项布局值及Ming新增对象树。未整场景选择ours/theirs。
- Soap只提取下列13个Intro模块文件，全部保持源提交原字节/GUID。未merge Soap，未采用其旧Level场景，未覆盖Soap.handoff。
- Soap的Setup在隔离Unity原生执行成功：当前场景恰好一个Intro，显式绑定现有唯一RunController；场景保存成功。同步回正式项目时只加入Setup生成的Prefab instance及SceneRoots条目，保留最新地图/宝箱/人物/收费/敌人及Dada UI全部既有对象；排除Unity无关Tilemap序列化重排。
- MainMenu本身未增加第二套Intro。Intro为Level的scene-local表现，Canvas order32760覆盖HUD/Edge，完成关闭IntroRoot。保留ownsPause、默认两页文案/节奏；没有新Skip/点击继续/音频/打字机。

## 实际验证

- 从真实MainMenu点Start；sceneLoaded时原生1920×1080渲染RGB全黑，文本alpha0，无地图/HUD/Edge像素；两次Play都通过。文本开始/退出均按源实现淡变，截图见下方。此证据为隔离Unity自动渲染，用户窗口的人眼无闪屏验收仍请亲自确认。
- 默认理论约15.9秒；完整成功流程实测 **16.754秒**（含本机帧调度及取图开销）。文案/参数没有加速修改。
- 第一轮正常完整播放后A/D、Space实际移动/跳跃，E打开真实Chest三卡，Esc关闭；真实HP绑定、Gameplay Edge在Choosing继续运行均通过。
- 同一实例完成后再PlayIntro不会重播已通过。用户最后要求“只完成这个黑屏”，后续Stop/Play扩展检查已停止，不标记为完成。
- 源码原有ownsPause保护完整保留。正常播放结束恢复Playing已通过；额外暂停归属检查按用户收口要求停止。
- 最终检查记录见 `OPENING_INTRO_CHECKS.txt`；Intro warning/error为0。前两轮剧情通过，旧出生点向左找Chest03的路线检查失败。核对发现最新Ming把出生y改为-1.41，Chest03仍在8.5，旧路线已失效。最终用位置夹具放到实际Chest03附近，仍由真实E/Interactor/Chest/Choice处理；没有改出生点或地图，不把位置夹具写成自然走到箱子。保留首轮日志 `OPENING_INTRO_FIRST_CHECKS.txt`。
- 完整剧情和真实交互通过后，自动Stop/Play夹具因临时InputSettings销毁触发测试工具错误。已修正工具恢复原设置，但按用户“只完成这个黑屏”要求停止后续检查；未将该夹具加入正式项目。没有运行全套奖励/敌人/地图回归，没有导出游戏，没有人工操作用户桌面。

## 你现在怎么看

停止当前Play，等导入编译完成，再打开MainMenu或Level_Whitebox点Play，点击Start Game。黑底两页结束后，用A/D、Space移动跳跃，靠近箱按E。

## Inspector调快

打开 `Assets/Prefabs/UI/OpeningStoryIntro.prefab`，选根对象的OpeningStoryIntro组件，在 **Unscaled seconds** 调整：

| 字段 | 当前默认 | 作用 |
|---|---:|---|
| minimumDisplaySeconds | 3 | 每页全亮最短停留 |
| maximumDisplaySeconds | 12 | 每页全亮最长停留 |
| baseReadingSeconds | 1 | 基础阅读时间 |
| secondsPerCharacter | 0.07 | 每个非空白字符增加的停留 |
| textFadeInSeconds | 0.4 | 每页文字淡入 |
| textFadeOutSeconds | 0.4 | 每页文字淡出 |
| blackFadeOutSeconds | 0.5 | 最后黑幕淡出 |

要先缩短节奏，主要降低secondsPerCharacter、maximumDisplaySeconds；第二页受minimumDisplaySeconds限制。当前全部保持Soap默认，未替你改值。

## 实际提取文件

- `Assets/Prefabs/UI.meta`
- `Assets/Prefabs/UI/OpeningStoryIntro.prefab`
- `Assets/Prefabs/UI/OpeningStoryIntro.prefab.meta`
- `Assets/Scripts/UI/Intro.meta`
- `Assets/Scripts/UI/Intro/Editor.meta`
- `Assets/Scripts/UI/Intro/Editor/OpeningStoryIntroSetup.cs`
- `Assets/Scripts/UI/Intro/Editor/OpeningStoryIntroSetup.cs.meta`
- `Assets/Scripts/UI/Intro/Editor/Regrowth.UI.Intro.Editor.asmdef`
- `Assets/Scripts/UI/Intro/Editor/Regrowth.UI.Intro.Editor.asmdef.meta`
- `Assets/Scripts/UI/Intro/OpeningStoryIntro.cs`
- `Assets/Scripts/UI/Intro/OpeningStoryIntro.cs.meta`
- `Assets/Scripts/UI/Intro/Regrowth.UI.Intro.asmdef`
- `Assets/Scripts/UI/Intro/Regrowth.UI.Intro.asmdef.meta`

## Git与后续

没有commit/push。最新Ming同步是尚未提交的merge状态，冲突已解决；Intro模块/场景增量也留在工作区供用户看。用户确认后再自行决定提交。原Soap handoff只读，公共规范与其他人的交接仅随最新Ming同步，未另行改写。
