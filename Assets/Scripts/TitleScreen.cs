using UnityEngine;
using TMPro;

// The game's front door. There is still only one scene — the title is a modal over
// the already-loaded map with timeScale pinned to 0, not a separate boot scene. That
// costs one panel instead of a scene, a loader and a second set of build settings,
// and the map is already there behind the scrim when the player taps PLAY.
//
// Runs after PauseMenu, which forces timeScale back to 1 in its own Start() to undo a
// frozen scene reload. Without the explicit order the two race and the game sometimes
// starts running behind the title.
[DefaultExecutionOrder(100)]
public class TitleScreen : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text playLabel;       // "PLAY" on a fresh install, "CONTINUE" with a save
    public GameObject newGameBtn;    // hidden when there is nothing to wipe
    public IntroTutorial intro;
    public SaveManager save;

    // HUD, BUILD and the settings cog. The scrim alone is not enough to retire them:
    // the project renders in linear colour space, where a 0.9-alpha black overlay
    // still leaves about 30% of the original luminance, so the HP bar and the BUILD
    // button stayed plainly legible through it. A title screen showing a live health
    // bar reads as a bug, so they are switched off outright and restored on PLAY.
    public GameObject[] hideWhileShown;

    // PauseMenu reads this so Escape / Android BACK can't open the settings sheet
    // underneath the title — Resume() would set timeScale to 1 and start the world
    // running behind a screen the player hasn't dismissed.
    public static bool Showing { get; private set; }

    void Awake()
    {
        // Claim the intro before IntroTutorial.Start() can put it on screen.
        if (intro != null) intro.deferred = true;
    }

    void Start()
    {
        bool hasRun = save != null && save.HasSave;
        if (playLabel != null) playLabel.text = hasRun ? "CONTINUE" : "PLAY";
        if (newGameBtn != null) newGameBtn.SetActive(hasRun);

        Showing = true;
        Time.timeScale = 0f;
        if (panel != null) panel.SetActive(true);
        SetChromeVisible(false);
    }

    // Wired to the PLAY / CONTINUE button.
    public void Play()
    {
        Showing = false;
        Time.timeScale = 1f;
        if (panel != null) panel.SetActive(false);
        SetChromeVisible(true);
        // Now that the title is gone, a first-time player gets the how-to-play card.
        if (intro != null) intro.ShowIfUnseen();
    }

    void SetChromeVisible(bool on)
    {
        if (hideWhileShown == null) return;
        foreach (var go in hideWhileShown)
            if (go != null) go.SetActive(on);
    }

    // A scene reload (New Game) starts a fresh TitleScreen, but the static survives it.
    void OnDisable() => Showing = false;
}
