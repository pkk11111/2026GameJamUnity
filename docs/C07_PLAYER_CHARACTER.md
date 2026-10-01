# C07 主场景角色与火焰试用

打开 `Assets/WhiteBox/Scenes/Level_Whitebox.unity`，重新进入Play。根白方块已隐藏，显示Dada人物。此图仍跳过头部教学，初始有躯干、100HP、三个空槽；未自动给手、腿或尾巴。

| 操作 | 条件与结果 |
|---|---|
| A/D、Space | 基础移动、跳跃；腿卡后可二段跳 |
| Enter / 鼠标左键 | 无手咬击，有手剑击；人物播放对应动作 |
| E靠近宝箱 | 三选一，确认手（剑）卡才长出手剑；预览/取消不改变身体 |
| Q / 鼠标右键 / 手柄右肩 | 持有FlameTail后发射慢飞火焰团；未取得或冷却中不发动 |
| Shift | 普通尾巴才能冲刺；换火焰尾失去冲刺 |
| Esc / R | 暂停 / 白板回位；R不重开、不退还宝箱 |

火焰团沿起手方向以默认1.5单位/秒飞2秒，半径0.6；处于范围内的敌人每0.2秒扣8，完整接触10跳才是80。撞墙/关闭门结束，敌人不会让火团消失；无持续燃烧。冷却从起手算8秒，不通过换尾刷新。允许边走边跳；释放期间不能另咬/剑/冲刺。暂停冻结，丢火尾或死亡立即终止。

宝箱都保持随机三选一，不保证出生点第一箱出现手或火尾；取消不刷新。当前共7项池：腿、手剑、普通尾、火焰尾、回血、上限、攻击。另一种尾卡需要明确确认替换，最多三个槽。

## Inspector 与文件

- `Test_Player/PlayerFireAttack`：duration=2、tickInterval=.2、cooldown=8、speed=1.5、radius=.6；damage来自同一PlayerState.initialFireDamage=8。
- `Test_Player/PlayerCharacterPresentation`：worldScale=.42；身体/方向/运动及动作都显式绑定。改变角色显示只调子视觉，不调根碰撞或运动。
- `MainPlayerAnimationSet.asset`：正式只读映射，复用Dada原切片与Clip，不依赖Art Test。源Clip的frameRate决定播放速度。
- 人物直接使用Dada原始图片，按用户要求保留白纸底，原PNG未改。`PlayerFlame`为临时程序特效，可在fireVisual/fireTrail接正式资源。
- 缺图：Dog0_FlameTail/Bite、Dog2_NoArmsFlameTail/Bite、Dog2_FlameTail/Fire。保留当前身体与攻击效果，缺动作不替换为其他组合。Idle来自Move首帧，受伤/死亡/冲刺仅颜色反馈。

普通咬/剑仍沿用10/15、瞬时圆判定和独立.6秒冷却；本轮.35秒表现互斥不等于新表近战时间轴全部完成。旧攻击卡仍对咬/剑/火各+3，百分比系统尚未迁移。4个站桩敌人仍39HP/接触7，其他地图敌人标记不是业务敌人。火焰尚无正式音频事件，既有咬剑Cue与Wwise配置保持。

## 已执行验证

Unity6000.2.9f1、Windows64目标，C07 30/0、C06回归39/0，主图画面检查完成；未重新构建Windows。旧C05包不含本轮内容。

全新Play后菜单 `Tools/pawgatory/C07/Run Main Character Checks (Play)` 可复跑。它使用临时键盘/平台/敌人判定夹具和真实宝箱按钮，最后令玩家死亡；停止Play再进恢复。驱动不保存在场景，UNITY_EDITOR条件编译，不进入正式包。日志可查看Console `[C07 RESULT]`，本机记录在Logs/C07。

Dada合并仍未提交；本轮不自动commit/push。由用户提交完整源码/meta/配置/场景/文档后再push。
