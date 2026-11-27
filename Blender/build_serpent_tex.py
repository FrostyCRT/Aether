import bpy, math
import numpy as np

# Peau du Serpent de la Faille (2026-11-25) : UV + 2 textures générées.
#  - UV de l'enveloppe (matériaux 0 et 1) : u = tour du corps (couture SOUS le ventre, invisible), v = longueur / TILE m.
#  - T_Serpent_Albedo : écailles d'obsidienne imbriquées (dos/flancs), larges plaques ventrales (ventre), rainures sombres.
#  - T_Serpent_Veins : masque des veines rouges (rainures « fêlées » par plaques + ligne dorsale), lu en émission.
#  - Cristaux (matériau 2) : couleur des sommets = « barycentriques » de PaintedCrystal (arêtes en long + facettes).
OUT = r"C:\Users\jules\My project\Aether Storm Survivor\Assets\Game\3D Models\Boss3\Textures"
TILE = 2.0
RES = 1024
L = 15.0; HEAD = 1.75; RNECK = 0.52

import os
os.makedirs(OUT, exist_ok=True)

def center_z_at(y):
    # même centre que build_serpent.py (corps puis tête)
    t = y / L
    if y >= L - HEAD:
        u = (y - (L - HEAD)) / HEAD
        return RNECK * 0.62 - 0.06 * u
    RMAX = 0.72
    if t < 0.62:
        r = 0.04 + (RMAX - 0.04) * math.sin(min(1.0, t / 0.62) * math.pi / 2) ** 0.9
    elif t < 0.86:
        k = (t - 0.62) / 0.24
        r = RMAX - (RMAX - RNECK) * (k * k * (3 - 2 * k))
    else:
        r = RNECK
    return r * 0.62

ob = bpy.data.objects['SK_Serpent_Rest']; me = ob.data
uv = me.uv_layers.get('UVMap') or me.uv_layers.new(name='UVMap')
col = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
import random; rnd = random.Random(3)
for p in me.polygons:
    li = list(p.loop_indices)
    if p.material_index <= 1:
        us = []
        for l in li:
            v = me.vertices[me.loops[l].vertex_index].co
            a = math.atan2(v.z - center_z_at(min(max(v.y, 0), L)), v.x)
            us.append(((a + math.pi / 2) / (2 * math.pi)) % 1.0)
        if max(us) - min(us) > 0.5:
            us = [u + 1.0 if u < 0.5 else u for u in us]
        for l, u in zip(li, us):
            v = me.vertices[me.loops[l].vertex_index].co
            uv.data[l].uv = (u, v.y / TILE)
            col.data[l].color = (1, 1, 1, 1)
    else:
        fid = rnd.random()
        if len(li) == 4:
            vals = ((0, 1, 1), (1, 0, 1), (1, 0, 1), (0, 1, 1))
            for k, l in enumerate(li): col.data[l].color = vals[k] + (fid,)
        else:
            bary = ((1, 0, 0), (0, 1, 0), (0, 0, 1))
            for k, l in enumerate(li): col.data[l].color = bary[k % 3] + (fid,)
        for l in li: uv.data[l].uv = (0.5, 0.5)

# ---------------------------------------------------------------- textures
def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)

def value_noise(xs, ys, freq_x, freq_y, seed):
    # bruit de valeurs périodique (raccord en u ET en v)
    r = np.random.RandomState(seed)
    g = r.rand(freq_y, freq_x)
    X = xs * freq_x; Y = ys * freq_y
    x0 = np.floor(X).astype(int); y0 = np.floor(Y).astype(int)
    fx = X - x0; fy = Y - y0
    fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    x0m, x1m = x0 % freq_x, (x0 + 1) % freq_x; y0m, y1m = y0 % freq_y, (y0 + 1) % freq_y
    a = g[y0m, x0m] * (1 - fx) + g[y0m, x1m] * fx
    b = g[y1m, x0m] * (1 - fx) + g[y1m, x1m] * fx
    return a * (1 - fy) + b * fy

ys, xs = np.mgrid[0:RES, 0:RES].astype(np.float32)
U = (xs + 0.5) / RES       # tour du corps (0 = sous le ventre, 0,5 = sur le dos)
V = (ys + 0.5) / RES       # long du corps (TILE m)

