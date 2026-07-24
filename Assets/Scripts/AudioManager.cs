using UnityEngine;

// One AudioSource, one static entry point. PlayOneShot mixes overlapping clips on a
// single source, so fast tools and rapid hits don't need a pool or per-call
// Instantiate. Drop new clips in the Inspector and call the matching Play* method.
//
// ponytail: no mixer groups, no per-category volume, no 3D positioning — everything
// is 2D UI-style audio. Add a mixer when there is enough content to balance.
[DefaultExecutionOrder(-100)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Clips (leave empty to stay silent)")]
    public AudioClip chop;         // axe / pickaxe hit on a node
    public AudioClip sell;         // coins in at the shop
    public AudioClip purchase;     // upgrade or buildable bought
    public AudioClip hit;          // weapon connecting with a creature
    public AudioClip playerHurt;   // creature bites the player
    public AudioClip mine;         // pickaxe striking stone (chop is axe-on-wood)
    public AudioClip treeFall;     // a tree toppling when felled

    [Header("Master")]
    [Range(0f, 1f)] public float volume = 0.8f;

    // Per-event trims. Everything shares one master, but a chop fires several times a
    // second while a sell fires once — at equal volume the tool sounds drown the game.
    // These are multiplied into the master, so master still rules the whole mix.
    [Header("Per-event volume (x master)")]
    [Range(0f, 1f)] public float chopVolume = 0.35f;
    [Range(0f, 1f)] public float mineVolume = 0.35f;
    [Range(0f, 1f)] public float hitVolume = 0.3f;
    [Range(0f, 1f)] public float treeFallVolume = 0.6f;
    [Range(0f, 1f)] public float playerHurtVolume = 0.7f;
    [Range(0f, 1f)] public float sellVolume = 0.7f;
    [Range(0f, 1f)] public float purchaseVolume = 0.6f;

    [Tooltip("A clip can't retrigger faster than this. Auto-attack (0.8s) with a 1.7s " +
             "sample already stacks two copies; without a floor a maxed axe stacks five.")]
    public float minRetriggerGap = 0.15f;

    AudioSource source;
    readonly System.Collections.Generic.Dictionary<AudioClip, float> lastPlayed =
        new System.Collections.Generic.Dictionary<AudioClip, float>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // Safe to call before the manager exists or with an unassigned clip — callers
    // shouldn't have to null-check to make a noise.
    public static void Play(AudioClip clip, float pitchJitter = 0f, float volumeScale = 1f)
    {
        var m = Instance;
        if (m == null || clip == null || m.source == null) return;

        // Overlapping copies of one sample sum in amplitude — that's what turns a
        // fast tool into a wall of noise, not the master volume.
        if (m.minRetriggerGap > 0f)
        {
            if (m.lastPlayed.TryGetValue(clip, out float last) &&
                Time.unscaledTime - last < m.minRetriggerGap) return;
            m.lastPlayed[clip] = Time.unscaledTime;
        }

        m.source.pitch = pitchJitter > 0f ? 1f + Random.Range(-pitchJitter, pitchJitter) : 1f;
        m.source.PlayOneShot(clip, m.volume * Mathf.Clamp01(volumeScale));
    }

    // Repeated identical samples turn to machine-gun quickly; the jitter on chop and
    // hit is what keeps a fast axe from sounding like a stuck loop.
    public static void Chop()      { var m = Instance; if (m != null) Play(m.chop, 0.12f, m.chopVolume); }
    public static void Hit()       { var m = Instance; if (m != null) Play(m.hit, 0.12f, m.hitVolume); }
    public static void Sell()      { var m = Instance; if (m != null) Play(m.sell, 0f, m.sellVolume); }
    public static void Purchase()  { var m = Instance; if (m != null) Play(m.purchase, 0f, m.purchaseVolume); }
    public static void PlayerHurt(){ var m = Instance; if (m != null) Play(m.playerHurt, 0.08f, m.playerHurtVolume); }
    public static void Mine()      { var m = Instance; if (m != null) Play(m.mine, 0.12f, m.mineVolume); }
    public static void TreeFall()  { var m = Instance; if (m != null) Play(m.treeFall, 0.06f, m.treeFallVolume); }
}
