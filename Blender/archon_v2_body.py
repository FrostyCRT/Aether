import bpy, bmesh, math
from mathutils import Vector, Quaternion, Matrix

# Archonte v2 (2026-11-26) - CORPS ORGANIQUE d'un seul tenant (retour utilisateur : la v1 « a l'air robotique »).
# Volumes en métaballes (fusion douce comme de l'argile) -> maillage -> remaillage voxel -> lissage -> réduction.
# Face vers -Y, haut +Z, mètres. Résultat : objet 'ArcV2_Body' dans la collection 'ArchonV2' (X = 60).
OX = 60.0
RES = 0.028
RK = 2.0          # rayon d'influence / rayon visible (étalonné)

col = bpy.data.collections.get('ArchonV2')
if col is None:
    col = bpy.data.collections.new('ArchonV2'); bpy.context.scene.collection.children.link(col)
for o in list(col.objects):
    if o.name.startswith('ArcV2_Body'): bpy.data.objects.remove(o, do_unlink=True)

mb = bpy.data.metaballs.new('ArcV2_BodyMB'); mb.resolution = RES; mb.render_resolution = RES; mb.threshold = 0.6
def el(kind, co, r, size=(1, 1, 1), rot=None, stiff=2.0):
    e = mb.elements.new(type=kind); e.co = co; e.radius = r * RK; e.stiffness = stiff
    e.size_x, e.size_y, e.size_z = size
    if rot is not None: e.rotation = rot
    return e
def capsule(a, b, r, stiff=2.0):
    a, b = Vector(a), Vector(b); d = b - a
    q = Vector((1, 0, 0)).rotation_difference(d.normalized())
    return el('CAPSULE', (a + b) / 2, r, (d.length / 2, 1, 1), q, stiff)

# ---- tronc : bassin, taille fine, cage thoracique, pectoraux, trapèzes
el('ELLIPSOID', (0, 0, 3.95), 0.5, (0.9, 0.7, 0.75))
el('ELLIPSOID', (0, 0, 4.55), 0.45, (0.8, 0.62, 0.9))
el('ELLIPSOID', (0, 0.0, 5.25), 0.6, (1.0, 0.68, 0.95))
for sx in (-1, 1):
    el('ELLIPSOID', (sx * 0.27, -0.2, 5.52), 0.28, (1.05, 0.7, 0.75), stiff=1.5)       # pectoraux
    el('ELLIPSOID', (sx * 0.78, 0.0, 5.9), 0.27, (1.0, 0.95, 0.95))                    # deltoïdes
el('ELLIPSOID', (0, 0.06, 6.0), 0.4, (1.35, 0.6, 0.45))                                # trapèzes
# ---- cou, tête, mâchoire, nez, arcades
capsule((0, 0.04, 6.0), (0, -0.02, 6.55), 0.2)
el('ELLIPSOID', (0, -0.01, 6.92), 0.34, (0.86, 0.96, 1.12))
el('ELLIPSOID', (0, -0.14, 6.7), 0.18, (0.95, 0.9, 0.85), stiff=1.5)
el('ELLIPSOID', (0, -0.31, 6.86), 0.05, (0.6, 0.8, 1.5), stiff=1.2)
# ---- bras (épaule -> coude -> poignet), mains longues et fines
for sx in (-1, 1):
    sh = Vector((sx * 0.92, 0.0, 5.85)); el_ = Vector((sx * 1.42, -0.12, 4.85)); wr = Vector((sx * 1.72, -0.42, 4.0))
    capsule(sh, el_, 0.2); capsule(el_, wr, 0.155)
    el('ELLIPSOID', el_ + (sh - el_) * 0.45, 0.19, (0.9, 0.9, 1.3), stiff=1.5)          # biceps
    el('ELLIPSOID', el_ + (wr - el_) * 0.3, 0.15, (0.9, 0.9, 1.3), stiff=1.5)           # avant-bras
    fa = (wr - el_).normalized()
    side = fa.cross(Vector((0, -1, 0.35))).normalized() * sx
    HK = 1.5
    palm_c = wr + fa * 0.16 * HK
    el('ELLIPSOID', palm_c, 0.13 * HK, (0.55, 0.95, 0.85), rot=Vector((0, 0, 1)).rotation_difference(fa), stiff=1.5)
    for i in range(4):
        base = wr + fa * 0.26 * HK + side * (-0.075 + 0.05 * i) * HK
        L = (0.2 + 0.04 * (1.5 - abs(i - 1.5))) * HK
        tip_dir = (fa + side * (-0.08 + 0.055 * i) + Vector((0, -0.15, 0))).normalized()
        mid = base + tip_dir * L * 0.55
        capsule(base, mid, 0.04, 1.2); capsule(mid, mid + (tip_dir + Vector((0, -0.25, -0.1))).normalized() * L * 0.45, 0.033, 1.2)
    tb = wr + fa * 0.1 * HK + side * 0.1 * HK
    capsule(tb, tb + (fa * 0.5 + side * 0.9).normalized() * 0.14 * HK, 0.045, 1.2)

mbo = bpy.data.objects.new('ArcV2_BodyMBObj', mb); col.objects.link(mbo); mbo.location = (OX, 0, 0)
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
me = bpy.data.meshes.new_from_object(mbo.evaluated_get(dg))
bpy.data.objects.remove(mbo, do_unlink=True); bpy.data.metaballs.remove(mb)
body = bpy.data.objects.new('ArcV2_Body', me); col.objects.link(body); body.location = (OX, 0, 0)
for p in me.polygons: p.use_smooth = True
# remaillage régulier + lissage + réduction
r = body.modifiers.new('Remesh', 'REMESH'); r.mode = 'VOXEL'; r.voxel_size = 0.022; r.use_smooth_shade = True
s = body.modifiers.new('Smooth', 'CORRECTIVE_SMOOTH'); s.iterations = 6; s.smooth_type = 'LENGTH_WEIGHTED'; s.use_only_smooth = True
d = body.modifiers.new('Decimate', 'DECIMATE'); d.ratio = 0.45
print('corps', len(me.vertices))
