import bpy, math
from mathutils import Vector, Matrix

# SQUELETTE + ANIMATIONS du Serpent v2 (2026-11-25, demande : « refaire toutes les animations de l'Animator, le plus
# précis possible, le plus de frames possible, mouvement ample et réussi, et le serpent doit TOUJOURS rester au milieu de
# ses animations (sinon en jeu ça fait un déplacement non calculé) »).
#
# Méthode : à chaque image on DESSINE la forme du corps (cap horizontal θ(q) et hauteur H(q) le long de la colonne,
# q = 0 queue -> 1 cou), on en déduit la position exacte de chaque articulation (longueur des os conservée), puis on
#  - tourne la forme pour que la tête regarde vers +Y (l'avant du boss) avec son balancement propre,
#  - la recale pour que son CENTRE DE MASSE (sections pondérées par r²) soit exactement sur l'origine.
# Les rotations locales des os s'en déduisent (matrices de repos). 60 images/s, une clé par image.
# Os : spine_00..spine_39 (queue -> cou), head, jaw (mâchoire inférieure, enfant de head).
FPS = 60
L = 28.0; HL = 2.8; RMAX = 0.82; RNECK = 0.6
S_NECK = L - HL; S_HINGE = S_NECK + 0.75; Z_HINGE = 0.34
NB = 40
NECK_END = S_NECK + 0.25

def sstep(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0); return t * t * (3 - 2 * t)

def body_r(s):
    t = s / S_NECK
    if t < 0.5: return 0.03 + (RMAX - 0.03) * math.sin(min(1.0, t / 0.5) * math.pi / 2) ** 0.85
    if t < 0.9:
        k = (t - 0.5) / 0.4; return RMAX - (RMAX - RNECK) * (k * k * (3 - 2 * k))
    return RNECK

def zc(s): return 0.558 * body_r(min(s, S_NECK)) + 0.004

mesh_ob = bpy.data.objects['SK_Serpent_Flat']
me = mesh_ob.data
# ---------------------------------------------------------------- origine au MILIEU du serpent (centre de masse au repos)
S_J = [NECK_END * i / NB for i in range(NB + 1)]
wts = [body_r((S_J[i] + S_J[i + 1]) / 2) ** 2 for i in range(NB)]
YMID = sum(w * (S_J[i] + S_J[i + 1]) / 2 for i, w in enumerate(wts)) / sum(wts)
if 'shifted' not in me:
    me.transform(Matrix.Translation((0, -YMID, 0))); me['shifted'] = YMID
print('origine au milieu : y =', round(YMID, 3))

for n in ('Serpent2_Rig',):
    if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n])
for a in list(bpy.data.actions):
    if a.name.startswith('Serpent2_'): bpy.data.actions.remove(a)

arm = bpy.data.armatures.new('Serpent2_Rig')
rig = bpy.data.objects.new('Serpent2_Rig', arm)
bpy.context.scene.collection.objects.link(rig)
rig.location = mesh_ob.location if mesh_ob.parent is None else mesh_ob.matrix_world.translation
bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
names = []; prev = None
for i in range(NB):
    b = arm.edit_bones.new('spine_%02d' % i)
    b.head = (0, S_J[i] - YMID, zc(S_J[i])); b.tail = (0, S_J[i + 1] - YMID, zc(S_J[i + 1])); b.roll = 0
    if prev: b.parent = prev; b.use_connect = True
    prev = b; names.append(b.name)
