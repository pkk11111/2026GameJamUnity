// 职责：显式菜单为当前白板全部地刺安装音频；编辑器操作，不改碰撞形状。
// 维护：audio-traps；交接：docs/handoffs/audio-traps.handoff；规范：根 AGENTS.md。
using System;
using Regrowth.Gameplay.WhiteBox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Regrowth.Audio.Editor
{
    public static class SpikeAudioSetup
    {
        [MenuItem("Tools/Audio/Install Whitebox Spike Audio")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play。");
            if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Gameplay/MainLevel.unity")
                throw new InvalidOperationException("请先打开 Level_Whitebox。");
            var owners = UnityEngine.Object.FindObjectsByType<ExplorationMusicZones>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (owners.Length != 1) throw new InvalidOperationException("场景须有唯一 SB_Main 加载控制器。");
            var spikes = UnityEngine.Object.FindObjectsByType<PrototypeSpike2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var spike in spikes)
            {
                var audio = spike.GetComponent<SpikeAudioEmitter>();
                if (audio == null) audio = Undo.AddComponent<SpikeAudioEmitter>(spike.gameObject);
                var so = new SerializedObject(audio);
                so.FindProperty("bankOwner").objectReferenceValue = owners[0];
                so.ApplyModifiedProperties();
                var emitter = new SerializedObject(spike.GetComponent<AkGameObj>());
                emitter.FindProperty("isEnvironmentAware").boolValue = false;
                emitter.ApplyModifiedProperties();
                Debug.Log($"[SpikeAudioSetup] {spike.name} at {spike.transform.position}", spike);
            }
            EditorSceneManager.MarkSceneDirty(owners[0].gameObject.scene);
            EditorSceneManager.SaveScene(owners[0].gameObject.scene);
            Debug.Log($"[SpikeAudioSetup] Installed {spikes.Length} spike emitters.");
        }
    }
}
