using UnityEngine;
using TMPro;

// Small dim readout in the settings sheet: build version, screen size, and what the
// device actually reported for its safe area and cutouts.
//
// This exists because notch bugs are undebuggable from the editor — Screen.safeArea is
// the whole screen there, so the HUD always looks fine no matter how wrong it is on
// hardware. When someone says "it clips the camera on my phone", this line is the
// difference between fixing it and guessing at a magic number.
//
// It also doubles as the version stamp every bug report should carry.
public class DeviceInfoLabel : MonoBehaviour
{
    public TMP_Text label;
    public SafeArea safeArea;

    void OnEnable() => Refresh();

    public void Refresh()
    {
        if (label == null) return;
        if (safeArea == null) safeArea = FindFirstObjectByType<SafeArea>();

        var s = Screen.safeArea;
        int cutouts = Screen.cutouts.Length;

        // Inset actually applied at each edge, in pixels — the number that matters.
        int left = Mathf.RoundToInt(s.xMin);
        int right = Mathf.RoundToInt(Screen.width - s.xMax);
        int bottom = Mathf.RoundToInt(s.yMin);
        int top = Mathf.RoundToInt(Screen.height - s.yMax);

        label.text =
            $"v{Application.version}  ·  {Screen.width}x{Screen.height}  ·  {Application.platform}\n" +
            $"safe insets  T{top} B{bottom} L{left} R{right}  ·  cutouts {cutouts}";
    }
}
