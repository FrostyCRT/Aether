import bpy, bmesh, math, random
from mathutils import Vector, Matrix

# Boss 3 v3 : L'ARCHONTE DE LA SOURCE (2026-11-26) - modèle détaillé.
# La Source de mana devenue consciente : colosse de porcelaine nacrée et d'or en lévitation au-dessus de la faille.
#
# LANGAGE DE FORMES (pour ne pas surcharger) :
#  - DIVIN = courbes lisses + rayonnement : porcelaine (masse principale), or (filets, bordures), lumière cyan (yeux,
#    poignets, paumes, runes, cœur). Le cristal pur n'est QUE dans le halo, le diadème, le cœur et la traîne.
#  - CORRUPTION = angles cassés, asymétrique (côté GAUCHE seulement) : éclats indigo + fêlures bordeaux lumineuses.
#  - Hiérarchie : grands volumes (torse, épaulières, halo, anneaux) > moyens (bras, mains, manteau, jupe, traîne)
#    > petits (filets d'or, larmes d'or, runes, fêlures).
# Face vers -Y, haut +Z, en mètres (~9,5 m). Collection "Archon", posée en X = 40.
rnd = random.Random(7)
OX = 40.0

col = bpy.data.collections.get('Archon')
if col is None:
    col = bpy.data.collections.new('Archon'); bpy.context.scene.collection.children.link(col)
for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)

def mat(name, base, metal=0.0, rough=0.4, emit=None, strength=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = base + (1,)
    b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough
    b.inputs['Emission Color'].default_value = (emit or (0, 0, 0)) + (1,)
    b.inputs['Emission Strength'].default_value = strength if emit else 0.0
    return m

M = {
    'porcelain': mat('Arc_Porcelain', (0.86, 0.85, 0.82), rough=0.32),
    'gold': mat('Arc_Gold', (0.86, 0.62, 0.24), metal=1.0, rough=0.28),
    'crystal': mat('Arc_Crystal', (0.55, 0.82, 1.0), rough=0.12, emit=(0.4, 0.8, 1.0), strength=0.6),
    'corrupt': mat('Arc_Corrupt', (0.10, 0.07, 0.22), rough=0.2, emit=(0.45, 0.06, 0.2), strength=0.4),
    'crack': mat('Arc_Crack', (0.9, 0.2, 0.45), emit=(0.85, 0.12, 0.4), strength=5.0),
    'heart': mat('Arc_Heart', (0.8, 0.95, 1.0), emit=(0.6, 0.9, 1.0), strength=9.0),
    'glow': mat('Arc_Glow', (0.8, 0.95, 1.0), emit=(0.6, 0.9, 1.0), strength=6.0),
}

OBJS = []
def obj(name, bm, m, smooth=True, sub=0):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = smooth
    me.materials.append(M[m])
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    o.location.x += OX
    if sub:
        md = o.modifiers.new('Sub', 'SUBSURF'); md.levels = sub; md.render_levels = sub
    OBJS.append(o)
    return o

def join(name, objs):
    """Réunit plusieurs pièces (matériaux conservés) en un seul objet."""
    with bpy.context.temp_override(active_object=objs[0], selected_editable_objects=objs, object=objs[0]):
        bpy.ops.object.join()
    objs[0].name = name; objs[0].data.name = name
    return objs[0]

def loft(rings, sides=24, cap=True, bm=None):
    """rings = [(z, rx, ry, cx, cy)]."""
    bm = bm or bmesh.new(); prev = None; first = None; sides *= 2
    for (z, rx, ry, cx, cy) in rings:
        cur = [bm.verts.new((cx + math.cos(2 * math.pi * k / sides) * rx, cy + math.sin(2 * math.pi * k / sides) * ry, z)) for k in range(sides)]
        if prev:
            for k in range(sides):
                k2 = (k + 1) % sides; bm.faces.new((prev[k], prev[k2], cur[k2], cur[k]))
        else: first = cur
        prev = cur
    if cap:
        bm.faces.new(list(reversed(first))); bm.faces.new(prev)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm

def prism(bm, base, d, length, radius, sides=6, tip=0.3, flat=1.0):
    d = Vector(d).normalized(); s = d.orthogonal().normalized(); u = d.cross(s).normalized()
    base = Vector(base); tw = rnd.random() * 6.28
    def P(t, r): return [base + d * length * t + (s * math.cos(tw + 2 * math.pi * k / sides) + u * math.sin(tw + 2 * math.pi * k / sides) * flat) * r for k in range(sides)]
    r0 = [bm.verts.new(p) for p in P(0, radius * 0.85)]; r1 = [bm.verts.new(p) for p in P(1 - tip, radius)]
    t = bm.verts.new(base + d * length); h = bm.verts.new(base - d * radius * 0.4)
    for k in range(sides):
        k2 = (k + 1) % sides
        bm.faces.new((r0[k], r0[k2], r1[k2], r1[k])); bm.faces.new((r1[k], r1[k2], t)); bm.faces.new((r0[k2], r0[k], h))

def shards(name, parts, m):
    bm = bmesh.new()
    for p in parts: prism(bm, *p)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj(name, bm, m, smooth=False)

def rotm(rot): return Matrix.Rotation(rot[2], 4, 'Z') @ Matrix.Rotation(rot[1], 4, 'Y') @ Matrix.Rotation(rot[0], 4, 'X')

def ellipsoid(name, c, r, m, rot=(0, 0, 0), seg=28, cut=None):
    bm = bmesh.new(); seg = max(36, int(seg * 1.4))
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg // 2, radius=1.0)
    if cut is not None:   # coque : on ne garde que z >= cut (avant mise à l'échelle)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < cut - 1e-4], context='VERTS')
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.06)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(c) @ rotm(rot) @ Matrix.Diagonal(Vector(r).to_4d()), verts=bm.verts)
    return obj(name, bm, m, sub=1)

