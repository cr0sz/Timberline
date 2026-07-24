using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using TMPro;

// Re-runnable builder for the MARKET — the place you walk into to sell loot and buy
// upgrades. Menu: Tools/Survival/Build Market.
//
// What it replaces: the Shop trigger used to be dressed with nineteen loose props
// dragged out of the Kabungus household pack — four ammo crates, four ammo cases, a
// briefcase, two toolboxes, a beer mug, a baseball bat and a chest. Modern-day garage
// clutter standing in a low-poly medieval valley, and no indication that it was a
// shop at all. This builds an actual trading post instead: a timbered counter under a
// striped awning, goods laid out on it, a signboard, and a merchant standing behind it.
//
// The stall is built as its own root under BaseCamp rather than as a child of Shop,
// because the Shop GameObject carries the trigger's non-uniform scale (3, 3, 2.5) and
// anything parented to it inherits that squash.
public static class MarketSetup
{
    const string PrefabDir = "Assets/Prefabs";
    // The same rig the player uses. The obvious pick was the JC Ranger, but that pack
    // is a MODULAR character: its FBX contains only clothing slots (shirt, pants,
    // shoes, four accessory meshes) — no head, no arms, no body — so it builds a
    // headless floating jerkin. This prefab is a complete human, already Humanoid, and
    // already proven by the player.
    const string MerchantModel = "Assets/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Prefabs_Humans/HumanMale_Character_FREE.prefab";
    // Dress the merchant so they don't read as a clone of the (shirtless) player. These
    // meshes ship INSIDE the prefab, already skinned to this skeleton and switched off,
    // so it's a SetActive rather than a rebind.
    static readonly string[] MerchantWears = { "Starter_Chest", "Starter_Pants", "Starter_Boots" };
    static readonly string[] MerchantHides = { "Underwear" };
    const string IdleFbx = "Assets/Blink/Art/Animations/Animations_Starter_Pack/Movement/Idle.fbx";
    // The clip's ASSET name, which is not the takeName in the .fbx.meta
    // ("human_male_idle_01"). Reading the meta and trusting it costs you a T-posing
    // merchant and a confusing warning — the importer renames the take.
    const string IdleClip = "Idle";
    const string MerchantController = "Assets/Animations/Merchant.controller";

    [MenuItem("Tools/Survival/Build Market")]
    public static void Build()
    {
        var shop = Object.FindFirstObjectByType<Shop>();
        if (shop == null) { Debug.LogError("[MarketSetup] no Shop in the scene."); return; }

        // --- clear the junk ---
        // Everything hanging off the Shop trigger, plus the two loose props the camp
        // used as its shop marker.
        int junk = shop.transform.childCount;
        for (int i = shop.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(shop.transform.GetChild(i).gameObject);
        junk += Kill("Shop_Chest") + Kill("Shop_Logs");
        Kill("Market");

        var camp = GameObject.Find("BaseCamp");
        var root = new GameObject("Market");
        if (camp != null) root.transform.SetParent(camp.transform);
        // Sit the stall on the trigger, but with a clean identity scale of its own.
        root.transform.position = shop.transform.position;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var wood = AssetDatabase.LoadAssetAtPath<Material>($"{PrefabDir}/WoodMat.mat");
        var stone = AssetDatabase.LoadAssetAtPath<Material>($"{PrefabDir}/StoneMat.mat");
        var beam = Mat("MarketBeam", new Color(0.32f, 0.21f, 0.13f));       // dark structural timber
        var clothA = Mat("MarketClothA", new Color(0.74f, 0.25f, 0.22f));   // awning stripe 1
        var clothB = Mat("MarketClothB", new Color(0.91f, 0.86f, 0.74f));   // awning stripe 2

        BuildStall(root.transform, wood, beam, clothA, clothB);
        BuildGoods(root.transform, wood, stone);
        BuildSign(root.transform, beam);
        BuildMerchant(root.transform);

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[MarketSetup] DONE — removed {junk} junk props, built stall + awning + goods + merchant.");
    }

    static int Kill(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) return 0;
        Object.DestroyImmediate(go);
        return 1;
    }

    // ------------------------------------------------------------------ the stall

