using System.Collections;
using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    public ResourceType resourceType;
    public int hitsToDeplete = 5;
    public int amountPerHit = 1;
    public int requiredToolLevel = 1;

    [Tooltip("Total units this node pays out before it falls, regardless of how few " +
             "hits it took. 0 = derive from hitsToDeplete x amountPerHit.")]
    public int totalYield = 0;

    [Header("Regrow")]
    public GameObject stumpPrefab;   // e.g. Tree_Oak_Stump (leave empty for rocks)
    public float respawnMin = 30f;
    public float respawnMax = 60f;

    [Header("Fell juice")]
    [Tooltip("Trees (have a stump) topple; rocks (no stump) sink away.")]
    public float toppleAngle = 85f;
    public float toppleDuration = 0.6f;
    public float sinkDuration = 0.3f;

    [Header("Hit juice")]
    [Tooltip("Squash/stretch on each chop, as a fraction of the node's scale.")]
    public float hitPunch = 0.06f;
    public float hitTilt = 2.5f;
    public float hitPunchDuration = 0.14f;

    int currentHits;
    int paidOut;
    bool depleted;
    Coroutine shakeRoutine;
    Renderer[] renderers;
    Collider col;
    GameObject stumpInstance;

    Quaternion origRotation;
    Vector3 origScale;
    Transform player;   // cached lazily for the regrow-collider guard

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        col = GetComponentInChildren<Collider>();
        origRotation = transform.rotation;
        origScale = transform.localScale;
    }

    // A node is worth a FIXED total, however many swings it took. A better tool
    // harvests it faster — never for less. (The old "amountPerHit x hits" maths
    // scaled backwards: hitsReduction cut the hit count, so a Lv10 axe felled a
    // Lv1 tree in two hits and got two wood out of it.)
    public int TotalYield() => totalYield > 0
        ? totalYield
        : Mathf.Max(1, hitsToDeplete * Mathf.Max(1, amountPerHit));

    public int Hit(int hitsReduction = 0)
    {
        if (depleted) return 0;                       // can't chop a stump

        currentHits++;
        int threshold = Mathf.Max(1, hitsToDeplete - hitsReduction);
        int total = TotalYield();
        int remaining = Mathf.Max(0, total - paidOut);

        int payout;
        if (currentHits >= threshold)
        {
            payout = remaining;                       // felling blow dumps the rest
            if (shakeRoutine != null) { StopCoroutine(shakeRoutine); shakeRoutine = null; }
            transform.localScale = origScale;
            transform.rotation = origRotation;
            StartCoroutine(DepleteThenRegrow());
        }
        else
        {
            payout = Mathf.Min(Mathf.CeilToInt(total / (float)threshold), remaining);
            Shake();
        }

        paidOut += payout;
        return payout;
    }

    // Squash + wobble on every chop so felling progress reads on the node itself.
    void Shake()
    {
        if (hitPunch <= 0f && hitTilt <= 0f) return;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    IEnumerator ShakeRoutine()
    {
        float t = 0f;
        float dur = Mathf.Max(0.01f, hitPunchDuration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float k = Mathf.Sin(u * Mathf.PI);                       // 0 -> 1 -> 0
            float wide = 1f + k * hitPunch;
            float tall = 1f - k * hitPunch;
            transform.localScale = new Vector3(origScale.x * wide, origScale.y * tall, origScale.z * wide);
            transform.rotation = origRotation *
                Quaternion.Euler(0f, 0f, Mathf.Sin(u * Mathf.PI * 3f) * hitTilt * (1f - u));
            yield return null;
        }
        transform.localScale = origScale;
        transform.rotation = origRotation;
        shakeRoutine = null;
    }

    IEnumerator DepleteThenRegrow()
    {
        depleted = true;
        if (col != null) col.enabled = false;         // stop blocking / re-hits immediately

        // Fell it: trees topple over their base, rocks shrink into the ground.
        if (stumpPrefab != null)
        {
            AudioManager.TreeFall();
            // Leaf burst up in the canopy, sized to the node.
            Bounds b = renderers != null && renderers.Length > 0 ? renderers[0].bounds : new Bounds(transform.position, Vector3.one);
            for (int i = 1; renderers != null && i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            VFXManager.Debris(new Vector3(transform.position.x, b.center.y + b.extents.y * 0.5f, transform.position.z),
                              new Color(0.36f, 0.58f, 0.22f), 1.8f);   // green leaves
            yield return Topple();
        }
        else yield return SinkAway();

        foreach (Renderer r in renderers) r.enabled = false;
        // Restore the transform now that it's hidden, so regrow pops back upright.
        transform.rotation = origRotation;
        transform.localScale = origScale;

        if (stumpPrefab != null)
        {
            stumpInstance = Instantiate(stumpPrefab, transform.position, origRotation);
            stumpInstance.transform.localScale = origScale;
        }

        yield return new WaitForSeconds(Random.Range(respawnMin, respawnMax));

        // Grow back
        if (stumpInstance != null) Destroy(stumpInstance);
        transform.rotation = origRotation;
        transform.localScale = origScale;
        foreach (Renderer r in renderers) r.enabled = true;

        // Don't pop the collider back on top of the player — it would shove the
        // CharacterController. Wait until they step off the trunk.
        if (col != null)
        {
            while (PlayerOnTrunk()) yield return new WaitForSeconds(1f);
            col.enabled = true;
        }
        currentHits = 0;
        paidOut = 0;
        depleted = false;
    }

    bool PlayerOnTrunk()
    {
        if (player == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null) return false;
            player = pc.transform;
        }
        return Vector3.Distance(player.position, transform.position) < 1.3f;
    }

    // Felling a node mid-respawn (spawner cull, scene teardown) must take its
    // stump with it, else the stump instance leaks.
    void OnDestroy()
    {
        if (stumpInstance != null) Destroy(stumpInstance);
    }

    // Tip the tree over a horizontal axis, pivoting on its ground contact so the
    // base stays planted. Accelerates (u*u) so it reads as a real fall.
    IEnumerator Topple()
    {
        Vector3 basePoint = GroundPivot();
        Vector2 rnd = Random.insideUnitCircle.normalized;
        Vector3 fallAxis = new Vector3(rnd.x, 0f, rnd.y);   // horizontal -> tips sideways
        if (fallAxis.sqrMagnitude < 0.001f) fallAxis = Vector3.right;

        float t = 0f, applied = 0f;
        while (t < toppleDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / toppleDuration);
            float target = (u * u) * toppleAngle;
            transform.RotateAround(basePoint, fallAxis, target - applied);
            applied = target;
            yield return null;
        }
    }

    IEnumerator SinkAway()
    {
        Vector3 s0 = transform.localScale;
        Vector3 s1 = s0 * 0.05f;
        float t = 0f;
        while (t < sinkDuration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(s0, s1, t / sinkDuration);
            yield return null;
        }
    }

    // Bottom-centre of the combined renderer bounds — robust whether the model's
    // pivot sits at its base or its centre.
    Vector3 GroundPivot()
    {
        if (renderers == null || renderers.Length == 0) return transform.position;
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return new Vector3(transform.position.x, b.min.y, transform.position.z);
    }
}
