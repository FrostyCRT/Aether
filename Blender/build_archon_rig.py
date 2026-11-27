import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Quaternion, Euler

# Boss 3 v3 : L'ARCHONTE - squelette, poids, couleurs des sommets, 6 animations, export FBX (2026-11-26).
# À lancer APRÈS build_archon.py (collection "Archon", posée en X = 40).
#  - Copie évaluée de chaque pièce (subdivisions appliquées), couleur des sommets 'Col' :
#      cristal / corruption = barycentriques de PaintedCrystal (RVB) + identité de facette (A) ;
#      le reste = gris 0,5 -> PaintedMetal / Porcelaine retombent sur la normale (skinnée) pour le liseré.
#  - Pièces rigides (100 % sur un os) sauf torse (Spine/Chest/Neck) et bras (UpperArm/Forearm), fondus aux jointures.
#  - Os tous orientés +Z (repère de repos identique) : les poses s'écrivent en rotations/positions du monde de repos.
#  - Noms des états = ceux de RiftSerpent.controller (Idle, Coiled, Slither, Bite, Dance, Death) : le code du boss
#    (IsCoiling, Slither, Bite, Dance, Death) reste valable. "head" en minuscules (BossCorruptedSource.HeadForwardDistance).
#  - Les animations restent SUR PLACE (le code déplace le boss) ; rien ne passe sous le sol sauf à la mort (retour à la faille).
OX = 40.0
FPS = 60
OUT = r"C:\Users\jules\My project\Aether Storm Survivor\Assets\Game\3D Models\Boss3\SK_Archon.fbx"
rnd = random.Random(11)
src = bpy.data.collections['Archon']

col = bpy.data.collections.get('ArchonRig')
if col is None:
    col = bpy.data.collections.new('ArchonRig'); bpy.context.scene.collection.children.link(col)
for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
for a in list(bpy.data.actions):
    if a.name.startswith('Archon_'): bpy.data.actions.remove(a)

def rotm(rot): return Matrix.Rotation(rot[2], 4, 'Z') @ Matrix.Rotation(rot[1], 4, 'Y') @ Matrix.Rotation(rot[0], 4, 'X')
def V(*a): return Vector(a)

# ---------------------------------------------------------------- os (têtes = pivots)
ua_R = (V(1.28, 0.05, 5.85), V(1.95, -0.2, 4.75), V(2.35, -0.55, 3.85))
BONES = [('Root', V(0, 0, 0), None), ('Hips', V(0, 0, 3.6), 'Root'), ('Spine', V(0, 0, 4.2), 'Hips'),
         ('Chest', V(0, 0, 5.0), 'Spine'), ('Neck', V(0, 0, 6.4), 'Chest'), ('head', V(0, 0, 6.75), 'Neck'),
         ('Halo', V(0, 0.5, 7.15), 'head'), ('Tail', V(0, 0, 3.4), 'Hips'), ('Fragments', V(0, 0, 0.8), 'Root'),
         ('RingInner', V(0, 0, 4.15), 'Hips'), ('RingOuter', V(0, 0, 3.4), 'Hips')]
for sx, s in ((1, 'R'), (-1, 'L')):
    m = V(sx, 1, 1)
    sh, el, wr = [p * 1 for p in ua_R]
    sh = V(sx * sh.x, sh.y, sh.z); el = V(sx * el.x, el.y, el.z); wr = V(sx * wr.x, wr.y, wr.z)
    hw = wr + (wr - el).normalized() * 0.42
    BONES += [('Shoulder.' + s, V(sx * 0.6, 0, 6.0), 'Chest'), ('UpperArm.' + s, sh, 'Shoulder.' + s),
              ('Forearm.' + s, el, 'UpperArm.' + s), ('Hand.' + s, hw, 'Forearm.' + s)]
    for k in range(3):
        BONES.append(('Mantle.%s%d' % (s, k), V(sx * (0.6 + 0.12 * k), 0.75, 6.1 - 0.15 * k), 'Chest'))
HEAD = {n: p for n, p, _ in BONES}

