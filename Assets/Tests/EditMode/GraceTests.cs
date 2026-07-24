using NUnit.Framework;

// First-run predator grace. A brand-new player used to be hunted from second zero,
// which read as unfair rather than hard. Prey are never held back — the grace makes
// the world safe, not empty.
public class GraceTests
{
    const float Ends = 360f;   // Time.time at which predators are released

    [Test]
    public void PredatorHeldDuringGrace()
    {
        Assert.IsTrue(CreatureSpawner.IsHeld(Ends, 0f, 0f, true));
        Assert.IsTrue(CreatureSpawner.IsHeld(Ends, 0f, 359.9f, true));
    }

    [Test]
    public void PredatorReleasedAtAndAfterExpiry()
    {
        Assert.IsFalse(CreatureSpawner.IsHeld(Ends, 0f, Ends, true));
        Assert.IsFalse(CreatureSpawner.IsHeld(Ends, 0f, 1000f, true));
    }

    [Test]
    public void PreyNeverHeld()
    {
        Assert.IsFalse(CreatureSpawner.IsHeld(0f, 0f, 0f, true));
        Assert.IsFalse(CreatureSpawner.IsHeld(0f, 0f, 1000f, true));
    }

    // graceEnds == 0 is how a returning player is encoded: the save existed, so no
    // grace was ever armed. Reloading a run must not re-arm it.
    [Test]
    public void NoGraceArmedMeansNothingHeld()
    {
        Assert.IsFalse(CreatureSpawner.IsHeld(Ends, 0f, 0f, false));
        Assert.IsFalse(CreatureSpawner.IsHeld(0f, 0f, 0f, false));
    }
}
