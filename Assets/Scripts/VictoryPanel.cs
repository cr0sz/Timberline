using UnityEngine;
using TMPro;

// Pops once when the last objective is cleared. ObjectiveManager only raises
// OnAllComplete on a live transition, never when a finished save is loaded, so
// this can't nag on every boot.
//
// It is also the door to prestige: mastering the valley is the only way to start a
// New Valley, and the panel offers it with the camp kept or razed. Dismiss leaves
// the run exactly as it was, so nobody is forced to reset.
public class VictoryPanel : MonoBehaviour
{
    public GameObject panel;
    public ObjectiveManager objectives;
    public SaveManager save;

    [Tooltip("Reads the bonus the NEXT valley will carry, e.g. 'Next valley: +50% on every sale'.")]
    public TMP_Text bonusLabel;

    void Awake()
    {
        if (objectives == null) objectives = FindFirstObjectByType<ObjectiveManager>();
        if (save == null) save = FindFirstObjectByType<SaveManager>();
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
        if (bonusLabel != null)
        {
            // The bonus quoted is the one AFTER this reset, which is what the player is
            // deciding about — quoting the current (already earned) one would read as
            // an offer of something they are about to lose.
            int next = Prestige.ValleysMastered + 1;
            int pct = Mathf.RoundToInt(Prestige.BonusPerValley * next * 100f);
            bonusLabel.text = $"A new valley pays <color=#F2C14E>+{pct}%</color> on every sale.";
        }
        if (panel != null) panel.SetActive(true);
    }

    // --- Buttons ---

    // Keep every structure you placed; reset the run around them.
    public void NewValleyKeepCamp() => Prestige_(true);

    // Bare ground, rebuild from nothing.
    public void NewValleyRazeCamp() => Prestige_(false);

    void Prestige_(bool keepCamp)
    {
        if (save == null) { Debug.LogWarning("[VictoryPanel] no SaveManager — cannot start a new valley."); return; }
        save.PrestigeReset(keepCamp);   // writes the new save and reloads the scene
    }

    // Wired to the panel's dismiss button. Keeps playing the finished run.
    public void Dismiss()
    {
        if (panel != null) panel.SetActive(false);
    }
}