arm_data = bpy.data.armatures.new('Archon_Armature')
rig = bpy.data.objects.new('Archon_Rig', arm_data); col.objects.link(rig)
rig.location = (OX, 0, 0)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for n, p, par in BONES:
    b = arm_data.edit_bones.new(n); b.head = p; b.tail = p + V(0, 0, 0.3); b.roll = 0
    if par: b.parent = arm_data.edit_bones[par]
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- copie des pièces, couleurs, poids
def bone_of(name):
    if name.startswith(('Arc_Head', 'Arc_Nose', 'Arc_Eyes', 'Arc_GoldTears', 'Arc_Diadem')): return 'head'
    if name.startswith('Arc_Halo'): return 'Halo'
    if name.startswith(('Arc_Belt', 'Arc_Tasset')): return 'Hips'
    if name.startswith('Arc_AbsBands'): return 'Spine'
    if name.startswith(('Arc_ChestPlate', 'Arc_ChestGold', 'Arc_Heart', 'Arc_Collar')): return 'Chest'
    if name.startswith('Arc_TailCrystals'): return 'Tail'
    if name.startswith('Arc_TailFragments'): return 'Fragments'
    if name.startswith('Arc_RingInner'): return 'RingInner'
    if name.startswith('Arc_RingOuter'): return 'RingOuter'
    if name == 'Arc_MantleCorrupt': return 'Mantle.L2'
    if name.startswith('Arc_Mantle'):     # Arc_MantleR0 / Arc_MantleRimL2 ...
        return 'Mantle.' + name.replace('Arc_MantleRim', '').replace('Arc_Mantle', '').replace('Gold', '')
    if name == 'Arc_PauldronGem': return 'Shoulder.R'
    if name.startswith('Arc_Pauldron'): return 'Shoulder.' + name[-1]
    if name.startswith('Arc_Elbow') or name.startswith('Arc_WristGlow'): return 'Forearm.' + name[-1]
    if name.startswith('Arc_Hand'): return 'Hand.' + ('R' if 'HandR' in name else 'L')
    return None    # pondéré par sommet (torse, bras, fêlures, corruption)

CRYSTAL_MATS = ('Arc_Crystal', 'Arc_Corrupt')
pieces = []
dg = bpy.context.evaluated_depsgraph_get()
for o in src.objects:
    if o.type != 'MESH': continue
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    me.transform(Matrix.Translation(o.location - V(OX, 0, 0)) @ o.matrix_basis.to_3x3().to_4x4())
    c = bpy.data.objects.new('P_' + o.name, me); col.objects.link(c); c.location = (OX, 0, 0)
    cattr = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
    crystal = me.materials and me.materials[0].name in CRYSTAL_MATS
    for p in me.polygons:
        li = list(p.loop_indices); fid = rnd.random()
        if crystal:
            if len(li) == 4: pat = ((0, 1, 1), (1, 0, 1), (1, 0, 1), (0, 1, 1))
            else: pat = [((1, 0, 0), (0, 1, 0), (0, 0, 1))[k % 3] for k in range(len(li))]
            for k, l in enumerate(li): cattr.data[l].color = tuple(pat[k]) + (fid,)
        else:
            for l in li: cattr.data[l].color = (0.5, 0.5, 0.5, 1.0)
    for bn, _, _ in BONES: c.vertex_groups.new(name=bn)
    fixed = bone_of(o.name)
    def put(vi, bn, w):
        if w > 1e-4: c.vertex_groups[bn].add([vi], w, 'REPLACE')
    for v in me.vertices:
        p = v.co
        if fixed: put(v.index, fixed, 1.0); continue
        n = o.name
        if n == 'Arc_Torso':
            a = max(0.0, min(1.0, (p.z - 4.55) / 0.5)); b = max(0.0, min(1.0, (p.z - 6.3) / 0.25))
            put(v.index, 'Spine', 1 - a); put(v.index, 'Chest', a * (1 - b)); put(v.index, 'Neck', a * b)
        elif n.startswith(('Arc_ArmGold', 'Arc_Arm')):
            sx = 1 if n.endswith('R') else -1
            sh, el, wr = [V(sx * q.x, q.y, q.z) for q in ua_R]
            fa = (wr - el).normalized()
            w = max(0.0, min(1.0, ((p - el).dot(fa) + 0.12) / 0.24))
            put(v.index, 'UpperArm.' + n[-1], 1 - w); put(v.index, 'Forearm.' + n[-1], w)
        elif n in ('Arc_Cracks', 'Arc_Corruption'):
            put(v.index, 'Shoulder.L' if (p.x < -0.95 and p.z > 5.55) else 'Chest', 1.0)
        else:
            put(v.index, 'Chest', 1.0)
    pieces.append(c)