def torus(name, c, R, r, m, rot=(0, 0, 0), flat=1.0, seg=64, scale=(1, 1), arc=1.0, sides=8):
    seg = int(seg * 1.6); sides = max(sides, 12)
    bm = bmesh.new(); rings = []; n = seg if arc >= 1.0 else seg + 1
    for i in range(n):
        a = 2 * math.pi * arc * i / seg; ring = []
        for j in range(sides):
            b = 2 * math.pi * j / sides
            ring.append(bm.verts.new(((R + math.cos(b) * r) * math.cos(a) * scale[0], (R + math.cos(b) * r) * math.sin(a) * scale[1], math.sin(b) * r * flat)))
        rings.append(ring)
    for i in range(seg if arc >= 1.0 else seg):
        A, B = rings[i], rings[(i + 1) % n]
        for j in range(sides):
            j2 = (j + 1) % sides; bm.faces.new((A[j], A[j2], B[j2], B[j]))
    if arc < 1.0:
        bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
    bmesh.ops.transform(bm, matrix=Matrix.Translation(c) @ rotm(rot), verts=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj(name, bm, m)

def tube(bm, pts, r0, r1, sides=8):
    """Tube effilé le long d'une polyligne lissée (Catmull-Rom) : doigts, filets d'or, fêlures."""
    P0 = [Vector(p) for p in pts]; sides = max(sides, 12)
    if len(P0) >= 2:
        E = [P0[0] * 2 - P0[1]] + P0 + [P0[-1] * 2 - P0[-2]]; pts = []
        for i in range(1, len(E) - 2):
            a, b, c, d = E[i - 1], E[i], E[i + 1], E[i + 2]
            steps = max(1, min(6, int((c - b).length / 0.05)))
            for k in range(steps):
                t = k / steps; t2 = t * t; t3 = t2 * t
                pts.append(0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3))
        pts.append(P0[-1])
    pts = [Vector(p) for p in pts]; prev = None; n = len(pts)
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        s = d.orthogonal().normalized(); u = d.cross(s)
        r = r0 + (r1 - r0) * i / (n - 1)
        cur = [bm.verts.new(p + (s * math.cos(2 * math.pi * k / sides) + u * math.sin(2 * math.pi * k / sides)) * r) for k in range(sides)]
        if prev:
            for k in range(sides):
                k2 = (k + 1) % sides; bm.faces.new((prev[k], prev[k2], cur[k2], cur[k]))
        else: bm.faces.new(list(reversed(cur)))
        prev = cur
    tip = bm.verts.new(pts[-1] + (pts[-1] - pts[-2]).normalized() * r1)
    for k in range(sides): bm.faces.new((prev[k], prev[(k + 1) % sides], tip))

