// 职责：主玩家咬/剑/火/冲刺互斥及起手方向锁，不存能力或伤害。
// 维护：controller；依赖：Core/Unity；显式主图绑定，旧独测可不接；规范：AGENTS.md。
using Regrowth.Core;
using UnityEngine;
namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerActionGate : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour runSource;
        [SerializeField] private MonoBehaviour healthSource;
        private MonoBehaviour owner;
        private double until;
        public bool IsBusy => isActiveAndEnabled && owner != null && owner.isActiveAndEnabled
        && Time.timeAsDouble < until && (healthSource as IHealth)?.IsAlive == true;
        public bool TryBegin(MonoBehaviour requester, float seconds)
        {
            if (requester == null || !requester.isActiveAndEnabled || IsBusy || !isActiveAndEnabled
            || (runSource as IRunContext)?.IsGameplayActive != true
            || (healthSource as IHealth)?.IsAlive != true || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                return false;
            }
            owner = requester;
            until = Time.timeAsDouble + seconds;
            return true;
        }
        public void Release(MonoBehaviour requester)
        {
            if (owner == requester)
            {
                owner = null;
            }
        }
        private void OnDisable()
        {
            owner = null;
        }
    }
}
