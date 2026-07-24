using UnityEngine;
using TMPro;

// Roblox-tycoon "buy pad". Walk onto it -> if you can afford the next tier it
// charges coins and builds/upgrades the structure at 'anchor'. No panel: the pad
// IS the button. A world label shows the next cost. Re-enter to buy the next tier.
public class UpgradeStation : MonoBehaviour
{
    public enum Kind { Campfire, Storage }

    [Header("What this pad builds")]
    public Kind kind = Kind.Campfire;
    public string displayName = "Campfire";

    [Header("Where the built structure goes")]
    public Transform anchor;                 // empty at the build spot; defaults to this pad
    public GameObject structurePrefab;        // campfire or crate prefab

    [Header("Cost")]
    public int baseCost = 50;
    public float costGrowth = 1.6f;
    public int maxTier = 4;

    [Header("Storage effect (Kind.Storage only)")]
    public int capacityPerTier = 25;

    [Header("Refs (auto-found if empty)")]
    public PlayerInventory inventory;
    public FloatingText floatingTextPrefab;
    public TMP_Text label;                    // world-space label above the pad

    [Header("Feel")]
    [Tooltip("Disc pulse speed while the player is standing on the pad. 0 = no pulse.")]
    public float pulseSpeed = 3.2f;
    [Tooltip("How much bigger the disc gets at the top of the pulse.")]
    public float pulseAmount = 0.12f;

    int tier = 0;                             // 0 = not built yet
    GameObject built;                         // the spawned structure instance

    // The pads have no open animation because they aren't SetActive panels, so PanelPop
    // never applied to them — they were the one interactive thing in the game with no
    // feedback at all. They get their own tell instead: the disc breathes while you're
    // standing on it, and punches once on a successful buy.
    Transform disc;
    Vector3 discBaseScale;
    bool playerOn;
    float punch;                              // 0..1, decays after a purchase

    public int Tier => tier;

    // Where the built structure currently STANDS, which is not the pad once the player
    // has dragged it somewhere (both structures carry a PlacedBuildable, so MOVE mode
    // can pick them up). SaveManager persists this so a relocated campfire stays put
    // across a reload instead of snapping back to its pad.
    public Vector3 BuiltPosition => built != null ? built.transform.position : anchor != null ? anchor.position : transform.position;
    public bool HasBuilt => built != null;

    // Restore a saved tier: rebuild the structure + apply visual effects, but do
    // NOT re-add storage capacity — SaveManager restores inventory.capacity
    // directly, so replaying it here would double-count.
    // `pos` is where the structure was last left; pass the pad's own position (or use
    // the single-arg overload) when there is no saved position.
    public void LoadTier(int t) => LoadTier(t, Vector3.zero, false);

    public void LoadTier(int t, Vector3 pos, bool hasPos)
    {
        if (anchor == null) anchor = transform;
        tier = Mathf.Clamp(t, 0, maxTier);
        // Campfire.SharedTier is static, so it survives a scene reload and would carry a
        // previous run's upgrades into a New Game. Reset it from the save on every load,
        // including the tier-0 "nothing built yet" case below.
        if (kind == Kind.Campfire) Campfire.SetSharedTier(Mathf.Max(1, tier));
        if (tier <= 0) { RefreshLabel(); return; }

        if (built == null && structurePrefab != null)
        {
            built = Instantiate(structurePrefab, hasPos ? pos : anchor.position, anchor.rotation);
            if (built.GetComponent<PlacedBuildable>() == null) built.AddComponent<PlacedBuildable>();
        }

        if (kind == Kind.Campfire) Campfire.SetSharedTier(tier);
        else if (built != null) // Storage: visual scale only; capacity restored elsewhere
        {
            built.transform.localScale = Vector3.one * (0.8f + tier * 0.15f);
        }
        RefreshLabel();
    }

    void Start()
    {
        if (anchor == null) anchor = transform;
        if (inventory == null) inventory = FindFirstObjectByType<PlayerInventory>();
        disc = transform.Find("Disc");
        if (disc != null) discBaseScale = disc.localScale;
        RefreshLabel();
    }

    void Update()
    {
        if (disc == null) return;

        // Breathe while the player stands here; snap back to rest once they leave.
        float pulse = playerOn && pulseSpeed > 0f
            ? Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f
            : 0f;
        float k = 1f + pulse * pulseAmount + punch * 0.35f;
        // Discs are flat cylinders — scaling Y as well would make the pad rise out of
        // the ground, so the pulse is horizontal only.
        disc.localScale = new Vector3(discBaseScale.x * k, discBaseScale.y, discBaseScale.z * k);

        if (punch > 0f) punch = Mathf.Max(0f, punch - Time.deltaTime * 3f);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() != null) playerOn = false;
    }

    int NextCost() => Mathf.RoundToInt(baseCost * Mathf.Pow(costGrowth, tier));

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;
        playerOn = true;
        TryUpgrade();
    }

    void TryUpgrade()
    {
        if (tier >= maxTier) { Toast($"{displayName} MAX"); return; }
        int cost = NextCost();
        if (inventory == null || !inventory.CanAffordCoins(cost)) { Toast($"Need {cost} coins"); return; }

        inventory.SpendCoins(cost);
        tier++;
        ApplyTier();
        punch = 1f;                 // the pad kicks so the buy is felt, not just read
        Toast($"-{cost}  {displayName} Lv{tier}");
        RefreshLabel();
    }

    void ApplyTier()
    {
        // Spawn the structure on first build.
        if (built == null && structurePrefab != null)
        {
            built = Instantiate(structurePrefab, anchor.position, anchor.rotation);
            // catalogIndex stays -1: this came from a pad, not the build catalog, so
            // BuildSystem must NOT try to re-save it as a catalog buildable. MOVE mode
            // only needs the marker to exist, and handles index -1 fine.
            if (built.GetComponent<PlacedBuildable>() == null) built.AddComponent<PlacedBuildable>();
        }

        if (kind == Kind.Campfire)
        {
            // Campfires are placeable from the build catalog now, so a player can own
            // several. The pad upgrades ALL of them (and any placed later) rather than
            // only the one it happens to have spawned — paying once to upgrade "the"
            // campfire and finding your other two still weak would read as a bug.
            Campfire.SetSharedTier(tier);
        }
        else // Storage
        {
            if (inventory != null) inventory.AddCapacity(capacityPerTier);
            if (built != null) built.transform.localScale = Vector3.one * (0.8f + tier * 0.15f);
        }
    }

    void RefreshLabel()
    {
        if (label == null) return;
        label.text = tier >= maxTier
            ? $"{displayName}\n<size=70%>MAX</size>"
            : $"{displayName} Lv{tier}\n<size=70%><color=#F2C14E>{NextCost()}</color> ↑</size>";
    }

    void Toast(string m)
    {
        if (floatingTextPrefab == null) return;
        FloatingText.Spawn(floatingTextPrefab, transform.position + Vector3.up * 2.2f, m);
    }
}
