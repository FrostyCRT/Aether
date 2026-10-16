# Outil de dev (2026-10-15) : ajoute l'alphabet CYRILLIQUE (russe et autres langues cyrilliques) à Bangers.
# Bangers n'a pas de cyrillique. Plutôt qu'une police de repli (rendu, contour et atlas différents), les lettres sont
# prises dans Fira Sans Extra Condensed Black Italic (Tools/fonts, licence OFL, comme Bangers) et retravaillées pour
# avoir les proportions de Bangers, puis ajoutées DANS Bangers-Regular-Chiffres.ttf : même atlas TMP, même matériau.
#   - hauteur des capitales = celle de Bangers (SCALE_Y), largeur ramenée à celle de Bangers (SCALE_X) ;
#   - inclinaison de Fira (8°) remplacée par celle de Bangers (SLANT_DEG) ;
#   - comme Bangers (dont les minuscules SONT les majuscules), chaque minuscule cyrillique affiche la majuscule ;
#   - signes au-dessus des lettres (Ё, Й, Ї, Ў...) rapetissés et reposés juste au-dessus, comme les accents français
#     (voir patch_bangers_accents.py) ;
#   - crénage (kerning) de Fira entre lettres cyrilliques recopié dans la table GPOS de Bangers.
# Ordre : 1) python Tools/patch_bangers_digits.py   (repart de Bangers-Regular.ttf d'origine)
#         2) python Tools/patch_bangers_accents.py
#         3) python Tools/patch_bangers_cyrillic.py (refuse de tourner deux fois : relancer 1, 2 puis 3)
#         4) Unity : menu Aether > Rebuild Bangers Digits (régénère l'atlas : chiffres, accents ET cyrillique)
# Requiert : pip install fonttools.
import math
import os
import sys
from fontTools.ttLib import TTFont
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.boundsPen import BoundsPen
from fontTools.otlLib.builder import buildPairPosGlyphsSubtable, buildValue

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "Assets", "Game", "Fonts", "Bangers-Regular-Chiffres.ttf")
SOURCE = os.path.join(ROOT, "Tools", "fonts", "FiraSansExtraCondensed-BlackItalic.ttf")

SCALE_Y = 1.06        # capitales Fira (696) -> Bangers (737-740)
SCALE_X = 0.84        # Bangers est plus étroite : chasse ~0,82 de Fira, 0,84 pour garder des fûts aussi gras
SOURCE_SLANT_DEG = 8.0
SLANT_DEG = 10.0      # inclinaison mesurée sur Bangers (I, H, L, E)
MARK_SCALE = {"dieresis": 0.70, "uni0308": 0.70, "breve": 0.60, "acute": 0.55, "grave": 0.55, "macron": 0.65}
MARK_DEFAULT = 0.65
GAP = 22              # unités entre le haut de la lettre et le bas du signe (comme les accents)
PREFIX = "cyr_"
FIRST, LAST = 0x0400, 0x04FF

dst = TTFont(PATH, recalcTimestamp=False)
src = TTFont(SOURCE)
dst_cmap = dst.getBestCmap()
if any(FIRST <= u <= LAST for u in dst_cmap):
    sys.exit("Le cyrillique est déjà dans " + PATH + " : relancer patch_bangers_digits.py puis patch_bangers_accents.py d'abord.")

src_cmap = src.getBestCmap()
src_gs = src.getGlyphSet()
src_glyf = src["glyf"]

# x' = SCALE_X * (x - y*tan(src)) + SCALE_Y*y*tan(dst) ; y' = SCALE_Y * y
shear = SCALE_Y * math.tan(math.radians(SLANT_DEG)) - SCALE_X * math.tan(math.radians(SOURCE_SLANT_DEG))
MAIN = (SCALE_X, 0, shear, SCALE_Y, 0, 0)
tan_dst = math.tan(math.radians(SLANT_DEG))


def record(glyph_name, matrix):
    rec = DecomposingRecordingPen(src_gs)
    src_gs[glyph_name].draw(TransformPen(rec, matrix))
    return rec


def bounds(rec):
    bp = BoundsPen(None)
    rec.replay(bp)
    return bp.bounds


def is_mark(name):
    gb = BoundsPen(src_gs)
    src_gs[name].draw(gb)
    return gb.bounds is not None and gb.bounds[1] >= 600   # entièrement au-dessus des minuscules / des capitales


def mark_scale(name):
    for key, s in MARK_SCALE.items():
        if key in name:
            return s
    return MARK_DEFAULT


def build(name):
    """Glyphe Fira `name` transformé (et signes diacritiques retravaillés) -> glyphe TrueType."""
    g = src_glyf[name]
    pen = TTGlyphPen(None)
    if g.isComposite() and any(is_mark(c.glyphName) for c in g.components):
        base_recs, marks = [], []
        for c in g.components:
            m = (1, 0, 0, 1, c.x, c.y)
            if is_mark(c.glyphName):
                marks.append((c.glyphName, m))
            else:
                base_recs.append(record(c.glyphName, (MAIN[0], 0, MAIN[2], MAIN[3], MAIN[0] * c.x + MAIN[2] * c.y, MAIN[3] * c.y)))
        top = max(bounds(r)[3] for r in base_recs)
        for r in base_recs:
            r.replay(pen)
        for mname, m in marks:
            r = record(mname, (MAIN[0], 0, MAIN[2], MAIN[3], MAIN[0] * m[4] + MAIN[2] * m[5], MAIN[3] * m[5]))
            x0, y0, x1, y1 = bounds(r)
            s = mark_scale(mname)
            cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
            new_bottom = top + GAP
            dy = new_bottom - (cy - (cy - y0) * s)
            # mise à l'échelle autour du centre, descente de dy, et décalage horizontal qui suit l'inclinaison
            final = (s, 0, 0, s, cx - s * cx + dy * tan_dst, cy - s * cy + dy)
            r.replay(TransformPen(pen, final))
    else:
        record(name, MAIN).replay(pen)
    return pen.glyph()


