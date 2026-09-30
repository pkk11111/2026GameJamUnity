// 职责：唯一玩家生命、构筑、姿态和攻击数值持有者；只发布已确认的最小状态事务。
// 模块/维护：controller，C02；直接依赖：Core、GameAudio；GameBootstrap 显式初始化/解绑；无独立奖励/献祭系统。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Audio;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Runtime
{
    /// <summary>
    /// 首次初始化满血/空构筑/四足。临时启停保留本生命周期状态，死亡不复活；
    /// 新局方式待 C04，不提供 SetHealth、Reset 或直接改姿态入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerState : MonoBehaviour, IHealth, IDamageable, ILoadoutState, IFormState,
        IPlayerStateCommands, IPlayerCombatState
    {
        [Header("首次初始化参数（暂定测试值）")]
        [SerializeField, Min(1), Tooltip("满血开局的最大生命；暂定100，首次初始化读取。不是最大生命代价下限。")]
        private int initialMaximumHealth = 100;
        [SerializeField, Min(0), Tooltip("基础咬击伤害；暂定10，首次初始化读取。0允许配置，命中端须跳过0伤害请求。")]
        private int initialBiteDamage = 10;
        [SerializeField, Min(0), Tooltip("剑击伤害；暂定15，首次初始化读取。未实现攻击增减/负面叠加。")]
        private int initialSwordDamage = 15;

        private readonly LoadoutCollection loadout = new LoadoutCollection();
        private IRunContext run;
        private int currentHealth;
        private int maximumHealth;
        private int biteDamage;
        private int swordDamage;
        private bool notifying;

        public bool IsInitialized { get; private set; }
        public bool IsBound => run != null;
        public int CurrentHealth => currentHealth;
        public int MaximumHealth => maximumHealth;
        public bool IsAlive => IsInitialized && currentHealth > 0;
        public int Capacity => LoadoutCollection.SlotCapacity;
        public IReadOnlyList<LoadoutItemId> Items => loadout.Items;
        public PlayerForm CurrentForm => Contains(LoadoutItemId.UprightForm) ? PlayerForm.Upright : PlayerForm.Quadruped;
        public int BiteDamage => biteDamage;
        public int SwordDamage => swordDamage;
        public bool CanBite => CanAct && CurrentForm == PlayerForm.Quadruped;
        public bool CanUseSword => CanAct && CurrentForm == PlayerForm.Upright && Contains(LoadoutItemId.Sword);
        private bool CanAct => IsAlive && IsBound && isActiveAndEnabled && run.IsGameplayActive;
        private bool CanWrite => IsAlive && IsBound && isActiveAndEnabled && !notifying
            && (run.IsGameplayActive || run.Phase == RunPhase.Choosing);

        /// <summary>变更完成后通知；死亡先 HealthChanged 后 Died。OnEnable订阅/OnDisable退订并读快照。</summary>
        public event Action HealthChanged;
        public event Action Died;
        /// <summary>槽/姿态已全部提交后通知一次；回调中写命令被拒绝，避免部分通知重入。</summary>
        public event Action LoadoutChanged;
        public event Action<PlayerForm> FormChanged;

        // Bootstrap 早于本组件 OnEnable；启动资格使用 enabled/activeInHierarchy。
        internal bool Initialize(IRunContext context)
        {
            if (context == null || IsBound || !enabled || !gameObject.activeInHierarchy
                || (!IsInitialized && (initialMaximumHealth <= 0 || initialBiteDamage < 0 || initialSwordDamage < 0)))
            {
                Debug.LogError("C02 PlayerState 初始化失败：检查入口唯一绑定、组件启用、正数初始HP和非负攻击配置。", this);
                return false;
            }

            run = context;
            if (!IsInitialized)
            {
                maximumHealth = initialMaximumHealth;
                currentHealth = maximumHealth;
                biteDamage = initialBiteDamage;
                swordDamage = initialSwordDamage;
                IsInitialized = true;
                NotifyHealth(false);
            }
            return true;
        }

        /// <summary>实际敌人/地形扣血才 true；只有 Playing，拒绝 default、死亡、暂停与重入。不实现护盾/自动无敌帧。</summary>
        public bool TryTakeDamage(DamageRequest request)
        {
            if (!CanAct || notifying || request.Amount <= 0
                || (request.Kind != DamageKind.Enemy && request.Kind != DamageKind.Terrain))
            {
                return false;
            }

            currentHealth -= Math.Min(currentHealth, request.Amount);
            NotifyHealth(true);
            return true;
        }

        public bool TryHeal(int amount)
        {
            if (!CanWrite || amount <= 0 || currentHealth == maximumHealth)
            {
                return false;
            }
            currentHealth += Math.Min(amount, maximumHealth - currentHealth);
            NotifyHealth(false);
            return true;
        }

        public bool Contains(LoadoutItemId item) => loadout.Contains(item);

        public bool TryAddLoadoutItem(LoadoutItemId item)
        {
            if (!CanWrite || !IsFirstEditionItem(item))
            {
                return false;
            }
            PlayerForm previousForm = CurrentForm;
            if (!loadout.TryAdd(item))
            {
                return false;
            }
            NotifyLoadout(previousForm);
            PlayAbilityCue(item, AudioCue.AbilityGained);
            return true;
        }

        public bool TryRemoveLoadoutItem(LoadoutItemId item)
        {
            if (!CanWrite || !IsFirstEditionItem(item))
            {
                return false;
            }
            PlayerForm previousForm = CurrentForm;
            if (!loadout.TryRemove(item))
            {
                return false;
            }
            NotifyLoadout(previousForm);
            PlayAbilityCue(item, AudioCue.AbilityLost);
            return true;
        }

        public bool TryReplaceLoadoutItem(LoadoutItemId removedItem, LoadoutItemId addedItem)
        {
            if (!CanWrite || !IsFirstEditionItem(removedItem) || !IsFirstEditionItem(addedItem))
            {
                return false;
            }
            PlayerForm previousForm = CurrentForm;
            if (!loadout.TryReplace(removedItem, addedItem))
            {
                return false;
            }
            NotifyLoadout(previousForm);
            PlayAbilityCue(removedItem, AudioCue.AbilityLost);
            PlayAbilityCue(addedItem, AudioCue.AbilityGained);
            return true;
        }

        internal void Shutdown()
        {
            // 解绑不是新局：HP/构筑/姿态/死亡标记保留，不能靠入口启停复活或刷新构筑。
            run = null;
        }

        private static bool IsFirstEditionItem(LoadoutItemId item)
        {
            return item == LoadoutItemId.Dash || item == LoadoutItemId.DoubleJump
                || item == LoadoutItemId.UprightForm || item == LoadoutItemId.Sword;
        }

        private void NotifyHealth(bool tookDamage)
        {
            notifying = true;
            try
            {
                HealthChanged?.Invoke();
                if (tookDamage)
                {
                    GameAudio.Play(AudioCue.PlayerHurt, gameObject);
                }
                if (!IsAlive)
                {
                    Died?.Invoke();
                }
            }
            finally
            {
                notifying = false;
            }
        }

        private void NotifyLoadout(PlayerForm previousForm)
        {
            notifying = true;
            try
            {
                LoadoutChanged?.Invoke();
                if (CurrentForm != previousForm)
                {
                    FormChanged?.Invoke(CurrentForm);
                }
            }
            finally
            {
                notifying = false;
            }
        }

        private void PlayAbilityCue(LoadoutItemId item, AudioCue cue)
        {
            // 音频契约只有技能 gained/lost；剑装备暂无独立获取事件，不冒用武器攻击。
            if (item == LoadoutItemId.Dash || item == LoadoutItemId.DoubleJump || item == LoadoutItemId.UprightForm)
            {
                GameAudio.Play(cue, gameObject);
            }
        }

        private void OnDisable()
        {
            Shutdown();
        }
    }
}
