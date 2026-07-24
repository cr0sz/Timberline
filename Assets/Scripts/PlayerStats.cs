using UnityEngine;

// Lifetime run counters (cumulative, never decrease) that objectives read.
// Separate from PlayerInventory, whose amounts go up and down with sell/spend.
public class PlayerStats : MonoBehaviour
{
    public event System.Action OnStatsChanged;

    int gatheredWood, gatheredStone, gatheredTotal, creaturesKilled;

    public int GatheredWood => gatheredWood;
    public int GatheredStone => gatheredStone;
    public int GatheredTotal => gatheredTotal;
    public int CreaturesKilled => creaturesKilled;

    public void AddGathered(ResourceType type, int amount)
    {
        if (amount <= 0) return;
        gatheredTotal += amount;
        if (type == ResourceType.Wood) gatheredWood += amount;
        else if (type == ResourceType.Stone) gatheredStone += amount;
        OnStatsChanged?.Invoke();
    }

    public void AddKill()
    {
        creaturesKilled++;
        OnStatsChanged?.Invoke();
    }

    public void LoadStats(int wood, int stone, int total, int kills)
    {
        gatheredWood = Mathf.Max(0, wood);
        gatheredStone = Mathf.Max(0, stone);
        gatheredTotal = Mathf.Max(0, total);
        creaturesKilled = Mathf.Max(0, kills);
        OnStatsChanged?.Invoke();
    }
}
