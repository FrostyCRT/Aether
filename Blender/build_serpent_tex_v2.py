import bpy, os
import numpy as np

# Textures du Serpent v2 (2026-11-25) : albédo, veines (émission) et CARTE DE NORMALES (relief de chaque petite écaille,
# des plaques ventrales). Même découpage UV que build_serpent_v2.py : u = tour du corps (0 = sous le ventre, 0,5 = dos),
# v = longueur / 2 m. Puis matériaux « Principled » (aperçu Blender + export GLB pour le site d'animation).
OUT = r"C:\Users\jules\My project\Aether Storm Survivor\Assets\Game\3D Models\Boss3\Textures"
os.makedirs(OUT, exist_ok=True)
RES = 2048

def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)

def value_noise(xs, ys, fx_, fy_, seed):
    r = np.random.RandomState(seed); g = r.rand(fy_, fx_)
    X = xs * fx_; Y = ys * fy_
    x0 = np.floor(X).astype(int); y0 = np.floor(Y).astype(int)
    fx = X - x0; fy = Y - y0; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    a = g[y0 % fy_, x0 % fx_] * (1 - fx) + g[y0 % fy_, (x0 + 1) % fx_] * fx
    b = g[(y0 + 1) % fy_, x0 % fx_] * (1 - fx) + g[(y0 + 1) % fy_, (x0 + 1) % fx_] * fx
    return a * (1 - fy) + b * fy

ys, xs = np.mgrid[0:RES, 0:RES].astype(np.float32)
U = (xs + 0.5) / RES; V = (ys + 0.5) / RES

NX, NY = 40, 18
jit = np.random.RandomState(7).rand(NY, NX, 2) * 0.12 - 0.06
cx = U * NX; cy = V * NY
F1 = np.full(U.shape, 9.0, np.float32); F2 = np.full(U.shape, 9.0, np.float32)
LOCY = np.zeros(U.shape, np.float32); IDC = np.zeros(U.shape, np.float32)
bi = np.floor(cy).astype(int)
for dy in (-1, 0, 1):
    iy = bi + dy; off = (iy % 2) * 0.5
    bx = np.floor(cx - off).astype(int)
    for dx in (-1, 0, 1):
        ix = bx + dx
        j = jit[iy % NY, ix % NX]
        px = ix + off + 0.5 + j[..., 0]; py = iy + 0.5 + j[..., 1]
        d = np.abs(cx - px) * 0.95 + np.abs((cy - py) * 1.25)
        closer = d < F1
        F2 = np.where(closer, F1, np.minimum(F2, d))
        LOCY = np.where(closer, cy - py, LOCY)
        IDC = np.where(closer, ((ix * 7 + iy * 13) % 17) / 17.0, IDC)
        F1 = np.where(closer, d, F1)
edge = F2 - F1
groove = 1 - smooth(0.0, 0.09, edge)
shade = np.clip(0.55 + LOCY * 0.9, 0, 1)
belly = smooth(0.13, 0.08, np.minimum(U, 1 - U))
bandY = (V * 18.0) % 1.0
bgroove = 1 - smooth(0.0, 0.06, np.minimum(bandY, 1 - bandY))

# ---- albédo
dark = np.array([0.008, 0.008, 0.016]); mid = np.array([0.03, 0.032, 0.058]); hi = np.array([0.09, 0.1, 0.18])
bcol = np.array([0.04, 0.035, 0.055]); bhi = np.array([0.085, 0.075, 0.105])
n1 = value_noise(U, V, 12, 6, 11); n2 = value_noise(U, V, 48, 24, 12)
sc = (shade * (0.85 + 0.3 * IDC))[..., None]
scales = mid * (1 - sc) + hi * sc
scales = scales * (0.8 + 0.25 * n1[..., None] + 0.15 * n2[..., None])
scales = scales * (1 - groove[..., None]) + dark * groove[..., None]
bel = bcol * (1 - bandY[..., None]) + bhi * bandY[..., None]
bel = bel * (1 - bgroove[..., None]) + dark * bgroove[..., None]
albedo = scales * (1 - belly[..., None]) + bel * belly[..., None]
patch = smooth(0.64, 0.76, value_noise(U, V, 8, 4, 21))   # fêlures par plaques, plus rares
dorsal = (1 - smooth(0.0, 0.01, np.abs(U - 0.5))) * smooth(0.35, 0.6, value_noise(U, V, 4, 10, 31))
veins = np.clip(groove * patch * (1 - belly) * 1.2 + dorsal * 0.85, 0, 1)
albedo = albedo * (1 - veins[..., None]) + np.array([0.22, 0.015, 0.015]) * veins[..., None]

