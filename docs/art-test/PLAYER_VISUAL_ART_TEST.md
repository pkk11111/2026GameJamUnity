# 主角角色动画测试模块

## 交付范围与入口

入口：`Assets/Scenes/Tests/Art/PlayerVisual_ArtTest.unity`，Unity 6000.2.9f1。
本包只交付主角美术与独立测试环境：39 张 PNG、95 帧、52 个 AnimationClip（39 个动作 + 13 个临时 Idle）。
3 个敌人资源保留在 Dada 本地，不属于本次 Ming 交付。没有 PSD、GIF、缓存或测试包。
正式玩家、PlayerState、输入、攻击伤害、主地图和 Wwise 未接入或修改。

资源路径：
- PNG：`Assets/Art/Characters/Player/Sprites/`
- Clip：`Assets/Art/Characters/Player/Animations/`
- Library/UI 配置：`Assets/Art/Characters/Player/Controllers/`
- 独立测试脚本：`Assets/Scripts/Tests/Art/`

场景中的 PlayerVisualRoot 已有 SpriteRenderer 与 Animator，Harness 通过 PlayableGraph 播放 Clip；没有单独的 Animator Controller 或 Prefab 资产。
测试场景不在正式 Build Settings 中。取得提交并让 Unity 导入即可打开场景，不需要 Photoshop 或本机导出目录。

## 测试 UI

1. Play 后选择 Head / Body / Arms+Sword / Legs / Normal Tail / Flame Tail 六个 Toggle。
2. 点击 Idle / Move / Jump / Bite / Attack / Fire。右侧列出当前组合、实际图片/Clip 名称、帧数和状态。
3. FPS 滑条 2–16 实时生效；单帧动作使用 Hold，默认 0.15 秒，可调 0.05–0.5 秒。
4. Move 循环；其他动作单次播放，结束回该组合 Move 首帧。Replay 重播，Pause 暂停，左右逐帧按钮检查顺序。
5. PASS / FAIL 只写本次会话记录和 Console，不写真实玩家状态或在线美术表。

状态优先级：非法组合 INVALID → 无动作权限 UNAVAILABLE → 合法动作缺图 MISSING → OK。
两尾通过 Toggle 互斥；关闭 Body 后仍勾手、腿、尾属于 INVALID。无头也非法。
Arms 始终包含剑；不存在有手却空手的形象。NoArms 是无手，咬击为普通攻击。

## 导入与调参

- 横向左到右，500×500 固定画布每格；保留透明边缘。原 PSD 组内顶到底为导出帧序。
- Sprite Multiple，Full Rect，Padding/Offset 0，PPU 100，Pivot (0.5,0.5)。
- Compression None，Max Texture Size 4096，Bilinear，Clamp，无 Mipmap，NPOT None。
- 所有已有 .meta、Sprite 子资源身份和 Clip GUID 保留。修改时不要删除 .meta 重导。
- Clip Sample Rate 是默认 FPS，下一次请求动作读取；各动作实值见下表。
- Idle 只是 Move 第一帧 + PlayerVisualRoot 0.99–1.01 缩放呼吸，周期 1 秒。这不是正式 Idle 美术完成。
- Library 每个动画的 Clip、Frames 可在 Inspector 替换；Harness 引用已保存于场景。
- Editor 菜单 `Tools/pawgatory/Art Test/Validate Imported Assets` 检查资源；`Run Play Checks` 保存/关闭/重开本测试场景后进入 Play 检查。启动前须退出 Play 并保存场景。
- `Sync Reviewed PNG Manifest` 只在明确要按审核清单重建时使用；默认从仓库内 Sprites 读取，不需要 D: 盘外部源文件。它会校验 PNG 哈希并更新本模块切片/Clip/Library；普通接收者无需运行。

## 当前 MISSING 与待定事项

- Dog0_FlameTail / Bite：无手无腿火尾；用户要求咬击动画暂待补充。
- Dog2_NoArmsFlameTail / Bite：无手有腿火尾；用户要求咬击动画暂待补充。
- Dog2_FlameTail / Fire：有手有腿火尾；尚无素材。

以上保留 MISSING，不用错误身体形象或喷火姿势自动替代。其他已交付 Bite 保持正常。
喷火为独立动作，不替代咬击/剑击。当前 Harness 采用火尾形式；独立 Fire 技能的另一种投放模式不在本测试范围。
双跳可复用 Jump；冲刺、空中攻击、下落/落地、受击/死亡、生长/舍弃的复用或通用效果方案留给正式接线任务。
特效表中的 Projectile_Fireball_01 尚未交付，不因角色 Fire 姿势存在而宣称火球/喷火玩法已完成。

## 保留的素材版本差异

