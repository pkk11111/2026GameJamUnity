// 职责：统一音频转发入口；不实现奖励或传送规则。
// 依赖：UnityEngine、System、AudioCue、IAudioBackend、NullAudioBackend。维护：总控；规范：根目录 AGENTS.md。
using System;
using UnityEngine;

namespace Regrowth.Audio
{
    /// <summary>Unity 主线程调用的统一入口，不参与 gameplay 成功判断。</summary>
    public static class GameAudio
    {
        private static readonly IAudioBackend nullBackend = new NullAudioBackend();
        private static IAudioBackend backend = nullBackend;

        /// <summary>后端就绪后安装；启动组件负责清理被替换的后端。</summary>
        public static void InstallBackend(IAudioBackend newBackend)
        {
            backend = newBackend ?? throw new ArgumentNullException(nameof(newBackend));
        }

        /// <summary>旧组件卸载不能覆盖已经安装的新组件。</summary>
        public static void UninstallBackend(IAudioBackend expectedBackend)
        {
            if (ReferenceEquals(backend, expectedBackend))
            {
                backend = nullBackend;
            }
        }

        /// <summary>null 使用全局对象；已销毁对象跳过。</summary>
        public static void Play(AudioCue cue, GameObject emitter = null)
        {
            if (emitter == null && !ReferenceEquals(emitter, null))
            {
                return;
            }

            backend.Play(cue, emitter);
        }

        /// <summary>仅停止指定发声对象上的声音。</summary>
        public static void StopAll(GameObject emitter)
        {
            if (emitter == null && !ReferenceEquals(emitter, null))
            {
                return;
            }

            backend.StopAll(emitter);
        }

        // Domain Reload 关闭时也不保留上一轮 Play 的后端。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBackend()
        {
            backend = nullBackend;
        }
    }
}

