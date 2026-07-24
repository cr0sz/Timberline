using System.Collections.Generic;
using UnityEngine;

// Keeps the world stocked with animals. Seeds each kind up to its max, tops up on an
// interval as they get hunted, spawns inside a radius but never on top of the player,
// and drops each spawn onto the ground by raycast.
//
// It also paces WHEN each kind first shows up. A flat "no predators for six minutes"
// gate was the first attempt and it was wrong: at minute six it dropped a wolf pack AND
// a tiger AND a bear on a player still holding a tier-1 spear. The bear alone is 90 HP
// hitting for 18 every 1.9s; a tier-1 spear does 4 every 0.8s, so killing it takes ~18s
// and costs ~160 damage against a 100 HP pool. That is unwinnable, not hard.
//
// So the delay is PER KIND and the opening escalates instead of detonating:
//
//   t=0      deer, chickens     the world is alive and there is meat to hunt
//   t=6min   wolves (x3)        20 HP each — a tier-1 spear wins, at a cost
//   t=10min  tiger (x1)         45 HP, wants weapon Lv3+
//   t=15min  bear (x1)          90 HP, wants weapon Lv5+; pays 180 coins if you win
//
// The whole curve is scene data (kinds[].firstSpawnDelay), not code, so it retunes from
// the Inspector without a recompile.
public class CreatureSpawner : MonoBehaviour
{
    [System.Serializable]
    public class Kind
    {
        public string name;
        public GameObject prefab;   // an animal prefab with a Creature on it
        public int max = 3;

        [Tooltip("FRESH RUNS ONLY: hold this animal out of the world for this many seconds " +
                 "after the run starts. 0 = present from the first frame (all prey). This is " +
                 "the difficulty curve of the opening — see the table in CreatureSpawner's " +
                 "class comment.")]
        public float firstSpawnDelay = 0f;
        [Tooltip("Toasted when this kind's delay expires. Blank = arrive silently.")]
        public string arrivalMessage;

        [System.NonSerialized] public readonly List<GameObject> alive = new List<GameObject>();
        [System.NonSerialized] public bool released;   // its delay has elapsed (or never applied)
    }

    public Kind[] kinds;
    public float radius = 22f;
    [Tooltip("Never spawn closer than this to the player.")]
    public float minPlayerDist = 10f;
    [Tooltip("Seconds between top-up checks.")]
    public float interval = 6f;

    float timer;
    Transform player;
    float runStart;
    bool graceArmed;      // false on a loaded save: every kind is present from frame one

    void Start()
    {
        var ph = FindFirstObjectByType<PlayerHealth>();
        if (ph != null) player = ph.transform;

        // The staggered opening is for run #1 only. A returning player has already
        // survived it; re-arming the delays on every reload would be the same bug
        // pointing the other way — an empty world every time you load.
        // HasSave is read before SaveManager.Load() (SaveManager is execution order 1000).
        var sm = FindFirstObjectByType<SaveManager>();
        graceArmed = sm == null || !sm.HasSave;
        runStart = Time.time;

        foreach (var k in kinds)
        {
            k.released = !IsHeld(k.firstSpawnDelay, runStart, runStart, graceArmed);
            if (!k.released) continue;
            for (int i = 0; i < k.max; i++) TrySpawn(k);
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < interval) return;
        timer = 0f;

        foreach (var k in kinds)
        {
            k.alive.RemoveAll(a => a == null);   // hunted ones drop out
            if (!k.released)
            {
                if (IsHeld(k.firstSpawnDelay, runStart, Time.time, graceArmed)) continue;
                Release(k);
                continue;
            }
            if (k.alive.Count < k.max) TrySpawn(k);
        }
    }

    // Pure so the pacing rule is testable without a play-mode clock.
    public static bool IsHeld(float delay, float runStart, float now, bool armed)
        => armed && delay > 0f && now < runStart + delay;

    // A kind's delay just expired. Announce it and seed it to full in one go — the
    // top-up path would otherwise trickle one animal in per `interval`, and predators
    // silently materialising behind you reads as a bug rather than as a turn in the game.
    void Release(Kind k)
    {
        k.released = true;

        if (!string.IsNullOrEmpty(k.arrivalMessage))
        {
            var bs = FindFirstObjectByType<BuildSystem>();
            if (bs != null) bs.Toast(k.arrivalMessage);
        }

        for (int i = k.alive.Count; i < k.max; i++) TrySpawn(k);
    }

    void TrySpawn(Kind k)
    {
        if (k.prefab == null) return;
        Vector3 pos = PickPoint();
        var go = Instantiate(k.prefab, pos, Quaternion.Euler(0f, Random.value * 360f, 0f));
        k.alive.Add(go);
    }

    Vector3 PickPoint()
    {
        for (int t = 0; t < 8; t++)
        {
            Vector2 c = Random.insideUnitCircle * radius;
            Vector3 p = transform.position + new Vector3(c.x, 0f, c.y);
            if (player == null || Vector3.Distance(p, player.position) >= minPlayerDist)
                return new Vector3(p.x, SampleY(p), p.z);
        }
        return new Vector3(transform.position.x, SampleY(transform.position), transform.position.z);
    }

    float SampleY(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out var hit, 200f))
            return hit.point.y;
        return transform.position.y;
    }
}
