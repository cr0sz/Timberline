using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Persists run progress to Application.persistentDataPath/save.json.
// Runs LAST (DefaultExecutionOrder 1000) so its Load overwrites every other
// system's default Start() values. Saves on app-pause/quit (mobile-critical)
// plus a throttled autosave whenever inventory changes.
[DefaultExecutionOrder(1000)]
public class SaveManager : MonoBehaviour
{
    [Serializable]
    class SaveData
    {
        public int version = 1;
        public int coins;
        public int capacity;
        public float moveSpeed;
        public int health;
        public int axeTier = 1, pickaxeTier = 1, weaponTier = 1;
        public int capacityLevel = 1, speedLevel = 1;
        public int[] resTypes;
        public int[] resAmounts;
        public int campfireTier;
        public int storageTier;
        // v2: player-placed buildables (JsonUtility serializes Vector3[]).
        public int[] buildIndices;
        public Vector3[] buildPositions;
        public float[] buildRotY;
        // v3 carried a `hunger` field. The hunger system was cut on 2026-07-21; the
        // field is gone and JsonUtility silently ignores it in older files, so the
        // version does NOT need a bump. Resource enum indices are unchanged.
        // v4
        public int objectiveIndex = -1;   // -1 = absent -> keep default (0)
        // v5: lifetime stat counters
        public int statWood, statStone, statTotal, statKills;
        // v6: the campfire and crate are draggable now (they carry a PlacedBuildable),
        // so where they STAND has to persist or a relocated camp snaps back to its pad
        // on reload. The bools distinguish "never built" from "built at the origin".
        public Vector3 campfirePos;
        public bool campfirePlaced;
        public Vector3 storagePos;
        public bool storagePlaced;
    }

    const int CurrentVersion = 7;

    // Catalog indices are stored raw in buildIndices, so cutting a catalog entry
    // renumbers everything after it and an old save rebuilds the wrong prefabs.
    //
    // v7 cut "Crate" (index 10), which did nothing at all. Campfire moved 11 -> 10.
    // Placed crates are dropped rather than remapped: the item no longer exists, and
    // silently turning someone's crates into campfires would hand out free
    // predator-repel zones (campfires are capped at 3 for exactly that reason).
    //
    // Any future cut adds its own block here and bumps CurrentVersion.
    const int CutCrateIndex = 10;

    /// <summary>
    /// Pure part of the migration, kept separate so it is testable without a SaveData:
    /// given the saved indices and the index that was cut, returns one (slot, newIndex)
    /// pair per SURVIVING entry. `slot` is the position in the original arrays, so the
    /// caller can carry positions and rotations across in step.
    /// </summary>
    public static List<(int slot, int newIndex)> RemapAfterCut(int[] indices, int cutIndex)
    {
        var kept = new List<(int, int)>();
        if (indices == null) return kept;
        for (int i = 0; i < indices.Length; i++)
        {
            int old = indices[i];
            if (old == cutIndex) continue;                       // the item no longer exists
            kept.Add((i, old > cutIndex ? old - 1 : old));
        }
        return kept;
    }

    static void MigrateBuildIndices(SaveData d)
    {
        if (d.version >= 7 || d.buildIndices == null) return;

        var kept = RemapAfterCut(d.buildIndices, CutCrateIndex);
        var idx = new List<int>();
        var pos = new List<Vector3>();
        var rot = new List<float>();
        foreach (var (slot, newIndex) in kept)
        {
            idx.Add(newIndex);
            // Written in lockstep by SnapshotBuildables, but a truncated or
            // hand-edited file must not throw here.
            if (d.buildPositions != null && slot < d.buildPositions.Length) pos.Add(d.buildPositions[slot]);
            if (d.buildRotY != null && slot < d.buildRotY.Length) rot.Add(d.buildRotY[slot]);
        }
        d.buildIndices = idx.ToArray();
        d.buildPositions = pos.ToArray();
        d.buildRotY = rot.ToArray();
    }
    [Tooltip("Min seconds between throttled autosaves.")]
    public float autosaveInterval = 3f;

    string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    PlayerInventory inventory;
    ToolInventory tools;
    PlayerController mover;
    PlayerHealth health;
    Shop shop;
    UpgradeStation campfire, storage;
    BuildSystem builder;
    ObjectiveManager objectives;
    PlayerStats stats;

    bool dirty;
    float lastSave;

    void Awake()
    {
        inventory = FindFirstObjectByType<PlayerInventory>();
        tools = FindFirstObjectByType<ToolInventory>();
        mover = FindFirstObjectByType<PlayerController>();
        health = FindFirstObjectByType<PlayerHealth>();
        shop = FindFirstObjectByType<Shop>();
        builder = FindFirstObjectByType<BuildSystem>();
        objectives = FindFirstObjectByType<ObjectiveManager>();
        stats = FindFirstObjectByType<PlayerStats>();
        foreach (var s in FindObjectsByType<UpgradeStation>(FindObjectsSortMode.None))
        {
            if (s.kind == UpgradeStation.Kind.Campfire) campfire = s;
            else if (s.kind == UpgradeStation.Kind.Storage) storage = s;
        }
        if (inventory != null) inventory.OnInventoryChanged += MarkDirty;
    }

