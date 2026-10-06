// 遮挡修复：structure-audit；交接docs/handoffs/structure-audit.handoff；保留已有范围/冷却与GUID。
// Soap/T07：攻击范围与间隔配置；伤害与权限只读取IPlayerCombatState。
// Inspector测试默认值待调参；接线/验收见docs/handoffs/Soap.handoff。
using UnityEngine;

namespace Regrowth.Gameplay
{
    [CreateAssetMenu(menuName = "GROWL AGAIN/Bite Config")]
    public sealed class BiteConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float biteRadius = 1.2f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.6f;
        [SerializeField] private LayerMask targetLayers = 1;
        [SerializeField] private bool includeTriggers = true;
        [SerializeField, Tooltip("近战遮挡层，默认全部；自动排除Trigger和伤害接收角色，实时读取。")]
        private LayerMask obstructionLayers = ~0;
        public LayerMask ObstructionLayers => obstructionLayers;
        public float Radius => biteRadius;
        public float CooldownSeconds => cooldownSeconds;
        public LayerMask TargetLayers => targetLayers;
        public bool IncludeTriggers => includeTriggers;
        public bool IsValid => biteRadius > 0f && !float.IsInfinity(biteRadius)
            && cooldownSeconds >= 0f && !float.IsInfinity(cooldownSeconds) && targetLayers.value != 0;
    }
}
