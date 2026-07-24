# Build/Move Rework — Design

Date: 2026-07-22
Status: Approved for planning

## Problem

Current build/move system (`Assets/Scripts/BuildSystem.cs`) has four confirmed pain points:

1. **Blind placement** — tapping a build button plops the prefab 2.5m in front of the player, ground-snapped, facing the player. No preview, no way to adjust position, no cancel; coins are spent before the player sees where it lands.
2. **No rotate / no grid** — structures can't be rotated or aligned to a grid, so walls and fences never line up into a clean base.
3. **Move is janky** — drag-to-move grabs whatever `PlacedBuildable` the ray hits with no snapping and no visual feedback; easy to grab the wrong thing.
4. **Overlap allowed** — nothing prevents structures clipping into each other, the player, terrain, or resource nodes.

## Goals

- A single, precise placement flow the player controls before committing coins.
- Rotation in 45° steps and an optional snap-to-grid.
- Collision/overlap validation with clear valid/invalid feedback.
- Move reuses the exact same flow — no separate janky path.

## Non-goals (YAGNI)

- No multi-select or bulk move.
- No free-angle (arbitrary) rotation — 45° steps only.
- No building on top of / stacking structures.
- Save/load format stays as-is (index + position + Y rotation).

## Architecture

One shared component, `PlacementController`, runs the ghost loop for both **place-new** and **move-existing**. Move is the same code path with the structure already paid for.

```
BuildSystem  ── StartPlacement(index) ──▶  PlacementController
   (catalog,                                   (ghost loop, input,
    coins,                                       validation,
    save/load)                                   confirm / cancel)
                                                     │
PlacedBuildable ◀── tap-to-move ──────────────────────┘
   (marker + cached footprint)
```

### Components

| Piece | Responsibility | Depends on |
|---|---|---|
| `PlacementController` | Owns placement state. Spawns/updates ghost, reads drag/rotate/grid input, runs `IsValid` each frame, swaps ghost material, handles confirm/cancel. | `BuildSystem` (catalog + coins), `PlacedBuildable`, cam, player |
| `BuildSystem` | Catalog + coin spend only. Build buttons call `StartPlacement(index)`. Save/load (`SnapshotBuildables`/`LoadBuildables`) unchanged. | `PlayerInventory`, `PlacementController` |
| `PlacedBuildable` | Marker with `catalogIndex`. Adds a cached local-bounds/footprint used by validation. | — |
| Placement UI bar | Confirm, Cancel, Rotate (+45°), Grid-toggle buttons. Visible only while in placement mode. | `PlacementController` |

## Data flow

### Place new
1. Build button → `BuildSystem.StartPlacement(index)`.
   - Affordability is **not** checked here; player may cancel. Coins are only spent on confirm.
2. `PlacementController` spawns a translucent ghost of `catalog[index].prefab`. Ghost colliders disabled (or set trigger-only) so they don't block the world or the validation overlap check against themselves.
3. Placement UI bar appears. Each frame:
   - Drag on ground → ghost position via `GroundUnderPointer` (reused from current code).
   - Grid toggle ON → snap position to `gridSize` cells (default 1m). OFF → free.
   - Rotate button → `yaw = (yaw + 45) % 360`, applied before grid snap.
   - `IsValid` → swap all ghost renderers to green (valid) or red (invalid) material.
4. Confirm (enabled only when valid):
   - `inventory.CanAffordCoins(cost)` re-checked. If not affordable → fail feedback + toast, stay in mode.
   - Instantiate real prefab at ghost transform, add/set `PlacedBuildable.catalogIndex`, `SpendCoins`, `AudioManager.Purchase()`, success feedback, toast `-{cost} {name}`.
   - Destroy ghost, exit placement mode.
5. Cancel → destroy ghost, exit. No coins were spent.

### Move existing
1. Tap a `PlacedBuildable` (only when not already in placement mode) → `StartPlacement` with `isMove = true`, source = tapped object.
2. Original is hidden (renderers off) so it doesn't block its own validation. Ghost spawns from the same prefab at the original's transform (position + yaw).
3. Same drag / rotate / grid / validation loop.
4. Confirm → move the original to the ghost transform, re-show it. No coins charged.
5. Cancel → re-show original at its untouched transform, destroy ghost.

## Validation — `IsValid(pos, yaw)`

Returns true only when both hold:

1. **No overlap.** `Physics.OverlapBox` at the ghost's world footprint (center + half-extents from cached bounds, rotated by yaw) against `blockingMask`. `blockingMask` = other buildables + player + resource nodes + water. Any hit → invalid. (Ghost's own colliders are disabled, so it can't self-collide; in move mode the original is hidden.)
2. **Ground under footprint.** Downward raycast at each of the 4 footprint corners; all must hit ground within a tolerance. Prevents structures floating off ledges or half over water.

Grid snap and 45° rotation are applied to `pos`/`yaw` *before* `IsValid` runs, so the check reflects the committed transform.

## Materials

- Two shared transparent materials: `ghostValid` (green) and `ghostInvalid` (red).
- On ghost spawn, cache each renderer's original materials; on every validity change, swap all renderers to the valid/invalid material.
- In move mode the ghost is a fresh instance of the prefab (originals stay cached on that instance), so no need to restore materials on a live object.

## Save / load

Unchanged. `SnapshotBuildables` / `LoadBuildables` still store `{catalogIndex, position, rotY}`. Rotation is already a free Y angle in the save, so 45° multiples serialize with no format change.

## Testing

- **Edit-mode unit tests** on pure logic (no scene):
  - Grid snap math — arbitrary point snaps to nearest cell center for given `gridSize`.
  - 45° rotate wrap — repeated rotate cycles 0→45→…→315→0.
  - `IsValid` footprint math — center + half-extents computed correctly for a given yaw (validate the box corners; overlap itself can be checked with mock colliders in a play-mode test).
- **Play-mode / MCP** verification of ghost visuals, material swap, and the confirm/cancel/move flow via Unity screenshot.

## Open risks

- Legacy Input is on (`activeInputHandler = Both`); placement input must keep using `Input.GetMouseButton(0)` / touch so mobile maps correctly — same as current move code.
- `blockingMask` needs the right layers assigned in the editor; if buildables/nodes aren't on distinct layers, add them as part of implementation.