    // Local -Z is the FRONT (the customer's side) — the same facing the camp's world
    // labels use, so the counter opens toward the camera rather than away from it.
    static void BuildStall(Transform root, Material wood, Material beam, Material clothA, Material clothB)
    {
        var stall = Child(root, "Stall");

        // counter: a solid front panel with a wider plank laid across the top
        Box(stall, "CounterBody", new Vector3(0f, 0.48f, 0.35f), new Vector3(3.5f, 0.96f, 0.55f), Vector3.zero, wood, true);
        Box(stall, "CounterTop", new Vector3(0f, 1.02f, 0.30f), new Vector3(3.8f, 0.12f, 0.95f), Vector3.zero, beam, true);
        // a lip along the front edge so the top doesn't read as a floating slab
        Box(stall, "CounterLip", new Vector3(0f, 0.93f, -0.14f), new Vector3(3.8f, 0.10f, 0.10f), Vector3.zero, beam, false);

        // back shelf, so the merchant has something behind them
        Box(stall, "ShelfPost_L", new Vector3(-1.6f, 0.75f, 1.30f), new Vector3(0.12f, 1.5f, 0.12f), Vector3.zero, beam, false);
        Box(stall, "ShelfPost_R", new Vector3(1.6f, 0.75f, 1.30f), new Vector3(0.12f, 1.5f, 0.12f), Vector3.zero, beam, false);
        Box(stall, "Shelf", new Vector3(0f, 1.20f, 1.30f), new Vector3(3.3f, 0.10f, 0.40f), Vector3.zero, wood, false);
        Box(stall, "ShelfLow", new Vector3(0f, 0.62f, 1.30f), new Vector3(3.3f, 0.10f, 0.40f), Vector3.zero, wood, false);

        // four corner posts carrying the awning
        float[] px = { -1.75f, 1.75f };
        float[] pz = { -0.15f, 1.35f };
        int n = 0;
        foreach (var x in px)
            foreach (var z in pz)
                Cyl(stall, $"Post{n++}", new Vector3(x, 1.3f, z), new Vector3(0.11f, 1.3f, 0.11f), Vector3.zero, beam, true);

        // cross beams at the top of the posts
        Box(stall, "BeamFront", new Vector3(0f, 2.58f, -0.15f), new Vector3(3.7f, 0.14f, 0.14f), Vector3.zero, beam, false);
        Box(stall, "BeamBack", new Vector3(0f, 2.58f, 1.35f), new Vector3(3.7f, 0.14f, 0.14f), Vector3.zero, beam, false);

        // Striped canopy: eight alternating planks, tilted forward so it reads as a
        // market awning rather than a flat lid. Alternating materials is what sells
        // "market stall" instantly — a single-colour roof reads as a shed.
        // Sits ABOVE the cross beams: at pivot 2.85 with an 8-degree slope the front
        // edge (1.2m out) drops ~0.17 to 2.68, still clear of the 2.58 beams. At the
        // first pass it was 2.72 with a 14-degree slope, which put the front edge at
        // 2.43 — under the front beam, so a dark bar cut straight across the awning.
        var awning = Child(stall, "Awning");
        awning.localPosition = new Vector3(0f, 2.85f, 0.55f);
        awning.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        const int Stripes = 8;
        const float StripeW = 0.52f;
        for (int i = 0; i < Stripes; i++)
        {
            float x = (i - (Stripes - 1) * 0.5f) * StripeW;
            Box(awning, $"Stripe{i}", new Vector3(x, 0f, 0f), new Vector3(StripeW, 0.07f, 2.4f),
                Vector3.zero, (i % 2 == 0) ? clothA : clothB, false);
        }
        // scalloped front valance — the little hanging edge every market awning has
        for (int i = 0; i < Stripes; i++)
        {
            float x = (i - (Stripes - 1) * 0.5f) * StripeW;
            Box(awning, $"Valance{i}", new Vector3(x, -0.13f, -1.22f), new Vector3(StripeW, 0.26f, 0.06f),
                Vector3.zero, (i % 2 == 0) ? clothA : clothB, false);
        }
    }

    // ------------------------------------------------------------------- the goods

