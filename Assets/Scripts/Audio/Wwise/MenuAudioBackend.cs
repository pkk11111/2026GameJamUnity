// 职责：主菜单UI的GameAudio后端与SB_Main生命周期；不启动关卡音乐、不控制场景切换。
// 维护controller/audio-fixes；依赖Audio.Core、Wwise；规范根AGENTS.md。
// 接线：主菜单独立对象，显式AkAudioListener；AkInitializer由场景提供。
// 交接：docs/handoffs/audio-fixes.handoff；退出菜单卸载自身，关卡继续使用原RewardAudioBackend。
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Regrowth.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(AkGameObj))]
    public sealed class MenuAudioBackend : MonoBehaviour, IAudioBackend
    {
        [SerializeField, Tooltip("必填，主菜单的Wwise监听器。")]
        private AkAudioListener listener;
        [SerializeField, Tooltip("重新启用时加载；不带扩展名。")]
        private string bankName = "SB_Main";
        [SerializeField] private string hoverEvent = "Play_UI_Hover";
        [SerializeField] private string clickEvent = "Play_UI_Click";
        [SerializeField, Range(-48f, 0f), Tooltip("主菜单UI增益，dB；与卡牌UI默认一致，实时读取。")]
        private float gainDb = -10f;
        [SerializeField, Min(1f), Tooltip("等待声音引擎的最大真实秒数；超时仍允许开始游戏。")]
        private float initializationTimeout = 15f;

        private bool ownsBank;
        private readonly HashSet<AudioCue> warned = new HashSet<AudioCue>();
        public bool IsReady { get; private set; }
        public uint LastPlayingId { get; private set; }
        public int HoverCount { get; private set; }
        public int ClickCount { get; private set; }

        private void OnEnable()
        {
            IsReady = false;
            LastPlayingId = 0;
            HoverCount = ClickCount = 0;
            warned.Clear();
            if (listener == null || string.IsNullOrWhiteSpace(bankName))
            {
                Debug.LogError("[MenuAudio] Bind listener and Bank Name.", this);
                return;
            }
            GameAudio.InstallBackend(this);
            StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
            float deadline = Time.realtimeSinceStartup + initializationTimeout;
            while (!AkUnitySoundEngine.IsInitialized())
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Debug.LogWarning("[MenuAudio] Wwise initialization timed out; menu remains usable.", this);
                    yield break;
                }
                yield return null;
            }
            AKRESULT result = AkUnitySoundEngine.LoadBank(bankName, out uint bankId);
            ownsBank = result == AKRESULT.AK_Success;
            IsReady = ownsBank || result == AKRESULT.AK_BankAlreadyLoaded;
            if (!IsReady)
            {
                Debug.LogWarning("[MenuAudio] Cannot load " + bankName + ": " + result, this);
            }
        }

        /// <summary>只映射菜单悬浮/确认；未就绪丢弃并限频提示，不缓存过期悬浮。</summary>
        public void Play(AudioCue cue, GameObject emitter)
        {
            string eventName = cue == AudioCue.UIHovered ? hoverEvent
                : cue == AudioCue.UIConfirm ? clickEvent : null;
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return;
            }
            if (!IsReady || !AkUnitySoundEngine.IsInitialized())
            {
                if (warned.Add(cue))
                {
                    Debug.LogWarning("[MenuAudio] Bank not ready; skipped " + cue, this);
                }
                return;
            }
            GameObject target = emitter != null ? emitter : gameObject;
            AkUnitySoundEngine.SetGameObjectOutputBusVolume(target, listener.gameObject, Mathf.Pow(10f, gainDb / 20f));
            LastPlayingId = AkUnitySoundEngine.PostEvent(eventName, target);
            if (LastPlayingId == 0)
            {
                if (warned.Add(cue))
                {
                    Debug.LogError("[MenuAudio] Failed to post " + eventName, this);
                }
                return;
            }
            if (cue == AudioCue.UIHovered)
            {
                HoverCount++;
            }
            else
            {
                ClickCount++;
            }
        }

        public void StopAll(GameObject emitter)
        {
            if (AkUnitySoundEngine.IsInitialized())
            {
                AkUnitySoundEngine.StopAll(emitter != null ? emitter : gameObject);
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            GameAudio.UninstallBackend(this);
            StopAll(null);
            if (ownsBank && AkUnitySoundEngine.IsInitialized())
            {
                AkUnitySoundEngine.UnloadBank(bankName, IntPtr.Zero);
            }
            IsReady = ownsBank = false;
        }
    }
}