# =====================================================================================================================
# TORSE : taille fine, poitrine large en V, nuque
TORSO = [(3.3, 0.30, 0.26, 0, 0), (3.7, 0.46, 0.38, 0, 0), (4.3, 0.52, 0.40, 0, 0), (4.85, 0.78, 0.50, 0, -0.04),
         (5.45, 1.22, 0.72, 0, -0.06), (5.9, 1.22, 0.68, 0, -0.02), (6.2, 0.82, 0.50, 0, 0.02), (6.42, 0.34, 0.30, 0, 0.04),
         (6.72, 0.26, 0.24, 0, 0.02)]

def torso_at(z):
    for (z0, a0, b0, _, y0), (z1, a1, b1, _, y1) in zip(TORSO, TORSO[1:]):
        if z0 <= z <= z1:
            t = (z - z0) / (z1 - z0); return a0 + (a1 - a0) * t, b0 + (b1 - b0) * t, y0 + (y1 - y0) * t
    return TORSO[-1][1], TORSO[-1][2], TORSO[-1][4]

def on_torso(z, ang_deg, off=0.035):
    """Point à la surface du torse ; ang -90 = face avant (-Y), 0 = flanc droit (+X), 180 = flanc gauche."""
    rx, ry, cy = torso_at(z); a = math.radians(ang_deg)
    return Vector((math.cos(a) * (rx + off), cy + math.sin(a) * (ry + off), z))

def smooth_rings(rings, k=4):
    E = [rings[0]] + rings + [rings[-1]]; out = []
    for i in range(1, len(E) - 2):
        for j in range(k):
            t = j / k; t2 = t * t; t3 = t2 * t
            out.append(tuple(0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3)
                             for a, b, c, d in zip(E[i - 1], E[i], E[i + 1], E[i + 2])))
    out.append(rings[-1]); return out
obj('Arc_Torso', loft(smooth_rings(TORSO), 32), 'porcelain', sub=1)

# Plastron : coque avant (moitié avant d'un torse un peu plus large) bordée d'or
def chest_plate():
    bm = bmesh.new(); zs = [4.75, 5.0, 5.3, 5.6, 5.9, 6.12]; arcs = 14; prev = None
    for z in zs:
        rx, ry, cy = torso_at(z); cur = []
        for k in range(arcs + 1):
            a = math.radians(-160 + 140 * k / arcs)
            cur.append(bm.verts.new((math.cos(a) * (rx + 0.05), cy + math.sin(a) * (ry + 0.06), z)))
        if prev:
            for k in range(arcs): bm.faces.new((prev[k], prev[k + 1], cur[k + 1], cur[k]))
        prev = cur
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=-0.05)
    return bm
obj('Arc_ChestPlate', chest_plate(), 'porcelain', sub=1)
bm = bmesh.new()
for z in (4.75, 6.12):                                             # bordures haute et basse
    tube(bm, [on_torso(z, -160 + 140 * k / 20, 0.10) for k in range(21)], 0.03, 0.03, 6)
for side in (-1, 1):                                               # filets en V vers le cœur
    tube(bm, [on_torso(6.05 - 0.2 * k, -90 + side * (55 - 11 * k), 0.13) for k in range(5)], 0.028, 0.02, 6)
    tube(bm, [on_torso(4.8 + 0.12 * k, -90 + side * (50 - 9 * k), 0.13) for k in range(4)], 0.025, 0.015, 6)
obj('Arc_ChestGold', bm, 'gold')

# Abdomen : 3 bandes dorées qui suivent l'ellipse
bm = bmesh.new()
for z in (4.15, 4.42, 4.68):
    rx, ry, cy = torso_at(z)
    loft([(z - 0.04, rx + 0.03, ry + 0.03, 0, cy), (z, rx + 0.055, ry + 0.055, 0, cy), (z + 0.04, rx + 0.03, ry + 0.03, 0, cy)], 32, bm=bm)
obj('Arc_AbsBands', bm, 'gold')
torus('Arc_Collar', (0, 0.03, 6.36), 0.40, 0.07, 'gold', flat=0.7)
torus('Arc_Belt', (0, 0, 3.78), 0.53, 0.09, 'gold', flat=0.9)

