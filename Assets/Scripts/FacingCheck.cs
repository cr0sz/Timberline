using UnityEngine;

// "Is that thing in front of me?" — shared by PlayerGatherer and PlayerCombat so the
// axe and the spear agree on what counts as facing a target.
//
// Both used to pick targets on DISTANCE ALONE, so you chopped a tree with your back
// to it and speared an animal standing behind you (user, 2026-07-23). Range still
// decides what is reachable; this decides what you are actually aimed at.
public static class FacingCheck
{
    // Targets outside this half-angle from `forward` are ignored. 70 degrees gives a
    // 140-degree cone: wide enough that you don't have to be pixel-perfect after
    // stopping, tight enough that anything behind you is clearly out.
    public const float DefaultHalfAngle = 70f;

    /// True when `target` lies inside a cone of `halfAngleDeg` around the object's
    /// forward. Compared on the horizontal plane only — looking slightly up or down a
    /// slope must not stop you chopping.
    public static bool InFront(Transform self, Vector3 target, float halfAngleDeg = DefaultHalfAngle)
    {
        Vector3 to = target - self.position;
        to.y = 0f;
        // Standing exactly on it: nothing sensible to aim at, so let it through rather
        // than making a target unreachable at point-blank range.
        if (to.sqrMagnitude < 0.0001f) return true;

        Vector3 fwd = self.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return true;

        float dot = Vector3.Dot(fwd.normalized, to.normalized);
        return dot >= Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
    }
}
