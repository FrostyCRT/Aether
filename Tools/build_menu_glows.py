# Génère les lueurs "sur mesure" des fonds de menu : chaque lueur est découpée dans l'illustration elle-même (pixels
# de la flamme, du cristal, de la sphère, du feuillage...), puis adoucie (coeur net + halo flou qui SUIT LA FORME), au
# lieu de simples ronds de couleur posés à peu près au bon endroit.
#
# Usage : python Tools/build_menu_glows.py [settings|hall|tree|char_aether|char_kael|char_lyra|all]   (numpy + Pillow)
#   -> textures dans Assets/Resources/<Dossier>/, puis recopier le tableau affiché dans le script de la page
#      (SettingsAmbientFX.Sources, ReputationHallFX.Sources, TreeAmbientFX.ShapedHalos, CharacterBackdropFX.Glows).
#   -> <page>_glow_preview.png à côté de ce script (ou dans GLOW_PREVIEW_DIR) : contrôle visuel, lueurs ajoutées au fond.
import os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE).replace("\\", "/") + "/"
BG = ROOT + "Assets/Game/Assets 2D/UI/Backgrounds/"
PREVIEW = os.environ.get("GLOW_PREVIEW_DIR", HERE + "/")

def clip(x): return np.clip(x, 0.0, 1.0)
def lum(a): return a[..., 0] * 0.299 + a[..., 1] * 0.587 + a[..., 2] * 0.114

