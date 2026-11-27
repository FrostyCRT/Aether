import bpy, math
from mathutils import Vector

# Squelette + animations du Serpent de la Faille (2026-11-25).
# Chaîne de NB os le long de +Y (queue -> cou) + os « head ». Os orientés +Y, roulis 0 : X local = X monde (tangage,
# + = relève), Z local = Z monde (flexion latérale). Poids : attribut 'anchor' de build_serpent.py (point de la colonne
# auquel le sommet est rattaché) -> mélange linéaire entre les deux os dont le centre encadre l'ancrage.
# Animations (30 i/s), noms = états de l'Animator « Snake » : Idle, Slither, Bite, Coiled, Dance, Death.
L = 15.0; HEAD = 1.75; RNECK = 0.52; RMAX = 0.72
NB = 30
NECK_END = L - HEAD * 0.85
FPS = 30

def center_z(s):
    t = s / L
    if t < 0.62:
        r = 0.04 + (RMAX - 0.04) * math.sin(min(1.0, t / 0.62) * math.pi / 2) ** 0.9
    elif t < 0.86:
        k = (t - 0.62) / 0.24; r = RMAX - (RMAX - RNECK) * (k * k * (3 - 2 * k))
    else:
        r = RNECK
    return r * 0.62

mesh_ob = bpy.data.objects['SK_Serpent_Rest']
for name in ('Serpent_Rig',):
    if name in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[name])
for a in list(bpy.data.actions):
    if a.name.startswith('Serpent_'): bpy.data.actions.remove(a)

arm = bpy.data.armatures.new('Serpent_Rig')
rig = bpy.data.objects.new('Serpent_Rig', arm)
bpy.context.scene.collection.objects.link(rig)
rig.location = mesh_ob.location
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
stations = [NECK_END * i / NB for i in range(NB + 1)]
prev = None; names = []
for i in range(NB):
    b = arm.edit_bones.new('spine_%02d' % i)
    b.head = (0, stations[i], center_z(stations[i])); b.tail = (0, stations[i + 1], center_z(stations[i + 1]))
    b.roll = 0
    if prev: b.parent = prev; b.use_connect = True
    prev = b; names.append(b.name)
hb = arm.edit_bones.new('head')
hb.head = prev.tail; hb.tail = (0, L, prev.tail.z); hb.roll = 0; hb.parent = prev; hb.use_connect = True
names.append('head')
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------- poids ----------------
centers = [(stations[i] + stations[i + 1]) * 0.5 for i in range(NB)] + [NECK_END + (L - NECK_END) * 0.5]
for vg in list(mesh_ob.vertex_groups): mesh_ob.vertex_groups.remove(vg)
groups = [mesh_ob.vertex_groups.new(name=n) for n in names]
anch = mesh_ob.data.attributes['anchor'].data
idx_by_bone = {}
for v in mesh_ob.data.vertices:
    a = anch[v.index].value
    if a >= NECK_END:
        w = [(NB, 1.0)]
    elif a <= centers[0]:
        w = [(0, 1.0)]
    else:
        j = 0
        while j < NB - 1 and centers[j + 1] < a: j += 1
        c0 = centers[j]; c1 = centers[j + 1]
        t = (a - c0) / (c1 - c0)
        w = [(j, 1 - t), (j + 1, t)]
    for bi, ww in w:
        if ww > 1e-4: idx_by_bone.setdefault((bi, round(ww, 4)), []).append(v.index)
for (bi, ww), ids in idx_by_bone.items(): groups[bi].add(ids, ww, 'REPLACE')
for m in list(mesh_ob.modifiers):
    if m.type == 'ARMATURE': mesh_ob.modifiers.remove(m)
mod = mesh_ob.modifiers.new('Armature', 'ARMATURE'); mod.object = rig
mesh_ob.parent = rig; mesh_ob.location = (0, 0, 0)

# ---------------- animations ----------------
def frac(i): return i / (NB - 1)       # 0 queue -> 1 cou

def rear(i, height):
    # cou dressé : les derniers os se relèvent puis se recourbent pour garder la tête à l'horizontale
    k = NB - 1 - i
    if k < 3: return -0.16 * height
    if k < 7: return 0.30 * height
    return 0.0

def wave(i, t, amp, cycles_body, speed, phase=0.0, neck_calm=True):
    # Onde latérale définie par la FORME du corps : x(s) = A(s) sin(2π(cycles·s/Lc - speed·t)), en mètres (la queue ondule
    # plus que l'avant). L'angle local de l'os i = angle de la tangente au début de l'os i - celui de l'os précédent : la
    # forme obtenue est exactement celle voulue, sans dérive cumulée (amp = amplitude latérale en mètres).
    def theta(j):
        s = stations[j]; ds = 0.05
        def x(ss):
            a = amp * (0.45 + 0.55 * (1 - ss / NECK_END))
            if neck_calm: a *= 1 - 0.85 * max(0.0, (ss / NECK_END - 0.75) / 0.25)
            return a * math.sin(2 * math.pi * (cycles_body * ss / NECK_END - speed * t) + phase)
        return math.atan((x(s + ds) - x(s - ds)) / (2 * ds))
    return theta(i) - (theta(i - 1) if i > 0 else 0.0)

