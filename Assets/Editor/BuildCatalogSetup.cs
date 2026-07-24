using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using TMPro;

// Re-runnable scene builder for the BUILD system and the two bits of UI that hang
// off it: the buildables catalog + its card grid, the BUILD/MOVE button pair, and
// the objective banner. Deletes what it made before rebuilding, so tuning is just
// "edit numbers, run again". Menu: Tools/Survival/Build Catalog + UI.
//
// ponytail: the structure prefabs are built from primitives rather than authored
// meshes because the project only ships 5 modular village pieces. Swap any entry's
// prefab for a real mesh later and nothing else changes.
public static class BuildCatalogSetup
{
    const string PrefabDir = "Assets/Prefabs";
    const string BuildDir = "Assets/Prefabs/Build";

    // --- palette (matches the existing HUD's warm-dark look) ---
    static readonly Color Panel = new Color(0.13f, 0.11f, 0.10f, 0.94f);
    static readonly Color PanelSoft = new Color(0.20f, 0.17f, 0.15f, 1f);
    static readonly Color Accent = new Color(0.91f, 0.64f, 0.29f, 1f);   // amber
    static readonly Color AccentDim = new Color(0.35f, 0.26f, 0.16f, 1f);
    static readonly Color Slate = new Color(0.24f, 0.42f, 0.60f, 1f);
    static readonly Color Ink = new Color(0.96f, 0.94f, 0.90f, 1f);
    static readonly Color InkDim = new Color(0.72f, 0.67f, 0.60f, 1f);
    // HUD styling: dark bar track + per-stat fills + a subtle chip behind each pill icon.
    // Track must be clearly DARKER than Panel (0.13) or the empty half of a bar
    // vanishes into the panel and you can't tell where the fill ends. It used to be
    // 0.19 — lighter than the panel by a hair, which read as no track at all.
    static readonly Color Track = new Color(0.07f, 0.06f, 0.05f, 1f);
    static readonly Color HpFill = new Color(0.82f, 0.30f, 0.27f, 1f);    // red
    static readonly Color FoodFill = new Color(0.88f, 0.56f, 0.24f, 1f);  // orange
    static readonly Color CarryFill = new Color(0.55f, 0.68f, 0.86f, 1f); // slate-blue (the BAG meter)
    static readonly Color Chip = new Color(0.27f, 0.22f, 0.18f, 1f);

