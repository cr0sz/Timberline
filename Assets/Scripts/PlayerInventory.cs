using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public int capacity = 50;
    public int coins = 0;
    public event Action OnInventoryChanged;

    Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        coins += amount;
        OnInventoryChanged?.Invoke();
    }

    public bool CanAffordCoins(int amount) => coins >= amount;

    public void SpendCoins(int amount)
    {
        coins = Mathf.Max(0, coins - amount);
        OnInventoryChanged?.Invoke();
    }

    // Convert everything carried into coins at the given per-resource prices.
    // Returns coins earned (0 if nothing to sell). Empties the carried resources.
    public int SellAll(Func<ResourceType, int> priceOf)
    {
        int earned = 0;
        foreach (var kv in new List<KeyValuePair<ResourceType, int>>(resources))
            earned += kv.Value * priceOf(kv.Key);
        if (earned <= 0) return 0;
        resources.Clear();
        coins += earned;
        OnInventoryChanged?.Invoke();
        return earned;
    }


    public void AddCapacity(int amount)
    {
        capacity += amount;
        OnInventoryChanged?.Invoke();
    }

    public int GetAmount(ResourceType type)
    {
        return resources.TryGetValue(type, out int amount) ? amount : 0;
    }

    public int TotalCarried()
    {
        int total = 0;
        foreach (var amount in resources.Values) total += amount;
        return total;
    }

    public bool IsFull => TotalCarried() >= capacity;

    // Returns how much actually fit. Overflow is discarded silently here on purpose —
    // callers decide how to surface it (PlayerGatherer throws a "Bag full!" toast).
    public int Add(ResourceType type, int amount)
    {
        int spaceLeft = capacity - TotalCarried();
        int actualAmount = Mathf.Min(amount, spaceLeft);
        if (actualAmount <= 0) return 0;

        if (!resources.ContainsKey(type)) resources[type] = 0;
        resources[type] += actualAmount;
        OnInventoryChanged?.Invoke();
        return actualAmount;
    }

    public bool CanAfford(ResourceType type, int amount)
    {
        return GetAmount(type) >= amount;
    }

    public void Spend(ResourceType type, int amount)
    {
        // Guard against spending a resource that was never gathered — indexing a
        // missing key throws KeyNotFoundException. Callers should check CanAfford,
        // but don't crash if one forgets.
        if (!resources.TryGetValue(type, out int have)) return;
        resources[type] = Mathf.Max(0, have - amount);
        OnInventoryChanged?.Invoke();
    }

    // Death penalty: drop a fraction of everything carried. Rounds up so dying with
    // a single item still costs you something — otherwise death is free at low hauls.
    // Returns how many units were lost (0 if the bag was empty).
    public int LoseFraction(float fraction)
    {
        if (fraction <= 0f || resources.Count == 0) return 0;
        int lost = 0;
        foreach (var kv in new List<KeyValuePair<ResourceType, int>>(resources))
        {
            if (kv.Value <= 0) continue;
            int drop = Mathf.Clamp(Mathf.CeilToInt(kv.Value * fraction), 0, kv.Value);
            resources[kv.Key] = kv.Value - drop;
            lost += drop;
        }
        if (lost > 0) OnInventoryChanged?.Invoke();
        return lost;
    }

    // --- Save/load support ---

    // Snapshot carried resources as parallel arrays (JsonUtility can't do dicts).
    public void SnapshotResources(out int[] types, out int[] amounts)
    {
        var list = new List<KeyValuePair<ResourceType, int>>(resources);
        types = new int[list.Count];
        amounts = new int[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            types[i] = (int)list[i].Key;
            amounts[i] = list[i].Value;
        }
    }

    // Restore from a save. Sets coins/capacity/resources DIRECTLY — never replays
    // the cumulative AddCapacity, so reloading can't double the bag size.
    public void LoadState(int coinsIn, int capacityIn, int[] types, int[] amounts)
    {
        coins = coinsIn;
        capacity = capacityIn;
        resources.Clear();
        if (types != null && amounts != null)
            for (int i = 0; i < types.Length && i < amounts.Length; i++)
                resources[(ResourceType)types[i]] = amounts[i];
        OnInventoryChanged?.Invoke();
    }
}