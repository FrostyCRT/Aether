import bpy, bmesh, math, random
from mathutils import Vector

# SERPENT DE LA FAILLE (boss 3, « Source Corrompue ») - concept validé le 2026-11-25 : Aether corrompu, obsidienne noire /
# bleu nuit, veines rouges, yeux rouges, collerette de cristaux brisés en éventail (cobra), crête d'éclats le long de
# l'échine, queue en lame de cristal. Long et DROIT au repos (squelette + animations), le long de +Y, tête vers +Y,
# ventre posé au sol (z = 0). Unités = mètres du jeu.
# Matériaux : 0 écailles (obsidienne), 1 ventre, 2 cristaux noirs, 3 rouge incandescent (yeux, cœurs des cristaux).
rnd = random.Random(5)
L = 15.0                 # longueur totale
HEAD = 1.75              # longueur de la tête (au bout, de L-HEAD à L)
RMAX = 0.72
RNECK = 0.52
SEG = 28                 # sommets par anneau

def body_radius(s):
    t = s / L
    if t < 0.62:   # queue -> ventre : s'épaissit
        return 0.04 + (RMAX - 0.04) * math.sin(min(1.0, t / 0.62) * math.pi / 2) ** 0.9
    if t < 0.86:   # vers le cou : s'affine un peu
        u = (t - 0.62) / 0.24
        return RMAX - (RMAX - RNECK) * (u * u * (3 - 2 * u))
    return RNECK

def center_z(s):
    return body_radius(s) * 0.62

def section(a, w, h, keel):
    # superellipse un peu carrée, ventre aplati, carène sur le dos
    c, sn = math.cos(a), math.sin(a)
    n = 2.6
    x = w * math.copysign(abs(c) ** (2 / n), c)
    z = h * math.copysign(abs(sn) ** (2 / n), sn)
    if z < -h * 0.62: z = -h * 0.62
    z += keel * max(0.0, sn) ** 8
    return x, z

def head_profile(u):
    # u : 0 (cou) -> 1 (bout du museau). Demi-largeur, demi-hauteur. Tête de vipère : triangle vu de dessus, large aux
    # mâchoires, dessus plat ; raccord exact avec le cou (même section qu'à la fin du corps).
    w0, h0 = RNECK * 1.22, RNECK * 0.92
    if u < 0.28:
        k = u / 0.28; k = k * k * (3 - 2 * k)
        w = w0 + (0.98 - w0) * k
        h = h0 + (0.5 - h0) * k
    elif u < 0.86:
        k = (u - 0.28) / 0.58
        w = 0.98 - (0.98 - 0.36) * k
        h = 0.5 - (0.5 - 0.3) * k
    else:
        k = (u - 0.86) / 0.14
        w = 0.36 - 0.24 * k ** 1.5
        h = 0.3 - 0.17 * k ** 1.5
    return w, h

