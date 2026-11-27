import bpy, bmesh, math, random, json
from mathutils import Vector, Matrix

# Boss 3 v4 (2026-11-26) : LE LOTUS DE LA SOURCE. Remplace l'Archonte (humanoïde « pas terminé » : la modélisation par
# code donne un mannequin ; une fleur, géométrique et radiale, se lit parfaitement sous la caméra à 65°).
# La Source de mana sous forme de fleur sacrée flottant au-dessus de la faille :
#  - 3 couronnes de pétales (10 longs pétales-lames presque à plat, 10 moyens, 8 intérieurs en coupe), creusés,
#    cintrés, cannelés (nervures), dégradé blanc crème (base) -> rose poudré (pointe) rangé dans 'Col' (facteur t) ;
#  - réceptacle (cœur du lotus) + couronne d'étamines ; au centre le CŒUR DE CRISTAL de la Source (bleu Cerf) ;
#  - calice sombre sous la fleur, racines-lianes qui plongent dans la faille ;
#  - corruption INDIGO très sombre d'un seul côté (éclats qui percent la fleur + nervures noircies sur 3 pétales).
# Haut +Z, mètres, avant -Y. Collection 'Lotus', posée en X = 80.
OX = 80.0
rnd = random.Random(3)
col = bpy.data.collections.get('Lotus')
if col is None:
    col = bpy.data.collections.new('Lotus'); bpy.context.scene.collection.children.link(col)
for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)

