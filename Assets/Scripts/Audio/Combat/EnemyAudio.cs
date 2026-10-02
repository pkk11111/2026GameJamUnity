// 职责：已提交敌人伤害/死亡/接触攻击音频；不写战斗状态。
// 依赖 Core/EnemyBasic/Audio.Core/Wwise组件；维护 audio-full-events；规范 AGENTS.md。
// 交接 docs/handoffs/audio-full-events.handoff；精英身份由场景明确配置。
using Regrowth.Core;
using Regrowth.Gameplay;
using UnityEngine;
namespace Regrowth.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBasic), typeof(AkGameObj))]
    public sealed class EnemyAudio : MonoBehaviour
    {
        [SerializeField] private bool elite;
        [SerializeField] private EnemyContactAttack contact;
        [SerializeField, Min(0f)] private float hurtInterval = 1f;
        private EnemyBasic owner;
        private double nextHurt;
        public bool IsElite => elite;
        private void OnEnable()
        {
            owner = GetComponent<EnemyBasic>();
            owner.DamageApplied += Hurt;
            owner.Died += Death;
            if (contact != null) contact.DamageApplied += Attack;
        }
        private void OnDisable()
        {
            if (owner != null) { owner.DamageApplied -= Hurt; owner.Died -= Death; }
            if (contact != null) contact.DamageApplied -= Attack;
        }
        private void Hurt(DamageRequest request)
        {
            if (!owner.IsAlive || Time.timeAsDouble < nextHurt) return;
            nextHurt = Time.timeAsDouble + hurtInterval;
            GameAudio.Play(elite ? AudioCue.EliteHurt : AudioCue.NPCHurt, gameObject);
        }
        private void Death() => GameAudio.Play(elite ? AudioCue.EliteDeath : AudioCue.NPCDeath, gameObject);
        private void Attack(DamageRequest request)
        {
            if (elite) GameAudio.Play(AudioCue.EliteAttack, gameObject);
        }
    }
}