# CŒUR DE LA SOURCE : gemme facettée sertie d'or, 4 griffes
bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.3)
bmesh.ops.transform(bm, matrix=Matrix.Translation((0, -0.74, 5.2)) @ Matrix.Diagonal((1, 0.8, 1.25, 1)), verts=bm.verts)
obj('Arc_Heart', bm, 'heart', smooth=False)
torus('Arc_HeartFrame', (0, -0.72, 5.2), 0.38, 0.055, 'gold', rot=(math.radians(90), 0, 0), scale=(1, 1.25))
bm = bmesh.new()
for k in range(4):
    a = math.radians(45 + 90 * k); p = Vector((math.cos(a) * 0.38, -0.74, 5.2 + math.sin(a) * 0.47))
    tube(bm, [p, p + Vector((-math.cos(a) * 0.1, -0.09, -math.sin(a) * 0.12))], 0.04, 0.012, 6)
obj('Arc_HeartClaws', bm, 'gold')

# =====================================================================================================================
# TÊTE : masque de statue, larmes d'or, diadème + crête de cristal, ailettes vers l'arrière
ellipsoid('Arc_Head', (0, -0.02, 7.02), (0.32, 0.33, 0.43), 'porcelain')
bm = bmesh.new()
prism(bm, (0, -0.31, 7.12), (0, -0.25, -1), 0.2, 0.032, sides=4, tip=0.2)
obj('Arc_Nose', bm, 'porcelain', smooth=False)
bm = bmesh.new()
for sx in (-1, 1):
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((sx * 0.12, -0.29, 7.07)) @ Matrix.Rotation(sx * math.radians(-14), 4, 'Y') @ Matrix.Diagonal((0.15, 0.04, 0.025, 1)))
obj('Arc_Eyes', bm, 'glow', smooth=False)
bm = bmesh.new()
for sx in (-1, 1):   # larmes d'or : sous chaque œil, descendent sur la joue
    tube(bm, [(sx * 0.13, -0.29, 7.03), (sx * 0.14, -0.285, 6.92), (sx * 0.15, -0.26, 6.8), (sx * 0.155, -0.22, 6.72)], 0.014, 0.006, 6)
obj('Arc_GoldTears', bm, 'gold')
torus('Arc_Diadem', (0, 0.0, 7.25), 0.315, 0.035, 'gold', rot=(math.radians(-14), 0, 0), flat=1.8)
shards('Arc_DiademCrystals', [((0, -0.28, 7.33), (0, -0.15, 1), 0.85, 0.07, 5, 0.4),
                              ((0.14, -0.24, 7.30), (0.35, -0.1, 1), 0.48, 0.05, 5, 0.4),
                              ((-0.14, -0.24, 7.30), (-0.35, -0.1, 1), 0.48, 0.05, 5, 0.4)], 'crystal')
# crête unique de porcelaine sur l'axe du crâne, balayée vers l'arrière (casque de statue), filet d'or
ellipsoid('Arc_HeadCrest', (0, 0.12, 7.3), (0.04, 0.4, 0.16), 'porcelain', rot=(math.radians(-22), 0, 0))
bm = bmesh.new(); tube(bm, [(0, -0.2, 7.36), (0, 0.05, 7.45), (0, 0.3, 7.38), (0, 0.5, 7.2)], 0.018, 0.008, 6)
obj('Arc_HeadCrestGold', bm, 'gold')

# COURONNE-HALO derrière la tête (plan XZ) : 2 cercles d'or, rayons de cristal alternés, glyphes lumineux ;
# côté gauche brisé par la corruption.
HC = Vector((0, 0.5, 7.15))
torus('Arc_HaloRing', HC, 1.2, 0.055, 'gold', rot=(math.radians(90), 0, 0))
torus('Arc_HaloRing2', HC + Vector((0, 0.05, 0)), 1.44, 0.03, 'gold', rot=(math.radians(90), 0, 0))
bm = bmesh.new()
for i in range(24):
    a = 2 * math.pi * i / 24
    if math.cos(a) < -0.55 and math.sin(a) < 0.5: continue                 # glyphes éteints côté corrompu
    c = HC + Vector((math.cos(a) * 1.32, 0.04, math.sin(a) * 1.32))
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation(c) @ Matrix.Rotation(-a, 4, 'Y') @ Matrix.Diagonal((0.05, 0.03, 0.12, 1)))
obj('Arc_HaloGlyphs', bm, 'glow', smooth=False)
pure, dark = [], []
N = 19
for i in range(N):
    a = math.radians(-12 + 204 * i / (N - 1))
    d = Vector((math.cos(a), 0, math.sin(a)))
    L = (0.7 + 1.1 * max(0.0, math.sin(a)) ** 2) * (1.0 if i % 2 == 0 else 0.6)
    base = HC + d * 1.2
    if i >= N - 5: dark.append((base, d + Vector((0, 0, -0.35 * rnd.random())), L * (0.4 + 0.35 * rnd.random()), 0.09, 5, 0.45))
    else: pure.append((base, d, L, 0.09, 5, 0.45))