def mat(name, base, metal=0.0, rough=0.5, emit=None, strength=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = base + (1,); b.inputs['Metallic'].default_value = metal; b.inputs['Roughness'].default_value = rough
    b.inputs['Emission Color'].default_value = (emit or (0, 0, 0)) + (1,); b.inputs['Emission Strength'].default_value = strength if emit else 0.0
    return m
M = {'petal': mat('Lotus_Petal', (0.97, 0.86, 0.9)), 'pod': mat('Lotus_Pod', (0.86, 0.84, 0.55)),
     'stamen': mat('Lotus_Stamen', (0.95, 0.8, 0.4)), 'crystal': mat('Lotus_Crystal', (0.45, 0.75, 0.95), rough=0.1, emit=(0.4, 0.7, 1.0), strength=0.4),
     'calyx': mat('Lotus_Calyx', (0.14, 0.3, 0.36)), 'root': mat('Lotus_Root', (0.18, 0.36, 0.42)),
     'corrupt': mat('Lotus_Corrupt', (0.1, 0.07, 0.2), rough=0.2, emit=(0.25, 0.1, 0.45), strength=0.3),
     'crack': mat('Lotus_Crack', (0.2, 0.12, 0.35)),
     'petalCorrupt': mat('Lotus_PetalCorrupt', (0.55, 0.45, 0.65))}

OBJS = []
SVALS = {}
def obj(name, bm, m, smooth=True, sub=0, tvals=None):
    me = bpy.data.meshes.new(name)
    tv = {v.index: tvals.get(v, 0.0) for v in bm.verts} if tvals is not None else None
    sv = {v.index: SVALS.get(v, 0.0) for v in bm.verts} if tvals is not None else None
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = smooth
    me.materials.append(M[m])
    if tv is not None:
        a = me.attributes.new('t', 'FLOAT', 'POINT')
        for i, v in tv.items(): a.data[i].value = v
        a2 = me.attributes.new('s', 'FLOAT', 'POINT')
        for i, v in sv.items(): a2.data[i].value = v
    o = bpy.data.objects.new(name, me); col.objects.link(o); o.location = (OX, 0, 0)
    if sub: md = o.modifiers.new('Sub', 'SUBSURF'); md.levels = sub; md.render_levels = sub
    OBJS.append(o); return o

# ---------------------------------------------------------------------------------------------------------------- pétale
def petal(bm, tvals, a, r0, z0, L, W, tilt, curl, cup, flute=0.012, nt=26, ns=14, T=0.07, droop=0.0, tear=0.0):
    """Pétale de lotus partant de (rayon r0, hauteur z0) dans la direction d'azimut a.
    tilt = angle de départ au-dessus de l'horizontale ; curl = relèvement supplémentaire vers la pointe (degrés)."""
    rad = Vector((math.cos(a), math.sin(a), 0)); up = Vector((0, 0, 1)); side = Vector((-math.sin(a), math.cos(a), 0))
    C = []; N = []; p = rad * r0 + up * z0
    for i in range(nt + 1):
        t = i / nt; th = math.radians(tilt + curl * t ** 1.6 - droop * t)
        d = rad * math.cos(th) + up * math.sin(th); n = -rad * math.sin(th) + up * math.cos(th)
        C.append(p.copy()); N.append(n); p = p + d * (L / nt)
    def w(t): return W * (max(0.0, math.sin(math.pi * t ** 0.72)) ** 0.75) + 0.1 * W * (1 - t)
    TT = []; BB = []
    for i in range(nt + 1):
        t = i / nt; rt = []; rb = []
        for j in range(ns + 1):
            s = -1 + 2 * j / ns; ww = w(t)
            if tear > 0 and s > 0:   # pétale corrompu : bord déchiré (une « morsure » + petites dents vers la pointe)
                bite = 0.55 * math.exp(-((t - 0.62) / 0.07) ** 2) + 0.14 * max(0.0, math.sin(t * 55.0)) ** 3 * min(1.0, max(0.0, (t - 0.4) / 0.4))
                ww *= 1.0 - tear * bite
            off = side * s * ww + N[i] * (cup * (s * ww) ** 2 / W + flute * math.cos(s * math.pi * 4.5) * (1 - abs(s)) * min(1, t * 3))
            th = T * (1 - s * s) * (0.35 + 0.65 * (1 - t))
            vt = bm.verts.new(C[i] + off + N[i] * th / 2); vb = bm.verts.new(C[i] + off - N[i] * th / 2)
            tvals[vt] = t; tvals[vb] = t; SVALS[vt] = s; SVALS[vb] = s; rt.append(vt); rb.append(vb)
        TT.append(rt); BB.append(rb)
    for i in range(nt):
        for j in range(ns):
            bm.faces.new((TT[i][j], TT[i][j + 1], TT[i + 1][j + 1], TT[i + 1][j]))
            bm.faces.new((BB[i][j], BB[i + 1][j], BB[i + 1][j + 1], BB[i][j + 1]))
        bm.faces.new((TT[i][0], TT[i + 1][0], BB[i + 1][0], BB[i][0])); bm.faces.new((TT[i][ns], BB[i][ns], BB[i + 1][ns], TT[i + 1][ns]))
    bm.faces.new([TT[0][j] for j in range(ns + 1)] + [BB[0][j] for j in range(ns, -1, -1)])
    return C, N, side, w

WHORLS = [  # (nombre, rayon de base, hauteur, longueur, demi-largeur, inclinaison, relèvement, creux, décalage, retombée)
    (12, 0.9, 1.55, 5.0, 0.95, 8, 34, 0.32, 0.0, 4),
    (10, 0.72, 1.7, 3.8, 0.88, 30, 32, 0.38, 0.5, 0),
    (8, 0.45, 1.85, 2.7, 0.72, 58, 20, 0.45, 0.25, 0)]
PETALS = []   # (couronne, index, azimut) - pour le rig
CORRUPT_SIDE = math.radians(205)
# RETIRÉ (2026-11-27, retour utilisateur : « le pétale violet fait plus bizarre qu'autre chose, pas besoin de marque de
# corruption ») - la fleur est entièrement rose ; le code de corruption reste désactivé par ce drapeau.
CORRUPTION = False
for wi, (n, r0, z0, L, W, tilt, curl, cup, off, droop) in enumerate(WHORLS):
    for k in range(n):
        a = 2 * math.pi * (k + off) / n + rnd.uniform(-0.04, 0.04)
        bm = bmesh.new(); tv = {}
        # pétales du côté corrompu : FANÉS (retombent, pointe recroquevillée vers le bas), bord DÉCHIRÉ, pointes indigo
        da = abs((a - CORRUPT_SIDE + math.pi) % (2 * math.pi) - math.pi)
        corrupt = CORRUPTION and da < (0.42 if wi == 0 else 0.3) and wi < 2
        Ln = L * rnd.uniform(0.95, 1.05); tl = tilt + rnd.uniform(-3, 3)
        if corrupt: Cl, _, _, _ = petal(bm, tv, a, r0, z0, Ln * 0.9, W * 0.92, tl - 6, curl * 0.15, cup * 1.3, droop=droop + 22, tear=1.0)
        else: Cl, _, _, _ = petal(bm, tv, a, r0, z0, Ln, W, tl, curl, cup, droop=droop)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        o = obj('Lotus_Petal_%d_%d' % (wi, k), bm, 'petalCorrupt' if corrupt else 'petal', sub=1, tvals=tv); o['azimuth'] = a; o['whorl'] = wi; o['pts'] = json.dumps([list(q) for q in Cl])
        PETALS.append((wi, k, a))

# ------------------------------------------------------------------------------------------- calice, réceptacle, étamines
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=20, radius=1.0)
bmesh.ops.transform(bm, matrix=Matrix.Translation((0, 0, 1.55)) @ Matrix.Diagonal((1.0, 1.0, 0.55, 1)), verts=bm.verts)
obj('Lotus_Calyx', bm, 'calyx', sub=1)
# réceptacle : cône renversé à dessus plat, alvéoles (graines) en creux
bm = bmesh.new(); prev = None; sides = 40
for i, (z, r) in enumerate([(1.75, 0.35), (1.95, 0.5), (2.12, 0.6), (2.2, 0.58)]):
    cur = [bm.verts.new((math.cos(2 * math.pi * k / sides) * r, math.sin(2 * math.pi * k / sides) * r, z)) for k in range(sides)]
    if prev:
        for k in range(sides): bm.faces.new((prev[k], prev[(k + 1) % sides], cur[(k + 1) % sides], cur[k]))
    prev = cur
