import sys
from PIL import Image, ImageFilter
from collections import deque
src, dst = sys.argv[1], sys.argv[2]
im = Image.open(src).convert("RGB")
W, H = im.size
px = im.load()
def lum(c): return max(c)
# fond = pixels sombres connectés au bord (remplissage depuis les bords)
bg = bytearray(W*H)
q = deque()
for x in range(W):
    for y in (0, H-1): q.append((x,y))
for y in range(H):
    for x in (0, W-1): q.append((x,y))
T = 40
while q:
    x,y = q.popleft()
    i = y*W+x
    if bg[i] or lum(px[x,y]) > T: continue
    bg[i] = 1
    if x>0: q.append((x-1,y))
    if x<W-1: q.append((x+1,y))
    if y>0: q.append((x,y-1))
    if y<H-1: q.append((x,y+1))
mask = Image.new("L", (W,H))
mp = mask.load()
for y in range(H):
    for x in range(W):
        i=y*W+x
        if bg[i]:
            # bord adouci : un fond légèrement éclairé (étincelles, halo) garde un peu d'opacité
            mp[x,y] = min(255, int(lum(px[x,y]) * 255 / T)) if lum(px[x,y]) > 8 else 0
        else:
            mp[x,y] = 255
mask = mask.filter(ImageFilter.GaussianBlur(1.2))
out = im.convert("RGBA"); out.putalpha(mask)
bbox = mask.point(lambda v: 255 if v > 10 else 0).getbbox()
pad = 24
bbox = (max(0,bbox[0]-pad), max(0,bbox[1]-pad), min(W,bbox[2]+pad), min(H,bbox[3]+pad))
out = out.crop(bbox)
w, h = out.size
s = 512 / h
out = out.resize((max(1,round(w*s)), 512), Image.LANCZOS)
out.save(dst)
print(dst, out.size)
