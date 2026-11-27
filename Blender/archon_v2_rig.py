import bpy, bmesh, math, random, json
from mathutils import Vector, Matrix, Quaternion, Euler

# Archonte v2 (2026-11-26) - squelette, poids, couleurs des sommets, 6 animations. À lancer après archon_v2_body.py
# puis archon_v2_parts.py. Export : voir archon_v2_export (fin de ce fichier, EXPORT = True).
#  - Corps d'un seul tenant : poids lissés par distance aux segments des os (bras, tronc, cou, tête).
#  - 7 mèches (chaînes de 4 os) et 10 voiles de robe (chaînes de 3 os) : poids selon la position le long de la courbe.
#  - Couleur 'Col' : peau / robe / mèches = dégradé peint (lu par le shader ArchonSoft) ; argent = gris 0,5 (liseré
#    sur la normale) ; cristal / corruption = barycentriques de PaintedCrystal.
#  - Os orientés +Z, poses en rotations du monde de repos ; noms d'états = ceux de l'Animator du boss 3 ; "head" en minuscules.
OX = 60.0
FPS = 60
EXPORT = True
OUT = r"C:\Users\jules\My project\Aether Storm Survivor\Assets\Game\3D Models\Boss3\SK_Archon.fbx"
rnd = random.Random(11)
src = bpy.data.collections['ArchonV2']

col = bpy.data.collections.get('ArchonV2Rig')
if col is None:
    col = bpy.data.collections.new('ArchonV2Rig'); bpy.context.scene.collection.children.link(col)
for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
for a in list(bpy.data.actions):
    if a.name.startswith('Archon_'): bpy.data.actions.remove(a)
for a in list(bpy.data.armatures):
    if a.users == 0: bpy.data.armatures.remove(a)
for c in ('Archon', 'ArchonRig'):
    if c in bpy.data.collections:
        for o in list(bpy.data.collections[c].objects):
            if o.type == 'ARMATURE': o.name = 'Old_' + o.name

def V(*a): return Vector(a)
def spline(P, per=8):
    P = [Vector(p) for p in P]; E = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]; out = []
    for i in range(1, len(E) - 2):
        a, b, c, d = E[i - 1], E[i], E[i + 1], E[i + 2]
        for k in range(per):
            t = k / per; t2 = t * t; t3 = t2 * t
            out.append(0.5 * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3))
    out.append(P[-1]); return out
def at(C, t): return C[min(len(C) - 1, int(round(t * (len(C) - 1))))]

# ---------------------------------------------------------------- os
ARM = {}
for sx, s in ((1, 'R'), (-1, 'L')):
    sh = V(sx * 0.92, 0.0, 5.85); el = V(sx * 1.42, -0.12, 4.85); wr = V(sx * 1.72, -0.42, 4.0)
    ARM[s] = (sh, el, wr, wr + (wr - el).normalized() * 0.55)
HC = V(0, 0.55, 7.1)
BONES = [('Root', V(0, 0, 0), None), ('Hips', V(0, 0, 4.1), 'Root'), ('Spine', V(0, 0, 4.6), 'Hips'),
         ('Chest', V(0, 0, 5.25), 'Spine'), ('Neck', V(0, 0, 6.05), 'Chest'), ('head', V(0, 0, 6.6), 'Neck'),
         ('Crown', V(0, 0.03, 7.56), 'head'), ('Halo', HC, 'head'), ('Tail', V(0, 0, 1.9), 'Hips'),
         ('Fragments', V(0, 0, 0.8), 'Root'), ('RingInner', V(0, 0, 4.2), 'Hips'), ('RingOuter', V(0, 0, 3.35), 'Hips')]
for s, (sh, el, wr, tip) in ARM.items():
    sx = 1 if s == 'R' else -1
    BONES += [('Shoulder.' + s, V(sx * 0.45, 0, 5.95), 'Chest'), ('UpperArm.' + s, sh, 'Shoulder.' + s),
              ('Forearm.' + s, el, 'UpperArm.' + s), ('Hand.' + s, wr, 'Forearm.' + s)]
