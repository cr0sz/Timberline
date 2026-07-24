using UnityEngine;

// "New Valley" — the post-win reset.
//
// The 30-goal chain ends with every upgrade capped (tools Lv15, bag Lv12, speed
// Lv10, both pads Lv4), so coins stop meaning anything and the game dead-ends.
// Prestige fixes the SINK, not the goal list: you hand the valley back, start the
// whole curve again, and keep a permanent cut on everything you sell.
//
// Static like Campfire.SharedTier, because it has to survive the scene reload that
// performs the reset. That also means it survives a "New Game" wipe, which is a bug
// waiting to happen — SaveManager.DeleteSave zeroes it explicitly for exactly that
// reason, and Load sets it on every boot.
public static class Prestige
{
    // Per valley mastered. 25% compounds fast enough to feel like a reward by the
    // second run without trivialising the third.
    public const float BonusPerValley = 0.25f;

    public static int ValleysMastered { get; private set; }

    /// <summary>Multiplier on every coin earned from selling. 1.0 on a first run.</summary>
    public static float EarningsMultiplier => 1f + BonusPerValley * ValleysMastered;

    public static void Set(int valleys) => ValleysMastered = Mathf.Max(0, valleys);

    /// <summary>Sell price after the prestige cut. Rounds up off .5 like the rest of the shop.</summary>
    public static int Apply(int basePrice) => Mathf.RoundToInt(basePrice * EarningsMultiplier);

    /// <summary>"+50%" for the HUD/victory panel. Empty on a first run — nothing to brag about yet.</summary>
    public static string BonusLabel =>
        ValleysMastered <= 0 ? "" : $"+{Mathf.RoundToInt(BonusPerValley * ValleysMastered * 100f)}%";
}
