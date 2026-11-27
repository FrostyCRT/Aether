import bpy, bmesh, math
from mathutils import Vector

# Boss 3 (Archonte) - PROJECTILE v2 : FEUILLE DE LUMIÈRE (2026-11-26). Retour : la plume « on dirait des missiles ».
# Feuille ovale à pointe effilée, légèrement creusée en gouttière et cintrée, nervure centrale + 4 paires de nervures
# secondaires en relief, petite tige. Pointe vers +Y (= avant du tir). Taille d'un projectile (~0,6 de long).
#  - matériau 0 : limbe (couleur des sommets = facteur t du dégradé, base -> pointe), shader ArchonSoft ;
#  - matériau 1 : nervures + tige (ArchonSoft, 2e jeu de couleurs).
OUT = r"C:\Users\jules\My project\Aether Storm Survivor\Assets\Game\3D Models\Boss3\SM_ArchonLeaf.fbx"
L = 0.56; W = 0.15; T = 0.016; Y0 = -L / 2
bm = bmesh.new(); tval = {}

def width(t): return W * max(0.0, math.sin(math.pi * min(1.0, t) ** 0.85)) ** 0.9
def surf(t, s):
    """point du limbe : t le long (0 = base, 1 = pointe), s en travers (-1..1) ; gouttière + cintrage."""
    w = width(t)
    return Vector((s * w, Y0 + L * t, 0.35 * (s * w) ** 2 / W + 0.05 * L * math.sin(math.pi * t)))

nt, ns = 30, 12
TT = [[None] * (ns + 1) for _ in range(nt + 1)]; BB = [[None] * (ns + 1) for _ in range(nt + 1)]
for i in range(nt + 1):
    t = i / nt
    for j in range(ns + 1):
        s = -1 + 2 * j / ns; p = surf(t, s); th = T * (1 - s * s) * (width(t) / W + 0.05)
        TT[i][j] = bm.verts.new(p + Vector((0, 0, th / 2))); BB[i][j] = bm.verts.new(p - Vector((0, 0, th / 2)))
        tval[TT[i][j]] = t; tval[BB[i][j]] = t
for i in range(nt):
    for j in range(ns):
        bm.faces.new((TT[i][j], TT[i][j + 1], TT[i + 1][j + 1], TT[i + 1][j])).material_index = 0
        bm.faces.new((BB[i][j], BB[i + 1][j], BB[i + 1][j + 1], BB[i][j + 1])).material_index = 0

def rib(pts, r0, r1, sides=8):
    prev = None; n = len(pts)
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized(); s = d.orthogonal().normalized(); u = d.cross(s)
        r = r0 + (r1 - r0) * i / (n - 1)
        cur = [bm.verts.new(p + (s * math.cos(2 * math.pi * k / sides) + u * math.sin(2 * math.pi * k / sides)) * r) for k in range(sides)]
        for v in cur: tval[v] = 0.0
        if prev:
            for k in range(sides): bm.faces.new((prev[k], prev[(k + 1) % sides], cur[(k + 1) % sides], cur[k])).material_index = 1
        prev = cur
# nervure centrale (dépasse en tige à l'arrière)
rib([surf(0, 0) + Vector((0, -0.07 + 0.07 * k / 4, 0)) for k in range(4)] + [surf(i / 20, 0) + Vector((0, 0, T * 0.55)) for i in range(20)], 0.009, 0.003)
# 4 paires de nervures secondaires, en arc vers la pointe
for k in range(4):
    t0 = 0.18 + 0.17 * k
    for sx in (-1, 1):
        rib([surf(min(0.98, t0 + 0.12 * q / 6), sx * 0.85 * q / 6) + Vector((0, 0, T * 0.45)) for q in range(7)], 0.0045, 0.0018, 6)

bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
tv = {v.index: tval.get(v, 0.0) for v in bm.verts}
me = bpy.data.meshes.new('SM_ArchonLeaf'); bm.to_mesh(me); bm.free()
for p in me.polygons: p.use_smooth = True
ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
for p in me.polygons:
    for l in p.loop_indices:
        t = tv.get(me.loops[l].vertex_index, 0.0); ca.data[l].color = (t, t, t, 1.0)
for mn in ('ArcLeaf_Blade', 'ArcLeaf_Veins'):
    me.materials.append(bpy.data.materials.get(mn) or bpy.data.materials.new(mn))
ob = bpy.data.objects.get('SM_ArchonLeaf')
if ob is None: ob = bpy.data.objects.new('SM_ArchonLeaf', me); bpy.context.scene.collection.objects.link(ob)
else: old = ob.data; ob.data = me; bpy.data.meshes.remove(old)
ob.location = (61, -8, 5)
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.sort_elements(type='MATERIAL', elements={'FACE'}); bpy.ops.object.mode_set(mode='OBJECT')
loc = ob.location.copy(); ob.location = (0, 0, 0)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'MESH'}, colors_type='LINEAR', mesh_smooth_type='FACE',
                         add_leaf_bones=False, bake_anim=False, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True)
ob.location = loc
print('feuille', tuple(round(v, 3) for v in ob.dimensions), len(me.polygons))
