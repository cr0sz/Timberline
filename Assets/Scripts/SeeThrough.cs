using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Fades world objects that sit between the camera and the player so the player is
// never fully hidden. SphereCasts camera->player each frame; hit MeshRenderers
// fade to a low alpha (a runtime transparent clone of their material) and restore
// their original opaque material once they're clear. Put this on the Main Camera.
public class SeeThrough : MonoBehaviour
{
    public Transform target;                 // the player
    public float castRadius = 0.4f;
    [Range(0.05f, 1f)] public float fadeAlpha = 0.28f;
    public float fadeSpeed = 6f;             // alpha units per second
    public LayerMask mask = ~0;
    [Tooltip("Stop the cast this far short of the player.")]
    public float endPadding = 1.2f;
    [Tooltip("Keep fading a tree for this long after it stops blocking. Without it, a " +
             "canopy clipping the very edge of the cast drops in and out of the hit list " +
             "as you walk, and the tree strobes. Costs a few frames of over-fade, which " +
             "nobody notices; the strobe, everybody notices.")]
    public float holdTime = 0.35f;

    Camera cam;
    readonly RaycastHit[] hitBuf = new RaycastHit[24];

    class Entry
    {
        public Renderer renderer;
        public Material[] opaque;             // original shared materials
        public Material[] fading;             // transparent clones (instances)
        public float alpha = 1f;
        public bool wanted;
        public float lastWanted = -999f;      // when this renderer last blocked the view
    }

    readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
    readonly List<Renderer> toDrop = new List<Renderer>();
    // GetComponentsInChildren allocates a fresh array every call; cache per collider
    // so a sightline object is only queried once. ponytail: hard-capped — dead
    // creatures leave stale keys, so clear wholesale past the cap rather than track
    // destruction. 256 >> the handful ever on a camera->player sightline.
    readonly Dictionary<Collider, MeshRenderer[]> rendererCache = new Dictionary<Collider, MeshRenderer[]>();

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (target == null)
        {
            var p = FindFirstObjectByType<PlayerController>();
            if (p != null) target = p.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null || cam == null) return;

        foreach (var e in entries.Values) e.wanted = false;

        Vector3 origin = cam.transform.position;
        Vector3 to = target.position - origin;
        float dist = to.magnitude - endPadding;
        if (dist > 0.1f)
        {
            Vector3 dir = to / to.magnitude;
            int n = Physics.SphereCastNonAlloc(origin, castRadius, dir, hitBuf, dist, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = hitBuf[i].collider;
                if (col == null) continue;
                if (target.IsChildOf(col.transform) || col.transform.IsChildOf(target)) continue;

                foreach (var r in RenderersOf(col))
                {
                    var e = GetOrAdd(r);
                    if (e != null) { e.wanted = true; e.lastWanted = Time.time; }
                }
            }
        }

        toDrop.Clear();
        foreach (var kv in entries)
        {
            var e = kv.Value;
            // Hysteresis: a renderer stays faded for holdTime after it stops blocking,
            // so an edge-clipping canopy that dips in and out of the SphereCast between
            // frames holds its fade instead of strobing.
            bool hold = e.wanted || Time.time - e.lastWanted < holdTime;
            float goal = hold ? fadeAlpha : 1f;
            e.alpha = Mathf.MoveTowards(e.alpha, goal, fadeSpeed * Time.deltaTime);
            if (!hold && e.alpha >= 0.999f) { Restore(e); toDrop.Add(kv.Key); }
            else SetAlpha(e);
        }
        foreach (var r in toDrop) entries.Remove(r);
    }

    MeshRenderer[] RenderersOf(Collider col)
    {
        if (rendererCache.TryGetValue(col, out var arr)) return arr;
        if (rendererCache.Count > 256) rendererCache.Clear();   // bound the leak
        arr = col.GetComponentsInChildren<MeshRenderer>();
        rendererCache[col] = arr;
        return arr;
    }

    Entry GetOrAdd(Renderer r)
    {
        if (entries.TryGetValue(r, out var existing)) return existing;

        var shared = r.sharedMaterials;
        if (shared == null || shared.Length == 0) return null;

        var fading = new Material[shared.Length];
        for (int i = 0; i < shared.Length; i++)
            fading[i] = shared[i] != null ? MakeTransparent(new Material(shared[i])) : null;

        var e = new Entry { renderer = r, opaque = shared, fading = fading, alpha = 1f, wanted = true };
        r.materials = fading;
        entries[r] = e;
        return e;
    }

    void SetAlpha(Entry e)
    {
        foreach (var m in e.fading)
        {
            if (m == null) continue;
            if (m.HasProperty("_BaseColor")) { var c = m.GetColor("_BaseColor"); c.a = e.alpha; m.SetColor("_BaseColor", c); }
            else if (m.HasProperty("_Color")) { var c = m.color; c.a = e.alpha; m.color = c; }
        }
    }

    void Restore(Entry e)
    {
        if (e.renderer != null) e.renderer.sharedMaterials = e.opaque;
        foreach (var m in e.fading) if (m != null) Destroy(m);
    }

    // Configure a cloned URP/Lit material for alpha-blended transparency.
    static Material MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);              // 0 opaque, 1 transparent
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }
}
