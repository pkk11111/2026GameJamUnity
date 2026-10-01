# C06 主场景动作试玩

2026-10-01；Ming/2d92989上的未提交接线，Unity6000.2.9f1 / Windows64。规则入口AGENTS规范21、源码契约8。复用Soap/27ef56c模块增量（no-commit试合并，HEAD保持2d92989），当前只证明原动作集成，未完成combat_rules的新战斗迁移。

## 手动试玩

1. 打开`Assets/WhiteBox/Scenes/Level_Whitebox.unity`，点击Play，再点Game视图聚焦。若之前跑了Auto，先Stop再Play。
2. 出生点保留用户的(-12.14,8.6)，三槽EMPTY。地图暂时跳过躯干教学，所以此处已经有基础咬击；正式头部开局教学仍未接。
3. A/D或方向键移动，Space一段跳；Enter或鼠标左键攻击。无手时青色短咬击，有手时金色斜剑光；方向跟随最后左右输入。HUD同时显示BITE / No arms或SWORD / Arms。
4. 附近Chest_03位于(-15.5,8.5)，靠近按E。随机三选一不保证出手剑；只有确认Arms + Sword卡后才占一个槽并切换剑击。打开或取消不授予；失去/替换Arms后回到咬击。没有独立剑卡或站立卡，无腿也能剑击。
5. 领取腿卡才有第二跳，普通尾卡才有Shift冲刺；空槽时只有基础移动/跳跃。Esc暂停/恢复，菜单中Esc取消；R仅回出生点，不能复活、重新生成箱子或重置已死敌人。

青色/金色都只有0.18秒，是动画到齐前的占位。白方块本身暂不变成完整身体，不根据角色图片判断能力；以槽位与HUD为准。鼠标在选卡时用于UI，建议手动对照攻击使用Enter；菜单Enter也会确认选项，不用它测试“菜单中无操作”。

## 场景与脚本职责

| 对象 | 配置与职责 |
|---|---|
| Test_Player / PlayerState | 唯一HP/三槽/身体/伤害，开局Items为空；当前跳过躯干教学 |
| WhiteboxPlayer2D | 唯一刚体写者；原速度7、跳高4.5、冲刺距离3，读取Legs/Tail权限 |
| PlayerAttackRouter | 唯一普通Attack消费者；读取同一状态选择Bite/Sword |
| PlayerBiteAttack / PlayerSwordAttack | 复用原即时圆命中与独立冷却、音频入口；动画不得再次结算 |
| PlayerFacing2D | Soap统一朝向；显式绑定InputReader，主图死区0.1；咬/剑及WhiteboxPlayer2D冲刺读取同一源；不翻根/不消费按键 |
| AttackPresentation | 无物理组件的Origin/青色Bite/金色Sword，后续美术替换视觉；原根缩放保持1×1.5 |
| Chest / ChoicePanel / PlayerHud | 原有实际领取事务和只读三槽显示，不再创建第二套卡槽或自动授予组件 |
| Trial Enemies - stationary T09 | 4个原EnemyBasic prefab实例；显式Run/玩家引用，无AI |

站桩敌人位置：(-51,1.5)、(-44,9.5)、(-54,31.5)、(-45,37.5)，位于原普通敌人标记附近真实地面。原红色标记均保留；其他红色大块/精英仍只是标记，打标记不会扣血。真实敌人死亡后消失并关闭碰撞。

旧集成测试值：玩家咬10/剑15；Bite半径1.2、Sword半径1.8、各0.6秒冷却；敌人39HP/接触7/各自0.6秒。配置位于Assets/Configs/Bite、Sword、EnemyBasic；伤害由PlayerState提供。以上不是新表10/20、共享0.5秒及150/10敌人的完成证明，不修改来源CSV迁就旧实现。

## 自动检查与实际结果

全新主图Play后，选择`Tools/pawgatory/C06/Run Main Action Checks (Play)`。当前39 passed / 0 failed；覆盖真实输入移动/攻击/朝向/跳跃/冲刺、空槽禁权限、真实宝箱候选/卡按钮确认Arms/取消/槽更新/失去回咬、运行阶段与敌人伤害/死亡。

检查为了隔离物理在本次Play创建临时平台、移动实际敌人，用真实状态命令准备腿/尾，宝箱走真实TryInteract与卡按钮；不等同人工按E走完全程。驱动只在UNITY_EDITOR编译，以DontSave临时挂载，不能保存进主图。C05随后全新Play回归69/0；Soap方向更新后已取得T08 77/0与T09 50/0；T07按用户收尾要求不再续查，历史42/0不冒充当前结果。

自动检查包含死亡用例，完成后无法移动是测试的终态；Stop再Play恢复手动。T08 Auto还会FreezeAll。已复验T08全新Manual：D约0.3秒正常右移1.5单位；没有复现手动模式不能移动，也不能据此断定用户当时一定是Auto。

本轮检查了咬/剑画面，预览在本机忽略目录Builds/C06；未构建新Windows包或验证正式动画/实际Bank音效。旧C05试走exe不包含这轮接线。

## 下轮仍需迁移

共享普通攻击冷却、前摇/持续有效/后摇、前缘矩形/墙遮挡、动作方向锁、冲刺与攻击互斥、共享受伤保护/多源仲裁、伤害倍率/新奖励公式、AI/精英、正式动画与音效。完整教学/收费/完整支线结算继续按授权推进。主图接通不等于新版T全量验收。

最新来源边界：用户明确以Ming为主；27ef56c未带入地图/Wwise/Core/Runtime/Input覆盖，主图保存的出生点仍(-12.14,8.6)。统一朝向通过后已移除本轮临时WhiteboxAttackFacing脚本及meta，不能保留两套方向源。咬/剑参考Origin保持右向，视觉只读HitCenter；主图不再镜像锚点。
