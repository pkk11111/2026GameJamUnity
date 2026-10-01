// 职责：为当前主场景玩家接线移动音频和接触帧；只补缺项，保留用户已有映射与调参。
// 维护 audio-player-movement；交接 docs/handoffs/audio-player-movement.handoff；规范 AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Presentation;
using Regrowth.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Regrowth.Audio.Editor
{
    public static class MovementAudioSetup
    {
        [MenuItem("Tools/Audio/Install Player Movement Audio")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/WhiteBox/Scenes/Level_Whitebox.unity")
                throw new InvalidOperationException("Open Level_Whitebox outside Play mode.");
            var players = UnityEngine.Object.FindObjectsByType<WhiteboxPlayer2D>(FindObjectsSortMode.None);
            var backends = UnityEngine.Object.FindObjectsByType<RewardAudioBackend>(FindObjectsSortMode.None);
            if (players.Length != 1 || backends.Length != 1)
                throw new InvalidOperationException("Require exactly one player and audio backend.");
            var player = players[0];
            var presentation = player.GetComponent<PlayerCharacterPresentation>();
            if (presentation == null) throw new InvalidOperationException("Player presentation is required.");
            var art = new SerializedObject(presentation);
            var set = art.FindProperty("animations").objectReferenceValue as PlayerAnimationSet;
            if (set == null) throw new InvalidOperationException("Bind the animation set before audio setup.");
            var component = player.GetComponent<PlayerMovementAudio>();
            if (component == null) component = Undo.AddComponent<PlayerMovementAudio>(player.gameObject);
            var data = new SerializedObject(component);
            Bind(data, "state", player.GetComponent<PlayerState>());
            Bind(data, "run", art.FindProperty("run").objectReferenceValue);
            Bind(data, "motor", player);
            Bind(data, "body", player.GetComponent<Rigidbody2D>());
            Bind(data, "input", new SerializedObject(player).FindProperty("inputReader").objectReferenceValue);
            Bind(data, "presentation", presentation);
            var timings = data.FindProperty("contactTimings");
            var known = new HashSet<AnimationClip>();
            for (int i = 0; i < timings.arraySize; i++)
                known.Add(timings.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue as AnimationClip);
            int added = 0;
            for (int mask = 0; mask < 32; mask++)
            {
                var entry = set.Find(mask, PlayerVisualAction.Move);
                if (entry == null || entry.clip == null || entry.frames == null || entry.frames.Length == 0 || !known.Add(entry.clip)) continue;
                var timing = timings.GetArrayElementAtIndex(timings.arraySize++);
                timing.FindPropertyRelative("clip").objectReferenceValue = entry.clip;
                var frames = timing.FindPropertyRelative("frames");
                bool legs = (mask & 4) != 0;
                // 2D contact poses: one body scoot per loop; two alternating leg poses when available.
                frames.arraySize = legs && entry.frames.Length > 2 ? 2 : 1;
                frames.GetArrayElementAtIndex(0).intValue = 0;
                if (frames.arraySize == 2) frames.GetArrayElementAtIndex(1).intValue = (entry.frames.Length + 1) / 2;
                added++;
            }
            data.ApplyModifiedProperties();
            AddMapping(backends[0], AudioCue.PlayerMoveNoFeet, "Play_Move_NoFoots");
            AddMapping(backends[0], AudioCue.PlayerFootstep, "Play_Footstep");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MovementAudio] Installed explicit player bindings; added " + added + " move clip timings.", component);
        }

        private static void Bind(SerializedObject data, string name, UnityEngine.Object value)
        {
            if (value == null) throw new InvalidOperationException("Missing movement audio binding: " + name);
            data.FindProperty(name).objectReferenceValue = value;
        }

        private static void AddMapping(RewardAudioBackend backend, AudioCue cue, string eventName)
        {
            var data = new SerializedObject(backend);
            var list = data.FindProperty("mappings");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("cue").intValue == (int)cue) return;
            var entry = list.GetArrayElementAtIndex(list.arraySize++);
            entry.FindPropertyRelative("cue").intValue = (int)cue;
            entry.FindPropertyRelative("eventName").stringValue = eventName;
            data.ApplyModifiedProperties();
        }
    }
}
