using UnityEngine;

// Insets this (stretched) RectTransform to the device Screen.safeArea so child
// UI clears notches / camera cutouts / rounded corners. Parent the gameplay HUD
// under it; leave full-bleed elements (touch zone, hit flash) outside.
// Recomputes only when the safe area or screen size actually changes.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform rt;
    Rect lastSafe = new Rect(0, 0, 0, 0);
    Vector2Int lastScreen;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        Apply();
    }

    void Update()
    {
        if (Screen.safeArea != lastSafe || Screen.width != lastScreen.x || Screen.height != lastScreen.y)
            Apply();
    }

    void Apply()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        lastSafe = Screen.safeArea;
        lastScreen = new Vector2Int(Screen.width, Screen.height);

        Rect s = Screen.safeArea;
        Vector2 min = s.position;
        Vector2 max = s.position + s.size;
        min.x /= Screen.width;  min.y /= Screen.height;
        max.x /= Screen.width;  max.y /= Screen.height;

        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
