// 职责：固定步接触伤害候选入口；不更改IDamageable“true代表实际扣血”的契约。
// 维护：structure-audit；依赖Core伤害数据/Unity；交接docs/handoffs/structure-audit.handoff。
// 来源在FixedUpdate(顺序100)提交，唯一玩家在顺序200结算；规范：根AGENTS.md。
using System;
using UnityEngine;

namespace Regrowth.Core
{
    public interface IContactDamageReceiver
    {
        /// <summary>
        /// true仅表示本固定步候选已接收，不表示已扣血。必须在FixedUpdate、结算器之前调用。
        /// source为启用的来源组件；sourceId为本局稳定唯一ID。仅实际扣血后调用onApplied一次。
        /// 同一步取最大伤害、同伤害按Ordinal ID；暂停/失效/过期候选不补结算。
        /// </summary>
        bool TryQueueContactDamage(DamageRequest request, MonoBehaviour source, string sourceId,
            Action<DamageRequest> onApplied);
    }
}
