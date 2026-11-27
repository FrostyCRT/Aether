import bpy, bmesh, math, random
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

# Archonte v2 (2026-11-26) - tout sauf le corps (voir archon_v2_body.py, à lancer avant).
# Être de lumière drapé : robe de voiles ondulants -> traîne de cristal dans la faille, mèches de lumière flottant dans le
# dos, couronne de cristal flottante, halo solaire basculé (lisible vue du dessus), cœur de la Source serti d'argent,
# filigranes d'argent en spirale sur les avant-bras, 2 anneaux d'argent à runes. Corruption VIOLETTE, côté gauche.
# Palette (comme le Dragon et le Cerf) : blanc bleuté, bleu glacier, argent, lumière cyan. Plus AUCUNE plaque d'armure.
OX = 60.0
rnd = random.Random(5)
col = bpy.data.collections['ArchonV2']
for o in list(col.objects):
    if not o.name.startswith('ArcV2_Body'): bpy.data.objects.remove(o, do_unlink=True)
body = bpy.data.objects['ArcV2_Body']

def mat(name, base, metal=0.0, rough=0.4, emit=None, strength=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = base + (1,); b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough
    b.inputs['Emission Color'].default_value = (emit or (0, 0, 0)) + (1,)
    b.inputs['Emission Strength'].default_value = strength if emit else 0.0
    return m
M = {'skin': mat('ArcV2_Skin', (0.88, 0.92, 1.0), rough=0.45),
     'robe': mat('ArcV2_Robe', (0.74, 0.86, 1.0), rough=0.5, emit=(0.5, 0.75, 1.0), strength=0.25),
     'hair': mat('ArcV2_Hair', (0.62, 0.86, 1.0), rough=0.4, emit=(0.45, 0.8, 1.0), strength=0.8),
     'silver': mat('ArcV2_Silver', (0.86, 0.89, 0.94), metal=1.0, rough=0.25),
     'crystal': mat('ArcV2_Crystal', (0.6, 0.85, 1.0), rough=0.1, emit=(0.45, 0.8, 1.0), strength=0.6),
     'corrupt': mat('ArcV2_Corrupt', (0.25, 0.12, 0.45), rough=0.2, emit=(0.5, 0.2, 0.9), strength=0.5),
     'crack': mat('ArcV2_Crack', (0.75, 0.45, 1.0), emit=(0.7, 0.35, 1.0), strength=5.0),
     'glow': mat('ArcV2_Glow', (0.8, 0.95, 1.0), emit=(0.6, 0.9, 1.0), strength=6.0)}
body.data.materials.clear(); body.data.materials.append(M['skin'])

OBJS = []
def obj(name, bm, m, smooth=True, sub=0):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = smooth
    me.materials.append(M[m])
    o = bpy.data.objects.new(name, me); col.objects.link(o); o.location = (OX, 0, 0)
    if sub:
        md = o.modifiers.new('Sub', 'SUBSURF'); md.levels = sub; md.render_levels = sub
    OBJS.append(o); return o

def spline(P, per=8):
    """Catmull-Rom à travers les points P."""
    P = [Vector(p) for p in P]; E = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]; out = []
    for i in range(1, len(E) - 2):
        a, b, c, d = E[i - 1], E[i], E[i + 1], E[i + 2]
        for k in range(per):
            t = k / per; t2 = t * t; t3 = t2 * t
            out.append(0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3))
    out.append(P[-1]); return out

