using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Shop : MonoBehaviour
{
    [Header("Player systems (drag the Player here)")]
    public PlayerInventory inventory;
    public ToolInventory toolInventory;
    public PlayerController playerController;

    [Header("UI")]
    public GameObject panel;        // the ShopPanel window (hidden until we walk in)
    public PlayerHealth playerHealth;   // read to refuse trading mid-fight
    public FloatingText floatingTextPrefab;  // optional — "Not while fighting!" toast

    // Each card shows: name+level on the label, price on the cost line, a Buy button.
    [Header("Name + level labels")]
    public TMP_Text axeLabel;
    public TMP_Text pickaxeLabel;
    public TMP_Text capacityLabel;
    public TMP_Text speedLabel;
    public TMP_Text weaponLabel;

    [Header("Cost lines (optional — leave empty if unused)")]
    public TMP_Text axeCostText;
    public TMP_Text pickaxeCostText;
    public TMP_Text capacityCostText;
    public TMP_Text speedCostText;
    public TMP_Text weaponCostText;

    [Header("Buy buttons")]
    public Button axeButton;
    public Button pickaxeButton;
    public Button capacityButton;
    public Button speedButton;
    public Button weaponButton;

    [Header("Sell")]
    public Button sellButton;       // "SELL ALL" — dumps carried resources for coins
    public TMP_Text sellButtonText; // shows how many coins you'd earn

    // --- Upgrade prices (in COINS now — you sell resources to earn coins) ---
    [Header("Upgrade costs (coins)")]
    public int axeBaseCost = 20;
    public int pickaxeBaseCost = 20;
    public int capacityBaseCost = 40;
    public int speedBaseCost = 40;
    public int weaponBaseCost = 50;
    [Tooltip("Every level multiplies the price by this. 1.0 = flat, 1.25 = climbs ~25% per level.")]
    public float costGrowth = 1.25f;   // was 1.4 — the exponential tail made maxing a tool ~5500 coins
    public int capacityStep = 25;      // +25 carry room per upgrade
    // +0.35/lvl x10 lands moveSpeed at 4 -> 7.5. It was 0.5, which reached 9 — a
    // sprint that crossed the whole 200m map fast enough to make the zone gating
    // meaningless. Retune here if the map ever grows.
    public float speedStep = 0.35f;

    // --- Sell prices: coins earned per unit of each resource ---
    [Header("Sell prices (coins per unit)")]
    public int woodPrice = 3;
    public int stonePrice = 3;   // was 6 — double-wood let Lv1/Lv5 rock zones out-earn the Lv10/Lv15 forests
    public int meatPrice = 10;
    public int hidePrice = 20;

    [Header("Caps")]
    public int maxToolTier = 15;
    public int maxCapacityLevel = 12;
    public int maxSpeedLevel = 10;

    const string CoinHex = "#F2C14E";

    int capacityLevel = 1;
    int speedLevel = 1;

    // --- Save/load support: these counters drive pricing/labels; the actual
    // effects (bag capacity, moveSpeed) are stored + restored elsewhere. ---
    public int CapacityLevel => capacityLevel;
    public int SpeedLevel => speedLevel;
    public void LoadLevels(int cap, int spd)
    {
        capacityLevel = Mathf.Max(1, cap);
        speedLevel = Mathf.Max(1, spd);
    }

    void Start()
    {
        // Event-driven refresh: repaint only when something the cards read changes,
        // not every open frame. OnInventoryChanged covers coins, carried resources,
        // capacity, and (via SpendCoins) every tool/upgrade buy.
        if (inventory != null) inventory.OnInventoryChanged += Refresh;
        if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    // You can't shop mid-fight. PlayerHealth already tracks a post-hit window for the
    // heal lockout, so reuse it rather than inventing a second notion of "in combat" —
    // one timer, one meaning, and the campfire and the shop agree on it.
    bool InCombat => playerHealth != null && playerHealth.HealBlocked;

    void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= Refresh;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() != null) TryOpen();
    }

    void TryOpen()
    {
        if (panel == null || panel.activeSelf) return;
        if (InCombat) { Toast("Not while you're fighting!"); return; }
        panel.SetActive(true);
        Refresh();   // paint once on open; changes after this come via the event
    }

    void Toast(string m)
    {
        if (floatingTextPrefab == null || playerController == null) return;
        FloatingText.Spawn(floatingTextPrefab, playerController.transform.position + Vector3.up * 2.2f, m);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() != null)
        {
            inRange = false;
            if (panel != null) panel.SetActive(false);
        }
    }

    bool inRange;

    void OnTriggerStay(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() != null) inRange = true;
    }

    void Update()
    {
        if (panel == null) return;

        // Getting hit slams the shop shut mid-browse — you don't haggle with a bear on
        // you. Reopening is handled here rather than in OnTriggerEnter, which never
        // fires again while the player is already standing inside the trigger.
        if (panel.activeSelf && InCombat) { panel.SetActive(false); return; }
        if (!panel.activeSelf) { if (inRange && !InCombat) TryOpen(); return; }

        // OnTriggerExit is missed if the player is teleported away (death
        // respawn) while inside, leaving the panel stuck open. Close on distance
        // as a backstop.
        if (playerController != null &&
            Vector3.Distance(playerController.transform.position, transform.position) > 12f)
        {
            inRange = false;
            panel.SetActive(false);
        }
    }

    // --- Cost formula: price climbs geometrically with each level bought ---
    int CostAt(int baseCost, int level) => Mathf.RoundToInt(baseCost * Mathf.Pow(costGrowth, level - 1));
    int AxeCost()      => CostAt(axeBaseCost, toolInventory.axeTier);
    int PickaxeCost()  => CostAt(pickaxeBaseCost, toolInventory.pickaxeTier);
    int CapacityCost() => CostAt(capacityBaseCost, capacityLevel);
    int SpeedCost()    => CostAt(speedBaseCost, speedLevel);
    int WeaponCost()   => CostAt(weaponBaseCost, toolInventory.weaponTier);

    bool AxeMaxed()      => toolInventory.axeTier >= maxToolTier;
    bool PickMaxed()     => toolInventory.pickaxeTier >= maxToolTier;
    bool CapacityMaxed() => capacityLevel >= maxCapacityLevel;
    bool SpeedMaxed()    => speedLevel >= maxSpeedLevel;
    bool WeaponMaxed()   => toolInventory.weaponTier >= maxToolTier;

    // Coins earned per unit for a given resource.
    public int PriceOf(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:  return woodPrice;
            case ResourceType.Stone: return stonePrice;
            case ResourceType.Meat:  return meatPrice;
            case ResourceType.Hide:  return hidePrice;
            default: return 1;
        }
    }

    // Gold sparkle over the player on any successful buy.
    void PurchaseFX()
    {
        if (playerController != null)
            VFXManager.Spark(playerController.transform.position + Vector3.up * 1.2f, new Color(0.95f, 0.78f, 0.32f));
    }

    // --- Buttons ---
    public void SellAll()
    {
        int earned = inventory.SellAll(PriceOf);   // converts everything carried into coins
        if (earned > 0) { AudioManager.Sell(); UIFeedback.Success(sellButton); }
        else UIFeedback.Fail(sellButton);          // silent when there was nothing to sell
    }

    public void BuyAxe()
    {
        if (AxeMaxed() || !inventory.CanAffordCoins(AxeCost())) { UIFeedback.Fail(axeButton); return; }
        inventory.SpendCoins(AxeCost());
        toolInventory.axeTier++;
        AudioManager.Purchase();
        PurchaseFX();
        UIFeedback.Success(axeButton);
    }

    public void BuyPickaxe()
    {
        if (PickMaxed() || !inventory.CanAffordCoins(PickaxeCost())) { UIFeedback.Fail(pickaxeButton); return; }
        inventory.SpendCoins(PickaxeCost());
        toolInventory.pickaxeTier++;
        AudioManager.Purchase();
        PurchaseFX();
        UIFeedback.Success(pickaxeButton);
    }

    public void BuyCapacity()
    {
        if (CapacityMaxed() || !inventory.CanAffordCoins(CapacityCost())) { UIFeedback.Fail(capacityButton); return; }
        inventory.SpendCoins(CapacityCost());
        capacityLevel++;
        inventory.AddCapacity(capacityStep);
        AudioManager.Purchase();
        PurchaseFX();
        UIFeedback.Success(capacityButton);
    }

    public void BuySpeed()
    {
        if (SpeedMaxed() || !inventory.CanAffordCoins(SpeedCost())) { UIFeedback.Fail(speedButton); return; }
        inventory.SpendCoins(SpeedCost());
        speedLevel++;
        playerController.moveSpeed += speedStep;
        AudioManager.Purchase();
        PurchaseFX();
        UIFeedback.Success(speedButton);
    }

    public void BuyWeapon()
    {
        if (WeaponMaxed() || !inventory.CanAffordCoins(WeaponCost())) { UIFeedback.Fail(weaponButton); return; }
        inventory.SpendCoins(WeaponCost());
        toolInventory.weaponTier++;
        AudioManager.Purchase();
        PurchaseFX();
        UIFeedback.Success(weaponButton);
    }

    // --- Repaint every open frame: labels, prices, and grey-out state ---
    void Refresh()
    {
        Paint(axeLabel, axeCostText, axeButton, "Axe", toolInventory.axeTier, AxeMaxed(), AxeCost());
        Paint(pickaxeLabel, pickaxeCostText, pickaxeButton, "Pickaxe", toolInventory.pickaxeTier, PickMaxed(), PickaxeCost());
        Paint(capacityLabel, capacityCostText, capacityButton, "Bag", capacityLevel, CapacityMaxed(), CapacityCost());
        Paint(speedLabel, speedCostText, speedButton, "Speed", speedLevel, SpeedMaxed(), SpeedCost());
        Paint(weaponLabel, weaponCostText, weaponButton, "Weapon", toolInventory.weaponTier, WeaponMaxed(), WeaponCost());
        RefreshSell();
    }

    void RefreshSell()
    {
        if (sellButton == null) return;
        int worth = 0;
        foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
            worth += inventory.GetAmount(t) * PriceOf(t);
        sellButton.interactable = worth > 0;
        if (sellButtonText != null)
            sellButtonText.text = worth > 0 ? $"SELL ALL  <color={CoinHex}>+{worth}</color>" : "SELL ALL";
    }

    // One card's worth of text + interactable state. Everything costs coins now.
    void Paint(TMP_Text label, TMP_Text costText, Button button, string name, int level, bool maxed, int cost)
    {
        if (label != null)
            label.text = maxed ? $"{name}\n<size=60%>MAX LEVEL</size>"
                               : $"{name}\n<size=60%>Lv {level}</size>";

        if (costText != null)
            costText.text = maxed ? "—" : $"<color={CoinHex}>{cost}</color>";

        if (button != null)
            button.interactable = !maxed && inventory.CanAffordCoins(cost);
    }
}
