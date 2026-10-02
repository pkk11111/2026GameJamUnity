// 职责：音乐分区、Bank 生命周期和 Wwise Event 适配；不修改玩家运动/公共 AudioCue。
// 模块/维护：audio-music-zones；依赖：Wwise 初始化器、显式玩家及触发器引用。
// 交接：docs/handoffs/audio-music-zones.handoff；规范：根 AGENTS.md。
using System;
using System.Collections;
using UnityEngine;

namespace Regrowth.Audio
{
    /// <summary>场景唯一音乐入口。启用时加载 Bank、设初始层并播放一次；禁用时只停自己的音乐。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AkGameObj))]
    public sealed class ExplorationMusicZones : MonoBehaviour
    {
        [Serializable]
        public sealed class Zone
        {
            [SerializeField, Tooltip("必填；覆盖本层的2D Trigger，运行时读取玩家中心是否在内。")]
            private BoxCollider2D bounds;
            [SerializeField, Tooltip("本层的 Wwise Set State Event；仅在切层时发送。")]
            private string stateEvent;
            internal BoxCollider2D Bounds => bounds;
            internal string StateEvent => stateEvent;
        }

        [Header("场景引用（重新启用生效）")]
        [SerializeField, Tooltip("必填；玩家根节点。出生和传送不依赖 OnTriggerEnter 回调。")]
        private Transform player;
        [SerializeField, Tooltip("从低到高排列；相交时优先列表后面的区域。")]
        private Zone[] zones;
        [Header("Wwise 映射（重新启用生效）")]
        [SerializeField, Tooltip("包含音乐和 State Events 的已生成 Bank，不带扩展名。")]
        private string bankName = "SB_Main";
        [SerializeField, Tooltip("启动时设为探索；进入精英战斗应由战斗系统另行通知。")]
        private string explorationEvent = "Set_State_Exploration";
        [SerializeField, Tooltip("仅初始化时播放一次，换层不重启。")]
        private string playEvent = "Play_Music_State";
        [SerializeField] private string eliteEvent = "Set_State_Elite";
        [SerializeField] private string stopEvent = "Stop_Music_State";
        [Header("运行参数")]
        [SerializeField, Min(0f), Tooltip("跨边界连续停留秒数，防止抖动；出生立即生效，使用未缩放时间。")]
        private float settleSeconds = 0.15f;
        [SerializeField, Min(1f), Tooltip("等待 Wwise 初始化的最大秒数；重新启用生效。")]
        private float initializationTimeout = 15f;
        [SerializeField, Tooltip("是否记录切层结果；实时生效。")]
        private bool logTransitions;

        /// <summary>当前已提交层，1起算；0表示尚未就绪。</summary>
        public int CurrentLevel { get; private set; }
        /// <summary>单次音乐实例，切层时不变；0表示未播放。</summary>
        public uint MusicPlayingId { get; private set; }
        private bool ownsBank;
        private bool ready;
        public bool IsBankReady => ready;
        public bool IsEliteMusic { get; private set; }

        /// <summary>Changes music state without restarting its playing instance.</summary>
        public void SetEliteCombat(bool value)
        {
            if (!ready || MusicPlayingId == 0 || value == IsEliteMusic) return;
            if (Post(value ? eliteEvent : explorationEvent)) IsEliteMusic = value;
        }

        /// <summary>Keep the bank resident so death and other SFX can finish.</summary>
        public void StopMusic()
        {
            if (!ready || MusicPlayingId == 0) return;
            Post(stopEvent);
            MusicPlayingId = 0;
        }
        private int pending = -1;
        private float pendingSince;

        private void OnEnable() { StartCoroutine(Initialize()); }

