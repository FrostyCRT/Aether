# Retire le liseré violet / magenta laissé par le détourage des images générées (fond magenta remplacé par de la
# transparence) : demande utilisateur du 2026-11-08 ("retirer le violet du détourage de l'arrière-plan transparent
# des images [...] on voit des traces de violet").
#
# Deux défauts corrigés, image par image :
#  1. BORD TEINTÉ : les pixels du contour (semi-transparents, ou opaques à moins de 3 px du vide) ont gardé une part
#     du fond magenta. Suppression de teinte (« despill ») : la part de magenta (min(R,B) - V) est retirée du rouge et
#     du bleu -> un contour sombre teinté redevient sombre, un bord clair redevient neutre. UNIQUEMENT si l'intérieur
#     le plus proche de l'objet n'est pas violet lui-même (Lyra, améthystes... gardent leurs vrais bords violets).
#  2. PIXELS INVISIBLES MAGENTA : les pixels totalement transparents gardent parfois la couleur du fond (ex. 252,0,248) ;
#     invisible telle quelle, mais le filtrage / les mipmaps la font baver sur le contour. Ils prennent la couleur de
#     l'intérieur le plus proche.
# L'alpha n'est jamais modifié. Original sauvegardé dans _ImageBackups/<date>/ (hors Assets, non importé par Unity).
#
# Usage : python Tools/defringe_images.py [--dry] [fichier ou dossier ...]   (numpy, Pillow, scipy)
#         sans argument : Assets/Game/Assets 2D et Assets/Resources (hors textures de lueurs générées).
import os, sys, shutil, datetime
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE).replace("\\", "/") + "/"
DEFAULT_DIRS = ["Assets/Game/Assets 2D", "Assets/Resources"]
SKIP = ["Assets/Resources/SettingsGlow", "Assets/Resources/HallGlow", "Assets/Resources/TreeGlow",
        "Assets/Resources/CharGlow", "Assets/Resources/Fonts"]
OUT_DIR = os.environ.get("DEFRINGE_OUT")   # aperçu : écrit le résultat dans ce dossier sans toucher l'original
BACKUP = ROOT + "_ImageBackups/" + datetime.date.today().isoformat() + "/"

def is_purple(rgb, strong=False):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    if strong: return (b - g > 0.10) & (r - g > 0.04)
    return (b - g > 0.05) & (r - g > 0.02)