with bpy.context.temp_override(active_object=pieces[0], selected_editable_objects=pieces, object=pieces[0]):
    bpy.ops.object.join()
body = pieces[0]; body.name = 'SK_Archon'; body.data.name = 'SK_Archon'
body.parent = rig; body.location = (0, 0, 0)
md = body.modifiers.new('Armature', 'ARMATURE'); md.object = rig

# ---------------------------------------------------------------- poses
REST_Q = {b.name: b.matrix_local.to_quaternion() for b in arm_data.bones}
RING_N = {'RingInner': (rotm((math.radians(12), math.radians(6), 0)) @ V(0, 0, 1, 0)).to_3d().normalized(),
          'RingOuter': (rotm((math.radians(-8), math.radians(-4), 0)) @ V(0, 0, 1, 0)).to_3d().normalized()}

def P(**kw):
    """pose = {os: [rx, ry, rz (degrés, repère monde de repos), lx, ly, lz, échelle, spin (degrés autour de la normale)]}"""
    return {k: list(v) + [0] * (8 - len(v)) if len(v) < 8 else list(v) for k, v in kw.items()}

def mirror(d):
    out = {}
    for k, v in d.items():
        out[k] = v
        if k.endswith('.R') or '.R' in k:
            kl = k.replace('.R', '.L'); out[kl] = [v[0], -v[1], -v[2], -v[3], v[4], v[5], v[6], v[7]]
    return out

def blend(a, b, t):
    keys = set(a) | set(b); out = {}
    for k in keys:
        va = a.get(k, [0, 0, 0, 0, 0, 0, 0, 0]); vb = b.get(k, [0, 0, 0, 0, 0, 0, 0, 0])
        out[k] = [x + (y - x) * t for x, y in zip(va, vb)]
    return out

def add(a, b):
    keys = set(a) | set(b); out = {}
    for k in keys:
        va = a.get(k, [0] * 8); vb = b.get(k, [0] * 8)
        out[k] = [x + y for x, y in zip(va, vb)]
    return out

def ease(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)

def timeline(keys, t):
    for (t0, p0), (t1, p1) in zip(keys, keys[1:]):
        if t0 <= t <= t1: return blend(p0, p1, ease((t - t0) / (t1 - t0)))
    return keys[-1][1]

S = math.sin
def life(t, amp=1.0, ring_turns=(2, 2), speed=1):
    """Mouvement de fond (bouclable : fréquences entières) : flottement, souffle, mains et manteau qui dérivent."""
    w = 2 * math.pi * t * speed; a = amp
    d = {'Hips': [1.5 * a * S(w + 1), 0, 2 * a * S(w), 0, 0, 0.12 * a * S(w)],
         'Spine': [1.0 * a * S(w + 0.6), 0, 0], 'Chest': [1.5 * a * S(w + 1.2), 1.0 * a * S(w + 0.3), 0],
         'head': [-2 * a * S(w + 1.8), 0, 4 * a * S(w + 0.5)], 'Halo': [0, 5 * a * S(w), 0],
         'Tail': [3 * a * S(w + 2.2), 3 * a * S(w + 1.6), 0],
         'Fragments': [0, 0, 0, 0, 0, 0.12 * a * S(w + 2.5), 0], 'RingInner': [0, 0, 0], 'RingOuter': [0, 0, 0]}
    d['Fragments'] = [0, 0, 360 * t, 0, 0, 0.12 * a * S(w + 2.5)]
    d['RingInner'] = [0, 0, 0, 0, 0, 0, 0, 360.0 / 14 * ring_turns[0] * t]
    d['RingOuter'] = [0, 0, 0, 0, 0, 0, 0, -360.0 / 20 * ring_turns[1] * t]
    for sx, s in ((1, 'R'), (-1, 'L')):
        ph = 0 if sx > 0 else 1.3
        d['Shoulder.' + s] = [0, -sx * 2 * a * S(w + ph), 0]
        d['UpperArm.' + s] = [3 * a * S(w + 0.4 + ph), -sx * 3 * a * S(w + 1.1 + ph), 0]
        d['Forearm.' + s] = [4 * a * S(w + 0.9 + ph), 0, 0]
        d['Hand.' + s] = [8 * a * S(w + 1.5 + ph), 0, sx * 6 * a * S(w + 0.7 + ph), 0, 0, 0.1 * a * S(w + 2 + ph)]
        for k in range(3):
            d['Mantle.%s%d' % (s, k)] = [2.5 * a * S(w + 0.7 * k + ph), -sx * 1.5 * a * S(w + k + ph), 0]
    return P(**{k: v for k, v in d.items()})

