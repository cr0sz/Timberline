using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Re-runnable VFX builder: Tools/Survival/Build Particle FX.
// Creates the URP particle materials (a runtime new Material(Shader.Find(...)) baked
// into a prefab loses its shader and renders MAGENTA — so these are saved as real
// .mat assets first), a soft-dot sprite, and three self-destroying ParticleSystem
// prefabs, then wires the two burst prefabs + the campfire rig onto VFXManager.
public static class ParticleSetup
{
    const string VfxDir = "Assets/VFX";
    const string PrefabDir = "Assets/Prefabs/VFX";
    const string TexPath = VfxDir + "/soft_dot.png";

    [MenuItem("Tools/Survival/Build Particle FX")]
    public static void Build()
    {
        Directory.CreateDirectory(VfxDir);
        Directory.CreateDirectory(PrefabDir);

        var tex = MakeSoftDot();
        var alphaMat = MakeMaterial("M_ParticleAlpha", tex, additive: false);
        var addMat = MakeMaterial("M_ParticleAdd", tex, additive: true);

        var debris = BuildDebris(alphaMat);
        var spark = BuildSpark(addMat);
        var campfire = BuildCampfire(alphaMat, addMat);

        Wire(debris, spark, campfire);

        AssetDatabase.SaveAssets();
        EditorSceneManagerSaveOpen();
        Debug.Log("[ParticleSetup] DONE — materials, soft dot, 3 FX prefabs, VFXManager wired.");
    }

    static void EditorSceneManagerSaveOpen()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
    }

    // 64px radial-alpha white dot, so particles read as soft puffs not hard squares.
    static Texture2D MakeSoftDot()
    {
        const int S = 64;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(S / 2f, S / 2f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (S / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a;                       // soften the edge
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        File.WriteAllBytes(TexPath, t.EncodeToPNG());
        AssetDatabase.ImportAsset(TexPath, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        imp.textureType = TextureImporterType.Default;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
    }

    static Material MakeMaterial(string name, Texture2D tex, bool additive)
    {
        string path = VfxDir + "/" + name + ".mat";
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var mat = new Material(sh);
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        // Force transparent surface + blend explicitly — the material upgrader that
        // normally reacts to _Surface/_Blend doesn't run on a script-made material.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_Blend", additive ? 2f : 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // --- prefab builders -----------------------------------------------------

    static ParticleSystem BuildDebris(Material mat)
    {
        var go = new GameObject("FX_Debris");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;   // VFXManager sets the tint, THEN Play()s — else the
                                    // burst fires white on Instantiate before it's coloured
        main.startLifetime = 0.6f;
        main.startSpeed = 2.6f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = Color.white;
        main.gravityModifier = 0.9f;
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        Burst(ps, 14);
        Sphere(ps, 0.15f);
        FadeAlpha(ps);

        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        return SavePrefab(go, "FX_Debris");
    }

    static ParticleSystem BuildSpark(Material mat)
    {
        var go = new GameObject("FX_Spark");
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.playOnAwake = false;   // tinted by VFXManager before Play()
        main.startLifetime = 0.7f;
        main.startSpeed = 2.0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
        main.startColor = Color.white;
        main.gravityModifier = -0.15f;               // drifts up
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        Burst(ps, 18);
        Cone(ps, 0.1f, 25f);
        FadeAlpha(ps);

        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        return SavePrefab(go, "FX_Spark");
    }

    // A parent holding a looping smoke column + a slow ember drift, for a campfire.
    static GameObject BuildCampfire(Material alphaMat, Material addMat)
    {
        var root = new GameObject("FX_Campfire");

        // smoke
        var smokeGo = new GameObject("Smoke");
        smokeGo.transform.SetParent(root.transform, false);
        smokeGo.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        var smoke = smokeGo.AddComponent<ParticleSystem>();
        var sm = smoke.main;
        sm.loop = true;
        sm.playOnAwake = true;
        sm.startLifetime = 2.0f;
        sm.startSpeed = 0.7f;
        sm.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        sm.startColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        sm.gravityModifier = -0.08f;
        sm.maxParticles = 40;
        sm.simulationSpace = ParticleSystemSimulationSpace.Local;
        var se = smoke.emission; se.rateOverTime = 8f;
        Sphere(smoke, 0.18f);
        GrowSize(smoke, 0.7f, 1.8f);
        FadeInOut(smoke, 0.5f);
        smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = alphaMat;

        // embers
        var emberGo = new GameObject("Embers");
        emberGo.transform.SetParent(root.transform, false);
        emberGo.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        var ember = emberGo.AddComponent<ParticleSystem>();
        var em = ember.main;
        em.loop = true;
        em.playOnAwake = true;
        em.startLifetime = 1.1f;
        em.startSpeed = 1.1f;
        em.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        em.startColor = new Color(1f, 0.55f, 0.15f, 1f);
        em.gravityModifier = -0.25f;
        em.maxParticles = 30;
        em.simulationSpace = ParticleSystemSimulationSpace.Local;
        var ee = ember.emission; ee.rateOverTime = 6f;
        Cone(ember, 0.12f, 18f);
        FadeInOut(ember, 1f);
        ember.GetComponent<ParticleSystemRenderer>().sharedMaterial = addMat;

        string path = PrefabDir + "/FX_Campfire.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // --- module helpers ------------------------------------------------------

    static void Burst(ParticleSystem ps, short count)
    {
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
    }

    static void Sphere(ParticleSystem ps, float radius)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = radius;
    }

    static void Cone(ParticleSystem ps, float radius, float angle)
    {
        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.radius = radius;
        sh.angle = angle;
        sh.rotation = new Vector3(-90f, 0f, 0f);     // point the cone up (+Y)
    }

    // Alpha 1 -> 0 over life. Multiplies the per-spawn startColor, so tinting still works.
    static void FadeAlpha(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    static void FadeInOut(ParticleSystem ps, float peak)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.3f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    static void GrowSize(ParticleSystem ps, float from, float to)
    {
        var s = ps.sizeOverLifetime;
        s.enabled = true;
        var curve = new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to));
        s.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    static ParticleSystem SavePrefab(GameObject go, string name)
    {
        string path = PrefabDir + "/" + name + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<ParticleSystem>();
    }

    static void Wire(ParticleSystem debris, ParticleSystem spark, GameObject campfire)
    {
        var gm = GameObject.Find("GameManager");
        if (gm == null) { Debug.LogWarning("[ParticleSetup] no GameManager — VFXManager not wired."); return; }
        var vm = gm.GetComponent<VFXManager>();
        if (vm == null) vm = gm.AddComponent<VFXManager>();
        vm.debrisPrefab = debris;
        vm.sparkPrefab = spark;
        vm.campfirePrefab = campfire;
        EditorUtility.SetDirty(vm);
    }
}
