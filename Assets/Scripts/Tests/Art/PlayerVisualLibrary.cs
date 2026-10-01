// 职责：独立美术测试的组合与动画引用，不读取或写入正式玩家状态。
// 模块：player-visual-art-test；依赖：UnityEngine；所有引用可在 Inspector 替换。
// 交接：docs/handoffs/Dada.handoff；规范：根 AGENTS.md。
using System;
using UnityEngine;

namespace Regrowth.Tests.Art
{
    public enum VisualAction { Idle, Move, Jump, Bite, Attack, Fire }
    public enum VisualTail { None, Normal, Flame }
    public enum VisualStatus { OK, INVALID, UNAVAILABLE, MISSING }

    /// <summary>仅测试输入快照；不代表真实构筑或可写 gameplay 端口。</summary>
    public readonly struct VisualCombination
    {
        public readonly bool Head, Body, Arms, Legs, NormalTail, FlameTail;
        public VisualCombination(bool head, bool body, bool arms, bool legs, bool normalTail, bool flameTail)
        {
            Head = head;
            Body = body;
            Arms = arms;
            Legs = legs;
            NormalTail = normalTail;
            FlameTail = flameTail;
        }
        public VisualTail Tail => FlameTail ? VisualTail.Flame : NormalTail ? VisualTail.Normal : VisualTail.None;
        public string Key => $"Body={(Body ? 1 : 0)} Arms={(Arms ? 1 : 0)} Legs={(Legs ? 1 : 0)} Tail={(NormalTail && FlameTail ? "BOTH" : Tail.ToString())}";
        public string Label => (Head ? "Head" : "No Head") + (Body ? " + Body" : "") + (Arms ? " + Arms" : "") + (Legs ? " + Legs" : "") + (NormalTail ? " + Tail" : "") + (FlameTail ? " + FlameTail" : "");
        public bool IsValid => Head && !(NormalTail && FlameTail) && (Body || !(Arms || Legs || NormalTail || FlameTail));
        public bool Allows(VisualAction action)
        {
            switch (action)
            {
                case VisualAction.Bite: return Body && !Arms;
                case VisualAction.Attack: return Body && Arms;
                case VisualAction.Fire: return Body && FlameTail;
                default: return true;
            }
        }
    }

    [Serializable]
    public sealed class VisualAnimation
    {
        [SerializeField, Tooltip("原始 PNG 文件名（不含扩展名）；Idle 记录来源 Move/Run。")]
        private string sourceName;
        [SerializeField, Tooltip("实际 Animation Clip；Sample Rate 为动作默认 FPS，下次请求时读取。")]
        private AnimationClip clip;
        [SerializeField, Tooltip("从左到右的完整 500×500 切片；Idle 只有第一帧。")]
        private Sprite[] frames;
        [SerializeField, Tooltip("动作类别，决定循环/单次播放；不触发伤害。")]
        private VisualAction action;
        public string SourceName => sourceName;
        public AnimationClip Clip => clip;
        public Sprite[] Frames => frames;
        public VisualAction Action => action;
        public int FrameCount => frames == null ? 0 : frames.Length;
        public bool Loop => action == VisualAction.Move;
        public bool IsReady => clip != null && clip.frameRate > 0f && FrameCount > 0 && Array.TrueForAll(frames, frame => frame != null);
        public VisualAnimation(string source, AnimationClip animation, Sprite[] orderedFrames, VisualAction requestedAction)
        {
            sourceName = source;
            clip = animation;
            frames = orderedFrames;
            action = requestedAction;
        }
    }

    [Serializable]
    public sealed class VisualFamily
    {
        [SerializeField] private bool body;
        [SerializeField] private bool arms;
        [SerializeField] private bool legs;
        [SerializeField] private VisualTail tail;
        [SerializeField, Tooltip("用户确认的身体组合前缀；缺图组合不建立替代条目。")]
        private string prefix;
        [SerializeField, Tooltip("每个动作最多一个引用。缺少合法动作时明确显示 MISSING。")]
        private VisualAnimation[] animations;
        public string Prefix => prefix;
        public VisualAnimation[] Animations => animations;
        public bool Matches(VisualCombination value) => body == value.Body && arms == value.Arms && legs == value.Legs && tail == value.Tail;
        public VisualAnimation Find(VisualAction action) => Array.Find(animations ?? Array.Empty<VisualAnimation>(), entry => entry != null && entry.Action == action);
        public VisualFamily(bool hasBody, bool hasArms, bool hasLegs, VisualTail tailKind, string visualPrefix, VisualAnimation[] clips)
        {
            body = hasBody;
            arms = hasArms;
            legs = hasLegs;
            tail = tailKind;
            prefix = visualPrefix;
            animations = clips;
        }
    }

    [CreateAssetMenu(menuName = "pawgatory/Tests/Player Visual Library")]
    public sealed class PlayerVisualLibrary : ScriptableObject
    {
        [SerializeField, Tooltip("十三个已确认身体组合；运行时只读，不保存测试会话状态。")]
        private VisualFamily[] families;
        [SerializeField, Tooltip("已导入但尚未确认身体映射的原始变体；不用于缺图兜底。")]
        private VisualAnimation[] unassignedVariants;
        public VisualFamily[] Families => families;
        public VisualAnimation[] UnassignedVariants => unassignedVariants;

        /// <summary>纯解析：非法优先，其次动作权限，再查组合和实际引用；无替代素材。</summary>
        public VisualStatus Resolve(VisualCombination state, VisualAction action, out VisualFamily family, out VisualAnimation animation, out string reason)
        {
            family = null;
            animation = null;
            if (!state.IsValid)
            {
                reason = !state.Head ? "Head must remain ON." : state.NormalTail && state.FlameTail ? "Two tails are invalid." : "Arms, Legs and Tail require Body.";
                return VisualStatus.INVALID;
            }
            family = Array.Find(families ?? Array.Empty<VisualFamily>(), entry => entry != null && entry.Matches(state));
            if (!state.Allows(action))
            {
                reason = $"{action} is unavailable for this combination.";
                return VisualStatus.UNAVAILABLE;
            }
            if (family == null)
            {
                reason = "MISSING VISUAL\n" + state.Key;
                return VisualStatus.MISSING;
            }
            animation = family.Find(action);
            if (animation == null || !animation.IsReady)
            {
                reason = $"MISSING ACTION / {family.Prefix} / {action}";
                return VisualStatus.MISSING;
            }
            reason = action == VisualAction.Idle ? "Temporary Idle: Move/Run frame 01 + breathing." : "";
            return VisualStatus.OK;
        }

        /// <summary>仅编辑器建库时调用；测试播放不修改共享配置资产。</summary>
        public void Configure(VisualFamily[] confirmedFamilies, VisualAnimation[] variants)
        {
            families = confirmedFamilies;
            unassignedVariants = variants;
        }
    }
}
