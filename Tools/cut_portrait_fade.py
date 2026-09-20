# Outil de dev (2026-09-19) - coupe la "fondue" du bas des portraits de la page Personnages.
#
# Les portraits (AetherTR / KaelTR / LyraTR.png) ont une fondue baked qui ne finit jamais en transparence
# totale (jambes et pieds encore visibles a 5-30 %). Cet outil fabrique des COPIES (<nom>_fade.png, les
# originaux ne sont pas touches) dont l'alpha atteint 0 EXACTEMENT au niveau du milieu de l'image du nom :
#   - fraction END de la hauteur = (418,23 + |y du nom|) / 836,46, avec y du nom = position locale de
#     CharacterName dans CharacterImage (cadre de 836,46 de haut) : -218,33 -> 0.761 ; -194,33 -> 0.7324 ;
#   - alpha = alpha d'origine x (1 - smoothstep(START, END, y)), START = END - 0.16 : la fondue s'etale sur ~16 %
#     de la hauteur au lieu de se terminer en dehors de l'ecran.
# Si l'image du nom est deplacee, changer END. Requiert : pip install pillow numpy
import shutil, uuid, sys
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent / "Assets/Game/Assets 2D/Player"
END = 0.7324            # nom remonte de 24 (2026-09-19) ; voir ci-dessus
START = END - 0.16

def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)

for name in ("AetherTR", "KaelTR", "LyraTR"):
    src = ROOT / f"{name}.png"
    dst = ROOT / f"{name}_fade.png"
    im = Image.open(src).convert("RGBA")
    a = np.array(im)
    h = a.shape[0]
    y = (np.arange(h) + 0.5) / h
    mask = 1.0 - smooth(START, END, y)              # 1 en haut, 0 a partir de END
    a[:, :, 3] = np.round(a[:, :, 3].astype(np.float32) * mask[:, None]).astype(np.uint8)
    Image.fromarray(a, "RGBA").save(dst)
    # .meta : copie de celui de l'original (memes reglages de sprite), nouveau guid
    meta_src, meta_dst = Path(str(src) + ".meta"), Path(str(dst) + ".meta")
    if not meta_dst.exists():
        txt = meta_src.read_text(encoding="utf-8")
        old = [l for l in txt.splitlines() if l.startswith("guid:")][0]
        txt = txt.replace(old, "guid: " + uuid.uuid4().hex)
        meta_dst.write_text(txt, encoding="utf-8")
    print(name, "->", dst.name, im.size)
