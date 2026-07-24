using UnityEngine;
using UnityEngine.AI;

// One animal. Predator chases + lunges to bite the player; Prey flees. Dies to
// the player's weapon and drops Meat/Hide into the player's carry (sold for coins).
//
// Movement is CharacterController-driven, but the DIRECTION comes from a real
// NavMesh path (NavMesh.CalculatePath, recomputed a few times a second) rather than
// a straight line at the target. No NavMeshAgent component: an agent would fight the
// CharacterController for the transform and take over gravity and the procedural
// lunge with it. Using the static path API keeps the agent purely as a route oracle
// and leaves every other behaviour untouched. Falls back to the old SphereCast slide
// when the scene has no baked NavMesh, so nothing breaks before the bake is run.
//
// The animals ship with only a locomotion blend tree (no attack clip), so the "bite"
// is a procedural forward lunge — reads as a pounce without needing an animation asset.
public class Creature : MonoBehaviour
{
    public enum Behavior { Predator, Prey }

    [Header("Kind")]
    public Behavior behavior = Behavior.Prey;

    [Header("Stats")]
    public int maxHealth = 20;
    public float moveSpeed = 3.5f;
    public float turnSpeed = 8f;

    [Header("Senses (metres)")]
    [Tooltip("Predator starts chasing / Prey starts fleeing inside this range.")]
    public float senseRange = 12f;
    [Tooltip("Predator stops here and bites.")]
    public float attackRange = 1.8f;

    [Header("Attack (Predator only)")]
    public int damage = 8;
    public float attackInterval = 1.2f;
    [Tooltip("How far the pounce hops forward.")]
    public float lungeDist = 0.7f;
    [Tooltip("Pounce duration (seconds).")]
    public float lungeDur = 0.25f;

    [Header("Pathfinding")]
    [Tooltip("Seconds between NavMesh path recalculations. Lower = tighter tracking, more CPU. " +
             "A path is also recomputed early whenever the destination drifts past repathMoveDist.")]
    public float repathInterval = 0.35f;
    [Tooltip("Recompute early once the destination has moved this far from the one the current path was built for.")]
    public float repathMoveDist = 1.5f;
    [Tooltip("How close to a path corner counts as reaching it.")]
    public float cornerTolerance = 0.6f;

    [Header("Obstacle steering (NavMesh fallback)")]
    [Tooltip("Used only when no baked NavMesh is reachable. Probe this far ahead and slide along " +
             "whatever it hits, so a blocked animal walks the length of a fence and rounds the end " +
             "instead of pressing into it. Keep it BELOW attackRange or a predator will deflect off " +
             "the player. 0 = off.")]
    public float avoidProbe = 1.0f;

    [Header("Loot on death")]
    public ResourceType dropA = ResourceType.Meat;
    public int dropAAmount = 2;
    public ResourceType dropB = ResourceType.Hide;
    public int dropBAmount = 1;
    public FloatingText floatingTextPrefab;   // optional

    [Header("Anim (optional)")]
    public Animator animator;
    [Tooltip("Extra float param to drive with 0/1 move speed, on top of the ones auto-detected.")]
    public string speedParam = "Speed";

    int health;
    CharacterController cc;
    Transform player;
    PlayerHealth playerHealth;
    PlayerInventory inventory;
    PlayerStats stats;
    float attackTimer;

    // Float params on this animal's controller that we drive with 0/1 move speed.
    // The packs don't agree on a name: our own rigs use "Speed", the ithappy animals
    // use "Vert". Setting a param that doesn't exist is a silent no-op that leaves the
    // animal frozen in its idle pose, so resolve the real names once at spawn.
    static readonly string[] KnownSpeedParams = { "Speed", "Vert", "MoveSpeed" };
    string[] driveParams = new string[0];

    // lunge state
    float lungeT;
    float prevLungeOff;
    Vector3 lungeDir;

    void Start()
    {
        health = maxHealth;
        cc = GetComponent<CharacterController>();
        // ponytail: single player, so scene lookups once at spawn are fine.
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        inventory = FindFirstObjectByType<PlayerInventory>();
        stats = FindFirstObjectByType<PlayerStats>();
        if (playerHealth != null) player = playerHealth.transform;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        CacheAnimParams();
    }

