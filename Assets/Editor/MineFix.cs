using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot, re-runnable fixes for the two mine-zone problems reported on device:
//
//   1. Every rock in a mine zone is now mineable. The zones were authored with
//      Rock_4 meshes as ResourceNodes and rock01/02/03 + PT_Ore_Rock_01 as pure
//      decoration mixed in among them — so you walked up to something that looked
//      like ore, swung at nothing, and it read as broken. Now anything shaped like
//      a rock in the mine CAN be mined. Per-node yield is trimmed so the extra nodes
//      do not inflate zone income much.
//
//   2. The player holds a PICKAXE while mining stone instead of the wood hatchet.
//      No pickaxe model shipped in the project, so this builds a low-poly one from
//      cubes (matching the flat-shaded look) parented into the hand grip, and wires
//      it to HeldToolSwap.
//
// Re-runnable: deletes and rebuilds the pickaxe, and is idempotent on the rocks
// (adds ResourceNode only where missing, then normalises every node's config).
public static class MineFix
{
    struct Zone { public string name; public int req; public int yield; public Zone(string n, int r, int y) { name = n; req = r; yield = y; } }

    // Uniform per-node yield chosen to hold each zone's total roughly constant after
    // the decoration rocks become nodes, while keeping stone slightly ahead of the
    // same-tier wood zone (Meadow wood 5, Orchard wood 10).
    //   Quarry:  89 -> 113 nodes, 7 -> 6/node  (623 -> ~678)
    //   OreField:90 -> 124 nodes, 14 -> 11/node (1260 -> ~1364)
    static readonly Zone[] Zones =
    {
        new Zone("Quarry_Stone_Lv1", 1, 6),
        new Zone("OreField_Lv5",     5, 11),
    };

    [MenuItem("Tools/Survival/Fix Mine Zones")]
    public static void Fix()
    {
        int converted = 0, normalised = 0;
        foreach (var z in Zones)
        {
            var zone = GameObject.Find("Zones/" + z.name);
            if (zone == null) { Debug.LogWarning($"[MineFix] zone {z.name} not found"); continue; }

            for (int i = 0; i < zone.transform.childCount; i++)
            {
                var rock = zone.transform.GetChild(i).gameObject;
                var node = rock.GetComponentInChildren<ResourceNode>();
                if (node == null)
                {
                    // A decoration rock: promote it to a real node. Its existing
                    // MeshCollider is what ResourceNode toggles on depletion.
                    node = rock.AddComponent<ResourceNode>();
                    converted++;
                }
                node.resourceType = ResourceType.Stone;
                node.requiredToolLevel = z.req;
                node.hitsToDeplete = 6;
                node.amountPerHit = 1;
                node.totalYield = z.yield;
                node.stumpPrefab = null;          // rocks sink, they don't leave a stump
                node.respawnMin = 30f;
                node.respawnMax = 60f;
                EditorUtility.SetDirty(rock);
                normalised++;
            }
        }

        BuildPickaxe();

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"[MineFix] DONE — {converted} rocks made mineable, {normalised} stone nodes normalised, pickaxe rebuilt and scene saved.");
    }

    // --- pickaxe ------------------------------------------------------------

    static Material MakeMat(string path, Color c, float smooth)
    {
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = c;
        m.SetFloat("_Smoothness", smooth);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void Box(string name, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());   // a held tool needs no collider
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localEulerAngles = euler;
        g.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void BuildPickaxe()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        var wood = MakeMat("Assets/Materials/PickaxeWood.mat", new Color(0.42f, 0.28f, 0.16f), 0.15f);
        var metal = MakeMat("Assets/Materials/PickaxeHead.mat", new Color(0.50f, 0.50f, 0.54f), 0.35f);

        var player = GameObject.Find("Player");
        Transform joint = null, hatchet = null;
        foreach (var t in player.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "jointItemR") joint = t;
            if (t.name == "Hatchet") hatchet = t;
        }
        if (joint == null || hatchet == null) { Debug.LogWarning("[MineFix] hand socket or hatchet not found"); return; }

        var old = joint.Find("Pickaxe");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Sits in the grip exactly like the hatchet; handle runs along local Y.
        var pick = new GameObject("Pickaxe");
        pick.transform.SetParent(joint, false);
        pick.transform.localPosition = hatchet.localPosition;
        pick.transform.localRotation = hatchet.localRotation;
        pick.transform.localScale = hatchet.localScale;

        Box("Handle", pick.transform, new Vector3(0f, 0.16f, 0f), new Vector3(0.035f, 0.52f, 0.035f), Vector3.zero, wood);
        Box("Head",   pick.transform, new Vector3(0f, 0.44f, 0f), new Vector3(0.30f, 0.055f, 0.055f), Vector3.zero, metal);
        // Tips angled DOWN toward the handle so it reads as a pick, not a hammer.
        Box("TipL", pick.transform, new Vector3(-0.18f, 0.425f, 0f), new Vector3(0.14f, 0.045f, 0.045f), new Vector3(0f, 0f, 26f), metal);
        Box("TipR", pick.transform, new Vector3(0.18f, 0.425f, 0f), new Vector3(0.14f, 0.045f, 0.045f), new Vector3(0f, 0f, -26f), metal);

        var swap = Object.FindFirstObjectByType<HeldToolSwap>();
        if (swap != null) { swap.pickaxe = pick; EditorUtility.SetDirty(swap); }
        pick.SetActive(false);   // HeldToolSwap turns it on only while mining
    }
}
