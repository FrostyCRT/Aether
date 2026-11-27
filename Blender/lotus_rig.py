import bpy, math, random, json
from mathutils import Vector, Matrix, Quaternion, Euler

# Boss 3 v4 : LOTUS DE LA SOURCE - squelette, poids, couleurs des sommets, 6 animations (2026-11-26).
# À lancer après lotus_build.py. Os orientés +Z ; poses = rotations dans le monde de repos.
#  - chaque pétale : chaîne de 3 os le long de sa nervure (ouverture/fermeture = rotation autour de son axe transverse) ;
#  - Heart (cœur de cristal), Pod (réceptacle + étamines), 6 racines (chaînes de 3 os), Body (calice, corruption).
#  - Noms d'états = ceux de l'Animator du boss 3 (Idle, Coiled, Slither, Bite, Dance, Death). Clips SUR PLACE.
#  - Couleur 'Col' : facteur t du dégradé (pétales) pour ArchonSoft ; barycentriques sur le cristal et la corruption.
OX = 80.0
FPS = 60
rnd = random.Random(9)
src = bpy.data.collections['Lotus']
col = bpy.data.collections.get('LotusRig')
if col is None:
    col = bpy.data.collections.new('LotusRig'); bpy.context.scene.collection.children.link(col)
for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
for a in list(bpy.data.actions):
    if a.name.startswith('Lotus_'): bpy.data.actions.remove(a)
for a in list(bpy.data.armatures):
    if a.users == 0: bpy.data.armatures.remove(a)
def V(*a): return Vector(a)

# ---------------------------------------------------------------- os
BONES = [('Root', V(0, 0, 0), None), ('Body', V(0, 0, 1.6), 'Root'), ('Pod', V(0, 0, 2.0), 'Body'), ('Heart', V(0, 0, 2.25), 'Body'),
         ('head', V(0, 0, 2.6), 'Heart')]
PT = (0.0, 0.4, 0.72)
CH = {}          # objet -> (courbe, [os], [t])
PET = []         # (nom de base, couronne, azimut, axe transverse)
for o in src.objects:
    if o.name.startswith('Lotus_Petal_'):
        C = [Vector(q) for q in json.loads(o['pts'])]; wi, k = o['whorl'], o.name.split('_')[-1]
        a = o['azimuth']; names = []
        for i, t in enumerate(PT):
            n = 'P%d_%s_%d' % (wi, k, i); BONES.append((n, C[int(round(t * (len(C) - 1)))], names[-1] if names else 'Body')); names.append(n)
        CH[o.name] = (C, names, PT); PET.append(('P%d_%s' % (wi, k), wi, a, V(-math.sin(a), math.cos(a), 0)))
ROOT_CURVES = json.loads(src.objects['Lotus_Roots']['roots'])
RC = []
for ri, P in enumerate(ROOT_CURVES):
    P = [Vector(q) for q in P]; names = []
    for i, idx in enumerate((0, 2, 3)):
        n = 'R%d_%d' % (ri, i); BONES.append((n, P[idx], names[-1] if names else 'Body')); names.append(n)
    dense = []
    for i in range(len(P) - 1):
        for q in range(10): dense.append((P[i].lerp(P[i + 1], q / 10), (i + q / 10) / (len(P) - 1)))
    RC.append((dense, names))

