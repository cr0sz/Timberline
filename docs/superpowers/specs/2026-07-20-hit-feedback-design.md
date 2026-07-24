# Hit Feedback + Combat Tuning — Design

Date: 2026-07-20

## Problem

Damage-when-attacked is fully coded and wired (Predators call `PlayerHealth.TakeDamage`),
but getting hit is invisible and rarely happens:

- Player auto-attack reach (2.6m) out-ranges creature bite (1.8m); weapon interval 0.8s.
  Fragile Wolf (20hp) dies/staggers before landing many bites.
- Player invuln window (2.0s) vs bite interval (1.2s) caps incoming to ~1 hit / 2s.
- No sensory feedback on hit: HP silently ticks, then regens after 5s.

## Goals

Make getting attacked *register* — both mechanically (hits land, matter) and
sensorily (the player sees/feels the hit).

## Tuning

| Knob | Old | New | Reason |
|------|-----|-----|--------|
| Player invuln | 2.0s | 0.8s | hits land more often, still blocks multi-hit spam |
| First bite timing | waits full interval | fires on reaching player | closing the gap = immediate threat |
| Wolf damage | 6 | 8 | 6/100 unnoticeable |
| Bear damage | 14 | 14 | already threatening |
| Regen delay | 5s | 7s | can't walk off every hit instantly |

## Juice (on getting hit)

1. **Red screen flash** — fullscreen UI Image (red), alpha 0 -> 0.4 -> 0 over ~0.3s.
   Most readable on mobile. New Image under Canvas.
2. **Camera shake** — short positional kick (~0.15s, decaying) on Main Camera.
   New `CameraShake` component.
3. **Knockback** — shove player away from attacker (~0.6m, decays over ~0.2s).
   Injected through `PlayerController.AddKnockback`.

## Architecture

- `PlayerHealth`: add `event Action<int, Vector3> OnDamaged` (damage, hitDirection);
  add `TakeDamage(int dmg, Vector3 sourcePos)` overload. Old `TakeDamage(int)` forwards
  with `sourcePos = transform.position` (zero direction -> no knockback for non-directional callers).
- `Creature.cs`: bite calls `TakeDamage(damage, transform.position)`; prime attackTimer so
  first bite fires on arrival.
- `PlayerController`: `AddKnockback(Vector3 velocity)` + per-frame decay via `controller.Move`.
- `CameraShake` (Main Camera): `Shake(intensity, duration)`, restores local offset.
- `PlayerHitFeedback` (Player): subscribes to `OnDamaged`, drives flash + shake + knockback.

## Scope

2 new scripts (`CameraShake`, `PlayerHitFeedback`), edits to 3 existing
(`PlayerHealth`, `Creature`, `PlayerController`), 1 new UI Image, prefab value tweaks.
All scene wiring via Unity MCP.

## Out of scope

Player block/dodge, enemy attack animation clips, ranged enemies, hit SFX (no audio asset).
