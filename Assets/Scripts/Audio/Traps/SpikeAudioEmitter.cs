// 职责：将地刺接受击退的事实映射到 Wwise，不处理伤害或另造接触冷却。
// 维护：audio-traps；依赖：本地 PrototypeSpike2D、场景唯一 SB_Main 加载者。
// 交接：docs/handoffs/audio-traps.handoff；规范：根 AGENTS.md。
using Regrowth.Gameplay.WhiteBox;
using UnityEngine;

namespace Regrowth.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(PrototypeSpike2D), typeof(AkGameObj))]
    public sealed class SpikeAudioEmitter : MonoBehaviour
    {
        [Header("音频接线")]
        [SerializeField, Tooltip("必填；当前场景加载 SB_Main 的音乐控制器。重新启用生效，不重复加载/卸载 Bank。")]
        private ExplorationMusicZones bankOwner;
        [SerializeField, Tooltip("Wwise Event 名，实时读取；Trap_Attack 是对象名，当前事件为 Play_Trap_Attack。")]
        private string attackEvent = "Play_Trap_Attack";
        [SerializeField, Tooltip("输出成功播放记录供验收，实时生效。")]
        private bool logPlayback;
        private PrototypeSpike2D spike;
        private bool warned;

        /// <summary>当前启用周期成功投递次数；仅作运行诊断，不驱动玩法。</summary>
        public int PostedCount { get; private set; }
        /// <summary>最近一次 Wwise 投递返回的实例 ID，0表示未成功。</summary>
        public uint LastPlayingId { get; private set; }

        private void OnEnable()
        {
            if (bankOwner == null || string.IsNullOrWhiteSpace(attackEvent))
            {
                Debug.LogError("[SpikeAudio] 缺少 Bank Owner 或 Event 映射。", this);
                enabled = false;
                return;
            }
            spike = GetComponent<PrototypeSpike2D>();
            PostedCount = 0; LastPlayingId = 0; warned = false;
            spike.KnockbackAccepted += PlayAcceptedHit;
        }

        private void OnDisable()
        {
            if (spike != null) spike.KnockbackAccepted -= PlayAcceptedHit;
        }

        private void PlayAcceptedHit()
        {
            // Bank 未就绪不排队补播旧碰撞，避免恢复/加载后突然发声。
            if (bankOwner == null || bankOwner.MusicPlayingId == 0 || !AkUnitySoundEngine.IsInitialized())
            {
                if (!warned) Debug.LogWarning("[SpikeAudio] SB_Main 尚未就绪，跳过本次地刺声音。", this);
                warned = true;
                return;
            }
            LastPlayingId = AkUnitySoundEngine.PostEvent(attackEvent, gameObject);
            if (LastPlayingId == 0)
            {
                if (!warned) Debug.LogError("[SpikeAudio] Event 投递失败：" + attackEvent, this);
                warned = true;
                return;
            }
            PostedCount++;
            if (logPlayback) Debug.Log($"[SpikeAudio] {name} {attackEvent} playingID={LastPlayingId}", this);
        }
    }
}
