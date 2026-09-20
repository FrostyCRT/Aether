# Outil de dev (2026-09-19) : fabrique Assets/Game/Fonts/Bangers-Regular-Chiffres.ttf a partir de
# Bangers-Regular.ttf en levant l'ambiguite 1 / 7 (le 1 recoit un pied, le 7 une barre : le "7 barre"
# usuel en francais). Le reste de la police est inchange. Requiert : pip install fonttools
# Ensuite, dans Unity : menu Aether > Rebuild Bangers Digits (regenere les chiffres de l'atlas TMP).
# Police renommee "Bangers Chiffres" (la licence OFL demande un autre nom pour une version modifiee).
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables import ttProgram
SRC=r"C:/Users/jules/My project/Aether/Assets/Game/Fonts/Bangers-Regular.ttf"
DST=r"C:/Users/jules/My project/Aether/Assets/Game/Fonts/Bangers-Regular-Chiffres.ttf"
f=TTFont(SRC)
glyf=f['glyf']; hmtx=f['hmtx']
SL=0.355   # pente des traits de Bangers (~19,5 degrés)

def area(pts):
    a=0
    for i in range(len(pts)):
        x1,y1=pts[i]; x2,y2=pts[(i+1)%len(pts)]
        a+=x1*y2-x2*y1
    return a/2

def add_para(gname, x0,x1,y0,y1):
    """parallélogramme penché : bord bas y0 de x0 à x1, bord haut y1 décalé par la pente."""
    g=glyf[gname]
    coords=list(g.coordinates); ends=list(g.endPtsOfContours); flags=list(g.flags)
    # orientation du contour existant
    first_end=ends[0]
    ref=area(coords[:first_end+1])
    dx=(y1-y0)*SL
    quad=[(x0,y0),(x0+dx,y1),(x1+dx,y1),(x1,y0)]   # antihoraire (aire > 0) 
    if area(quad)>0 and ref<0: quad=quad[::-1]
    if area(quad)<0 and ref>0: quad=quad[::-1]
    from fontTools.ttLib.tables._g_l_y_f import GlyphCoordinates
    newc=coords+[(int(round(x)),int(round(y))) for x,y in quad]
    g.coordinates=GlyphCoordinates(newc)
    g.endPtsOfContours=ends+[len(newc)-1]
    g.flags=flags+[1]*4
    g.numberOfContours=len(ends)+1
    g.program=ttProgram.Program(); g.program.fromBytecode(b'')
    g.recalcBounds(glyf)

# 1 : pied (empattement) ; 7 : barre horizontale (7 "barré", habituel en français)
add_para('one', 40, 330, 4, 112)
add_para('seven', 42, 308, 262, 372)
# marge d'avance : le pied du 1 dépasse un peu
adv,lsb=hmtx['one']; hmtx['one']=(adv+30, glyf['one'].xMin)
adv,lsb=hmtx['seven']; hmtx['seven']=(adv+10, glyf['seven'].xMin)

# renommer (la licence OFL demande de ne pas garder le nom d'origine pour une version modifiée)
for rec in f['name'].names:
    if rec.nameID in (1,4,16): rec.string="Bangers Chiffres"
    if rec.nameID==6: rec.string="BangersChiffres-Regular"
f.save(DST)
print("saved")
