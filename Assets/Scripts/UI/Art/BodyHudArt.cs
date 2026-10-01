// 职责：从唯一PlayerState的只读身体/构筑接口显示身体HUD，不授予能力或写生命。
// 依赖：Regrowth.Core、UnityEngine；维护Dada，交接docs/handoffs/Dada.handoff；规范AGENTS.md。
using Regrowth.Core;
using UnityEngine;
namespace Regrowth.UI.Art
{
    public sealed class BodyHudArt : MonoBehaviour
    {
        [SerializeField, Tooltip("与PlayerHud相同的PlayerState；启用时绑定，停用退订。")]
        private MonoBehaviour stateSource;
        [SerializeField] private GameObject hpGroup, torso, armsBase, arms, legs, tail, flame, swordLabel, legsLabel, dashLabel, fireLabel;
        private ILoadoutState loadout;
        private IPlayerBodyState body;
        private void OnEnable()
        {
            loadout=stateSource as ILoadoutState; body=stateSource as IPlayerBodyState;
            if (loadout == null || body == null) { Debug.LogError("BodyHudArt: bind the same PlayerState as PlayerHud.",this); return; }
            loadout.LoadoutChanged+=Refresh; body.BodyChanged+=Refresh; Refresh();
        }
        private void OnDisable()
        {
            if (loadout != null) { loadout.LoadoutChanged-=Refresh; }
            if (body != null) { body.BodyChanged-=Refresh; }
        }
        public void Refresh()
        {
            if (body == null || loadout == null) { return; }
            hpGroup.SetActive(body.HasBodyCore); torso.SetActive(body.HasBodyCore); armsBase.SetActive(body.HasBodyCore);
            bool hasArms=loadout.Contains(LoadoutItemId.Arms), hasLegs=loadout.Contains(LoadoutItemId.Legs);
            bool hasTail=loadout.Contains(LoadoutItemId.Tail), hasFlameTail=loadout.Contains(LoadoutItemId.FlameTail);
            bool hasFire=hasFlameTail || loadout.Contains(LoadoutItemId.FlameBreath);
            arms.SetActive(hasArms); swordLabel.SetActive(hasArms);
            legs.SetActive(hasLegs); legsLabel.SetActive(hasLegs);
            tail.SetActive(hasTail || hasFlameTail); dashLabel.SetActive(hasTail);
            flame.SetActive(hasFire); fireLabel.SetActive(hasFire);
        }
    }
}
