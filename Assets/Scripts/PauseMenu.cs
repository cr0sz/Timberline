using UnityEngine;
using UnityEngine.UI;
using TMPro;


// Pause + sound, the two things a phone game is expected to have and this one had
// neither of. Freezes with Time.timeScale, so anything that must keep animating
// while paused has to run on unscaled time (PanelPop and UIFeedback already do).
//
// Mute drives AudioListener.volume rather than AudioManager.volume: the manager's
// master is a mix knob that the per-event trims multiply into, and stamping it to
// zero would lose the author's setting. AudioListener sits above the whole mix.
public class PauseMenu : MonoBehaviour
{
    public GameObject panel;

    [Header("Sound switch")]
    public TMP_Text soundLabel;          // reads ON / OFF
    public Image soundTrack;             // the switch body, recoloured per state
    public RectTransform soundKnob;      // slides between the two x positions below
    public float knobOnX = 26f;
    public float knobOffX = -26f;
    public Color trackOn = new Color(0.91f, 0.64f, 0.29f, 1f);
    public Color trackOff = new Color(0.27f, 0.22f, 0.18f, 1f);

    const string MuteKey = "survival.muted";

    public bool Paused { get; private set; }
    public static bool Muted
    {
        get => PlayerPrefs.GetInt(MuteKey, 0) == 1;
        private set { PlayerPrefs.SetInt(MuteKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    void Awake()
    {
        // Apply the saved preference before anything can make a sound.
        AudioListener.volume = Muted ? 0f : 1f;
    }

    void Start()
    {
        // A scene reload inherits whatever timeScale was left behind, so never
        // assume it starts at 1 — New Game from a paused menu would load frozen.
        Time.timeScale = 1f;
        Paused = false;
        if (panel != null) panel.SetActive(false);
        RefreshLabels();
    }

    void Update()
    {
        // Escape is also the Android BACK button, which previously did nothing.
        if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
    }

    public void Toggle()
    {
        if (Paused) Resume(); else Pause();
    }

    public void Pause()
    {
        Paused = true;
        Time.timeScale = 0f;
        if (panel != null) panel.SetActive(true);
        RefreshLabels();
    }

    public void Resume()
    {
        Paused = false;
        Time.timeScale = 1f;
        if (panel != null) panel.SetActive(false);
        RefreshLabels();
    }

    public void ToggleMute()
    {
        Muted = !Muted;
        AudioListener.volume = Muted ? 0f : 1f;
        RefreshLabels();
    }

    void RefreshLabels()
    {
        bool on = !Muted;
        if (soundLabel != null) soundLabel.text = on ? "ON" : "OFF";
        if (soundTrack != null) soundTrack.color = on ? trackOn : trackOff;
        if (soundKnob != null)
        {
            var p = soundKnob.anchoredPosition;
            p.x = on ? knobOnX : knobOffX;
            soundKnob.anchoredPosition = p;
        }
    }

    // Leaving the scene paused would freeze the next one — always unfreeze first.
    void OnDisable()
    {
        if (Paused) Time.timeScale = 1f;
    }
}
