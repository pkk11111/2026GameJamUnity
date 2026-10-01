// Soap/T09：站桩敌人的初始配置；运行值只保存在EnemyBasic，数值为未冻结测试默认。
// Inspector接线与验证见docs/handoffs/Soap.handoff；规范入口AGENTS.md。
using UnityEngine;

namespace Regrowth.Gameplay
{
    [CreateAssetMenu(menuName = "pawgatory/Enemy Basic Config")]
    public sealed class EnemyBasicConfig : ScriptableObject
    {
        [Header("未冻结测试默认值")]
        [SerializeField, Min(1), Tooltip("首次启用初始化最大/当前HP；重新启用不重置生命。")]
        private int maximumHealth = 39;
        [SerializeField, Min(1), Tooltip("首次初始化的接触伤害；之后读取EnemyBasic运行攻击值。")]
        private int contactDamage = 7;
        [SerializeField, Min(0.01f), Tooltip("接触周期，游戏秒；每次扫描读取，恢复阶段等待一个新周期。")]
        private float contactInterval = 0.6f;
        [SerializeField, Tooltip("扫描玩家Collider的层；每次扫描读取。")]
        private LayerMask playerLayers = 1;
        [SerializeField, Tooltip("扫描是否包含玩家Trigger；实体Collider仍独立参与。")]
        private bool includePlayerTriggers = true;
        public int MaximumHealth => maximumHealth;
        public int ContactDamage => contactDamage;
        public float ContactInterval => contactInterval;
        public LayerMask PlayerLayers => playerLayers;
        public bool IncludePlayerTriggers => includePlayerTriggers;
        public bool IsValid => maximumHealth > 0 && contactDamage > 0 && contactInterval > 0f
            && !float.IsInfinity(contactInterval) && playerLayers.value != 0;
    }
}
