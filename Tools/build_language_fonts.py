# Outil de dev (2026-10-15) : polices des langues asiatiques + contrôle de couverture de TOUTES les langues.
#
# 1) Chinois, japonais, coréen : Bangers n'a ni idéogrammes, ni kana, ni hangeul. Chaque langue a sa police de repli
#    Noto Sans (SC / JP / KR) Black (Tools/fonts, licence OFL), réduite aux seuls caractères dont elle a besoin : tout
#    caractère de sa table (LocTableXX) et de son nom de langue absent de Bangers, + la ponctuation de la langue
#    -> Assets/Game/Fonts/Lang/NotoSans<XX>-Black-Jeu.ttf (quelques centaines de Ko au lieu de 6 à 10 Mo)
#    + la liste de ces caractères (même nom, .chars.txt) lue par l'Éditeur.
#    Dans Unity, le menu Aether > Rebuild Language Fonts en fait des atlas TMP statiques (aucun calcul de glyphe en
#    partie), polices de repli globales de TextMeshPro ; en jeu, celle de la langue courante passe en tête
#    (LanguageFonts.cs) : un idéogramme commun au chinois et au japonais n'a pas le même dessin dans les deux pays.
# 2) Contrôle : chaque caractère de chaque table doit exister dans Bangers (cyrillique compris) ou dans la police de SA
#    langue (pas dans celle d'une autre : le dessin serait faux). Sinon : erreur.
#
# À relancer après toute modification de tr_zh / tr_ja / tr_ko (puis Unity : Aether > Rebuild Language Fonts ; l'Éditeur
# signale en rouge un caractère oublié). Requiert : pip install fonttools.
import glob
import os
import re
import sys
from fontTools.ttLib import TTFont
from fontTools import subset

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOC = os.path.join(ROOT, "Assets", "Game", "Scripts", "Localization")
BANGERS = os.path.join(ROOT, "Assets", "Game", "Fonts", "Bangers-Regular-Chiffres.ttf")
OUT_DIR = os.path.join(ROOT, "Assets", "Game", "Fonts", "Lang")
SCHEMA = os.path.join(ROOT, "Assets", "Game", "Scripts", "Settings", "SettingsSchema.cs")

# ponctuation et symboles toujours inclus (même s'ils ne sont pas encore dans la table)
CJK_PUNCT = "，。、：；！？（）《》〈〉「」『』【】⋯·～﹏％＋－×÷＝０１２３４５６７８９．"
SPECS = [
    # code, table, police source, nom de langue (SettingsSchema.LanguageNames), ponctuation en plus
    ("zh", "LocTableZH.cs", "NotoSansSC-Black.ttf", "简体中文", CJK_PUNCT + "￥"),
    ("ja", "LocTableJA.cs", "NotoSansJP-Black.ttf", "日本語", CJK_PUNCT + "ー・〜々"),
    ("ko", "LocTableKO.cs", "NotoSansKR-Black.ttf", "한국어", "·％＋"),
]

# Coréen : ponctuation remplacée par "langs.js build ko" par des caractères que Bangers n'a pas (même liste que KO_PUNCT
# dans Tools/loc/langs.js). Dans la police coréenne réduite, chacun pointe vers le dessin NORMAL (étroit) du signe
# d'origine : la ponctuation coréenne est dessinée par la police coréenne, à la bonne taille, au lieu de Bangers (penché).
KO_PUNCT = {"．": ".", "，": ",", "：": ":", "！": "!", "？": "?", "（": "(", "）": ")", "＋": "+", "―": "—",
            "": "‘", "": "’"}

ENTRY = re.compile(r'\{\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*\}')


def unesc(s):
    return re.sub(r"\\(.)", lambda m: {"n": "\n", "t": "\t"}.get(m.group(1), m.group(1)), s)


def table_values(path):
    return [unesc(m.group(2)) for m in ENTRY.finditer(open(path, encoding="utf-8").read())]


