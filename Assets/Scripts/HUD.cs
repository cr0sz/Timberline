using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public PlayerInventory inventory;
    public PlayerHealth health;
    public TMP_Text woodText;
    public TMP_Text stoneText;
    public TMP_Text meatText;
    public TMP_Text hideText;
    public TMP_Text coinsText;
    public TMP_Text capacityText;
    public Image capacityBar;
    public Image healthBar;
    public TMP_Text healthText;
    public ObjectiveManager objectives;
    public TMP_Text objectiveText;
    public TMP_Text objectiveCountText;   // "3/10" on the right of the banner
    public Image objectiveBar;            // filled progress bar under the label

    void OnEnable()
    {
        inventory.OnInventoryChanged += Refresh;
        if (health != null) health.OnHealthChanged += RefreshHealth;
        if (objectives != null) objectives.OnObjectiveChanged += RefreshObjective;
        Refresh();
        RefreshHealth(0);
        RefreshObjective();
    }

    void OnDisable()
    {
        inventory.OnInventoryChanged -= Refresh;
        if (health != null) health.OnHealthChanged -= RefreshHealth;
        if (objectives != null) objectives.OnObjectiveChanged -= RefreshObjective;
    }

    void Refresh()
    {
        woodText.text = inventory.GetAmount(ResourceType.Wood).ToString();
        stoneText.text =  inventory.GetAmount(ResourceType.Stone).ToString();
        // Meat/Hide are carried but sell-only — they need a pill or the bag reads wrong.
        if (meatText != null) meatText.text = inventory.GetAmount(ResourceType.Meat).ToString();
        if (hideText != null) hideText.text = inventory.GetAmount(ResourceType.Hide).ToString();
        if (coinsText != null) coinsText.text = inventory.coins.ToString();
        capacityText.text = $"{inventory.TotalCarried()}/{inventory.capacity}";

        if (capacityBar != null)
            capacityBar.fillAmount = inventory.capacity > 0
                ? (float)inventory.TotalCarried() / inventory.capacity
                : 0f;
    }

    // Separate handler so a health change doesn't rebuild the inventory strings.
    // The int arg (currentHealth) is unused — we read the fraction off the component.
    void RefreshHealth(int _)
    {
        if (health == null) return;
        if (healthBar != null) healthBar.fillAmount = health.HealthFraction;
        if (healthText != null) healthText.text = $"{health.CurrentHealth}/{health.MaxHealth}";
    }

    void RefreshObjective()
    {
        if (objectives == null) return;
        bool done = objectives.AllDone;
        int progress = objectives.CurrentProgress;
        int target = objectives.CurrentTarget;

        if (objectiveText != null) objectiveText.text = objectives.CurrentLabel;
        if (objectiveCountText != null) objectiveCountText.text = done ? "" : $"{progress}/{target}";
        if (objectiveBar != null)
            objectiveBar.fillAmount = done ? 1f : (target > 0 ? (float)progress / target : 0f);
    }
}