hb = arm.edit_bones.new('head'); hb.head = prev.tail; hb.tail = (0, L - YMID, prev.tail.z); hb.roll = 0
hb.parent = prev; hb.use_connect = True; names.append('head')
jb = arm.edit_bones.new('jaw'); jb.head = (0, S_HINGE - YMID, Z_HINGE * 0.6); jb.tail = (0, L - 0.2 - YMID, 0.1); jb.roll = 0
jb.parent = hb; jb.use_connect = False
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- poids
for vg in list(mesh_ob.vertex_groups): mesh_ob.vertex_groups.remove(vg)
groups = {n: mesh_ob.vertex_groups.new(name=n) for n in names + ['jaw']}
an = me.attributes['anchor'].data; pt = me.attributes['part'].data
centers = [(S_J[i] + S_J[i + 1]) / 2 for i in range(NB)]
buckets = {}
def add(bn, w, vi): buckets.setdefault((bn, round(w, 3)), []).append(vi)
for v in me.vertices:
    part = pt[v.index].value; a = an[v.index].value
    if part == 1: add('jaw', 1.0, v.index); continue
    if part == 2 or a >= NECK_END: add('head', 1.0, v.index); continue
    if a <= centers[0]: add(names[0], 1.0, v.index); continue
    j = min(NB - 2, max(0, int((a - centers[0]) / (centers[1] - centers[0]))))
    while j < NB - 2 and centers[j + 1] < a: j += 1
    t = min(1.0, max(0.0, (a - centers[j]) / (centers[j + 1] - centers[j])))
    if a > centers[-1]:       # entre le dernier os du cou et la tête
        t = min(1.0, (a - centers[-1]) / (NECK_END - centers[-1] + 0.6)); add(names[NB - 1], 1 - t, v.index); add('head', t, v.index); continue
    add(names[j], 1 - t, v.index); add(names[j + 1], t, v.index)
for (bn, w), ids in buckets.items():
    if w > 1e-4: groups[bn].add(ids, w, 'REPLACE')
for m in list(mesh_ob.modifiers):
    if m.type == 'ARMATURE': mesh_ob.modifiers.remove(m)
mod = mesh_ob.modifiers.new('Armature', 'ARMATURE'); mod.object = rig
mesh_ob.parent = rig; mesh_ob.matrix_parent_inverse.identity(); mesh_ob.location = (0, 0, 0)

# ---------------------------------------------------------------- forme -> articulations -> rotations locales
REST = {b.name: b.matrix_local.copy() for b in arm.bones}
LEN = [(Vector((0, S_J[i + 1], zc(S_J[i + 1]))) - Vector((0, S_J[i], zc(S_J[i])))).length for i in range(NB)]
Q = [S_J[i] / NECK_END for i in range(NB + 1)]

def basis(pos, d):
    d = d.normalized()
    x = d.cross(Vector((0, 0, 1)))
    if x.length < 1e-6: x = Vector((1, 0, 0))
    x.normalize(); z = x.cross(d).normalized()
    m = Matrix((x, d, z)).transposed().to_4x4(); m.translation = pos
    return m

def solve(yaw_fn, lift_fn, head_yaw, head_pitch, jaw, face_yaw):
    # 1) chemin : hauteur imposée (repos + levée), cap horizontal imposé, longueur des os conservée
    J = [Vector((0, 0, zc(0) + lift_fn(0.0)))]
    for i in range(NB):
        qm = (Q[i] + Q[i + 1]) * 0.5
        z1 = zc(S_J[i + 1]) + lift_fn(Q[i + 1])
        dz = max(-0.95 * LEN[i], min(0.95 * LEN[i], z1 - J[i].z))
        h = math.sqrt(LEN[i] ** 2 - dz * dz)
        th = yaw_fn(qm)
        J.append(Vector((J[i].x - h * math.sin(th), J[i].y + h * math.cos(th), J[i].z + dz)))
    # 2) la tête regarde vers +Y (+ son balancement) : rotation de toute la forme autour de Z
    neck = J[NB] - J[NB - 1]; raw = math.atan2(-neck.x, neck.y)
    R = Matrix.Rotation(face_yaw - raw, 3, 'Z')
    J = [R @ p for p in J]
    # 3) centre de masse (sections pondérées par r²) recalé sur l'origine, dans le plan du sol
    cx = sum(wts[i] * (J[i].x + J[i + 1].x) / 2 for i in range(NB)) / sum(wts)
    cy = sum(wts[i] * (J[i].y + J[i + 1].y) / 2 for i in range(NB)) / sum(wts)
    J = [p - Vector((cx, cy, 0)) for p in J]
    # 4) matrices de pose (repère armature) puis bases locales
    pose = {}
    for i in range(NB): pose[names[i]] = basis(J[i], J[i + 1] - J[i])
    nd = J[NB] - J[NB - 1]; ny = math.atan2(-nd.x, nd.y) + head_yaw
    hd = Vector((-math.sin(ny) * math.cos(head_pitch), math.cos(ny) * math.cos(head_pitch), math.sin(head_pitch)))
    pose['head'] = basis(J[NB], hd)
    local = {}
    for i, n in enumerate(names):
        if i == 0: local[n] = REST[n].inverted() @ pose[n]
        else:
            p = names[i - 1]
            local[n] = (REST[p].inverted() @ REST[n]).inverted() @ pose[p].inverted() @ pose[n]
    local['jaw'] = Matrix.Rotation(jaw, 4, 'X')
    return local, J