    void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= MarkDirty;
    }

    void Start() => Load();

    void MarkDirty() => dirty = true;

    void Update()
    {
        if (dirty && Time.time - lastSave >= autosaveInterval) Save();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && dirty) Save();
    }

    void OnApplicationQuit()
    {
        if (dirty) Save();
    }

    public void Save()
    {
        try
        {
            var d = new SaveData { version = CurrentVersion };
            if (inventory != null)
            {
                d.coins = inventory.coins;
                d.capacity = inventory.capacity;
                inventory.SnapshotResources(out d.resTypes, out d.resAmounts);
            }
            if (tools != null) { d.axeTier = tools.axeTier; d.pickaxeTier = tools.pickaxeTier; d.weaponTier = tools.weaponTier; }
            if (mover != null) d.moveSpeed = mover.moveSpeed;
            if (health != null) d.health = health.CurrentHealth;
            if (shop != null) { d.capacityLevel = shop.CapacityLevel; d.speedLevel = shop.SpeedLevel; }
            if (campfire != null)
            {
                d.campfireTier = campfire.Tier;
                d.campfirePlaced = campfire.HasBuilt;
                d.campfirePos = campfire.BuiltPosition;
            }
            if (storage != null)
            {
                d.storageTier = storage.Tier;
                d.storagePlaced = storage.HasBuilt;
                d.storagePos = storage.BuiltPosition;
            }
            if (builder != null) builder.SnapshotBuildables(out d.buildIndices, out d.buildPositions, out d.buildRotY);
            if (objectives != null) d.objectiveIndex = objectives.Index;
            if (stats != null)
            {
                d.statWood = stats.GatheredWood;
                d.statStone = stats.GatheredStone;
                d.statTotal = stats.GatheredTotal;
                d.statKills = stats.CreaturesKilled;
            }

            File.WriteAllText(SavePath, JsonUtility.ToJson(d));
            dirty = false;
            lastSave = Time.time;
        }
        catch (Exception e) { Debug.LogWarning($"[SaveManager] save failed: {e.Message}"); }
    }

    public void Load()
    {
        try
        {
            if (!File.Exists(SavePath)) { lastSave = Time.time; return; }
            var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            // Accept any version we know (1..CurrentVersion). Older files just
            // deserialize with newer fields null (e.g. v1 has no buildables).
            if (d == null || d.version < 1 || d.version > CurrentVersion) { lastSave = Time.time; return; }

            if (inventory != null) inventory.LoadState(d.coins, d.capacity, d.resTypes, d.resAmounts);
            if (tools != null) { tools.axeTier = d.axeTier; tools.pickaxeTier = d.pickaxeTier; tools.weaponTier = d.weaponTier; }
            if (mover != null && d.moveSpeed > 0.01f) mover.moveSpeed = d.moveSpeed;  // guard: never lock movement
            if (health != null) health.LoadHealth(d.health);
            if (shop != null) shop.LoadLevels(d.capacityLevel, d.speedLevel);
            // Pre-v6 files carry no position — hasPos false falls back to the pad.
            bool hasPos = d.version >= 6;
            if (campfire != null) campfire.LoadTier(d.campfireTier, d.campfirePos, hasPos && d.campfirePlaced);
            if (storage != null) storage.LoadTier(d.storageTier, d.storagePos, hasPos && d.storagePlaced);
            if (builder != null)
            {
                MigrateBuildIndices(d);
                builder.LoadBuildables(d.buildIndices, d.buildPositions, d.buildRotY);
            }
            if (stats != null && d.version >= 5) stats.LoadStats(d.statWood, d.statStone, d.statTotal, d.statKills);
            if (objectives != null && d.objectiveIndex >= 0) objectives.LoadIndex(d.objectiveIndex);   // reads stats, so load stats first

            dirty = false;
            lastSave = Time.time;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] load failed, starting fresh: {e.Message}");
            lastSave = Time.time;
        }
    }

    // True when a run exists on disk. Read before Load() runs, so a fresh start can
    // be detected by anything that needs to treat run #1 differently (see
    // CreatureSpawner's predator grace period).
    public bool HasSave => File.Exists(SavePath);

    // Wired to the pause menu's "New Game" button.
    public void DeleteSave()
    {
        try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch { }
        // Progress lives in two places: the save file AND the PlayerPrefs gate on the
        // how-to-play card. Deleting only the file left the intro suppressed forever
        // after the first wipe. Mute stays — that's a setting, not progress.
        PlayerPrefs.DeleteKey(IntroTutorial.SeenKey);
        PlayerPrefs.Save();
    }
}