    // Built-in rounded UISprite (5px sliced) — turns Panel_'s flat rectangles into
    // rounded panels at any size. Cached; editor-only resource.
    static Sprite _round;
    static Sprite RoundSprite => _round != null ? _round
        : (_round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"));
    static void Round(Image img) { if (img != null) { img.sprite = RoundSprite; img.type = Image.Type.Sliced; } }

    // A plain white square, generated into the project on first run.
    //
    // This exists because Image.Type.Filled REQUIRES a sprite: with sprite == null,
    // Image.OnPopulateMesh skips the filled path entirely and falls back to
    // GenerateSimpleSprite, so fillAmount is ignored and the bar always draws full.
    // The built-in UISprite would satisfy that, but Filled also disables 9-slicing,
    // so its rounded corner art gets stretched down the bar and the ends balloon
    // into a capsule. A square sprite is the only thing that is both fillable and
    // actually rectangular.
    const string RectSpritePath = "Assets/UI/WhiteRect.png";
    static Sprite _rect;
    static Sprite RectSprite
    {
        get
        {
            if (_rect != null) return _rect;
            _rect = AssetDatabase.LoadAssetAtPath<Sprite>(RectSpritePath);
            if (_rect == null) _rect = CreateRectSprite();
            return _rect;
        }
    }

    static Sprite CreateRectSprite()
    {
        if (!AssetDatabase.IsValidFolder("Assets/UI")) AssetDatabase.CreateFolder("Assets", "UI");

        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var px = new Color32[64];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        System.IO.File.WriteAllBytes(RectSpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(RectSpritePath, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(RectSpritePath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(RectSpritePath);
    }

    // Hard-edged rectangle. Bars use this, not Round: a meter needs a straight edge
    // so the fill level is readable at a glance.
    static void Flat(Image img) { if (img != null) { img.sprite = RectSprite; img.type = Image.Type.Simple; } }

    [MenuItem("Tools/Survival/Build Catalog + UI")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(BuildDir)) AssetDatabase.CreateFolder(PrefabDir, "Build");

        var catalog = BuildCatalog();
        var pc = WireBuildSystem(catalog);
        RebuildBuildMenu(catalog);
        RebuildPlacementBar(pc);
        RebuildToggles();
        RebuildShopPanel();
        CompactHud();
        RebuildObjectiveBanner();
        RebuildPauseAndVictory();
        RaiseOverlays();

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[BuildCatalogSetup] DONE — {catalog.Count} buildables, menu grid, BUILD/MOVE pair, objective banner.");
    }

    // uGUI draws in sibling order, and the menus are built before the HUD and the
    // objective banner — so the banner rendered straight over an open modal and the
    // settings icon sat on top of the build sheet's close button. Push every
    // full-screen overlay to the end, last one wins.
    static void RaiseOverlays()
    {
        var root = SafeRoot();
        if (root == null) return;
        // Order matters — uGUI draws by sibling index, so the last one wins. TitlePanel
        // is last of all: it is the only screen that must cover the HUD and the BUILD
        // button, which are siblings created later in the rebuild and would otherwise
        // render on top of it and stay tappable through the scrim.
        foreach (string n in new[] { "BuildMenu", "PausePanel", "VictoryPanel", "IntroPanel", "TitlePanel" })
        {
            var t = FindDeep(root, n);
            if (t != null) t.SetAsLastSibling();
        }
    }

    // ------------------------------------------------------------------ catalog

    struct Entry
    {
        public string name; public GameObject prefab; public int cost; public int maxCount;
        public Entry(string n, GameObject p, int c, int max = 0) { name = n; prefab = p; cost = c; maxCount = max; }
    }

    static List<Entry> BuildCatalog()
    {
        var woodMat = AssetDatabase.LoadAssetAtPath<Material>($"{PrefabDir}/WoodMat.mat");
        var stoneMat = AssetDatabase.LoadAssetAtPath<Material>($"{PrefabDir}/StoneMat.mat");

        var list = new List<Entry>
        {
            new Entry("Fence",      Village("Fence/PT_Modular_Fence_Wood_01", "B_Fence1"), 25),
            new Entry("Fence II",   Village("Fence/PT_Modular_Fence_Wood_02", "B_Fence2"), 25),
            new Entry("Fence III",  Village("Fence/PT_Modular_Fence_Wood_03", "B_Fence3"), 25),
            new Entry("Gate",       Village("Fence/PT_Modular_Gate_Wood_01",  "B_Gate"),   60),
            new Entry("Bridge",     Village("Bridge/PT_Wooden_Bridge_02",     "B_Bridge"), 80),
            new Entry("Wood Wall",  WoodWall(woodMat),                          45),
            new Entry("Stone Wall", StoneWall(stoneMat),                        90),
            new Entry("Barricade",  Barricade(woodMat),                         55),
            new Entry("Deck",       Deck(woodMat),                              35),
            // Torch was cut 2026-07-23: there is no night, so it was bought, placed
            // and lit for nothing. Its "keeps the dark things away" job moved to the
            // campfire, which now repels predators. NOTE: removing it shifted the
            // catalog indices of Watchtower (10->9) and Crate (11->10), so a save
            // written before this change rebuilds those two as the wrong prefab.
            new Entry("Watchtower", Watchtower(woodMat),                       220),
            new Entry("Crate",      Wrap(Load($"{PrefabDir}/StorageCrate.prefab"), "B_Crate"), 150),
            // Campfires are placeable now, CAPPED AT 3 (user call). Each one hands out
            // a predator-free radius, so uncapped fires would let the player pave the
            // valley into one big safe zone and delete the combat game. The pad still
            // exists and upgrades the tier of every fire at once.
            new Entry("Campfire",   Load($"{PrefabDir}/Campfire.prefab"),       50, 3),
        };

        list.RemoveAll(e => e.prefab == null);
        return list;
    }

    static GameObject Village(string sub, string outName) =>
        Wrap(Load($"Assets/Polytope Studio/Lowpoly_Village/Prefabs/Modular/{sub}.prefab"), outName);

    // Vendor prefabs ship with no collider and no PlacedBuildable, and we don't want
    // to edit assets inside an imported pack. Clone into our own Build/ folder and
    // add what a placed structure needs there.
    static GameObject Wrap(GameObject src, string outName)
    {
        if (src == null) return null;
        var copy = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        copy.name = outName;
        return Save(copy, outName);
    }

    static GameObject Load(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) Debug.LogWarning($"[BuildCatalogSetup] missing prefab: {path}");
        return go;
    }

    // -------------------------------------------------- procedural structures

    static GameObject Save(GameObject root, string name)
    {
        // A structure you can't bump into reads as a hologram — make sure every
        // buildable has at least one solid collider before it becomes a prefab.
        if (root.GetComponentInChildren<Collider>() == null)
        {
            var b = root.AddComponent<BoxCollider>();
            var bounds = Encapsulate(root);
            b.center = root.transform.InverseTransformPoint(bounds.center);
            b.size = bounds.size;
        }
        if (root.GetComponent<PlacedBuildable>() == null) root.AddComponent<PlacedBuildable>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{BuildDir}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Bounds Encapsulate(GameObject root)
    {
        var rends = root.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(root.transform.position, Vector3.one);
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    static GameObject Box(Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localRotation = Quaternion.Euler(euler);
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Cyl(Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.transform.SetParent(parent);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localRotation = Quaternion.Euler(euler);
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject WoodWall(Material wood)
    {
        var root = new GameObject("WoodWall");
        for (int i = 0; i < 5; i++)
            Box(root.transform, new Vector3(-0.8f + i * 0.4f, 1.0f, 0f), new Vector3(0.36f, 2.0f, 0.16f), Vector3.zero, wood);
        Box(root.transform, new Vector3(0f, 1.85f, 0f), new Vector3(2.1f, 0.14f, 0.22f), Vector3.zero, wood);
        return Save(root, "B_WoodWall");
    }

    static GameObject StoneWall(Material stone)
    {
        var root = new GameObject("StoneWall");
        // Three staggered courses of blocks — reads as masonry without a mesh.
        for (int row = 0; row < 3; row++)
        {
            float y = 0.35f + row * 0.66f;
            float off = (row % 2 == 0) ? 0f : 0.28f;
            for (int i = 0; i < 4; i++)
                Box(root.transform, new Vector3(-0.85f + i * 0.56f + off, y, 0f),
                    new Vector3(0.52f, 0.62f, 0.42f), new Vector3(0f, (i + row) * 3f, 0f), stone);
        }
        return Save(root, "B_StoneWall");
    }

    static GameObject Barricade(Material wood)
    {
        var root = new GameObject("Barricade");
        Cyl(root.transform, new Vector3(-0.7f, 0.6f, 0f), new Vector3(0.11f, 0.6f, 0.11f), new Vector3(0f, 0f, 12f), wood);
        Cyl(root.transform, new Vector3(0.7f, 0.6f, 0f), new Vector3(0.11f, 0.6f, 0.11f), new Vector3(0f, 0f, -12f), wood);
        Cyl(root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.09f, 0.95f, 0.09f), new Vector3(0f, 0f, 90f), wood);
        Cyl(root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.09f, 0.95f, 0.09f), new Vector3(0f, 0f, 90f), wood);
        return Save(root, "B_Barricade");
    }

    static GameObject Deck(Material wood)
    {
        var root = new GameObject("Deck");
        for (int i = 0; i < 6; i++)
            Box(root.transform, new Vector3(0f, 0.08f, -1.0f + i * 0.4f), new Vector3(2.4f, 0.14f, 0.36f), Vector3.zero, wood);
        return Save(root, "B_Deck");
    }

    static GameObject Watchtower(Material wood)
    {
        var root = new GameObject("Watchtower");
        float[] xs = { -0.9f, 0.9f };
        foreach (var x in xs)
            foreach (var z in xs)
                Cyl(root.transform, new Vector3(x, 1.5f, z), new Vector3(0.14f, 1.5f, 0.14f), Vector3.zero, wood);
        Box(root.transform, new Vector3(0f, 3.05f, 0f), new Vector3(2.4f, 0.16f, 2.4f), Vector3.zero, wood);
        // rails
        Box(root.transform, new Vector3(0f, 3.55f, 1.15f), new Vector3(2.4f, 0.12f, 0.12f), Vector3.zero, wood);
        Box(root.transform, new Vector3(0f, 3.55f, -1.15f), new Vector3(2.4f, 0.12f, 0.12f), Vector3.zero, wood);
        Box(root.transform, new Vector3(1.15f, 3.55f, 0f), new Vector3(0.12f, 0.12f, 2.4f), Vector3.zero, wood);
        return Save(root, "B_Watchtower");
    }

    // ------------------------------------------------------------- wire system

    static PlacementController WireBuildSystem(List<Entry> entries)
    {
        var bs = Object.FindFirstObjectByType<BuildSystem>();
        if (bs == null) { Debug.LogWarning("[BuildCatalogSetup] no BuildSystem in scene."); return null; }

        var arr = new BuildSystem.Buildable[entries.Count];
        for (int i = 0; i < entries.Count; i++)
            arr[i] = new BuildSystem.Buildable
            {
                name = entries[i].name,
                prefab = entries[i].prefab,
                cost = entries[i].cost,
                maxCount = entries[i].maxCount,
            };
        bs.catalog = arr;

        // Placement lives on the same GameObject as the BuildSystem. Ghost materials
        // and the bar UI are filled in later (RebuildPlacementBar); player/system
        // links are set here.
        var pc = bs.GetComponent<PlacementController>();
        if (pc == null) pc = bs.gameObject.AddComponent<PlacementController>();
        pc.buildSystem = bs;
        pc.player = bs.player;
        bs.placement = pc;

        EditorUtility.SetDirty(bs);
        EditorUtility.SetDirty(pc);
        return pc;
    }

    // The on-screen strip shown only while a ghost is live: Rotate | Grid | Cancel |
    // Confirm, pinned bottom-centre above the joystick zone. Rebuilt each run and
    // wired straight to PlacementController.
    static void RebuildPlacementBar(PlacementController pc)
    {
        var root = SafeRoot();
        if (root == null) return;
        Kill(root, "PlacementBar");
        if (pc == null) return;

        const float BarW = 760f, BarH = 108f, Gap = 12f;
        var bar = Rect("PlacementBar", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                       new Vector2(0f, 150f), new Vector2(BarW, BarH));
        Round(Panel_(bar, Panel));

        float cellW = (BarW - Gap * 5f) / 4f;
        float x0 = Gap + cellW * 0.5f;

        Button MakeBtn(string name, string text, Color tint, int slot, out TextMeshProUGUI label)
        {
            var rt = Rect(name, bar, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                          new Vector2(x0 + slot * (cellW + Gap), 0f), new Vector2(cellW, BarH - Gap * 2f));
            Panel_(rt, tint);
            Round(rt.GetComponent<Image>());
            var b = rt.gameObject.AddComponent<Button>();
            var cols = b.colors; cols.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); b.colors = cols;
            label = Label("Text", rt, text, 28f, TextAlignmentOptions.Center,
                          tint == Accent ? new Color(0.12f, 0.09f, 0.06f) : Ink,
                          Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return b;
        }

        var rotateBtn = MakeBtn("RotateBtn", "ROTATE", PanelSoft, 0, out _);
        var gridBtn = MakeBtn("GridBtn", "GRID: OFF", PanelSoft, 1, out var gridLabel);
        var cancelBtn = MakeBtn("CancelBtn", "CANCEL", new Color(0.32f, 0.20f, 0.18f, 1f), 2, out _);
        var confirmBtn = MakeBtn("ConfirmBtn", "PLACE", Accent, 3, out _);

        pc.bar = bar.gameObject;
        pc.rotateButton = rotateBtn;
        pc.gridButton = gridBtn;
        pc.cancelButton = cancelBtn;
        pc.confirmButton = confirmBtn;
        pc.gridLabel = gridLabel;

        UnityEventTools.AddPersistentListener(rotateBtn.onClick, new UnityEngine.Events.UnityAction(pc.Rotate));
        UnityEventTools.AddPersistentListener(gridBtn.onClick, new UnityEngine.Events.UnityAction(pc.ToggleGrid));
        UnityEventTools.AddPersistentListener(cancelBtn.onClick, new UnityEngine.Events.UnityAction(pc.Cancel));
        UnityEventTools.AddPersistentListener(confirmBtn.onClick, new UnityEngine.Events.UnityAction(pc.Confirm));

        bar.gameObject.SetActive(false);   // PlacementController turns it on while placing
        EditorUtility.SetDirty(pc);
    }

    // --------------------------------------------------------------- UI helpers

    static Transform SafeRoot()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) return null;
        var sar = FindDeep(canvas.transform, "SafeAreaRoot");
        return sar != null ? sar : canvas.transform;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }

    static void Kill(Transform parent, string name)
    {
        if (parent == null) return;
        var t = FindDeep(parent, name);
        if (t != null && t != parent) Object.DestroyImmediate(t.gameObject);
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image Panel_(RectTransform rt, Color c)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        return img;
    }

    static TextMeshProUGUI Label(string name, Transform parent, string text, float size,
                                 TextAlignmentOptions align, Color color,
                                 Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offMin; rt.offsetMax = offMax;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.alignment = align; t.color = color;
        t.textWrappingMode = TMPro.TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    // ------------------------------------------------------------- BUILD menu

    static void RebuildBuildMenu(List<Entry> entries)
    {
        var root = SafeRoot();
        if (root == null) { Debug.LogWarning("[BuildCatalogSetup] no Canvas."); return; }
        var bs = Object.FindFirstObjectByType<BuildSystem>();

        Kill(root, "BuildMenu");

        // Centred modal. The old 900x520 was sized back when the game still shipped
        // landscape through a portrait-referenced scaler; the game is portrait now
        // (batch 2), and at 520 tall this showed barely two rows of a twelve-item
        // catalog in the middle of a 1920-tall screen. Matched to the shop sheet's
        // footprint so the two read as one system.
        const float MenuW = 1000f, MenuH = 1150f, HeadH = 96f, ToolH = 110f;
        var menu = Overlay("BuildMenu", root, out var card, MenuW, MenuH);
        card.anchoredPosition = new Vector2(0f, 40f);   // clear of the joystick thumb zone

        // header strip
        var head = Rect("Header", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(MenuW, HeadH));
        Panel_(head, PanelSoft);
        Label("Title", head, "BUILD & MOVE", 42f, TextAlignmentOptions.Left, Accent,
              Vector2.zero, Vector2.one, new Vector2(28f, 0f), new Vector2(-96f, 0f))
            .fontStyle = FontStyles.Bold;

        var closeRT = Rect("Close", head, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(64f, 64f));
        Panel_(closeRT, new Color(0.32f, 0.20f, 0.18f, 1f));
        Label("X", closeRT, "×", 52f, TextAlignmentOptions.Center, Ink, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var closeBtn = closeRT.gameObject.AddComponent<Button>();

        // Toolbar: MOVE used to be a separate slab floating on the HUD next to
        // BUILD. It belongs here — it acts on things you placed from this menu, and
        // two permanent buttons for one workflow was clutter.
        var tool = Rect("Toolbar", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -HeadH), new Vector2(MenuW, ToolH));
        var moveRT = Rect("MoveBtn", tool, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          Vector2.zero, new Vector2(MenuW - 44f, ToolH - 22f));
        Panel_(moveRT, Slate);
        Round(moveRT.GetComponent<Image>());
        var moveBtn = moveRT.gameObject.AddComponent<Button>();
        Label("Text", moveRT, "MOVE A STRUCTURE", 34f, TextAlignmentOptions.Center, Ink,
              Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).fontStyle = FontStyles.Bold;

        // scroll view — 12 cards don't fit a phone screen, and the catalog will grow
        var viewport = Rect("Viewport", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -HeadH - ToolH), new Vector2(MenuW, MenuH - HeadH - ToolH));
        var vpImg = viewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.001f);   // invisible, but still catches the drag
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = Rect("Content", viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(MenuW, 0f));
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        // 3 columns across 1000: 20 padding each side + 2x18 spacing leaves 924/3 = 308.
        grid.cellSize = new Vector2(308f, 190f);
        grid.spacing = new Vector2(18f, 18f);
        grid.padding = new RectOffset(20, 20, 18, 18);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = card.gameObject.AddComponent<ScrollRect>();
        scroll.content = content; scroll.viewport = viewport;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            // Rim + inset fill, matching the shop tiles so both sheets read alike.
            var rimRT = Rect($"Card_{e.name}", content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Round(Panel_(rimRT, CardRim));
            var cardRT = Rect("Fill", rimRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            cardRT.anchorMin = Vector2.zero; cardRT.anchorMax = Vector2.one;
            cardRT.offsetMin = new Vector2(2f, 2f); cardRT.offsetMax = new Vector2(-2f, -2f);
            Round(Panel_(cardRT, CardFill));

            Label("Name", cardRT, e.name, 32f, TextAlignmentOptions.Center, Ink,
                  new Vector2(0f, 0.42f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-10f, -12f))
                .fontStyle = FontStyles.Bold;
            // A capped buildable says so on the card, or the only way to discover the
            // limit is to be refused at the moment you try to place the fourth one.
            string sub = e.maxCount > 0 ? $"{e.cost} coins   max {e.maxCount}" : $"{e.cost} coins";
            Label("Cost", cardRT, sub, 26f, TextAlignmentOptions.Center, Accent,
                  new Vector2(0f, 0f), new Vector2(1f, 0.42f), new Vector2(10f, 10f), new Vector2(-10f, 0f));

            var btn = cardRT.gameObject.AddComponent<Button>();
            var colors = btn.colors; colors.highlightedColor = AccentDim; colors.pressedColor = AccentDim; btn.colors = colors;
            if (bs != null)
            {
                int index = i;   // captured per card; BuildSystem.Place takes the catalog index
                UnityEventTools.AddIntPersistentListener(btn.onClick, new UnityEngine.Events.UnityAction<int>(bs.Place), index);
                // Close the sheet so the armed ghost is actually visible to position.
                UnityEventTools.AddBoolPersistentListener(btn.onClick,
                    new UnityEngine.Events.UnityAction<bool>(menu.gameObject.SetActive), false);
            }
        }

        // close button hides the whole menu
        UnityEventTools.AddBoolPersistentListener(closeBtn.onClick,
            new UnityEngine.Events.UnityAction<bool>(menu.gameObject.SetActive), false);

        // MOVE: arm move-mode, then close the sheet so the player can actually see
        // the structure they are dragging around.
        if (bs != null)
        {
            UnityEventTools.AddPersistentListener(moveBtn.onClick,
                new UnityEngine.Events.UnityAction(bs.ToggleMove));
            UnityEventTools.AddBoolPersistentListener(moveBtn.onClick,
                new UnityEngine.Events.UnityAction<bool>(menu.gameObject.SetActive), false);
        }

        menu.gameObject.SetActive(false);
    }

    // --------------------------------------------------------------- shop panel

    // The loot pack ships as plain Default textures, so LoadAssetAtPath<Sprite> returns
    // null and every icon silently falls back to a grey square. Flip the importer to
    // Sprite on first use. Point filtering + no compression, because this is pixel art
    // and bilinear turns it to mush at 120px.
    static Sprite LoadIcon(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null) return sprite;

        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // The BUY green. Kept from the hand-built panel — the user likes the colour; it
    // was the PROPORTIONS that were wrong.
    static readonly Color Buy = new Color(0.36f, 0.66f, 0.36f, 1f);
    // Card fill is the ORIGINAL panel's value, read back out of git; the rim is a
    // lighter shade of it so each tile has a visible edge.
    static readonly Color CardFill = new Color(0.23f, 0.19f, 0.16f, 1f);
    static readonly Color CardRim = new Color(0.35f, 0.29f, 0.24f, 1f);

    // Per-upgrade chip tints, applied at 0.2 alpha behind the glyph — same treatment
    // the original panel used.
    static readonly Color TintWood = new Color(0.82f, 0.54f, 0.24f);
    static readonly Color TintStone = new Color(0.62f, 0.66f, 0.72f);
    static readonly Color TintBag = new Color(0.45f, 0.73f, 0.38f);
    static readonly Color TintSpeed = new Color(0.35f, 0.66f, 0.90f);
    static readonly Color TintWeapon = new Color(0.85f, 0.38f, 0.32f);
    static readonly Color TintCoin = new Color(0.95f, 0.78f, 0.35f);
    static readonly Color TintFire = new Color(1f, 0.55f, 0.18f);

    // The old ShopPanel was hand-placed in the scene and had drifted: a slab pinned
    // low that ate the bottom ~60% of a portrait screen, sitting off-centre with its
    // cards crowded against one edge. Rebuilt here so it obeys the same geometry as
    // every other sheet — a centred card of a fixed size, header strip, even 2-column
    // grid, one full-width action bar at the foot.
    //
    // No scrim, unlike BUILD/SETTINGS: the shop opens because you WALKED here and
    // closes when you walk away, so the world has to stay visible and the joystick has
    // to stay live underneath.
    static void RebuildShopPanel()
    {
        var root = SafeRoot();
        if (root == null) return;
        var shop = Object.FindFirstObjectByType<Shop>();
        if (shop == null) { Debug.LogWarning("[BuildCatalogSetup] no Shop in scene — panel skipped."); return; }

        Kill(root, "ShopPanel");

        // Card geometry, in the 1080x1920 portrait design space. Sized off the user's
        // marked-up screenshot: the sheet should fill from roughly a fifth down the
        // screen to three-quarters down, near the full canvas width — the first pass
        // at 960x756 read as a small box adrift in the middle of the screen.
        const float PW = 1000f, HeadH = 104f, Pad = 22f, Gap = 20f;
        const float CellW = (PW - Pad * 2f - Gap) / 2f;      // 468
        const float CellH = 332f;
        const float GridH = CellH * 3f + Gap * 2f;           // 5 upgrade tiles + sell tile
        const float PH = HeadH + Pad + GridH + Pad;          // 1180

        // Nudged slightly ABOVE centre so the sheet clears the joystick thumb zone at
        // the bottom of a tall phone.
        var panel = Rect("ShopPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                         new Vector2(0f, 40f), new Vector2(PW, PH));
        Round(Panel_(panel, Panel));
        panel.gameObject.AddComponent<PanelPop>();

        // header: title left, walk-away hint right
        var head = Rect("Header", panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        Vector2.zero, new Vector2(PW, HeadH));
        Panel_(head, PanelSoft);
        Label("Title", head, "UPGRADES", 42f, TextAlignmentOptions.Left, Accent,
              Vector2.zero, Vector2.one, new Vector2(Pad + 8f, 0f), new Vector2(-340f, 0f))
            .fontStyle = FontStyles.Bold;
        Label("Hint", head, "walk away to close", 26f, TextAlignmentOptions.Right, InkDim,
              Vector2.zero, Vector2.one, new Vector2(PW - 340f, 0f), new Vector2(-Pad - 8f, 0f));

        // Six tiles on a fixed 2x3 grid, hand-placed rather than GridLayoutGroup —
        // the count is fixed at five upgrades plus the sell tile, and auto-layout has
        // bitten this project before (see the HUD [TRAP]).
        // A tile is TWO rounded rects: a lighter rim, and the fill inset 2px inside it.
        // That 2px edge is what made the original cards read as raised cards rather
        // than dark rectangles lost against a dark panel (user: "mine look flat").
        // Cheaper and sharper than a UI Outline component, which just re-draws the
        // sprite at an offset and smears at the corners.
        RectTransform Tile(int slot)
        {
            float x = Pad + (slot % 2) * (CellW + Gap);
            float y = -HeadH - Pad - (slot / 2) * (CellH + Gap);
            var rim = Rect($"Tile{slot}", panel, new Vector2(0f, 1f), new Vector2(0f, 1f),
                           new Vector2(x, y), new Vector2(CellW, CellH));
            Round(Panel_(rim, CardRim));

            var fill = Rect("Fill", rim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(2f, 2f); fill.offsetMax = new Vector2(-2f, -2f);
            Round(Panel_(fill, CardFill));
            return fill;   // children hang off the fill, so the rim is never covered
        }

        // Circular tinted chip with a flat icon on it — the ORIGINAL panel's treatment,
        // restored. The batch-12 pass hung pixel-art loot sprites here instead and they
        // read as cheap against the flat low-poly UI (user: "icons suck").
        // Art is Assets/UI/Generated, the project's own flat icon set: a white circle
        // for the chip plus solid-colour glyphs. Anything that set has no glyph for
        // (weapon, coins) is drawn from rects, the way the settings and build glyphs are.
        void Chip_(RectTransform tile, string iconAsset, Color tint)
        {
            var chip = Rect("Chip", tile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -16f), new Vector2(104f, 104f));
            var chipImg = Panel_(chip, new Color(tint.r, tint.g, tint.b, 0.2f));
            var circle = LoadIcon("Assets/UI/Generated/circle.png");
            if (circle != null) chipImg.sprite = circle; else Round(chipImg);
            chipImg.raycastTarget = false;

            var iconRT = Rect("Icon", chip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                              Vector2.zero, Vector2.zero);
            iconRT.anchorMin = Vector2.zero; iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = new Vector2(22f, 22f); iconRT.offsetMax = new Vector2(-22f, -22f);
            var img = iconRT.gameObject.AddComponent<Image>();
            img.raycastTarget = false;          // the BUY button owns the touch
            img.preserveAspect = true;

            if (iconAsset == "weapon") { Object.DestroyImmediate(img); SpearGlyph(iconRT, tint); return; }
            if (iconAsset == "coins") { Object.DestroyImmediate(img); CoinGlyph(iconRT, tint); return; }

            var sprite = LoadIcon($"Assets/UI/Generated/{iconAsset}.png");
            if (sprite != null) img.sprite = sprite;
            else { img.color = tint; Debug.LogWarning($"[BuildCatalogSetup] missing icon {iconAsset}"); }
        }

        // One upgrade card: icon, name+level, price, BUY. Returns the pieces Shop needs.
        // Vertical budget in a 332-tall tile, measured from the top:
        //   14..134 chip | 138..190 name | 190..234 cost | 238..314 button
        Button Card(int slot, string title, string iconAsset, Color tint, out TextMeshProUGUI label, out TextMeshProUGUI cost)
        {
            var tile = Tile(slot);
            const float BtnH = 76f;
            Chip_(tile, iconAsset, tint);

            label = Label("Name", tile, title, 34f, TextAlignmentOptions.Center, Ink,
                          new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -190f), new Vector2(-10f, -138f));
            label.textWrappingMode = TMPro.TextWrappingModes.Normal;   // Paint() puts "Lv N" on a second line
            label.fontStyle = FontStyles.Bold;

            cost = Label("Cost", tile, "0", 32f, TextAlignmentOptions.Center, Accent,
                         new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -234f), new Vector2(-10f, -190f));
            cost.fontStyle = FontStyles.Bold;

            var btnRT = Rect("Buy", tile, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                             new Vector2(0f, 18f), new Vector2(CellW - 40f, BtnH));
            Panel_(btnRT, Buy);
            Round(btnRT.GetComponent<Image>());
            Label("Text", btnRT, "BUY", 32f, TextAlignmentOptions.Center, new Color(0.08f, 0.14f, 0.08f),
                  Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).fontStyle = FontStyles.Bold;
            var b = btnRT.gameObject.AddComponent<Button>();
            b.targetGraphic = btnRT.GetComponent<Image>();
            var cols = b.colors;
            cols.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cols.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);   // greyed when unaffordable
            b.colors = cols;
            return b;
        }

        // The tool icons show WHAT THE TOOL HARVESTS — axe/logs, pickaxe/stone — which
        // is what the original panel did and it reads instantly. Recovered by pulling
        // the pre-session ShopPanel back out of git rather than guessing.
        var axeBtn = Card(0, "Axe", "icon_wood", TintWood, out var axeLabel, out var axeCost);
        var pickBtn = Card(1, "Pickaxe", "icon_stone", TintStone, out var pickLabel, out var pickCost);
        var bagBtn = Card(2, "Bag", "icon_bag", TintBag, out var bagLabel, out var bagCost);
        var spdBtn = Card(3, "Speed", "icon_speed", TintSpeed, out var spdLabel, out var spdCost);
        var wpnBtn = Card(4, "Weapon", "weapon", TintWeapon, out var wpnLabel, out var wpnCost);

        // Sell tile takes the sixth slot — same footprint as a card so the grid stays
        // even, but amber and captioned, because selling is the opposite action to buying.
        var sellTile = Tile(5);
        Chip_(sellTile, "coins", TintCoin);
        Label("Caption", sellTile, "Turn your haul into coins", 26f, TextAlignmentOptions.Center, InkDim,
              new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -222f), new Vector2(-14f, -142f))
            .textWrappingMode = TMPro.TextWrappingModes.Normal;
        var sellRT = Rect("Sell", sellTile, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                          new Vector2(0f, 18f), new Vector2(CellW - 40f, 76f));
        Panel_(sellRT, Accent);
        Round(sellRT.GetComponent<Image>());
        var sellText = Label("Text", sellRT, "SELL ALL", 32f, TextAlignmentOptions.Center,
                             new Color(0.12f, 0.09f, 0.06f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        sellText.fontStyle = FontStyles.Bold;
        var sellBtn = sellRT.gameObject.AddComponent<Button>();
        sellBtn.targetGraphic = sellRT.GetComponent<Image>();
        var sc = sellBtn.colors;
        sc.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        sc.disabledColor = new Color(0.45f, 0.42f, 0.36f, 1f);
        sellBtn.colors = sc;

        // --- hand every piece back to Shop.cs ---
        shop.panel = panel.gameObject;
        shop.axeLabel = axeLabel; shop.pickaxeLabel = pickLabel; shop.capacityLabel = bagLabel;
        shop.speedLabel = spdLabel; shop.weaponLabel = wpnLabel;
        shop.axeCostText = axeCost; shop.pickaxeCostText = pickCost; shop.capacityCostText = bagCost;
        shop.speedCostText = spdCost; shop.weaponCostText = wpnCost;
        shop.axeButton = axeBtn; shop.pickaxeButton = pickBtn; shop.capacityButton = bagBtn;
        shop.speedButton = spdBtn; shop.weaponButton = wpnBtn;
        shop.sellButton = sellBtn; shop.sellButtonText = sellText;
        shop.playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        shop.floatingTextPrefab = AssetDatabase.LoadAssetAtPath<FloatingText>($"{PrefabDir}/FloatingText.prefab");

        UnityEventTools.AddPersistentListener(axeBtn.onClick, new UnityEngine.Events.UnityAction(shop.BuyAxe));
        UnityEventTools.AddPersistentListener(pickBtn.onClick, new UnityEngine.Events.UnityAction(shop.BuyPickaxe));
        UnityEventTools.AddPersistentListener(bagBtn.onClick, new UnityEngine.Events.UnityAction(shop.BuyCapacity));
        UnityEventTools.AddPersistentListener(spdBtn.onClick, new UnityEngine.Events.UnityAction(shop.BuySpeed));
        UnityEventTools.AddPersistentListener(wpnBtn.onClick, new UnityEngine.Events.UnityAction(shop.BuyWeapon));
        UnityEventTools.AddPersistentListener(sellBtn.onClick, new UnityEngine.Events.UnityAction(shop.SellAll));

        EditorUtility.SetDirty(shop);
        panel.gameObject.SetActive(false);   // Shop opens it on walk-in
    }

    // --------------------------------------------------------- build entry point

    // One icon, bottom-right. BUILD and MOVE used to be two permanent text slabs
    // stacked on the HUD for what is really a single workflow; MOVE now lives
    // inside the sheet this opens.
    static void RebuildToggles()
    {
        var root = SafeRoot();
        if (root == null) return;
        var menu = FindDeep(root, "BuildMenu");

        Kill(root, "BuildToggle");
        Kill(root, "MoveToggle");
        // TycoonSetup used to parent its standalone MOVE button to the CANVAS, not to
        // SafeAreaRoot, so the Kill above never reached it and every re-run of that tool
        // left a second button sitting on top of this one. The builder is gone now, but
        // sweep by global name too so an already-saved scene gets cleaned.
        var stray = GameObject.Find("MoveToggle");
        if (stray != null) Object.DestroyImmediate(stray);

        var build = IconButton(root, "BuildToggle", new Vector2(1f, 0f),
                               new Vector2(-24f, 36f), 92f, Accent, out var buildBtn);
        BuildGlyph(build, new Color(0.12f, 0.09f, 0.06f));   // dark bricks on amber
        // The bare brick glyph was being read as a "move" button. Caption it.
        var cap = Label("Caption", build, "BUILD", 22f, TextAlignmentOptions.Center, Ink,
                        new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        cap.rectTransform.pivot = new Vector2(0.5f, 0f);
        cap.rectTransform.anchoredPosition = new Vector2(0f, 6f);   // sits above the button
        cap.rectTransform.sizeDelta = new Vector2(120f, 28f);
        cap.fontStyle = FontStyles.Bold;
        cap.outlineColor = new Color32(0, 0, 0, 220);
        cap.outlineWidth = 0.22f;                                   // legible over any terrain
        cap.raycastTarget = false;

        if (menu != null)
        {
            var toggle = build.gameObject.AddComponent<UIToggle>();
            toggle.target = menu.gameObject;
            UnityEventTools.AddPersistentListener(buildBtn.onClick,
                new UnityEngine.Events.UnityAction(toggle.Toggle));
        }
    }

    // -------------------------------------------------- pause menu + win screen

    // A phone game with no pause and no mute. This builds both, and takes the
    // chance to move the destructive "New Game" button off the HUD: it used to be a
    // full-size red slab in the top-right corner, one stray tap from wiping a run.
    // It now lives behind a deliberate tap on the settings icon, which is where a
    // reset belongs.
    static void RebuildPauseAndVictory()
    {
        var root = SafeRoot();
        if (root == null) return;

        Kill(root, "ResetBtn");        // the old corner slab
        Kill(root, "MenuToggle");
        Kill(root, "PausePanel");
        Kill(root, "VictoryPanel");
        Kill(root, "IntroPanel");
        Kill(root, "TitlePanel");

        var gm = GameObject.Find("GameManager");

        // --- settings icon, top-right where the reset button used to sit ---
        var toggleRT = IconButton(root, "MenuToggle", new Vector2(1f, 1f),
                                  new Vector2(-24f, -24f), 92f, PanelSoft, out var toggleBtn);
        SettingsGlyph(toggleRT, Ink);

        // --- the settings sheet ---
        // Three fat full-width colour slabs read like an ad interstitial, not a
        // menu. This is built as a settings sheet instead: titled header with a
        // close X, a labelled row carrying its control on the right, and the one
        // destructive action fenced off in its own footer under a rule.
        const float PW = 620f, PH = 400f, Pad = 28f, RowH = 96f, BtnH = 84f;
        var pause = Overlay("PausePanel", root, out var pauseCard, PW, PH);

        // header strip
        var head = Rect("Header", pauseCard, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        Vector2.zero, new Vector2(PW, 92f));
        Panel_(head, PanelSoft);
        Label("Title", head, "SETTINGS", 36f, TextAlignmentOptions.Left, Accent,
              Vector2.zero, Vector2.one, new Vector2(Pad, 0f), new Vector2(-92f, 0f));

        var closeRT = Rect("CloseBtn", head, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                           new Vector2(-14f, 0f), new Vector2(60f, 60f));
        Panel_(closeRT, new Color(0.32f, 0.20f, 0.18f, 1f));
        Round(closeRT.GetComponent<Image>());
        Label("X", closeRT, "×", 46f, TextAlignmentOptions.Center, Ink, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var closeBtn = closeRT.gameObject.AddComponent<Button>();

        // --- Sound row: label left, switch right ---
        var soundRow = Rect("SoundRow", pauseCard, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -92f), new Vector2(PW, RowH));
        Label("Label", soundRow, "Sound", 34f, TextAlignmentOptions.Left, Ink,
              Vector2.zero, Vector2.one, new Vector2(Pad, 0f), new Vector2(-190f, 0f));

        var track = Rect("SoundTrack", soundRow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                         new Vector2(-Pad, 0f), new Vector2(112f, 56f));
        var trackImg = Panel_(track, Accent);
        Round(trackImg);
        var knob = Rect("Knob", track, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(26f, 0f), new Vector2(46f, 46f));
        Panel_(knob, Ink);
        Round(knob.GetComponent<Image>());
        // Label sits clear of the 112px track (which spans -140..-28 from the right
        // edge). Label() leaves the pivot centred, so this is a centre position —
        // -190 with a 76 width ends at -152, twelve px shy of the track.
        var soundState = Label("State", soundRow, "ON", 26f, TextAlignmentOptions.Right, InkDim,
                               new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
        soundState.rectTransform.anchoredPosition = new Vector2(-190f, 0f);
        soundState.rectTransform.sizeDelta = new Vector2(76f, 40f);
        // The whole row is the hit target — a 112px switch is a mean thing to poke.
        var soundBtn = soundRow.gameObject.AddComponent<Button>();
        Panel_(soundRow, new Color(1f, 1f, 1f, 0f)).raycastTarget = true;   // invisible, still tappable
        soundBtn.targetGraphic = soundRow.GetComponent<Image>();

        Rule(pauseCard, -92f - RowH, PW - Pad * 2f);

        // --- footer: the one destructive action, fenced off ---
        Label("DangerCap", pauseCard, "Wipes your saved progress.", 24f, TextAlignmentOptions.Center, InkDim,
              new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(Pad, -268f), new Vector2(-Pad, -230f));
        var reset = MenuButton(pauseCard, "ResetBtn", "New Game", new Color(0.42f, 0.19f, 0.17f, 1f),
                               -284f, PW - Pad * 2f, BtnH);
        reset.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0.95f, 0.55f, 0.50f);

        // ResetButton wires its own onClick in Awake and drives its own label text.
        var rb = reset.gameObject.AddComponent<ResetButton>();
        rb.label = reset.GetComponentInChildren<TextMeshProUGUI>();

        // --- the win screen ---
        var victory = Overlay("VictoryPanel", root, out var winCard, 640f, 420f);

        Label("Title", winCard, "VALLEY MASTERED", 46f, TextAlignmentOptions.Center, Accent,
              new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -110f), new Vector2(0f, -36f));
        Label("Body", winCard, "Every objective complete.\nThe camp is yours — keep building.",
              30f, TextAlignmentOptions.Center, Ink,
              new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 130f), new Vector2(-24f, -130f))
            .textWrappingMode = TMPro.TextWrappingModes.Normal;
        var cont = MenuButton(winCard, "ContinueBtn", "CONTINUE", Accent, -318f, 640f - Pad * 2f, BtnH);

        // --- first-run how-to-play card ---
        // A new player lands in an open map with a joystick and no idea the goal is
        // to walk to a tree and stand still. Shown once (IntroTutorial gates on
        // PlayerPrefs), so it is the ONLY chance to explain anything.
        //
        // It used to be four sentences in one centred blob. It is now one row per
        // idea, each with the same icon the player will meet in the UI, so the card
        // doubles as a legend: the log icon here is the log icon on the Axe card.
        // Six rows is the ceiling — past that nobody reads it.
        // IntroRowH, not RowH — the settings sheet above already owns that name.
        //
        // SIZING: this card is read once, by someone who has never seen the game, on a
        // phone held at arm's length — so it is deliberately the largest panel in the
        // project. The first pass was 780x886 with 27pt body text and read as a small
        // box adrift in a lot of empty screen (user, 2026-07-23).
        //
        // Design space is 1080x1920 and the CanvasScaler matches on WIDTH (match=0), so
        // the real pixel size of a glyph is fontSize * (deviceWidth / 1080). At 40pt on
        // a 1179-wide phone that lands at ~44px, which is roughly the 16sp Android body
        // default — 27pt landed at ~29px, well under it, which is why it read as small.
        // Row height has to lead the font: the two longest lines wrap to 3 at this size.
        const float IW = 960f, IntroRowH = 168f, ChipD = 88f;
        const int Rows = 6;
        const float IntroTop = 158f;                       // below the title
        const float IntroBtnH = 104f;                      // fatter than the settings BtnH — it is the only tap target here
        const float IH = IntroTop + IntroRowH * Rows + 24f + IntroBtnH + Pad;   // 1322
        var intro = Overlay("IntroPanel", root, out var introCard, IW, IH);
        Label("Title", introCard, "WELCOME", 64f, TextAlignmentOptions.Center, Accent,
              new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -130f), new Vector2(0f, -42f))
            .fontStyle = FontStyles.Bold;