# ---------------------------------------------------------------- les 6 animations (t = 0..1 sur la durée)
def S_wave(q, amp, k, phase):
    env = 1 - sstep(0.68, 0.93, q)          # l'avant dressé regarde droit devant
    return amp * math.sin(2 * math.pi * (k * q + phase)) * env

def rear(q, hmax, q0=0.7):
    return hmax * sstep(q0, 1.0, q) ** 1.5

def idle(t):
    br = math.sin(2 * math.pi * t)
    y = lambda q: S_wave(q, 0.55, 1.5, 0.15 - t)
    l = lambda q: rear(q, 2.6 + 0.15 * br)
    return y, l, math.radians(6) * math.sin(2 * math.pi * t + 0.8), math.radians(-8 + 3 * br), math.radians(3) * (0.5 + 0.5 * math.sin(4 * math.pi * t)), 0.0

def slither(t):
    y = lambda q: 0.72 * math.sin(2 * math.pi * (1.8 * q - t)) * (1 - 0.75 * sstep(0.75, 0.95, q))
    l = lambda q: rear(q, 1.1 + 0.1 * math.sin(4 * math.pi * t), 0.78)
    return y, l, math.radians(-10) * math.sin(2 * math.pi * t), math.radians(-4), math.radians(2), math.radians(4) * math.sin(2 * math.pi * t)

def bite(t):
    # 0-0,45 : se ramasse (cou en S serré, tête haute, gueule grande ouverte) ; 0,45-0,6 : frappe (le cou se déplie, la
    # tête plonge, la gueule claque) ; 0,6-1 : retour à la pose de repos.
    wind = sstep(0.0, 0.42, t) * (1 - sstep(0.45, 0.56, t))
    strike = sstep(0.45, 0.56, t) * (1 - sstep(0.66, 1.0, t))
    base_y, base_l, *_ = idle(0.0)
    def y(q):
        neckS = 0.75 * math.sin(2 * math.pi * 2.2 * (q - 0.72)) * sstep(0.7, 0.8, q) * (1 - sstep(0.93, 1.0, q))
        return base_y(q) * (1 - 0.3 * wind) + neckS * wind
    def l(q):
        return base_l(q) + rear(q, 1.4, 0.72) * wind - rear(q, 1.9, 0.75) * strike
    hp = math.radians(-8) + math.radians(24) * wind - math.radians(26) * strike
    snap = sstep(0.5, 0.57, t) * (1 - sstep(0.68, 0.85, t))
    jaw = math.radians(-14) * wind + math.radians(28) * snap
    return y, l, 0.0, hp, jaw, 0.0

