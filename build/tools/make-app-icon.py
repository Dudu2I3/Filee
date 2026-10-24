"""Draws the app icon (macOS squircle, titanium ring, accent slice) to src/Filee.App/Assets/Icons/filee.svg.
Render it to filee.png (1024x1024, transparent) and filee.ico with any SVG rasteriser, e.g. headless Chromium.
   python3 build/tools/make-app-icon.py
"""
import math

CX = CY = 512
RO, RI = 300, 150
ROUND = 16
GAP = 20
CENTERS = [-90, -30, 30, 90, 150, 210]
PULL = 1  # index of the slice pulled out (the "green" one)

def body_path(n=5.0, a=412, pts=240):
    out = []
    for i in range(pts):
        t = 2 * math.pi * i / pts
        c, s = math.cos(t), math.sin(t)
        x = CX + a * math.copysign(abs(c) ** (2 / n), c)
        y = CY + a * math.copysign(abs(s) ** (2 / n), s)
        out.append(f"{x:.1f},{y:.1f}")
    return "M" + " L".join(out) + " Z"

def pt(r, deg):
    a = math.radians(deg)
    return CX + r * math.cos(a), CY + r * math.sin(a)

def sector(center, ro=RO, ri=RI, rnd=ROUND, gap=GAP):
    ro2, ri2 = ro - rnd, ri + rnd
    d = rnd + gap / 2
    a0, a1 = center - 30, center + 30
    o0, o1 = a0 + math.degrees(d / ro2), a1 - math.degrees(d / ro2)
    i0, i1 = a0 + math.degrees(d / ri2), a1 - math.degrees(d / ri2)
    p = lambda r, a: "%.1f %.1f" % pt(r, a)
    return (f"M{p(ro2,o0)} A{ro2} {ro2} 0 0 1 {p(ro2,o1)} L{p(ri2,i1)} "
            f"A{ri2} {ri2} 0 0 0 {p(ri2,i0)} Z")

def pull(i, amt=16):
    if i != PULL: return ""
    a = math.radians(CENTERS[i])
    return f' transform="translate({amt*math.cos(a):.1f},{amt*math.sin(a):.1f})"'

FILE = "M-58,-74 h78 l40,40 v108 h-118 z"
FOLD = "M20,-74 v40 h40 z"

def doc(defs, content):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024">
<defs>
<filter id="shadow" x="-20%" y="-20%" width="140%" height="150%"><feDropShadow dx="0" dy="14" stdDeviation="16" flood-color="#000" flood-opacity=".35"/></filter>
<filter id="blur30"><feGaussianBlur stdDeviation="30"/></filter>
<filter id="blur12"><feGaussianBlur stdDeviation="12"/></filter>
<filter id="glow"><feGaussianBlur stdDeviation="10" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
<clipPath id="body"><path d="{body_path()}"/></clipPath>
<linearGradient id="topGloss" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".28"/><stop offset=".5" stop-color="#fff" stop-opacity="0"/></linearGradient>
<linearGradient id="rim" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".55"/><stop offset=".4" stop-color="#fff" stop-opacity=".08"/><stop offset="1" stop-color="#fff" stop-opacity=".22"/></linearGradient>
{defs}
</defs>
<path d="{body_path()}" fill="#000" filter="url(#shadow)"/>
{content}
<g clip-path="url(#body)"><rect x="100" y="100" width="824" height="824" fill="url(#topGloss)"/></g>
<path d="{body_path(a=411)}" fill="none" stroke="url(#rim)" stroke-width="2.5"/>
</svg>'''

def hub_file(fill, fold, extra=""):
    return f'<g transform="translate({CX},{CY+2})"><path d="{FILE}" fill="{fill}" stroke="{fill}" stroke-width="10" stroke-linejoin="round"{extra}/><path d="{FOLD}" fill="{fold}" stroke="{fold}" stroke-width="6" stroke-linejoin="round"/></g>'

def sectors(fill_for, shadow=None, op=1.0):
    out = []
    for i, ctr in enumerate(CENTERS):
        s = ""
        if shadow:
            s += f'<path d="{sector(ctr)}" fill="{shadow}" stroke="{shadow}" stroke-width="{ROUND*2}" stroke-linejoin="round" opacity=".5" transform="translate(0,10)" filter="url(#blur12)"/>'
        f = fill_for(i)
        s += f'<path d="{sector(ctr)}" fill="url(#{f})" stroke="url(#{f})" stroke-width="{ROUND*2}" stroke-linejoin="round" opacity="{op}"/>'
        out.append(f'<g{pull(i)}>{s}</g>')
    return "".join(out)

# G: Titanium - near-black, metallic grey ring, one accent slice
def variant_g():
    defs = '''
<linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#2B2C31"/><stop offset=".5" stop-color="#16171B"/><stop offset="1" stop-color="#0B0B0D"/></linearGradient>
<linearGradient id="ti" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#C9CAD1"/><stop offset=".5" stop-color="#8C8E98"/><stop offset="1" stop-color="#55575F"/></linearGradient>
<linearGradient id="ti2" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#B2B3BB"/><stop offset=".5" stop-color="#777983"/><stop offset="1" stop-color="#474951"/></linearGradient>
<linearGradient id="acc" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#9B93F2"/><stop offset="1" stop-color="#5249B8"/></linearGradient>
<linearGradient id="hubg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#17181C"/><stop offset="1" stop-color="#08080A"/></linearGradient>
'''
    c = [f'<path d="{body_path()}" fill="url(#bg)"/>']
    c.append(sectors(lambda i: "acc" if i == PULL else ("ti" if i % 2 == 0 else "ti2"), shadow="#000"))
    c.append(f'<circle cx="{CX}" cy="{CY}" r="112" fill="url(#hubg)"/><circle cx="{CX}" cy="{CY}" r="112" fill="none" stroke="#fff" stroke-opacity=".22" stroke-width="3"/>')
    c.append(hub_file("#F1F1F5", "#9B93F2"))
    return doc(defs, "\n".join(c))

open("src/Filee.App/Assets/Icons/filee.svg", "w").write(variant_g())
