using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Ghost-preview placement, shared by BUILD (drop a new structure) and MOVE
// (reposition one you already placed).
//
//   BUILD: tap a build card -> StartPlacement(index). A translucent copy of the
//          prefab appears; drag it along the ground, tap Rotate for 45-deg steps,
//          toggle grid snap. Confirm pays + drops the real prefab; Cancel spends
//          nothing.
//   MOVE:  tap MOVE in the sheet (ArmMove) then tap a placed structure. The
//          original hides, a ghost takes its place, same loop. Confirm writes the
//          new transform back; Cancel leaves it untouched. Arming is required, so
//          you can't grab a structure by accident.
//
// The ghost turns green where it may sit and red where it would overlap another
// structure, the player, a resource node or a creature, or hang off the ground.
// Legacy Input (Input.GetMouseButton(0)) is used so the first touch drives it on
// mobile, matching the rest of the project.
public class PlacementController : MonoBehaviour
{
    public BuildSystem buildSystem;
    public Transform player;
    public float placeDistance = 2.5f;
    public float gridSize = 1f;
    public float groundProbe = 4f;   // how far a footprint corner may be above ground before it's "hanging"

    [Header("Placement bar (assigned by BuildCatalogSetup)")]
    public GameObject bar;            // the on-screen Rotate/Grid/Cancel/Confirm strip; shown only while placing
    public Button confirmButton;
    public Button cancelButton;
    public Button rotateButton;
    public Button gridButton;
    public TextMeshProUGUI gridLabel;

    [Header("Ghost materials (auto-created if left null)")]
    public Material ghostValidMat;
    public Material ghostInvalidMat;

    Camera cam;
    Canvas canvas;
    GameObject ghost;
    Renderer[] ghostRenderers;
    Bounds localBounds;              // footprint at yaw 0, relative to the ghost root
    int index = -1;
    bool isMove;
    PlacedBuildable moveSource;
    Collider[] moveSourceCols;
    float yaw;
    bool gridOn;
    bool valid;
    bool tintValid;
    bool moveArmed;

    bool Active => ghost != null;

    // True while a ghost is live. FloatingJoystick reads this to stop driving the
    // player (and eating touches) mid-placement, so a drag moves the ghost only.
    public static bool Placing { get; private set; }

    void Start()
    {
        cam = Camera.main;
        if (buildSystem == null) buildSystem = FindFirstObjectByType<BuildSystem>();
        if (player == null && buildSystem != null) player = buildSystem.player;
        if (bar != null) canvas = bar.GetComponentInParent<Canvas>();
        EnsureGhostMats();
        if (bar != null) bar.SetActive(false);
        Placing = false;
        RefreshGridLabel();
    }

    // ---------------------------------------------------------------- lifecycle

    // BUILD: arm a ghost for catalog[index]. Bails with a toast if you can't afford
    // it, so you never drag a ghost you can't buy.
    public void StartPlacement(int index)
    {
        if (buildSystem == null) return;
        var prefab = buildSystem.GetPrefab(index);
        if (prefab == null || player == null) return;
        // Refuse before the ghost appears, so a capped item never dangles a preview
        // the player can't actually place.
        if (!buildSystem.UnderLimit(index))
        {
            UIFeedback.FailOnClicked();
            buildSystem.Toast($"{buildSystem.GetName(index)} limit is {buildSystem.GetMaxCount(index)}");
            return;
        }
        if (!buildSystem.CanAfford(index))
        {
            UIFeedback.FailOnClicked();
            buildSystem.Toast($"Need {buildSystem.GetCost(index)} coins");
            return;
        }

        CancelInternal();                 // never stack two ghosts
        this.index = index;
        isMove = false;
        moveSource = null;
        yaw = player.eulerAngles.y;

        Vector3 pos = player.position + player.forward * placeDistance;
        pos.y = GroundY(pos);
        SpawnGhost(prefab, pos);
    }

    // Called by the sheet's MOVE button. The next tapped structure gets picked up.
    public void ArmMove()
    {
        if (Active) return;
        moveArmed = true;
        if (buildSystem != null) buildSystem.Toast("Tap a structure to move");
    }

    // MOVE: hide the tapped structure and float a ghost in its place.
    public void StartMove(PlacedBuildable pb)
    {
        if (Active || pb == null) return;
        CancelInternal();
        index = pb.catalogIndex;          // may be -1 (unknown) — move still works, just no coins
        isMove = true;
        moveSource = pb;
        yaw = pb.transform.eulerAngles.y;

        // Hide the original and switch off its colliders so it can't block its own
        // ghost during the overlap check.
        SetVisible(pb.gameObject, false);
        moveSourceCols = pb.GetComponentsInChildren<Collider>();
        foreach (var c in moveSourceCols) c.enabled = false;

        // Ghost from the catalog prefab when we know it, else from a copy of the
        // structure itself.
        var src = buildSystem != null ? buildSystem.GetPrefab(index) : null;
        SpawnGhost(src != null ? src : pb.gameObject, pb.transform.position);
    }