# écailles : grille décalée, cellules allongées dans le sens du corps ; F1/F2 de Voronoi (plus proche / 2e plus proche)
NX, NY = 34, 16
r = np.random.RandomState(7)
jit = r.rand(NY, NX, 2) * 0.14 - 0.07
cx = U * NX; cy = V * NY
F1 = np.full(U.shape, 9.0, np.float32); F2 = np.full(U.shape, 9.0, np.float32); IDY = np.zeros(U.shape, np.float32); LOCY = np.zeros(U.shape, np.float32)
base_i = np.floor(cy).astype(int)
for dy in (-1, 0, 1):
    iy = base_i + dy
    off = (iy % 2) * 0.5
    bx = np.floor(cx - off).astype(int)
    for dx in (-1, 0, 1):
        ix = bx + dx
        j = jit[iy % NY, ix % NX]
        px = ix + off + 0.5 + j[..., 0]; py = iy + 0.5 + j[..., 1]
        ddx = (cx - px); ddy = (cy - py) * 1.25
        d = np.abs(ddx) * 0.95 + np.abs(ddy)          # métrique en losange : écailles de reptile
        closer = d < F1
        F2 = np.where(closer, F1, np.minimum(F2, d))
        LOCY = np.where(closer, cy - py, LOCY)
        IDY = np.where(closer, (ix * 7 + iy * 13) % 17 / 17.0, IDY)
        F1 = np.where(closer, d, F1)
edge = F2 - F1
groove = 1 - smooth(0.0, 0.1, edge)                       # 1 dans la rainure
shade = np.clip(0.55 + LOCY * 0.9, 0, 1)                    # avant de l'écaille plus clair (imbrication)

# plaques ventrales : bandes transversales larges
belly = smooth(0.13, 0.08, np.minimum(U, 1 - U))            # 1 sous le ventre
bandY = (V * 18.0) % 1.0
bgroove = 1 - smooth(0.0, 0.07, np.minimum(bandY, 1 - bandY))
bshade = 0.6 + 0.4 * bandY

dark = np.array([0.008, 0.008, 0.016]); mid = np.array([0.03, 0.032, 0.058]); hi = np.array([0.085, 0.095, 0.17])
bcol = np.array([0.04, 0.035, 0.055]); bhi = np.array([0.085, 0.075, 0.105])
n1 = value_noise(U, V, 12, 6, 11)
sc = (shade * (0.85 + 0.3 * IDY))[..., None]
scales = mid * (1 - sc) + hi * sc
scales = scales * (0.85 + 0.3 * n1[..., None])
scales = scales * (1 - groove[..., None]) + dark * groove[..., None]
bel = bcol * (1 - bshade[..., None]) + bhi * bshade[..., None]
bel = bel * (1 - bgroove[..., None]) + dark * bgroove[..., None]
albedo = scales * (1 - belly[..., None]) + bel * belly[..., None]

# veines : rainures des écailles, seulement dans des plaques de « fêlure » (bruit), plus une ligne le long du dos
patch = smooth(0.58, 0.72, value_noise(U, V, 8, 4, 21))
dorsal = (1 - smooth(0.0, 0.012, np.abs(U - 0.5))) * smooth(0.35, 0.6, value_noise(U, V, 4, 10, 31))
veins = np.clip(groove * patch * (1 - belly) * 1.2 + dorsal * 0.85, 0, 1)
albedo = albedo * (1 - veins[..., None]) + np.array([0.22, 0.015, 0.015]) * veins[..., None]

def save(name, rgb):
    img = bpy.data.images.get(name)
    if img is None: img = bpy.data.images.new(name, RES, RES, alpha=False)
    px = np.ones((RES, RES, 4), np.float32); px[..., :3] = np.clip(rgb, 0, 1)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = OUT + "\\" + name + ".png"; img.file_format = 'PNG'
    img.save()

save('T_Serpent_Albedo', albedo ** (1 / 2.2))     # en sRGB
save('T_Serpent_Veins', np.repeat(veins[..., None], 3, axis=2))
print('ok', me.uv_layers.active.name)