HAIR_T = (0.0, 0.25, 0.5, 0.75); ROBE_T = (0.0, 0.35, 0.7)
CHAINS = {}       # nom d'objet -> (courbe dense, [noms d'os], [t des os])
for o in src.objects:
    if o.name.startswith(('ArcV2_Hair_', 'ArcV2_Robe_')):
        C = spline(json.loads(o['pts']), 20)
        hair = o.name.startswith('ArcV2_Hair_'); idx = o.name.split('_')[-1]
        Ts = HAIR_T if hair else ROBE_T; names = []
        for i, t in enumerate(Ts):
            n = ('Hair%s_%d' if hair else 'Robe%s_%d') % (idx, i)
            BONES.append((n, at(C, t), names[-1] if names else ('head' if hair else 'Hips'))); names.append(n)
        CHAINS[o.name] = (C, names, Ts)
ROBE_TAN = {o.name.split('_')[-1]: Vector(o['tan']) for o in src.objects if o.name.startswith('ArcV2_Robe_')}

arm_data = bpy.data.armatures.new('Archon_Armature')
rig = bpy.data.objects.new('Archon_Rig', arm_data); col.objects.link(rig); rig.location = (OX, 0, 0)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for n, p, par in BONES:
    b = arm_data.edit_bones.new(n); b.head = p; b.tail = p + V(0, 0, 0.25); b.roll = 0
    if par: b.parent = arm_data.edit_bones[par]
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- pièces
SEGS = [('Hips', V(0, 0, 3.4), V(0, 0, 4.35)), ('Spine', V(0, 0, 4.35), V(0, 0, 4.95)), ('Chest', V(0, 0, 4.95), V(0, 0, 5.9)),
        ('Neck', V(0, 0, 5.95), V(0, 0, 6.5)), ('head', V(0, 0, 6.5), V(0, 0, 7.4))]
for s, (sh, el, wr, tip) in ARM.items():
    SEGS += [('Shoulder.' + s, V(sh.x * 0.35, 0, 5.95), sh * 0.85 + V(0, 0, 5.95 * 0.15)), ('UpperArm.' + s, sh, el),
             ('Forearm.' + s, el, wr), ('Hand.' + s, wr, tip)]
def seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared)); return (p - (a + ab * t)).length

def bone_of(n):
    if n.startswith(('ArcV2_Hood', 'ArcV2_Diadem', 'ArcV2_Eyes')): return 'head'
    if n.startswith('ArcV2_Crown'): return 'Crown'
    if n.startswith('ArcV2_Halo'): return 'Halo'
    if n.startswith(('ArcV2_Heart', 'ArcV2_Cracks')): return 'Chest'
    if n.startswith('ArcV2_Sash'): return 'Hips'
    if n.startswith('ArcV2_Tail'): return 'Tail'
    if n.startswith('ArcV2_Fragments'): return 'Fragments'
    if n.startswith('ArcV2_RingInner'): return 'RingInner'
    if n.startswith('ArcV2_RingOuter'): return 'RingOuter'
    if n.startswith('ArcV2_Corruption'): return 'Shoulder.L'
    return None