        // One row: tinted circular chip on the left, text on the right. `glyph` draws
        // into the chip when there is no sprite for the idea.
        void IntroRow(int slot, string iconAsset, Color tint, string text, System.Action<Transform, Color> glyph)
        {
            float y = -IntroTop - slot * IntroRowH;
            var row = Rect($"Row{slot}", introCard, new Vector2(0f, 1f), new Vector2(0f, 1f),
                           new Vector2(Pad, y), new Vector2(IW - Pad * 2f, IntroRowH));

            var chip = Rect("Chip", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(6f, 0f), new Vector2(ChipD, ChipD));
            var chipImg = Panel_(chip, new Color(tint.r, tint.g, tint.b, 0.2f));
            var circle = LoadIcon("Assets/UI/Generated/circle.png");
            if (circle != null) chipImg.sprite = circle; else Round(chipImg);
            chipImg.raycastTarget = false;

            var iconRT = Rect("Icon", chip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            iconRT.anchorMin = Vector2.zero; iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = new Vector2(15f, 15f); iconRT.offsetMax = new Vector2(-15f, -15f);

            if (glyph != null)
            {
                // The rect glyphs are drawn at shop-chip scale (a ~68px icon area);
                // this chip's is ~58, so shrink the whole group rather than
                // re-authoring every rect at a second size.
                iconRT.localScale = Vector3.one * 0.86f;
                glyph(iconRT, tint);
            }
            else
            {
                var img = iconRT.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
                var sprite = LoadIcon($"Assets/UI/Generated/{iconAsset}.png");
                if (sprite != null) img.sprite = sprite; else img.color = tint;
            }

            Label("Text", row, text, 40f, TextAlignmentOptions.Left, Ink,
                  new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(ChipD + 26f, 6f), new Vector2(-6f, -6f))
                .textWrappingMode = TMPro.TextWrappingModes.Normal;
        }

        IntroRow(0, "icon_speed", TintSpeed, "Drag anywhere to move.", null);
        // Rows 1 and 5 both say "face it" on purpose: targeting is a facing cone now
        // (FacingCheck), and a player who doesn't know that reads the dead swing as a
        // broken game rather than as aiming.
        IntroRow(1, "icon_wood", TintWood, "Face a tree or rock, stand still, and you gather it.", null);
        IntroRow(2, null, TintCoin, "Visit the market to sell your haul and buy better tools.", CoinGlyph);
        IntroRow(3, null, Accent, "Tap BUILD, bottom right, to place fences, walls and campfires.", BuildGlyph);
        IntroRow(4, null, TintFire, "A campfire heals you, scares animals off, and is where you respawn. Three at most.", FlameGlyph);
        IntroRow(5, null, TintWeapon, "Face an animal and stand still to swing. Predators arrive in a few minutes — build before they do.", SpearGlyph);

        var gotit = MenuButton(introCard, "GotItBtn", "GOT IT", Accent,
                               -(IH - Pad - IntroBtnH), IW - Pad * 2f, IntroBtnH, 44f);

        // --- title screen ---
        // Full-bleed, not an Overlay() card: this is the first thing anyone sees, and a
        // small box floating on the map reads as a popup rather than as a front door.
        // It is still a panel over the live scene — TitleScreen pins timeScale to 0 —
        // which buys a title without a second scene, a loader or a second build target.
        //
        // Everything hangs off a CENTRED container, not off the top edge. The scaler
        // matches on width (design 1080), so design-space height is deviceHeight/scale
        // — 2341 on a 1179x2556 phone, not 1920. Anything measured down from the top
        // pins to the top and dumps every extra unit of a taller screen into the
        // bottom of the frame; the first pass did exactly that and left the lower
        // third empty. Anchored at the centre, the slack splits evenly instead.
        const float TitleBtnW = 620f, PlayH = 132f, NewH = 96f;
        const float ContentH = 760f;
        var title = Rect("TitlePanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                         Vector2.zero, Vector2.zero);
        Stretch(title);
        // Near-opaque, unlike the 0.66 modal scrim: the valley should read as a
        // backdrop, not as a game already running that the player is missing.
        Panel_(title, new Color(0.06f, 0.05f, 0.04f, 0.9f));

        var content = Rect("Content", title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                           Vector2.zero, new Vector2(1080f, ContentH));

        // Offsets below are measured down from the container's own top edge.
        Label("Title", content, "SURVIVAL", 132f, TextAlignmentOptions.Center, Accent,
              new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -200f), new Vector2(0f, 0f));
        Label("Tagline", content, "Chop. Sell. Upgrade. Survive.", 36f, TextAlignmentOptions.Center, InkDim,
              new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -282f), new Vector2(0f, -218f));

