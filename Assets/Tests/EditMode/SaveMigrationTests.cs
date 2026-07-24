using System.Linq;
using NUnit.Framework;

// Pins the save migration that runs when a catalog entry is cut. Getting this wrong
// is silent and destructive: the indices are raw array positions, so an off-by-one
// rebuilds a player's whole camp as the wrong prefabs on next load.
//
// v7 cut "Crate" at index 10, moving Campfire 11 -> 10.
public class SaveMigrationTests
{
    const int Crate = 10;

    [Test]
    public void EntriesBeforeTheCutKeepTheirIndex()
    {
        var kept = SaveManager.RemapAfterCut(new[] { 0, 5, 9 }, Crate);
        CollectionAssert.AreEqual(new[] { 0, 5, 9 }, kept.Select(k => k.newIndex).ToArray());
    }

    [Test]
    public void EntriesAfterTheCutShiftDownByOne()
    {
        // Campfire was 11 and must come back as 10, not as the cut crate.
        var kept = SaveManager.RemapAfterCut(new[] { 11 }, Crate);
        Assert.AreEqual(1, kept.Count);
        Assert.AreEqual(10, kept[0].newIndex);
    }

    [Test]
    public void CutEntriesAreDroppedNotRemapped()
    {
        var kept = SaveManager.RemapAfterCut(new[] { Crate, Crate }, Crate);
        Assert.IsEmpty(kept, "placed crates must vanish, never become another buildable");
    }

    [Test]
    public void SurvivingSlotsPointBackAtTheirOriginalPosition()
    {
        // Slot is what lets the caller carry positions/rotations across in step.
        // Input: fence(0), crate(10), campfire(11)  ->  fence keeps slot 0, campfire slot 2.
        var kept = SaveManager.RemapAfterCut(new[] { 0, Crate, 11 }, Crate);
        CollectionAssert.AreEqual(new[] { 0, 2 }, kept.Select(k => k.slot).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 10 }, kept.Select(k => k.newIndex).ToArray());
    }

    [Test]
    public void NullAndEmptyAreHandled()
    {
        Assert.IsEmpty(SaveManager.RemapAfterCut(null, Crate));
        Assert.IsEmpty(SaveManager.RemapAfterCut(new int[0], Crate));
    }
}
