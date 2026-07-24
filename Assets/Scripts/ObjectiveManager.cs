using System.Collections.Generic;
using UnityEngine;

// A simple linear goal chain: "earn 50 coins" -> "upgrade axe" -> "build a
// campfire" ... Each milestone is checked against live state whenever the
// inventory changes; completing one pays a coin reward and advances. Gives the
// grind direction without any per-frame counters.
public class ObjectiveManager : MonoBehaviour
{
    public enum Kind
    {
        Coins, AxeTier, PickaxeTier, WeaponTier, CampfireTier, StorageTier,
        GatherWood, GatherStone, KillCreatures
    }

    [System.Serializable]
    public class Objective
    {
        public string label;
        public Kind kind;
        public int target;
        public int reward;
        public Objective(string l, Kind k, int t, int r) { label = l; kind = k; target = t; reward = r; }
    }

    public event System.Action OnObjectiveChanged;
    // Fires once, at the moment the last goal is cleared during play. Deliberately
    // does NOT fire when a finished save is loaded, or the win screen would pop on
    // every boot forever.
    public event System.Action OnAllComplete;

    readonly List<Objective> list = new List<Objective>();
    int index;
    bool checking;

    PlayerInventory inv;
    ToolInventory tools;
    PlayerStats stats;
    UpgradeStation campfire, storage;

    void Awake()
    {
        inv = FindFirstObjectByType<PlayerInventory>();
        tools = FindFirstObjectByType<ToolInventory>();
        stats = FindFirstObjectByType<PlayerStats>();
        foreach (var s in FindObjectsByType<UpgradeStation>(FindObjectsSortMode.None))
        {
            if (s.kind == UpgradeStation.Kind.Campfire) campfire = s;
            else if (s.kind == UpgradeStation.Kind.Storage) storage = s;
        }

        // The chain has to span the ACTUAL progression, not just its first rung.
        // Maxing one tool costs ~1740 coins and the tool caps are Lv15, so a chain
        // ending at 500 coins (as it did) was spent inside the first quarter of one
        // upgrade track and then went silent for the rest of the game.
        // Tool levels are chosen to line up with the zone gates: Lv5 opens the
        // orchard + ore field, Lv10 the pine forest, Lv15 the deep poplar wood.

        // --- opening: teach the loop ---
        list.Add(new Objective("Chop 20 wood",          Kind.GatherWood,    20,    20));
        list.Add(new Objective("Earn 50 coins",         Kind.Coins,         50,    25));
        list.Add(new Objective("Upgrade axe to Lv2",    Kind.AxeTier,       2,     30));
        list.Add(new Objective("Mine 20 stone",         Kind.GatherStone,   20,    30));
        list.Add(new Objective("Build a campfire",      Kind.CampfireTier,  1,     40));
        list.Add(new Objective("Hunt 3 animals",        Kind.KillCreatures, 3,     40));
        list.Add(new Objective("Reach 200 coins",       Kind.Coins,         200,   50));
        list.Add(new Objective("Upgrade weapon Lv2",    Kind.WeaponTier,    2,     50));
        list.Add(new Objective("Build storage",         Kind.StorageTier,   1,     60));
        list.Add(new Objective("Reach 500 coins",       Kind.Coins,         500,   100));

        // --- mid: open the Lv5 zones, widen the camp ---
        list.Add(new Objective("Chop 100 wood",         Kind.GatherWood,    100,   80));
        list.Add(new Objective("Upgrade axe to Lv5",    Kind.AxeTier,       5,     120));
        list.Add(new Objective("Upgrade pickaxe to Lv5",Kind.PickaxeTier,   5,     120));
        list.Add(new Objective("Mine 150 stone",        Kind.GatherStone,   150,   150));
        list.Add(new Objective("Reach 1500 coins",      Kind.Coins,         1500,  200));
        list.Add(new Objective("Hunt 15 animals",       Kind.KillCreatures, 15,    200));
        list.Add(new Objective("Campfire to Lv2",       Kind.CampfireTier,  2,     200));
        list.Add(new Objective("Storage to Lv2",        Kind.StorageTier,   2,     200));

        // --- late: the pine forest and a real war chest ---
        list.Add(new Objective("Upgrade axe to Lv10",   Kind.AxeTier,       10,    350));
        list.Add(new Objective("Upgrade weapon to Lv5", Kind.WeaponTier,    5,     350));
        list.Add(new Objective("Reach 4000 coins",      Kind.Coins,         4000,  450));
        list.Add(new Objective("Chop 500 wood",         Kind.GatherWood,    500,   450));
        list.Add(new Objective("Storage to Lv4 (max)",  Kind.StorageTier,   4,     500));
        list.Add(new Objective("Campfire to Lv4 (max)", Kind.CampfireTier,  4,     500));

        // --- endgame: cap everything out ---
        list.Add(new Objective("Axe to Lv15 (max)",     Kind.AxeTier,       15,    700));
        list.Add(new Objective("Pickaxe to Lv15 (max)", Kind.PickaxeTier,   15,    700));
        list.Add(new Objective("Hunt 50 animals",       Kind.KillCreatures, 50,    700));
        list.Add(new Objective("Reach 10000 coins",     Kind.Coins,         10000, 1000));
        list.Add(new Objective("Weapon to Lv15 (max)",  Kind.WeaponTier,    15,    1500));
        list.Add(new Objective("Master the valley — 25000 coins", Kind.Coins, 25000, 2500));
    }

