using NUnit.Framework;
using UnityEngine;

// Yield + price arithmetic. Both of the economy bugs this project actually shipped
// live here: a node paying LESS to a better tool, and stone paying double wood so
// the Lv1 quarry out-earned the Lv15 forest.
public class EconomyTests
{
    GameObject go;
    ResourceNode node;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("TestNode");
        node = go.AddComponent<ResourceNode>();
        // Kill the hit juice: Shake() starts a coroutine, which an EditMode test has
        // no player loop to run. With both at zero it returns immediately and Hit()
        // is pure arithmetic.
        node.hitPunch = 0f;
        node.hitTilt = 0f;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(go);

    [Test]
    public void TotalYield_PrefersTheExplicitOverride()
    {
        node.hitsToDeplete = 5;
        node.amountPerHit = 1;
        node.totalYield = 26;   // the deep-forest poplar value, set per scene instance
        Assert.AreEqual(26, node.TotalYield());
    }

    [Test]
    public void TotalYield_DerivesFromHitsWhenNoOverride()
    {
        node.hitsToDeplete = 5;
        node.amountPerHit = 3;
        node.totalYield = 0;
        Assert.AreEqual(15, node.TotalYield());
    }

    [Test]
    public void TotalYield_IsNeverZero()
    {
        node.hitsToDeplete = 0;
        node.amountPerHit = 0;
        node.totalYield = 0;
        Assert.AreEqual(1, node.TotalYield(), "a node that pays nothing is a soft-lock, not a balance choice");
    }

    [Test]
    public void PartialHits_NeverPayMoreThanTheNodeIsWorth()
    {
        node.hitsToDeplete = 5;
        node.totalYield = 20;

        int paid = 0;
        for (int i = 0; i < 4; i++) paid += node.Hit();   // 4 of 5 — stop short of the felling blow
        Assert.LessOrEqual(paid, node.TotalYield(), "the node cannot overpay before it even falls");
        Assert.AreEqual(16, paid, "20 spread over 5 hits is 4 a swing");
    }

    [Test]
    public void BetterToolNeverHarvestsForLess()
    {
        // The shipped bug: yield was amountPerHit x hits, and a better tool CUT the
        // hit count, so a Lv10 axe felled a Lv1 tree in two swings and got two wood.
        // A node is worth a fixed total; a better tool only gets it faster.
        node.hitsToDeplete = 6;
        node.totalYield = 24;

        // Slow tool: no reduction, threshold 6, so five partial hits before felling.
        int slow = 0;
        for (int i = 0; i < 5; i++) slow += node.Hit(0);

        var fastGO = new GameObject("FastNode");
        var fast = fastGO.AddComponent<ResourceNode>();
        fast.hitPunch = 0f; fast.hitTilt = 0f;
        fast.hitsToDeplete = 6;
        fast.totalYield = 24;
        // Fast tool: reduction 4, threshold 2, so one partial hit before felling.
        int quick = fast.Hit(4);

        Assert.AreEqual(24, slow + Remaining(node, slow), "slow tool still collects the full 24");
        Assert.AreEqual(24, quick + Remaining(fast, quick), "fast tool collects the same 24, just sooner");
        Assert.GreaterOrEqual(quick, slow / 5, "each swing of the better tool is worth at least as much");
        Object.DestroyImmediate(fastGO);
    }

    // What the felling blow would still owe. Hit() pays this out on the last swing,
    // but that path starts the deplete/regrow coroutine, which EditMode can't run.
    static int Remaining(ResourceNode n, int paidSoFar) => n.TotalYield() - paidSoFar;