def ribbon(bm, P, width, side_hint, thick=0.03, folds=0.0, fold_amp=0.0, ns=10, per=8, twist=0.0):
    """Ruban/voile le long d'une courbe : width(t) = demi-largeur ; side_hint(t) = direction de la largeur ;
    plis : décalage sinusoïdal en travers (voiles). Deux faces + bords pincés (forme fermée et fine)."""
    C = spline(P, per); n = len(C); top = []; bot = []
    for i, p in enumerate(C):
        t = i / (n - 1)
        tg = (C[min(i + 1, n - 1)] - C[max(i - 1, 0)]).normalized()
        sd = Vector(side_hint(t)); sd = (sd - tg * sd.dot(tg)).normalized()
        if twist: sd = Matrix.Rotation(twist * t, 3, tg) @ sd
        nr = tg.cross(sd).normalized()
        w = width(t); rt, rb = [], []
        for j in range(ns + 1):
            s = -1 + 2 * j / ns
            off = sd * s * w + nr * (fold_amp * w * math.sin(math.pi * folds * s) * min(1.0, t * 4))
            th = thick * (1 - s * s) * (1 - 0.7 * t)
            rt.append(bm.verts.new(p + off + nr * th)); rb.append(bm.verts.new(p + off - nr * th))
        top.append(rt); bot.append(rb)
    for i in range(n - 1):
        for j in range(ns):
            bm.faces.new((top[i][j], top[i][j + 1], top[i + 1][j + 1], top[i + 1][j]))
            bm.faces.new((bot[i][j], bot[i + 1][j], bot[i + 1][j + 1], bot[i][j + 1]))
        for j in (0, ns):   # bords
            if j == 0: bm.faces.new((top[i][0], top[i + 1][0], bot[i + 1][0], bot[i][0]))
            else: bm.faces.new((top[i][ns], bot[i][ns], bot[i + 1][ns], top[i + 1][ns]))
    bm.faces.new([top[0][j] for j in range(ns + 1)] + [bot[0][j] for j in range(ns, -1, -1)])
    bm.faces.new([top[-1][j] for j in range(ns, -1, -1)] + [bot[-1][j] for j in range(ns + 1)])

def tube(bm, P, r0, r1, sides=12, per=6):
    C = spline(P, per) if len(P) > 2 else [Vector(P[0]).lerp(Vector(P[1]), k / 8) for k in range(9)]
    n = len(C); prev = None
    for i, p in enumerate(C):
        d = (C[min(i + 1, n - 1)] - C[max(i - 1, 0)]).normalized(); s = d.orthogonal().normalized(); u = d.cross(s)
        r = r0 + (r1 - r0) * i / (n - 1)
        cur = [bm.verts.new(p + (s * math.cos(2 * math.pi * k / sides) + u * math.sin(2 * math.pi * k / sides)) * r) for k in range(sides)]
        if prev:
            for k in range(sides): bm.faces.new((prev[k], prev[(k + 1) % sides], cur[(k + 1) % sides], cur[k]))
        else: bm.faces.new(list(reversed(cur)))
        prev = cur
    bm.faces.new(prev)

def prism(bm, base, d, length, radius, sides=6, tip=0.3):
    d = Vector(d).normalized(); s = d.orthogonal().normalized(); u = d.cross(s).normalized(); base = Vector(base); tw = rnd.random() * 6.28
    def P(t, r): return [base + d * length * t + (s * math.cos(tw + 2 * math.pi * k / sides) + u * math.sin(tw + 2 * math.pi * k / sides)) * r for k in range(sides)]
    r0 = [bm.verts.new(p) for p in P(0, radius * 0.85)]; r1 = [bm.verts.new(p) for p in P(1 - tip, radius)]
    t = bm.verts.new(base + d * length); h = bm.verts.new(base - d * radius * 0.4)
    for k in range(sides):
        k2 = (k + 1) % sides
        bm.faces.new((r0[k], r0[k2], r1[k2], r1[k])); bm.faces.new((r1[k], r1[k2], t)); bm.faces.new((r0[k2], r0[k], h))
def shards(name, parts, m):
    bm = bmesh.new()
    for p in parts: prism(bm, *p)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); return obj(name, bm, m, smooth=False)

def torus(name, c, R, r, m, rot=Matrix(), seg=128, sides=12, scale=(1, 1), flat=1.0):
    bm = bmesh.new(); rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        rings.append([bm.verts.new(((R + math.cos(2 * math.pi * j / sides) * r) * math.cos(a) * scale[0],
                                     (R + math.cos(2 * math.pi * j / sides) * r) * math.sin(a) * scale[1],
                                     math.sin(2 * math.pi * j / sides) * r * flat)) for j in range(sides)])
    for i in range(seg):
        A, B = rings[i], rings[(i + 1) % seg]
        for j in range(sides): bm.faces.new((A[j], A[(j + 1) % sides], B[(j + 1) % sides], B[j]))
    bmesh.ops.transform(bm, matrix=Matrix.Translation(c) @ rot.to_4x4(), verts=bm.verts)
    return obj(name, bm, m)

