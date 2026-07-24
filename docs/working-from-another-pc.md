# Working on Timberline from another PC

Two repos exist and they are not interchangeable:

| Repo | Contents | Use it for |
|---|---|---|
| `cr0sz/Survival` (private) | the **whole Unity project**, art and audio included, ~954 MB in Git LFS | actually developing the game |
| `cr0sz/Timberline` | source, editor tooling, tests, docs — **no art** | showing people; it will not open as a playable project |

Only the first one is a working copy.

---

## Read this before you clone

**A fresh clone costs ~954 MB of Git LFS bandwidth, and GitHub's free tier gives you
1 GB per month — for the whole account.**

So one clone burns roughly 95% of the monthly allowance. A second clone in the same
month fails, and while you are over quota LFS is disabled repo-wide: `git push` of any
LFS file is rejected and fresh checkouts come down as text pointer files instead of
real assets, which looks exactly like a corrupted project.

Check usage at **GitHub → Settings → Billing → Git LFS Data**.

### Prefer copying the folder

For a second machine you own, copy `C:\Users\bekir\Documents\projeler\Survival` over a
USB drive, external disk or LAN share. Costs no bandwidth and is faster than cloning.

Copy these:

```
Assets/  Packages/  ProjectSettings/  docs/  dev/  .git/  .gitattributes  .gitignore
```

**Skip `Library/`, `Temp/`, `Logs/`, `obj/`, `Builds/`, `.vs/`.** `Library/` is a
multi-GB derived cache — Unity rebuilds it on first open. Bringing `.git/` along means
the copy is still a real repo with full history and its LFS objects already local, so
you can commit and push from it immediately.

First open after a copy takes **10–30 minutes** while Unity reimports every asset. That
is normal and happens once.

---

## If you do clone

Install **Git LFS before cloning**. Cloning first and installing after leaves every
asset as a pointer file, and the project opens with missing models, pink materials and
broken prefabs.

```bash
git lfs install
git clone https://github.com/cr0sz/Survival.git
cd Survival
git lfs pull
```

If you already cloned without LFS:

```bash
git lfs install
git lfs pull
```

---

## What the machine needs

| | |
|---|---|
| **Unity** | 6.3 LTS, exactly **6000.3.13f1** (`ProjectSettings/ProjectVersion.txt`) |
| **Module** | Android Build Support, **including** the SDK and NDK sub-options |
| **Git LFS** | required, see above |
| **Python + Pillow** | only to regenerate branding (`dev/make_logo.py`) |

A different Unity patch version will silently reimport and reserialise assets, producing
enormous unrelated diffs. Install the exact version from Unity Hub.

---

## What does NOT travel

- **Your save game.** It lives in `Application.persistentDataPath`, outside the project.
  The other PC starts a fresh run. That is usually what you want.
- **`Builds/`.** Gitignored. Rebuild with **Tools/Survival/Build Android APK**.
- **`Library/`.** Regenerated on first open.

## First thing to do after opening

Confirm the project is intact rather than assuming it:

1. **Tools/Survival/Run EditMode Tests** → expect `[TESTS] passed=48 failed=0`.
2. Open `Assets/Scenes/Map.unity` and look at it. Pink materials or missing meshes mean
   LFS did not pull — go back and run `git lfs pull`.

## Turkish Windows warning

Do not run **Assets → Generate Shader Includes** on a Turkish locale. The dotted-i
casing rule corrupts generated `.cs.hlsl` files (`i` → `İ`). See
[`gotchas.md`](gotchas.md) and the `turkish-locale-shader-includes` note.
