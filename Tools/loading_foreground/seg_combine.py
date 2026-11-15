import numpy as np
from PIL import Image, ImageFilter

d = 'seg/'
bg = Image.open(d + 'bg.png').convert('RGB')
W, H = bg.size
rgb = np.asarray(bg).astype(np.float32) / 255.0
hsv = np.asarray(bg.convert('HSV')).astype(np.float32)
h = hsv[..., 0] / 255.0 * 360.0
s = hsv[..., 1] / 255.0
v = hsv[..., 2] / 255.0

def m(name):
    return np.asarray(Image.open(d + name)).astype(np.float32) / 255.0

body = np.maximum.reduce([m('mask_bir.png'), m('mask_wA.png'), m('mask_wB.png')])

yy, xx = np.mgrid[0:H, 0:W]

def box(x0, y0, x1, y1):
    return (xx >= x0) & (xx < x1) & (yy >= y0) & (yy < y1)

# Lueurs magiques, par couleur, chacune dans la zone de son personnage (coordonnées 1920 x 1080).
green = ((h > 70) & (h < 170) & (s > 0.35) & (v > 0.45)) & box(270, 500, 760, 960)        # spirale de Kael
fire = ((h < 45) | (h > 340)) & (s > 0.45) & (v > 0.6) & box(700, 420, 1180, 820)        # feu d'Aether
violet = ((h > 220) & (h < 300) & (s > 0.12) & (v > 0.62)) & box(1150, 260, 1560, 820)    # volutes de Lyra
glow = (green | fire | violet).astype(np.float32)
glow_img = Image.fromarray((glow * 255).astype(np.uint8))
# referme les trous du tracé (les lueurs sont des traits fins semi-transparents) puis adoucit
glow_img = glow_img.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(2))
glow = np.asarray(glow_img).astype(np.float32) / 255.0 * 0.9

mask = np.maximum(body, glow)
# bord net mais sans escalier
mask_img = Image.fromarray((np.clip(mask, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8))
mask_img.save(d + 'mask_final.png')

a = np.asarray(mask_img).astype(np.float32)[..., None] / 255.0
over = rgb * (1 - a * 0.65) + np.array([1.0, 0.15, 0.15]) * a * 0.65
Image.fromarray((over * 255).astype(np.uint8)).resize((960, 540), Image.LANCZOS).save(d + 'overlay_final.png')
print('ok')