# surface du corps (pour poser fêlures et filigranes dessus)
dg = bpy.context.evaluated_depsgraph_get()
bvh = BVHTree.FromObject(body, dg)
def on_body(p, toward, lift=0.012):
    """Projette p sur le corps en visant 'toward' (point intérieur) ; renvoie le point de surface légèrement décollé."""
    p = Vector(p); d = (Vector(toward) - p).normalized()
    hit, nrm, _, _ = bvh.ray_cast(p + Vector((OX, 0, 0)), d)
    if hit is None: return p
    return hit - Vector((OX, 0, 0)) + nrm * lift

# ================= ROBE DE LUMIÈRE : 10 voiles ondulants autour des hanches, qui se resserrent vers la traîne =========
import json
for k in range(10):
    bm = bmesh.new()
    a = 2 * math.pi * k / 10 + 0.3 + 0.08 * rnd.random()
    rad = Vector((math.cos(a), math.sin(a) * 0.85, 0)); tan = Vector((-math.sin(a), math.cos(a), 0))
    front = math.sin(a) < -0.3
    L = 0.85 if front else 1.0
    P = [Vector((0, 0, 4.25)) + rad * 0.4, Vector((0, 0, 3.55)) + rad * 0.72, Vector((0, 0, 3.55 - 1.0 * L)) + rad * 0.9,
         Vector((0, 0, 3.55 - 1.9 * L)) + rad * 0.62 + tan * 0.15, Vector((0, 0, 3.55 - 2.6 * L)) + rad * 0.22 + tan * 0.25]
    ribbon(bm, P, lambda t: 0.3 + 0.32 * math.sin(math.pi * min(1, t * 1.6)) * (1 - t) + 0.02, lambda t, tan=tan: tan,
           thick=0.025, folds=2.0, fold_amp=0.18, ns=12, per=8, twist=0.25)
    o = obj('ArcV2_Robe_%d' % k, bm, 'robe', sub=1); o['pts'] = json.dumps([list(p) for p in P]); o['tan'] = list(tan)
# ceinture : cordon d'argent + 2 pans qui pendent devant
bm = bmesh.new()
tube(bm, [Vector((math.cos(2 * math.pi * k / 24) * 0.47, math.sin(2 * math.pi * k / 24) * 0.4, 4.28 + 0.03 * math.sin(4 * math.pi * k / 24))) for k in range(25)], 0.03, 0.03, 12, 3)
obj('ArcV2_Sash', bm, 'silver')

# ================= MÈCHES DE LUMIÈRE : 8 rubans qui naissent derrière la tête et flottent en éventail dans le dos ======
for j in range(7):
    bm = bmesh.new()
    x = -1 + 2 * j / 6; sx = 1 if x >= 0 else -1
    o = Vector((x * 0.22, 0.24, 7.12 - 0.12 * abs(x)))
    sp = x * (0.42 + 0.1 * rnd.random()); ph = rnd.random() * 6.28
    P = [o, o + Vector((sp * 0.35, 0.45, -0.25)), o + Vector((sp * 0.8 + 0.08 * math.sin(ph), 0.95, -0.95)),
         o + Vector((sp * 1.15 - 0.1 * math.sin(ph), 1.3, -1.8)), o + Vector((sp * 1.35 + 0.12 * math.sin(ph), 1.5, -2.7)),
         o + Vector((sp * 1.5, 1.55, -3.4 - 0.3 * rnd.random()))]
    ribbon(bm, P, lambda t: 0.12 + 0.17 * math.sin(math.pi * min(1, t * 1.25)) * (1 - 0.8 * t), lambda t, x=x: (1, -0.15 * x, 0),
           thick=0.025, folds=1.5, fold_amp=0.22, ns=8, per=10, twist=0.9 * x + 0.3 * math.sin(ph))
    o = obj('ArcV2_Hair_%d' % j, bm, 'hair', sub=1); o['pts'] = json.dumps([list(p) for p in P])

