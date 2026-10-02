# Outil de dev (2026-09-26) : rapetisse les ACCENTS de Bangers (é è ê ë à â ä î ï ô ö ù û ü ÿ É È À...).
# Dans Bangers l'accent mesure ~340 unités (47 % de la hauteur des lettres) et dépasse jusqu'à y = 1118 pour une police dont
# l'ascendante est 883 : il traverse les éléments placés au-dessus du texte.
# Ici on réduit chaque accent (échelle par type de marque) et on le repose juste au-dessus de sa lettre, centré comme avant.
# Ordre : 1) python Tools/patch_bangers_digits.py  (produit Bangers-Regular-Chiffres.ttf depuis l'original)
#         2) python Tools/patch_bangers_accents.py (retravaille ce fichier en place ; relancer 1 puis 2 pour repartir de zéro)
#         3) Unity : menu Aether > Rebuild Bangers Digits (régénère chiffres ET lettres accentuées de l'atlas TMP)
# Requiert : pip install fonttools. Paramètres à retoucher : SCALE (taille) et GAP (écart accent / lettre).
import os
from fontTools.ttLib import TTFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "Assets", "Game", "Fonts", "Bangers-Regular-Chiffres.ttf")

# échelle appliquée à chaque marque (les plus hautes sont les plus réduites)
SCALE = {
    "acutecomb": 0.55, "gravecomb": 0.55,
    "uni0302": 0.60, "uni030C": 0.60,          # circonflexe, caron
    "tildecomb": 0.65,
    "uni0308": 0.70, "uni030A": 0.70,          # tréma, rond
}
GAP = 22   # unités entre le haut de la lettre et le bas de l'accent

f = TTFont(PATH)
glyf = f["glyf"]
hmtx = f["hmtx"]

def mark_key(name):
    return name[:-5] if name.endswith(".case") else name

changed = []
for gname in f.getGlyphOrder():
    g = glyf[gname]
    if not g.isComposite():
        continue
    base = None
    marks = []
    for c in g.components:
        if mark_key(c.glyphName) in SCALE:
            marks.append(c)
        elif base is None:
            base = c
    if not marks or base is None:
        continue

    base_top = glyf[base.glyphName].yMax + base.y
    for c in marks:
        s = SCALE[mark_key(c.glyphName)]
        m = glyf[c.glyphName]
        cx = c.x + (m.xMin + m.xMax) / 2.0            # centre horizontal d'origine, conservé
        new_bottom = base_top + GAP
        c.transform = [[s, 0], [0, s]]
        c.x = int(round(cx - s * (m.xMin + m.xMax) / 2.0))
        c.y = int(round(new_bottom - s * m.yMin))
    g.recalcBounds(glyf)
    adv, _ = hmtx[gname]
    hmtx[gname] = (adv, g.xMin)
    changed.append(gname)

f.save(PATH)
print("accents rapetisses sur", len(changed), "glyphes :", ", ".join(changed[:12]), "...")
