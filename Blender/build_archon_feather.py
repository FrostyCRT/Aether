import bpy, bmesh, math
from mathutils import Vector, Matrix

# Boss 3 v2 (Archonte) - PROJECTILE : plume-lame de lumière (2026-11-26). Même matière que les mèches de l'Archonte.
# Taille d'un projectile (même longueur que l'ancien SM_RiftShard, ~0,6), pointe vers +Y (= avant du tir dans Unity).
#  - matériau 0 : la lame (dégradé peint dans 'Col' : blanc à la pointe -> bleu glacier à l'arrière), shader ArchonSoft ;
#  - matériau 1 : nervure centrale lumineuse (cyan-blanc), Unlit.
#  - 2 fines traînées qui s'écartent à l'arrière (lecture du mouvement), dans le matériau 0.
OUT = r"C:\Users\jules\My project\Aether Storm Survivor\Assets\Game\3D Models\Boss3\SM_ArchonFeather.fbx"
L = 0.62; W = 0.062; T = 0.018

bm = bmesh.new(); col_of = {}
def blade(base, d, side, nrm, L, W, T, curve, mat, nt=24, ns=8):
    d = Vector(d).normalized(); side = Vector(side).normalized(); nrm = Vector(nrm).normalized(); base = Vector(base)
    def w(t): return W * max(0.0, math.sin(math.pi * t)) ** 0.75 * (1.0 + 0.35 * (0.5 - t))
    def pt(t, s, top):
        th = T * (1 - s * s) * (w(t) / W)
        return base + d * L * t + side * s * w(t) + nrm * (curve * math.sin(math.pi * t) * L + (th / 2 if top else -th / 2))
    TT = [[bm.verts.new(pt(i / nt, -1 + 2 * j / ns, True)) for j in range(ns + 1)] for i in range(nt + 1)]
    BB = [[bm.verts.new(pt(i / nt, -1 + 2 * j / ns, False)) for j in range(ns + 1)] for i in range(nt + 1)]
    for i in range(nt):
        for j in range(ns):
            for f in (bm.faces.new((TT[i][j], TT[i][j + 1], TT[i + 1][j + 1], TT[i + 1][j])),
                      bm.faces.new((BB[i][j], BB[i + 1][j], BB[i + 1][j + 1], BB[i][j + 1]))):
                f.material_index = mat
            for v in TT[i] + BB[i]: col_of[v] = i / nt
    for v in TT[nt] + BB[nt]: col_of[v] = 1.0

# lame principale (de y = -0.31 à +0.31), légèrement cintrée
blade((0, -L / 2, 0), (0, 1, 0), (1, 0, 0), (0, 0, 1), L, W, T, 0.04, 0)
# 2 traînées fines qui partent de l'arrière et s'écartent
for sx in (-1, 1):
    blade((sx * 0.01, -0.18, 0), (sx * 0.32, -1, 0.05), (1, sx * 0.3, 0), (0, 0, 1), 0.24, 0.018, 0.008, 0.02, 0, nt=12, ns=4)
# nervure centrale (cylindre effilé qui traverse la lame)
prev = None; sides = 10; n = 24
for i in range(n + 1):
    t = i / n; y = -L / 2 + 0.03 + (L - 0.05) * t; r = 0.011 * (1 - 0.7 * t) + 0.002
    z = 0.04 * math.sin(math.pi * (y + L / 2) / L) * L
    cur = [bm.verts.new((math.cos(2 * math.pi * k / sides) * r, y, z + math.sin(2 * math.pi * k / sides) * r)) for k in range(sides)]
    if prev:
        for k in range(sides):
            f = bm.faces.new((prev[k], prev[(k + 1) % sides], cur[(k + 1) % sides], cur[k])); f.material_index = 1
    prev = cur
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

me = bpy.data.meshes.new('SM_ArchonFeather');
cols = {v.index: col_of.get(v, 0.5) for v in bm.verts}
bm.to_mesh(me); bm.free()
for p in me.polygons: p.use_smooth = True
ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
for p in me.polygons:
    for l in p.loop_indices:
        t = cols.get(me.loops[l].vertex_index, 0.5)
        c = tuple(a + (b - a) * t for a, b in zip((0.5, 0.74, 1.0), (0.97, 0.99, 1.0)))
        ca.data[l].color = c + (1.0,)
for mn in ('ArcFeather_Blade', 'ArcFeather_Core'):
    me.materials.append(bpy.data.materials.get(mn) or bpy.data.materials.new(mn))
ob = bpy.data.objects.get('SM_ArchonFeather')
if ob is None: ob = bpy.data.objects.new('SM_ArchonFeather', me); bpy.context.scene.collection.objects.link(ob)
else: old = ob.data; ob.data = me; bpy.data.meshes.remove(old)
ob.location = (60, -8, 5)

bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.sort_elements(type='MATERIAL', elements={'FACE'}); bpy.ops.object.mode_set(mode='OBJECT')
loc = ob.location.copy(); ob.location = (0, 0, 0)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'MESH'}, colors_type='LINEAR', mesh_smooth_type='FACE',
                         add_leaf_bones=False, bake_anim=False, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True)
ob.location = loc
print('plume', tuple(round(v, 3) for v in ob.dimensions), len(me.polygons))
