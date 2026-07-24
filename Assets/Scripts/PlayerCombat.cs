using UnityEngine;

// Auto-swings the equipped weapon (spear) at the nearest creature in range.
// Damage/interval come from ToolInventory so shop weapon upgrades feed straight
// in. Mirrors PlayerGatherer's overlap-query targeting.
public class PlayerCombat : MonoBehaviour
{
    public Animator animator;
    public ToolInventory toolInventory;
    public FloatingText floatingTextPrefab;
    public float weaponRange = 2.6f;
    [Range(15f, 180f)]
    [Tooltip("You must be facing a creature to swing at it — targets outside this " +
             "half-angle from your forward are ignored. 180 turns the check off.")]
    public float facingHalfAngle = FacingCheck.DefaultHalfAngle;
    [Tooltip("Animator trigger fired once per swing. Leave blank to disable.")]
    public string attackParam = "Attack";
    [Tooltip("Swings are held while this is moving, so the attack clip never plays " +
             "mid-run. Found on this object if left empty.")]
    public PlayerController movement;

    readonly Collider[] hits = new Collider[32];   // match PlayerGatherer; dense packs
    float timer;

    // True while a creature is in weapon range — HeldToolSwap uses it to keep the spear out.
    public bool HasTarget { get; private set; }

    void Awake()
    {
        if (movement == null) movement = GetComponent<PlayerController>();
    }

    void Update()
    {
        Creature target = FindNearest();
        // Proximity, not "am I swinging" — otherwise the spear pops in and out of the
        // hand every time you jog past an animal.
        HasTarget = target != null;

        bool moving = movement != null && movement.IsMoving;
        if (moving)
        {
            // Drop any queued swing so it can't fire a frame after you start running.
            if (animator != null && !string.IsNullOrEmpty(attackParam)) animator.ResetTrigger(attackParam);
            // Stay primed: stopping next to an animal should land a hit immediately
            // rather than making you wait out a fresh interval.
            if (target != null) timer = toolInventory.GetWeaponInterval();
            return;
        }

        if (target != null)
        {
            timer += Time.deltaTime;
            if (timer >= toolInventory.GetWeaponInterval())
            {
                timer = 0f;
                Swing();
                AudioManager.Hit();
                int dmg = toolInventory.GetWeaponDamage();
                Vector3 bloodAt = target.transform.position + Vector3.up * 0.8f;
                bool killed = target.TakeDamage(dmg);
                // Bigger spray on the killing blow (the creature is about to vanish).
                VFXManager.Debris(bloodAt, new Color(0.6f, 0.06f, 0.06f), killed ? 1.6f : 1f);
                if (floatingTextPrefab != null && !killed)
                    FloatingText.Spawn(floatingTextPrefab, target.transform.position + Vector3.up * 1.5f, $"-{dmg}");
            }
        }
        else timer = 0f;
    }

    Creature FindNearest()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, weaponRange, hits);
        Creature nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var c = hits[i].GetComponentInParent<Creature>();
            if (c == null) continue;
            // Same rule as gathering: you have to be looking at it. Without this you
            // speared an animal standing behind you.
            if (!FacingCheck.InFront(transform, c.transform.position, facingHalfAngle)) continue;
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < best) { best = d; nearest = c; }
        }
        return nearest;
    }

    // One trigger per swing. This used to be a bool held true for as long as a
    // creature was in range: the attack clip played once, hit its last frame and
    // froze there, because the only way out of the state was the bool going false.
    void Swing()
    {
        if (animator == null || string.IsNullOrEmpty(attackParam)) return;
        foreach (var p in animator.parameters)
            if (p.name == attackParam && p.type == AnimatorControllerParameterType.Trigger)
            { animator.SetTrigger(attackParam); return; }
    }
}
