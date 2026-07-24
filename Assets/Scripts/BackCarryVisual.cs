using System.Collections.Generic;
using UnityEngine;

// Shows what you're carrying as a bundle strapped to the player's back.
// The pile grows with how full the bag is: carry more, see more. To keep a huge
// haul from turning into a tower, once the bundle would exceed maxBundleHeight the
// whole thing scales down to fit — so a full bag always reads as "packed", whatever
// the capacity happens to be. Any resource with a visual assigned shows up (wood,
// stone, meat, hide, ...).
public class BackCarryVisual : MonoBehaviour
{
    [System.Serializable]
    public class CarryVisual
    {
        public ResourceType type;
        public GameObject prefab;
        [Tooltip("Rotation applied to every piece — lay logs on their side, leave rocks upright.")]
        public Vector3 pieceEuler;
        [System.NonSerialized] public List<GameObject> pool = new List<GameObject>();
        // How tall this prefab actually is, measured off its meshes the first time it's
        // needed. A flat pelt and a fat log can't share one spacing number.
        [System.NonSerialized] public float height = -1f;
    }

    public Transform backSocket;
    public PlayerInventory inventory;
    [Tooltip("One entry per resource you want to see on the back.")]
    public CarryVisual[] visuals;

    [Header("Bundle shape")]
    [Tooltip("Columns across the shoulders (left-right). A column fills to " +
             "maxPerColumn, then the next column starts; once all columns are used " +
             "the stack grows backwards a row at a time.")]
    public int columns = 2;
    [Tooltip("Pieces stacked in one column before spilling into the next.")]
    public int maxPerColumn = 8;
    [Tooltip("IGNORED — depth now grows on its own as columns fill up.")]
    public int depth = 1;
    [Tooltip("X/Z gaps between pieces. Y is IGNORED — each layer's height is measured " +
             "off the meshes so a flat pelt and a fat log both sit flush.")]
    public Vector3 pieceSpacing = new Vector3(0.15f, 0.1f, 0.07f);
    [Tooltip("How far each layer sinks into the one below. 0 = balanced exactly on top, " +
             "0.25 = nested into it.")]
    [Range(0f, 0.5f)] public float nestFactor = 0.2f;
    public float pieceScale = 0.5f;
    [Tooltip("Bundle taller than this gets scaled down to fit — keeps big hauls tidy. " +
             "Set high for a stacker-game tower that grows past the head.")]
    public float maxBundleHeight = 0.75f;
    [Tooltip("ON: stack straddles the socket (tidy bundle). OFF: stack grows straight UP " +
             "from the socket like a stacker-game tower.")]
    public bool centerVertically = true;
    [Tooltip("Hard safety cap on spawned meshes, no matter how big the bag gets.")]
    public int hardCap = 60;
    public float jitter = 0.015f;
    [Tooltip("Every other row shifts sideways by this fraction of a piece — reads as a woodpile, not a wall.")]
    [Range(0f, 0.5f)] public float rowStagger = 0.35f;
    // ponytail: no "lean" knob — tilt the backSocket transform in the scene instead.

    [Header("Container")]
    [Tooltip("Optional crate/basket that holds the load, so it reads as a carried pack " +
             "instead of loose logs roped to the spine. Empty = the old loose pile.")]
    public GameObject containerPrefab;
    public Vector3 containerLocalPosition = Vector3.zero;
    public Vector3 containerLocalEuler = Vector3.zero;
    public float containerScale = 0.4f;
    [Tooltip("Shift the whole resource pile relative to the socket — lift it so the pieces " +
             "sit inside/poke out the top of the crate rather than floating behind it.")]
    public Vector3 pileLocalOffset = new Vector3(0f, 0.05f, 0f);

    GameObject container;

    void OnEnable()
    {
        inventory.OnInventoryChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        inventory.OnInventoryChanged -= Refresh;
    }

    void Refresh()
    {
        if (visuals == null) return;

        // Real counts per resource, then the running total.
        int[] counts = new int[visuals.Length];
        int total = 0;
        for (int v = 0; v < visuals.Length; v++)
        {
            counts[v] = inventory.GetAmount(visuals[v].type);
            total += counts[v];
        }

        // If the real haul exceeds the safety cap, show it proportionally instead.
        if (total > hardCap && total > 0)
        {
            int shown = 0;
            for (int v = 0; v < visuals.Length; v++)
            {
                counts[v] = Mathf.RoundToInt(counts[v] / (float)total * hardCap);
                shown += counts[v];
            }
            total = shown;
        }

        for (int v = 0; v < visuals.Length; v++)
            SyncPool(visuals[v].pool, visuals[v].prefab, counts[v]);

        // The crate only appears when you're actually carrying something.
        EnsureContainer();
        if (container != null) container.SetActive(total > 0);

        LayOut();
    }