# ================= TÊTE : yeux fendus lumineux, diadème d'argent, couronne de cristal flottante ==========================
bm = bmesh.new()
for sx in (-1, 1):
    a = on_body((sx * 0.13, -0.6, 6.98), (sx * 0.1, 0.2, 6.98), 0.0)
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation(a) @ Matrix.Rotation(sx * math.radians(-12), 4, 'Y') @ Matrix.Diagonal((0.13, 0.03, 0.022, 1)))
obj('ArcV2_Eyes', bm, 'glow', smooth=False)
HCEN = Vector((0, 0.0, 6.95)); HR = Vector((0.40, 0.43, 0.49))
def in_face(x, z): return (x / 0.29) ** 2 + ((z - 6.9) / 0.36) ** 2 < 1.0
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=64, v_segments=40, radius=1.0)
bmesh.ops.transform(bm, matrix=Matrix.Translation(HCEN) @ Matrix.Diagonal(HR.to_4d()), verts=bm.verts)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if (v.co.y < -0.05 and in_face(v.co.x, v.co.z)) or v.co.z < 6.45], context='VERTS')
# le bas du voile tombe sur la nuque et les épaules
for v in bm.verts:
    if v.co.z < 6.75: v.co += Vector((v.co.x * 0.4, 0.05, 0)) * (6.75 - v.co.z) / 0.3
bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.03)
obj('ArcV2_Hood', bm, 'hair', sub=1)
bm = bmesh.new(); rim = []
for k in range(49):
    t = 2 * math.pi * k / 48; x = 0.3 * math.cos(t); z = 6.9 + 0.37 * math.sin(t)
    q = 1 - (x / HR.x) ** 2 - ((z - HCEN.z) / HR.z) ** 2
    rim.append(Vector((x, -math.sqrt(max(q, 0.0)) * HR.y - 0.012, z)))
tube(bm, rim, 0.024, 0.024, 10, 2)
tube(bm, [(0, -0.43, 7.27), (0, -0.4, 7.36), (0, -0.33, 7.43)], 0.02, 0.01, 8, 6)
obj('ArcV2_Diadem', bm, 'silver')
# (couronne flottante retirée le 2026-11-26 : lisibilité)

# ================= HALO SOLAIRE (basculé de 65° vers l'arrière : soleil autour de la tête vu du dessus) =================
HC = Vector((0, 0.55, 7.1)); TILT = Matrix.Rotation(math.radians(90 - 65), 3, 'X')
torus('ArcV2_HaloRing', HC, 1.0, 0.045, 'silver', rot=TILT)
pure, dark = [], []
for i in range(12):
    a = 2 * math.pi * i / 12 + math.pi / 12
    d = TILT @ Vector((math.cos(a), math.sin(a), 0))
    L = (0.32 + 0.22 * (i % 2 == 0)) * (1.0 + 0.3 * max(0.0, math.sin(a)))
    item = (HC + d * 1.02, d, L if not (2.4 < a < 3.6) else L * 0.55, 0.06, 5, 0.45)
    (dark if 2.4 < a < 3.6 else pure).append(item)
shards('ArcV2_HaloCrystals', pure, 'crystal'); shards('ArcV2_HaloCorrupt', dark, 'corrupt')

# ================= CŒUR DE LA SOURCE : gemme dans le sternum, sertie de filigrane d'argent ============================
hc = on_body((0, -0.9, 5.25), (0, 0, 5.25), 0.02)
bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.17)
bmesh.ops.transform(bm, matrix=Matrix.Translation(hc) @ Matrix.Diagonal((0.85, 0.7, 1.2, 1)), verts=bm.verts)
obj('ArcV2_Heart', bm, 'glow', smooth=False)
bm = bmesh.new()
for k in range(6):   # 6 filets d'argent qui rayonnent du cœur en suivant le torse
    a = math.pi / 2 + 2 * math.pi * k / 6
    path = []
    for r in (0.17, 0.27, 0.37, 0.46):
        x = math.cos(a) * r * 1.15; z = hc.z + math.sin(a) * r
        path.append(on_body((x, -1.5, z), (x, 0.5, z), 0.012))
    tube(bm, path, 0.02, 0.006, 8, 6)
torus_bm = None
obj('ArcV2_HeartSilver', bm, 'silver')