arm_data = bpy.data.armatures.new('Lotus_Armature')
rig = bpy.data.objects.new('Lotus_Rig', arm_data); col.objects.link(rig); rig.location = (OX, 0, 0)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for n, p, par in BONES:
    b = arm_data.edit_bones.new(n); b.head = p; b.tail = p + V(0, 0, 0.2); b.roll = 0
    if par: b.parent = arm_data.edit_bones[par]
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- pièces, poids, couleurs
CRYSTAL = ('Lotus_Crystal', 'Lotus_Corrupt')
CORRUPT_SIDE = math.radians(205)
def corruption(o, mname, t, s):
    return 0.0   # corruption retirée de la fleur (2026-11-27)
    if mname == 'Lotus_PetalCorrupt': return 1.0
    if not o.name.startswith('Lotus_Petal_'): return 0.0
    a = o['azimuth']; d = (CORRUPT_SIDE - a + math.pi) % (2 * math.pi) - math.pi     # écart signé vers le côté corrompu
    reach = 1.0 if o['whorl'] == 0 else 0.8
    k = max(0.0, 1.0 - abs(d) / reach)
    if k <= 0: return 0.0
    toward = s * (1.0 if d > 0 else -1.0)                                           # bord du pétale tourné vers la corruption
    side = min(1.0, max(0.0, (toward + 0.4) / 1.2))
    return min(1.0, k * 0.85 * side * min(1.0, max(0.0, (t - 0.1) / 0.6)))
pieces = []
dg = bpy.context.evaluated_depsgraph_get()
for o in src.objects:
    if o.type != 'MESH': continue
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    c = bpy.data.objects.new('P_' + o.name, me); col.objects.link(c); c.location = (OX, 0, 0)
    for bn, _, _ in BONES: c.vertex_groups.new(name=bn)
    mname = me.materials[0].name if me.materials else ''
    tattr = me.attributes.get('t'); sattr = me.attributes.get('s')
    vt = {}; vs = {}; pid = rnd.random()
    for v in me.vertices:
        p = v.co
        if o.name in CH:
            C, names, Ts = CH[o.name]
            i = min(range(len(C)), key=lambda q: (C[q] - p).length_squared); t = i / (len(C) - 1)
            for k in range(len(Ts)):
                t0 = Ts[k]; t1 = Ts[k + 1] if k + 1 < len(Ts) else 9.0
                if t0 <= t < t1 or (k == len(Ts) - 1 and t >= t0):
                    f = 0.0 if t1 > 1.5 else (t - t0) / (t1 - t0)
                    c.vertex_groups[names[k]].add([v.index], 1 - f, 'REPLACE')
                    if f > 0: c.vertex_groups[names[k + 1]].add([v.index], f, 'REPLACE')
                    break
        elif o.name == 'Lotus_Roots':
            best = None
            for dense, names in RC:
                for q, tt in dense:
                    d = (q - p).length_squared
                    if best is None or d < best[0]: best = (d, names, tt)
            _, names, tt = best
            seg = [0.0, 0.5, 0.75]
            for k in range(3):
                t0 = seg[k]; t1 = seg[k + 1] if k < 2 else 9.0
                if t0 <= tt < t1 or k == 2:
                    f = 0.0 if t1 > 1.5 else (tt - t0) / (t1 - t0)
                    c.vertex_groups[names[k]].add([v.index], 1 - f, 'REPLACE')
                    if f > 0: c.vertex_groups[names[k + 1]].add([v.index], f, 'REPLACE')
                    break
        elif o.name == 'Lotus_Heart':
            c.vertex_groups['Heart'].add([v.index], 1.0, 'REPLACE')
        elif o.name in ('Lotus_Pod', 'Lotus_Seeds', 'Lotus_Stamens'):
            c.vertex_groups['Pod'].add([v.index], 1.0, 'REPLACE')
        else:
            c.vertex_groups['Body'].add([v.index], 1.0, 'REPLACE')
        if tattr is not None: vt[v.index] = tattr.data[v.index].value
        if sattr is not None: vs[v.index] = sattr.data[v.index].value
    cattr = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
    for poly in me.polygons:
        li = list(poly.loop_indices); fid = rnd.random()
        if mname in CRYSTAL:
            pat = ((0, 1, 1), (1, 0, 1), (1, 0, 1), (0, 1, 1)) if len(li) == 4 else [((1, 0, 0), (0, 1, 0), (0, 0, 1))[k % 3] for k in range(len(li))]
            for k, l in enumerate(li): cattr.data[l].color = tuple(pat[k]) + (fid,)
        else:
            # R = t (base -> pointe), G = position en travers (0..1, 0,5 = nervure centrale), B = identité du pétale,
            # A = CORRUPTION (1 = pétale corrompu ; décroît sur les pétales voisins, du côté qui les touche et vers la pointe)
            for l in li:
                vi = me.loops[l].vertex_index; tt = vt.get(vi, 0.0); ss = vs.get(vi, 0.0)
                cattr.data[l].color = (tt, ss * 0.5 + 0.5, pid, corruption(o, mname, tt, ss))
    pieces.append(c)
