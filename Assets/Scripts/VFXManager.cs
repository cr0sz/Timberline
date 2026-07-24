using UnityEngine;

// Scene singleton (lives on GameManager) that spawns one-shot particle bursts.
// The prefabs' ParticleSystems are non-looping with StopAction=Destroy, so a burst
// cleans itself up — this just instantiates, tints, and forgets. All calls are
// null-safe: no VFXManager in the scene = silent, never a crash.
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Tooltip("Falling debris: chips, dust, leaves, blood. Tinted per call.")]
    public ParticleSystem debrisPrefab;
    [Tooltip("Rising additive spark: purchase sparkle, embers. Tinted per call.")]
    public ParticleSystem sparkPrefab;
    [Tooltip("Looping smoke+ember rig, parented onto a campfire.")]
    public GameObject campfirePrefab;

    void Awake()
    {
        // Last one wins; a reload/rebuild shouldn't leave two.
        Instance = this;
    }

    static void Spawn(ParticleSystem prefab, Vector3 pos, Color tint, float scale)
    {
        if (prefab == null) return;
        var ps = Instantiate(prefab, pos, Quaternion.identity);
        ps.transform.localScale = Vector3.one * scale;
        var main = ps.main;
        main.startColor = tint;
        ps.Play();
    }

    // Falling debris burst — chips (brown), dust (grey), leaves (green), blood (red).
    public static void Debris(Vector3 pos, Color tint, float scale = 1f)
    {
        if (Instance != null) Spawn(Instance.debrisPrefab, pos, tint, scale);
    }

    // Rising additive sparkle — a coin buy, a campfire ember pop.
    public static void Spark(Vector3 pos, Color tint, float scale = 1f)
    {
        if (Instance != null) Spawn(Instance.sparkPrefab, pos, tint, scale);
    }

    // Parent a looping smoke+ember rig onto a campfire (called from Campfire.Start).
    public static void AttachCampfire(Transform fire)
    {
        if (Instance == null || Instance.campfirePrefab == null) return;
        var fx = Instantiate(Instance.campfirePrefab, fire.position, Quaternion.identity);
        fx.transform.SetParent(fire, worldPositionStays: true);
    }
}
