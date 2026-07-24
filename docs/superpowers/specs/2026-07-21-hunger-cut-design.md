# Cut the Hunger System — Design

Date: 2026-07-21

## Problem

Hunger is a pure tax with no interesting decision behind it, and the economy makes
cooking strictly dominated by selling.

The numbers, measured from the live components:

- `maxHunger` 100, `drainPerSecond` 0.4 → full to empty in **250s (~4.2 min)**.
- `autoEatThreshold` 40, `hungerPerFood` 30 → sustained play needs **1 Food per 75s**,
  roughly 48 Food/hour.
- Food comes from exactly one place: `Campfire` converts Meat → Food 1:1 at 0.5/s
  while the player stands inside its radius. Nothing in the world drops Food.
- Meat sells for 10c. Food sells for **1c** — `Shop.PriceOf` has no `Food` case, so it
  falls through to `default: return 1`.

So every 75 seconds of not-starving costs the player 9c of destroyed value, and the
only alternatives are standing still at a campfire or dying. Death costs 30% of carried
resources plus 10% of coins, which at low hauls is often *cheaper* than cooking.

Berry bushes were proposed and rejected (2026-07-21). Rebalancing was considered and
rejected in favour of removal: the game is a gather → carry → sell → upgrade tycoon,
and hunger interrupts that loop without adding a decision to it.

## Decision

Remove the hunger system entirely. The campfire keeps healing and loses cooking. Meat
becomes a pure sell item.

`ResourceType.Food` **stays in the enum**, unused, with a comment explaining why.

## Save compatibility

`ResourceType` is `Wood, Stone, Food, Meat, Hide` and `SaveData.resTypes` stores raw
enum ints. Deleting `Food` would shift `Meat` 3→2 and `Hide` 4→3, silently rewriting
every existing save's inventory into the wrong resources. That is the single
highest-risk part of this change and the reason the enum value stays put.

`SaveData.hunger` is dropped. JsonUtility ignores unknown JSON fields on read and simply
omits absent ones on write, so:

- Old saves (with `hunger`) load fine — the field is ignored.
- New saves omit it.

**`CurrentVersion` stays 5.** No bump, no migration path, no loader changes beyond
deleting the two hunger lines.

## Changes by file

### Deleted outright

| Path | Note |
|------|------|
| `Assets/Scripts/PlayerHunger.cs` | whole file, plus its `.meta` |

### Edited

| File | Change |
|------|--------|
| `PlayerHealth.cs` | Delete `Drain(int)` — `PlayerHunger` was its only caller. Keep `OnRespawn` (legitimate hook, zero cost) but fix its comment, which currently reads "e.g. PlayerHunger refills on respawn". |
| `Campfire.cs` | Delete `cookPerSecond`, `cookBuffer`, the Meat→Food `while` loop and the `AudioManager.Cook()` call. Healing, `SetTier`, radius, light scaling and the gizmo all stay untouched. |
| `AudioManager.cs` | Delete the `cook` clip field, `cookVolume`, and `Cook()`. No callers remain. |
| `HUD.cs` | Delete `hunger`, `hungerBar`, `hungerText`, `foodText`, `RefreshHunger()`, its call in `OnEnable`, and the subscribe/unsubscribe pair. |
| `SaveManager.cs` | Delete the `hunger` field from `SaveData`, the `hungerSys` field, its `FindObjectOfType` in `Awake`, the save line, and the load line. |
| `ResourceType.cs` | Add a comment on `Food` marking it vestigial and explaining that its index is load-bearing for saves. |
| `Editor/BuildCatalogSetup.cs` | Remove the `HungerGroup` bar row and the `FoodPill` entry from the pill grid. Adjust panel height for one fewer bar row. |

### Scene (`Map.unity`)

- Remove the `PlayerHunger` component from `Player`.
- Rebuild the HUD via `Tools/Survival/Build Catalog + UI` so the layout change is
  reproducible rather than hand-edited.

## HUD layout consequence

- Bar rows 3 → 2: **HP, CARRY**. (FOOD row gone.)
- Pills 6 → 5, as 3 + 2: **Wood / Stone / Coins**, then **Meat / Hide**.
- HUD panel is currently 380x270. It loses one bar row, so it shrinks vertically and
  the objective tracker beneath it moves up correspondingly.

## Deliberately out of scope

- The orange `FoodFill` colour constant and `Assets/1 Icons/Icons_18.png` (cooked cut)
  become unused. Both stay in the project; neither costs anything and the icon may be
  wanted later.
- The campfire tycoon pad costs 50 coins and now only scales heal rate. That is a
  thinner upgrade than it was. Noted, not addressed here.
- Meat/Hide sell prices are unchanged.

## Verification

Play-mode checks via MCP, matching how the rest of this project has been verified:

1. **No compile errors** and no `MissingComponentException` / null-ref spam in the
   console after the scene loads.
2. **Health is stable at rest.** Park the player away from creatures for ~5 minutes of
   game time; HP must not tick down. (Previously starvation drained 2 HP/s at zero
   hunger; a leftover drain path would show up here.)
3. **HUD reads correctly.** Two bars, five pills, no null text refs, panel not
   overlapping the objective tracker.
4. **Campfire still heals.** Damage the player, stand in radius, confirm HP climbs and
   that Meat is *not* consumed.
5. **Save round-trip.** Load a pre-change `save.json` (which contains `hunger`) and
   confirm coins, capacity, tiers and — critically — `resTypes`/`resAmounts` restore to
   the same resources, with Meat and Hide landing in the right slots.
6. **New save omits `hunger`** and reloads cleanly.