    // What's actually FOR sale here is upgrades, so the counter is dressed with the
    // trade rather than with product: stacked wares, a scale, coins, crates of stock.
    static void BuildGoods(Transform root, Material wood, Material stone)
    {
        var goods = Child(root, "Goods");

        // Kabungus props that survive the medieval read: bowls, a pot, a plate, a cup,
        // coins. Everything modern (toolboxes, briefcases, ammo) stays deleted.
        Prop(goods, "Bowl", new Vector3(-1.25f, 1.09f, 0.28f), 1.0f, 0f);
        Prop(goods, "Bowl_001", new Vector3(-0.92f, 1.09f, 0.42f), 1.0f, 35f);
        Prop(goods, "Pot", new Vector3(1.30f, 1.09f, 0.34f), 0.9f, -20f);
        Prop(goods, "Plate", new Vector3(0.55f, 1.09f, 0.46f), 1.0f, 0f);
        Prop(goods, "Goblet", new Vector3(0.90f, 1.09f, 0.24f), 1.0f, 0f);
        Prop(goods, "Money", new Vector3(-0.15f, 1.09f, 0.22f), 1.0f, 12f);
        // stock on the back shelf
        Prop(goods, "Bottle3Green", new Vector3(-1.05f, 1.26f, 1.30f), 1.0f, 0f);
        Prop(goods, "Bottle3Blue", new Vector3(-0.72f, 1.26f, 1.30f), 1.0f, 0f);
        Prop(goods, "Bottle1", new Vector3(-0.40f, 1.26f, 1.30f), 1.0f, 0f);
        Prop(goods, "Mug", new Vector3(0.95f, 1.26f, 1.30f), 1.0f, 0f);
        Prop(goods, "Cup", new Vector3(1.25f, 1.26f, 1.30f), 1.0f, 0f);
        // the trader's own tools, hung at the end of the counter
        Prop(goods, "Hatchet", new Vector3(1.62f, 1.11f, 0.55f), 1.0f, 90f);

        // Crates of stock stacked beside the stall — plain boxes, not the ammo crates.
        var crates = Child(goods, "Crates");
        Box(crates, "Crate0", new Vector3(-2.45f, 0.30f, 0.55f), new Vector3(0.62f, 0.60f, 0.62f), new Vector3(0f, 14f, 0f), wood, true);
        Box(crates, "Crate1", new Vector3(-2.40f, 0.88f, 0.62f), new Vector3(0.54f, 0.54f, 0.54f), new Vector3(0f, -22f, 0f), wood, true);
        Box(crates, "Crate2", new Vector3(2.42f, 0.30f, 0.70f), new Vector3(0.62f, 0.60f, 0.62f), new Vector3(0f, -9f, 0f), wood, true);
        // a couple of sacks (squashed spheres) to break up the boxiness
        Sphere(crates, "Sack0", new Vector3(2.38f, 0.78f, 0.70f), new Vector3(0.52f, 0.40f, 0.52f), stone);
        Sphere(crates, "Sack1", new Vector3(2.05f, 0.26f, 1.15f), new Vector3(0.48f, 0.36f, 0.48f), stone);
    }

    // Instantiate a Kabungus household prefab as counter dressing. Colliders are
    // stripped — a bowl you can bump into is a bowl that shoves the player around.
    static void Prop(Transform parent, string name, Vector3 pos, float scale, float yaw)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Kabungus/HouseholdItems/Prefabs/{name}.prefab");
        if (src == null) { Debug.LogWarning($"[MarketSetup] missing prop: {name}"); return; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        go.name = name;
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
    }

    // -------------------------------------------------------------------- the sign