ZERO = P()
# Charge (Coiled) : bras levés sur les côtés, paumes vers le ciel, tête relevée, manteau ouvert
CHARGE = mirror(P(**{'Hips': [0, 0, 0, 0, 0, 0.35], 'Chest': [-7, 0, 0], 'head': [-12, 0, 0], 'Halo': [0, 0, 0, 0, 0, 0, 0.12],
                     'Shoulder.R': [0, -6, 0], 'UpperArm.R': [-10, -58, 0], 'Forearm.R': [-15, -25, 0],
                     'Hand.R': [-70, 0, 0, 0, 0, 0.35], 'Mantle.R0': [-4, -16, 0], 'Mantle.R1': [-4, -12, 0], 'Mantle.R2': [-4, -8, 0],
                     'Tail': [0, 0, 0]}))
# Glissade (Slither) : penché en avant, bras ramenés en arrière, manteau et traîne qui traînent
GLIDE = mirror(P(**{'Hips': [16, 0, 0, 0, 0, 0.25], 'Chest': [6, 0, 0], 'head': [-16, 0, 0],
                    'UpperArm.R': [32, 10, 0], 'Forearm.R': [18, 0, 0], 'Hand.R': [30, 0, 0, 0, 0.45, 0.15],
                    'Mantle.R0': [16, 0, 0], 'Mantle.R1': [20, 0, 0], 'Mantle.R2': [24, 0, 0], 'Tail': [12, 0, 0],
                    'RingInner': [6, 0, 0], 'RingOuter': [8, 0, 0]}))
# Ruée (Bite) : armé en arrière puis poussée des deux mains vers l'avant
WIND = mirror(P(**{'Hips': [-10, 0, 0, 0, 0.2, 0.2], 'Chest': [-6, 0, 0], 'head': [-6, 0, 0],
                   'UpperArm.R': [35, -35, 0], 'Forearm.R': [20, -10, 0], 'Hand.R': [20, 0, 0, 0, 0.4, 0.35],
                   'Mantle.R0': [-6, -10, 0], 'Mantle.R1': [-6, -8, 0], 'Mantle.R2': [-6, -6, 0]}))
STRIKE = mirror(P(**{'Hips': [22, 0, 0, 0, -0.5, 0.1], 'Chest': [9, 0, 0], 'head': [-18, 0, 0], 'Tail': [18, 0, 0],
                     'UpperArm.R': [-78, -12, -16], 'Forearm.R': [-12, 0, 0], 'Hand.R': [-70, 0, 0, 0, -0.9, 0.15],
                     'Mantle.R0': [22, 0, 0], 'Mantle.R1': [26, 0, 0], 'Mantle.R2': [30, 0, 0]}))
# Implosion (Dance) : bras grands ouverts -> mains jointes devant le cœur ; anneaux qui se resserrent
OPEN = mirror(P(**{'Chest': [-6, 0, 0], 'head': [-8, 0, 0], 'UpperArm.R': [-5, -78, 0], 'Forearm.R': [0, -8, 0],
                   'Hand.R': [0, 0, 0, 0, 0, 0.2], 'Halo': [0, 0, 0, 0, 0, 0, 0.1],
                   'RingInner': [0, 0, 0, 0, 0, 0, 0.12], 'RingOuter': [0, 0, 0, 0, 0, 0, 0.12],
                   'Mantle.R0': [-4, -18, 0], 'Mantle.R1': [-4, -14, 0], 'Mantle.R2': [-4, -10, 0]}))