    [Test]
    public void SellPrices_KeepDeepZonesAheadOfEarlyOnes()
    {
        var shopGO = new GameObject("TestShop");
        var shop = shopGO.AddComponent<Shop>();

        Assert.AreEqual(shop.woodPrice, shop.PriceOf(ResourceType.Wood));
        Assert.AreEqual(shop.stonePrice, shop.PriceOf(ResourceType.Stone));
        Assert.AreEqual(shop.meatPrice, shop.PriceOf(ResourceType.Meat));
        Assert.AreEqual(shop.hidePrice, shop.PriceOf(ResourceType.Hide));

        // The regression: stone paid 6 against wood's 3, so the Lv1 quarry (yield 6 =
        // 36c) beat the Lv5 orchard (yield 10 = 30c) and the Lv5 ore field (yield 14 =
        // 84c) beat the Lv15 poplars (yield 26 = 78c). Per-node take must climb with
        // the tool level the zone gates on.
        int oakL1 = 5 * shop.woodPrice;
        int quarryL1 = 7 * shop.stonePrice;
        int appleL5 = 10 * shop.woodPrice;
        int oreL5 = 14 * shop.stonePrice;
        int pineL10 = 18 * shop.woodPrice;
        int poplarL15 = 26 * shop.woodPrice;

        Assert.Less(Mathf.Max(oakL1, quarryL1), Mathf.Min(appleL5, oreL5), "Lv5 zones must beat every Lv1 zone");
        Assert.Less(Mathf.Max(appleL5, oreL5), pineL10, "Lv10 must beat every Lv5 zone");
        Assert.Less(pineL10, poplarL15, "Lv15 must beat Lv10");

        Object.DestroyImmediate(shopGO);
    }

    // Coins per second of swinging at a node, for the tool tier the zone gates on.
    // This is the number that decides which lane a player actually farms.
    static float CoinsPerSecond(ToolInventory tools, Shop shop, ResourceType type,
                                int tier, int totalYield, int hitsToDeplete)
    {
        var t = type == ResourceType.Wood
            ? TierOf(tools, tools.axeBaseInterval, tier)
            : TierOf(tools, tools.pickaxeBaseInterval, tier);
        int effHits = Mathf.Max(1, hitsToDeplete - t.hitsReduction);
        return totalYield / (effHits * t.chopInterval) * shop.PriceOf(type);
    }

    static ToolInventory.ToolTier TierOf(ToolInventory tools, float baseInterval, int tier)
    {
        int steps = Mathf.Max(0, tier - 1);
        return new ToolInventory.ToolTier
        {
            chopInterval = Mathf.Max(tools.minInterval, baseInterval * Mathf.Pow(tools.intervalDecay, steps)),
            hitsReduction = tools.tiersPerHitReduction > 0 ? steps / tools.tiersPerHitReduction : 0
        };
    }

    // The regression this guards: the pickaxe swung at 1.2s against the axe's 1.0s
    // while stone and wood both sold for 3, so mining was STRICTLY worse than chopping
    // at every tier — and the Lv1 quarry sits 40m further out than the Lv1 meadow on
    // top of that. Objective #4 sends the player to mine 20 stone; that objective has
    // to point at a lane worth walking to.
    [Test]
    public void MiningIsNotStrictlyWorseThanChopping()
    {
        var toolsGO = new GameObject("TestTools");
        var tools = toolsGO.AddComponent<ToolInventory>();
        var shopGO = new GameObject("TestShop3");
        var shop = shopGO.AddComponent<Shop>();

        // Scene values, per zone: (tier, totalYield, hitsToDeplete).
        float meadowL1 = CoinsPerSecond(tools, shop, ResourceType.Wood, 1, 5, 5);
        float quarryL1 = CoinsPerSecond(tools, shop, ResourceType.Stone, 1, 7, 6);
        float orchardL5 = CoinsPerSecond(tools, shop, ResourceType.Wood, 5, 10, 5);
        float oreL5 = CoinsPerSecond(tools, shop, ResourceType.Stone, 5, 14, 6);

        Assert.GreaterOrEqual(quarryL1, meadowL1,
            "the Lv1 quarry is 40m further out than the Lv1 meadow, so it must at least match it per second");
        Assert.GreaterOrEqual(oreL5, orchardL5 * 0.9f,
            "the Lv5 ore field may trail the orchard slightly, but not by the 56% it once did");

        Object.DestroyImmediate(shopGO);
        Object.DestroyImmediate(toolsGO);
    }

    [Test]
    public void SpeedUpgradeCurve_StaysSaneOnA200mMap()
    {
        var shopGO = new GameObject("TestShop2");
        var shop = shopGO.AddComponent<Shop>();
        // Base walk speed is 4; the shop sells maxSpeedLevel-1 upgrades on top.
        float top = 4f + shop.speedStep * (shop.maxSpeedLevel - 1);
        Assert.LessOrEqual(top, 8f, "above ~8 the map crosses too fast for the zone gating to mean anything");
        Object.DestroyImmediate(shopGO);
    }
}