CRYSTAL = ('ArcV2_Crystal', 'ArcV2_Corrupt')
def lerpc(a, b, t): return tuple(x + (y - x) * t for x, y in zip(a, b))
pieces = []
dg = bpy.context.evaluated_depsgraph_get()
for o in src.objects:
    if o.type != 'MESH': continue
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    c = bpy.data.objects.new('P_' + o.name, me); col.objects.link(c); c.location = (OX, 0, 0)
    for bn, _, _ in BONES: c.vertex_groups.new(name=bn)
    mname = me.materials[0].name if me.materials else ''
    chain = CHAINS.get(o.name); fixed = bone_of(o.name)
    vt = {}
    for v in me.vertices:
        p = v.co
        if fixed:
            c.vertex_groups[fixed].add([v.index], 1.0, 'REPLACE'); continue
        if chain:
            C, names, Ts = chain
            i = min(range(len(C)), key=lambda k: (C[k] - p).length_squared); t = i / (len(C) - 1); vt[v.index] = t
            for k in range(len(Ts)):
                t0 = Ts[k]; t1 = Ts[k + 1] if k + 1 < len(Ts) else 9.0
                if t0 <= t < t1:
                    f = 0.0 if t1 > 1.5 else (t - t0) / (t1 - t0)
                    c.vertex_groups[names[k]].add([v.index], 1 - f, 'REPLACE')
                    if f > 0: c.vertex_groups[names[k + 1]].add([v.index], f, 'REPLACE')
                    break
            continue
        if o.name == 'ArcV2_ArmSilver':
            s = 'R' if p.x > 0 else 'L'; c.vertex_groups['Forearm.' + s].add([v.index], 1.0, 'REPLACE'); continue
        ds = sorted(((seg_dist(p, a, b), n) for n, a, b in SEGS))[:3]
        ws = [1.0 / (d + 0.03) ** 6 for d, _ in ds]; tot = sum(ws)
        for (d, n), w in zip(ds, ws):
            if w / tot > 0.01: c.vertex_groups[n].add([v.index], w / tot, 'REPLACE')
    cattr = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
    for poly in me.polygons:
        li = list(poly.loop_indices); fid = rnd.random()
        if mname in CRYSTAL:
            pat = ((0, 1, 1), (1, 0, 1), (1, 0, 1), (0, 1, 1)) if len(li) == 4 else [((1, 0, 0), (0, 1, 0), (0, 0, 1))[k % 3] for k in range(len(li))]
            for k, l in enumerate(li): cattr.data[l].color = tuple(pat[k]) + (fid,)
            continue
        for l in li:
            vi = me.loops[l].vertex_index; z = me.vertices[vi].co.z
            # MODIFIÉ (essais de palettes) : la couleur des sommets ne porte plus une teinte mais un FACTEUR de dégradé t
            # (0 = base, 1 = bout) en RVB ; ArchonSoft mélange ses 2 couleurs _ColorA -> _ColorB selon t (réglable en jeu).
            if mname == 'ArcV2_Skin': tt = max(0.0, min(1.0, (6.8 - z) / 3.0)) * 0.35
            elif mname == 'ArcV2_Robe' and o.name.startswith('ArcV2_Robe_'): tt = vt.get(vi, 0.0) ** 0.8
            elif mname == 'ArcV2_Robe': tt = 0.0
            elif mname == 'ArcV2_Hair': tt = vt.get(vi, 0.0) ** 0.7
            else: tt = None
            col_ = (tt, tt, tt) if tt is not None else (0.5, 0.5, 0.5)
            cattr.data[l].color = col_ + (1.0,)
    pieces.append(c)

with bpy.context.temp_override(active_object=pieces[0], selected_editable_objects=pieces, object=pieces[0]):
    bpy.ops.object.join()
body = pieces[0]; body.name = 'SK_Archon'; body.data.name = 'SK_Archon'
body.parent = rig; body.location = (0, 0, 0)
md = body.modifiers.new('Armature', 'ARMATURE'); md.object = rig

# ---------------------------------------------------------------- poses
REST_Q = {b.name: b.matrix_local.to_quaternion() for b in arm_data.bones}
def rotm(r): return Matrix.Rotation(r[2], 4, 'Z') @ Matrix.Rotation(r[1], 4, 'Y') @ Matrix.Rotation(r[0], 4, 'X')
RING_N = {'RingInner': (rotm((math.radians(12), math.radians(6), 0)) @ V(0, 0, 1, 0)).to_3d().normalized(),
          'RingOuter': (rotm((math.radians(-8), math.radians(-4), 0)) @ V(0, 0, 1, 0)).to_3d().normalized()}
