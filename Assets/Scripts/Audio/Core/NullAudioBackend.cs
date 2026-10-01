// 职责：默认空后端；不实现奖励或传送规则。
// 依赖：UnityEngine.GameObject、IAudioBackend。维护：总控；规范：根目录 AGENTS.md。
using UnityEngine;

namespace Regrowth.Audio
{
    /// <summary>默认无声后端；不阻塞、不缓存、不刷日志。</summary>
    internal sealed class NullAudioBackend : IAudioBackend
    {
        public void Play(AudioCue cue, GameObject emitter)
        {
        }

        public void StopAll(GameObject emitter)
        {
        }
    }
}

