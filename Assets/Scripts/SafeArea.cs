using UnityEngine;

// Insets this (stretched) RectTransform so child UI clears notches, camera cutouts
// and rounded corners. Parent the gameplay HUD under it; leave full-bleed elements
// (touch zone, hit flash, modal scrims) outside.
//
// This does NOT trust Screen.safeArea alone. The project ships with
// androidRenderOutsideSafeArea = 1, so the app draws under the cutout and relies
// entirely on the OS reporting the inset — and plenty of Android OEMs, especially
// on punch-hole cameras, report a FULL-SCREEN safe area while still punching a hole
// in the display. The HUD then sits under the camera. So the inset is the WORST CASE
// of three sources:
//
//   1. Screen.safeArea      — correct on iOS and well-behaved Android.
//   2. Screen.cutouts       — reported by some devices that get (1) wrong.
//   3. a minimum margin     — for rounded corners, and for devices that report
//                             neither, where nothing can be detected at all.
//
// Recomputes only when something actually changes.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    [Tooltip("Minimum inset on every edge, as a fraction of the SHORTER screen dimension. " +
             "Covers rounded corners. 0.015 is about 18px on a 1179-wide phone.")]
    [Range(0f, 0.1f)] public float minEdgeInset = 0.015f;

    [Tooltip("Extra inset on the TOP edge only, as a fraction of screen height. Raise this " +
             "if a specific phone still clips the HUD under its camera — that means the " +
             "device reports neither a safe area nor a cutout, and nothing can detect it. " +
             "0.035 clears a typical punch-hole.")]
    [Range(0f, 0.15f)] public float extraTopInset = 0f;

    RectTransform rt;
    Rect lastSafe = new Rect(0, 0, 0, 0);
    Vector2Int lastScreen;
    int lastCutoutCount = -1;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
        // Cutouts can arrive a frame or two after startup, and rotate with the device.
        if (Screen.safeArea != lastSafe
            || Screen.width != lastScreen.x || Screen.height != lastScreen.y
            || Screen.cutouts.Length != lastCutoutCount)
            Apply();
    }

    void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        lastSafe = Screen.safeArea;
        lastScreen = new Vector2Int(Screen.width, Screen.height);

        var cutouts = Screen.cutouts;
        lastCutoutCount = cutouts.Length;

        Rect s = Screen.safeArea;

        // (2) Push each edge past any cutout that still intrudes. A cutout is attributed
        // to whichever screen edge it sits nearest, so a top punch-hole lowers the top
        // edge rather than eating the whole screen.
        foreach (var c in cutouts)
        {
            if (!c.Overlaps(s)) continue;

            float toTop = Screen.height - c.yMax;
            float toBottom = c.yMin;
            float toLeft = c.xMin;
            float toRight = Screen.width - c.xMax;
            float nearest = Mathf.Min(Mathf.Min(toTop, toBottom), Mathf.Min(toLeft, toRight));

            if (nearest == toTop) s.yMax = Mathf.Min(s.yMax, c.yMin);
            else if (nearest == toBottom) s.yMin = Mathf.Max(s.yMin, c.yMax);
            else if (nearest == toLeft) s.xMin = Mathf.Max(s.xMin, c.xMax);
            else s.xMax = Mathf.Min(s.xMax, c.xMin);
        }

        // (3) Minimum margin, so the HUD never sits flush against a rounded corner.
        float floor = Mathf.Min(Screen.width, Screen.height) * minEdgeInset;
        s.xMin = Mathf.Max(s.xMin, floor);
        s.yMin = Mathf.Max(s.yMin, floor);
        s.xMax = Mathf.Min(s.xMax, Screen.width - floor);
        s.yMax = Mathf.Min(s.yMax, Screen.height - floor);

        float top = Screen.height * extraTopInset;
        if (top > 0f) s.yMax = Mathf.Min(s.yMax, Screen.height - top);

        // Never invert: a bad report must not collapse the HUD to nothing.
        if (s.width <= 1f || s.height <= 1f) { s = new Rect(0, 0, Screen.width, Screen.height); }

        Vector2 min = new Vector2(s.xMin / Screen.width, s.yMin / Screen.height);
        Vector2 max = new Vector2(s.xMax / Screen.width, s.yMax / Screen.height);

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>What the device actually reported, for the on-screen diagnostic.</summary>
    public string DescribeDevice()
    {
        var c = Screen.cutouts;
        string cut = c.Length == 0 ? "none" : string.Join(", ", System.Array.ConvertAll(c, r => r.ToString()));
        return $"screen {Screen.width}x{Screen.height}\n" +
               $"safeArea {Screen.safeArea}\n" +
               $"cutouts ({c.Length}): {cut}\n" +
               $"applied anchors {rt.anchorMin} .. {rt.anchorMax}";
    }
}