CLASP = mirror(P(**{'Hips': [6, 0, 0, 0, 0, -0.15], 'Chest': [8, 0, 0], 'head': [10, 0, 0], 'UpperArm.R': [-62, -18, -40],
                    'Forearm.R': [-30, 0, -35], 'Hand.R': [-40, 0, -30, 0, -0.35, 0.45], 'Halo': [0, 0, 0, 0, 0, 0, -0.08],
                    'RingInner': [0, 0, 0, 0, 0, 0, -0.42], 'RingOuter': [0, 0, 0, 0, 0, 0, -0.45],
                    'Mantle.R0': [8, 10, 0], 'Mantle.R1': [8, 8, 0], 'Mantle.R2': [8, 6, 0]}))
# Mort : sursaut, puis l'Archonte s'affaisse et retourne dans la faille ; halo, mains et anneaux tombent
RECOIL = mirror(P(**{'Hips': [-8, 0, 0, 0, 0, 0.3], 'Chest': [-14, 0, 0], 'head': [-28, 0, 0],
                     'UpperArm.R': [10, -45, 0], 'Forearm.R': [0, -15, 0], 'Hand.R': [0, 0, 0, 0.3, 0, 0.5]}))
FALL = mirror(P(**{'Hips': [24, 0, 4, 0, 0, -2.3], 'Chest': [12, 0, 0], 'head': [38, 0, 6], 'Tail': [-10, 0, 0],
                   'Halo': [40, 25, 0, 0, 0.6, -0.8], 'UpperArm.R': [12, 14, 0], 'Forearm.R': [16, 0, 0],
                   'Hand.R': [70, 0, 30, 0.2, -0.3, -0.8], 'Mantle.R0': [20, 22, 0], 'Mantle.R1': [24, 20, 0], 'Mantle.R2': [28, 18, 0],
                   'RingInner': [-12, -6, 0, 0, 0, -0.9], 'RingOuter': [8, 4, 0, 0, 0, -0.95], 'Fragments': [0, 0, 0, 0, 0, -0.42]}))
FALL['Hand.L'] = [60, 0, -40, -0.3, 0.2, -0.75, 0, 0]

def clip_idle(t): return life(t, 1.0, (2, 2))
def clip_coiled(t):
    w = 2 * math.pi * t
    tremble = P(**{'Chest': [0.8 * S(6 * w), 0.6 * S(5 * w + 1), 0], 'Hand.R': [4 * S(4 * w), 0, 0], 'Hand.L': [4 * S(4 * w + 2), 0, 0]})
    return add(add(CHARGE, life(t, 0.5, (4, 4), 1)), tremble)
def clip_slither(t): return add(GLIDE, life(t, 0.6, (3, 3), 1))
def clip_bite(t):
    base = timeline([(0, ZERO), (0.28, WIND), (0.42, STRIKE), (0.6, STRIKE), (1.0, ZERO)], t)
    return add(base, life(t, 0.3, (1, 1)))
def clip_dance(t):
    base = timeline([(0, OPEN), (0.35, OPEN), (0.55, CLASP), (0.72, CLASP), (1.0, OPEN)], t)
    return add(base, life(t, 0.4, (3, 3), 1))
def clip_death(t):
    return timeline([(0, ZERO), (0.16, RECOIL), (0.85, FALL), (1.0, FALL)], t)

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
report = []
for name, dur, fn, loop in CLIPS:
    act = bpy.data.actions.new('Archon_' + name); act.use_fake_user = True
    rig.animation_data.action = act
    N = int(round(dur * FPS)); minz = 99
    for f in range(N + 1):
        t = f / N
        apply_pose(fn(t % 1.0 if (loop and f < N) else (0.0 if loop else t)))
        for pb in rig.pose.bones:
            pb.keyframe_insert('rotation_quaternion', frame=f); pb.keyframe_insert('location', frame=f); pb.keyframe_insert('scale', frame=f)
        if f % 6 == 0 and name != 'Death':
            bpy.context.view_layer.update()
            minz = min(minz, min((rig.matrix_world @ pb.head).z for pb in rig.pose.bones if pb.name.startswith(('Hand', 'Fragments'))))
    act.frame_range = (0, N)
    report.append('%s : %d images, plus bas (mains/fragments) %.2f m' % (name, N, minz))
rig.animation_data.action = bpy.data.actions['Archon_Idle']
bpy.context.scene.frame_start = 0; bpy.context.scene.frame_end = 240
print('\n'.join(report))
