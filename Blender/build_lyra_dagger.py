import bpy, bmesh, math
from mathutils import Vector

# Lame croissant de Lyra (2026-11-25). Pointe vers +Y, à plat dans XY.
# Matériaux : 0 lame bleu nuit, 1 argent (fil du tranchant, garde, pommeau), 2 poignée, 3 gemme violette.
STATIONS = [(0.0, 0.0, 1), (0.15, 0.5, 0), (0.62, 1.0, 0), (0.93, 0.5, 1), (1.0, 0.0, None)]

def quad(bm, vs, m):
    f = bm.faces.new(vs); f.material_index = m; return f

def build():
    bm = bmesh.new()
    # ---------- LAME ----------
    N = 44; y0, y1 = -0.07, 0.62; XS = 0.03
    So, Si, B = 0.15, 0.06, 0.036
    top = []; bot = []
    for i in range(N + 1):
        t = i / N; y = y0 + t * (y1 - y0)
        xo = XS - B * (1 - t) - So * math.sin(math.pi * t)
        xi = XS + B * (1 - t) - Si * math.sin(math.pi * t)
        H = 0.032 * ((1 - t) ** 0.55)
        rt = []; rb = []
        for (u, zf, _) in STATIONS:
            x = xo + (xi - xo) * u; z = H * zf
            vt = bm.verts.new((x, y, z)); rt.append(vt)
            rb.append(vt if zf == 0 else bm.verts.new((x, y, -z)))
        top.append(rt); bot.append(rb)
    for i in range(N):
        for s in range(len(STATIONS) - 1):
            m = STATIONS[s][2]
            quad(bm, (top[i][s], top[i][s+1], top[i+1][s+1], top[i+1][s]), m)
            quad(bm, (bot[i][s+1], bot[i][s], bot[i+1][s], bot[i+1][s+1]), m)
    ring = top[0] + list(reversed(bot[0][1:-1]))
    quad(bm, list(reversed(ring)), 0)
    # ---------- GARDE en croissant, cornes vers l'avant ----------
    M = 48; A = math.radians(118); C = Vector((0.0, 0.015, 0)); r1 = 0.135
    prof = [(0.0, 0.0), (0.0, 0.013), (0.5, 0.03), (1.0, 0.013), (1.0, 0.0)]
    rowsT = []; rowsB = []
    for j in range(M + 1):
        a = -A + 2 * A * j / M
        d = Vector((math.sin(a), -math.cos(a), 0))
        T = 0.058 * (math.cos(a / A * math.pi / 2) ** 0.6) + 0.004
        po = C + r1 * d
        rt = []; rb = []
        for (v, z) in prof:
            p = po - d * (T * v)
            vt = bm.verts.new((p.x, p.y, z)); rt.append(vt)
            rb.append(vt if z == 0 else bm.verts.new((p.x, p.y, -z)))
        rowsT.append(rt); rowsB.append(rb)
    for j in range(M):
        for s in range(len(prof) - 1):
            quad(bm, (rowsT[j][s+1], rowsT[j][s], rowsT[j+1][s], rowsT[j+1][s+1]), 1)
            quad(bm, (rowsB[j][s], rowsB[j][s+1], rowsB[j+1][s+1], rowsB[j+1][s]), 1)
    for ri, flip in ((0, False), (M, True)):
        ringv = rowsT[ri] + list(reversed(rowsB[ri][1:-1]))
        quad(bm, ringv if flip else list(reversed(ringv)), 1)
    # ---------- GEMME sur la garde ----------
    gc = Vector((0.0, C.y - r1 + 0.03, 0)); segs = 8; rx, ry = 0.028, 0.034
    girT = []; girB = []; tabT = []; tabB = []
    for k in range(segs):
        a = 2 * math.pi * k / segs + math.pi / 8
        girT.append(bm.verts.new((gc.x + rx * math.cos(a), gc.y + ry * math.sin(a), 0.028)))
        girB.append(bm.verts.new((gc.x + rx * math.cos(a), gc.y + ry * math.sin(a), -0.028)))
        a = 2 * math.pi * k / segs
        tabT.append(bm.verts.new((gc.x + rx * 0.5 * math.cos(a), gc.y + ry * 0.5 * math.sin(a), 0.05)))
        tabB.append(bm.verts.new((gc.x + rx * 0.5 * math.cos(a), gc.y + ry * 0.5 * math.sin(a), -0.05)))
    for k in range(segs):
        k2 = (k + 1) % segs
        for (g, tb, sgn) in ((girT, tabT, 1), (girB, tabB, -1)):
            for ff in ((g[k], g[k2], tb[k2]), (g[k], tb[k2], tb[k])):
                quad(bm, ff if sgn > 0 else tuple(reversed(ff)), 3)
        quad(bm, (girB[k], girB[k2], girT[k2], girT[k]), 3)
    quad(bm, tabT, 3); quad(bm, list(reversed(tabB)), 3)
    # ---------- POIGNÉE ----------
    G = 14; L = 30; ya, yb = -0.095, -0.245
    rings = []
    for i in range(L + 1):
        t = i / L; y = ya + (yb - ya) * t
        r = 0.019 + 0.0045 * abs(math.sin(math.pi * t * 6))
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * k / G), y, r * 0.85 * math.sin(2 * math.pi * k / G))) for k in range(G)])
    for i in range(L):
        for k in range(G):
            k2 = (k + 1) % G
            quad(bm, (rings[i][k], rings[i][k2], rings[i+1][k2], rings[i+1][k]), 2)
    # ---------- POMMEAU ----------
    P = 8; prof2 = [(-0.238, 0.022), (-0.252, 0.03), (-0.272, 0.024)]
    pr = [[bm.verts.new((r * math.cos(2 * math.pi * k / P), y, r * 0.8 * math.sin(2 * math.pi * k / P))) for k in range(P)] for (y, r) in prof2]
    tipv = bm.verts.new((0, -0.298, 0)); topv = bm.verts.new((0, -0.236, 0))
    for i in range(len(pr) - 1):
        for k in range(P):
            k2 = (k + 1) % P
            quad(bm, (pr[i][k], pr[i][k2], pr[i+1][k2], pr[i+1][k]), 1)
    for k in range(P):
        k2 = (k + 1) % P
        quad(bm, (pr[-1][k], pr[-1][k2], tipv), 1)
        quad(bm, (pr[0][k2], pr[0][k], topv), 1)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm

name = 'SM_LyraDagger_v2'
ob = bpy.data.objects.get(name)
bm = build()
me = bpy.data.meshes.new(name + '_tmp')
bm.to_mesh(me); bm.free()
for p in me.polygons: p.use_smooth = False
attr = me.color_attributes.new('OutlineN', 'FLOAT_COLOR', 'CORNER')
for l in me.loops:
    n = me.vertices[l.vertex_index].normal
    attr.data[l.index].color = (n.x * 0.5 + 0.5, n.y * 0.5 + 0.5, n.z * 0.5 + 0.5, 1)
for mn in ('MD_0', 'MD_1', 'MD_2', 'MD_3'):
    me.materials.append(bpy.data.materials.get(mn) or bpy.data.materials.new(mn))
if ob is None:
    ob = bpy.data.objects.new(name, me)
    bpy.data.objects['SM_LyraDagger'].users_collection[0].objects.link(ob)
    ob.location = (12.0, -3.0, 0)
else:
    oldme = ob.data; ob.data = me; bpy.data.meshes.remove(oldme)
me.name = name
print(len(me.polygons), tuple(round(v, 3) for v in ob.dimensions))
