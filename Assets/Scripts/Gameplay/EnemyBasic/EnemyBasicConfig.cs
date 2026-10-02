// Soap/T09：EnemyBasic初始配置。正式初始值50HP/5伤（2026-10-02总控调参）；0.6独立接触间隔仍待共享保护迁移。
// AI参数来自combat_parameters；每个新敌人首次启用保存AI快照，不写回SO。
// Inspector接线与验证见docs/handoffs/Soap.handoff；规范入口AGENTS.md。
using UnityEngine;

namespace Regrowth.Gameplay
{
    [CreateAssetMenu(menuName = "pawgatory/Enemy Basic Config")]
    public sealed class EnemyBasicConfig : ScriptableObject
    {
        [Header("正式初始生命与伤害 / legacy接触周期")]
        [SerializeField, Min(1), Tooltip("首次启用初始化最大/当前HP；重新启用不重置生命。")]
        private int maximumHealth = 50;
        [SerializeField, Min(1), Tooltip("首次初始化的接触伤害；之后读取EnemyBasic运行攻击值。")]
        private int contactDamage = 5;
        [SerializeField, Min(0.01f), Tooltip("接触周期，游戏秒；每次扫描读取，恢复阶段等待一个新周期。")]
        private float contactInterval = 0.6f;
        [SerializeField, Tooltip("扫描玩家Collider的层；每次扫描读取。")]
        private LayerMask playerLayers = 1;
        [SerializeField, Tooltip("扫描是否包含玩家Trigger；实体Collider仍独立参与。")]
        private bool includePlayerTriggers = true;
        [Header("Enemy AI - base speed 7 gives chase target 5")]
        [SerializeField, Min(0.01f)] private float chaseSpeedFactor = 5f / 7f;
        [SerializeField, Min(0.01f)] private float aggroRadius = 5f;
        [SerializeField, Min(0.01f)] private float activityLeftOffset = 6f;
        [SerializeField, Min(0.01f)] private float activityRightOffset = 6f;
        [SerializeField, Min(0.01f)] private float patrolSpeed = 1.5f;
        [SerializeField, Min(0.01f)] private float returnSpeed = 1.5f;
        [SerializeField, Tooltip("Solid terrain/closed doors; triggers and actor colliders are excluded by AI.")]
        private LayerMask obstacleLayers = 1;
        public float ChaseSpeedFactor => chaseSpeedFactor;
        public float AggroRadius => aggroRadius;
        public float ActivityLeftOffset => activityLeftOffset;
        public float ActivityRightOffset => activityRightOffset;
        public float PatrolSpeed => patrolSpeed;
        public float ReturnSpeed => returnSpeed;
        public LayerMask ObstacleLayers => obstacleLayers;
        public bool IsAIValid => Positive(chaseSpeedFactor) && Positive(aggroRadius) && Positive(activityLeftOffset)
            && Positive(activityRightOffset) && Positive(patrolSpeed) && Positive(returnSpeed) && obstacleLayers.value != 0;
        private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
        public int MaximumHealth => maximumHealth;
        public int ContactDamage => contactDamage;
        public float ContactInterval => contactInterval;
        public LayerMask PlayerLayers => playerLayers;
        public bool IncludePlayerTriggers => includePlayerTriggers;
        public bool IsValid => maximumHealth > 0 && contactDamage > 0 && contactInterval > 0f
            && !float.IsInfinity(contactInterval) && playerLayers.value != 0;
    }
}