def hsv(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = a.max(-1); mn = a.min(-1); d = mx - mn + 1e-6
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60.0
    s = (mx - mn) / (mx + 1e-6)
    return h, s, mx

def mask(a, kind, p, X0, Y0):
    if kind == "warm":      # flammes : chaud et clair
        r, b = a[..., 0], a[..., 2]
        return clip((r - b - 0.04) * 5.0) * clip((lum(a) - p["lo"]) * 4.0)
    if kind == "cool":      # cristaux : bleu et clair
        r, b = a[..., 0], a[..., 2]
        return clip((b - r + 0.01) * 6.0) * clip((lum(a) - p["lo"]) * 4.0)
    if kind == "bright":    # brume lumineuse, clairière : seulement la luminosité
        return clip((lum(a) - p["lo"]) * 3.0)
    if kind == "hue":       # feuillage d'une couleur donnée (teinte en degrés, saturation, luminosité mini)
        h, s, v = hsv(a)
        h0, h1 = p["hue"]
        if h0 <= h1: inh = (h >= h0) & (h <= h1)
        else: inh = (h >= h0) | (h <= h1)
        return inh * clip((s - p.get("smin", 0.3)) * 4.0) * clip((v - p["lo"]) * 3.0)
    if kind == "disc":      # sphère de verre : disque + liseré + reflets
        h, w = a.shape[:2]
        cx, cy, r = p["disc"]
        yy, xx = np.mgrid[0:h, 0:w]
        d = np.sqrt((xx - (cx - X0)) ** 2 + (yy - (cy - Y0)) ** 2)
        inside = clip((r - d) / 2.0)
        inner = clip((r - 5 - d) / 4.0)
        ring = inside * (1.0 - inner)
        return inside * 0.12 + ring * 0.55 + inside * clip((lum(a) - 0.32) * 3.0) * 0.6
    raise ValueError(kind)

def blur(m, rad):
    # vrai noyau gaussien séparable, en flottants (le GaussianBlur de PIL approxime par des boîtes : halos octogonaux)
    if rad <= 0: return m
    r = int(rad * 3)
    x = np.arange(-r, r + 1, dtype=np.float32)
    k = np.exp(-0.5 * (x / rad) ** 2); k /= k.sum()
    m = np.apply_along_axis(lambda v: np.convolve(v, k, mode="same"), 0, m)
    return np.apply_along_axis(lambda v: np.convolve(v, k, mode="same"), 1, m)

# Chaque source : nom, zone (x0,y0,x1,y1) en pixels de l'illustration, type, paramètres, rayon du halo, gain du halo,
# gain du coeur, couleur. "scale" < 1 = texture exportée plus petite (grandes lueurs très floues : pas besoin du détail).
W_ = (1.00, 0.78, 0.42)
PAGES = {
    "settings": dict(src="SettingsBackground.png", out="SettingsGlow", sources=[
        ("candle_l",  (228, 372, 250, 410), "warm", dict(lo=0.45), 10, 1.6, 1.0, W_),
        ("candle_m",  (262, 340, 284, 380), "warm", dict(lo=0.45), 10, 1.6, 1.0, W_),
        ("candle_r",  (294, 376, 316, 414), "warm", dict(lo=0.45), 10, 1.6, 1.0, W_),
        ("lantern_l", (296, 676, 350, 790), "warm", dict(lo=0.40), 22, 2.2, 1.0, (1.00, 0.74, 0.36)),
        ("lantern_r", (1430, 728, 1500, 800), "warm", dict(lo=0.40), 24, 2.2, 1.0, (1.00, 0.74, 0.36)),
        ("crystals",  (30, 488, 200, 680), "cool", dict(lo=0.17), 14, 0.9, 0.45, (0.50, 0.70, 1.00)),
        ("amethyst",  (1400, 770, 1540, 840), "cool", dict(lo=0.30), 8, 0.9, 0.6, (0.78, 0.60, 1.00)),
        ("sphere_1",  (1560, 368, 1624, 432), "disc", dict(disc=(1592, 400, 28)), 12, 0.8, 0.30, (0.62, 0.82, 1.00)),
        ("sphere_2",  (1636, 368, 1700, 432), "disc", dict(disc=(1668, 400, 27)), 12, 0.8, 0.30, (0.78, 0.62, 1.00)),
        ("sphere_3",  (1694, 408, 1760, 474), "disc", dict(disc=(1727, 441, 30)), 12, 0.8, 0.30, (0.70, 0.80, 0.95)),
        ("sphere_4",  (1624, 516, 1692, 584), "disc", dict(disc=(1658, 550, 32)), 13, 0.8, 0.30, (0.78, 0.60, 1.00)),
        ("sphere_5",  (1742, 541, 1810, 609), "disc", dict(disc=(1776, 575, 32)), 13, 0.8, 0.30, (0.50, 0.70, 1.00)),
        ("globe",     (834, 498, 890, 556), "disc", dict(disc=(862, 527, 26)), 12, 0.7, 0.25, (0.55, 0.72, 1.00)),
    ]),
    # Grand hall de la Réputation (1888 x 1080) : flamme de chaque torche, et la brume claire au fond de la salle.
    "hall": dict(src="ghibli_stone_hall_1920x1080 1.png", out="HallGlow", sources=[
        ("torch_1", (492, 530, 545, 600), "warm", dict(lo=0.62), 16, 2.0, 1.0, (1.00, 0.66, 0.30)),
        ("torch_2", (688, 598, 724, 650), "warm", dict(lo=0.62), 13, 2.0, 1.0, (1.00, 0.66, 0.30)),
        ("torch_3", (804, 638, 832, 678), "warm", dict(lo=0.62), 11, 2.0, 1.0, (1.00, 0.66, 0.30)),
        ("torch_4", (1292, 642, 1316, 678), "warm", dict(lo=0.62), 11, 2.0, 1.0, (1.00, 0.66, 0.30)),
        ("torch_5", (1348, 602, 1384, 650), "warm", dict(lo=0.62), 13, 2.0, 1.0, (1.00, 0.66, 0.30)),
        ("torch_6", (1462, 518, 1502, 592), "warm", dict(lo=0.62), 15, 2.0, 1.0, (1.00, 0.66, 0.30)),
        ("torch_7", (1696, 396, 1752, 492), "warm", dict(lo=0.62), 18, 2.0, 1.0, (1.00, 0.66, 0.30)),
        ("torch_8", (266, 684, 296, 712), "warm", dict(lo=0.55), 9, 1.6, 0.8, (1.00, 0.66, 0.30)),
        ("hall_fog", (760, 330, 1180, 820), "bright", dict(lo=0.50), 40, 1.0, 0.0, (1.00, 0.80, 0.55), 0.5),
    ]),
    # Arbre des Compétences (2048 x 1152) : feuillage orange, rayon doré, feuillage vert, feuillage turquoise et ses
    # paillettes, clairière ensoleillée.
    "tree": dict(src="BackgroundV4.png", out="TreeGlow", ref=(2048, 1152), sources=[
        ("canopy_orange", (0, 0, 860, 520), "hue", dict(hue=(355, 40), smin=0.45, lo=0.55), 26, 1.0, 0.35, (1.00, 0.58, 0.22), 0.5),
        ("sunburst", (300, 120, 900, 680), "hue", dict(hue=(40, 62), smin=0.25, lo=0.80), 30, 1.0, 0.30, (1.00, 0.86, 0.45), 0.5),
        ("canopy_green", (680, 0, 1400, 400), "hue", dict(hue=(70, 140), smin=0.35, lo=0.60), 24, 0.9, 0.25, (0.62, 1.00, 0.55), 0.5),
        ("canopy_cyan", (1240, 0, 2048, 560), "hue", dict(hue=(160, 200), smin=0.30, lo=0.62), 26, 1.0, 0.35, (0.40, 0.90, 1.00), 0.5),
        ("cyan_stream", (1300, 330, 1640, 720), "bright", dict(lo=0.72), 14, 0.9, 0.5, (0.85, 0.95, 0.70), 0.5),
        ("clearing", (520, 760, 1560, 1152), "hue", dict(hue=(45, 95), smin=0.35, lo=0.80), 28, 1.0, 0.20, (1.00, 0.90, 0.55), 0.5),
    ]),
    # Page Personnages : gros cristal flottant au centre (et ses volutes / paillettes). Un fond par héros
    # (Personnages/Background<Nom>.jpg), une texture par élément.
    "char_aether": dict(src="Personnages/BackgroundAether.jpg", out="CharGlow", prefix="aether_", sources=[
        ("crystal", (940, 385, 1080, 740), "bright", dict(lo=0.55, poly=[(1010, 392), (1072, 466), (1074, 642), (1018, 734), (948, 644), (948, 470)]), 22, 1.6, 0.55, (1.00, 0.82, 0.50)),
    ]),
    "char_kael": dict(src="Personnages/BackgroundKael.jpg", out="CharGlow", prefix="kael_", sources=[
        ("crystal", (900, 350, 1220, 760), "hue", dict(hue=(90, 170), smin=0.35, lo=0.35), 22, 1.6, 0.55, (0.50, 1.00, 0.62)),
    ]),
    "char_lyra": dict(src="Personnages/BackgroundLyra.jpg", out="CharGlow", prefix="lyra_", sources=[
        ("crystal", (840, 330, 1280, 780), "hue", dict(hue=(200, 295), smin=0.30, lo=0.45), 22, 1.6, 0.55, (0.62, 0.55, 1.00)),
    ]),
}

def build(page):
    cfg = PAGES[page]
    im = Image.open(BG + cfg["src"]).convert("RGB")
    if "ref" in cfg: im = im.resize(cfg["ref"], Image.LANCZOS)   # repère = taille du sprite importé (fichier plus grand)
    A = np.asarray(im).astype(np.float32) / 255.0
    H, W = A.shape[:2]
    out = ROOT + "Assets/Resources/" + cfg["out"] + "/"
    os.makedirs(out, exist_ok=True)
    prev_add = np.zeros((H, W, 3), np.float32)
    lines = []
    for src in cfg["sources"]:
        name, (x0, y0, x1, y1), kind, p, rad, hg, cg, col = src[:8]
        scale = src[8] if len(src) > 8 else 1.0
        pad = int(rad * 3)
        X0, Y0, X1, Y1 = max(0, x0 - pad), max(0, y0 - pad), min(W, x1 + pad), min(H, y1 + pad)
        a = A[Y0:Y1, X0:X1]
        h, w = a.shape[:2]
        zone = np.zeros((h, w), np.float32)
        zone[y0 - Y0:y1 - Y0, x0 - X0:x1 - X0] = 1.0
        if "poly" in p:     # forme exacte de l'objet (ex. cristal à facettes) : rien hors du polygone
            from PIL import ImageDraw
            pm = Image.new("L", (w, h), 0)
            ImageDraw.Draw(pm).polygon([(px - X0, py - Y0) for px, py in p["poly"]], fill=255)
            zone = zone * (np.asarray(pm).astype(np.float32) / 255.0)
        # bords de zone adoucis pour les grandes régions (pas d'arête droite dans le halo)
        if scale < 1.0: zone = blur(zone, rad * 0.6)
        m = mask(a, kind, p, X0, Y0) * zone
        if m.max() <= 0.01:
            print("VIDE", name); continue
        m = m / m.max()
        core = blur(m, 1.2)
        halo = blur(m, rad)
        halo = halo / max(halo.max(), 1e-4)
        halo = halo ** 1.4                      # retombée plus douce, sans « plateau » autour de la source
        alpha = clip(core * cg + halo * hg * 0.55)
        alpha = alpha * alpha * (3 - 2 * alpha) * 0.5 + alpha * 0.5
        rgba = np.zeros((h, w, 4), np.float32)
        rgba[..., 0] = col[0]; rgba[..., 1] = col[1]; rgba[..., 2] = col[2]
        wc = clip(core * 1.2) * 0.6 * (1.0 if cg >= 0.5 else cg)    # coeur vers le blanc (flammes), peu pour le feuillage
        for c in range(3): rgba[..., c] = rgba[..., c] * (1 - wc) + wc
        rgba[..., 3] = alpha
        img = Image.fromarray((clip(rgba) * 255).astype(np.uint8), "RGBA")
        if scale < 1.0: img = img.resize((max(4, int(w * scale)), max(4, int(h * scale))), Image.LANCZOS)
        img.save(out + cfg.get("prefix", "") + name + ".png")
        prev_add[Y0:Y1, X0:X1] += rgba[..., :3] * alpha[..., None] * 0.8
        lines.append('        ("%s", %df, %df, %df, %df),' % (cfg.get("prefix", "") + name, X0, Y0, w, h))
    pv = clip(A + prev_add)
    Image.fromarray((pv * 255).astype(np.uint8)).save(PREVIEW + page + "_glow_preview.png")
    print("== %s (%d x %d)" % (page, W, H))
    print("\n".join(lines))

if __name__ == "__main__":
    which = sys.argv[1] if len(sys.argv) > 1 else "all"
    for page in (PAGES if which == "all" else [which]):
        build(page)