def process(path, dry=False):
    im = Image.open(path)
    if im.mode not in ("RGBA", "LA", "P"): return None
    im = im.convert("RGBA")
    a = np.asarray(im).astype(np.float32) / 255.0
    al = a[..., 3]
    if al.min() > 0.99: return None
    rgb = a[..., :3].copy()

    solid = al >= 0.5
    # distance au VIDE réel (alpha < 0,1) : le contour est mesuré depuis le vide, pas depuis toute zone semi-transparente
    # (les images en fondu - portrait de Lyra... - ont un intérieur semi-transparent qui fait partie du dessin)
    clear = al < 0.1
    dist_in = ndimage.distance_transform_edt(~clear)
    trusted = (al >= 0.9) & (dist_in >= 3)
    if trusted.sum() < 16: return None
    _, idx = ndimage.distance_transform_edt(~trusted, return_indices=True)
    near = rgb[idx[0], idx[1]]                                      # couleur de l'intérieur le plus proche

    # Bande traitée : le contour (à moins de 3,5 px du vide). Si le DESSIN ne contient pas de violet (moins de 2 % des
    # pixels bien opaques), toute la partie semi-transparente l'est aussi : un halo rose dans une fumée est alors un
    # reste du fond (plume Cadence, lame de la Réputation). Sinon (Lyra, parchemin des fusions...), contour seulement.
    opaque = al >= 0.9
    purple_art = (is_purple(rgb, strong=True) & opaque).sum() > 0.02 * max(1, int(opaque.sum()))
    band = (al > 0) & (dist_in < 3.5)
    if not purple_art: band |= (al > 0) & (al < 0.95)
    interior_purple = is_purple(near, strong=True)
    spill = np.minimum(rgb[..., 0], rgb[..., 2]) - rgb[..., 1]
    fix = band & ~interior_purple & is_purple(rgb) & (spill > 0)

    # 2e passe, plus large (jusqu'à 8 px du vide : mèches de cheveux, petits trous) mais limitée au MAGENTA PUR du fond
    # de détourage (teinte ~285-325°, très saturé) - jamais une couleur de dessin normale - toujours protégée par
    # l'intérieur voisin (pris plus loin, à 8 px) s'il est violet lui-même.
    deep = (al >= 0.9) & (dist_in >= 8)
    # Garde-fou : une image dont l'intérieur contient déjà une vraie part de magenta / rose (carte Couteaux...) utilise
    # cette couleur dans son dessin -> les passes 2 et 3 (magenta pur) ne s'y appliquent pas, seul le contour fin l'est.
    imx = rgb.max(-1); imn = rgb.min(-1); idd = imx - imn + 1e-6
    ihue = np.where(imx == rgb[..., 0], ((rgb[..., 1] - rgb[..., 2]) / idd) % 6, np.where(imx == rgb[..., 1], (rgb[..., 2] - rgb[..., 0]) / idd + 2, (rgb[..., 0] - rgb[..., 1]) / idd + 4)) * 60.0
    inner = (al >= 0.3) & (dist_in >= 3)
    magenta_art = (((ihue >= 275) & (ihue <= 335) & (idd / (imx + 1e-6) > 0.3)) & inner).sum() > 0.03 * max(1, int(inner.sum()))
    if deep.sum() >= 16 and not magenta_art:
        _, idx8 = ndimage.distance_transform_edt(~deep, return_indices=True)
        near8 = rgb[idx8[0], idx8[1]]
        mx = rgb.max(-1); mn = rgb.min(-1); d = mx - mn + 1e-6
        r_, g_, b_ = rgb[..., 0], rgb[..., 1], rgb[..., 2]
        hue = np.where(mx == r_, ((g_ - b_) / d) % 6, np.where(mx == g_, (b_ - r_) / d + 2, (r_ - g_) / d + 4)) * 60.0
        sat = d / (mx + 1e-6)
        matte = (hue >= 285) & (hue <= 325) & (sat > 0.5) & (mx > 0.35)
        wide = (al > 0) & (dist_in < 8)
        # protection : l'objet autour est lui-même magenta / rose (ex. tourbillon de la carte Couteaux)
        nmx = near8.max(-1); nmn = near8.min(-1); nd = nmx - nmn + 1e-6
        nr, ng, nb = near8[..., 0], near8[..., 1], near8[..., 2]
        nhue = np.where(nmx == nr, ((ng - nb) / nd) % 6, np.where(nmx == ng, (nb - nr) / nd + 2, (nr - ng) / nd + 4)) * 60.0
        near_magenta = (nhue >= 275) & (nhue <= 335) & (nd / (nmx + 1e-6) > 0.3)
        fix |= wide & matte & ~near_magenta & (spill > 0)
    n_fix = int(fix.sum())

    out = rgb.copy()
    s = np.clip(spill, 0, None)
    out[..., 0] = np.where(fix, rgb[..., 0] - s, rgb[..., 0])
    out[..., 2] = np.where(fix, rgb[..., 2] - s, rgb[..., 2])

    transparent = al <= 0.004
    tr_purple = transparent & is_purple(rgb, strong=True)
    n_tr = int(tr_purple.sum())
    out[transparent] = near[transparent]                            # plus de couleur parasite sous la transparence

    # 3e passe : POCHES de fond restées opaques (petit bloc de magenta pur coincé entre deux mèches, par exemple) :
    # bloc de magenta très pur, < 0,5 % de l'objet, dont le pourtour n'est pas magenta -> rendu transparent, comme le
    # reste du fond. Les grandes zones roses voulues (tourbillon des Couteaux...) dépassent la limite de taille.
    mx = rgb.max(-1); mn = rgb.min(-1); d = mx - mn + 1e-6
    r_, g_, b_ = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.where(mx == r_, ((g_ - b_) / d) % 6, np.where(mx == g_, (b_ - r_) / d + 2, (r_ - g_) / d + 4)) * 60.0
    pure = (hue >= 290) & (hue <= 322) & (d / (mx + 1e-6) > 0.55) & (mx > 0.45) & (al > 0.5)
    lab, nlab = ndimage.label(pure) if not magenta_art else (None, 0)
    pocket = np.zeros_like(pure)
    if nlab:
        limit = 0.005 * max(1, int(solid.sum()))
        sizes = ndimage.sum(pure, lab, range(1, nlab + 1))
        for i, sz in enumerate(sizes, start=1):
            if sz < 1 or sz > limit: continue
            blob = lab == i
            near_blob = ndimage.binary_dilation(blob, iterations=3)
            ring = ndimage.binary_dilation(blob, iterations=7) & ~near_blob & (al > 0.5)   # pourtour pris à 3-7 px (le bord immédiat est un dégradé du même magenta)
            if ring.sum() == 0: continue
            rr = rgb[ring]
            rmx = rr.max(-1); rmn = rr.min(-1); rd = rmx - rmn + 1e-6
            rhue = np.where(rmx == rr[:, 0], ((rr[:, 1] - rr[:, 2]) / rd) % 6, np.where(rmx == rr[:, 1], (rr[:, 2] - rr[:, 0]) / rd + 2, (rr[:, 0] - rr[:, 1]) / rd + 4)) * 60.0
            ring_mag = ((rhue >= 275) & (rhue <= 335) & (rd / (rmx + 1e-6) > 0.35)).mean()
            if ring_mag < 0.25: pocket |= blob
    n_pocket = int(pocket.sum())
    if n_pocket:
        pocket = ndimage.binary_dilation(pocket, iterations=3) & (is_purple(rgb) | pocket)   # + son dégradé magenta
        al = np.where(pocket, 0.0, al)
        out[pocket] = near[pocket]
        n_fix += n_pocket

    if n_fix == 0 and n_tr == 0: return (0, 0)
    if not dry and OUT_DIR:
        res = np.dstack([np.clip(out, 0, 1), al])
        Image.fromarray((res * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT_DIR, os.path.basename(path)))
    elif not dry:
        rel = os.path.relpath(path, ROOT)
        bk = os.path.join(BACKUP, rel)
        os.makedirs(os.path.dirname(bk), exist_ok=True)
        if not os.path.exists(bk): shutil.copy2(path, bk)
        res = np.dstack([np.clip(out, 0, 1), al])
        Image.fromarray((res * 255 + 0.5).astype(np.uint8), "RGBA").save(path, optimize=True)
    return (n_fix, n_tr)

def targets(args):
    if not args: args = [ROOT + d for d in DEFAULT_DIRS]
    for t in args:
        if os.path.isfile(t): yield t; continue
        for dp, dn, fn in os.walk(t):
            relp = os.path.relpath(dp, ROOT).replace("\\", "/")
            if any(relp.startswith(s) for s in SKIP): continue
            for f in fn:
                if f.lower().endswith(".png"): yield os.path.join(dp, f)

if __name__ == "__main__":
    dry = "--dry" in sys.argv
    args = [x for x in sys.argv[1:] if x != "--dry"]
    total = 0
    for p in targets(args):
        r = process(p, dry)
        if r and (r[0] or r[1]):
            total += 1
            print("%-90s bord corrigé %7d px   transparents magenta %8d px" % (os.path.relpath(p, ROOT), r[0], r[1]))
    print(("[essai] " if dry else "") + "%d image(s) concernée(s)" % total + ("" if dry else " ; originaux dans " + BACKUP))
