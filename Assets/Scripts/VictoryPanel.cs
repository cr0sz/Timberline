using UnityEngine;

// Pops once when the last objective is cleared. ObjectiveManager only raises
// OnAllComplete on a live transition, never when a finished save is loaded, so
// this can't nag on every boot.
public class VictoryPanel : MonoBehaviour
{
    public GameObject panel;
    public ObjectiveManager objectives;

    void Awake()
    {
        if (objectives == null) objectives = FindFirstObjectByType<ObjectiveManager>();
        if (panel != null) panel.SetActive(false);
    }

    void OnEnable()
    {
        if (objectives != null) objectives.OnAllComplete += Show;
    }

    void OnDisable()
    {
        if (objectives != null) objectives.OnAllComplete -= Show;
    }

    void Show()
    {
        if (panel != null) panel.SetActive(true);
    }

    // Wired to the panel's dismiss button.
    public void Dismiss()
    {
        if (panel != null) panel.SetActive(false);
    }
}
