# Gotchas

Landmines this project actually stepped on. All of them are **fixed** — these are
warnings, not open work. Read the relevant one before touching animation, UI layout,
editor scripts, or the build catalog.

Open work lives in [`Assets/TODO.txt`](../Assets/TODO.txt). History lives in git.

---

## A compile error in `Assets/Editor` freezes the whole project

And it looks exactly like a wedged asset pipeline. Unity keeps the last good
`Assembly-CSharp-Editor.dll` and never completes a domain reload, so **runtime** script
edits silently stop taking effect too — reflection keeps reporting the old type while
the `.cs` on disk is obviously correct. `AssetDatabase.Refresh`,
`RequestScriptCompilation`, Assets → Refresh and a play-mode cycle all report success
and change nothing.

**The MCP console shows ZERO errors while this is happening.** Confirmed again on
2026-07-24: `Unity_ReadConsole Types:["Error"]` returned an empty list while the log
held `Assets\Editor\BuildTool.cs(32,49): error CS0103`.

Two diagnostics that do work:

```bash
# 1. Is the DLL older than the source? Then an editor script failed to compile.
ls -l Library/ScriptAssemblies/Survival.Editor.dll

# 2. The real compiler output lives here, not in the console MCP can read.
grep "error CS" "$LOCALAPPDATA/Unity/Editor/Editor.log" | tail -5
```

Cost most of a session, twice. Check the DLL mtime first.

## A scrim does not hide what is under it

The project renders in **linear colour space**. A 0.9-alpha black overlay leaves about
30% of the original luminance once it is gamma-encoded, so the HP bar stayed plainly
legible under what the inspector called a 90%-opaque panel.

Measured, not guessed: render twice and `GetPixel` the same coordinate with the panel
on and off. `RGBA(0.820, 0.294, 0.271)` became `RGBA(0.286, 0.102, 0.094)` — dimmed,
not hidden.

If a screen must *hide* chrome (the title screen), `SetActive(false)` the chrome. Do
not chase it by raising scrim alpha.

## Panels live under `SafeAreaRoot`, not the root Canvas

The root Canvas has exactly three children: `TouchZone`, `HitFlash`, `SafeAreaRoot`.
Everything else is a child of `SafeAreaRoot`. Draw order is sibling order, so
`SetAsLastSibling()` for an overlay must target the `SafeAreaRoot` child — sorting the
root Canvas's children does nothing useful.

## Canvas design height is not 1920

The `CanvasScaler` reference is 1080x1920 matching on **width**, so design-space height
is `deviceHeight / (deviceWidth / 1080)` — **2341** on a 1179x2556 phone, not 1920.

Anything anchored to the top edge and measured downward pins to the top and dumps every
extra unit of a taller screen into the bottom of the frame. Anchor full-screen layouts
to the **centre** so the slack splits evenly.

## Animator clip references fail silently

A missing motion GUID makes the state play nothing — the character collapses to bind
pose. A misnamed float parameter is a no-op — the animal freezes in idle. Both shipped
unnoticed for a session.

If something "has no animation", check the motion and parameter **names** first, before
touching gameplay code.

## A one-shot clip driven by a held bool freezes on its last frame

The state has no way out while the bool stays true. One-shots need a trigger plus an
exit-time transition. Bools are only correct for looping states (Gathering).

## Auto-layout silently overwrites hand-placed rects

HUD widgets were driven by `VerticalLayoutGroup` + `ContentSizeFitter`, which overwrite
any rect you set by hand. The HUD is now hand-placed and those components are stripped
on every `BuildCatalogSetup` run. Don't re-add them unless you switch the whole panel
back to auto-layout.

Related: don't add a `ContentSizeFitter` to a panel whose layout group is frozen — it
collapses the panel.

## A filled UI bar needs a sprite that is present AND square

Two ways to get this wrong, both silent, both shipped:

- **Rounded sprite** (the built-in `UISprite`): `Image.Type.Filled` disables 9-slicing,
  so the corner art stretches down the bar and the ends balloon into a capsule.
- **Null sprite**: `Image` skips the filled path entirely and falls back to
  `GenerateSimpleSprite`, so `fillAmount` is **ignored** and the bar always renders
  full. The objective bar shipped like this and nobody noticed.

Use `Assets/UI/WhiteRect.png` via the `Flat()` / `StyleFill()` helpers. Never null.

## Catalog indices are baked into saves

`SaveData.buildIndices` stores raw catalog positions, so **cutting a build catalog entry
renumbers everything after it** and an old save rebuilds the wrong prefabs. This bit
twice: the Torch cut (2026-07-23) shipped without a migration, and the Crate cut
(2026-07-24) needed save version 7.

Adding a cut means: bump `SaveManager.CurrentVersion`, add a block to
`MigrateBuildIndices`, and add a case to `SaveMigrationTests`.

Dropped entries are **dropped**, never remapped — silently turning a player's crates
into campfires would hand out free predator-repel zones that the 3-campfire cap exists
to prevent.

## Rebuilding the catalog churns the diff

Running **Tools/Survival/Build Catalog + UI** rebuilds the primitive-built prefabs from
scratch, so five of them (Barricade, Deck, StoneWall, Watchtower, WoodWall) come back
with fresh internal fileIDs — roughly 670 lines of diff each, with no content change.
Asset GUIDs are stable so `BuildSystem`'s catalog references survive (verified). Expect
the noise; don't go hunting for what "changed".

## `Invoke` does not tick at `timeScale 0`

`ResetButton`'s two-tap wipe guard used `Invoke` to disarm after 3s. Every screen it
lives on (pause sheet, title screen) runs at `timeScale 0`, where `Invoke`'s clock never
advances — so the button stayed armed indefinitely and the next stray tap would wipe the
save. Use a coroutine with `WaitForSecondsRealtime`.

Same rule for anything that must animate while paused: `PanelPop` and `UIFeedback`
already run on unscaled time.

## A behaviour can live in the prefab, not the script

Retracted claim, kept as a lesson: "creatures walk through every wall" was **wrong**.
It came from reading `Creature.Move` (`transform.position += dir * speed`, no sweep) and
concluding colliders were ignored. The prefabs carry a `CharacterController`, and Unity
depenetrates one out of overlapping geometry even when you move it by writing
`transform.position` — so walls do stop animals.

The specific mistake: `GetComponentsInChildren<Collider>()` reported one collider, which
was read as a mesh collider. **`CharacterController` subclasses `Collider`.**

Never conclude a behaviour from a single `.cs` file when prefab composition decides it.