with bpy.context.temp_override(active_object=pieces[0], selected_editable_objects=pieces, object=pieces[0]):
    bpy.ops.object.join()
body = pieces[0]; body.name = 'SK_Lotus'; body.data.name = 'SK_Lotus'
body.parent = rig; body.location = (0, 0, 0)
md = body.modifiers.new('Armature', 'ARMATURE'); md.object = rig

# ---------------------------------------------------------------- poses
REST_Q = {b.name: b.matrix_local.to_quaternion() for b in arm_data.bones}
S = math.sin
def ease(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)
def keyed(keys, t):
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t0 <= t <= t1: return v0 + (v1 - v0) * ease((t - t0) / (t1 - t0))
    return keys[-1][1]

def pose(t, open_=(0, 0, 0), wave=1.0, speed=1, body_rx=0.0, body_z=0.0, heart_up=0.0, heart_spin=1.0, heart_scale=0.0,
         tremble=0.0, root_amp=1.0, front_snap=0.0, droop=(0, 0, 0), body_scale=0.0):
    """open_[couronne] : degrés d'ouverture (+ = vers le bas / ouvert, - = refermé vers le haut)."""
    w = 2 * math.pi * t * speed; P = {}
    P['Body'] = (Euler((math.radians(body_rx + 1.2 * wave * S(w)), 0, math.radians(1.5 * wave * S(w + 1))), 'XYZ').to_quaternion(), V(0, 0, body_z + 0.12 * wave * S(w)), body_scale)
    P['Heart'] = (Quaternion(V(0, 0, 1), math.radians(45 * heart_spin * t * (1 if speed else 0))), V(0, 0, heart_up + 0.08 * S(w * 2)), heart_scale + 0.03 * S(w * 2))
    P['Pod'] = (Quaternion(), V(0, 0, 0), 0.02 * S(w * 2 + 1))
    for name, wi, a, side in PET:
        base = open_[wi] + droop[wi] + wave * (2.5 + 1.5 * wi) * S(w + a * 2) + tremble * S(w * 7 + a * 3)
        # pétales extérieurs côté avant (-Y) : claquent vers l'avant à la Frappe
        if front_snap and wi == 0: base += front_snap * max(0.0, -math.sin(a)) ** 2
        for i in range(3):
            ang = base * (0.65, 0.25, 0.15)[i] + droop[wi] * (0, 0.35, 0.5)[i]
            P['%s_%d' % (name, i)] = (Quaternion(side, math.radians(ang)), V(0, 0, 0), 0.0)
    for ri in range(len(RC)):
        aa = 2 * math.pi * ri / 6 + 0.5; side = V(-math.sin(aa), math.cos(aa), 0)
        for i in range(3):
            P['R%d_%d' % (ri, i)] = (Quaternion(side, math.radians(root_amp * 6 * S(w * 2 - i * 1.1 + ri))) @ Quaternion(V(0, 0, 1), math.radians(root_amp * 5 * S(w + ri - i))), V(0, 0, 0), 0.0)
    return P

