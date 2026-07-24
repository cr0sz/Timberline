# Save / Load — Design

Date: 2026-07-20

## Problem

Nothing persists. Closing the app wipes coins, carried resources, all upgrade
levels, health, and tycoon station tiers. Mobile OS kills backgrounded apps
constantly, so every session starts from zero. HIGH priority.

## Scope

Full progress **minus** hand-placed buildables (fences/gates/bridges positions)
and a new-game UI. Buildables deferred: the `PlacedBuildable` marker carries no
prefab id today, so persisting them needs new plumbing — separate follow-up.

## Architecture

Single `SaveManager` on a new `GameManager` GameObject. Auto-finds Player
systems + Shop + both UpgradeStations in `Awake` (single-player scene, no manual
wiring). `[DefaultExecutionOrder(1000)]` so its load runs after every other
`Start()` and overwrites their defaults cleanly.

- File: `Application.persistentDataPath/save.json`, via `JsonUtility`.
- **Save** triggers: `OnApplicationPause(true)` + `OnApplicationQuit`
  (mobile-critical) + throttled autosave — a dirty flag set from
  `PlayerInventory.OnInventoryChanged`, flushed at most every 3s so gathering
  doesn't thrash disk.
- **Load**: on boot in `Start`. Missing / unparseable / version-mismatch file is
  caught and ignored → fresh start.

## SaveData (v1)

Flat `[Serializable]` class (JsonUtility-friendly, no dictionaries):
`version, coins, capacity, moveSpeed, health, axeTier, pickaxeTier, weaponTier,
capacityLevel, speedLevel, resTypes[], resAmounts[], campfireTier, storageTier`.

Carried resources stored as parallel `int[]` (type as `(int)ResourceType`,
amount) because JsonUtility can't serialize `Dictionary`.

## Double-count hazard + fix

Storage upgrades call `inventory.AddCapacity(+25)` and speed upgrades do
`moveSpeed += step` — cumulative. Naive reload that replayed effects would
double them. Fix: persist the **final derived values** (capacity, moveSpeed,
health) and set them **directly** on load; never replay Add/`+=`. The level
counters (capacityLevel, speedLevel, tool tiers, station tiers) are restored
only so Shop pricing/labels and re-upgrades stay correct.
`UpgradeStation.LoadTier` respawns the campfire/crate structure and sets the
campfire visual tier, but does **not** re-add capacity.

## Edits (small accessors, no logic rewrites)

- `PlayerInventory`: `SnapshotResources()` + `LoadState(coins, capacity, res)`.
- `Shop`: `CapacityLevel` / `SpeedLevel` getters + `LoadLevels(cap, spd)`.
- `PlayerHealth`: `LoadHealth(hp)`.
- `UpgradeStation`: `Tier` getter + `LoadTier(t)` (spawn + apply, skip capacity).
- `ToolInventory` / `PlayerController`: none — tiers & moveSpeed already public.
- New: `SaveManager.cs` + one `GameManager` GameObject.

## Verification

Play -> change state (coins, tiers, tycoon) -> save -> read save.json off disk.
Then stop, re-enter play fresh -> boot load -> confirm restored.
persistentDataPath survives across play sessions, so this is a true reload test.

## Out of scope

Hand-placed/moved buildables persistence, new-game UI button (a `DeleteSave()`
method is left for it), cloud save, multiple slots.