def build():
    bm = bmesh.new()
    rings = []
    # ---------------- corps + tête (une seule enveloppe) ----------------
    step = 0.075
    ns = int((L - HEAD) / step)
    stations = [i * step for i in range(ns + 1)]
    nh = 30
    stations += [L - HEAD + HEAD * (j / nh) for j in range(1, nh + 1)]
    for s in stations:
        ring = []
        if s <= L - HEAD + 1e-6:
            r = body_radius(s); w, h = r * 1.22, r * 0.92; zc = center_z(s); keel = r * 0.08
            for k in range(SEG):
                a = 2 * math.pi * k / SEG
                x, z = section(a, w, h, keel)
                # écailles dorsales : légère bosse par plaque (relief), plus marquée sur le dos
                plate = 0.5 + 0.5 * math.cos(2 * math.pi * s / 0.34)
                top = max(0.0, math.sin(a))
                bump = 1 + 0.025 * plate * top
                ring.append(bm.verts.new((x * bump, s, zc + z * bump)))
        else:
            u = (s - (L - HEAD)) / HEAD
            w, h = head_profile(u)
            zc = RNECK * 0.62 - 0.06 * u
            for k in range(SEG):
                a = 2 * math.pi * k / SEG
                x, z = section(a, w, h, 0.0)
                # dessus plus plat que le corps
                if z > 0: z *= 0.82 + 0.18 * abs(math.cos(a))
                # arcades sourcilières saillantes au-dessus des yeux
                brow = math.exp(-((u - 0.55) / 0.11) ** 2) * math.exp(-((abs(math.cos(a)) - 0.7) / 0.16) ** 2) * max(0.0, math.sin(a))
                z += brow * 0.2
                x *= 1 + brow * 0.18
                # sillon de la gueule sur les flancs
                groove = math.exp(-((math.sin(a) + 0.15) / 0.08) ** 2) * min(1.0, u * 3) * (1 - u) * 1.6
                x *= 1 - 0.06 * groove
                ring.append(bm.verts.new((x, s, zc + z)))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(SEG):
            k2 = (k + 1) % SEG
            f = bm.faces.new((rings[i][k], rings[i][k2], rings[i+1][k2], rings[i+1][k]))
            # ventre = bas de la section
            a = 2 * math.pi * (k + 0.5) / SEG
            f.material_index = 1 if math.sin(a) < -0.55 else 0
    # bouchons
    tail_tip = bm.verts.new((0, -0.12, center_z(0)))
    for k in range(SEG):
        f = bm.faces.new((rings[0][(k + 1) % SEG], rings[0][k], tail_tip)); f.material_index = 0
    nose = bm.verts.new((0, L + 0.04, rings[-1][0].co.z))
    for k in range(SEG):
        f = bm.faces.new((rings[-1][k], rings[-1][(k + 1) % SEG], nose)); f.material_index = 0

    # Point d'ancrage le long de la colonne (attribut 'anchor') : enveloppe = sa propre position ; un cristal entier = le
    # point où il est planté (il reste rigide). Sert à l'aperçu posé ET au squelettage (os le plus proche).
    AN = bm.verts.layers.float.new('anchor')
    for v in bm.verts: v[AN] = v.co.y

    # ---------------- cristaux ----------------
    def crystal(base, direction, length, radius, sides=6, mat=2, core=True, anchor=None):
        before = set(bm.verts)
        _crystal(base, direction, length, radius, sides, mat, core)
        a = base.y if anchor is None else anchor
        for v in bm.verts:
            if v not in before: v[AN] = a

    def _crystal(base, direction, length, radius, sides, mat, core):
        d = direction.normalized()
        side = d.orthogonal().normalized(); up = d.cross(side).normalized()
        tw = rnd.random() * 6.28
        radii = [radius * (0.8 + 0.4 * rnd.random()) for _ in range(sides)]
        def ringp(t, sc):
            return [bm.verts.new(base + d * (length * t) + (side * math.cos(2 * math.pi * k / sides + tw) + up * math.sin(2 * math.pi * k / sides + tw)) * radii[k] * sc) for k in range(sides)]
        r0 = ringp(0.0, 0.7); r1 = ringp(0.66, 1.0)
        tip = bm.verts.new(base + d * length); heel = bm.verts.new(base - d * (length * 0.08))
        for k in range(sides):
            k2 = (k + 1) % sides
            for f in (bm.faces.new((r0[k], r0[k2], r1[k2], r1[k])), bm.faces.new((r1[k], r1[k2], tip)), bm.faces.new((r0[k2], r0[k], heel))):
                f.material_index = mat
        if core:   # fine lame rouge au cœur, visible par les fêlures
            c0 = base + d * (length * 0.2); c1 = base + d * (length * 0.75)
            w = radius * 0.28
            a1 = bm.verts.new(c0 + side * w); a2 = bm.verts.new(c1 + side * w * 0.5); a3 = bm.verts.new(c1 - side * w * 0.5); a4 = bm.verts.new(c0 - side * w)
            f = bm.faces.new((a1, a2, a3, a4)); f.material_index = 3   # matériau double face dans Unity

    neck_s = L - HEAD - 0.15
    nzc = center_z(neck_s)
    # collerette = capuchon de cobra en éclats de cristal : s'étale sur les CÔTÉS (lisible vu de dessus), éclats les plus
    # longs à l'horizontale, courts au sommet, légèrement couchés vers l'arrière ; une 2e rangée plus courte devant.
    for row, (cnt, scale, back, ds) in enumerate(((15, 1.0, -0.32, 0.0), (11, 0.62, -0.18, 0.28))):
        for i in range(cnt):
            f = i / (cnt - 1)
            ang = math.radians(-100 + 200 * f)          # 0 = vers le haut, ±90 = à l horizontale (pas plus bas : sol)
            radial = Vector((math.sin(ang), 0, math.cos(ang)))
            side_w = abs(math.sin(ang))
            length = (0.7 + 1.6 * side_w ** 1.6 + 0.2 * rnd.random()) * scale
            base = Vector((0, neck_s + ds, nzc)) + radial * (RNECK * 0.9)
            dirv = (radial + Vector((0, back, 0.0))).normalized()
            crystal(base, dirv, length, (0.1 + 0.07 * side_w) * scale, anchor=neck_s + ds)
    # cornes de cristal à l'arrière du crâne, balayées vers l'arrière
    for sgn in (1, -1):
        base = Vector((0.5 * sgn, L - HEAD * 0.72, RNECK * 0.62 + 0.42))
        crystal(base, Vector((0.28 * sgn, -1.0, 0.5)), 1.05, 0.1, sides=5)
    # crête dorsale : éclats qui rapetissent vers la queue
    s = neck_s - 0.7
    while s > 0.9:
        r = body_radius(s)
        size = 0.25 + 0.75 * (s / L)
        lean = Vector((0.18 * (rnd.random() - 0.5), -0.55, 1.0))
        crystal(Vector((0, s, center_z(s) + r * 0.92 * 0.95)), lean, 0.85 * size + 0.15, 0.1 * size + 0.04, sides=5, core=size > 0.55)
        s -= 0.42 + 0.25 * (1 - s / L)
    # lame de la queue
    crystal(Vector((0, 0.35, center_z(0.35))), Vector((0, -1.0, 0.08)), 1.2, 0.12, sides=4)
    # yeux
    for sgn in (1, -1):
        u = 0.56; w, h = head_profile(u)
        c = Vector((w * 0.93 * sgn, L - HEAD + HEAD * u, RNECK * 0.62 - 0.06 * u + h * 0.42))
        eye = bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=0.085)
        for v in eye['verts']:
            v.co = c + Vector((v.co.x * 0.8, v.co.y * 1.3, v.co.z * 0.7))
        for f in {f for v in eye['verts'] for f in v.link_faces}: f.material_index = 3
        for v in eye['verts']: v[AN] = c.y
    # la tête est rigide (un seul os) : tout ce qui est au-delà du cou s'ancre au milieu du crâne
    head_anchor = L - HEAD * 0.5
    for v in bm.verts:
        if v[AN] > L - HEAD * 0.85: v[AN] = head_anchor
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm

def make_obj(name, bm, x):
    me = bpy.data.meshes.new(name + '_tmp'); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    for mn, c in (('MS_Scales', (0.06, 0.06, 0.1, 1)), ('MS_Belly', (0.16, 0.13, 0.2, 1)), ('MS_Crystal', (0.02, 0.02, 0.04, 1)), ('MS_Glow', (1, 0.12, 0.08, 1))):
        m = bpy.data.materials.get(mn) or bpy.data.materials.new(mn); m.diffuse_color = c; me.materials.append(m)
    ob = bpy.data.objects.get(name)
    if ob is None:
        ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    else:
        old = ob.data; ob.data = me; bpy.data.meshes.remove(old)
    me.name = name; ob.location = (x, -25, 0)
    return ob

# ---------------- aperçu posé (déformation le long d'une colonne en S, avant dressé) ----------------
def posed_spine(s):
    t = s / L
    x = 2.2 * math.sin(t * 2.2 * math.pi) * min(1.0, t / 0.15) * (1 - max(0.0, (t - 0.75) / 0.25) * 0.7)
    y = s * 0.78
    lift = 0.0
    if t > 0.72:
        k = (t - 0.72) / 0.28
        lift = 3.2 * k ** 1.6
        y -= 1.2 * k ** 2
    return Vector((x, y, lift))

def pose(ob_src, name, x):
    me = ob_src.data.copy()
    ob = bpy.data.objects.get(name)
    if ob is None:
        ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    else:
        old = ob.data; ob.data = me; bpy.data.meshes.remove(old)
    me.name = name
    eps = 0.05
    anchors = me.attributes['anchor'].data
    for v in me.vertices:
        s = min(max(anchors[v.index].value, 0.0), L)
        p = posed_spine(s); tng = (posed_spine(min(L, s + eps)) - posed_spine(max(0, s - eps))).normalized()
        upw = Vector((0, 0, 1)); side = tng.cross(upw).normalized(); up = side.cross(tng).normalized()
        over = v.co.y - s     # au-delà des bouts (museau, lame de queue)
        v.co = p + side * v.co.x + up * v.co.z + tng * over
    ob.location = (x, -25, 0)
    return ob

bm = build()
rest = make_obj('SK_Serpent_Rest', bm, -14.0)
posed = pose(rest, 'SK_Serpent_PosePreview', -4.0)
bpy.ops.wm.save_mainfile()
print('faces', len(rest.data.polygons), 'verts', len(rest.data.vertices), 'dims', tuple(round(v, 2) for v in rest.dimensions))