# ---- hauteur -> normales : chaque écaille bombée, plus haute à son bord libre (imbrication), rainures creusées
dome = 1 - np.clip(F1 / 0.62, 0, 1) ** 1.6
H = dome * 0.6 + np.clip(0.5 - LOCY, 0, 1) * 0.35 - groove * 0.5
Hb = 0.6 * bandY - bgroove * 0.4
H = H * (1 - belly) + Hb * belly
H += (n2 - 0.5) * 0.04
gx = (np.roll(H, -1, axis=1) - np.roll(H, 1, axis=1)) * 0.5
gy = (np.roll(H, -1, axis=0) - np.roll(H, 1, axis=0)) * 0.5
STRENGTH = 9.0
nx = -gx * STRENGTH; ny = -gy * STRENGTH; nz = np.ones_like(H)
ln = np.sqrt(nx * nx + ny * ny + nz * nz)
normal = np.stack([nx / ln, ny / ln, nz / ln], axis=-1) * 0.5 + 0.5

def save(name, rgb, srgb):
    img = bpy.data.images.get(name)
    if img is not None: bpy.data.images.remove(img)
    img = bpy.data.images.new(name, RES, RES, alpha=False, float_buffer=False)
    if not srgb: img.colorspace_settings.name = 'Non-Color'
    px = np.ones((RES, RES, 4), np.float32); px[..., :3] = np.clip(rgb, 0, 1)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = os.path.join(OUT, name + ".png"); img.file_format = 'PNG'; img.save()
    return img

alb = save('T_Serpent_Albedo', albedo ** (1 / 2.2), True)
vns = save('T_Serpent_Veins', np.repeat(veins[..., None], 3, axis=2), False)
nrm = save('T_Serpent_Normal', normal, False)

# ---- matériaux Principled (aperçu + GLB)
def principled(name):
    m = bpy.data.materials[name]; m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); bs = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nt.links.new(bs.outputs[0], out.inputs[0]); return m, nt, bs

def textured(name, tint=(1, 1, 1, 1)):
    m, nt, bs = principled(name)
    ta = nt.nodes.new('ShaderNodeTexImage'); ta.image = alb
    tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = nrm
    nm = nt.nodes.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = 1.0
    tv = nt.nodes.new('ShaderNodeTexImage'); tv.image = vns
    mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
    mix.inputs['Factor'].default_value = 1.0; mix.inputs[7].default_value = tint
    nt.links.new(ta.outputs[0], mix.inputs[6]); nt.links.new(mix.outputs[2], bs.inputs['Base Color'])
    nt.links.new(tn.outputs[0], nm.inputs['Color']); nt.links.new(nm.outputs[0], bs.inputs['Normal'])
    red = nt.nodes.new('ShaderNodeMix'); red.data_type = 'RGBA'; red.blend_type = 'MULTIPLY'
    red.inputs['Factor'].default_value = 1.0; red.inputs[7].default_value = (1.0, 0.08, 0.04, 1)
    nt.links.new(tv.outputs[0], red.inputs[6]); nt.links.new(red.outputs[2], bs.inputs['Emission Color'])
    bs.inputs['Emission Strength'].default_value = 2.5
    bs.inputs['Roughness'].default_value = 0.42; bs.inputs['Metallic'].default_value = 0.15
    return m

def flat(name, color, rough=0.5, metal=0.0, emit=None):
    m, nt, bs = principled(name)
    bs.inputs['Base Color'].default_value = color; bs.inputs['Roughness'].default_value = rough; bs.inputs['Metallic'].default_value = metal
    if emit: bs.inputs['Emission Color'].default_value = emit; bs.inputs['Emission Strength'].default_value = 4.0

textured('MS_Skin'); textured('MS_Belly'); textured('MS_Membrane', (0.9, 0.8, 1.1, 1))
flat('MS_Crystal', (0.03, 0.03, 0.06, 1), 0.12, 0.35)
flat('MS_Glow', (1, 0.1, 0.05, 1), 0.4, 0, (1, 0.1, 0.05, 1))
flat('MS_Mouth', (0.3, 0.03, 0.06, 1), 0.55); flat('MS_Bone', (0.8, 0.76, 0.66, 1), 0.4)
flat('MS_Black', (0.005, 0.005, 0.008, 1), 0.2); flat('MS_Tongue', (0.5, 0.07, 0.12, 1), 0.45)
print('textures + matériaux ok')