# --- lettres à ajouter : tout le bloc cyrillique de Fira ; une minuscule pointe vers le glyphe de sa majuscule
targets = {}      # unicode -> nom du glyphe Fira à utiliser
for u in range(FIRST, LAST + 1):
    if u not in src_cmap:
        continue
    up = chr(u).upper()
    if len(up) == 1 and ord(up) != u and ord(up) in src_cmap:
        targets[u] = src_cmap[ord(up)]
    else:
        targets[u] = src_cmap[u]

glyph_order = dst.getGlyphOrder()
glyf, hmtx = dst["glyf"], dst["hmtx"]
added = {}        # nom Fira -> nom Bangers
for fname in sorted(set(targets.values())):
    nname = PREFIX + fname
    glyph = build(fname)
    glyf[nname] = glyph
    glyph.recalcBounds(glyf)
    adv = round(src["hmtx"][fname][0] * SCALE_X)
    hmtx[nname] = (adv, getattr(glyph, "xMin", 0))     # glyf[...] = ... a déjà ajouté le nom à l'ordre des glyphes
    added[fname] = nname
glyph_order = glyf.glyphOrder
dst.setGlyphOrder(glyph_order)
if "post" in dst and hasattr(dst["post"], "extraNames"):
    dst["post"].extraNames = []      # format 2 : les noms sont recalculés à l'enregistrement

for table in dst["cmap"].tables:
    if table.isUnicode():
        for u, fname in targets.items():
            if table.format != 4 or u <= 0xFFFF:
                table.cmap[u] = added[fname]

# --- crénage : valeurs de Fira entre glyphes cyrilliques ajoutés (lookups PairPos, y compris en extension)
def pair_lookups(font):
    gpos = font["GPOS"].table
    kern_idx = set()
    for fr in gpos.FeatureList.FeatureRecord:
        if fr.FeatureTag == "kern":
            kern_idx.update(fr.Feature.LookupListIndex)
    for i in sorted(kern_idx):
        lk = gpos.LookupList.Lookup[i]
        for st in lk.SubTable:
            if lk.LookupType == 9:
                if st.ExtensionLookupType != 2:
                    continue
                st = st.ExtSubTable
            elif lk.LookupType != 2:
                continue
            yield st


def kern_value(sub, left, right):
    cov = sub.Coverage.glyphs
    if left not in cov:
        return None
    if sub.Format == 1:
        ps = sub.PairSet[cov.index(left)]
        for rec in ps.PairValueRecord:
            if rec.SecondGlyph == right:
                return getattr(rec.Value1, "XAdvance", 0) if rec.Value1 else 0
        return None
    c1 = sub.ClassDef1.classDefs.get(left, 0)
    c2 = sub.ClassDef2.classDefs.get(right, 0)
    rec = sub.Class1Record[c1].Class2Record[c2]
    return getattr(rec.Value1, "XAdvance", 0) if rec.Value1 else 0


subs = [s for s in pair_lookups(src) if getattr(s, "Format", 0) in (1, 2)]
pairs = {}
names = sorted(added)
for left in names:
    for right in names:
        for sub in subs:            # premier sous-tableau qui couvre la paire (règle OpenType)
            v = kern_value(sub, left, right)
            if v is not None:
                if v:
                    pairs[(added[left], added[right])] = buildValue({"XAdvance": round(v * SCALE_X)})
                break
if pairs:
    # ajoutées comme sous-table supplémentaire de la recherche de crénage EXISTANTE de Bangers : le moteur de polices
    # de Unity (TextCore) ne lit pas une recherche "kern" ajoutée à côté (vérifié : 0 paire cyrillique importée)
    gpos = dst["GPOS"].table
    kern_lookup = None
    for fr in gpos.FeatureList.FeatureRecord:
        if fr.FeatureTag == "kern":
            for i in fr.Feature.LookupListIndex:
                if gpos.LookupList.Lookup[i].LookupType == 2:
                    kern_lookup = gpos.LookupList.Lookup[i]
                    break
        if kern_lookup:
            break
    if kern_lookup is None:
        sys.exit("Pas de recherche de crénage (GPOS kern, type 2) dans Bangers.")
    kern_lookup.SubTable.append(buildPairPosGlyphsSubtable({k: (v, None) for k, v in pairs.items()}, dst.getReverseGlyphMap()))
    kern_lookup.SubTableCount = len(kern_lookup.SubTable)

dst["maxp"].numGlyphs = len(glyph_order)
dst.save(PATH)
print(f"{len(targets)} caractères cyrilliques ajoutés ({len(added)} glyphes), {len(pairs)} paires de crénage -> {PATH}")
