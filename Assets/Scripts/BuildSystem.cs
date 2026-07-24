using UnityEngine;

// The coin sink. Owns the buildables catalog + coins, and the save/load of placed
// structures. Placement itself (the ghost preview, drag, rotate, grid, overlap
// check) lives in PlacementController — a build-bar button arms a ghost, and only a
// confirmed ghost calls back into CommitPlace to actually spend coins and drop the
// real prefab. Nothing is charged until confirm.
public class BuildSystem : MonoBehaviour
{
    [System.Serializable]
    public class Buildable
    {
        public string name;
        public GameObject prefab;
        public int cost = 50;
        [Tooltip("How many of this may exist at once. 0 = unlimited. Used by the campfire, " +
                 "which is capped at 3 — it hands out a safe radius, so unlimited fires would " +
                 "let the player pave the whole map into a no-predator zone.")]
        public int maxCount = 0;
    }

    public Buildable[] catalog;
    public PlayerInventory inventory;
    public Transform player;
    public float placeDistance = 2.5f;
    public FloatingText floatingTextPrefab;
    public PlacementController placement;

    void Start()
    {
        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
        if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>();
        if (placement == null) placement = FindFirstObjectByType<PlacementController>();
    }

    // --- Catalog access (used by PlacementController to build the ghost) ---

    public bool InRange(int index) => catalog != null && index >= 0 && index < catalog.Length;
    public GameObject GetPrefab(int index) => InRange(index) ? catalog[index].prefab : null;
    public int GetCost(int index) => InRange(index) ? catalog[index].cost : 0;
    public string GetName(int index) => InRange(index) ? catalog[index].name : "";
    public bool CanAfford(int index) => InRange(index) && inventory != null && inventory.CanAffordCoins(catalog[index].cost);
    public int GetMaxCount(int index) => InRange(index) ? catalog[index].maxCount : 0;

    // How many of this catalog entry are standing right now. Counted by walking the
    // live PlacedBuildable markers rather than by keeping a tally, so it can't drift
    // out of sync when a structure is destroyed or a save is loaded.
    public int CountPlaced(int index)
    {
        int n = 0;
        foreach (var pb in FindObjectsByType<PlacedBuildable>(FindObjectsSortMode.None))
            if (pb.catalogIndex == index) n++;
        return n;
    }

    // False when this entry is capped and the cap is already reached.
    public bool UnderLimit(int index)
    {
        int max = GetMaxCount(index);
        return max <= 0 || CountPlaced(index) < max;
    }

    // Hooked to the build-bar buttons (one per catalog index). Arms a ghost preview;
    // the player positions it and confirms before any coins move.
    public void Place(int index)
    {
        if (placement == null) placement = FindFirstObjectByType<PlacementController>();
        if (placement != null) placement.StartPlacement(index);
    }

    // Called by PlacementController when a ghost is confirmed. Re-checks affordability
    // (the player may have spent since arming), instantiates the real structure, and
    // charges for it. Returns false if it couldn't be afforded.
    public bool CommitPlace(int index, Vector3 pos, float yaw)
    {
        if (!InRange(index)) return false;
        var b = catalog[index];
        if (b.prefab == null || inventory == null) return false;
        // Re-check the cap here as well as on arming: the player could have placed the
        // last allowed one from a second ghost while this one was still floating.
        if (!UnderLimit(index)) { UIFeedback.FailOnClicked(); Toast($"{b.name} limit is {b.maxCount}"); return false; }
        if (!inventory.CanAffordCoins(b.cost)) { UIFeedback.FailOnClicked(); Toast($"Need {b.cost} coins"); return false; }

        var go = Instantiate(b.prefab, pos, Quaternion.Euler(0f, yaw, 0f));
        var pb = go.GetComponent<PlacedBuildable>();
        if (pb == null) pb = go.AddComponent<PlacedBuildable>();
        pb.catalogIndex = index;                 // remember which catalog entry, so it can be re-saved
        inventory.SpendCoins(b.cost);
        AudioManager.Purchase();
        UIFeedback.SuccessOnClicked();
        Toast($"-{b.cost}  {b.name}");
        return true;
    }

    // The build sheet's "MOVE A STRUCTURE" button arms move-mode on the controller so
    // the next tapped structure gets picked up. Kept for the existing button wiring.
    public void ToggleMove()
    {
        if (placement == null) placement = FindFirstObjectByType<PlacementController>();
        if (placement != null) placement.ArmMove();
    }

    // --- Save/load support ---

    // Snapshot every placed structure (Y-only rotation, matching CommitPlace/Move).
    public void SnapshotBuildables(out int[] indices, out Vector3[] positions, out float[] rotY)
    {
        var all = FindObjectsByType<PlacedBuildable>(FindObjectsSortMode.None);
        // Only persist ones with a known catalog index.
        var valid = new System.Collections.Generic.List<PlacedBuildable>();
        foreach (var pb in all)
            if (pb.catalogIndex >= 0 && catalog != null && pb.catalogIndex < catalog.Length)
                valid.Add(pb);

        indices = new int[valid.Count];
        positions = new Vector3[valid.Count];
        rotY = new float[valid.Count];
        for (int i = 0; i < valid.Count; i++)
        {
            indices[i] = valid[i].catalogIndex;
            positions[i] = valid[i].transform.position;
            rotY[i] = valid[i].transform.eulerAngles.y;
        }
    }

    // Rebuild placed structures from a save. Skips any index outside the catalog.
    public void LoadBuildables(int[] indices, Vector3[] positions, float[] rotY)
    {
        if (indices == null || positions == null || rotY == null) return;
        int n = Mathf.Min(indices.Length, Mathf.Min(positions.Length, rotY.Length));
        for (int i = 0; i < n; i++)
        {
            int idx = indices[i];
            if (catalog == null || idx < 0 || idx >= catalog.Length) continue;
            var prefab = catalog[idx].prefab;
            if (prefab == null) continue;

            var go = Instantiate(prefab, positions[i], Quaternion.Euler(0f, rotY[i], 0f));
            var pb = go.GetComponent<PlacedBuildable>();
            if (pb == null) pb = go.AddComponent<PlacedBuildable>();
            pb.catalogIndex = idx;
        }
    }

    public void Toast(string m)
    {
        if (floatingTextPrefab == null || player == null) return;
        FloatingText.Spawn(floatingTextPrefab, player.position + Vector3.up * 2.2f, m);
    }
}