def clip_idle(t): return pose(t)
def clip_coiled(t): return pose(t, open_=(14, 24, 30), wave=0.5, speed=2, heart_up=0.45, heart_spin=4, heart_scale=0.12, tremble=1.2)
def clip_slither(t): return pose(t, open_=(-22, -18, -8), wave=0.6, speed=2, body_rx=7, root_amp=1.5)
def clip_bite(t):
    o = keyed([(0, 0), (0.3, -38), (0.45, 10), (0.6, 10), (1.0, 0)], t)
    rx = keyed([(0, 0), (0.3, -8), (0.45, 9), (0.6, 9), (1.0, 0)], t)
    return pose(t, open_=(o, o * 0.7, o * 0.4), wave=0.3, body_rx=rx, front_snap=keyed([(0, 0), (0.3, -20), (0.45, 12), (0.6, 12), (1.0, 0)], t))
def clip_dance(t):
    k = keyed([(0, 0.0), (0.35, 0.0), (0.55, 1.0), (0.72, 1.0), (1.0, 0.0)], t)
    o = (14 + (-72 - 14) * k, 24 + (-58 - 24) * k, 30 + (-38 - 30) * k)
    return pose(t, open_=o, wave=0.4, speed=1, heart_up=0.3 * (1 - k), heart_spin=3, heart_scale=0.15 * (1 - k) - 0.1 * k)
def clip_death(t):
    k = keyed([(0, 0.0), (0.15, -0.3), (0.85, 1.0), (1.0, 1.0)], t)
    return pose(t, open_=(0, 0, 0), wave=0.3 * (1 - t), droop=(5 * max(k, 0), 50 * max(k, 0), 60 * max(k, 0)), body_z=-0.15 * max(k, 0),
                heart_up=-0.6 * max(k, 0), heart_spin=0.5, heart_scale=-0.35 * max(k, 0), root_amp=1 - t, body_rx=6 * max(k, 0))

CLIPS = [('Idle', 4.0, clip_idle, True), ('Coiled', 2.0, clip_coiled, True), ('Slither', 2.0, clip_slither, True),
         ('Bite', 1.4, clip_bite, False), ('Dance', 2.4, clip_dance, True), ('Death', 2.5, clip_death, False)]

def apply(P):
    for pb in rig.pose.bones:
        q, loc, sc = P.get(pb.name, (Quaternion(), V(0, 0, 0), 0.0))
        r = REST_Q[pb.name]
        pb.rotation_quaternion = r.inverted() @ q @ r
        pb.location = r.inverted() @ loc
        s = 1.0 + sc; pb.scale = (s, s, s)

bpy.context.scene.render.fps = FPS
rig.animation_data_create()
for pb in rig.pose.bones: pb.rotation_mode = 'QUATERNION'
report = []
for name, dur, fn, loop in CLIPS:
    act = bpy.data.actions.new('Lotus_' + name); act.use_fake_user = True
    rig.animation_data.action = act
    N = int(round(dur * FPS)); minz = 99
    for f in range(N + 1):
        t = f / N
        apply(fn(t % 1.0 if (loop and f < N) else (0.0 if loop else t)))
        for pb in rig.pose.bones:
            pb.keyframe_insert('rotation_quaternion', frame=f); pb.keyframe_insert('location', frame=f); pb.keyframe_insert('scale', frame=f)
        if f % 10 == 0 and name != 'Death':
            bpy.context.view_layer.update()
            for pb in rig.pose.bones:
                if pb.name.startswith('P') and pb.name.endswith('_2'): minz = min(minz, (rig.matrix_world @ pb.head).z)
    act.frame_range = (0, N)
    report.append('%s %d images, pétale le plus bas %.2f m' % (name, N, minz))
rig.animation_data.action = bpy.data.actions['Lotus_Idle']
bpy.context.scene.frame_start = 0; bpy.context.scene.frame_end = 240; bpy.context.scene.frame_set(0)
print('\n'.join(report)); print('os', len(BONES), 'triangles', sum(len(p.vertices) - 2 for p in body.data.polygons))