按用户要求保留既有 final PNG：Dog2_Jump_NoArmsFlameTail（2 帧）、Dog2_Move_NoArmsFlameTail（3 帧）、Dog2_Jump_NoArms_Tail（2 帧）、Dog2_Move_NoArmsTail（5 帧）。
这四项与较新橙色 PSD 像素不同，其中最后一项 PSD 是 3 帧。当前交付为保留版，不擅自覆盖。
源文件所有权和命名已由美术确认；完整原始导出/敌人记录继续留在 Dada 本地。

## 13 种身体组合

| 前缀 | Body | Arms+Sword | Legs | Tail | 已有动作（另均有临时 Idle） |
|---|---:|---:|---:|---|---|
| `DogHead` | 0 | 0 | 0 | None | Move, Jump |
| `Dog0` | 1 | 0 | 0 | None | Move, Jump, Bite |
| `Dog0_NoArmsTail` | 1 | 0 | 0 | Normal | Move, Jump, Bite |
| `Dog0_FlameTail` | 1 | 0 | 0 | Flame | Move, Jump, Fire |
| `Dog0_Sword` | 1 | 1 | 0 | None | Move, Jump, Attack |
| `Dog0_TailSword` | 1 | 1 | 0 | Normal | Move, Jump, Attack |
| `Dog0_FlameTailSword` | 1 | 1 | 0 | Flame | Move, Jump, Attack, Fire |
| `Dog2_NoArms` | 1 | 0 | 1 | None | Move, Jump, Bite |
| `Dog2_NoArmsTail` | 1 | 0 | 1 | Normal | Move, Jump, Bite |
| `Dog2_NoArmsFlameTail` | 1 | 0 | 1 | Flame | Move, Jump, Fire |
| `Dog2_Sword` | 1 | 1 | 1 | None | Move, Jump, Attack |
| `Dog2_Tail` | 1 | 1 | 1 | Normal | Move, Jump, Attack |
| `Dog2_FlameTail` | 1 | 1 | 1 | Flame | Move, Jump, Attack |

## PNG / 同名 Clip 清单

| 名称 | 帧数 | 默认 FPS |
|---|---:|---:|
| `Dog0_Attack_FlameTailSword` | 3 | 12 |
| `Dog0_Attack_Tail` | 3 | 12 |
| `Dog0_Bite` | 1 | 6.667 |
| `Dog0_Bite_Tail` | 1 | 6.667 |
| `Dog0_Fire_FlameTail` | 1 | 6.667 |
| `Dog0_Fire_FlameTailSword` | 1 | 6.667 |
| `Dog0_Jump` | 2 | 6 |
| `Dog0_Jump_FlameTail` | 2 | 6 |
| `Dog0_Jump_FlameTailSword` | 2 | 6 |
| `Dog0_Jump_NoArmsTail` | 2 | 6 |
| `Dog0_Jump_Tail` | 2 | 6 |
| `Dog0_Move` | 2 | 6 |
| `Dog0_Move_FlameTail` | 2 | 6 |
| `Dog0_Move_FlameTailSword` | 3 | 8 |
| `Dog0_Move_NoArmsTail` | 2 | 6 |
| `Dog0_Move_Tail` | 3 | 8 |
| `Dog0_Sword_Attack` | 3 | 12 |
| `Dog0_Sword_Jump` | 2 | 6 |
| `Dog0_Sword_Move` | 3 | 8 |
| `Dog2_Attack_FlameTail` | 4 | 12 |
| `Dog2_Attack_Sword` | 2 | 12 |
| `Dog2_Attack_Tail` | 4 | 12 |
| `Dog2_Bite_NoArms` | 1 | 6.667 |
| `Dog2_Bite_NoArms_Tail` | 1 | 6.667 |
| `Dog2_Fire_NoArmsFlameTail` | 1 | 6.667 |
| `Dog2_Jump_FlameTail` | 2 | 6 |
| `Dog2_Jump_NoArms` | 2 | 6 |
| `Dog2_Jump_NoArms_Tail` | 2 | 6 |
| `Dog2_Jump_NoArmsFlameTail` | 2 | 6 |
| `Dog2_Jump_Sword` | 2 | 6 |
| `Dog2_Jump_Tail` | 2 | 6 |
| `Dog2_Move_FlameTail` | 4 | 8 |
| `Dog2_Move_NoArms` | 3 | 8 |
| `Dog2_Move_NoArmsFlameTail` | 3 | 8 |
| `Dog2_Move_NoArmsTail` | 5 | 8 |
| `Dog2_Move_Sword` | 3 | 8 |
| `Dog2_Move_Tail` | 4 | 8 |
| `DogHead_Jump` | 2 | 6 |
| `DogHead_Move` | 6 | 8 |

## 验证与交接

当前结果见 `docs/handoffs/Dada.handoff` 和 `player-package-verification.json`。完整提交文件清单见 `player-package-files.txt`。自动 Play 检查不代替美术本人对姿势、风格和手感的最终验收。
