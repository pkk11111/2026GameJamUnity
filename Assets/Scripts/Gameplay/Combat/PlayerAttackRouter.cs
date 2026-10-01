// Soap/T07 V5：唯一普通Attack消费者。剑优先，未绑定剑动作时不回退咬击。
// 同一输入/状态显式绑定；不存HP/构筑，不写运动或时间。接线见Soap.handoff。
// 直接依赖：Core/UnityEngine；规则：根AGENTS.md，维护：Soap。
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-110)]
    public sealed class PlayerAttackRouter : MonoBehaviour
    {
        [Header("唯一输入与状态（重新启用时绑定）")]
        [SerializeField, Tooltip("唯一IPlayerInput组件；其他动作不得消费普通Attack。")]
        private MonoBehaviour inputSource;
        [SerializeField, Tooltip("唯一IPlayerCombatState，必须与动作绑定同一组件。")]
        private MonoBehaviour combatStateSource;
        [Header("动作接点（重新启用时绑定）")]
        [SerializeField, Tooltip("实现IPlayerAttackAction的咬击组件。")]
        private MonoBehaviour biteActionSource;
        [SerializeField, Tooltip("T08可选接点；未绑定时有手Attack只消费，不咬击、不发剑击Cue。")]
        private MonoBehaviour swordActionSource;
        private IPlayerInput input;
        private IPlayerCombatState combat;
        public bool IsWired => inputSource != null && combatStateSource != null && input != null && combat != null;

        private void OnEnable()
        {
            input = inputSource as IPlayerInput;
            combat = combatStateSource as IPlayerCombatState;
            if (!IsWired)
            {
                Debug.LogWarning("[Attack Router] Missing input/combat read port; inspect this component.", this);
            }
            if (!(biteActionSource is IPlayerAttackAction))
            {
                Debug.LogWarning("[Attack Router] Missing bite action; inspect this component.", this);
            }
            if (swordActionSource != null && !(swordActionSource is IPlayerAttackAction))
            {
                Debug.LogWarning("[Attack Router] Sword source must implement IPlayerAttackAction.", this);
            }
        }

        private void FixedUpdate()
        {
            // 先消费一次；冷却、无权限、动作缺失等拒绝都不保留请求。
            // Paused/Choosing/Dead输入清理由唯一C01适配器负责。
            if (!IsWired || !input.TryConsumeAttack())
            {
                return;
            }
            if (combat.CanUseSword)
            {
                Dispatch(swordActionSource);
            }
            else if (combat.CanBite)
            {
                Dispatch(biteActionSource);
            }
        }

        private void Dispatch(MonoBehaviour source)
        {
            if (source == null || !source.isActiveAndEnabled || !(source is IPlayerAttackAction action))
            {
                return;
            }
            if (!ReferenceEquals(action.CombatState, combat))
            {
                Debug.LogWarning("[Attack Router] Action must read the same combat state as the router.", this);
                return;
            }
            action.TryAttack();
        }
    }
}
