using UnityEngine;

public class PlayerGatherer : MonoBehaviour
{
    public PlayerController playerController;
    public Animator animator;
    public ToolInventory toolInventory;
    public PlayerInventory inventory;
    public FloatingText floatingTextPrefab;
    public float gatherRange = 2.5f;
    [Range(15f, 180f)]
    [Tooltip("You must be facing the node to chop it — targets outside this half-angle " +
             "from your forward are ignored. 180 turns the check off (any direction).")]
    public float facingHalfAngle = FacingCheck.DefaultHalfAngle;

    [Tooltip("Seconds between 'Bag full!' toasts — without this it fires every swing.")]
    public float bagFullToastInterval = 3f;

    readonly Collider[] hits = new Collider[32];   // dense forest can exceed 16
    float timer;
    float bagFullTimer = -99f;
    PlayerStats stats;

    // True while actively chopping/mining — HeldToolSwap uses it to show the axe.
    public bool Gathering { get; private set; }

    void Update()
    {
        ResourceNode target = FindNearest();

        bool strongEnough = false;
        ToolInventory.ToolTier tool = default;
        if (target != null)
        {
            tool = toolInventory.GetToolFor(target.resourceType);
            int myLevel = toolInventory.GetTierFor(target.resourceType);
            strongEnough = myLevel >= target.requiredToolLevel;
        }

        bool standingAtTarget = target != null && !playerController.IsMoving;
        bool canGather = standingAtTarget && strongEnough;

        if (canGather)
        {
            timer += Time.deltaTime;
            if (timer >= tool.chopInterval)
            {
                timer = 0f;
                int amount = target.Hit(tool.hitsReduction);
                Vector3 fxAt = target.transform.position + Vector3.up * 1f;
                if (target.resourceType == ResourceType.Stone)
                {
                    AudioManager.Mine();
                    VFXManager.Debris(fxAt, new Color(0.62f, 0.62f, 0.60f));   // grey chips
                }
                else
                {
                    AudioManager.Chop();
                    VFXManager.Debris(fxAt, new Color(0.52f, 0.34f, 0.17f));   // wood chips
                }
                if (amount > 0)
                {
                    // Add() clamps to the bag — anything past capacity is thrown away.
                    // Silently dropping it reads as broken, so say so (throttled).
                    int taken = inventory.Add(target.resourceType, amount);
                    if (taken <= 0)
                    {
                        if (Time.time - bagFullTimer >= bagFullToastInterval)
                        {
                            bagFullTimer = Time.time;
                            SpawnFloatingText("<color=#E24C47>Bag full!</color>");
                        }
                    }
                    else
                    {
                        if (stats == null) stats = GetComponent<PlayerStats>();
                        if (stats != null) stats.AddGathered(target.resourceType, taken);
                        SpawnFloatingText($"+{taken} {target.resourceType}");
                    }
                }
            }
        }
        else if (standingAtTarget && !strongEnough)   // too weak: nag once a second
        {
            timer += Time.deltaTime;
            if (timer >= 1f)
            {
                timer = 0f;
                string toolName = target.resourceType == ResourceType.Stone ? "pickaxe" : "axe";
                SpawnFloatingText($"Need Lv{target.requiredToolLevel} {toolName}");
            }
        }
        else
        {
            timer = 0f;
        }

        Gathering = canGather;
        animator.SetBool("Gathering", canGather);
    }

    void SpawnFloatingText(string message)
    {
        if (floatingTextPrefab == null) return;
        Vector3 pos = playerController.transform.position + Vector3.up * 2f;
        FloatingText.Spawn(floatingTextPrefab, pos, message);
    }

    // Asks physics what's actually in range each frame. A felled tree disables its
    // collider, so stumps drop out of the query on their own.
    ResourceNode FindNearest()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, gatherRange, hits);
        ResourceNode nearest = null;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            ResourceNode node = hits[i].GetComponent<ResourceNode>();
            if (node == null) continue;
            // Trees use one convex hull around the whole canopy, so overlapping the
            // collider means nothing. Measure to the trunk and gate on that.
            float dist = Vector3.Distance(transform.position, node.transform.position);
            if (dist > gatherRange) continue;
            // You have to be looking at it. Without this you chopped whatever happened
            // to be nearest, including a tree directly behind you.
            if (!FacingCheck.InFront(transform, node.transform.position, facingHalfAngle)) continue;
            if (dist < nearestDist) { nearestDist = dist; nearest = node; }
        }
        return nearest;
    }
}