        private IEnumerator Initialize()
        {
            if (player == null || zones == null || zones.Length == 0 || string.IsNullOrWhiteSpace(bankName)
                || string.IsNullOrWhiteSpace(playEvent) || string.IsNullOrWhiteSpace(explorationEvent))
            { Fail("缺少玩家、音乐区域或 Wwise 映射。"); yield break; }
            foreach (var zone in zones)
                if (zone == null || zone.Bounds == null || !zone.Bounds.isTrigger || string.IsNullOrWhiteSpace(zone.StateEvent))
                { Fail("区域必须绑定 BoxCollider2D Trigger 和 State Event。"); yield break; }
            float deadline = Time.realtimeSinceStartup + initializationTimeout;
            while (!AkUnitySoundEngine.IsInitialized())
            {
                if (Time.realtimeSinceStartup >= deadline) { Fail("Wwise 初始化超时，请检查 WwiseGlobal。"); yield break; }
                yield return null;
            }
            var result = AkUnitySoundEngine.LoadBank(bankName, out uint bankId);
            ownsBank = result == AKRESULT.AK_Success;
            if (!ownsBank && result != AKRESULT.AK_BankAlreadyLoaded)
            { Fail("加载 " + bankName + " 失败：" + result); yield break; }
            if (!Post(explorationEvent)) yield break;
            int initial = LocatePlayer();
            if (initial < 0) { Fail("出生点不在音乐区域中，请扩大区域或检查玩家引用。"); yield break; }
            if (!Apply(initial)) yield break;
            MusicPlayingId = AkUnitySoundEngine.PostEvent(playEvent, gameObject,
                (uint)AkCallbackType.AK_EnableGetMusicPlayPosition, null, null);
            if (MusicPlayingId == 0) { Fail("播放音乐失败：" + playEvent); yield break; }
            ready = true;
        }

        private void LateUpdate()
        {
            if (!ready || player == null) return;
            int next = LocatePlayer();
            // 离开所有区域保持当前层；不使用 Exit 回调将全局 State 重置。
            if (next < 0 || next + 1 == CurrentLevel) { pending = -1; return; }
            if (pending != next) { pending = next; pendingSince = Time.unscaledTime; }
            if (Time.unscaledTime - pendingSince >= settleSeconds) { Apply(next); pending = -1; }
        }

        private int LocatePlayer()
        {
            for (int i = zones.Length - 1; i >= 0; i--)
                if (zones[i].Bounds != null && zones[i].Bounds.enabled && zones[i].Bounds.gameObject.activeInHierarchy
                    && zones[i].Bounds.OverlapPoint(player.position)) return i;
            return -1;
        }

        private bool Apply(int index)
        {
            if (!Post(zones[index].StateEvent)) return false;
            CurrentLevel = index + 1;
            if (logTransitions) Debug.Log("[MusicZones] Level" + CurrentLevel + " -> " + zones[index].StateEvent, this);
            return true;
        }

        private bool Post(string eventName)
        {
            if (AkUnitySoundEngine.PostEvent(eventName, gameObject) != 0) return true;
            Fail("Event 发送失败：" + eventName + "；请检查 SoundBank。");
            return false;
        }

        private void Fail(string message) { Debug.LogError("[MusicZones] " + message, this); enabled = false; }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (AkUnitySoundEngine.IsInitialized())
            {
                if (MusicPlayingId != 0) { AkUnitySoundEngine.PostEvent(stopEvent, gameObject); AkUnitySoundEngine.StopPlayingID(MusicPlayingId); }
                if (ownsBank) AkUnitySoundEngine.UnloadBank(bankName, IntPtr.Zero);
            }
            ready = false; ownsBank = false; MusicPlayingId = 0; CurrentLevel = 0; pending = -1;
            IsEliteMusic = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (zones == null) return;
            for (int i = 0; i < zones.Length; i++)
            {
                var box = zones[i]?.Bounds;
                if (box == null) continue;
                Gizmos.color = i == 0 ? new Color(0.5f, 0.2f, 0.9f) : i == 1 ? Color.yellow : Color.cyan;
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.offset, box.size);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
