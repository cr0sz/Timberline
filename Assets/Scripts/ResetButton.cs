using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Guarded "New Game" reset. First tap arms (label -> a confirm prompt); a second
// tap within the window deletes the save and reloads the scene. Two taps so a
// stray touch can't wipe a run. Wires its own Button in Awake — no inspector
// onClick hookup needed.
[RequireComponent(typeof(Button))]
public class ResetButton : MonoBehaviour
{
    public TMP_Text label;
    public string idleText = "New Game";
    public string armedText = "Tap to wipe!";
    public float armWindow = 3f;

    bool armed;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
        if (label != null) label.text = idleText;
    }

    public void OnClick()
    {
        if (armed)
        {
            var sm = FindFirstObjectByType<SaveManager>();
            if (sm != null) sm.DeleteSave();
            // This button now lives inside the pause menu, so timeScale is 0 when it
            // is pressed. LoadScene does NOT reset it — without this the fresh scene
            // loads frozen solid.
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }
        armed = true;
        if (label != null) label.text = armedText;
        CancelInvoke(nameof(Disarm));
        Invoke(nameof(Disarm), armWindow);
    }

    void Disarm()
    {
        armed = false;
        if (label != null) label.text = idleText;
    }
}
