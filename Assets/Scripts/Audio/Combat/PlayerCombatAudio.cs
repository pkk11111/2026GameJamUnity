// 职责：成功攻击通知转音频、落地边沿和精英音乐；不做伤害或移动。
// 依赖 Core/Runtime/WhiteBox/Bite/Sword/Fire/EnemyBasic/Audio；维护 audio-full-events。
// 交接 docs/handoffs/audio-full-events.handoff；规范 AGENTS.md；跨对象引用必须显式绑定。
using System.Collections.Generic;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using UnityEngine;
namespace Regrowth.Audio
{
    [DisallowMultipleComponent]
    public sealed class PlayerCombatAudio : MonoBehaviour
    {
        [SerializeField] private PlayerState state;
        [SerializeField] private WhiteboxPlayer2D motor;
        [SerializeField] private PlayerBiteAttack bite;
        [SerializeField] private PlayerSwordAttack sword;
        [SerializeField] private PlayerFireAttack fire;
        [SerializeField] private EnemyBasic elite;
        [SerializeField] private EnemyBasicAI eliteAI;
        [SerializeField] private ExplorationMusicZones music;
        [SerializeField, Min(0f)] private float minimumAirTime = 0.08f;
        private readonly HashSet<IDamageable> fireHits = new HashSet<IDamageable>();
        private bool sampled, grounded, airborne, armed;
        private float airTime;
        private void OnEnable()
        {
            if (state == null || motor == null || bite == null || sword == null || fire == null || elite == null || eliteAI == null || music == null)
            { Debug.LogError("[CombatAudio] Missing explicit bindings.", this); enabled = false; return; }
            bite.HitAccepted += BiteHit; sword.HitAccepted += SwordHit;
            fire.AttackStarted += FireStart; fire.HitAccepted += FireHit;
            state.Died += PlayerDeath; motor.Relocated += ResetLanding;
            ResetLanding();
        }
        private void OnDisable()
        {
            if (bite != null) bite.HitAccepted -= BiteHit;
            if (sword != null) sword.HitAccepted -= SwordHit;
            if (fire != null) { fire.AttackStarted -= FireStart; fire.HitAccepted -= FireHit; }
            if (state != null) state.Died -= PlayerDeath;
            if (motor != null) motor.Relocated -= ResetLanding;
            fireHits.Clear();
            if (music != null) music.SetEliteCombat(false);
        }
        private void FireStart() { fireHits.Clear(); GameAudio.Play(AudioCue.PlayerFire, gameObject); }
        private void FireHit(IDamageable target)
        {
            if (target is EnemyBasic enemy && fireHits.Add(target)) GameAudio.Play(AudioCue.FireHit, enemy.gameObject);
        }
        private void BiteHit(IDamageable target) => Hit(target, AudioCue.BiteHitNPC, AudioCue.BiteHitElite);
        private void SwordHit(IDamageable target) => Hit(target, AudioCue.SwordHitNPC, AudioCue.SwordHitElite);
        private static void Hit(IDamageable target, AudioCue normal, AudioCue boss)
        {
            if (target is EnemyBasic enemy && enemy.TryGetComponent<EnemyAudio>(out var audio))
                GameAudio.Play(audio.IsElite ? boss : normal, enemy.gameObject);
        }
        private void PlayerDeath() { ResetLanding(); music.StopMusic(); }
        private void ResetLanding() { sampled = grounded = airborne = armed = false; airTime = 0f; }
        private void LateUpdate()
        {
            if (state == null || motor == null || music == null) return;
            if (!state.IsAlive) { music.StopMusic(); return; }
            music.SetEliteCombat(elite != null && elite.IsAlive && elite.isActiveAndEnabled && eliteAI != null && eliteAI.isActiveAndEnabled && eliteAI.State == EnemyAIState.Chase);
        }
        private void FixedUpdate()
        {
            if (motor == null || state == null || !state.IsAlive || !motor.IsGameplayActive || motor.HasPendingRelocation)
            { ResetLanding(); return; }
            bool now = motor.IsGrounded;
            if (!sampled) { sampled = true; grounded = now; armed = now; return; }
            if (!now) { airborne = true; airTime += Time.fixedDeltaTime; }
            if (now && !grounded && airborne && armed && airTime >= minimumAirTime)
                GameAudio.Play(AudioCue.PlayerLand, gameObject);
            if (now) { armed = true; airborne = false; airTime = 0f; }
            grounded = now;
        }
    }
}
