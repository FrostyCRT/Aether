import bpy, bmesh, math, random
from mathutils import Vector, Matrix

# Projectiles des boss 1 et 2 (2026-11-25) : la forme vient du boss, le ROUGE porte le code danger commun.
#  - SM_DragonDart : dard d'écaille du Dragon de Mana (acier argenté, matériau 0) avec une nervure centrale rouge
#    incandescente (matériau 1). Pointe vers +Y (= avant du projectile dans Unity). Couleur des sommets = OutlineN
#    (normale lissée *0,5+0,5, repère Blender) pour le liseré de PaintedMetal.
#  - SM_DeerThorn : épine de bois de cristal du Cerf (une pointe + deux ramures), coque de cristal (matériau 0) et cœur
#    (matériau 1, plus petit, à l'intérieur). Couleur des sommets = barycentriques par triangle (arêtes de PaintedCrystal),
#    alpha = nuance de facette.
OUT = r"C:\Users\jules\My project\Aether Storm Survivor\Assets\Game\3D Models\Lot1"
rnd = random.Random(11)

def quad(bm, vs, m):
    f = bm.faces.new(vs); f.material_index = m; return f

def new_object(name, me, x):
    ob = bpy.data.objects.get(name)
    if ob is None:
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
    else:
        old = ob.data; ob.data = me; bpy.data.meshes.remove(old)
    me.name = name
    ob.location = (x, -6.0, 0)
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

# ---------------------------------------------------------------- 1. Dard du Dragon
def build_dart():
    bm = bmesh.new()
    N = 30; L0, L1 = -0.22, 0.30            # talon -> pointe (longueur 0,52)
    rows = []
    for i in range(N + 1):
        t = i / N
        y = L0 + (L1 - L0) * t
        # profil en pointe de flèche (triangle effilé, flancs légèrement creusés) : se lit « flèche » vu de dessus
        if t < 0.06:
            w = 0.03 + (0.12 - 0.03) * t / 0.06
        else:
            w = 0.12 * ((1 - t) / 0.94) ** 1.25
        h = 0.03 * (1 - t) ** 0.6 + 0.004 if t < 1 else 0.0
        w = max(w, 0.0)
        rows.append([bm.verts.new((-w, y, 0)), bm.verts.new((0, y, h)), bm.verts.new((w, y, 0)), bm.verts.new((0, y, -h))])
    for i in range(N):
        for s in range(4):
            s2 = (s + 1) % 4
            quad(bm, (rows[i][s], rows[i][s2], rows[i+1][s2], rows[i+1][s]), 0)
    quad(bm, list(reversed(rows[0])), 0)
    # nervure rouge : fine bande en relief sur les deux faces, du talon aux 3/4 de la lame
    for sign in (1, -1):
        M = 20; strip = []
        for i in range(M + 1):
            t = 0.05 + 0.65 * i / M
            y = L0 + (L1 - L0) * t
            h = (0.03 * (1 - t) ** 0.6 + 0.004) * sign
            wd = 0.012 * (1 - abs(2 * i / M - 1)) ** 0.5 + 0.003
            strip.append((bm.verts.new((-wd, y, h * 0.92)), bm.verts.new((0, y, h + 0.006 * sign)), bm.verts.new((wd, y, h * 0.92))))
        for i in range(M):
            a, b = strip[i], strip[i+1]
            for s in range(2):
                f = (a[s], a[s+1], b[s+1], b[s]) if sign > 0 else (a[s+1], a[s], b[s], b[s+1])
                quad(bm, f, 1)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new('dart_tmp'); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    col = me.color_attributes.new('OutlineN', 'FLOAT_COLOR', 'CORNER')
    for l in me.loops:
        n = me.vertices[l.vertex_index].normal
        col.data[l.index].color = (n.x * 0.5 + 0.5, n.y * 0.5 + 0.5, n.z * 0.5 + 0.5, 1)
    for mn in ('MDart_Steel', 'MDart_Core'):
        me.materials.append(bpy.data.materials.get(mn) or bpy.data.materials.new(mn))
    return me

# ---------------------------------------------------------------- 2. Épine du Cerf
def spike(bm, base, direction, length, radius, sides, mat, tilt_seed):
    d = direction.normalized()
    side = d.orthogonal().normalized(); up = d.cross(side).normalized()
    ring = []
    for k in range(sides):
        a = 2 * math.pi * k / sides + tilt_seed
        r = radius * (0.85 + 0.3 * rnd.random())
        ring.append(base + d * (length * 0.22) + (side * math.cos(a) + up * math.sin(a)) * r)
    tip = bm.verts.new(base + d * length)
    heel = bm.verts.new(base - d * (length * 0.12))
    rv = [bm.verts.new(p) for p in ring]
    for k in range(sides):
        k2 = (k + 1) % sides
        f = bm.faces.new((rv[k], rv[k2], tip)); f.material_index = mat
        f = bm.faces.new((rv[k2], rv[k], heel)); f.material_index = mat

