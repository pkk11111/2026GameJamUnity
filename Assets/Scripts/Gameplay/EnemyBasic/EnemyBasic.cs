// Soap/T09：一个敌人唯一HP/攻击值持有者；不写SO、不复活、不写玩家/运行阶段。
// Inspector绑定Config、唯一RunContext，可选未来注册适配器；详见Soap.handoff。
using System;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class EnemyBasic : MonoBehaviour, IHealth, IDamageable
    {
        [SerializeField, Tooltip("必填初始HP/攻击与实时接触策略配置；不能存运行时HP。")]
        private EnemyBasicConfig config;
        [SerializeField, Tooltip("唯一运行阶段读口；正式集成由总控显式绑定。")]
        private MonoBehaviour runContextSource;
        [SerializeField, Tooltip("可选IEnemyRegistrationAdapter；尚无正式T17服务，允许为空。")]
        private MonoBehaviour registrationSource;
        [SerializeField, Tooltip("本敌人明确绑定的实体/攻击Collider；死亡立即关闭，死后启停不恢复。不影响视觉。")]
        private Collider2D[] collidersToDisableOnDeath = new Collider2D[0];
        [SerializeField, Tooltip("明确绑定的本敌人SpriteRenderer；死亡立即隐藏，死体启停也不显示，不销毁对象。")]
        private SpriteRenderer[] renderersToHideOnDeath = new SpriteRenderer[0];
        private IRunContext run;
        private IEnemyRegistrationAdapter registration;
        private int currentHealth, maximumHealth, attackDamage;
        private bool notifying, registered, deathPublished;
        public int CurrentHealth => currentHealth;
        public int MaximumHealth => maximumHealth;
        public int AttackDamage => attackDamage;
        public bool IsInitialized { get; private set; }
        public bool IsAlive => IsInitialized && currentHealth > 0;
        public IRunContext RunContext => run;
        public EnemyBasicConfig Config => config;
        public bool CanAct => isActiveAndEnabled && IsAlive && runContextSource != null
            && run != null && run.IsGameplayActive && run.Phase == RunPhase.Playing;
        // Unity主线程提交后同步通知；订阅者禁重入写命令，退出生命周期须退订。
        public event Action HealthChanged;
        public event Action Died;

        private void OnEnable()
        {
            // 独立于Run/Config接线校验：已死亡对象重启也不能重新挡路。
            if (deathPublished)
            {
                DisableDeathColliders();
                HideDeathPresentation();
            }
            run = runContextSource as IRunContext;
            registration = registrationSource as IEnemyRegistrationAdapter;
            if (config == null || !config.IsValid || run == null
                || (registrationSource != null && registration == null))
            {
                Debug.LogWarning("[T09 Enemy] Bind valid Config/RunContext/optional registration adapter.", this);
                return;
            }
            if (!IsInitialized)
            {
                maximumHealth = config.MaximumHealth;
                currentHealth = maximumHealth;
                attackDamage = config.ContactDamage;
                IsInitialized = true;
            }
            if (IsAlive && registration != null)
            {
                registered = true;
                registration.Register(this);
            }
        }

        /// <summary>主线程仅存活Playing接受正数合法伤害；false无变化，成功通知HP，首次0再通知死亡。</summary>
        public bool TryTakeDamage(DamageRequest request)
        {
            if (!CanAct || notifying || request.Amount <= 0
                || (request.Kind != DamageKind.Enemy && request.Kind != DamageKind.Terrain))
            {
                return false;
            }
            currentHealth -= Math.Min(currentHealth, request.Amount);
            bool died = currentHealth == 0 && !deathPublished;
            if (died)
            {
                deathPublished = true; // 提交在事件之前，重入/启停不能第二次死亡。
                DisableDeathColliders(); // 所有死亡订阅者观察到的尸体已无阻挡/攻击碰撞。
                HideDeathPresentation(); // 使用同一死亡状态；立即隐藏，不做动画/淡出或删除敌人。
            }
            notifying = true;
            try
            {
                if (died)
                {
                    Unregister();
                }
                HealthChanged?.Invoke();
            }
            finally
            {
                try
                {
                    if (died)
                    {
                        Died?.Invoke();
                    }
                }
                finally
                {
                    notifying = false;
                }
            }
            return true;
        }

        /// <summary>
        /// 未来服务传入已计算的完整快照；比例/叠加/取整均由该服务负责，本模块不猜规则。
        /// 拒绝零HP、死敌及事件重入；无变化不发HealthChanged。可在Register回调中应用。
        /// </summary>
        public bool TrySetRuntimeStats(int newMaximumHealth, int newCurrentHealth, int newAttackDamage)
        {
            if (!isActiveAndEnabled || !IsAlive || notifying || newMaximumHealth <= 0
                || newCurrentHealth <= 0 || newCurrentHealth > newMaximumHealth || newAttackDamage <= 0)
            {
                return false;
            }
            bool changed = maximumHealth != newMaximumHealth || currentHealth != newCurrentHealth;
            maximumHealth = newMaximumHealth; currentHealth = newCurrentHealth; attackDamage = newAttackDamage;
            if (changed)
            {
                notifying = true;
                try
                {
                    HealthChanged?.Invoke();
                }
                finally
                {
                    notifying = false;
                }
            }
            return true;
        }
        private void Unregister()
        {
            if (!registered)
            {
                return;
            }
            registered = false;
            registration?.Unregister(this);
        }
        private void DisableDeathColliders()
        {
            if (collidersToDisableOnDeath == null)
            {
                return;
            }
            foreach (Collider2D collider in collidersToDisableOnDeath)
            {
                // 只允许显式绑定的自身层级；错绑玩家/地面等引用不能关闭无关碰撞。
                if (collider != null && collider.transform.IsChildOf(transform))
                {
                    collider.enabled = false;
                }
            }
        }
        private void OnDisable()
        {
            if (deathPublished)
            {
                DisableDeathColliders();
                HideDeathPresentation();
            }
            Unregister(); run = null; registration = null;
        }
        private void HideDeathPresentation()
        {
            if (renderersToHideOnDeath == null)
            {
                return;
            }
            foreach (SpriteRenderer renderer in renderersToHideOnDeath)
            {
                // 仅显式绑定的自身视觉，错绑其他物体不会隐藏它们。
                if (renderer != null && renderer.transform.IsChildOf(transform))
                {
                    renderer.enabled = false;
                }
            }
        }
    }
}
