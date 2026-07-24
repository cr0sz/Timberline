using NUnit.Framework;
using UnityEngine;

// The campfire's repel geometry. Creature reads RepelDepth every frame to decide
// whether a predator must back out of camp, so the shape of that function is the
// whole "camp is safe ground" feature.
public class CampfireTests
{
    GameObject go;
    Campfire fire;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("TestCampfire");
        go.transform.position = Vector3.zero;
        fire = go.AddComponent<Campfire>();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(go);

    [Test]
    public void RepelRadius_AlwaysExceedsHealRadius()
    {
        // If the safe pocket were the same size as the healing pocket, a predator
        // could stand exactly on the line you need to heal from.
        for (int tier = 1; tier <= 4; tier++)
        {
            fire.SetTier(tier);
            Assert.Greater(fire.repelRadius, fire.radius, $"tier {tier}: predators must be pushed outside the heal zone");
        }
    }

    [Test]
    public void BothRadii_GrowWithTier()
    {
        fire.SetTier(1);
        float r1 = fire.radius, p1 = fire.repelRadius;
        fire.SetTier(4);
        Assert.Greater(fire.radius, r1, "upgrading must widen the heal zone");
        Assert.Greater(fire.repelRadius, p1, "upgrading must widen the safe zone");
    }

    [Test]
    public void RepelDepth_IsZeroOutsideAndPositiveInside()
    {
        fire.SetTier(1);
        float r = fire.repelRadius;

        Assert.AreEqual(0f, fire.RepelDepth(new Vector3(r + 1f, 0f, 0f)), "clear of the fire = no push");
        Assert.AreEqual(0f, fire.RepelDepth(new Vector3(r, 0f, 0f)), "exactly on the edge = no push");
        Assert.Greater(fire.RepelDepth(Vector3.zero), 0f, "dead centre = maximum push");
    }

    [Test]
    public void RepelDepth_IsDeepestAtTheCentre()
    {
        fire.SetTier(2);
        float atCentre = fire.RepelDepth(Vector3.zero);
        float halfway = fire.RepelDepth(new Vector3(fire.repelRadius * 0.5f, 0f, 0f));
        Assert.Greater(atCentre, halfway, "depth must fall off with distance so the push points outward");
        Assert.AreEqual(fire.repelRadius, atCentre, 0.001f);
    }

    [Test]
    public void RepelDepth_MeasuresIn3D_SoHeightWeakensThePush()
    {
        // Worth pinning down rather than assuming: RepelDepth uses a full 3D distance,
        // so an animal standing on a deck or a rock above the fire is treated as
        // FURTHER out than one at ground level the same horizontal distance away.
        // Fine while the terrain is flat; if verticality ever matters, flatten the Y
        // here and in Creature.RepelDirection together.
        fire.SetTier(1);
        float ground = fire.RepelDepth(new Vector3(1f, 0f, 0f));
        float raised = fire.RepelDepth(new Vector3(1f, 3f, 0f));
        Assert.Greater(ground, raised);
    }
}
