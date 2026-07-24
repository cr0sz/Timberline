using UnityEngine;

public class ToolInventory : MonoBehaviour
{
    public struct ToolTier
    {
        public float chopInterval;   // seconds between swings (lower = faster)
        public int hitsReduction;    // trees/rocks die in this many fewer hits
    }

    [Header("Axe (chops Wood)")]
    public float axeBaseInterval = 1f;      // tier-1 swing speed
    [Header("Pickaxe (mines Stone)")]
    // Was 1.2 — a slower swing than the axe for no design reason. Stone and wood sell
    // for the same 3 coins, so the extra 0.2s made stone STRICTLY worse than wood at
    // every tier (17% less income per second at Lv1, 56% less at Lv5) while the Lv1
    // quarry also sits 40m further from the shop than the Lv1 meadow. Objective #4
    // ("Mine 20 stone") was pointing the player at the worse of the two lanes.
    public float pickaxeBaseInterval = 1f;

    [Header("Weapon (fights creatures)")]
    public float weaponBaseDamage = 4f;      // tier-1 hit
    [Tooltip("Each tier multiplies damage by this. 1.25 = +25% per level.")]
    public float weaponDamageGrowth = 1.25f;
    public float weaponBaseInterval = 0.8f;  // seconds between swings at tier 1
    public int weaponTier = 1;

    [Header("Scaling (shared) — tuned by formula, not a fixed table")]
    [Tooltip("Each tier multiplies the swing interval by this. 0.93 = ~7% faster per level.")]
    public float intervalDecay = 0.93f;
    [Tooltip("Fastest possible swing, so high tiers don't hit zero.")]
    public float minInterval = 0.2f;
    [Tooltip("Gain +1 hitsReduction every this many tiers.")]
    public int tiersPerHitReduction = 2;

    public int axeTier = 1;
    public int pickaxeTier = 1;

    // Stats now come from a formula, so buying tiers 4..15 keeps improving the tool
    // instead of flatlining at the old 3-entry array's last row.
    public ToolTier GetToolFor(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:  return TierStats(axeBaseInterval, axeTier);
            case ResourceType.Stone: return TierStats(pickaxeBaseInterval, pickaxeTier);
            default:                 return new ToolTier { chopInterval = 1f, hitsReduction = 0 };
        }
    }

    ToolTier TierStats(float baseInterval, int tier)
    {
        int steps = Mathf.Max(0, tier - 1);
        float interval = Mathf.Max(minInterval, baseInterval * Mathf.Pow(intervalDecay, steps));
        int reduction = tiersPerHitReduction > 0 ? steps / tiersPerHitReduction : 0;
        return new ToolTier { chopInterval = interval, hitsReduction = reduction };
    }

    // Weapon damage grows geometrically with tier (Step 4 attack loop reads this).
    public int GetWeaponDamage()
    {
        int steps = Mathf.Max(0, weaponTier - 1);
        return Mathf.RoundToInt(weaponBaseDamage * Mathf.Pow(weaponDamageGrowth, steps));
    }

    public float GetWeaponInterval() => weaponBaseInterval;

    public int GetTierFor(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:  return axeTier;
            case ResourceType.Stone: return pickaxeTier;
            default: return 999;   // no tool needed
        }
    }
}