bm.faces.new(prev)
obj('Lotus_Pod', bm, 'pod', sub=1)

# étamines : filaments qui s'ouvrent autour du réceptacle, petite anthère au bout
bm = bmesh.new()
for k in range(44):
    aa = 2 * math.pi * k / 44 + rnd.uniform(-0.05, 0.05); rad = Vector((math.cos(aa), math.sin(aa), 0))
    b0 = rad * 0.58 + Vector((0, 0, 2.15)); L = rnd.uniform(0.45, 0.65)
    pts = [b0 + rad * (L * u * 0.55) + Vector((0, 0, L * math.sin(u * 1.2) * 0.8)) for u in (0, 0.33, 0.66, 1.0)]
    prev = None
    for i, pp in enumerate(pts):
        d = (pts[min(i + 1, 3)] - pts[max(i - 1, 0)]).normalized(); s = d.orthogonal().normalized(); u = d.cross(s); r = 0.018 * (1 - 0.4 * i / 3)
        cur = [bm.verts.new(pp + (s * math.cos(2 * math.pi * q / 6) + u * math.sin(2 * math.pi * q / 6)) * r) for q in range(6)]
        if prev:
            for q in range(6): bm.faces.new((prev[q], prev[(q + 1) % 6], cur[(q + 1) % 6], cur[q]))
        prev = cur
    sb = bmesh.new(); bmesh.ops.create_uvsphere(sb, u_segments=8, v_segments=5, radius=0.045)
    bmesh.ops.transform(sb, matrix=Matrix.Translation(pts[-1]) @ Matrix.Diagonal((0.8, 0.8, 1.4, 1)), verts=sb.verts)
    me = bpy.data.meshes.new('t'); sb.to_mesh(me); sb.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)
obj('Lotus_Stamens', bm, 'stamen')

# ------------------------------------------------------------------------------------------------- cœur de cristal
def prism(bm, base, d, length, radius, sides=6, tip=0.3, heel=0.4):
    d = Vector(d).normalized(); s = d.orthogonal().normalized(); u = d.cross(s).normalized(); base = Vector(base); tw = rnd.random() * 6.28
    def P(t, r): return [base + d * length * t + (s * math.cos(tw + 2 * math.pi * k / sides) + u * math.sin(tw + 2 * math.pi * k / sides)) * r for k in range(sides)]
    r0 = [bm.verts.new(p) for p in P(0, radius * 0.8)]; r1 = [bm.verts.new(p) for p in P(1 - tip, radius)]
    t = bm.verts.new(base + d * length); h = bm.verts.new(base - d * radius * heel)
    for k in range(sides):
        k2 = (k + 1) % sides
        bm.faces.new((r0[k], r0[k2], r1[k2], r1[k])); bm.faces.new((r1[k], r1[k2], t)); bm.faces.new((r0[k2], r0[k], h))
bm = bmesh.new()
prism(bm, (0, 0, 2.1), (0, 0, 1), 4.0, 0.48, 8, 0.32, 0.2)                     # flèche centrale
for k in range(7):                                                              # couronne de flèches inclinées
    aa = 2 * math.pi * k / 7 + 0.2
    prism(bm, (math.cos(aa) * 0.32, math.sin(aa) * 0.32, 2.15), (math.cos(aa) * 0.28, math.sin(aa) * 0.28, 1), 2.2 + 0.5 * (k % 3) * 0.5, 0.24, 6, 0.36)
