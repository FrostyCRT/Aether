import bpy, bmesh, math, random
from mathutils import Vector

# Valse de lune (2026-11-25) : 1) SM_LyraDaggerCrystal = Lame croissant dont la lame (matériau 0) porte les
# « barycentriques » du shader Aether/PaintedCrystal dans la couleur des sommets (arêtes blanches le long des lignes de
# facettes, pas sur les diagonales) ; les autres matériaux gardent OutlineN (liseré de PaintedMetal).
# 2) SM_MoonShard = petit croissant de lune en cristal (charge du Kaléidoscope), un seul matériau.
# Encodage couleur : r = 0 sur la ligne de facette « début », g = 0 sur la ligne « fin », b = 0 sur les coupes
# transversales (une tous les CHUNK segments) ; a = identifiant de facette (nuance).

HERE = r"C:\Users\jules\My project\Aether Storm Survivor\Blender"
OUT = r"C:\Users\jules\My project\Aether Storm Survivor\Assets\Game\3D Models\Lot1"
src = open(HERE + r"\build_lyra_dagger.py", encoding="utf-8").read()
exec(src.split("name = 'SM_LyraDagger_v2'")[0])   # définit build(), STATIONS

def finish(name, bm, mats, colorfn):
    me = bpy.data.meshes.new(name + '_tmp')
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    attr = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
    for p in me.polygons:
        for li in p.loop_indices:
            attr.data[li].color = colorfn(me, p, me.loops[li])
    for mn in mats:
        me.materials.append(bpy.data.materials.get(mn) or bpy.data.materials.new(mn))
    ob = bpy.data.objects.get(name)
    if ob is None:
        ob = bpy.data.objects.new(name, me)
        bpy.data.objects['SM_LyraDagger'].users_collection[0].objects.link(ob)
    else:
        old = ob.data; ob.data = me; bpy.data.meshes.remove(old)
    me.name = name
    return ob

def export(ob, path):
    bpy.ops.object.select_all(action='DESELECT')
    ob.hide_set(False); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.sort_elements(type='MATERIAL', elements={'FACE'})
    bpy.ops.object.mode_set(mode='OBJECT')
    loc = ob.location.copy(); ob.location = (0, 0, 0)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, colors_type='LINEAR',
                             mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False,
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True)
    ob.location = loc

# ---------------- 1. Dague à lame de cristal ----------------
N = 44; y0, y1 = -0.07, 0.62; XS = 0.03; So, Si, B = 0.15, 0.06, 0.036; CHUNK = 7
def blade_uv(co):
    t = min(max((co.y - y0) / (y1 - y0), 0.0), 1.0)
    xo = XS - B * (1 - t) - So * math.sin(math.pi * t)
    xi = XS + B * (1 - t) - Si * math.sin(math.pi * t)
    w = xi - xo
    return t, ((co.x - xo) / w if w > 1e-4 else None)

rnd = random.Random(7)
fids = {}
def dagger_color(me, p, loop):
    v = me.vertices[loop.vertex_index]
    if p.material_index != 0:
        n = v.normal
        return (n.x * 0.5 + 0.5, n.y * 0.5 + 0.5, n.z * 0.5 + 0.5, 1)
    us = [blade_uv(me.vertices[me.loops[li].vertex_index].co)[1] for li in p.loop_indices]
    us = [u for u in us if u is not None]
    umin = min(us) if us else 0.0
    t, u = blade_uv(v.co)
    i = round(t * N)
    b = 0.0 if i % CHUNK == 0 else 1.0
    key = (round(umin * 20), int(t * N) // CHUNK)
    if key not in fids: fids[key] = rnd.random()
    if u is None: r, g = 0.5, 0.5
    elif abs(u - umin) < 1e-3: r, g = 0.0, 1.0
    else: r, g = 1.0, 0.0
    return (r, g, b, fids[key])

# build() recrée la dague ; les points de la lame et des autres pièces sont distingués par le matériau de la face
bm = build()
dob = finish('SM_LyraDaggerCrystal', bm, ('MD_0', 'MD_1', 'MD_2', 'MD_3'), dagger_color)
dob.location = (13.0, -3.0, 0)
export(dob, OUT + r"\SM_LyraDaggerCrystal.fbx")

# ---------------- 2. Croissant de lune en cristal ----------------
def build_moon():
    bm = bmesh.new()
    M = 36; A = math.radians(125); r1 = 0.17; T0 = 0.085; H = 0.032
    rows = []
    for j in range(M + 1):
        a = -A + 2 * A * j / M
        d = Vector((math.sin(a), -math.cos(a), 0))
        T = T0 * (math.cos(a / A * math.pi / 2) ** 0.7) + 0.003
        h = H * (math.cos(a / A * math.pi / 2) ** 0.5) + 0.002
        po = d * r1
        pm = po - d * (T * 0.5)
        pi_ = po - d * T
        rows.append([bm.verts.new((po.x, po.y, 0)), bm.verts.new((pm.x, pm.y, h)),
                     bm.verts.new((pi_.x, pi_.y, 0)), bm.verts.new((pm.x, pm.y, -h))])
    for j in range(M):
        for s in range(4):
            s2 = (s + 1) % 4
            bm.faces.new((rows[j][s], rows[j][s2], rows[j+1][s2], rows[j+1][s]))
    for j, flip in ((0, True), (M, False)):
        bm.faces.new(rows[j] if flip else list(reversed(rows[j])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm, M, A

mbm, MM, MA = build_moon()
mfid = {}
MCH = 6
def moon_color(me, p, loop):
    v = me.vertices[loop.vertex_index]
    co = v.co
    ang = math.atan2(co.x, -co.y)            # angle autour du centre
    j = round((ang + MA) / (2 * MA) * MM)
    if len(p.loop_indices) != 4:                # bouts des cornes
        return (0.5, 0.5, 1.0, 0.5)
    # section : 0 bord extérieur, 1 arête du dessus, 2 bord intérieur, 3 arête du dessous
    def sec(c):
        rr = math.hypot(c.x, c.y)
        if abs(c.z) > 1e-4: return 1 if c.z > 0 else 3
        return 0 if rr > 0.17 - 1e-4 else 2
    secs = [sec(me.vertices[me.loops[li].vertex_index].co) for li in p.loop_indices]
    first = min(secs) if not (0 in secs and 3 in secs) else 3
    s = sec(co)
    b = 0.0 if j % MCH == 0 else 1.0
    key = (first, j // MCH)
    if key not in mfid: mfid[key] = rnd.random()
    return ((0.0, 1.0) if s == first else (1.0, 0.0)) + (b, mfid[key])

mob = finish('SM_MoonShard', mbm, ('MD_Moon',), moon_color)
mob.location = (14.0, -3.0, 0)
export(mob, OUT + r"\SM_MoonShard.fbx")
bpy.ops.wm.save_mainfile()
print('dagger', len(dob.data.polygons), tuple(round(x, 3) for x in dob.dimensions),
      'moon', len(mob.data.polygons), tuple(round(x, 3) for x in mob.dimensions))
