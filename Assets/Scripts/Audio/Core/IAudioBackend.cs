// 职责：后端接入端口；不实现奖励或传送规则。
// 依赖：UnityEngine.GameObject、AudioCue。维护：总控；规范：根目录 AGENTS.md。
using UnityEngine;

namespace Regrowth.Audio
{
    /// <summary>null 是全局发声对象；StopAll(null) 不表示停整个游戏。</summary>
    public interface IAudioBackend
    {
        void Play(AudioCue cue, GameObject emitter);
        void StopAll(GameObject emitter);
    }
}