shards('Arc_HaloCrystals', pure, 'crystal')
shards('Arc_HaloCorrupt', dark, 'corrupt')
# AJOUTÉ (essai en jeu) - la caméra du jeu regarde presque à la verticale : un halo vertical n'y était qu'un trait.
# Basculé de 65° vers l'arrière (le haut part derrière la tête) : vu d'en haut, il forme un soleil autour de la tête.
HALO_TILT = Matrix.Translation(HC) @ Matrix.Rotation(math.radians(-65), 4, 'X') @ Matrix.Translation(-HC)
for o in OBJS:
    if o.name.startswith('Arc_Halo'): o.data.transform(HALO_TILT)

# =====================================================================================================================
# ÉPAULIÈRES à 2 étages (coques bordées d'or), gemme cyan sur la droite ; épaule gauche envahie
for sx, nm in ((1, 'R'), (-1, 'L')):
    ellipsoid('Arc_Pauldron' + nm, (sx * 1.3, 0.0, 5.95), (0.66, 0.58, 0.42), 'porcelain', rot=(0, sx * math.radians(-28), 0), cut=-0.15)
    torus('Arc_PauldronRim' + nm, (sx * 1.34, 0.0, 5.88), 0.59, 0.045, 'gold', rot=(0, sx * math.radians(-28), 0), scale=(1.05, 0.95))
    ellipsoid('Arc_PauldronTop' + nm, (sx * 1.12, 0.02, 6.3), (0.44, 0.42, 0.24), 'porcelain', rot=(0, sx * math.radians(-18), 0), cut=-0.1)
    torus('Arc_PauldronTopRim' + nm, (sx * 1.13, 0.02, 6.27), 0.4, 0.032, 'gold', rot=(0, sx * math.radians(-18), 0))
bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.1)
bmesh.ops.translate(bm, vec=(1.62, -0.28, 6.02), verts=bm.verts)
obj('Arc_PauldronGem', bm, 'glow', smooth=False)
shards('Arc_Corruption',
       [((-1.25 - 0.2 * k, 0.12 - 0.22 * (k % 2), 6.25 - 0.06 * k), (-0.4 - 0.3 * k, 0.25 * (k % 2) - 0.1, 1 - 0.18 * k), 1.0 - 0.18 * k, 0.12 - 0.015 * k, 5, 0.4) for k in range(4)]
       + [(on_torso(4.75, 195, 0.0), (-1, -0.3, 0.35), 0.55, 0.08, 5, 0.4), (on_torso(4.45, 205, 0.0), (-1, -0.5, -0.1), 0.4, 0.06, 5, 0.4)], 'corrupt')
# Fêlures lumineuses : partent des éclats et se ramifient sur le flanc gauche et l'épaulière gauche
bm = bmesh.new()
def crack(z0, a0, steps, dz, da, r=0.016):
    pts = []; z, a = z0, a0
    for k in range(steps):
        pts.append(on_torso(z, a, 0.045)); z += dz * (0.7 + 0.6 * rnd.random()); a += da * (0.4 + 1.2 * rnd.random()) * (1 if k % 2 else -0.4)
    tube(bm, pts, r, r * 0.3, 5)
crack(4.75, 195, 6, 0.12, -9); crack(4.7, 200, 5, -0.13, -7); crack(5.2, 182, 4, 0.1, -10, 0.012); crack(4.45, 205, 4, -0.11, 6, 0.012)
for k in range(3):   # sur l'épaulière gauche
    a = math.radians(150 + 25 * k); pts = []
    for t in range(4):
        rr = 0.66 - 0.04 * t; pts.append(Vector((-1.3 + math.cos(a) * rr * 0.9 - 0.04 * t, math.sin(a) * rr * 0.5, 5.95 + 0.33 - 0.09 * t)))
        a += 0.18 * (1 if t % 2 else -1)
    tube(bm, pts, 0.014, 0.005, 5)
obj('Arc_Cracks', bm, 'crack')

