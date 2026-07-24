using UnityEngine;
using UnityEngine.UI;

// Turns a PlayerHealth hit into sensory feedback: red screen flash, camera
// shake, and knockback away from the attacker. Lives on the Player next to
// PlayerHealth and PlayerController.
[RequireComponent(typeof(PlayerHealth))]
public class PlayerHitFeedback : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Fullscreen red overlay Image, starts transparent.")]
    public Image flashImage;
    public CameraShake cameraShake;

    [Header("Tuning")]
    [Range(0f, 1f)] public float flashPeak = 0.4f;   // max overlay alpha
    public float flashDuration = 0.3f;
    [Range(0f, 1f)] public float shakeAmount = 0.6f;
    public float knockbackSpeed = 3.5f;              // initial m/s, decays in PlayerController (~0.35m; keeps player in retaliation range)

    PlayerHealth health;
    PlayerController mover;
    float flashT;

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
        mover = GetComponent<PlayerController>();
    }

    void OnEnable()  { health.OnDamaged += HandleDamaged; }
    void OnDisable() { health.OnDamaged -= HandleDamaged; }

    void HandleDamaged(int dmg, Vector3 dir)
    {
        flashT = flashDuration;
        if (cameraShake != null) cameraShake.Shake(shakeAmount);
        if (mover != null && dir.sqrMagnitude > 0.0001f)
            mover.AddKnockback(dir * knockbackSpeed);
    }

    void Update()
    {
        if (flashImage == null) return;

        float a = 0f;
        if (flashT > 0f)
        {
            flashT -= Time.deltaTime;
            a = flashPeak * Mathf.Clamp01(flashT / flashDuration);   // instant on, fade out
        }
        var c = flashImage.color;
        if (!Mathf.Approximately(c.a, a)) { c.a = a; flashImage.color = c; }
    }
}
