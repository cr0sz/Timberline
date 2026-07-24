using System.Collections.Generic;
using UnityEngine;

// Sits on a built campfire. Three jobs:
//   1. HEALS the player while they stand within radius (subject to PlayerHealth's
//      post-hit lockout, so you can't out-heal a predator by standing in it).
//   2. REPELS predators — Creature reads the static registry and refuses to enter
//      repelRadius. This is the job the torch used to pretend to have; the torch was
//      cut 2026-07-23 because the game has no night, so "fire keeps them away" now
//      lives on the one fire that costs something to build.
//   3. Acts as the player's RESPAWN POINT once built, so dying sends you back to
//      camp instead of the map's original spawn.
// Tier scales heal rate, both radii and the light; UpgradeStation calls SetTier on
// build/upgrade. It used to cook Meat into Food as well; that went out with the
// hunger system (see docs/superpowers/specs/2026-07-21-hunger-cut-design.md).
public class Campfire : MonoBehaviour
{
    public float radius = 4f;
    public float healPerSecond = 4f;
    [Tooltip("Predators refuse to come closer than this. Wider than the heal radius " +
             "so the safe pocket is bigger than the healing pocket — otherwise a bear " +
             "stands exactly on the line you need to heal from.")]
    public float repelRadius = 7f;
    public Light fireLight;          // optional — brightened per tier

    // The scare radius is shown ONLY while you are positioning a campfire, on the
    // placement ghost (see PlacementController). A permanent ring around every fire was
    // tried and rejected — three fires meant three orange circles painted across camp
    // at all times (user, 2026-07-23).
    public static readonly Color RingColor = new Color(1f, 0.55f, 0.18f, 0.85f);

    // Every lit campfire in the scene. Creature scans this instead of a
    // FindObjectsByType every frame. ponytail: a plain list — there is one campfire
    // in practice and a handful at worst, so no spatial index.
    public static readonly List<Campfire> All = new List<Campfire>();

    // Campfires are now BUILDABLES (capped at 3 in the catalog), not just the one
    // structure the tycoon pad spawned — so "which tier is this fire?" can't live on
    // the pad any more. One shared tier for every fire: the pad upgrades it, and a
    // freshly placed fire picks it up in Start, so a new fire is never weaker than
    // the ones you already paid to upgrade.
    public static int SharedTier = 1;

    /// Set the tier for every campfire, present and future. Called by the pad.
    public static void SetSharedTier(int tier)
    {
        SharedTier = Mathf.Max(1, tier);
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null) All[i].SetTier(SharedTier);
    }

    PlayerHealth player;
    float buffer;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        if (player == null)
        {
            var ph = FindFirstObjectByType<PlayerHealth>();
            if (ph != null) player = ph;
        }
        // Once there's a fire, camp is where you wake up. Last campfire built wins,
        // which is what you want when the player relocates their base.
        if (player != null) player.spawnPoint = transform;
        VFXManager.AttachCampfire(transform);   // looping smoke + embers
        // A fire dropped from the build catalog has never been tiered — adopt the tier
        // the player already bought rather than sitting at the raw field defaults.
        SetTier(SharedTier);
    }

    // What repelRadius a fire at `tier` will end up with. PlacementController needs this
    // BEFORE the fire exists, to size the preview ring on the ghost — so the maths has
    // to live somewhere both can reach. Keep in step with SetTier.
    public static float RepelRadiusForTier(int tier) => (3.5f + Mathf.Max(1, tier) * 0.75f) + 2.5f;

    // Called by UpgradeStation each time the campfire is built/upgraded.
    public void SetTier(int tier)
    {
        radius = 3.5f + tier * 0.75f;        // Lv1 ~4.25m -> grows
        healPerSecond = 3f + tier * 2f;      // Lv1 5/s -> faster
        repelRadius = radius + 2.5f;         // safe pocket always outruns the heal pocket
        float s = 0.8f + tier * 0.25f;       // fire gets bigger
        transform.localScale = Vector3.one * s;
        if (fireLight != null)
        {
            fireLight.range = repelRadius;
            fireLight.intensity = 1.5f + tier * 0.6f;
        }
    }

    // How far INTO this fire's repel zone the given point is (0 = outside/on the edge).
    // Creature uses the largest such depth to decide which fire is pushing it hardest.
    public float RepelDepth(Vector3 point)
    {
        float d = Vector3.Distance(point, transform.position);
        return d >= repelRadius ? 0f : repelRadius - d;
    }

    void Update()
    {
        if (player == null) return;
        if (Vector3.Distance(player.transform.position, transform.position) > radius) return;

        // Drop the buffer while healing is locked out, or it banks up during the fight
        // and dumps a burst heal the instant the lockout expires.
        if (player.HealBlocked) { buffer = 0f; return; }

        buffer += healPerSecond * Time.deltaTime;
        if (buffer >= 1f)
        {
            int whole = Mathf.FloorToInt(buffer);
            buffer -= whole;
            player.Heal(whole);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, repelRadius);
    }
}
