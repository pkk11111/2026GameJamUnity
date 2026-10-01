// 职责：本次地图贴图的显式编辑器接线与检查，不参与游戏状态。
// 维护：world-artwork；交接 docs/handoffs/world-artwork.handoff；规范 AGENTS.md。
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class WorldArtworkSetup
{
    static WorldArtworkSetup() { EditorApplication.delayCall += Inspect; }
    private static void Inspect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "Level_Whitebox") return;
        Directory.CreateDirectory("Temp/WorldArtwork");
        var text = new StringBuilder(Application.dataPath + "\n" + scene.path + " dirty=" + scene.isDirty + "\n");
        foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
        {
            if (t.name.StartsWith("Door_") || t.name.StartsWith("Chest_") || t.name.StartsWith("Portal") || t.name.Contains("Exit") || t.name.Contains("Goal"))
            {
                text.AppendLine(t.name + " pos=" + t.position + " scale=" + t.lossyScale);
                foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>(true))
                    text.AppendLine("  " + sr.name + " sprite=" + (sr.sprite ? sr.sprite.name : "null") + " local=" + sr.transform.localPosition + " scale=" + sr.transform.localScale + " bounds=" + sr.bounds + " color=" + sr.color + " mode=" + sr.drawMode + " order=" + sr.sortingOrder);
            }
        }
        foreach (var map in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>(true)))
        {
            var cells = new System.Collections.Generic.List<Vector3Int>();
            foreach (var p in map.cellBounds.allPositionsWithin) { if (map.HasTile(p)) cells.Add(p); }
            var groups = cells.GroupBy(p => map.GetTile(p));
            foreach (var g in groups)
            {
                text.AppendLine("TILES " + map.name + " " + g.Key.name + " count=" + g.Count() + " first=" + g.First() + " color=" + map.GetColor(g.First()));
                if (g.Key.name == "Tile_CheckPoint") foreach (var p in g) text.AppendLine("GREEN " + p + " world=" + map.GetCellCenterWorld(p));
            }
            foreach (var p in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile(p);
                if (tile && (tile.name.ToLower().Contains("exit") || tile.name.ToLower().Contains("goal")))
                    text.AppendLine("EXIT TILE " + map.name + " " + p + " world=" + map.GetCellCenterWorld(p) + " tile=" + tile.name);
            }
        }
        File.WriteAllText("Temp/WorldArtwork/inspect.txt", text.ToString());
    }
}
