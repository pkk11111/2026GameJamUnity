// 职责：正式玩家只读动作映射；复用 Dada 的切片和 Clip，运行时不依赖 Art Test。
// 维护：controller；依赖 Unity；规范 AGENTS.md；来源见 Dada.handoff。
using System;
using UnityEngine;
namespace Regrowth.Presentation
{
    public enum PlayerVisualAction
    {
        Idle, Move, Jump, Bite, Attack, Fire
    }
    [Serializable]
    public sealed class PlayerAnimationEntry
    {
        public int bodyMask;
        public string family;
        public PlayerVisualAction action;
        public AnimationClip clip;
        public Sprite[] frames;
        public float footOffset;
        public float centerOffset;
    }
    [CreateAssetMenu(menuName = "pawgatory/Player Animation Set")]
    public sealed class PlayerAnimationSet : ScriptableObject
    {
        [SerializeField] private PlayerAnimationEntry[] entries;
        public PlayerAnimationEntry Find(int mask, PlayerVisualAction action) =>
        Array.Find(entries ?? Array.Empty<PlayerAnimationEntry>(), e => e.bodyMask == mask && e.action == action);
        // Editor conversion only; references retain source GUIDs. No copied PNG or Clip.
        public void Configure(PlayerAnimationEntry[] value)
        {
            entries = value;
        }
    }
}