    public void Confirm()
    {
        if (!Active || !valid) return;

        if (isMove)
        {
            if (moveSource != null)
            {
                moveSource.transform.SetPositionAndRotation(ghost.transform.position, Quaternion.Euler(0f, yaw, 0f));
                RestoreMoveSource();
                if (buildSystem != null) buildSystem.Toast("Moved");
            }
            Cleanup();
        }
        else
        {
            // Only tear down the ghost if the purchase actually went through.
            if (buildSystem != null && buildSystem.CommitPlace(index, ghost.transform.position, yaw))
                Cleanup();
        }
    }

    public void Cancel()
    {
        if (!Active) return;
        if (isMove) RestoreMoveSource();
        Cleanup();
    }

    public void Rotate()
    {
        if (!Active) return;
        yaw = Mathf.Repeat(yaw + 45f, 360f);
        ghost.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    public void ToggleGrid()
    {
        gridOn = !gridOn;
        RefreshGridLabel();
    }

    // ------------------------------------------------------------------- update

    void Update()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (Active)
        {
            // Drag anywhere on the ground to reposition the ghost. Guard only against
            // the placement bar itself — NOT PointerOverUI, because the joystick's
            // fullscreen touch zone makes that true across the whole screen.
            if (Input.GetMouseButton(0) && !OverBar() && GroundUnderPointer(out Vector3 p))
            {
                p = Snap(p);
                ghost.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            }

            valid = IsValid();
            if (valid != tintValid) { SetTint(valid); tintValid = valid; }
            if (confirmButton != null) confirmButton.interactable = valid;
        }
        else if (moveArmed && Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 500f))
            {
                var pb = hit.collider.GetComponentInParent<PlacedBuildable>();
                if (pb != null) { moveArmed = false; StartMove(pb); }
            }
        }
    }

    // --------------------------------------------------------------- validation

    // Green only when the footprint overlaps nothing solid AND every corner has
    // ground beneath it.
    bool IsValid()
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 center = ghost.transform.position + rot * localBounds.center;
        Vector3 half = localBounds.extents;

        var hits = Physics.OverlapBox(center, half, rot, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (IsObstacle(h)) return false;

        // Four footprint corners must each find ground within groundProbe below.
        Vector3 min = localBounds.min, max = localBounds.max;
        float y = localBounds.center.y;
        Vector2[] xz = { new(min.x, min.z), new(min.x, max.z), new(max.x, min.z), new(max.x, max.z) };
        foreach (var c in xz)
        {
            Vector3 corner = ghost.transform.position + rot * new Vector3(c.x, y, c.y);
            if (!Physics.Raycast(corner + Vector3.up * 0.5f, Vector3.down, groundProbe + 0.5f, ~0, QueryTriggerInteraction.Ignore))
                return false;
        }
        return true;
    }

    // A collider blocks placement if it belongs to another structure, the player, a
    // resource node, or a creature. Ground/terrain has none of these components, so
    // it never blocks. The move source (hidden, colliders off) can't reach here.
    bool IsObstacle(Collider c)
    {
        if (c.GetComponentInParent<PlacedBuildable>() is PlacedBuildable pb && pb != moveSource) return true;
        if (c.GetComponentInParent<PlayerController>() != null) return true;
        if (c.GetComponentInParent<ResourceNode>() != null) return true;
        if (c.GetComponentInParent<Creature>() != null) return true;
        return false;
    }

    // ----------------------------------------------------------------- ghost I/O

    void SpawnGhost(GameObject src, Vector3 pos)
    {
        ghost = Instantiate(src, pos, Quaternion.Euler(0f, yaw, 0f));
        ghost.name = "__Ghost";

        // A ghost is preview only: no placed-marker (so it isn't saved or grabbed),
        // no active colliders (so it neither blocks the world nor self-collides), and
        // no scripts/lights firing while it's just a hologram.
        foreach (var pb in ghost.GetComponentsInChildren<PlacedBuildable>()) Destroy(pb);
        foreach (var col in ghost.GetComponentsInChildren<Collider>()) col.enabled = false;
        foreach (var mb in ghost.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
        foreach (var lt in ghost.GetComponentsInChildren<Light>()) lt.enabled = false;

        localBounds = ComputeLocalBounds(ghost);
        ghostRenderers = ghost.GetComponentsInChildren<Renderer>();
        tintValid = true; SetTint(true);

        // A campfire hands out a predator-free radius, so you need to see that radius
        // WHILE you choose the spot — that's the one moment it matters. It goes on the
        // ghost, so it disappears the instant you place or cancel. (An always-on ring
        // around every built fire was tried and rejected: three fires meant three
        // orange circles painted across camp permanently.)
        // Added after SetTint so the ring keeps its own colour instead of being
        // repainted green/red with the rest of the ghost.
        if (ghost.GetComponentInChildren<Campfire>(true) != null)
            GroundRing.Attach(ghost.transform, Campfire.RepelRadiusForTier(Campfire.SharedTier), Campfire.RingColor);

        if (bar != null) bar.SetActive(true);
        if (confirmButton != null) confirmButton.interactable = false;
        Placing = true;
    }

    void Cleanup()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
        ghostRenderers = null;
        index = -1;
        isMove = false;
        moveSource = null;
        moveSourceCols = null;
        if (bar != null) bar.SetActive(false);
        Placing = false;
    }

    // Cleanup without touching a move source — used before arming a fresh ghost.
    void CancelInternal()
    {
        if (isMove) RestoreMoveSource();
        Cleanup();
    }

    void RestoreMoveSource()
    {
        if (moveSource != null) SetVisible(moveSource.gameObject, true);
        if (moveSourceCols != null) foreach (var c in moveSourceCols) if (c != null) c.enabled = true;
        moveSourceCols = null;
    }

    // ------------------------------------------------------------------- helpers

    Vector3 Snap(Vector3 p)
    {
        if (!gridOn) return p;
        p.x = Mathf.Round(p.x / gridSize) * gridSize;
        p.z = Mathf.Round(p.z / gridSize) * gridSize;
        p.y = GroundY(p);
        return p;
    }

    // Screen ray -> point on the ground plane at the player's foot height, then a
    // vertical raycast to snap onto whatever terrain/mesh is actually there.
    bool GroundUnderPointer(out Vector3 point)
    {
        point = default;
        float y = player != null ? player.position.y : 0f;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
        if (!plane.Raycast(ray, out float enter)) return false;
        Vector3 hit = ray.GetPoint(enter);
        hit.y = GroundY(hit);
        point = hit;
        return true;
    }

    float GroundY(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out var h, 200f, ~0, QueryTriggerInteraction.Ignore))
            return h.point.y;
        return player != null ? player.position.y : p.y;
    }

    // True when the finger is over the placement bar, so tapping Rotate/Grid/Cancel/
    // Place doesn't also yank the ghost. Deliberately ignores every other UI element
    // (the joystick zone especially) so the rest of the screen drags the ghost.
    bool OverBar()
    {
        if (bar == null || !bar.activeSelf) return false;
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)bar.transform, Input.mousePosition, cam);
    }

    // Combined renderer bounds, expressed relative to the ghost root. Measured while
    // the ghost is at whatever position/rotation it spawned; only the size and the
    // local offset are kept, and we always re-rotate by yaw when using it.
    static Bounds ComputeLocalBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        // Undo the ghost's current yaw so the stored footprint is axis-aligned in
        // local space; IsValid re-applies the live yaw.
        Vector3 local = Quaternion.Inverse(go.transform.rotation) * (b.center - go.transform.position);
        return new Bounds(local, b.size);
    }

    void SetTint(bool ok)
    {
        if (ghostRenderers == null) return;
        var m = ok ? ghostValidMat : ghostInvalidMat;
        foreach (var r in ghostRenderers)
        {
            var arr = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < arr.Length; i++) arr[i] = m;
            r.sharedMaterials = arr;
        }
    }

    static void SetVisible(GameObject go, bool on)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = on;
    }

    void RefreshGridLabel()
    {
        if (gridLabel != null) gridLabel.text = gridOn ? "GRID: ON" : "GRID: OFF";
    }

    // Transparent stand-in materials so we don't depend on any authored asset.
    void EnsureGhostMats()
    {
        if (ghostValidMat == null) ghostValidMat = MakeGhostMat(new Color(0.35f, 0.9f, 0.45f, 0.5f));
        if (ghostInvalidMat == null) ghostInvalidMat = MakeGhostMat(new Color(0.95f, 0.32f, 0.30f, 0.5f));
    }

    static Material MakeGhostMat(Color c)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var m = new Material(shader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        // Transparent surface for URP/Unlit.
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }
}