        var playRT = MenuButton(content, "PlayBtn", "PLAY", Accent, -460f, TitleBtnW, PlayH, 52f);
        var newRT = MenuButton(content, "NewGameBtn", "New Game", new Color(0.42f, 0.19f, 0.17f, 1f),
                               -460f - PlayH - 20f, TitleBtnW, NewH, 34f);
        var newLabel = newRT.GetComponentInChildren<TextMeshProUGUI>();
        newLabel.color = new Color(0.95f, 0.55f, 0.50f);
        // Same guarded two-tap wipe the settings sheet uses — it wires its own onClick
        // and drives its own label, so there is nothing to hook up here.
        newRT.gameObject.AddComponent<ResetButton>().label = newLabel;

        // --- components + wiring ---
        if (gm != null)
        {
            var pm = gm.GetComponent<PauseMenu>();
            if (pm == null) pm = gm.AddComponent<PauseMenu>();
            pm.panel = pause.gameObject;
            pm.soundLabel = soundState;
            pm.soundTrack = trackImg;
            pm.soundKnob = knob;
            pm.knobOnX = 26f;
            pm.knobOffX = -26f;
            pm.trackOn = Accent;
            pm.trackOff = Chip;

            UnityEventTools.AddPersistentListener(toggleBtn.onClick, new UnityEngine.Events.UnityAction(pm.Toggle));
            UnityEventTools.AddPersistentListener(closeBtn.onClick, new UnityEngine.Events.UnityAction(pm.Resume));
            UnityEventTools.AddPersistentListener(soundBtn.onClick, new UnityEngine.Events.UnityAction(pm.ToggleMute));

            var vp = gm.GetComponent<VictoryPanel>();
            if (vp == null) vp = gm.AddComponent<VictoryPanel>();
            vp.panel = victory.gameObject;
            vp.objectives = Object.FindFirstObjectByType<ObjectiveManager>();
            UnityEventTools.AddPersistentListener(cont.GetComponent<Button>().onClick, new UnityEngine.Events.UnityAction(vp.Dismiss));

            var it = gm.GetComponent<IntroTutorial>();
            if (it == null) it = gm.AddComponent<IntroTutorial>();
            it.panel = intro.gameObject;
            UnityEventTools.AddPersistentListener(gotit.GetComponent<Button>().onClick, new UnityEngine.Events.UnityAction(it.Dismiss));

            var ts = gm.GetComponent<TitleScreen>();
            if (ts == null) ts = gm.AddComponent<TitleScreen>();
            ts.panel = title.gameObject;
            ts.playLabel = playRT.GetComponentInChildren<TextMeshProUGUI>();
            ts.newGameBtn = newRT.gameObject;
            ts.intro = it;                       // the title defers the how-to-play card until PLAY
            ts.save = Object.FindFirstObjectByType<SaveManager>();
            // Everything that must not be on screen behind the title. Looked up by
            // name because these are built by other passes (CompactHud, RebuildToggles,
            // RebuildObjectiveBanner) that have already run by now.
            var chrome = new List<GameObject>();
            foreach (string n in new[] { "HUDPanel", "BuildToggle", "MoveToggle", "MenuToggle", "ObjectiveBanner" })
            {
                var t = FindDeep(root, n);
                if (t != null) chrome.Add(t.gameObject);
            }
            ts.hideWhileShown = chrome.ToArray();
            UnityEventTools.AddPersistentListener(playRT.GetComponent<Button>().onClick, new UnityEngine.Events.UnityAction(ts.Play));

            EditorUtility.SetDirty(gm);
        }
        else Debug.LogWarning("[BuildCatalogSetup] no GameManager — pause/victory/intro not wired.");

