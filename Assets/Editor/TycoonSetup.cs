using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using TMPro;

// One-shot scene builder for the tycoon camp: two walk-in upgrade pads
// (Campfire + Storage) and a MOVE button on the build UI. Re-runnable — it
// deletes anything it made before rebuilding. Menu: Tools/Survival/Build Tycoon Setup.
public static class TycoonSetup
{
    const string PrefabDir = "Assets/Prefabs";

    // Asset-backed material — a runtime `new Material` loses its shader when baked
    // into a prefab (shows magenta). Saving it as a .mat keeps the reference alive.
    static Material MakeMat(string name, Color baseCol, Color? emission = null)
    {
        var path = $"{PrefabDir}/M_{name}.mat";
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = baseCol;
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    [MenuItem("Tools/Survival/Build Tycoon Setup")]
    public static void Build()
    {
        // clean prior run
        Kill("CampfirePad"); Kill("StoragePad"); Kill("MoveToggle");

        var floatingText = AssetDatabase.LoadAssetAtPath<FloatingText>($"{PrefabDir}/FloatingText.prefab");
        var woodMat = AssetDatabase.LoadAssetAtPath<Material>($"{PrefabDir}/WoodMat.mat");
        var stoneMat = AssetDatabase.LoadAssetAtPath<Material>($"{PrefabDir}/StoneMat.mat");

        var campfirePrefab = BuildCampfirePrefab(woodMat, stoneMat);
        var cratePrefab = BuildCratePrefab();

        var camp = GameObject.Find("BaseCamp");
        Transform campT = camp != null ? camp.transform : null;

        // Pad placement is hemmed in by Landmark_Menhir, the 16m rock at the top of
        // camp: its footprint is x[-10.1..-1.9] z[4.1..11.6]. StoragePad used to sit at
        // (-4, 0, 6) — INSIDE that, so the pad was buried in the rock and walking onto
        // it put the player inside the mesh, which read as "you can walk into the
        // mountain". Both pads now sit south of z=4, clear of the rock.
        MakePad("CampfirePad", new Vector3(-4f, 0f, 3f), new Color(1f, 0.5f, 0.1f),
                UpgradeStation.Kind.Campfire, "Campfire", campfirePrefab, 50, floatingText, campT);
        MakePad("StoragePad", new Vector3(-4f, 0f, 0f), new Color(0.55f, 0.4f, 0.2f),
                UpgradeStation.Kind.Storage, "Storage", cratePrefab, 60, floatingText, campT);

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[TycoonSetup] DONE — pads + campfire/crate prefabs + MOVE button built and scene saved.");
    }

    static void Kill(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
    }

    // ---- Campfire prefab: stone ring + crossed logs + firelight, Campfire.cs ----
    static GameObject BuildCampfirePrefab(Material woodMat, Material stoneMat)
    {
        var root = new GameObject("Campfire");
        var fire = root.AddComponent<Campfire>();

        var pit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pit.name = "Pit"; pit.transform.SetParent(root.transform);
        pit.transform.localScale = new Vector3(1.2f, 0.06f, 1.2f);
        pit.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        if (stoneMat != null) pit.GetComponent<Renderer>().sharedMaterial = stoneMat;
        Object.DestroyImmediate(pit.GetComponent<Collider>());

        // ring of stones (flattened, darker so they read as rocks not eggs)
        var rockMat = MakeMat("FireRock", new Color(0.32f, 0.32f, 0.34f));
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Stone"; s.transform.SetParent(root.transform);
            s.transform.localScale = new Vector3(0.26f, 0.18f, 0.26f);
            s.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.62f, 0.07f, Mathf.Sin(a) * 0.62f);
            s.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            s.GetComponent<Renderer>().sharedMaterial = rockMat;
            Object.DestroyImmediate(s.GetComponent<Collider>());
        }

        // flames — emissive tongues. Modest emission so the ORANGE reads (high
        // emission blows out to white). A tall body + narrow tip fakes a flame point.
        var flameOuter = MakeMat("FlameOuter", new Color(1f, 0.35f, 0.05f), new Color(1f, 0.35f, 0.05f) * 1.4f);
        var flameInner = MakeMat("FlameInner", new Color(1f, 0.8f, 0.25f), new Color(1f, 0.8f, 0.25f) * 1.5f);
        void Flame(Vector3 pos, Vector3 scale, Material m)
        {
            var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            f.name = "Flame"; f.transform.SetParent(root.transform);
            f.transform.localScale = scale;
            f.transform.localPosition = pos;
            f.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(f.GetComponent<Collider>());
        }
        Flame(new Vector3(0f, 0.34f, 0f), new Vector3(0.34f, 0.55f, 0.34f), flameOuter);  // body
        Flame(new Vector3(0f, 0.62f, 0f), new Vector3(0.14f, 0.34f, 0.14f), flameOuter);  // tip point
        Flame(new Vector3(0.15f, 0.28f, 0.05f), new Vector3(0.16f, 0.42f, 0.16f), flameOuter); // side lick
        Flame(new Vector3(-0.14f, 0.26f, -0.06f), new Vector3(0.14f, 0.36f, 0.14f), flameOuter);// side lick
        Flame(new Vector3(0f, 0.3f, 0f), new Vector3(0.18f, 0.4f, 0.18f), flameInner);    // yellow core