def language_names():
    m = re.search(r"LanguageNames\s*=\s*\{([^}]*)\}", open(SCHEMA, encoding="utf-8").read())
    return re.findall(r'"([^"]*)"', m.group(1))


def strip_tags(v):
    return re.sub(r"<[^>]+>", "", v)


bangers = TTFont(BANGERS).getBestCmap()
os.makedirs(OUT_DIR, exist_ok=True)
fonts = {}          # code -> cmap de la police réduite
problems = 0

# --- 1) une police réduite par langue
for code, table, source, name, extra in SPECS:
    src_path = os.path.join(ROOT, "Tools", "fonts", source)
    chars = set(extra)
    for v in table_values(os.path.join(LOC, table)) + [name]:
        chars.update(ch for ch in strip_tags(v) if ch not in "\n\t " and ord(ch) not in bangers)
    src_cmap = TTFont(src_path).getBestCmap()
    remap = KO_PUNCT if code == "ko" else {}
    for sub_ch in remap:
        if ord(sub_ch) in bangers:
            sys.exit(f"KO_PUNCT : {sub_ch!r} existe dans Bangers, qui le dessinerait : choisir un autre caractère")
    missing = sorted(ch for ch in chars if ord(remap.get(ch, ch)) not in src_cmap)
    if missing:
        sys.exit(f"Caractères absents de {source} : " + " ".join(missing))
    opts = subset.Options()
    opts.layout_features = ["*"]
    opts.name_IDs = ["*"]
    opts.name_languages = ["*"]
    opts.notdef_outline = True
    opts.hinting = False
    opts.desubroutinize = True
    sub = subset.Subsetter(opts)
    sub.populate(unicodes=[ord(remap.get(c, c)) for c in chars])
    font = TTFont(src_path, recalcTimestamp=False)   # fichier identique d'un lancement à l'autre (pas de faux changement git)
    sub.subset(font)
    for table in font["cmap"].tables:                # caractère de remplacement -> dessin du signe d'origine
        if table.isUnicode():
            for sub_ch, src_ch in remap.items():
                if ord(src_ch) in table.cmap:
                    table.cmap[ord(sub_ch)] = table.cmap[ord(src_ch)]
    out = os.path.join(OUT_DIR, source.replace(".ttf", "-Jeu.ttf"))
    font.save(out)
    # liste des caractères de l'atlas, lue par Aether > Rebuild Language Fonts (une seule source de vérité)
    with open(out.replace(".ttf", ".chars.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("".join(sorted(chars)))
    fonts[code] = TTFont(out).getBestCmap()
    print(f"{code} : {len(chars)} caractères -> {os.path.relpath(out, ROOT)} ({os.path.getsize(out) // 1024} Ko)")

# --- 2) couverture : chaque langue dessinée par Bangers ou par SA police
own = {spec[1]: spec[0] for spec in SPECS}
for path in sorted(glob.glob(os.path.join(LOC, "LocTable*.cs"))):
    code = own.get(os.path.basename(path))
    allowed = [fonts[code]] if code else []
    miss = set()
    for v in table_values(path):
        for ch in strip_tags(v):
            if ch in "\n\t" or ord(ch) in bangers or any(ord(ch) in a for a in allowed):
                continue
            miss.add(ch)
    if miss:
        problems += 1
        print(f"  MANQUE dans {os.path.basename(path)} : " + " ".join(f"{c} (U+{ord(c):04X})" for c in sorted(miss)))
# noms des langues (affichés dans toutes les langues) : chacun doit être dessinable
for name in language_names():
    for ch in name:
        if ord(ch) not in bangers and not any(ord(ch) in cm for cm in fonts.values()):
            problems += 1
            print(f"  MANQUE pour le nom de langue « {name} » : {ch}")
print("Couverture des polices : " + ("tous les caractères de toutes les langues sont dessinables." if not problems
                                       else f"{problems} problème(s)."))
sys.exit(1 if problems else 0)