        // All start hidden; their components decide what to show at runtime
        // (IntroTutorial re-enables its panel on a first run).
        pause.gameObject.SetActive(false);
        victory.gameObject.SetActive(false);
        intro.gameObject.SetActive(false);
        title.gameObject.SetActive(false);   // TitleScreen.Start re-enables it on every boot
    }

    // --- procedural glyphs -------------------------------------------------
    // The only icon art in the project is a pixel-art loot pack (gems, fish,
    // barrels) — no gear, no hammer, and that style would fight the flat UI. These
    // draw from plain rects instead, so they scale cleanly and match the panels.

    static RectTransform Glyph(Transform parent, string name, Vector2 pos, Vector2 size, Color c)
    {
        var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var img = Panel_(rt, c);
        img.sprite = RectSprite;
        img.raycastTarget = false;      // the button underneath owns the touch
        return rt;
    }

    // Sliders: three rails with offset knobs. Reads as "settings" without needing
    // a gear, which is a miserable shape to build out of rectangles.
    static void SettingsGlyph(Transform parent, Color c)
    {
        float[] rowY = { 18f, 0f, -18f };
        float[] knobX = { -8f, 12f, -2f };
        for (int i = 0; i < 3; i++)
        {
            Glyph(parent, "Rail" + i, new Vector2(0f, rowY[i]), new Vector2(44f, 4f), c);
            Glyph(parent, "Knob" + i, new Vector2(knobX[i], rowY[i]), new Vector2(10f, 16f), c);
        }
    }

    // Assets/UI/Generated has flat glyphs for wood, stone, bag and speed but nothing
    // for the weapon or for coins, so those two are drawn here in the same solid-shape
    // style rather than importing a mismatched sprite.

    // Spear: a shaft with a diamond head. Reads as "weapon" at 60px in a way an axe
    // doesn't (the axe glyph would collide with the Axe upgrade's log icon).
    static void SpearGlyph(Transform parent, Color c)
    {
        Glyph(parent, "Shaft", new Vector2(0f, -6f), new Vector2(7f, 44f), c);
        // Head, built from two rotated squares so it comes to a point.
        var a = Glyph(parent, "HeadA", new Vector2(0f, 20f), new Vector2(19f, 19f), c);
        a.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var b = Glyph(parent, "Cross", new Vector2(0f, 4f), new Vector2(24f, 6f), c);
        b.localRotation = Quaternion.Euler(0f, 0f, 0f);
    }

    // Coin stack: three squashed circles. Uses the same circle sprite as the chip, so
    // it stays perfectly round at any size instead of going blocky like a rect would.
    static void CoinGlyph(Transform parent, Color c)
    {
        var circle = LoadIcon("Assets/UI/Generated/circle.png");
        float[] y = { -18f, 0f, 18f };
        for (int i = 0; i < 3; i++)
        {
            var rt = Rect("Coin" + i, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          new Vector2(0f, y[i]), new Vector2(46f, 20f));
            var img = Panel_(rt, i == 1 ? c : c * 0.86f);
            if (circle != null) img.sprite = circle;
            img.raycastTarget = false;
        }
    }

    // A flame over two crossed logs — the campfire, for the intro card. Built from a
    // rotated square with its bottom corners squared off by a second rect, which is
    // enough of a teardrop to read as fire at this size.
    static void FlameGlyph(Transform parent, Color c)
    {
        var logL = Glyph(parent, "LogL", new Vector2(0f, -22f), new Vector2(34f, 7f), c * 0.62f);
        logL.localRotation = Quaternion.Euler(0f, 0f, 12f);
        var logR = Glyph(parent, "LogR", new Vector2(0f, -22f), new Vector2(34f, 7f), c * 0.62f);
        logR.localRotation = Quaternion.Euler(0f, 0f, -12f);

        var tip = Glyph(parent, "Tip", new Vector2(0f, 10f), new Vector2(23f, 23f), c);
        tip.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Glyph(parent, "Base", new Vector2(0f, -6f), new Vector2(26f, 18f), c);
        // Hot core, so the flame isn't one flat colour.
        Glyph(parent, "Core", new Vector2(0f, -4f), new Vector2(11f, 14f), Color.Lerp(c, Color.white, 0.55f));
    }

    // A little brick wall — this game's build menu is fences and walls, so bricks
    // say "build" more directly than a hammer would.
    static void BuildGlyph(Transform parent, Color c)
    {
        Glyph(parent, "B0", new Vector2(-14f, 11f), new Vector2(26f, 16f), c);
        Glyph(parent, "B1", new Vector2(14f, 11f), new Vector2(26f, 16f), c);
        Glyph(parent, "B2", new Vector2(0f, -11f), new Vector2(26f, 16f), c);
        Glyph(parent, "B3", new Vector2(-25f, -11f), new Vector2(12f, 16f), c);
        Glyph(parent, "B4", new Vector2(25f, -11f), new Vector2(12f, 16f), c);
    }

    // Square icon button. Nothing but the glyph — no text label.
    static RectTransform IconButton(Transform parent, string name, Vector2 anchor, Vector2 pos,
                                    float size, Color bg, out Button btn)
    {
        var rt = Rect(name, parent, anchor, anchor, pos, new Vector2(size, size));
        Panel_(rt, bg);
        Round(rt.GetComponent<Image>());
        btn = rt.gameObject.AddComponent<Button>();
        var cols = btn.colors; cols.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); btn.colors = cols;
        return rt;
    }

    // Hairline separator between settings rows.
    static void Rule(Transform parent, float y, float w)
    {
        var rt = Rect("Rule", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(w, 2f));
        Panel_(rt, new Color(1f, 1f, 1f, 0.09f));
    }

    // A modal: full-screen dim scrim with a centred card on top. The scrim matters
    // for more than looks — without it the card floated over a fully visible HUD and
    // the game didn't read as stopped, and the live buttons underneath stayed
    // tappable. An opaque Image on the scrim eats those raycasts.
    // PanelPop goes on the CARD, not the root: scaling a full-screen scrim in from
    // 90% would show the world past its edges.
    static RectTransform Overlay(string name, Transform parent, out RectTransform card, float w, float h)
    {
        var rootRT = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(rootRT);

        var scrim = Rect("Scrim", rootRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(scrim);
        Panel_(scrim, new Color(0f, 0f, 0f, 0.66f));

        card = Rect("Card", rootRT, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
        var img = Panel_(card, new Color(0.13f, 0.11f, 0.10f, 1f));   // opaque: the card must be legible
        Round(img);
        card.gameObject.AddComponent<PanelPop>();
        return rootRT;
    }

    // Full-width button inside a menu card, measured down from the card's top edge.
    static RectTransform MenuButton(Transform parent, string name, string text, Color tint, float y, float w, float h,
                                    float fontSize = 32f)
    {
        var rt = Rect(name, parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(w, h));
        Panel_(rt, tint);
        Round(rt.GetComponent<Image>());
        var btn = rt.gameObject.AddComponent<Button>();
        var c = btn.colors; c.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); btn.colors = c;
        Label("Text", rt, text, fontSize, TextAlignmentOptions.Center,
              tint == Accent ? new Color(0.12f, 0.09f, 0.06f) : Ink,
              Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return rt;
    }

    // ------------------------------------------------------------ compact HUD

    // Design-space geometry. The canvas reference is 1080 wide, so these are also
    // roughly percentages: 380 wide is ~35% of the screen.
    const float HudX = 24f, HudY = -24f;
    const float HudW = 470f, HudPad = 18f;
    const float RowW = HudW - HudPad * 2f;   // 434
    const float BarH = 48f, PillH = 84f, HudGap = 12f;
    // Two bar rows (HP, BAG) — the FOOD bar went out with the hunger system.
    const float HudH = HudPad + BarH + HudGap + PillH * 2f + 6f + HudGap + BarH + HudPad;

    // The HUD was a 360x512 panel at scale 1.6 — 576x819 on screen, over half the
    // width and a third of the height. Same widgets, packed: bars carry their own
    // label and value inline, and the three resource pills sit in one row instead
    // of three stacked cards. Reparents nothing, so HUD.cs keeps its references.
    static void CompactHud()
    {
        var root = SafeRoot();
        if (root == null) return;
        var panel = FindDeep(root, "HUDPanel") as RectTransform;
        if (panel == null) { Debug.LogWarning("[BuildCatalogSetup] no HUDPanel."); return; }

        // The panel drove itself with a VerticalLayoutGroup + ContentSizeFitter, which
        // silently overwrote every rect set below and forced the old 512-tall stack.
        // Placement here is explicit, so the auto-layout has to go.
        StripLayout(panel, true);

        panel.localScale = Vector3.one;
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(HudX, HudY);
        panel.sizeDelta = new Vector2(HudW, HudH);

        // Rounded panel + a signature amber spine down the left edge — echoes the
        // objective tracker's spine so the HUD and quest strip read as one system.
        var panelImg = panel.GetComponent<Image>();
        if (panelImg != null) { panelImg.color = Panel; Round(panelImg); }
        Kill(panel, "HudSpine");
        Panel_(Rect("HudSpine", panel, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, 0f), new Vector2(6f, HudH)), Accent);

        // Hunger is gone, so the FOOD bar and the Food pill go with it. Kill any that
        // survive from an earlier run — this builder is re-runnable over a live scene.
        Kill(panel, "HungerGroup");
        Kill(panel, "FoodPill");

        float y = -HudPad;
        y = BarRow(panel, "HealthGroup", y, "HP") - HudGap;

        // Three pills, then two. Meat/Hide are carried but sell-only.
        var hud = Object.FindFirstObjectByType<HUD>();
        string[][] rows =
        {
            new[] { "WoodPill", "StonePill", "CoinsPill" },
            new[] { "MeatPill", "HidePill" },
        };
        var tint = new[] { new Color(0.82f, 0.35f, 0.30f), new Color(0.75f, 0.62f, 0.45f) };
        // Real Meat/Hide icons (repurposed from the loot pack — swap for bespoke
        // low-poly art when Unity AI generation is available). Null-safe: a missing
        // sprite just leaves the tinted-wood placeholder in place.
        var pillIcon = new[]
        {
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/1 Icons/Icons_17.png"),  // Meat — raw red flesh
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/1 Icons/Icons_15.png"),  // Hide — pelt
        };
        float pillW = (RowW - HudGap * 2f) / 3f;

        for (int r = 0; r < rows.Length; r++)
        {
            for (int i = 0; i < rows[r].Length; i++)
            {
                var p = FindDeep(panel, rows[r][i]) as RectTransform;
                if (p == null && r == 1) p = ClonePill(panel, FindDeep(panel, "WoodPill"), rows[r][i], tint[i]);
                if (p == null) continue;

                TopLeft(p, HudPad + i * (pillW + HudGap), y - r * (PillH + 6f), pillW, PillH);

                // Each pill is a rounded card; the icon sits on a slightly darker chip.
                var pImg = p.GetComponent<Image>();
                if (pImg != null) { pImg.color = PanelSoft; Round(pImg); }

                var chip = FindDeep(p, "Chip") as RectTransform;
                if (chip != null)
                {
                    TopLeft(chip, 8f, -8f, PillH - 16f, PillH - 16f);
                    var chipImg = chip.GetComponent<Image>();
                    if (chipImg != null) { chipImg.color = Chip; Round(chipImg); }
                }

                // Row 1 (Meat/Hide) gets its own icon; drop the wood-tint recolor.
                if (r == 1 && chip != null && pillIcon[i] != null)
                {
                    var iconT = FindDeep(chip, "Icon");
                    var iconImg = iconT != null ? iconT.GetComponent<Image>() : null;
                    if (iconImg != null) { iconImg.sprite = pillIcon[i]; iconImg.color = Color.white; }
                }

                var num = FindDeep(p, "Num") as RectTransform;
                if (num != null)
                {
                    TopLeft(num, PillH, -8f, pillW - PillH - 10f, PillH - 16f);
                    var t = num.GetComponent<TextMeshProUGUI>();
                    t.fontSize = 34f; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.MidlineRight;

                    if (hud != null && r == 1)
                    {
                        if (i == 0) hud.meatText = t;
                        else hud.hideText = t;
                    }
                }
            }
        }
        if (hud != null) EditorUtility.SetDirty(hud);
        y -= PillH * 2f + 6f + HudGap;

        BarRow(panel, "CapacityGroup", y, "BAG");

        // Distinct bar fills. MUST stay Image.Type.Filled — HUD.cs drives fillAmount
        // (HP drops when hit, BAG grows as you carry). Rounding them to Sliced
        // silently kills fillAmount (bar reads full always), so only the track behind
        // is rounded, never the fill.
        if (hud != null)
        {
            StyleFill(hud.healthBar, HpFill);
            StyleFill(hud.capacityBar, CarryFill);
        }
    }

    static void StyleFill(Image img, Color c)
    {
        if (img == null) return;
        img.color = c;
        // Must be a real square sprite — see RectSprite. A rounded sprite here gets
        // stretched into a capsule (Filled disables slicing), and a null sprite makes
        // Image ignore fillAmount and draw the bar permanently full.
        img.sprite = RectSprite;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    // One inline bar: full-width track with its name on the left and value on the
    // right, both drawn over the bar. Returns the y to continue from.
    static float BarRow(RectTransform panel, string groupName, float y, string title)
    {
        var g = FindDeep(panel, groupName) as RectTransform;
        if (g == null) return y - BarH;
        TopLeft(g, HudPad, y, RowW, BarH);

        var bg = FindDeep(g, "BarBG") as RectTransform;
        if (bg != null)
        {
            Stretch(bg);
            var bgImg = bg.GetComponent<Image>();
            if (bgImg != null) { bgImg.color = Track; Flat(bgImg); }
        }

        foreach (Transform child in g)
        {
            var t = child.GetComponent<TextMeshProUGUI>();
            if (t == null) continue;
            var rt = (RectTransform)child;
            bool isHeader = child.name == "Header";
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(isHeader ? 12f : 0f, 0f);
            rt.offsetMax = new Vector2(isHeader ? 0f : -12f, 0f);
            t.fontSize = 26f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = isHeader ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            // Text floats over a fill that can be red, orange OR the dark empty track —
            // no flat colour reads on all three. White + a dark outline always does.
            t.color = Ink;
            t.outlineColor = new Color32(0, 0, 0, 220);
            t.outlineWidth = 0.22f;
            if (isHeader) t.text = title;
            child.SetAsLastSibling();          // draw over the bar, not under it
        }
        return y - BarH;
    }

    // Copy an existing pill so the new ones inherit its sprite, chip and font setup.
    // Only the icon tint differs — there are no per-resource icon sprites yet.
    static RectTransform ClonePill(Transform panel, Transform source, string name, Color iconTint)
    {
        if (source == null) return null;
        var go = Object.Instantiate(source.gameObject, panel);
        go.name = name;
        StripLayout(go.transform, true);

        var chip = FindDeep(go.transform, "Chip");
        var icon = chip != null ? FindDeep(chip, "Icon") : null;
        if (icon != null) icon.GetComponent<Image>().color = iconTint;

        return go.GetComponent<RectTransform>();
    }

    // Drop auto-layout drivers so hand-set rects survive.
    static void StripLayout(Transform t, bool recursive)
    {
        foreach (var c in t.GetComponents<Component>())
            if (c is LayoutGroup || c is ContentSizeFitter || c is LayoutElement)
                Object.DestroyImmediate(c);
        if (!recursive) return;
        foreach (Transform child in t) StripLayout(child, true);
    }

    static void TopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ----------------------------------------------------- objective banner

    static void RebuildObjectiveBanner()
    {
        var root = SafeRoot();
        if (root == null) return;

        Kill(root, "ObjectiveText");
        Kill(root, "ObjectiveBanner");

        // A tracker tucked under the stats block, same width as it — not a full-width
        // banner across the top. Quest text is reference, not a headline.
        const float H = 72f;
        var banner = Rect("ObjectiveBanner", root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                          new Vector2(HudX, HudY - HudH - HudGap), new Vector2(HudW, H));
        Round(Panel_(banner, Panel));

        // gold spine on the left so the strip reads as a quest tracker, not a tooltip
        var spine = Rect("Spine", banner, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(6f, H));
        Panel_(spine, Accent);

        var label = Label("Label", banner, "Objective", 28f, TextAlignmentOptions.Left, Ink,
                          new Vector2(0f, 0.28f), new Vector2(1f, 1f), new Vector2(18f, 0f), new Vector2(-96f, -6f));
        label.fontStyle = FontStyles.Bold;
        var count = Label("Count", banner, "0/0", 28f, TextAlignmentOptions.Right, Accent,
                          new Vector2(0f, 0.28f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-14f, -6f));
        count.fontStyle = FontStyles.Bold;

        var track = Rect("BarTrack", banner, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(3f, 8f), new Vector2(HudW - 22f, 6f));
        Panel_(track, AccentDim);
        var fillRT = Rect("BarFill", track, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        fillRT.anchorMin = new Vector2(0f, 0f); fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
        // Panel_ leaves the sprite null, which silently disabled fillAmount — this
        // progress bar always rendered full, whatever the objective progress was.
        var fill = Panel_(fillRT, Accent);
        StyleFill(fill, Accent);
        fill.fillAmount = 0f;

        var hud = Object.FindFirstObjectByType<HUD>();
        if (hud != null)
        {
            hud.objectiveText = label;
            hud.objectiveCountText = count;
            hud.objectiveBar = fill;
            EditorUtility.SetDirty(hud);
        }
        else Debug.LogWarning("[BuildCatalogSetup] no HUD found — objective banner not wired.");

    }
}
