// 职责：宝箱/卡片/能力动作的 Wwise 映射；业务成功由原调用者判定。
// 维护：audio-rewards；交接：docs/handoffs/audio-rewards.handoff；不重复加载 SB_Main。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Regrowth.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(AkGameObj))]
    public sealed class RewardAudioBackend : MonoBehaviour, IAudioBackend
    {
        [Serializable]
        public sealed class Mapping
        {
            [Tooltip("稳定业务音频身份，不重排枚举数值。")]
            public AudioCue cue;
            [Tooltip("Wwise Event 精确名称；留空表示暂不接入。")]
            public string eventName;
            public Mapping(AudioCue cue, string eventName) { this.cue = cue; this.eventName = eventName; }
        }
        [SerializeField, Tooltip("必填；当前场景 SB_Main 的唯一加载者，禁用后不补播旧事件。")]
        private ExplorationMusicZones bankOwner;
        [SerializeField, Tooltip("UI全局声音的显式监听器；由安装工具绑定当前唯一AkAudioListener。")]
        private AkAudioListener uiListener;
        [SerializeField, Range(-48f, 0f), Tooltip("UI声音额外增益(dB)，暂定-10避免瞬态叠加削波；实时生效，不改变音乐和世界音效。")]
        private float uiGainDb = -10f;
        [SerializeField, Tooltip("实时读取；仅列出本模块已确认的映射，未列出的 Cue 保持静默。")]
        private Mapping[] mappings = {
            new Mapping(AudioCue.ChestOpened, "Play_Box_Open"),
            new Mapping(AudioCue.SwitchActivated, "Play_SwitchActivate"),
            new Mapping(AudioCue.CardsPresented, "Play_Card_PopOut"),
            new Mapping(AudioCue.CardHovered, "Play_Card_Hover"),
            new Mapping(AudioCue.CardSelected, "Play_Card_Selected"),
            new Mapping(AudioCue.AbilityGained, "Play_Player_AbilityGain"),
            new Mapping(AudioCue.AbilityLost, "Play_Player_AbilityLoss"),
            new Mapping(AudioCue.UIConfirm, "Play_UI_Click"),
            new Mapping(AudioCue.UIHovered, "Play_UI_Hover"),
            new Mapping(AudioCue.PlayerJump, "Play_Jump"),
            new Mapping(AudioCue.PlayerDash, "Play_Dash"),
            new Mapping(AudioCue.PlayerDoubleJump, "Play_DoubleJump"),
            new Mapping(AudioCue.PlayerMoveNoFeet, "Play_Move_NoFoots"),
            new Mapping(AudioCue.PlayerFootstep, "Play_Footstep")
        };
        private readonly Dictionary<AudioCue, int> counts = new Dictionary<AudioCue, int>();
        private readonly HashSet<AudioCue> warned = new HashSet<AudioCue>();
        public uint LastPlayingId { get; private set; }
        public bool IsReady => bankOwner != null && bankOwner.IsBankReady && AkUnitySoundEngine.IsInitialized();
        public int Count(AudioCue cue) => counts.TryGetValue(cue, out int count) ? count : 0;

        private void OnEnable()
        {
            if (bankOwner == null) { Debug.LogError("[RewardAudio] 缺少 Bank Owner。", this); return; }
            counts.Clear(); warned.Clear(); LastPlayingId = 0;
            GameAudio.InstallBackend(this);
        }
        private void OnDisable() => GameAudio.UninstallBackend(this);
        public void Play(AudioCue cue, GameObject emitter)
        {
            string eventName = null;
            foreach (var mapping in mappings)
                if (mapping != null && mapping.cue == cue) { eventName = mapping.eventName; break; }
            if (string.IsNullOrWhiteSpace(eventName)) return;
            if (!IsReady)
            {
                if (warned.Add(cue)) Debug.LogWarning("[RewardAudio] Bank 未就绪，跳过 " + cue, this);
                return;
            }
            var target = emitter != null ? emitter : gameObject;
            if (target == gameObject && uiListener != null)
                AkUnitySoundEngine.SetGameObjectOutputBusVolume(gameObject, uiListener.gameObject, Mathf.Pow(10f, uiGainDb / 20f));
            LastPlayingId = AkUnitySoundEngine.PostEvent(eventName, target);
            if (LastPlayingId == 0)
            {
                if (warned.Add(cue)) Debug.LogError("[RewardAudio] 投递失败 " + eventName, target);
                return;
            }
            counts[cue] = Count(cue) + 1;
        }
        public void StopAll(GameObject emitter)
        {
            if (AkUnitySoundEngine.IsInitialized()) AkUnitySoundEngine.StopAll(emitter != null ? emitter : gameObject);
        }
    }
}
