using UnityEngine;

// One-time mobile runtime setup. Lives on GameManager. Uncaps the framerate to
// 60 (many Androids idle at 30 until told otherwise) and, on a real handheld,
// forces the Mobile quality level regardless of the editor's per-platform default.
public class MobileBootstrap : MonoBehaviour
{
    public int targetFrameRate = 60;
    [Tooltip("Quality level name to force on handheld devices (case-insensitive).")]
    public string mobileQualityName = "Mobile";

    void Awake()
    {
        QualitySettings.vSyncCount = 0;            // vSync overrides targetFrameRate; disable it
        Application.targetFrameRate = targetFrameRate;

        if (SystemInfo.deviceType == DeviceType.Handheld)
            ForceQuality(mobileQualityName);
    }

    void ForceQuality(string levelName)
    {
        var names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
            if (string.Equals(names[i], levelName, System.StringComparison.OrdinalIgnoreCase))
            {
                QualitySettings.SetQualityLevel(i, true);
                return;
            }
    }
}
