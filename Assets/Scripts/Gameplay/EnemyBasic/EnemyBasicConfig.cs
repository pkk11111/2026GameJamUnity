// 节奏/武器反馈维护：enemy-ai；参数首次启用快照，交接docs/handoffs/enemy-ai.handoff。
// Soap/T09：EnemyBasic初始配置。正式初始值50HP/5伤（2026-10-02总控调参）；接触保护已由PlayerState统一持有，旧周期字段只保留序列化兼容。
// AI参数来自combat_parameters；每个新敌人首次启用保存AI快照，不写回SO。
// Inspector接线与验证见docs/handoffs/Soap.handoff；规范入口AGENTS.md。
using UnityEngine;

namespace Regrowth.Gameplay
{
    [CreateAssetMenu(menuName = "pawgatory/Enemy Basic Config")]
    public sealed class EnemyBasicConfig : ScriptableObject
    {
        [Header("正式初始生命与伤害")]
        [SerializeField, Min(1), Tooltip("首次启用初始化最大/当前HP；重新启用不重置生命。")]
        private int maximumHealth = 50;
        [SerializeField, Min(1), Tooltip("首次初始化的接触伤害；之后读取EnemyBasic运行攻击值。")]
        private int contactDamage = 5;
        [SerializeField, HideInInspector] // 旧资产/历史测试兼容；正式接触不读取，不参与配置合法性。
        private float contactInterval = 0.6f;
        [SerializeField, Tooltip("扫描玩家Collider的层；每次扫描读取。")]
        private LayerMask playerLayers = 1;
        [SerializeField, HideInInspector] // 旧资产兼容；正式伤害只接受玩家根实体Collider。
        private bool includePlayerTriggers = true;
        [SerializeField, Tooltip("接触伤害的实心遮挡层；默认全部，排除角色与Trigger，实时读取。")]
        private LayerMask contactObstructionLayers = ~0;
        public LayerMask ContactObstructionLayers => contactObstructionLayers;
        [Header("Enemy AI - base speed 7 gives chase target 5")]
        [SerializeField, Min(0.01f)] private float chaseSpeedFactor = 5f / 7f;
        [SerializeField, Min(0.01f)] private float aggroRadius = 5f;
        [SerializeField, Min(0.01f)] private float activityLeftOffset = 6f;
        [SerializeField, Min(0.01f)] private float activityRightOffset = 6f;
        [SerializeField, Min(0.01f)] private float patrolSpeed = 1.5f;
        [SerializeField, Min(0.01f)] private float returnSpeed = 1.5f;
        [SerializeField, Tooltip("Solid terrain/closed doors; triggers and actor colliders are excluded by AI.")]
        private LayerMask obstacleLayers = 1;
        [Header("巡逻、警觉与剑击反馈（首次启用生效）")]
        [SerializeField, Min(0f), Tooltip("巡逻折返/回到出生点时的观察停顿，秒；0关闭，暂定0.45。")]
        private float patrolPauseSeconds = 0.45f;
        [SerializeField, Min(0f), Tooltip("首次发现玩家到追击前的停顿，秒；追击中不重复触发，暂定0.3。")]
        private float alertSeconds = 0.3f;
        [SerializeField, Min(0f), Tooltip("剑命中后水平后退速度，单位/秒；受实体及领地边界限制，暂定3。")]
        private float weaponKnockbackSpeed = 3f;
        [SerializeField, Min(0f), Tooltip("剑命中后的后退时长，秒；普通怪暂定0.12。")]
        private float weaponKnockbackSeconds = 0.12f;
        [SerializeField, Min(0f), Tooltip("剑命中后停止AI移动/接触伤害的总时长，秒；必须不少于后退时长，暂定0.18。")]
        private float weaponRecoverySeconds = 0.18f;
        public float PatrolPauseSeconds => patrolPauseSeconds;
        public float AlertSeconds => alertSeconds;
        public float WeaponKnockbackSpeed => weaponKnockbackSpeed;
        public float WeaponKnockbackSeconds => weaponKnockbackSeconds;
        public float WeaponRecoverySeconds => weaponRecoverySeconds;
        public float ChaseSpeedFactor => chaseSpeedFactor;
        public float AggroRadius => aggroRadius;
        public float ActivityLeftOffset => activityLeftOffset;
        public float ActivityRightOffset => activityRightOffset;
        public float PatrolSpeed => patrolSpeed;
        public float ReturnSpeed => returnSpeed;
        public LayerMask ObstacleLayers => obstacleLayers;
        public bool IsAIValid => Positive(chaseSpeedFactor) && Positive(aggroRadius) && Positive(activityLeftOffset)
            && Positive(activityRightOffset) && Positive(patrolSpeed) && Positive(returnSpeed) && obstacleLayers.value != 0
            && NonNegative(patrolPauseSeconds) && NonNegative(alertSeconds)
            && NonNegative(weaponKnockbackSpeed) && NonNegative(weaponKnockbackSeconds)
            && NonNegative(weaponRecoverySeconds) && weaponRecoverySeconds >= weaponKnockbackSeconds;
        private static bool NonNegative(float value) => value >= 0f && !float.IsInfinity(value);
        private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
        public int MaximumHealth => maximumHealth;
        public int ContactDamage => contactDamage;
        public float ContactInterval => contactInterval;
        public LayerMask PlayerLayers => playerLayers;
        public bool IncludePlayerTriggers => includePlayerTriggers;
        public bool IsValid => maximumHealth > 0 && contactDamage > 0
            && playerLayers.value != 0;
    }
}