HAIRS = sorted({n.split('_')[0][4:] for n, _, _ in BONES if n.startswith('Hair')})
ROBES = sorted({n.split('_')[0][4:] for n, _, _ in BONES if n.startswith('Robe')})

def P(**kw): return {k: list(v) + [0] * (8 - len(v)) for k, v in kw.items()}
def mirror(d):
    out = dict(d)
    for k, v in d.items():
        if '.R' in k: out[k.replace('.R', '.L')] = [v[0], -v[1], -v[2], -v[3], v[4], v[5], v[6], v[7]]
    return out
def blend(a, b, t):
    out = {}
    for k in set(a) | set(b):
        va = a.get(k, [0] * 8); vb = b.get(k, [0] * 8); out[k] = [x + (y - x) * t for x, y in zip(va, vb)]
    return out
def add(*ds):
    out = {}
    for d in ds:
        for k, v in d.items():
            o = out.get(k, [0] * 8); out[k] = [x + y for x, y in zip(o, v)]
    return out
def ease(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def timeline(keys, t):
    for (t0, p0), (t1, p1) in zip(keys, keys[1:]):
        if t0 <= t <= t1: return blend(p0, p1, ease((t - t0) / (t1 - t0)))
    return keys[-1][1]
def robe_rot(k, ang):
    e = Quaternion(ROBE_TAN[k], math.radians(ang)).to_euler('XYZ'); return [math.degrees(e.x), math.degrees(e.y), math.degrees(e.z)]

S = math.sin
def life(t, amp=1.0, turns=(2, 2), hair=1.0, robe=1.0, flare=0.0, stream=0.0):
    """Fond bouclable (fréquences entières) : flottement, souffle, bras, mèches et voiles qui ondulent."""
    w = 2 * math.pi * t; a = amp; d = {}
    d['Hips'] = [1.5 * a * S(w + 1), 0, 2 * a * S(w), 0, 0, 0.12 * a * S(w)]
    d['Spine'] = [1.0 * a * S(w + 0.6), 0, 0]; d['Chest'] = [1.5 * a * S(w + 1.2), 1.0 * a * S(w + 0.3), 0]
    d['head'] = [-2 * a * S(w + 1.8), 0, 4 * a * S(w + 0.5)]; d['Halo'] = [0, 0, 6 * a * S(w)]
    d['Crown'] = [0, 0, 360 * t / 9 * turns[0] * 0.5 * 9 / 9]
    d['Tail'] = [3 * a * S(w + 2.2), 3 * a * S(w + 1.6), 0]
    d['Fragments'] = [0, 0, 360 * t, 0, 0, 0.12 * a * S(w + 2.5)]
    d['RingInner'] = [0, 0, 0, 0, 0, 0, 0, 360.0 / 16 * turns[0] * t]
    d['RingOuter'] = [0, 0, 0, 0, 0, 0, 0, -360.0 / 22 * turns[1] * t]
    for sx, s in ((1, 'R'), (-1, 'L')):
        ph = 0 if sx > 0 else 1.3
        d['Shoulder.' + s] = [0, -sx * 2 * a * S(w + ph), 0]
        d['UpperArm.' + s] = [3 * a * S(w + 0.4 + ph), -sx * 3 * a * S(w + 1.1 + ph), 0]
        d['Forearm.' + s] = [4 * a * S(w + 0.9 + ph), 0, 0]
        d['Hand.' + s] = [8 * a * S(w + 1.5 + ph), 0, sx * 6 * a * S(w + 0.7 + ph)]
    for j in HAIRS:
        jj = int(j); x = -1 + 2 * jj / 6
        for i in range(4):
            d['Hair%s_%d' % (j, i)] = [hair * (4 + 2 * i) * S(2 * w - 0.9 * i + jj) + (stream if i == 0 else stream * 0.3),
                                       0, hair * 3 * S(w - 0.9 * i + 2 * jj) + flare * x * (12 if i == 0 else 4)]
    for k in ROBES:
        kk = int(k)
        for i in range(3):
            d['Robe%s_%d' % (k, i)] = [x + y for x, y in zip(robe_rot(k, robe * (2 + 1.5 * i) * S(2 * w - 1.0 * i + 0.7 * kk) - flare * (8 if i == 0 else 3)),
                                                         [stream * (0.5 if i == 0 else 0.3), 0, 0])]
    return P(**d)

ZERO = P()
CHARGE = mirror(P(**{'Hips': [0, 0, 0, 0, 0, 0.35], 'Chest': [-7, 0, 0], 'head': [-12, 0, 0], 'Halo': [0, 0, 0, 0, 0, 0, 0.12],
                     'Shoulder.R': [0, -6, 0], 'UpperArm.R': [-10, -58, 0], 'Forearm.R': [-15, -25, 0], 'Hand.R': [-60, 0, 0]}))
GLIDE = mirror(P(**{'Hips': [16, 0, 0, 0, 0, 0.25], 'Chest': [6, 0, 0], 'head': [-16, 0, 0],
                    'UpperArm.R': [32, 10, 0], 'Forearm.R': [18, 0, 0], 'Hand.R': [30, 0, 0], 'Tail': [12, 0, 0],
                    'RingInner': [6, 0, 0], 'RingOuter': [8, 0, 0]}))
WIND = mirror(P(**{'Hips': [-10, 0, 0, 0, 0.2, 0.2], 'Chest': [-6, 0, 0], 'head': [-6, 0, 0],
                   'UpperArm.R': [35, -35, 0], 'Forearm.R': [20, -10, 0], 'Hand.R': [20, 0, 0]}))
STRIKE = mirror(P(**{'Hips': [22, 0, 0, 0, -0.5, 0.1], 'Chest': [9, 0, 0], 'head': [-18, 0, 0], 'Tail': [18, 0, 0],
                     'UpperArm.R': [-78, -12, -16], 'Forearm.R': [-12, 0, 0], 'Hand.R': [-70, 0, 0]}))
OPEN = mirror(P(**{'Chest': [-6, 0, 0], 'head': [-8, 0, 0], 'UpperArm.R': [-5, -78, 0], 'Forearm.R': [0, -8, 0],
                   'Halo': [0, 0, 0, 0, 0, 0, 0.1], 'RingInner': [0, 0, 0, 0, 0, 0, 0.12], 'RingOuter': [0, 0, 0, 0, 0, 0, 0.12]}))
CLASP = mirror(P(**{'Hips': [6, 0, 0, 0, 0, -0.15], 'Chest': [8, 0, 0], 'head': [10, 0, 0], 'UpperArm.R': [-62, -18, -40],
                    'Forearm.R': [-30, 0, -35], 'Hand.R': [-40, 0, -30], 'Halo': [0, 0, 0, 0, 0, 0, -0.08],
                    'RingInner': [0, 0, 0, 0, 0, 0, -0.42], 'RingOuter': [0, 0, 0, 0, 0, 0, -0.45]}))
RECOIL = mirror(P(**{'Hips': [-8, 0, 0, 0, 0, 0.3], 'Chest': [-14, 0, 0], 'head': [-28, 0, 0],
                     'UpperArm.R': [10, -45, 0], 'Forearm.R': [0, -15, 0]}))
FALL = mirror(P(**{'Hips': [24, 0, 4, 0, 0, -2.3], 'Chest': [12, 0, 0], 'head': [38, 0, 6], 'Tail': [-10, 0, 0],
                   'Halo': [40, 25, 0, 0, 0.6, -0.8], 'UpperArm.R': [12, 14, 0], 'Forearm.R': [16, 0, 0], 'Hand.R': [30, 0, 10],
                   'RingInner': [-12, -6, 0, 0, 0, -0.9], 'RingOuter': [8, 4, 0, 0, 0, -0.95], 'Fragments': [0, 0, 0, 0, 0, -0.42]}))

def clip_idle(t): return life(t)
def clip_coiled(t):
    w = 2 * math.pi * t
    return add(CHARGE, life(t, 0.5, (4, 4), hair=1.3, robe=1.3, flare=1.0),
               P(**{'Chest': [0.8 * S(6 * w), 0.6 * S(5 * w + 1), 0], 'Hand.R': [4 * S(4 * w), 0, 0], 'Hand.L': [4 * S(4 * w + 2), 0, 0]}))
def clip_slither(t): return add(GLIDE, life(t, 0.6, (3, 3), hair=1.4, robe=1.5, stream=22))
def clip_bite(t):
    return add(timeline([(0, ZERO), (0.28, WIND), (0.42, STRIKE), (0.6, STRIKE), (1.0, ZERO)], t), life(t, 0.3, (1, 1), hair=1.2, robe=1.2,
               stream=14 * ease((t - 0.3) / 0.15) * (1 - ease((t - 0.6) / 0.4))))
def clip_dance(t):
    k = timeline([(0, 0.0), (0.35, 0.0), (0.55, 1.0), (0.72, 1.0), (1.0, 0.0)], t) if False else None
    return add(timeline([(0, OPEN), (0.35, OPEN), (0.55, CLASP), (0.72, CLASP), (1.0, OPEN)], t), life(t, 0.4, (3, 3), hair=1.2, robe=1.2, flare=0.6))
def clip_death(t): return add(timeline([(0, ZERO), (0.16, RECOIL), (0.85, FALL), (1.0, FALL)], t), life(t, 0.2 * (1 - t), (1, 1), hair=1 - t, robe=1 - t))

CLIPS = [('Idle', 4.0, clip_idle, True), ('Coiled', 2.0, clip_coiled, True), ('Slither', 2.0, clip_slither, True),
         ('Bite', 1.4, clip_bite, False), ('Dance', 2.4, clip_dance, True), ('Death', 2.5, clip_death, False)]

def apply_pose(pose):
    for pb in rig.pose.bones:
        v = pose.get(pb.name, [0] * 8)
        qw = Euler((math.radians(v[0]), math.radians(v[1]), math.radians(v[2])), 'XYZ').to_quaternion()
        if pb.name in RING_N and abs(v[7]) > 1e-6: qw = Quaternion(RING_N[pb.name], math.radians(v[7])) @ qw
        r = REST_Q[pb.name]
        pb.rotation_quaternion = r.inverted() @ qw @ r
        pb.location = r.inverted() @ V(v[3], v[4], v[5])
        s = 1.0 + v[6]; pb.scale = (s, s, s)

bpy.context.scene.render.fps = FPS
rig.animation_data_create()
for pb in rig.pose.bones: pb.rotation_mode = 'QUATERNION'
for name, dur, fn, loop in CLIPS:
    act = bpy.data.actions.new('Archon_' + name); act.use_fake_user = True
    rig.animation_data.action = act
    N = int(round(dur * FPS))
    for f in range(N + 1):
        t = f / N
        apply_pose(fn(t % 1.0 if (loop and f < N) else (0.0 if loop else t)))
        for pb in rig.pose.bones:
            pb.keyframe_insert('rotation_quaternion', frame=f); pb.keyframe_insert('location', frame=f); pb.keyframe_insert('scale', frame=f)
    act.frame_range = (0, N)
rig.animation_data.action = bpy.data.actions['Archon_Idle']
bpy.context.scene.frame_start = 0; bpy.context.scene.frame_end = 240; bpy.context.scene.frame_set(0)
print('rig OK', len(BONES), 'os', sum(len(p.vertices) - 2 for p in body.data.polygons), 'triangles')