def coiled(t):
    br = math.sin(2 * math.pi * t)
    TH = 2 * math.pi * 1.15 * (1 + 0.03 * br)
    def y(q):
        if q < 0.78: return TH * (q / 0.78) ** 1.35
        return TH
    l = lambda q: rear(q, 3.2 + 0.2 * br, 0.76)
    trem = math.radians(1.5) * math.sin(2 * math.pi * 6 * t)
    return y, l, math.radians(8) * math.sin(2 * math.pi * t) + trem, math.radians(-14 + 4 * br), math.radians(-6) + math.radians(4) * math.sin(2 * math.pi * 2 * t), 0.0

def dance(t):
    sw = math.sin(2 * math.pi * t)
    TH = 2 * math.pi * 0.7
    def y(q):
        base = TH * (q / 0.66) ** 1.3 if q < 0.66 else TH
        sway = 0.55 * sw * sstep(0.62, 1.0, q) + 0.25 * math.sin(2 * math.pi * (2 * q - t)) * sstep(0.62, 0.9, q)
        return base + sway
    l = lambda q: rear(q, 4.0 + 0.25 * math.sin(4 * math.pi * t), 0.62)
    return y, l, -0.6 * 0.55 * sw, math.radians(-16), math.radians(-8) + math.radians(6) * (0.5 + 0.5 * math.sin(4 * math.pi * t)), 0.0

def death(t):
    up = sstep(0.0, 0.18, t) * (1 - sstep(0.2, 0.42, t))
    fall = sstep(0.22, 0.48, t)
    bounce = 0.25 * math.sin(math.pi * sstep(0.48, 0.62, t)) if 0.48 < t < 0.62 else 0.0
    shiver = math.radians(4) * math.sin(2 * math.pi * 7 * t) * (1 - sstep(0.4, 0.85, t))
    base_y, base_l, *_ = idle(0.0)
    y = lambda q: base_y(q) * (1 - 0.55 * fall) + shiver * (1 - q)
    l = lambda q: base_l(q) * (1 - fall) + rear(q, 0.8, 0.72) * up + rear(q, bounce, 0.8)
    hp = math.radians(-8) * (1 - fall) + math.radians(18) * up
    jaw = math.radians(-12) * up * (1 - fall) + math.radians(8) * fall
    return y, l, math.radians(28) * fall, hp, jaw, 0.0

CLIPS = [('Idle', 3.0, idle, True), ('Slither', 2.0, slither, True), ('Bite', 1.4, bite, False),
         ('Coiled', 2.0, coiled, True), ('Dance', 2.4, dance, True), ('Death', 2.5, death, False)]

bpy.context.scene.render.fps = FPS
rig.animation_data_create()
for pb in rig.pose.bones: pb.rotation_mode = 'QUATERNION'
report = []
for name, dur, fn, loop in CLIPS:
    act = bpy.data.actions.new('Serpent2_' + name); act.use_fake_user = True
    rig.animation_data.action = act
    N = int(round(dur * FPS))
    minz = 9; maxr = 0
    for f in range(N + 1):
        t = f / N
        y, l, hy, hp, jaw, fy = fn(t if not loop else t % 1.0)
        local, J = solve(y, l, hy, hp, jaw, fy)
        for n, m in local.items():
            pb = rig.pose.bones[n]
            pb.rotation_quaternion = m.to_quaternion()
            pb.keyframe_insert('rotation_quaternion', frame=f)
            if n == names[0]:
                pb.location = m.translation; pb.keyframe_insert('location', frame=f)
        for i in range(NB):
            minz = min(minz, J[i].z - 0.558 * body_r(S_J[i]) )
        maxr = max(maxr, max(math.hypot(p.x, p.y) for p in J))
    act.frame_range = (0, N)
    report.append('%s : %d images, bas du corps min %.3f, rayon max %.1f m' % (name, N, minz, maxr))
rig.animation_data.action = bpy.data.actions['Serpent2_Idle']
bpy.context.scene.frame_start = 0; bpy.context.scene.frame_end = 180
bpy.ops.wm.save_mainfile()
print('\n'.join(report))