# =====================================================================================================================
# BRAS épais (épaule -> coude -> poignet) : brassard au biceps, cubitière, canon d'or évasé au poignet ;
# MAINS DÉTACHÉES : paume + doigts à 2 phalanges légèrement fléchis + sceau lumineux dans la paume.
def limb(bm, p0, p1, r0, r1, sides=20, n=8, bulge=0.0):
    sides = max(sides * 2, 24); n = max(n * 3, 6)
    p0, p1 = Vector(p0), Vector(p1); d = p1 - p0
    def R(t): return (r0 + (r1 - r0) * t) * (1 + bulge * math.sin(math.pi * t))
    sub = loft([(d.length * t / n, R(t / n), R(t / n) * 0.92, 0, 0) for t in range(n + 1)], sides)
    bmesh.ops.transform(sub, matrix=Matrix.Translation(p0) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4(), verts=sub.verts)
    me = bpy.data.meshes.new('tmp'); sub.to_mesh(me); sub.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)

def hand(name, wrist, d, n, k=1.55):
    d = Vector(d).normalized(); n = Vector(n).normalized(); n = (n - d * n.dot(d)).normalized(); s = d.cross(n).normalized()
    w = Vector(wrist); bm = bmesh.new()
    m = Matrix((s.to_4d(), d.to_4d(), n.to_4d(), (0, 0, 0, 1))).transposed(); m.translation = w + d * 0.21 * k
    sph = bmesh.new(); bmesh.ops.create_uvsphere(sph, u_segments=28, v_segments=14, radius=1.0)
    bmesh.ops.transform(sph, matrix=m @ Matrix.Diagonal((0.2 * k, 0.24 * k, 0.075 * k, 1)), verts=sph.verts)
    me = bpy.data.meshes.new('tmp'); sph.to_mesh(me); sph.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)
    for i in range(4):
        base = w + d * 0.4 * k + s * (-0.12 + 0.08 * i) * k
        L = (0.24 + 0.05 * (1.5 - abs(i - 1.5))) * k; dd = (d + s * (-0.1 + 0.067 * i)).normalized()
        mid = base + dd * L * 0.55; dd2 = (dd - n * 0.5).normalized()
        tube(bm, [base, mid, mid + dd2 * L * 0.45], 0.042 * k, 0.02 * k, 8)
    tb = w + d * 0.14 * k + s * 0.16 * k; td = (d * 0.6 + s * 1.0).normalized()
    tube(bm, [tb, tb + td * 0.13 * k, tb + (td + d * 0.6 - n * 0.3).normalized() * 0.26 * k], 0.045 * k, 0.022 * k, 8)
    ho = obj(name, bm, 'porcelain', sub=1)
    # sceau lumineux dans le plan de la paume (côté -n)
    seal = torus(name + 'Seal', (0, 0, 0), 0.11 * k, 0.012 * k, 'glow')
    seal.data.transform(Matrix.Translation(w + d * 0.22 * k - n * 0.085 * k) @ n.to_track_quat('Z', 'Y').to_matrix().to_4x4())
    return ho

for sx, nm in ((1, 'R'), (-1, 'L')):
    sh = Vector((sx * 1.28, 0.05, 5.85)); el = Vector((sx * 1.95, -0.2, 4.75)); wr = Vector((sx * 2.35, -0.55, 3.85))
    bm = bmesh.new(); limb(bm, sh, el, 0.32, 0.25, bulge=0.16); limb(bm, el, wr, 0.27, 0.21, bulge=0.12)
    sph = bmesh.new(); bmesh.ops.create_uvsphere(sph, u_segments=48, v_segments=24, radius=0.3)
    bmesh.ops.translate(sph, vec=el, verts=sph.verts)
    me = bpy.data.meshes.new('tmp'); sph.to_mesh(me); sph.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)
    obj('Arc_Arm' + nm, bm, 'porcelain')
    ua = (el - sh).normalized(); fa = (wr - el).normalized()
    bm = bmesh.new()
    limb(bm, sh + ua * 0.62, sh + ua * 0.72, 0.36, 0.35, 20, 2)                       # brassard
    limb(bm, el + fa * 0.42, wr + fa * 0.05, 0.3, 0.29, 20, 4)                         # canon d'avant-bras
    limb(bm, wr - fa * 0.05, wr + fa * 0.06, 0.3, 0.34, 20, 2)                         # évasement du poignet
    obj('Arc_ArmGold' + nm, bm, 'gold')
    bm = bmesh.new(); prism(bm, el - fa * 0.05 + Vector((sx * 0.12, 0.12, 0)), -fa + Vector((sx * 0.3, 0.4, 0)), 0.45, 0.14, 4, 0.5)
    obj('Arc_Elbow' + nm, bm, 'porcelain', smooth=False)
    hw = wr + fa * 0.42                       # main détachée : écart lumineux
    hand('Arc_Hand' + nm, hw, fa + Vector((sx * 0.35, -0.25, 0)), Vector((0, -1, 0.35)))
    torus('Arc_WristGlow' + nm, Vector((0, 0, 0)), 0.2, 0.035, 'glow')
    o = col.objects['Arc_WristGlow' + nm]
    o.data.transform(fa.to_track_quat('Z', 'Y').to_matrix().to_4x4()); o.location = Vector((OX, 0, 0)) + wr + fa * 0.22

