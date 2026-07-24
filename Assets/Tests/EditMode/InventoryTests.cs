using NUnit.Framework;
using UnityEngine;

// The project shipped with zero automated tests, which is why two silent regressions
// survived for whole sessions (the objective bar always rendering full, and the
// backwards yield scaling that made a better axe pay LESS). These cover the pure
// logic — no scene, no play mode — so the arithmetic that decides the economy has a
// floor under it.
public class InventoryTests
{
    PlayerInventory inv;
    GameObject go;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("TestInventory");
        inv = go.AddComponent<PlayerInventory>();
        inv.capacity = 10;
        inv.coins = 0;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(go);

    [Test]
    public void Add_ReturnsWhatFit_AndClampsToCapacity()
    {
        Assert.AreEqual(6, inv.Add(ResourceType.Wood, 6), "should take all 6 into an empty 10-slot bag");
        Assert.AreEqual(4, inv.Add(ResourceType.Stone, 9), "only 4 slots left, so only 4 fit");
        Assert.AreEqual(10, inv.TotalCarried());
        Assert.IsTrue(inv.IsFull);
    }

    [Test]
    public void Add_IntoFullBag_TakesNothing()
    {
        inv.Add(ResourceType.Wood, 10);
        Assert.AreEqual(0, inv.Add(ResourceType.Wood, 5));
        Assert.AreEqual(10, inv.TotalCarried());
    }

    [Test]
    public void Spend_OnResourceNeverGathered_DoesNotThrow()
    {
        // Indexing a missing dictionary key would throw KeyNotFoundException; the
        // guard in Spend exists precisely so a caller that forgets CanAfford can't
        // crash the run.
        Assert.DoesNotThrow(() => inv.Spend(ResourceType.Hide, 3));
        Assert.AreEqual(0, inv.GetAmount(ResourceType.Hide));
    }

    [Test]
    public void Spend_ClampsAtZero()
    {
        inv.Add(ResourceType.Wood, 3);
        inv.Spend(ResourceType.Wood, 99);
        Assert.AreEqual(0, inv.GetAmount(ResourceType.Wood), "must never go negative");
    }

    [Test]
    public void SpendCoins_ClampsAtZero()
    {
        inv.AddCoins(50);
        inv.SpendCoins(80);
        Assert.AreEqual(0, inv.coins);
    }

    [Test]
    public void LoseFraction_RoundsUp_SoDeathIsNeverFree()
    {
        inv.Add(ResourceType.Wood, 1);
        // 1 unit at 30% rounds up to 1 — dying while carrying a single item must
        // still cost that item, or death is free at low hauls.
        Assert.AreEqual(1, inv.LoseFraction(0.3f));
        Assert.AreEqual(0, inv.GetAmount(ResourceType.Wood));
    }

    [Test]
    public void LoseFraction_NeverDropsMoreThanHeld()
    {
        inv.Add(ResourceType.Wood, 4);
        Assert.AreEqual(4, inv.LoseFraction(2f), "a fraction above 1 still can't take more than you carry");
        Assert.AreEqual(0, inv.GetAmount(ResourceType.Wood));
    }

    [Test]
    public void LoseFraction_OnEmptyBag_IsZero()
    {
        Assert.AreEqual(0, inv.LoseFraction(0.5f));
    }

    [Test]
    public void SellAll_PaysPerUnitPrice_AndEmptiesTheBag()
    {
        inv.Add(ResourceType.Wood, 3);    // 3 x 3 = 9
        inv.Add(ResourceType.Hide, 2);    // 2 x 20 = 40
        int earned = inv.SellAll(t => t == ResourceType.Wood ? 3 : t == ResourceType.Hide ? 20 : 1);
        Assert.AreEqual(49, earned);
        Assert.AreEqual(49, inv.coins);
        Assert.AreEqual(0, inv.TotalCarried(), "selling must clear the carry");
    }

    [Test]
    public void SellAll_WithNothingCarried_EarnsNothing()
    {
        Assert.AreEqual(0, inv.SellAll(t => 10));
        Assert.AreEqual(0, inv.coins);
    }

    [Test]
    public void SaveRoundTrip_RestoresExactly_AndDoesNotDoubleCapacity()
    {
        inv.Add(ResourceType.Wood, 5);
        inv.Add(ResourceType.Stone, 2);
        inv.AddCoins(123);
        inv.AddCapacity(25);                     // capacity now 35
        inv.SnapshotResources(out var types, out var amounts);
        int savedCap = inv.capacity, savedCoins = inv.coins;

        // Reload into a fresh inventory, the way SaveManager does on boot.
        var go2 = new GameObject("Reloaded");
        var inv2 = go2.AddComponent<PlayerInventory>();
        inv2.LoadState(savedCoins, savedCap, types, amounts);

        Assert.AreEqual(123, inv2.coins);
        Assert.AreEqual(35, inv2.capacity, "capacity is SET, never re-added — replaying AddCapacity would double the bag");
        Assert.AreEqual(5, inv2.GetAmount(ResourceType.Wood));
        Assert.AreEqual(2, inv2.GetAmount(ResourceType.Stone));
        Object.DestroyImmediate(go2);
    }

    [Test]
    public void LoadState_WithNullArrays_ClearsRatherThanThrows()
    {
        inv.Add(ResourceType.Wood, 4);
        Assert.DoesNotThrow(() => inv.LoadState(10, 50, null, null), "a v1 save has no resource arrays");
        Assert.AreEqual(0, inv.TotalCarried());
        Assert.AreEqual(10, inv.coins);
    }
}
