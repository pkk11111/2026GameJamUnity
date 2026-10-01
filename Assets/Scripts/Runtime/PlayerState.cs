// 职责：唯一生命/三槽/身体/历史/攻击状态；奖励先全量验证再提交，Bootstrap初始化，事件仅通知。
// 模块/维护：controller / T12-V5；交接：docs/handoffs/controller.handoff；规范：根 AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Audio;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerState : MonoBehaviour, IHealth, IDamageable, ILoadoutState, IFormState,
        IPlayerStateCommands, IPlayerCombatState, IPlayerBodyState, IPlayerRewardCommands
    {
        [Header("初始化（暂定测试值；首次初始化读取）")]
        [SerializeField, Min(1), Tooltip("躯干首次取得时的最大HP；默认100，后续部件再生不重复满血。")]
        private int initialMaximumHealth = 100;
        [SerializeField, Min(0), Tooltip("初始咬击伤害；实际动作模块读取，暂定10。")]
        private int initialBiteDamage = 10;
        [SerializeField, Min(0), Tooltip("初始剑击伤害；暂定15，双手权限不依赖腿。")]
        private int initialSwordDamage = 15;
        [SerializeField, Min(1), Tooltip("火焰每跳基础伤害，暂定8；与旧加攻击奖励一同原子增加。")]
        private int initialFireDamage = 8;
        [SerializeField, Tooltip("仅白板/旧独测跳过躯干教学；正式教学场景必须关闭。首次初始化生效。")]
        private bool prototypeStartWithBodyCore = true;

        private readonly LoadoutCollection loadout = new LoadoutCollection();
        private readonly List<LoadoutItemId> everOwned = new List<LoadoutItemId>();
        private IReadOnlyList<LoadoutItemId> historyView;
        private IRunContext run;
        private int currentHealth;
        private int maximumHealth;
        private int biteDamage;
        private int swordDamage;
        private int fireDamage;
        private int attackPercent = 100;
        private double protectionUntil;
        private bool notifying;
        public bool IsInitialized { get; private set; }
        public bool IsBound => run != null;
        public bool HasBodyCore { get; private set; }
        public int CurrentHealth => currentHealth;
        public int MaximumHealth => maximumHealth;
        // 头部是安全的活跃状态；不能把尚未初始化的HP当死亡。
        public bool IsAlive => IsInitialized && (!HasBodyCore || currentHealth > 0);
        public int Capacity => LoadoutRules.Capacity;
        public IReadOnlyList<LoadoutItemId> Items => loadout.Items;
        public IReadOnlyList<LoadoutItemId> EverOwnedItems => historyView ?? (historyView = everOwned.AsReadOnly());
        public bool WasEverOwned(LoadoutItemId item) => everOwned.Contains(item);
        public PlayerForm CurrentForm => Contains(LoadoutItemId.Arms) ? PlayerForm.Upright : PlayerForm.Quadruped;
        public int AttackPercent => attackPercent;
        public bool HasDamageProtection => Time.timeAsDouble < protectionUntil;
        public int BiteDamage => Scaled(biteDamage, attackPercent);
        public int SwordDamage => Scaled(swordDamage, attackPercent);
        public int FireDamage => Scaled(fireDamage, attackPercent);
        private static int Scaled(int value, int percent) => (int)(((long)value * percent + 99) / 100);
        public bool CanUseFire => CanAct && HasBodyCore && Contains(LoadoutItemId.FlameTail);
        public bool CanBite => CanAct && HasBodyCore && !Contains(LoadoutItemId.Arms);
        public bool CanUseSword => CanAct && HasBodyCore && Contains(LoadoutItemId.Arms);
        private bool CanAct => IsAlive && IsBound && isActiveAndEnabled && run.IsGameplayActive;
        private bool CanWrite => IsAlive && IsBound && isActiveAndEnabled && !notifying
            && (run.IsGameplayActive || run.Phase == RunPhase.Choosing);
        public event Action HealthChanged;
        public event Action Died;
        public event Action LoadoutChanged;
        public event Action<PlayerForm> FormChanged;
        public event Action BodyChanged;

        internal bool Initialize(IRunContext context)
        {
            if (context == null || IsBound || !enabled || !gameObject.activeInHierarchy
                || (!IsInitialized && (initialMaximumHealth <= 0 || initialBiteDamage < 0 || initialSwordDamage < 0 || initialFireDamage <= 0)))
            {
                Debug.LogError("PlayerState 初始化失败：检查唯一绑定、启用和HP/攻击配置。", this);
                return false;
            }
            run = context;
            if (!IsInitialized)
            {
                HasBodyCore = prototypeStartWithBodyCore;
                maximumHealth = HasBodyCore ? initialMaximumHealth : 0;
                currentHealth = maximumHealth;
                biteDamage = initialBiteDamage;
                swordDamage = initialSwordDamage;
                fireDamage = initialFireDamage;
                IsInitialized = true;
                Notify(false, true, false, CurrentForm, false);
            }
            return true;
        }

        public bool TryAcquireBodyCore(Action onCommitted = null)
        {
            if (!CanWrite || HasBodyCore || initialMaximumHealth <= 0)
            {
                return false;
            }
            HasBodyCore = true;
            maximumHealth = initialMaximumHealth;
            currentHealth = maximumHealth;
            notifying = true;
            try
            {
                CommitOwner(onCommitted);
                BodyChanged?.Invoke();
                HealthChanged?.Invoke();
            }
            finally
            {
                notifying = false;
            }
            return true;
        }

        /// <summary>实际Playing敌人/地形伤害才true；头部安全期拒绝伤害，费用不得走这里。</summary>
        public bool TryTakeDamage(DamageRequest request)
        {
            if (!CanAct || !HasBodyCore || notifying || HasDamageProtection || request.Amount <= 0
                || (request.Kind != DamageKind.Enemy && request.Kind != DamageKind.Terrain))
            {
                return false;
            }
            currentHealth -= Math.Min(currentHealth, request.Amount);
            Notify(false, true, false, CurrentForm, true);
            return true;
        }

        public bool TryHeal(int amount)
        {
            if (!HasBodyCore || currentHealth == maximumHealth)
            {
                return false;
            }
            return TryApplyReward(new PlayerReward(heal: amount));
        }
        public bool Contains(LoadoutItemId item) => loadout.Contains(item);
        public bool TryAddLoadoutItem(LoadoutItemId item) => TryApplyReward(new PlayerReward(item));
        public bool TryReplaceLoadoutItem(LoadoutItemId removedItem, LoadoutItemId addedItem) =>
            TryApplyReward(new PlayerReward(addedItem), removedItem);

        public bool TryRemoveLoadoutItem(LoadoutItemId item)
        {
            if (!CanWrite || !HasBodyCore || !LoadoutRules.IsV5Item(item))
            {
                return false;
            }
            PlayerForm previous = CurrentForm;
            if (!loadout.TryRemove(item))
            {
                return false;
            }
            Notify(true, false, false, previous, false);
            GameAudio.Play(AudioCue.AbilityLost, gameObject);
            return true;
        }

        public bool TryApplyReward(PlayerReward reward, LoadoutItemId? removedItem = null, Action onCommitted = null)
        {
            if (!CanWrite || !HasBodyCore || !reward.IsValid
                || (removedItem.HasValue && (!reward.Item.HasValue || !Contains(removedItem.Value))))
            {
                return false;
            }
            long nextMaximum = (long)maximumHealth + reward.MaximumHealthIncrease;
            long nextBite = (long)biteDamage + reward.AttackIncrease;
            long nextSword = (long)swordDamage + reward.AttackIncrease;
            long nextFire = (long)fireDamage + reward.AttackIncrease;
            long nextPercent = (long)attackPercent + reward.AttackPercentIncrease;
            if (nextMaximum > int.MaxValue || nextBite > int.MaxValue || nextSword > int.MaxValue || nextFire > int.MaxValue
                || nextPercent > int.MaxValue || (nextBite * nextPercent + 99) / 100 > int.MaxValue
                || (nextSword * nextPercent + 99) / 100 > int.MaxValue || (nextFire * nextPercent + 99) / 100 > int.MaxValue)
            {
                return false;
            }
            if (reward.Item.HasValue)
            {
                LoadoutItemId added = reward.Item.Value;
                if (Contains(added) || (!removedItem.HasValue && Items.Count >= Capacity))
                {
                    return false;
                }
                foreach (LoadoutItemId held in Items)
                {
                    if ((!removedItem.HasValue || held != removedItem.Value) && LoadoutRules.Conflicts(held, added))
                    {
                        return false;
                    }
                }
            }
            PlayerForm previous = CurrentForm;
            int nextHealth = (int)Math.Min(nextMaximum, (long)currentHealth + reward.Heal);
            bool healthChanged = nextMaximum != maximumHealth || nextHealth != currentHealth;
            if (reward.Item.HasValue)
            {
                bool changed = removedItem.HasValue
                    ? loadout.TryReplace(removedItem.Value, reward.Item.Value) : loadout.TryAdd(reward.Item.Value);
                if (!changed)
                {
                    return false;
                }
                if (!everOwned.Contains(reward.Item.Value))
                {
                    everOwned.Add(reward.Item.Value);
                }
            }
            maximumHealth = (int)nextMaximum;
            currentHealth = nextHealth;
            biteDamage = (int)nextBite;
            swordDamage = (int)nextSword;
            fireDamage = (int)nextFire;
            attackPercent = (int)nextPercent;
            notifying = true;
            try
            {
                // 箱领取标记与数值/槽在任何状态订阅者运行前全部可见。
                CommitOwner(onCommitted);
                if (healthChanged)
                {
                    HealthChanged?.Invoke();
                }
                if (reward.Item.HasValue)
                {
                    LoadoutChanged?.Invoke();
                }
                if (CurrentForm != previous)
                {
                    FormChanged?.Invoke(CurrentForm);
                }
            }
            finally
            {
                notifying = false;
            }
            if (removedItem.HasValue)
            {
                GameAudio.Play(AudioCue.AbilityLost, gameObject);
            }
            if (reward.Item.HasValue)
            {
                GameAudio.Play(AudioCue.AbilityGained, gameObject);
            }
            return true;
        }

        private void CommitOwner(Action callback)
        {
            if (callback == null)
            {
                return;
            }
            try
            {
                callback();
            }
            catch (Exception exception)
            {
                // 所有状态已提交；报告订阅方程序错误，不把成功事务伪报为false并重复领取。
                Debug.LogException(exception, this);
            }
        }

        public static LoadoutItemId? CostItem(TeleportCostKind kind)
        {
            switch (kind)
            {
                case TeleportCostKind.ShedLegs: return LoadoutItemId.Legs;
                case TeleportCostKind.ShedArms: return LoadoutItemId.Arms;
                case TeleportCostKind.ShedTail: return LoadoutItemId.Tail;
                case TeleportCostKind.ShedFlameTail: return LoadoutItemId.FlameTail;
                default: return null;
            }
        }

        /// <summary>模拟唯一状态的代价；敌人部分由唯一全图服务另验。false不修改，reason可用于灰卡。</summary>
        public bool CanPayTeleportCost(TeleportCostKind kind, int amount, int minimumAttackPercent, out string reason)
        {
            reason = string.Empty;
            if (!CanWrite || !HasBodyCore || amount <= 0 || minimumAttackPercent <= 0)
            {
                reason = "Player or cost is unavailable.";
                return false;
            }
            var item = CostItem(kind);
            if (item.HasValue)
            {
                if (Contains(item.Value))
                {
                    return true;
                }
                reason = "This body part is no longer held.";
                return false;
            }
            switch (kind)
            {
                case TeleportCostKind.CurrentHealth:
                    if (amount <= 100 && currentHealth - ((long)currentHealth * amount + 99) / 100 > 0)
                    {
                        return true;
                    }
                    reason = "Would cause death.";
                    return false;
                case TeleportCostKind.MaximumHealth:
                    if (amount <= 100 && maximumHealth - ((long)maximumHealth * amount + 99) / 100 > 0)
                    {
                        return true;
                    }
                    reason = "Would reduce maximum health to zero.";
                    return false;
                case TeleportCostKind.Attack:
                    if ((long)attackPercent - amount >= minimumAttackPercent)
                    {
                        return true;
                    }
                    reason = "Attack cannot fall below " + minimumAttackPercent + "%.";
                    return false;
                case TeleportCostKind.EnemyHealth:
                case TeleportCostKind.EnemyAttack:
                    return true;
                default:
                    reason = "Unknown cost.";
                    return false;
            }
        }

        /// <summary>
        /// 主线程物理提交：全量复验后只执行一次费用与世界迁移。世界回调false必须无副作用；
        /// true必须已完成无通知的迁移/敌人写入，随后统一通知。死亡/重入/无效代价均拒绝。
        /// </summary>
        public bool TryCommitTeleportCost(TeleportCostKind kind, int amount, int minimumAttackPercent,
            float protectionSeconds, Func<bool> tryCommitWorld, Action onCommitted)
        {
            if (!CanPayTeleportCost(kind, amount, minimumAttackPercent, out _)
                || tryCommitWorld == null || protectionSeconds < 0f || float.IsNaN(protectionSeconds)
                || float.IsInfinity(protectionSeconds))
            {
                return false;
            }
            var item = CostItem(kind);
            var previous = CurrentForm;
            int nextHealth = currentHealth;
            int nextMaximum = maximumHealth;
            if (kind == TeleportCostKind.CurrentHealth)
            {
                nextHealth -= (int)(((long)currentHealth * amount + 99) / 100);
            }
            if (kind == TeleportCostKind.MaximumHealth)
            {
                nextMaximum -= (int)(((long)maximumHealth * amount + 99) / 100);
                nextHealth = Math.Min(nextHealth, nextMaximum);
            }
            notifying = true;
            try
            {
                if (!tryCommitWorld())
                {
                    return false;
                }
                if (item.HasValue)
                {
                    loadout.TryRemove(item.Value);
                }
                bool changedHealth = currentHealth != nextHealth || maximumHealth != nextMaximum;
                currentHealth = nextHealth;
                maximumHealth = nextMaximum;
                if (kind == TeleportCostKind.Attack)
                {
                    attackPercent -= amount;
                }
                protectionUntil = Math.Max(protectionUntil, Time.timeAsDouble + protectionSeconds);
                CommitOwner(onCommitted);
                if (changedHealth)
                {
                    CommitOwner(() => HealthChanged?.Invoke());
                }
                if (item.HasValue)
                {
                    CommitOwner(() => LoadoutChanged?.Invoke());
                }
                if (CurrentForm != previous)
                {
                    CommitOwner(() => FormChanged?.Invoke(CurrentForm));
                }
            }
            finally
            {
                notifying = false;
            }
            if (item.HasValue)
            {
                GameAudio.Play(AudioCue.AbilityLost, gameObject);
            }
            return true;
        }
        internal void Shutdown()
        {
            run = null;
        }
        private void Notify(bool itemsChanged, bool healthChanged, bool bodyChanged, PlayerForm previous, bool hurt)
        {
            notifying = true;
            try
            {
                if (healthChanged)
                {
                    HealthChanged?.Invoke();
                }
                if (itemsChanged)
                {
                    LoadoutChanged?.Invoke();
                }
                if (bodyChanged)
                {
                    BodyChanged?.Invoke();
                }
                if (CurrentForm != previous)
                {
                    FormChanged?.Invoke(CurrentForm);
                }
                if (hurt)
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
        private void OnDisable()
        {
            Shutdown();
        }
    }
}
