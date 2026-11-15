import sys, numpy as np, onnxruntime as ort
from PIL import Image

# usage: seg_run.py model src out kind [crop x0 y0 x1 y1]
model, src, out, kind = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
img = Image.open(src).convert("RGB")
crop = None
if len(sys.argv) > 5:
    crop = tuple(int(v) for v in sys.argv[5:9])
    work = img.crop(crop)
else:
    work = img
W, H = work.size
sess = ort.InferenceSession(model, providers=["CPUExecutionProvider"])
inp = sess.get_inputs()[0]
size = 1024
x = np.asarray(work.resize((size, size), Image.LANCZOS)).astype(np.float32) / 255.0
std = np.array([0.229, 0.224, 0.225], np.float32) if kind == "birefnet" else np.array([1.0, 1.0, 1.0], np.float32)
x = (x - np.array([0.485, 0.456, 0.406], np.float32)) / std
x = x.transpose(2, 0, 1)[None]
pred = sess.run(None, {inp.name: x})[0][:, 0, :, :]
if kind == "birefnet":
    pred = 1.0 / (1.0 + np.exp(-pred))
mi, ma = pred.min(), pred.max()
pred = (pred - mi) / (ma - mi)
mask = Image.fromarray((np.squeeze(pred) * 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)
if crop:
    full = Image.new("L", img.size, 0)
    full.paste(mask, crop[:2])
    mask = full
mask.save(out)
print("ok", W, H)