for k in range(9):                                                              # petits éclats au pied
    aa = 2 * math.pi * k / 9 + 0.5
    prism(bm, (math.cos(aa) * 0.5, math.sin(aa) * 0.5, 2.15), (math.cos(aa) * 0.8, math.sin(aa) * 0.8, 1), 0.8 + 0.3 * (k % 2), 0.13, 5, 0.4)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
obj('Lotus_Heart', bm, 'crystal', smooth=False)

# ------------------------------------------------------------------------------------------------- racines-lianes
bm = bmesh.new()
ROOTS = []
for k in range(6):
    aa = 2 * math.pi * k / 6 + 0.5; rad = Vector((math.cos(aa), math.sin(aa), 0)); tg = Vector((-math.sin(aa), math.cos(aa), 0))
    P = [rad * 0.35 + Vector((0, 0, 1.2)), rad * 0.8 + Vector((0, 0, 0.85)) + tg * 0.2, rad * 1.4 + Vector((0, 0, 0.55)) - tg * 0.15,
         rad * 1.9 + Vector((0, 0, 0.42)) + tg * 0.2, rad * 2.2 + Vector((0, 0, 0.36))]   # restent au-dessus du sol
    E = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]; C = []
    for i in range(1, len(E) - 2):
        a_, b_, c_, d_ = E[i - 1], E[i], E[i + 1], E[i + 2]
        for q in range(8):
            t = q / 8; t2 = t * t; t3 = t2 * t
            C.append(0.5 * (2 * b_ + (-a_ + c_) * t + (2 * a_ - 5 * b_ + 4 * c_ - d_) * t2 + (-a_ + 3 * b_ - 3 * c_ + d_) * t3))
    C.append(P[-1]); ROOTS.append(P)
    prev = None; n = len(C)
    for i, pp in enumerate(C):
        d = (C[min(i + 1, n - 1)] - C[max(i - 1, 0)]).normalized(); s = d.orthogonal().normalized(); u = d.cross(s); r = 0.13 * (1 - 0.85 * i / (n - 1)) + 0.012
        cur = [bm.verts.new(pp + (s * math.cos(2 * math.pi * q / 10) + u * math.sin(2 * math.pi * q / 10)) * r) for q in range(10)]
        if prev:
            for q in range(10): bm.faces.new((prev[q], prev[(q + 1) % 10], cur[(q + 1) % 10], cur[q]))
        prev = cur
    bm.faces.new(prev)
ro = obj('Lotus_Roots', bm, 'root'); ro['roots'] = json.dumps([[list(q) for q in P] for P in ROOTS])

if CORRUPTION:
    # ------------------------------------------------------------------------------------------------- corruption (un côté)
    bm = bmesh.new()
    for k in range(9):
        aa = CORRUPT_SIDE + rnd.uniform(-0.45, 0.45); rad = Vector((math.cos(aa), math.sin(aa), 0))
        rr = rnd.uniform(0.9, 2.6)
        # éclats qui PERCENT la couronne de pétales vers le haut (visibles vue du dessus)
        prism(bm, rad * rr + Vector((0, 0, 1.35 + 0.1 * rr)), rad * 0.35 + Vector((0, 0, 1)) + Vector((rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25), 0)),
              rnd.uniform(0.9, 1.7), rnd.uniform(0.09, 0.16), 5, 0.4)
    for k in range(5):
        aa = CORRUPT_SIDE + (-0.32, -0.12, 0.05, 0.22, 0.38)[k]; rad = Vector((math.cos(aa), math.sin(aa), 0))
        rr = (2.2, 3.1, 1.6, 2.7, 3.4)[k]
        prism(bm, rad * rr + Vector((0, 0, 0.9)), rad * (0.35 + 0.1 * k) + Vector((0, 0, 1)), (2.6, 2.1, 2.9, 2.3, 1.8)[k], (0.2, 0.17, 0.22, 0.18, 0.15)[k], 6, 0.35)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj('Lotus_Corruption', bm, 'corrupt', smooth=False)

dg = bpy.context.evaluated_depsgraph_get(); tot = 0
for o in col.objects:
    e = o.evaluated_get(dg); m = e.to_mesh(); tot += sum(len(p.vertices) - 2 for p in m.polygons); e.to_mesh_clear()
print('Lotus', len(col.objects), 'objets', tot, 'triangles')