# =====================================================================================================================
# MANTEAU : 3 plaques de porcelaine courbes par côté, bordées d'or, déployées en éventail derrière les épaules
# (lisses : contraste avec les rayons du halo). La plaque basse gauche est fendue et envahie d'indigo.
def mantle_frame(sx, k):
    """Plaque k (0 = haute et la plus ouverte) : pivot au dos de l'épaule, axe long d (vers le bas, l'arrière et
    l'extérieur), face plate tournée vers l'arrière. Renvoie (pivot, matrice locale->monde, demi-longueur, demi-largeur)."""
    piv = Vector((sx * (0.6 + 0.12 * k), 0.75, 6.1 - 0.15 * k))
    d = Vector((sx * (0.85 - 0.25 * k), 0.5 + 0.1 * k, -(0.55 + 0.35 * k))).normalized()
    y = (Vector((0, 1, 0)) - d * d.y).normalized(); x = y.cross(d)
    L = 1.55 - 0.2 * k; W = 0.36 - 0.03 * k
    m = Matrix((x.to_4d(), y.to_4d(), d.to_4d(), (0, 0, 0, 1))).transposed(); m.translation = piv + d * L
    return piv, m, L, W

def leaf(bm, base, d, side, nrm, L, W, T, curve=0.08, nt=30, ns=14):
    """Lame effilée (plume de porcelaine) : large près de la base, pointe fine, arête centrale sur la face avant,
    légèrement cintrée. Renvoie le contour (bord droit -> pointe -> bord gauche) pour le filet d'or."""
    d = Vector(d).normalized(); side = Vector(side).normalized(); nrm = Vector(nrm).normalized(); base = Vector(base)
    def width(t): return W * max(0.0, math.sin(math.pi * (0.15 + 0.85 * t))) ** 0.7
    def pt(t, s, top):
        w = width(t); th = T * (1 - s * s) * (w / W) * (1 - 0.55 * t)
        ridge = 0.45 * T * max(0.0, 1 - abs(s) * 2.2) * (1 - t) if top else 0.0
        return base + d * L * t + side * s * w + nrm * (curve * L * math.sin(math.pi * t) + (th / 2 + ridge if top else -th / 2))
    TT = [[bm.verts.new(pt(i / nt, -1 + 2 * j / ns, True)) for j in range(ns + 1)] for i in range(nt + 1)]
    BB = [[bm.verts.new(pt(i / nt, -1 + 2 * j / ns, False)) for j in range(ns + 1)] for i in range(nt + 1)]
    for i in range(nt):
        for j in range(ns):
            bm.faces.new((TT[i][j], TT[i][j + 1], TT[i + 1][j + 1], TT[i + 1][j]))
            bm.faces.new((BB[i][j], BB[i + 1][j], BB[i + 1][j + 1], BB[i][j + 1]))
    bm.faces.new(TT[0] + [BB[0][j] for j in range(ns - 1, 0, -1)])
    edge = [pt(i / nt, 1.0, False) for i in range(nt + 1)] + [pt(i / nt, -1.0, False) for i in range(nt - 1, -1, -1)]
    return edge

def finish_leaves(name, bm, rims, mat_name='porcelain'):
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj(name, bm, mat_name, sub=1)
    gb = bmesh.new()
    for e in rims: tube(gb, e[::2], 0.02, 0.02, 10)
    obj(name + 'Gold', gb, 'gold')

