// 职责：显式菜单安装音乐区域；只在编辑模式操作用户授权的白板场景。
// 维护：audio-music-zones；交接：docs/handoffs/audio-music-zones.handoff；规范：根 AGENTS.md。
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Regrowth.Audio.Editor
{
    public static class MusicZoneSetup
    {
        private const string ScenePath = "Assets/Scenes/Gameplay/MainLevel.unity";

        [MenuItem("Tools/Audio/Install Whitebox Music Zones")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play 模式。");
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            if (UnityEngine.Object.FindFirstObjectByType<ExplorationMusicZones>() != null)
            { Debug.Log("[MusicZones] 已安装；保留现有 Inspector 配置。"); return; }
            var player = GameObject.Find("Player");
            var groundObject = GameObject.Find("TM_Ground");
            var ground = groundObject == null ? null : groundObject.GetComponent<Tilemap>();
            if (player == null || ground == null) throw new InvalidOperationException("缺少白板玩家/地面。");
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, 0);
            foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
            {
                if (!ground.HasTile(cell)) continue;
                min = Vector3.Min(min, ground.CellToWorld(cell));
                max = Vector3.Max(max, ground.CellToWorld(cell + new Vector3Int(1, 1, 0)));
            }
            // 用户图二：两条手绘线约位于地图底部向上20%、49%；不是三等分。
            float lower = Mathf.Round(min.y + (max.y - min.y) * 0.20f);
            float upper = Mathf.Round(min.y + (max.y - min.y) * 0.49f);
            var root = new GameObject("Audio_MusicZones");
            Undo.RegisterCreatedObjectUndo(root, "Install music zones");
            var runtime = GameObject.Find("00 Runtime");
            if (runtime != null) root.transform.SetParent(runtime.transform, false);
            var controller = root.AddComponent<ExplorationMusicZones>();
            var emitter = new SerializedObject(root.GetComponent<AkGameObj>());
            emitter.FindProperty("isEnvironmentAware").boolValue = false;
            emitter.ApplyModifiedPropertiesWithoutUndo();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("player").objectReferenceValue = player.transform;
            var zones = serialized.FindProperty("zones");
            zones.arraySize = 3;
            float[] edges = { min.y - 5f, lower, upper, max.y + 5f };
            for (int i = 0; i < 3; i++)
            {
                var zoneObject = new GameObject("MusicZone_Level" + (i + 1));
                zoneObject.transform.SetParent(root.transform, false);
                zoneObject.transform.position = new Vector3((min.x + max.x) / 2, (edges[i] + edges[i + 1]) / 2, 0);
                var box = zoneObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(max.x - min.x + 10, edges[i + 1] - edges[i]);
                var zone = zones.GetArrayElementAtIndex(i);
                zone.FindPropertyRelative("bounds").objectReferenceValue = box;
                zone.FindPropertyRelative("stateEvent").stringValue = "Set_State_Level" + (i + 1);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject = root;
            Debug.Log($"[MusicZones] INSTALLED map=({min.x},{min.y})..({max.x},{max.y}); boundaries Y={lower},{upper}; player={player.transform.position}");
        }
    }
}