    void OnEnable()
    {
        if (inv != null) inv.OnInventoryChanged += Check;
        if (stats != null) stats.OnStatsChanged += Check;
    }
    void OnDisable()
    {
        if (inv != null) inv.OnInventoryChanged -= Check;
        if (stats != null) stats.OnStatsChanged -= Check;
    }

    void Start()
    {
        Check();
        OnObjectiveChanged?.Invoke();
    }

    void Check()
    {
        if (checking) return;               // AddCoins below re-fires OnInventoryChanged
        checking = true;
        bool wasAllDone = AllDone;          // so a finished save doesn't re-fire victory
        bool advanced = false;
        while (index < list.Count && Evaluate(list[index]) >= list[index].target)
        {
            if (inv != null && list[index].reward > 0) inv.AddCoins(list[index].reward);
            index++;
            advanced = true;
        }
        checking = false;
        OnObjectiveChanged?.Invoke();   // refresh HUD on every change, so partial progress (3/20) shows live
        if (advanced && !wasAllDone && AllDone) OnAllComplete?.Invoke();
    }

    int Evaluate(Objective o)
    {
        switch (o.kind)
        {
            case Kind.Coins:        return inv != null ? inv.coins : 0;
            case Kind.AxeTier:      return tools != null ? tools.axeTier : 0;
            case Kind.PickaxeTier:  return tools != null ? tools.pickaxeTier : 0;
            case Kind.WeaponTier:   return tools != null ? tools.weaponTier : 0;
            case Kind.CampfireTier: return campfire != null ? campfire.Tier : 0;
            case Kind.StorageTier:  return storage != null ? storage.Tier : 0;
            case Kind.GatherWood:   return stats != null ? stats.GatheredWood : 0;
            case Kind.GatherStone:  return stats != null ? stats.GatheredStone : 0;
            case Kind.KillCreatures:return stats != null ? stats.CreaturesKilled : 0;
            default: return 0;
        }
    }

    // --- HUD readouts ---
    public bool AllDone => index >= list.Count;
    public int Count => list.Count;
    public string CurrentLabel => AllDone ? "Valley mastered — every goal complete" : list[index].label;
    public int CurrentProgress => AllDone ? 0 : Mathf.Min(Evaluate(list[index]), list[index].target);
    public int CurrentTarget => AllDone ? 0 : list[index].target;

    // --- Save/load ---
    public int Index => index;
    public void LoadIndex(int i)
    {
        index = Mathf.Clamp(i, 0, list.Count);
        Check();                            // in case state already satisfies later goals
        OnObjectiveChanged?.Invoke();
    }
}