# ================= BRACELETS D'ARGENT (poignet + haut de l'avant-bras), fins et collés au bras ==========================
bm = bmesh.new()
for sx in (-1, 1):
    el_ = Vector((sx * 1.42, -0.12, 4.85)); wr = Vector((sx * 1.72, -0.42, 4.0)); fa = (wr - el_).normalized()
    s_ = fa.orthogonal().normalized(); u = fa.cross(s_)
    for f, r in ((0.97, 0.024), (0.9, 0.014), (0.28, 0.014)):
        c = el_.lerp(wr, f)
        ar = (0.2 + (0.155 - 0.2) * f) * 1.12 + 0.012          # rayon du bras à cet endroit (métaballes), + marge
        ring = [c + (s_ * math.cos(2 * math.pi * k / 32) + u * math.sin(2 * math.pi * k / 32)) * ar for k in range(33)]
        tube(bm, ring, r, r, 8, 2)
obj('ArcV2_ArmSilver', bm, 'silver')

# ================= TRAÎNE DE CRISTAL (au bout de la robe) + fragments en suspension ===================================
tail = [((0, 0, 1.75), (0, 0, -1), 1.3, 0.26, 7, 0.5)]
for k in range(6):
    a = 2 * math.pi * k / 6 + 0.4
    tail.append(((math.cos(a) * 0.2, math.sin(a) * 0.18, 1.85), (math.cos(a) * 0.4, math.sin(a) * 0.35, -1), 0.7 + 0.4 * rnd.random(), 0.11, 6, 0.45))
shards('ArcV2_Tail', tail, 'crystal')
shards('ArcV2_Fragments', [((math.cos(a) * 0.4, math.sin(a) * 0.4, 0.4 + 0.18 * i), (0.1 * math.cos(a), 0.1 * math.sin(a), -1), 0.26 - 0.02 * i, 0.06, 5, 0.5)
                           for i, a in enumerate([0.4, 2.2, 3.9, 5.3, 1.3])], 'crystal')

# (anneaux à la taille retirés le 2026-11-26 : vu du dessus, ils brouillaient la silhouette)

# ================= CORRUPTION (côté gauche) : éclats violets à l'épaule et dans le dos, fêlures violettes ============
sh = on_body((-1.2, 0.1, 6.0), (-0.6, 0, 5.8), 0.0)
shards('ArcV2_Corruption', [(sh + Vector((0.05 * k, 0.12 * (k % 2), 0.05 * k)), (-0.5 - 0.25 * k, 0.4 + 0.2 * (k % 2), 1 - 0.2 * k), 0.75 - 0.12 * k, 0.09 - 0.012 * k, 5, 0.4) for k in range(4)]
                         + [(on_body((-0.6, 0.8, 5.4), (0, 0, 5.4), 0.0), (-0.6, 1, 0.3), 0.55, 0.07, 5, 0.4),
                            (on_body((-0.45, 0.8, 5.1), (0, 0, 5.1), 0.0), (-0.4, 1, -0.1), 0.4, 0.06, 5, 0.4)], 'corrupt')
bm = bmesh.new()
def crack(x0, z0, steps, dx, dz):
    pts = []; x, z = x0, z0
    for k in range(steps):
        pts.append(on_body((x, -1.5, z), (x, 0.5, z), 0.01))
        x += dx * (0.5 + rnd.random()) * (1 if k % 2 == 0 else -0.6); z += dz * (0.7 + 0.6 * rnd.random())
    tube(bm, pts, 0.016, 0.004, 6, 4)
crack(-0.5, 5.8, 7, 0.05, -0.13); crack(-0.4, 5.55, 5, 0.06, -0.11); crack(-0.55, 5.3, 5, 0.04, -0.12); crack(-0.3, 5.85, 4, -0.05, -0.1)
obj('ArcV2_Cracks', bm, 'crack')

dg = bpy.context.evaluated_depsgraph_get(); tot = 0
for o in col.objects:
    e = o.evaluated_get(dg); m = e.to_mesh(); tot += sum(len(p.vertices) - 2 for p in m.polygons); e.to_mesh_clear()
print('ArchonV2', len(col.objects), 'objets', tot, 'triangles')