        // crossed logs
        for (int i = 0; i < 3; i++)
        {
            var l = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            l.name = "Log"; l.transform.SetParent(root.transform);
            l.transform.localScale = new Vector3(0.12f, 0.45f, 0.12f);
            l.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            l.transform.localRotation = Quaternion.Euler(70f, i * 60f, 0f);
            if (woodMat != null) l.GetComponent<Renderer>().sharedMaterial = woodMat;
            Object.DestroyImmediate(l.GetComponent<Collider>());
        }

        // firelight
        var lightGO = new GameObject("FireLight");
        lightGO.transform.SetParent(root.transform);
        lightGO.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        var lt = lightGO.AddComponent<Light>();
        lt.type = LightType.Point; lt.color = new Color(1f, 0.6f, 0.25f);
        lt.range = 5f; lt.intensity = 2f;
        fire.fireLight = lt;

        var path = $"{PrefabDir}/Campfire.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ---- Storage crate prefab: clone an existing AmmoCrate mesh in the scene ----
    static GameObject BuildCratePrefab()
    {
        var src = GameObject.Find("AmmoCrate");
        var path = $"{PrefabDir}/StorageCrate.prefab";
        if (src == null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
            var p = PrefabUtility.SaveAsPrefabAsset(box, path);
            Object.DestroyImmediate(box);
            return p;
        }
        var copy = Object.Instantiate(src);
        copy.name = "StorageCrate";
        copy.transform.localScale = src.transform.lossyScale;
        var prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
        Object.DestroyImmediate(copy);
        return prefab;
    }

    // ---- Walk-in pad: disc marker + trigger + world label + UpgradeStation ----
    static void MakePad(string name, Vector3 pos, Color tint, UpgradeStation.Kind kind,
                        string display, GameObject structurePrefab, int baseCost,
                        FloatingText ft, Transform parent)
    {
        var pad = new GameObject(name);
        if (parent != null) pad.transform.SetParent(parent);
        pad.transform.position = pos;

        // visual disc
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Disc"; disc.transform.SetParent(pad.transform);
        disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        disc.transform.localScale = new Vector3(1.6f, 0.05f, 1.6f);
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.GetComponent<Renderer>().sharedMaterial = MakeMat($"Pad_{name}", tint, tint * 0.6f);

        // trigger
        var box = pad.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(2.2f, 2f, 2.2f);
        box.center = new Vector3(0f, 1f, 0f);

        // world label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(pad.transform);
        labelGO.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        // NO yaw. TextMeshPro reads correctly from its local -Z side, which is already
        // the camera side — the 180 that used to be here (commented "face -Z") turned
        // the readable face away and rendered every pad label MIRRORED through the
        // un-culled quad. Confirmed in a play render: "Campfire MAX" came out backwards.
        labelGO.transform.localRotation = Quaternion.identity;
        var tmp = labelGO.AddComponent<TextMeshPro>();
        tmp.text = display;
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        var rt = tmp.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(4f, 1.5f);

        // component
        var us = pad.AddComponent<UpgradeStation>();
        us.kind = kind;
        us.displayName = display;
        us.structurePrefab = structurePrefab;
        us.baseCost = baseCost;
        us.floatingTextPrefab = ft;
        us.label = tmp;
        us.inventory = Object.FindFirstObjectByType<PlayerInventory>();
    }

    // The standalone white "MOVE" button that used to live here is GONE (2026-07-23).
    // It was dead legacy twice over: MOVE moved into the BUILD sheet sessions ago, and
    // then became the ghost-placement ArmMove flow. It also sat at bottom-right
    // (-20, 110) — directly on top of the BUILD toggle.
    //
    // WHY IT KEPT COMING BACK: it was parented to the CANVAS, but BuildCatalogSetup's
    // cleanup calls Kill(root, "MoveToggle") with root = SafeAreaRoot, so the search
    // never reached it. Every re-run of this tool resurrected a button nothing else
    // could delete. The Kill("MoveToggle") in Build() above still runs, and uses
    // GameObject.Find, so it clears any survivor from an older scene.
}
