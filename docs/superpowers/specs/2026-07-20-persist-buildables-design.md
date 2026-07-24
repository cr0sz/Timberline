# Persist Placed Buildables (SaveData v2) — Design

Date: 2026-07-20

## Problem

Save/load v1 persists everything except player-placed/moved structures
(fences/gates/bridges from BuildSystem). `PlacedBuildable` is a bare marker with
no prefab identity, so it can't be rebuilt on load.

## Design

- `PlacedBuildable`: add `public int catalogIndex = -1;`.
- `BuildSystem.Place(index)`: stamp `catalogIndex = index` on the added marker.
- `BuildSystem`:
  - `SnapshotBuildables(out int[] idx, out Vector3[] pos, out float[] rotY)` —
    reads every `PlacedBuildable` (Y-only rotation, matching how Place/Move set it).
  - `LoadBuildables(int[] idx, Vector3[] pos, float[] rotY)` — instantiate each
    from `catalog[idx].prefab` at pos + `Quaternion.Euler(0,rotY,0)`, add a
    `PlacedBuildable` carrying the index. Bounds-check idx against catalog; skip
    invalid entries.
- `SaveManager` / `SaveData`:
  - Bump `CurrentVersion` to 2. Add `int[] buildIndices; Vector3[] buildPositions;
    float[] buildRotY;` (JsonUtility serializes `Vector3[]`).
  - Save: gather via `SnapshotBuildables`.
  - Load: accept version 1 OR 2 (reject only <1 or >CurrentVersion). A v1 file
    deserializes with null buildable arrays -> `LoadBuildables` no-ops. So
    existing v1 saves keep loading, just without buildables.

## Round-trip / edge cases

- Rebuilt buildables carry their index, so the next snapshot re-persists them.
- MOVE mode updates transform directly; snapshot reads live pos/rot, so moved
  structures persist at their new spot.
- Rotation is Y-only (Place and Move both set `Euler(0,y,0)`), so a single float
  is lossless.
- Catalog reordered/shrunk between builds -> out-of-range indices skipped.

## Verify

Play -> build 2-3 structures, move one -> save -> stop -> fresh boot -> confirm
same count, positions, and prefab types rebuilt. Confirm a v1 save still loads
(core state intact, no crash).

## Out of scope

Non-catalog runtime structures, per-instance state (health/tier) on buildables.
