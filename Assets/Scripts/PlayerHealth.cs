using System;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;

    [Tooltip("Empty GameObject the player teleports to on death.")]
    public Transform spawnPoint;

    [Header("Death penalty")]
    [Range(0f, 1f)]
    [Tooltip("Fraction of carried resources dropped on death. 0 = keep everything.")]
    public float deathResourceLoss = 0.3f;
    [Range(0f, 1f)]
    [Tooltip("Fraction of coins lost on death.")]
    public float deathCoinLoss = 0.1f;
    public FloatingText floatingTextPrefab;   // optional — shows what you lost

    private float regenDelay = 7f;      // Delay before health regeneration starts
    private float regenperSecond = 3f;  // Health points regenerated per second
    private float invulntime = 1.0f;    // Immunity window after a hit / respawn

    [Tooltip("No healing from ANY source for this long after taking a hit. Without it you " +
             "can stand in a campfire and out-heal a predator between its swings.")]
    public float healLockout = 5f;

    // True while a hit is too recent to be healed through.
    public bool HealBlocked => Time.time - lastHitTime < healLockout;

    public event System.Action<int> OnHealthChanged;          // passes currentHealth
    // Fired only on an actual hit (not heal/regen): damage dealt + unit push
    // direction away from the attacker (Vector3.zero when source is unknown).
    public event System.Action<int, Vector3> OnDamaged;
    public event System.Action OnRespawn;                      // fired after death teleport + reset

    private CharacterController controller;
    private float lastHitTime = -999f;  // time of most recent damage
    private float invulnUntil;           // no damage before this time
    private float regenBuffer;           // accumulates fractional HP

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth);
    }

    // Restore HP (campfire, future food). Clamps to maxHealth, fires the HUD event.
    // The lockout lives HERE rather than in Campfire so every current and future
    // healer is covered by one guard.
    public void Heal(int amount)
    {
        if (HealBlocked) return;
        if (amount <= 0 || currentHealth <= 0 || currentHealth >= maxHealth) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth);
    }

    // Non-directional damage (no knockback) — kept for callers without a source.
    public void TakeDamage(int dmg) => TakeDamage(dmg, transform.position);

    // sourcePos: world position of the attacker, used to push the player away.
    public void TakeDamage(int dmg, Vector3 sourcePos)
    {
        if (Time.time < invulnUntil) return;        // immune right now
        if (dmg <= 0 || currentHealth <= 0) return;

        int before = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - dmg);
        int dealt = before - currentHealth;
        lastHitTime = Time.time;
        invulnUntil = Time.time + invulntime;
        OnHealthChanged?.Invoke(currentHealth);

        Vector3 dir = transform.position - sourcePos;
        dir.y = 0f;
        OnDamaged?.Invoke(dealt, dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero);
        AudioManager.PlayerHurt();

        if (currentHealth <= 0) Die();
    }

    private void Die()
    {
        ApplyDeathPenalty();

        // CharacterController resists direct position sets — disable it around the teleport.
        if (spawnPoint != null)
        {
            controller.enabled = false;
            transform.position = spawnPoint.position;
            controller.enabled = true;
        }

        currentHealth = maxHealth;
        regenBuffer = 0f;
        invulnUntil = Time.time + invulntime;
        OnHealthChanged?.Invoke(currentHealth);
        OnRespawn?.Invoke();          // hook for anything that resets on death
    }

    // Dying costs you part of the haul you were carrying, so a deep run is a real bet.
    // ponytail: resources vanish rather than spilling as pickups — no loot-bag prefab,
    // add one if "run back to your corpse" ever becomes the design.
    private void ApplyDeathPenalty()
    {
        var inv = GetComponent<PlayerInventory>();
        if (inv == null) return;

        int lost = inv.LoseFraction(deathResourceLoss);
        int coinsLost = Mathf.FloorToInt(inv.coins * deathCoinLoss);
        if (coinsLost > 0) inv.SpendCoins(coinsLost);

        if (floatingTextPrefab != null && (lost > 0 || coinsLost > 0))
            FloatingText.Spawn(floatingTextPrefab, transform.position + Vector3.up * 2.2f,
                coinsLost > 0 ? $"Lost {lost} items, {coinsLost} coins" : $"Lost {lost} items");
    }

    void Update()
    {
        if (currentHealth >= maxHealth) return;
        if (Time.time - lastHitTime < regenDelay) return;

        regenBuffer += regenperSecond * Time.deltaTime;
        if (regenBuffer < 1f) return;

        int whole = Mathf.FloorToInt(regenBuffer);
        regenBuffer -= whole;
        currentHealth = Mathf.Min(maxHealth, currentHealth + whole);
        OnHealthChanged?.Invoke(currentHealth);
    }

    // Restore saved HP. Never load into a corpse (0 -> full) so a bad save can't
    // spawn the player dead.
    public void LoadHealth(int hp)
    {
        currentHealth = Mathf.Clamp(hp, 0, maxHealth);
        if (currentHealth <= 0) currentHealth = maxHealth;
        lastHitTime = Time.time;
        OnHealthChanged?.Invoke(currentHealth);
    }

    // For the HUD bar fill (0..1).
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public float HealthFraction => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
}