def pose_idle(t, T):
    p = {}
    for i in range(NB):
        p[i] = (rear(i, 1.0), wave(i, t / T, 0.35, 1.5, 1.0))
    p['head'] = (math.radians(-6) + math.radians(3) * math.sin(2 * math.pi * t / T), math.radians(5) * math.sin(2 * math.pi * t / T + 1.0))
    return p

def pose_slither(t, T):
    p = {}
    for i in range(NB):
        p[i] = (rear(i, 0.55), wave(i, t / T, 1.0, 1.6, 1.0))
    p['head'] = (math.radians(-4), math.radians(-8) * math.sin(2 * math.pi * t / T))
    return p

def pose_dance(t, T):
    p = {}
    sway = math.sin(2 * math.pi * t / T)
    for i in range(NB):
        k = NB - 1 - i
        lat = math.radians(3.5) * sway if k < 9 else 0.0
        p[i] = (rear(i, 1.35), lat + wave(i, t / T, 0.3, 1.0, 1.0))
    p['head'] = (math.radians(-10), -math.radians(12) * sway)
    return p

def pose_coiled(t, T):
    p = {}
    for i in range(NB):
        curl = math.radians(4 + 17 * frac(i) ** 1.3) if i < NB - 7 else 0.0   # spirale qui se resserre vers l'avant : la tête se dresse au centre
        trem = math.radians(1.2) * math.sin(2 * math.pi * 6 * t / T + i * 0.7)
        p[i] = (rear(i, 1.45), curl + trem)
    p['head'] = (math.radians(-14), math.radians(2) * math.sin(2 * math.pi * 3 * t / T))
    return p

def pose_bite(t, T):
    u = t / T
    base = pose_idle(0, 1)
    # 0-0,4 : se ramasse (cou plus cambré, tête relevée) ; 0,4-0,55 : frappe (cou se déplie vers l'avant-bas) ; puis retour
    def ease(x): x = min(max(x, 0), 1); return x * x * (3 - 2 * x)
    load = ease(u / 0.4) * (1 - ease((u - 0.4) / 0.15))
    strike = ease((u - 0.4) / 0.15) * (1 - ease((u - 0.62) / 0.38))
    p = {}
    for i in range(NB):
        k = NB - 1 - i
        rx, rz = base[i]
        if k < 7:
            rx += math.radians(9) * load * (1 if k >= 3 else -1.4) - math.radians(13) * strike * (1 if k >= 3 else -0.4)
        p[i] = (rx, rz)
    p['head'] = (base['head'][0] + math.radians(22) * load - math.radians(28) * strike, 0.0)
    return p

def pose_death(t, T):
    u = min(t / (T * 0.8), 1.0)
    e = u * u * (3 - 2 * u)
    a = pose_idle(0, 1)
    p = {}
    for i in range(NB):
        rx, rz = a[i]
        settle = wave(i, 0.0, 0.6, 1.3, 0.0, neck_calm=False)
        p[i] = (rx * (1 - e), rz * (1 - e) + settle * e)
    p['head'] = (a['head'][0] * (1 - e) + math.radians(4) * e, math.radians(35) * e)
    return p

CLIPS = [('Idle', 69, pose_idle, True), ('Slither', 56, pose_slither, True), ('Bite', 38, pose_bite, False),
         ('Coiled', 50, pose_coiled, True), ('Dance', 60, pose_dance, True), ('Death', 68, pose_death, False)]

bpy.context.scene.render.fps = FPS
rig.animation_data_create()
for pb in rig.pose.bones: pb.rotation_mode = 'XYZ'
for name, frames, fn, loop in CLIPS:
    act = bpy.data.actions.new('Serpent_' + name)
    act.use_fake_user = True
    rig.animation_data.action = act
    T = frames / FPS
    for f in range(0, frames + 1, 2 if frames > 40 else 1):
        t = f / FPS
        p = fn(t, T)
        for i in range(NB + 1):
            key = 'head' if i == NB else i
            pb = rig.pose.bones[names[i]]
            rx, rz = p[key]
            pb.rotation_euler = (rx, 0.0, rz)
            pb.keyframe_insert('rotation_euler', frame=f)
    act.frame_range = (0, frames)
rig.animation_data.action = bpy.data.actions['Serpent_Idle']
bpy.context.scene.frame_start = 0; bpy.context.scene.frame_end = 68
bpy.ops.wm.save_mainfile()
print('bones', len(arm.bones), 'actions', [a.name for a in bpy.data.actions if a.name.startswith('Serpent_')])