def crystal(bm, base, direction, length, radius, mat, twist, faces_out):
    # Prisme hexagonal (légèrement évasé vers l'avant, rayons irréguliers) + pointe taillée en pyramide + talon court.
    # faces_out reçoit (face, type) : 'side' = flanc du prisme (arêtes en long seulement), 'tri' = facette triangulaire.
    d = direction.normalized()
    side = d.orthogonal().normalized(); up = d.cross(side).normalized()
    n = 6
    radii = [radius * (0.82 + 0.36 * rnd.random()) for _ in range(n)]
    def ring(t, scale):
        pts = []
        for k in range(n):
            a = 2 * math.pi * k / n + twist
            pts.append(bm.verts.new(base + d * (length * t) + (side * math.cos(a) + up * math.sin(a)) * radii[k] * scale))
        return pts
    r0 = ring(0.06, 0.78); r1 = ring(0.62, 1.0)
    tip = bm.verts.new(base + d * length)
    heel = bm.verts.new(base - d * (length * 0.04))
    for k in range(n):
        k2 = (k + 1) % n
        f = bm.faces.new((r0[k], r0[k2], r1[k2], r1[k])); f.material_index = mat; faces_out.append((f, 'side'))
        f = bm.faces.new((r1[k], r1[k2], tip)); f.material_index = mat; faces_out.append((f, 'tri'))
        f = bm.faces.new((r0[k2], r0[k], heel)); f.material_index = mat; faces_out.append((f, 'tri'))

def build_thorn():
    # (2026-11-25, retour : « en bleu comme ses bois de cristal [...] trop flashy, sans reliefs, plus beau mais pas trop »)
    # petit amas de cristaux comme les bois du Cerf : un prisme principal + deux cristaux en ramure, cœur plus petit.
    bm = bmesh.new()
    Y = Vector((0, 1, 0))
    tagged = []
    for mat, s in ((0, 1.0), (1, 0.55)):
        crystal(bm, Vector((0, -0.2 * s, 0)), Y, 0.5 * s, 0.062 * s, mat, 0.25, tagged)
        if mat == 0:
            crystal(bm, Vector((0.02, -0.08, 0.01)), Vector((0.62, 1, 0.12)), 0.24, 0.034, mat, 0.9, tagged)
            crystal(bm, Vector((-0.02, -0.13, -0.01)), Vector((-0.7, 0.9, -0.08)), 0.19, 0.03, mat, 1.7, tagged)
            crystal(bm, Vector((0.0, -0.16, 0.0)), Vector((0.15, 0.5, 1.0)), 0.11, 0.022, mat, 0.4, tagged)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f, k in tagged: f.tag = (k == 'side')
    side_flags = [f.tag for f in bm.faces]
    me = bpy.data.meshes.new('thorn_tmp'); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    col = me.color_attributes.new('Bary', 'FLOAT_COLOR', 'CORNER')
    bary = ((1, 0, 0), (0, 1, 0), (0, 0, 1))
    for p in me.polygons:
        fid = rnd.random()
        li = list(p.loop_indices)
        if side_flags[p.index] and len(li) == 4:
            # flanc : traits sur les deux arêtes en long (coins 0-3 d'un côté, 1-2 de l'autre), pas sur la diagonale
            vals = ((0, 1, 1), (1, 0, 1), (1, 0, 1), (0, 1, 1))
            for k, l in enumerate(li): col.data[l].color = vals[k] + (fid,)
        else:
            for k, l in enumerate(li):
                b = bary[k % 3]
                col.data[l].color = (b[0], b[1], b[2], fid)
    for mn in ('MThorn_Shell', 'MThorn_Core'):
        me.materials.append(bpy.data.materials.get(mn) or bpy.data.materials.new(mn))
    return me

dart = new_object('SM_DragonDart', build_dart(), 16.0)
thorn = new_object('SM_DeerThorn', build_thorn(), 17.0)
cols = {'MDart_Steel': (0.75, 0.8, 0.85, 1), 'MDart_Core': (1, 0.1, 0.05, 1), 'MThorn_Shell': (0.9, 0.95, 1, 1), 'MThorn_Core': (1, 0.1, 0.05, 1)}
for k, c in cols.items(): bpy.data.materials[k].diffuse_color = c
export(dart, OUT + r"\SM_DragonDart.fbx")
export(thorn, OUT + r"\SM_DeerThorn.fbx")
bpy.ops.wm.save_mainfile()
print('dart', len(dart.data.polygons), tuple(round(v, 3) for v in dart.dimensions),
      'thorn', len(thorn.data.polygons), tuple(round(v, 3) for v in thorn.dimensions))