    static void BuildSign(Transform root, Material beam)
    {
        var sign = Child(root, "Sign");
        sign.localPosition = new Vector3(0f, 3.42f, -0.50f);
        sign.localRotation = Quaternion.Euler(-8f, 0f, 0f);

        Box(sign, "Board", Vector3.zero, new Vector3(2.0f, 0.62f, 0.08f), Vector3.zero, beam, false);
        Box(sign, "TrimTop", new Vector3(0f, 0.34f, 0f), new Vector3(2.15f, 0.08f, 0.12f), Vector3.zero, beam, false);
        Box(sign, "TrimBot", new Vector3(0f, -0.34f, 0f), new Vector3(2.15f, 0.08f, 0.12f), Vector3.zero, beam, false);

        // TextMeshPro reads correctly from its local -Z side, NOT +Z — so a label on a
        // -Z-facing sign needs NO yaw. Rotating it 180 (the intuitive guess, and what
        // TycoonSetup's pad labels do) turns the readable face away and you see the
        // back of the glyphs through the un-culled quad: "MARKET" renders as "TEKRAM".
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(sign);
        labelGO.transform.localPosition = new Vector3(0f, 0f, -0.06f);
        labelGO.transform.localRotation = Quaternion.identity;
        labelGO.transform.localScale = Vector3.one;
        var tmp = labelGO.AddComponent<TextMeshPro>();
        tmp.text = "MARKET";
        tmp.fontSize = 3.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.96f, 0.82f, 0.45f);   // amber, matching the UI accent
        tmp.fontStyle = FontStyles.Bold;
        var rt = tmp.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(2.0f, 0.6f);
    }

    // ---------------------------------------------------------------- the merchant

    // A static mesh behind a counter reads as a mannequin, so the NPC gets a real
    // looping idle. The Ranger imports as Humanoid (animationType 3) and the starter
    // pack's idle is Humanoid too, so it retargets with no rig work — that is the only
    // reason this is a two-line animator instead of an authoring job.
    static void BuildMerchant(Transform root)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(MerchantModel);
        if (model == null) { Debug.LogWarning($"[MarketSetup] missing merchant model: {MerchantModel}"); return; }

        var npc = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
        npc.name = "Merchant";
        npc.transform.localPosition = new Vector3(0.15f, 0f, 0.92f);   // behind the counter
        npc.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);  // facing the customer (-Z)

        foreach (var r in npc.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (System.Array.IndexOf(MerchantWears, r.gameObject.name) >= 0) r.gameObject.SetActive(true);
            else if (System.Array.IndexOf(MerchantHides, r.gameObject.name) >= 0) r.gameObject.SetActive(false);
        }

        var anim = npc.GetComponent<Animator>();
        if (anim == null) anim = npc.AddComponent<Animator>();
        anim.runtimeAnimatorController = EnsureMerchantController();
        anim.applyRootMotion = false;   // root motion would walk them out of the stall

        // The merchant is scenery, not an obstacle: a collider here would let the
        // player shove them around, and would block placement checks for no reason.
        foreach (var c in npc.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
    }

    static AnimatorController EnsureMerchantController()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(MerchantController);
        if (existing != null) return existing;

        AnimationClip idle = null;
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(IdleFbx))
        {
            // An FBX also exposes hidden "__preview__" duplicates of every take; those
            // are editor scratch objects and assigning one leaves the state empty.
            if (a is AnimationClip c && !c.name.StartsWith("__preview__") && c.name == IdleClip)
            { idle = c; break; }
        }
        if (idle == null)
        {
            Debug.LogWarning($"[MarketSetup] clip '{IdleClip}' not found in {IdleFbx} — merchant will T-pose.");
            return null;
        }

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(MerchantController);
        var state = ctrl.layers[0].stateMachine.AddState("Idle");
        state.motion = idle;
        ctrl.layers[0].stateMachine.defaultState = state;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    // ------------------------------------------------------------------- primitives

    static Material Mat(string name, Color c)
    {
        var path = $"{PrefabDir}/M_{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { existing.color = c; EditorUtility.SetDirty(existing); return existing; }

        // Asset-backed, not `new Material` — a runtime material loses its shader
        // reference when the scene reloads and renders magenta.
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
        m.SetFloat("_Smoothness", 0.05f);   // matte, to sit with the flat-shaded world
        m.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material m, bool solid)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localRotation = Quaternion.Euler(euler);
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        // Only the pieces the player could walk into keep a collider. Awning stripes
        // and trim keep none — 16 stripe colliders three metres up is pure physics tax.
        if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material m, bool solid)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localRotation = Quaternion.Euler(euler);
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Sphere(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = name;
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }
}
