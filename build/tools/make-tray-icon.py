"""Draws the macOS menu-bar icon: a one-colour (black + alpha) ring with a document, used as a template image so macOS
paints it white on dark menu bars and black on light ones. Writes src/Filee.App/Assets/Icons/tray-template.svg/.png.
   python3 build/tools/make-tray-icon.py   (needs Pillow-free headless Chromium via node + playwright for the PNG)
"""
import math

CX = CY = 512
RO, RI, RND, GAP = 330, 175, 10, 34
CENTERS = [-90, -30, 30, 90, 150, 210]
PULL = 1

def pt(r, deg):
    a = math.radians(deg)
    return CX + r * math.cos(a), CY + r * math.sin(a)

def sector(c):
    ro2, ri2, d = RO - RND, RI + RND, RND + GAP / 2
    o0, o1 = c - 30 + math.degrees(d / ro2), c + 30 - math.degrees(d / ro2)
    i0, i1 = c - 30 + math.degrees(d / ri2), c + 30 - math.degrees(d / ri2)
    p = lambda r, a: "%.1f %.1f" % pt(r, a)
    return f"M{p(ro2,o0)} A{ro2} {ro2} 0 0 1 {p(ro2,o1)} L{p(ri2,i1)} A{ri2} {ri2} 0 0 0 {p(ri2,i0)} Z"

parts = []
for i, c in enumerate(CENTERS):
    a = math.radians(c)
    t = f' transform="translate({20*math.cos(a):.1f},{20*math.sin(a):.1f})"' if i == PULL else ""
    parts.append(f'<path d="{sector(c)}" stroke-width="{RND*2}"{t}/>')
file = '<g transform="translate(512,514) scale(1.15)"><path d="M-58,-74 h78 l40,40 v108 h-118 z" stroke-width="10"/></g>'
svg = ('<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="150 150 724 724" '
       'fill="#000" stroke="#000" stroke-linejoin="round">' + "".join(parts) + file + '</svg>')
open("src/Filee.App/Assets/Icons/tray-template.svg", "w").write(svg)
