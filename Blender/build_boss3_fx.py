import bpy, bmesh, math, random
from mathutils import Vector

# Boss 3 (Serpent de la Faille) - projectile et cristal de pulsation (2026-11-25).
#  - SM_RiftShard : éclat d'obsidienne effilé (pointe vers +Y = sens du tir), 2 petites pointes latérales ; coque (mat 0)
#    + cœur plus petit à l'intérieur (mat 1, bordeaux sourd dans Unity).
#  - SM_RiftCrystal : grappe de prismes d'obsidienne (comme les nervures du capuchon), axe principal +Y (= le haut une fois
#    posé dans OrbitalCrystal), socle en y = 0 ; coque (mat 0) + cœurs (mat 1, indigo dans Unity).
# Couleur des sommets = « barycentriques » de PaintedCrystal (arêtes en long des prismes + facettes des pointes).
OUT = r"C:\Users\jules\My project\Aether Storm Survivor\Assets\Game\3D Models\Lot1"
rnd = random.Random(21)

def build(parts):
    bm = bmesh.new(); tags = []
    def prism(base, d, length, radius, sides, mat, tip_frac=0.32, heel_frac=0.06, flare=1.0):
        d = d.normalized(); side = d.orthogonal().normalized(); up = d.cross(side).normalized()
        tw = rnd.random() * 6.28; radii = [radius * (0.82 + 0.36 * rnd.random()) for _ in range(sides)]
        def ring(t, sc): return [bm.verts.new(base + d * (length * t) + (side * math.cos(2 * math.pi * k / sides + tw) + up * math.sin(2 * math.pi * k / sides + tw)) * radii[k] * sc) for k in range(sides)]
        r0 = ring(0.0, 0.8); r1 = ring(1 - tip_frac, flare)
        tip = bm.verts.new(base + d * length); heel = bm.verts.new(base - d * (length * heel_frac))
        for k in range(sides):
            k2 = (k + 1) % sides
            f = bm.faces.new((r0[k], r0[k2], r1[k2], r1[k])); f.material_index = mat; tags.append((f, True))
            f = bm.faces.new((r1[k], r1[k2], tip)); f.material_index = mat; tags.append((f, False))
            f = bm.faces.new((r0[k2], r0[k], heel)); f.material_index = mat; tags.append((f, False))
    for p in parts: prism(*p)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f, side in tags: f.tag = side
    flags = [f.tag for f in bm.faces]
    return bm, flags

def make(name, bm, flags, x):
    me = bpy.data.meshes.new(name + '_tmp'); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = False
    col = me.color_attributes.new('Bary', 'FLOAT_COLOR', 'CORNER')
    for p in me.polygons:
        fid = rnd.random(); li = list(p.loop_indices)
        if flags[p.index] and len(li) == 4:
            for k, l in enumerate(li): col.data[l].color = ((0, 1, 1), (1, 0, 1), (1, 0, 1), (0, 1, 1))[k] + (fid,)
        else:
            for k, l in enumerate(li): col.data[l].color = ((1, 0, 0), (0, 1, 0), (0, 0, 1))[k % 3] + (fid,)
    for mn in ('MRift_Shell', 'MRift_Core'):
        me.materials.append(bpy.data.materials.get(mn) or bpy.data.materials.new(mn))
    ob = bpy.data.objects.get(name)
    if ob is None: ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    else: old = ob.data; ob.data = me; bpy.data.meshes.remove(old)
    me.name = name; ob.location = (x, -50, 0)
    return ob

def export(ob, path):
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.sort_elements(type='MATERIAL', elements={'FACE'}); bpy.ops.object.mode_set(mode='OBJECT')
    loc = ob.location.copy(); ob.location = (0, 0, 0)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, colors_type='LINEAR', mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True)
    ob.location = loc

Y = Vector((0, 1, 0))
# ---- projectile : éclat principal + 2 éclats latéraux couchés vers l'arrière ; cœur = même éclat réduit
shard_parts = [(Vector((0, -0.2, 0)), Y, 0.52, 0.06, 6, 0, 0.42, 0.25, 1.0),
               (Vector((0.02, -0.06, 0.0)), Vector((0.7, -1.0, 0.1)), 0.18, 0.028, 4, 0, 0.4, 0.05, 1.0),
               (Vector((-0.02, -0.1, 0.01)), Vector((-0.7, -1.0, -0.1)), 0.15, 0.025, 4, 0, 0.4, 0.05, 1.0),
               (Vector((0, -0.12, 0)), Y, 0.3, 0.03, 6, 1, 0.4, 0.2, 1.0)]
bm, fl = build(shard_parts)
shard = make('SM_RiftShard', bm, fl, 20.0)
# ---- cristal de pulsation : prisme central haut + 4 prismes penchés vers l'extérieur ; cœurs dans les deux plus grands
cr_parts = [(Vector((0, 0.0, 0)), Y, 1.0, 0.13, 6, 0, 0.26, 0.04, 1.0),
            (Vector((0.08, 0.02, 0.02)), Vector((0.55, 1, 0.15)), 0.62, 0.085, 6, 0, 0.3, 0.04, 1.0),
            (Vector((-0.08, 0.02, -0.03)), Vector((-0.5, 1, -0.25)), 0.55, 0.08, 6, 0, 0.3, 0.04, 1.0),
            (Vector((0.0, 0.01, 0.09)), Vector((0.1, 1, 0.6)), 0.42, 0.06, 5, 0, 0.32, 0.04, 1.0),
            (Vector((0.02, 0.01, -0.09)), Vector((-0.15, 1, -0.6)), 0.36, 0.055, 5, 0, 0.32, 0.04, 1.0),
            (Vector((0, 0.08, 0)), Y, 0.72, 0.06, 6, 1, 0.3, 0.04, 1.0),
            (Vector((0.08, 0.06, 0.02)), Vector((0.55, 1, 0.15)), 0.4, 0.04, 6, 1, 0.3, 0.04, 1.0)]
bm, fl = build(cr_parts)
crys = make('SM_RiftCrystal', bm, fl, 21.5)
export(shard, OUT + r"\SM_RiftShard.fbx"); export(crys, OUT + r"\SM_RiftCrystal.fbx")
bpy.ops.wm.save_mainfile()
print('shard', tuple(round(v, 2) for v in shard.dimensions), 'crystal', tuple(round(v, 2) for v in crys.dimensions))
