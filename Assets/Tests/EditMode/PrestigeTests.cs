using NUnit.Framework;

// Prestige is STATIC so it can survive the scene reload that performs the reset.
// That makes leakage the main risk: between tests, and — the real bug it guards —
// across a "New Game" wipe, where a stale count would quietly hand a fresh player a
// permanent earnings bonus. SaveManager.DeleteSave calls Prestige.Set(0) for that.
public class PrestigeTests
{
    [SetUp]
    [TearDown]
    public void Reset() => Prestige.Set(0);

    [Test]
    public void FirstRunHasNoBonus()
    {
        Assert.AreEqual(0, Prestige.ValleysMastered);
        Assert.AreEqual(1f, Prestige.EarningsMultiplier, 0.0001f);
        Assert.AreEqual(3, Prestige.Apply(3), "a first run must sell at the authored price");
        Assert.IsEmpty(Prestige.BonusLabel, "nothing to brag about on run one");
    }

    [Test]
    public void EachValleyAddsTwentyFivePercent()
    {
        Prestige.Set(1);
        Assert.AreEqual(1.25f, Prestige.EarningsMultiplier, 0.0001f);
        Prestige.Set(3);
        Assert.AreEqual(1.75f, Prestige.EarningsMultiplier, 0.0001f);
        Assert.AreEqual("+75%", Prestige.BonusLabel);
    }

    [Test]
    public void BonusRaisesSellPrices()
    {
        Prestige.Set(4);                       // x2.0
        Assert.AreEqual(6, Prestige.Apply(3),  "wood 3 -> 6");
        Assert.AreEqual(40, Prestige.Apply(20), "hide 20 -> 40");
    }

    [Test]
    public void MultiplierIsUniformSoPriceOrderingSurvives()
    {
        // EconomyTests pins that deep-zone resources out-earn early ones. A uniform
        // multiplier must not reorder them at any prestige level.
        int[] basePrices = { 3, 3, 10, 20 };
        for (int valleys = 0; valleys <= 8; valleys++)
        {
            Prestige.Set(valleys);
            for (int i = 1; i < basePrices.Length; i++)
            {
                if (basePrices[i] <= basePrices[i - 1]) continue;
                Assert.Greater(Prestige.Apply(basePrices[i]), Prestige.Apply(basePrices[i - 1]),
                               $"ordering broke at {valleys} valleys");
            }
        }
    }

    [Test]
    public void NegativeCountsAreClamped()
    {
        // A corrupt or hand-edited save must never produce a sub-1x multiplier, which
        // would silently make the game unwinnable.
        Prestige.Set(-5);
        Assert.AreEqual(0, Prestige.ValleysMastered);
        Assert.AreEqual(1f, Prestige.EarningsMultiplier, 0.0001f);
    }
}
