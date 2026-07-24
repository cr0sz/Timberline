using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Tap feedback for uGUI buttons: a scale punch on the button plus a colour flash on
// the card behind it. Buying an upgrade only ever changed a number before, so a
// successful purchase read the same as a rejected one.
//
// Purely visual — nothing in here touches game state. Attaches itself on demand, so
// callers just say UIFeedback.Success(axeButton).
public class UIFeedback : MonoBehaviour
{
    static readonly Color Good = new Color(0.32f, 0.86f, 0.42f);
    static readonly Color Bad  = new Color(0.89f, 0.30f, 0.28f);

    public static void Success(Component c) => Play(c, Good);
    public static void Fail(Component c) => Play(c, Bad);

    // For buttons built at runtime (the BUILD card grid) where there's no serialized
    // reference to punch — feed back on whatever the player actually tapped.
    public static void SuccessOnClicked() => Play(Clicked(), Good);
    public static void FailOnClicked() => Play(Clicked(), Bad);

    static Component Clicked()
    {
        var es = EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return null;
        return es.currentSelectedGameObject.transform;
    }

    static void Play(Component c, Color flash)
    {
        if (c == null || !c.gameObject.activeInHierarchy) return;
        var fb = c.GetComponent<UIFeedback>();
        if (fb == null) fb = c.gameObject.AddComponent<UIFeedback>();
        fb.Run(flash);
    }

    [Header("Punch")]
    public float punchScale = 0.12f;
    public float punchDuration = 0.18f;
    [Header("Flash")]
    public float flashDuration = 0.28f;

    RectTransform rect;
    Vector3 baseScale = Vector3.one;
    Graphic flashTarget;
    Color flashBase;
    Coroutine routine;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        if (rect != null) baseScale = rect.localScale;
        // Flash the card the button sits in if there is one, else the button itself —
        // a whole card lighting up reads much better than a 40px border.
        if (transform.parent != null) flashTarget = transform.parent.GetComponent<Image>();
        if (flashTarget == null) flashTarget = GetComponent<Graphic>();
        if (flashTarget != null) flashBase = flashTarget.color;
    }

    void Run(Color flash)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            Reset();
        }
        routine = StartCoroutine(PlayRoutine(flash));
    }

    void Reset()
    {
        if (rect != null) rect.localScale = baseScale;
        if (flashTarget != null) flashTarget.color = flashBase;
    }

    IEnumerator PlayRoutine(Color flash)
    {
        float dur = Mathf.Max(punchDuration, flashDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;

            if (rect != null && punchDuration > 0f)
            {
                float u = Mathf.Clamp01(t / punchDuration);
                float k = Mathf.Sin(u * Mathf.PI);            // 1 -> 1.12 -> 1
                rect.localScale = baseScale * (1f + k * punchScale);
            }
            if (flashTarget != null && flashDuration > 0f)
            {
                float u = Mathf.Clamp01(t / flashDuration);
                flashTarget.color = Color.Lerp(flash, flashBase, u);
            }
            yield return null;
        }
        Reset();
        routine = null;
    }
}
