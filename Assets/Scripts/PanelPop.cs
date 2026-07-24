using System.Collections;
using UnityEngine;

// Panels used to blink into existence on SetActive(true). This scales them in from
// 90% and fades the CanvasGroup up over ~0.15s so opening the shop / build menu
// reads as a window arriving instead of a texture swap.
// Unscaled time on purpose — a pause menu still animates at timeScale 0.
[RequireComponent(typeof(CanvasGroup))]
public class PanelPop : MonoBehaviour
{
    public float duration = 0.15f;
    [Range(0.5f, 1f)] public float fromScale = 0.9f;

    CanvasGroup group;
    RectTransform rect;
    Vector3 baseScale = Vector3.one;
    bool captured;

    void Awake() { Capture(); }

    // The resting scale is whatever the panel was authored at — never assume 1,
    // some HUD panels are scaled in the scene.
    void Capture()
    {
        if (captured) return;
        rect = GetComponent<RectTransform>();
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        if (rect != null) baseScale = rect.localScale;
        captured = true;
    }

    void OnEnable()
    {
        Capture();
        StopAllCoroutines();
        StartCoroutine(Pop());
    }

    void OnDisable()
    {
        // Leave it resting, not mid-pop — the next enable starts from a clean slate.
        if (rect != null) rect.localScale = baseScale;
        if (group != null) group.alpha = 1f;
    }

    IEnumerator Pop()
    {
        float t = 0f;
        float dur = Mathf.Max(0.01f, duration);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / dur);
            float e = 1f - (1f - u) * (1f - u);          // ease-out quad
            group.alpha = e;
            if (rect != null) rect.localScale = baseScale * Mathf.Lerp(fromScale, 1f, e);
            yield return null;
        }
        group.alpha = 1f;
        if (rect != null) rect.localScale = baseScale;
    }
}
