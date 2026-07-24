using UnityEngine;
using TMPro;

// The game's front door. There is still only one scene — the title is a modal over
// the already-loaded map with timeScale pinned to 0, not a separate boot scene. That
// costs one panel instead of a scene, a loader and a second set of build settings.
//
// While it is up the camera is lifted off the player and parked high over the valley,
// slowly orbiting, so the first thing anyone sees is the map rather than the patch of
// dirt the player happens to be standing on.
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
    // button stayed plainly legible through it. Switched off outright, restored on PLAY.
    public GameObject[] hideWhileShown;

    [Header("Scenic camera")]
    public PlayerController player;
    [Tooltip("World point the title camera looks at. Camp is the origin; +Z is north up the valley.")]
    public Vector3 focus = new Vector3(0f, 0f, 6f);
    [Tooltip("Matches the ScreenshotTool hero shot: the game rig (8 up, 6 back) pulled back 4.5x.")]
    public float height = 36f;
    public float distance = 27f;
    public float pitch = 50f;
    [Tooltip("Degrees per second. A full turn takes ~2.5 minutes at 2.4 — slow enough to read as drift, not motion.")]
    public float orbitSpeed = 2.4f;

    // PauseMenu reads this so Escape / Android BACK can't open the settings sheet
    // underneath the title — Resume() would set timeScale to 1 and start the world
    // running behind a screen the player hasn't dismissed.
    public static bool Showing { get; private set; }

    Transform cam;
    Vector3 camLocalPos;
    Quaternion camLocalRot;
    float yaw;

    void Awake()
    {
        // Claim the intro before IntroTutorial.Start() can put it on screen.
        if (intro != null) intro.deferred = true;
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
            // The camera is a CHILD of the follow rig. Remember where it sits so PLAY
            // can put it back exactly, rather than guessing the authored offset.
            camLocalPos = cam.localPosition;
            camLocalRot = cam.localRotation;
        }
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

        if (player != null) player.cameraFollowEnabled = false;
        PlaceCamera();
    }

    void LateUpdate()
    {
        // LateUpdate, and after PlayerController's — otherwise the follow rig's own
        // LateUpdate would fight this on the frame the title is dismissed.
        if (!Showing) return;
        // UNSCALED: timeScale is 0 while the title is up, so a scaled clock would
        // freeze the orbit dead.
        yaw += orbitSpeed * Time.unscaledDeltaTime;
        PlaceCamera();
    }

    void PlaceCamera()
    {
        if (cam == null) return;
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        cam.SetPositionAndRotation(focus + rot * new Vector3(0f, 0f, -distance) + Vector3.up * height, rot);
    }

    // Wired to the PLAY / CONTINUE button.
    public void Play()
    {
        Showing = false;
        Time.timeScale = 1f;
        if (panel != null) panel.SetActive(false);
        SetChromeVisible(true);

        // Hand the camera back: restore the authored local offset, then drop the rig
        // onto the player so it does not smooth-damp across the whole valley.
        if (cam != null) cam.SetLocalPositionAndRotation(camLocalPos, camLocalRot);
        if (player != null)
        {
            player.SnapCameraToPlayer();
            player.cameraFollowEnabled = true;
        }

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
