using NUnit.Framework;
using UnityEngine;

// The "am I looking at it?" cone shared by gathering and combat. Before this existed
// both picked targets on distance alone, so you chopped a tree with your back to it
// and speared an animal standing behind you.
public class FacingTests
{
    GameObject go;
    Transform self;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("FacingDummy");
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.LookRotation(Vector3.forward);   // facing +Z
        self = go.transform;
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(go);

    [Test]
    public void DeadAhead_IsInFront()
    {
        Assert.IsTrue(FacingCheck.InFront(self, new Vector3(0f, 0f, 5f)));
    }

    [Test]
    public void DirectlyBehind_IsNot()
    {
        Assert.IsFalse(FacingCheck.InFront(self, new Vector3(0f, 0f, -5f)),
            "this is the whole point: no chopping a tree at your back");
    }

    [Test]
    public void DirectlySideways_IsOutsideTheDefaultCone()
    {
        // 90 degrees off, against a 70-degree half-angle.
        Assert.IsFalse(FacingCheck.InFront(self, new Vector3(5f, 0f, 0f)));
    }

    [Test]
    public void ForwardDiagonal_IsInFront()
    {
        // 45 degrees off — comfortably inside 70, so you don't have to be perfectly
        // squared up after coming to a stop.
        Assert.IsTrue(FacingCheck.InFront(self, new Vector3(3f, 0f, 3f)));
        Assert.IsTrue(FacingCheck.InFront(self, new Vector3(-3f, 0f, 3f)));
    }

    [Test]
    public void HeightIsIgnored()
    {
        // Measured on the horizontal plane only: a node up a slope, or an animal in a
        // ditch, must not fall out of the cone just for being higher or lower.
        Assert.IsTrue(FacingCheck.InFront(self, new Vector3(0f, 20f, 3f)));
        Assert.IsTrue(FacingCheck.InFront(self, new Vector3(0f, -20f, 3f)));
    }

    [Test]
    public void StandingExactlyOnTarget_IsAllowed()
    {
        // No sensible direction to test; refusing here would make a target unreachable
        // at point-blank range.
        Assert.IsTrue(FacingCheck.InFront(self, Vector3.zero));
    }

    [Test]
    public void WiderAngle_LetsMoreThrough()
    {
        Vector3 side = new Vector3(5f, 0f, 0f);
        Assert.IsFalse(FacingCheck.InFront(self, side, 70f));
        Assert.IsTrue(FacingCheck.InFront(self, side, 100f), "a wider cone must admit a sideways target");
    }

    [Test]
    public void OneEightyAcceptsEverything()
    {
        // The documented "off switch" for the check.
        Assert.IsTrue(FacingCheck.InFront(self, new Vector3(0f, 0f, -5f), 180f));
    }

    [Test]
    public void RotatingTheActorMovesTheCone()
    {
        var target = new Vector3(5f, 0f, 0f);
        Assert.IsFalse(FacingCheck.InFront(self, target));
        self.rotation = Quaternion.LookRotation(Vector3.right);   // turn to face it
        Assert.IsTrue(FacingCheck.InFront(self, target));
    }
}
