using UnityEngine;

// Trauma-based positional shake for the camera. Sits on the Main Camera and
// nudges its localPosition each frame; the constant follow-offset is untouched,
// so it layers cleanly on top of PlayerController's camera follow.
public class CameraShake : MonoBehaviour
{
    [Tooltip("Max local-space offset (metres) at full trauma.")]
    public float maxOffset = 0.25f;
    [Tooltip("How fast trauma drains (per second).")]
    public float decay = 1.8f;

    Vector3 baseLocalPos;
    float trauma;   // 0..1

    void Awake() => baseLocalPos = transform.localPosition;

    // Add trauma (0..1). Called on a hit; multiple hits stack up to full.
    public void Shake(float amount) => trauma = Mathf.Clamp01(trauma + amount);

    void LateUpdate()
    {
        if (trauma <= 0f) { transform.localPosition = baseLocalPos; return; }

        float s = trauma * trauma;   // ease: falls off quickly near the end
        Vector3 off = new Vector3(Random.value * 2f - 1f, Random.value * 2f - 1f, 0f) * (maxOffset * s);
        transform.localPosition = baseLocalPos + off;
        trauma = Mathf.Max(0f, trauma - decay * Time.deltaTime);
    }
}
