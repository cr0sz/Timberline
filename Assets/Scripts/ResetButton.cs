using System.Collections;
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
        // Realtime, not Invoke: every screen this button lives on (pause sheet, title)
        // runs at timeScale 0, where Invoke's scaled clock never advances — the button
        // would stay armed forever and the next stray tap would wipe the save.
        StopAllCoroutines();
        StartCoroutine(DisarmAfter(armWindow));
    }

    IEnumerator DisarmAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        armed = false;
        if (label != null) label.text = idleText;
    }
}