for sx, nm in ((1, 'R'), (-1, 'L')):
    for k in range(3):
        piv, m, L, W = mantle_frame(sx, k)
        bm = bmesh.new()
        e = leaf(bm, piv, m.col[2].to_3d(), m.col[0].to_3d(), m.col[1].to_3d(), 2 * L, W * 1.25, 0.1, curve=0.05)
        finish_leaves('Arc_Mantle%s%d' % (nm, k), bm, [e])
_p, _m, _L, _W = mantle_frame(-1, 2)
_tip = _m.translation + (_m.col[2].to_3d()) * _L * 0.75
shards('Arc_MantleCorrupt', [(_tip, (-0.4, 0.6, -1), 0.75, 0.1, 5, 0.4), (_tip + Vector((0.15, 0.05, 0.4)), (-1, 0.5, -0.2), 0.5, 0.08, 5, 0.4),
                             (_tip + Vector((-0.1, 0.1, -0.3)), (-0.5, 0.3, -1), 0.45, 0.07, 5, 0.4)], 'corrupt')

# =====================================================================================================================
# JUPE DE PLAQUES (sous la ceinture) : 10 pétales de porcelaine à bord d'or, qui s'ouvrent sur la traîne
bm = bmesh.new(); rims = []
for k in range(10):
    a = 2 * math.pi * k / 10 + math.pi / 10; L = 0.62 if math.sin(a) < 0 else 0.8     # plus courts devant (on voit la traîne)
    top = Vector((math.cos(a) * 0.55, math.sin(a) * 0.45, 3.68))
    bot = top + Vector((math.cos(a) * 0.42, math.sin(a) * 0.36, -L * 1.45))
    d = (bot - top).normalized(); sd = Vector((-math.sin(a), math.cos(a), 0)); rad = Vector((math.cos(a), math.sin(a), 0))
    nr = (rad - d * rad.dot(d)).normalized()
    rims.append(leaf(bm, top, d, sd, nr, (bot - top).length * 1.08, 0.26, 0.07, curve=0.04, nt=14, ns=8))
finish_leaves('Arc_Tassets', bm, rims)

# TRAÎNE D'ÉCLATS : plonge dans la faille, fragments en suspension sous la pointe
tail = [((0, 0, 3.4), (0, 0, -1), 2.8, 0.42, 7, 0.5)]
for k in range(7):
    a = 2 * math.pi * k / 7 + 0.3
    tail.append(((math.cos(a) * 0.35, math.sin(a) * 0.3, 3.45), (math.cos(a) * 0.42, math.sin(a) * 0.38, -1), 1.3 + 0.7 * rnd.random(), 0.16, 6, 0.45))
shards('Arc_TailCrystals', tail, 'crystal')
shards('Arc_TailFragments', [((math.cos(a) * 0.45, math.sin(a) * 0.45, 0.45 + 0.22 * i), (0.1 * math.cos(a), 0.1 * math.sin(a), -1), 0.32 - 0.03 * i, 0.08, 5, 0.5)
                              for i, a in enumerate([0.4, 2.2, 3.9, 5.3, 1.3])], 'crystal')

# =====================================================================================================================
# ANNEAUX RUNIQUES : rubans d'or plats, plaques de runes lumineuses régulières
for nm, z, R, tilt, nr in (('Arc_RingInner', 4.15, 2.45, 12, 14), ('Arc_RingOuter', 3.4, 3.05, -8, 20)):
    rot = (math.radians(tilt), math.radians(tilt * 0.5), 0)
    torus(nm, (0, 0, z), R, 0.11, 'gold', rot=rot, flat=0.32, seg=96)
    bm = bmesh.new()
    mt = Matrix.Translation((0, 0, z)) @ rotm(rot)
    for i in range(nr):
        a = 2 * math.pi * i / nr
        bmesh.ops.create_cube(bm, size=1.0, matrix=mt @ Matrix.Rotation(a, 4, 'Z') @ Matrix.Translation((R, 0, 0.035)) @ Matrix.Diagonal((0.13, 0.22, 0.04, 1)))
        bmesh.ops.create_cube(bm, size=1.0, matrix=mt @ Matrix.Rotation(a + math.pi / nr, 4, 'Z') @ Matrix.Translation((R, 0, 0.035)) @ Matrix.Diagonal((0.05, 0.05, 0.04, 1)))
    obj(nm + 'Runes', bm, 'glow', smooth=False)

print('Archon OK', len(col.objects), sum(len(o.data.polygons) for o in col.objects))