    void CacheAnimParams()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        var found = new System.Collections.Generic.List<string>();
        foreach (var p in animator.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Float) continue;
            if (p.name == speedParam || System.Array.IndexOf(KnownSpeedParams, p.name) >= 0)
                found.Add(p.name);
        }
        driveParams = found.ToArray();
    }

    void Update()
    {
        ApplyGravity();
        if (player == null) { Animate(0f); return; }

        float dist = Vector3.Distance(transform.position, player.position);
        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        Vector3 dir = flat.sqrMagnitude > 0.0001f ? flat.normalized : transform.forward;

        float moving = 0f;

        if (behavior == Behavior.Predator)
        {
            // Fire beats hunger. A predator inside a campfire's repel zone backs out of
            // it before doing anything else, so a built camp is genuinely safe ground
            // and the fire is worth its coins. Straight-line retreat on purpose: the
            // animal is fleeing a thing it can see, not navigating to somewhere.
            Vector3 away = RepelDirection();
            if (away != Vector3.zero)
            {
                MoveDirect(away);
                Animate(1f);
                attackTimer = attackInterval;
                return;
            }

            if (dist <= senseRange && dist > attackRange)
            {
                MoveTowards(player.position, dir);
                moving = 1f;
                attackTimer = attackInterval;   // primed: bite the instant we arrive
            }
            else if (dist <= attackRange)
            {
                Face(dir);
                attackTimer += Time.deltaTime;
                if (attackTimer >= attackInterval)
                {
                    attackTimer = 0f;
                    lungeT = lungeDur;          // start the pounce
                    prevLungeOff = 0f;
                    lungeDir = dir;
                    if (playerHealth != null) playerHealth.TakeDamage(damage, transform.position);
                }
            }
        }
        else // Prey
        {
            if (dist <= senseRange)
            {
                // Path to a point away from the player rather than stepping straight
                // backwards, so a cornered deer runs along the fence to the gap
                // instead of shivering against it.
                MoveTowards(transform.position - dir * 8f, -dir);
                moving = 1f;
            }
        }

        // Procedural bite: hop forward and back. Applied as an incremental delta so
        // it layers on top of movement and nets to zero displacement.
        if (lungeT > 0f)
        {
            lungeT -= Time.deltaTime;
            float u = 1f - Mathf.Clamp01(lungeT / lungeDur);   // 0 → 1
            float off = Mathf.Sin(u * Mathf.PI) * lungeDist;   // 0 → peak → 0
            Translate(lungeDir * (off - prevLungeOff));
            prevLungeOff = off;
            moving = 1f;
        }
        else prevLungeOff = 0f;

        Animate(moving);
    }

    // CharacterController.Move never applies gravity on its own, so an animal that
    // spawns above the ground (its ground-raycast landed on another animal, a rock,
    // the player) would hang in the air and "fly" around. Pull it down every frame.
    float vy;
    void ApplyGravity()
    {
        if (cc == null || !cc.enabled) return;
        vy = cc.isGrounded ? -2f : vy - 20f * Time.deltaTime;
        cc.Move(Vector3.up * vy * Time.deltaTime);
    }

    // Walk toward `destination`, routing around obstacles via the NavMesh. `fallbackDir`
    // is the naive straight-line direction, used when there's no usable path (no bake,
    // destination off-mesh, partial path) — it still goes through Steer() so the old
    // wall-sliding behaviour survives as a floor.
    void MoveTowards(Vector3 destination, Vector3 fallbackDir)
    {
        Vector3 dir = PathDirection(destination);
        if (dir == Vector3.zero) dir = Steer(fallbackDir);
        Face(dir);
        Translate(dir * moveSpeed * Time.deltaTime);
    }

    // Straight-line move with no pathing at all — for fleeing a campfire, where the
    // whole point is to get away from a position rather than reach one.
    void MoveDirect(Vector3 dir)
    {
        dir = Steer(dir);
        Face(dir);
        Translate(dir * moveSpeed * Time.deltaTime);
    }

    // Everything that moves this animal goes through here. Writing transform.position
    // directly TELEPORTS a CharacterController — no sweep, no collision — so animals
    // walked straight into the player, and once the two capsules overlap the player's
    // own Move() can't push out either. That's why you could pass through them.
    void Translate(Vector3 delta)
    {
        if (cc != null && cc.enabled) cc.Move(delta);
        else transform.position += delta;
    }

    // ------------------------------------------------------------- campfire repel

    // Unit vector away from whichever campfire has this animal deepest inside its
    // repel radius, or zero when clear of every fire. Prey ignore fire — only
    // predators are supposed to be kept out of camp.
    Vector3 RepelDirection()
    {
        Campfire worst = null;
        float deepest = 0f;
        var all = Campfire.All;
        for (int i = 0; i < all.Count; i++)
        {
            var f = all[i];
            if (f == null) continue;
            float d = f.RepelDepth(transform.position);
            if (d > deepest) { deepest = d; worst = f; }
        }
        if (worst == null) return Vector3.zero;

        Vector3 away = transform.position - worst.transform.position;
        away.y = 0f;
        // Dead centre on the fire: pick any horizontal direction rather than freezing.
        if (away.sqrMagnitude < 0.0001f) return transform.forward;
        return away.normalized;
    }

    // ---------------------------------------------------------------- pathfinding

    NavMeshPath path;
    Vector3 pathTarget;          // destination the current path was built for
    int corner;                  // index of the corner we're currently walking to
    float nextRepath;

    // Horizontal unit vector toward the next corner of a NavMesh path to `destination`,
    // or zero when no usable path exists (caller falls back to straight-line steering).
    Vector3 PathDirection(Vector3 destination)
    {
        if (path == null) path = new NavMeshPath();

        bool stale = Time.time >= nextRepath ||
                     (destination - pathTarget).sqrMagnitude > repathMoveDist * repathMoveDist;
        if (stale) Repath(destination);

        // A PARTIAL path leads to the nearest reachable point, not the target — still
        // strictly better than walking into the wall, so it's accepted. Only an
        // outright invalid path drops us to the fallback.
        if (path.status == NavMeshPathStatus.PathInvalid || path.corners.Length < 2) return Vector3.zero;

        // corners[0] is where the path starts, which is behind us the moment we move.
        var corners = path.corners;
        if (corner < 1) corner = 1;
        while (corner < corners.Length && Flat(corners[corner] - transform.position).magnitude <= cornerTolerance)
            corner++;
        if (corner >= corners.Length) return Vector3.zero;   // arrived; nothing left to walk to

        Vector3 to = Flat(corners[corner] - transform.position);
        return to.sqrMagnitude > 0.0001f ? to.normalized : Vector3.zero;
    }

    void Repath(Vector3 destination)
    {
        nextRepath = Time.time + repathInterval;
        pathTarget = destination;
        corner = 1;

        // Both ends must sit ON the mesh or CalculatePath just fails. Animals stand on
        // terrain that is usually mesh anyway; the player may be on a deck or a rock
        // that isn't, hence the sample. 4m covers a jump onto a placed structure.
        if (!NavMesh.SamplePosition(transform.position, out var from, 4f, NavMesh.AllAreas) ||
            !NavMesh.SamplePosition(destination, out var to, 4f, NavMesh.AllAreas))
        {
            path.ClearCorners();
            return;
        }
        NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // FALLBACK ONLY — PathDirection is the real router now. One SphereCast and a slide:
    // deflects along a wall's tangent so a blocked animal walks the length of a short
    // fence and rounds the end rather than standing against it. It still stalls in a
    // concave pocket, which is exactly why pathfinding was added; this survives so an
    // unbaked scene degrades instead of breaking. Also used deliberately for the
    // campfire retreat, which wants a straight line away, not a route to somewhere.
    Vector3 Steer(Vector3 want)
    {
        if (avoidProbe <= 0f) return want;
        const float r = 0.5f;                                   // ~CharacterController radius
        Vector3 origin = transform.position + Vector3.up * r;
        if (Physics.SphereCast(origin, r, want, out RaycastHit hit, avoidProbe,
                               Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.distance > 0.01f)                            // skip our own overlapping collider
        {
            Vector3 n = hit.normal; n.y = 0f;
            if (n.sqrMagnitude > 0.0001f)
            {
                Vector3 slide = Vector3.ProjectOnPlane(want, n.normalized);
                slide.y = 0f;
                if (slide.sqrMagnitude > 0.0001f) return slide.normalized;
            }
        }
        return want;
    }

    void Face(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    void Animate(float moving)
    {
        if (animator == null) return;
        for (int i = 0; i < driveParams.Length; i++)
            animator.SetFloat(driveParams[i], moving);
    }

    // Called by PlayerCombat. Returns true if this hit killed the creature.
    public bool TakeDamage(int dmg)
    {
        if (health <= 0) return false;
        health = Mathf.Max(0, health - dmg);
        if (health <= 0) { Die(); return true; }
        return false;
    }

    void Die()
    {
        if (stats != null) stats.AddKill();      // killed by the player's weapon
        if (inventory != null)
        {
            if (dropAAmount > 0) inventory.Add(dropA, dropAAmount);
            if (dropBAmount > 0) inventory.Add(dropB, dropBAmount);
        }
        if (floatingTextPrefab != null)
            FloatingText.Spawn(floatingTextPrefab, transform.position + Vector3.up * 1.5f,
                               $"+{dropAAmount} {dropA}  +{dropBAmount} {dropB}");
        Destroy(gameObject);
    }
}
