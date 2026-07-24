using UnityEngine;

// First-run how-to-play card. Shows once, gated on PlayerPrefs, so a returning
// player never sees it again. The dismiss button sets the gate. No gameplay hooks
// and no pause — the player spawns stationary and the modal scrim holds their
// attention until they tap GOT IT.
public class IntroTutorial : MonoBehaviour
{
    public GameObject panel;
    // Public so SaveManager.DeleteSave can clear it — "New Game" has to un-see the
    // intro, or a wiped player is dropped into a fresh world with no explanation.
    public const string SeenKey = "SeenIntro";

    void Start()
    {
        bool seen = PlayerPrefs.GetInt(SeenKey, 0) == 1;
        if (panel != null) panel.SetActive(!seen);
    }

    // Wired to the card's GOT IT button.
    public void Dismiss()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        if (panel != null) panel.SetActive(false);
    }
}
