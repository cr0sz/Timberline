"""Generates the app icon set and the itch.io capsule from one vector-ish spec.

Re-runnable: `python dev/make_logo.py`. Writes to Branding/.

The mark is a low-poly conifer, split down the middle into a lit and a shadowed
facet so it reads as faceted geometry rather than a flat pictogram — that is the
one visual idea the whole game shares. Amber on near-black is the UI palette
(Accent #E8A34A on Panel #1F1C1A), not the world palette, because a green tree on
a green field disappears at 48px in a launcher grid.
"""
from PIL import Image, ImageDraw, ImageFont
import os

BG      = (31, 28, 26)      # #1F1C1A  panel dark
MOUNT   = (58, 49, 41)      # #3A3129  backdrop ridge, barely there
LIT     = (232, 163, 74)    # #E8A34A  Accent — the lit facet
SHADE   = (201, 130, 47)    # #C9822F  the shadowed facet
TRUNK   = (107, 74, 42)     # #6B4A2A

NAME = "TIMBERLINE"

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# Launcher icons must live under Assets/ so BuildTool can AssetDatabase.Load them.
ICONS = os.path.join(ROOT, "Assets", "UI", "Branding")
# Store art is not a game asset; keeping it out of Assets/ keeps it out of the build.
STORE = os.path.join(ROOT, "Branding")


def _font(px):
    """Bold system font, falling back to PIL's bitmap default if none is present."""
    for path in (r"C:\Windows\Fonts\arialbd.ttf",
                 r"C:\Windows\Fonts\seguisb.ttf",
                 "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"):
        if os.path.exists(path):
            return ImageFont.truetype(path, px)
    return ImageFont.load_default()


def draw_mark(size, bg=True, pad=0.14):
    """Draw the logo at `size` px square. Supersampled 4x, then downscaled."""
    S = size * 4
    img = Image.new("RGBA", (S, S), BG + (255,) if bg else (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def P(x, y):
        """Unit square (0..1) -> pixels, inset by `pad` on all sides."""
        lo, span = pad * S, (1 - 2 * pad) * S
        return (lo + x * span, lo + y * span)

    # Backdrop ridge. Sits low and dark so it adds depth without competing.
    if bg:
        d.polygon([P(-0.1, 1.05), P(0.30, 0.55), P(0.70, 1.05)], fill=MOUNT)
        d.polygon([P(0.45, 1.05), P(0.82, 0.62), P(1.15, 1.05)], fill=MOUNT)

    # Trunk.
    d.polygon([P(0.44, 0.98), P(0.56, 0.98), P(0.55, 0.74), P(0.45, 0.74)], fill=TRUNK)

    # Three stacked tiers, each split into a lit left facet and a shaded right one.
    # Widths taper upward; the apex tier is narrow enough to read as a point.
    tiers = [
        (0.86, 0.56, 0.42),   # (base_y, apex_y, half_width) bottom tier
        (0.66, 0.36, 0.34),
        (0.46, 0.14, 0.25),   # apex
    ]
    for base_y, apex_y, hw in tiers:
        d.polygon([P(0.5 - hw, base_y), P(0.5, apex_y), P(0.5, base_y)], fill=LIT)
        d.polygon([P(0.5, base_y), P(0.5, apex_y), P(0.5 + hw, base_y)], fill=SHADE)

    return img.resize((size, size), Image.LANCZOS)


def wordmark(draw, xy, px, tracking=0.14, fill=LIT):
    """Letter-spaced wordmark. PIL has no tracking, so letters are placed one at a
    time; wide spacing is what keeps a single word from reading as body text."""
    font = _font(px)
    x, y = xy
    gap = px * tracking
    for ch in NAME:
        draw.text((x, y), ch, font=font, fill=fill)
        x += draw.textlength(ch, font=font) + gap
    return x - gap - xy[0]          # total drawn width


def wordmark_width(px, tracking=0.14):
    probe = ImageDraw.Draw(Image.new("RGB", (1, 1)))
    font = _font(px)
    return sum(probe.textlength(c, font=font) for c in NAME) + px * tracking * (len(NAME) - 1)


def main():
    os.makedirs(ICONS, exist_ok=True)
    os.makedirs(STORE, exist_ok=True)
    written = []

    def note(p):
        written.append(os.path.relpath(p, ROOT))

    # Launcher icons — inside Assets/ so the build can pick them up.
    for s in (48, 72, 96, 144, 192, 512, 1024):
        p = os.path.join(ICONS, f"icon-{s}.png")
        draw_mark(s).convert("RGB").save(p)
        note(p)

    # Transparent mark, for overlaying on screenshots.
    p = os.path.join(ICONS, "mark-1024-transparent.png")
    draw_mark(1024, bg=False).save(p)
    note(p)

    # itch.io cover (630x500) and the Play feature graphic (1024x500) are the same
    # lock-up at two sizes: mark left, wordmark and tagline right, the pair centred as
    # one group. Type size is SOLVED for the space available rather than hardcoded —
    # a fixed size ran TIMBERLINE straight off the right edge of the 1024 canvas.
    def lockup(w, h, mark_px, tagline, out_name):
        img = Image.new("RGB", (w, h), BG)
        d = ImageDraw.Draw(img)
        margin = int(w * 0.045)
        gap = int(w * 0.03)
        text_w = w - margin * 2 - mark_px - gap

        px = mark_px                                  # start big, shrink until it fits
        while px > 8 and wordmark_width(px) > text_w:
            px -= 2
        tag_px = max(12, int(px * 0.36))

        word_w = wordmark_width(px)
        # textlength returns a float; PIL.paste needs ints for the box.
        x0 = int((w - (mark_px + gap + word_w)) // 2)

        mk = draw_mark(mark_px, bg=False)
        img.paste(mk, (x0, (h - mark_px) // 2), mk)

        tx = x0 + mark_px + gap
        block_h = px + int(px * 0.55) + tag_px
        ty = (h - block_h) // 2
        wordmark(d, (tx, ty), px)
        d.text((tx + 2, ty + px + int(px * 0.55)), tagline, font=_font(tag_px), fill=(150, 138, 124))

        p = os.path.join(STORE, out_name)
        img.save(p)
        note(p)

    # · is a middle dot, written escaped so the file stays pure ASCII and cannot
    # be mangled by a tool that guesses the encoding as cp1252.
    dot = " · "
    lockup(630, 500, 240, dot.join(("chop", "sell", "upgrade")), "itch-cover-630x500.png")
    lockup(1024, 500, 300, dot.join(("chop", "sell", "upgrade", "survive")), "play-feature-1024x500.png")

    # Wide wordmark on transparent, for the README header.
    w = int(wordmark_width(96)) + 40
    lock = Image.new("RGBA", (w, 150), (0, 0, 0, 0))
    wordmark(ImageDraw.Draw(lock), (20, 20), 96)
    p = os.path.join(STORE, "wordmark-transparent.png")
    lock.save(p)
    note(p)

    print("wrote", len(written))
    for x in written:
        print("  ", x)


if __name__ == "__main__":
    main()