    // Spawn the container once and strap it to the socket. Decoration only — no
    // colliders, no physics.
    void EnsureContainer()
    {
        if (containerPrefab == null || container != null) return;
        container = Instantiate(containerPrefab, backSocket);
        container.transform.localPosition = containerLocalPosition;
        container.transform.localRotation = Quaternion.Euler(containerLocalEuler);
        container.transform.localScale = Vector3.one * containerScale;
        foreach (var c in container.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var rb in container.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
        // A build prefab (e.g. B_Crate) carries PlacedBuildable — strip it, or the save
        // system (FindObjectsOfType<PlacedBuildable>) snapshots this decoration as a
        // real placed structure and MOVE lets you grab it off the player's back.
        foreach (var pb in container.GetComponentsInChildren<PlacedBuildable>()) DestroyImmediate(pb);
    }

    // Grow or shrink a pool to exactly 'count' live pieces — only touches the delta,
    // so a single pickup doesn't rebuild the whole stack every frame.
    void SyncPool(List<GameObject> pool, GameObject prefab, int count)
    {
        if (prefab == null) count = 0;

        while (pool.Count > count)
        {
            int last = pool.Count - 1;
            if (pool[last] != null) Destroy(pool[last]);
            pool.RemoveAt(last);
        }
        while (pool.Count < count)
        {
            var piece = Instantiate(prefab, backSocket);
            // it's decoration — never let it collide or fall
            foreach (var c in piece.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var rb in piece.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
            pool.Add(piece);
        }
    }

    // Height a prefab actually occupies, measured off its meshes in prefab space.
    // Cached per visual — the meshes never change at runtime.
    static float MeasureHeight(GameObject prefab)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            // prefab root sits at identity, so localToWorld here is prefab-local
            var mb = mf.sharedMesh.bounds;
            var m = mf.transform.localToWorldMatrix;
            for (int c = 0; c < 8; c++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3(
                    (c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                var p = m.MultiplyPoint3x4(corner);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return any ? Mathf.Max(0.001f, b.size.y) : 0.1f;
    }

    void LayOut()
    {
        int total = 0;
        foreach (var vis in visuals) total += vis.pool.Count;
        if (total == 0) return;

        foreach (var vis in visuals)
            if (vis.height < 0f) vis.height = vis.prefab != null ? MeasureHeight(vis.prefab) : 0.1f;

        int perCol = Mathf.Max(1, maxPerColumn);
        int cols = Mathf.Max(1, columns);
        int colCount = Mathf.CeilToInt(total / (float)perCol);

        // Walk the pieces once to work out how tall each column ends up. Pieces have
        // different heights, so a column is a running sum, not count * spacing.
        var colY = new float[colCount];    // running centre height per column
        var colPrev = new float[colCount]; // height of the last piece placed there
        var yOf = new float[total];
        {
            int i = 0;
            foreach (var vis in visuals)
                foreach (var piece in vis.pool)
                {
                    int c = i / perCol;
                    if (i % perCol == 0) colY[c] = 0f;
                    else colY[c] += (colPrev[c] + vis.height) * 0.5f * (1f - nestFactor);
                    colPrev[c] = vis.height;
                    yOf[i] = colY[c];
                    i++;
                }
        }

        // shrink-to-fit keys off the tallest column, so one long column can't tower away
        float tallest = 0f;
        for (int c = 0; c < colCount; c++) tallest = Mathf.Max(tallest, colY[c] + colPrev[c] * 0.5f);
        float stackHeight = tallest * pieceScale;
        float fit = stackHeight > maxBundleHeight ? maxBundleHeight / stackHeight : 1f;
        float scale = pieceScale * fit;

        // Straddle the socket (centred bundle) or grow straight up from it (tower).
        float yCentre = centerVertically ? tallest * scale * 0.5f : 0f;

        {
            int i = 0;
            foreach (var vis in visuals)
                foreach (var piece in vis.pool)
                {
                    Place(piece, i, i / perCol, cols, fit, scale, vis.pieceEuler, yOf[i] * scale - yCentre);
                    i++;
                }
        }
    }

    void Place(GameObject piece, int i, int col, int cols, float fit, float scale, Vector3 pieceEuler, float y)
    {
        if (piece == null) return;
        int cx = col % cols;          // across the shoulders
        int cz = col / cols;          // then backwards, a row at a time

        // Brick-stagger alternate depth rows so the pile reads as a heaped load
        // rather than a flat grid pinned to the back.
        float stagger = (cz % 2 == 1) ? rowStagger : 0f;
        float x = (cx - (cols - 1) * 0.5f + stagger) * pieceSpacing.x * fit;
        float z = cz * pieceSpacing.z * fit;

        // Jitter seeded from the piece index, not Random — otherwise every pickup
        // re-rolls the whole pile and the bundle visibly shuffles on your back.
        Vector3 j = new Vector3(Hash(i, 0) * jitter, Hash(i, 1) * jitter * 0.4f, Hash(i, 2) * jitter);
        piece.transform.localPosition = new Vector3(x, y, z) + j + pileLocalOffset;
        piece.transform.localRotation = Quaternion.Euler(pieceEuler + new Vector3(0f, Hash(i, 3) * 10f, Hash(i, 4) * 6f));
        piece.transform.localScale = Vector3.one * scale;
    }

    // Deterministic -1..1 from (index, channel). Stable across rebuilds.
    static float Hash(int i, int channel)
    {
        int h = i * 374761393 + channel * 668265263;
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xFFFF) / 32767.5f - 1f;
    }
}
