import bpy, bmesh, math, random
from mathutils import Vector, Matrix

# SERPENT DE LA FAILLE v2 (2026-11-25, retours : « pas assez long, bouche ouverte, tout à plat même la tête (animations
# faites sur un site), rien ne doit traverser le sol, plus de détails : écailles, éléments précis, la totale »).
# Pose : droit le long de +Y (queue en 0, museau en L), TOUT À PLAT, aucun sommet sous z = 0. Gueule ouverte : la
# mâchoire inférieure repose au sol, le crâne pivote de OPEN autour de la charnière.
# Matériaux : 0 peau, 1 ventre, 2 cristal d'obsidienne, 3 rouge incandescent, 4 gueule, 5 os (crochets/dents),
#             6 membrane du capuchon, 7 noir (pupilles, narines), 8 langue.
rnd = random.Random(9)
L = 28.0; HL = 2.8; RMAX = 0.82; RNECK = 0.6
SEG = 56; STEP = 0.06; TILE = 2.0
S_NECK = L - HL
S_HINGE = S_NECK + 0.75
Z_HINGE = 0.34
OPEN = math.radians(30)

def sstep(a, b, x):
    t = min(max((x - a) / (b - a), 0.0), 1.0); return t * t * (3 - 2 * t)

def body_r(s):
    t = s / S_NECK
    if t < 0.5: return 0.03 + (RMAX - 0.03) * math.sin(min(1.0, t / 0.5) * math.pi / 2) ** 0.85
    if t < 0.9:
        k = (t - 0.5) / 0.4; return RMAX - (RMAX - RNECK) * (k * k * (3 - 2 * k))
    return RNECK

def superellipse(a, w, h, n=2.6):
    c, s = math.cos(a), math.sin(a)
    return w * math.copysign(abs(c) ** (2 / n), c), h * math.copysign(abs(s) ** (2 / n), s)

bm = bmesh.new()
UL = bm.verts.layers.float.new('u'); VL = bm.verts.layers.float.new('v')
FT = bm.faces.layers.int.new('kind')     # 0 enveloppe (UV en tour), 1 mâchoire (UV en tour), 2 membrane, 3 cristal, 4 autre
OPENV = bm.verts.layers.float.new('open')   # poids de l'ouverture du crâne (1 = tourne avec le crâne)
# Pour le squelette (build_serpent_rig_v2.py) : 'anchor' = point de la colonne (y au repos) auquel le sommet est rattaché
# (un cristal entier partage l'ancrage de sa base : il reste rigide) ; 'part' = 0 corps, 1 mâchoire inférieure, 2 tête rigide.
AL = bm.verts.layers.float.new('anchor'); PL = bm.verts.layers.int.new('part')
CTX = {'anchor': None, 'part': 0}

def nv(co, u=0.5, v=0.5, op=0.0):
    vt = bm.verts.new(co); vt[UL] = u; vt[VL] = v; vt[OPENV] = op
    vt[AL] = co[1] if CTX['anchor'] is None else CTX['anchor']; vt[PL] = CTX['part']
    return vt

class anchored:
    # with anchored(y, part): tout ce qui est créé dedans est rattaché rigidement à y (et à la partie donnée)
    def __init__(self, a=None, part=None): self.a = a; self.p = part
    def __enter__(self):
        self.sa, self.sp = CTX['anchor'], CTX['part']
        if self.a is not None: CTX['anchor'] = self.a
        if self.p is not None: CTX['part'] = self.p
    def __exit__(self, *x): CTX['anchor'], CTX['part'] = self.sa, self.sp

def face(vs, mat, kind):
    f = bm.faces.new(vs); f.material_index = mat; f[FT] = kind; return f

# ------------------------------------------------------------------ enveloppe corps + crâne
def head_dims(u):
    if u < 0.3:
        k = sstep(0, 0.3, u); w = RNECK * 1.22 + (1.05 - RNECK * 1.22) * k; h = RNECK * 0.9 + (0.36 - RNECK * 0.9) * k
    elif u < 0.85:
        k = (u - 0.3) / 0.55; w = 1.05 - 0.63 * k; h = 0.36 - 0.14 * k
    else:
        k = (u - 0.85) / 0.15; w = 0.42 - 0.28 * k ** 1.4; h = 0.22 - 0.14 * k ** 1.4
    return w, h

def skin_disp(s, a, r):
    sn = math.sin(a)
    wd = sstep(0.55, 0.8, sn); wb = sstep(-0.5, -0.75, sn); wl = max(0.0, 1 - wd - wb)
    u1 = (s / 0.32) % 1.0
    hd = 0.09 * (1 - u1) ** 1.6 + 0.05 * sstep(0.95, 1.0, sn)
    da = 2 * math.pi / 30; row = int(math.floor((a % (2 * math.pi)) / da))
    u2 = (s / 0.2 + 0.5 * (row % 2)) % 1.0
    hl = 0.038 * (1 - u2) ** 1.6 * (0.55 + 0.45 * math.sin(math.pi * ((a % (2 * math.pi)) / da % 1.0)))
    u3 = (s / 0.16) % 1.0
    hb = -0.012 if u3 < 0.12 else 0.0
    k = min(1.0, r / 0.5)
    return (wd * hd + wl * hl) * k, wb * hb * k

stations = []
s = 0.0
while s < S_NECK: stations.append(s); s += STEP
NH = 70
stations += [S_NECK + HL * j / NH for j in range(NH + 1)]
rings = []
for s in stations:
    ring = []
    if s <= S_NECK + 1e-6:
        r = body_r(s); w, h = r * 1.22, r * 0.9; zc = 0.62 * h + 0.004
        for k in range(SEG):
            a = 2 * math.pi * k / SEG
            x, z = superellipse(a, w, h)
            if z < -0.62 * h: z = -0.62 * h
            out, belly = skin_disp(s, a, r)
            ln = math.hypot(x, z) or 1.0
            if math.sin(a) > -0.5: x += x / ln * out; z += z / ln * out
            z -= belly    # rainures ventrales : vers l'intérieur (vers le haut), jamais sous le sol
            ring.append(nv((x, s, zc + z), ((a + math.pi / 2) / (2 * math.pi)) % 1.0, s / TILE))
    else:
        u = (s - S_NECK) / HL
        w, h = head_dims(u)
        bottom = Z_HINGE * sstep(S_HINGE - 0.5, S_HINGE, s)
        zc = bottom + 0.62 * h + 0.004
        op = sstep(S_HINGE - 0.4, S_HINGE + 0.1, s)
        for k in range(SEG):
            a = 2 * math.pi * k / SEG
            x, z = superellipse(a, w, h, 2.9)
            if z < -0.62 * h: z = -0.62 * h
            sn = math.sin(a)
            if z > 0: z *= 0.85 + 0.15 * abs(math.cos(a))
            brow = math.exp(-((u - 0.5) / 0.1) ** 2) * math.exp(-((abs(math.cos(a)) - 0.72) / 0.14) ** 2) * max(0.0, sn)
            z += brow * 0.13; x *= 1 + brow * 0.15
            keel = math.exp(-((u - 0.45) / 0.35) ** 2) * max(0.0, sn) ** 14 * 0.05
            z += keel
            # écussons du dessus de la tête (légères bosses régulières)
            z += 0.012 * max(0.0, sn) ** 3 * (0.5 + 0.5 * math.cos(2 * math.pi * s / 0.22))
            ring.append(nv((x, s, zc + z), ((a + math.pi / 2) / (2 * math.pi)) % 1.0, s / TILE, op))
    rings.append(ring)
for i in range(len(rings) - 1):
    s_mid = (stations[i] + stations[i + 1]) * 0.5
    for k in range(SEG):
        k2 = (k + 1) % SEG
        a = 2 * math.pi * (k + 0.5) / SEG
        if s_mid > S_HINGE - 0.15 and math.sin(a) < -0.55: mat = 4        # palais (dessous du crâne ouvert)
        elif math.sin(a) < -0.55: mat = 1
        else: mat = 0
        face((rings[i][k], rings[i + 1][k], rings[i + 1][k2], rings[i][k2]), mat, 0)
tail = nv((0, -0.08, rings[0][0].co.z))
for k in range(SEG): face((rings[0][(k + 1) % SEG], rings[0][k], tail), 0, 4)
tipz = sum(v.co.z for v in rings[-1]) / SEG
nose = nv((0, L + 0.05, tipz), op=1.0)
for k in range(SEG): face((rings[-1][k], rings[-1][(k + 1) % SEG], nose), 0, 4)

# ------------------------------------------------------------------ mâchoire inférieure (au sol)
JS0 = S_HINGE - 0.3; JS1 = L - 0.12; NJ = 34; JSEG = 28
jr = []
CTX['part'] = 1
for j in range(NJ + 1):
    s = JS0 + (JS1 - JS0) * j / NJ
    u = (s - S_NECK) / HL
    w, _ = head_dims(min(u, 0.99)); w *= 0.9
    top = Z_HINGE - (Z_HINGE - 0.2) * (j / NJ)
    hh = (top - 0.006) / 1.62
    zc = 0.006 + 0.62 * hh
    ring = []
    for k in range(JSEG):
        a = 2 * math.pi * k / JSEG
        x, z = superellipse(a, w, hh, 2.4)
        if z < -0.62 * hh: z = -0.62 * hh
        sn = math.sin(a)
        if sn > 0: z -= hh * 0.9 * sn ** 6          # creux de la gueule entre les deux rebords
        ring.append(nv((x, s, zc + z), ((a + math.pi / 2) / (2 * math.pi)) % 1.0, s / TILE))
    jr.append(ring)
for i in range(NJ):
    for k in range(JSEG):
        k2 = (k + 1) % JSEG
        a = 2 * math.pi * (k + 0.5) / JSEG
        sn = math.sin(a)
        mat = 4 if sn > 0.5 else (1 if sn < -0.55 else 0)
        face((jr[i][k], jr[i + 1][k], jr[i + 1][k2], jr[i][k2]), mat, 1)
c0 = nv((0, JS0 - 0.02, sum(v.co.z for v in jr[0]) / JSEG)); c1 = nv((0, JS1 + 0.06, sum(v.co.z for v in jr[-1]) / JSEG))
for k in range(JSEG):
    face((jr[0][(k + 1) % JSEG], jr[0][k], c0), 0, 4)
    face((jr[-1][k], jr[-1][(k + 1) % JSEG], c1), 0, 4)
CTX['part'] = 0

# ------------------------------------------------------------------ outils : cristaux, cônes, ellipsoïdes
def crystal(base, direction, length, radius, sides=6, mat=2, core=True, flat=1.0, op=0.0):
    with anchored(base.y if CTX['anchor'] is None else None):
        _crystal(base, direction, length, radius, sides, mat, core, flat, op)

def _crystal(base, direction, length, radius, sides, mat, core, flat, op):
    d = direction.normalized()
    side = d.orthogonal().normalized(); up = d.cross(side).normalized()
    tw = rnd.random() * 6.28
    radii = [radius * (0.8 + 0.4 * rnd.random()) for _ in range(sides)]
    def ringp(t, sc):
        return [nv(base + d * (length * t) + (side * math.cos(2 * math.pi * k / sides + tw) + up * math.sin(2 * math.pi * k / sides + tw) * flat) * radii[k] * sc, op=op) for k in range(sides)]
    r0 = ringp(0.0, 0.7); r1 = ringp(0.66, 1.0)
    tip = nv(base + d * length, op=op); heel = nv(base - d * (length * 0.08), op=op)
    for k in range(sides):
        k2 = (k + 1) % sides
        face((r0[k], r0[k2], r1[k2], r1[k]), mat, 3)
        face((r1[k], r1[k2], tip), mat, 3)
        face((r0[k2], r0[k], heel), mat, 3)
    if core:
        c0 = base + d * (length * 0.2); c1 = base + d * (length * 0.72); w = radius * 0.3
        face((nv(c0 + side * w, op=op), nv(c1 + side * w * 0.5, op=op), nv(c1 - side * w * 0.5, op=op), nv(c0 - side * w, op=op)), 3, 4)

def cone(base, direction, length, radius, mat, sides=7, bend=Vector((0, 0, 0)), op=0.0, steps=5):
    with anchored(base.y if CTX['anchor'] is None else None):
        _cone(base, direction, length, radius, mat, sides, bend, op, steps)

def _cone(base, direction, length, radius, mat, sides, bend, op, steps):
    d = direction.normalized(); side = d.orthogonal().normalized(); up = d.cross(side).normalized()
    prev = None
    for i in range(steps + 1):
        t = i / steps
        c = base + d * (length * t) + bend * (t * t)
        rr = radius * (1 - t) ** 0.9
        if i == steps:
            tip = nv(c, op=op)
            for k in range(sides): face((prev[k], prev[(k + 1) % sides], tip), mat, 4)
            break
        ring = [nv(c + (side * math.cos(2 * math.pi * k / sides) + up * math.sin(2 * math.pi * k / sides)) * rr, op=op) for k in range(sides)]
        if prev:
            for k in range(sides):
                face((prev[k], prev[(k + 1) % sides], ring[(k + 1) % sides], ring[k]), mat, 4)
        else:
            cap = nv(c - d * 0.01, op=op)
            for k in range(sides): face((ring[(k + 1) % sides], ring[k], cap), mat, 4)
        prev = ring

def ellipsoid(center, radii, mat, op=0.0, rot=None):
    res = bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=9, radius=1.0)
    for v in res['verts']:
        p = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2]))
        if rot: p = rot @ p
        v.co = center + p; v[OPENV] = op; v[UL] = 0.5; v[VL] = 0.5
        v[AL] = center.y if CTX['anchor'] is None else CTX['anchor']; v[PL] = CTX['part']
    for f in {f for v in res['verts'] for f in v.link_faces}:
        f.material_index = mat; f[FT] = 4

def head_point(u, ang, out=0.0):
    # point de la surface du crâne AVANT ouverture (u le long de la tête, ang autour de la section)
    s = S_NECK + HL * u
    w, h = head_dims(u)
    bottom = Z_HINGE * sstep(S_HINGE - 0.5, S_HINGE, s)
    zc = bottom + 0.62 * h + 0.004
    x, z = superellipse(ang, w + out, h + out, 2.9)
    if z < -0.62 * h: z = -0.62 * h
    return Vector((x, s, zc + z))

# ------------------------------------------------------------------ tête : yeux, narines, crochets, dents, cornes
HEAD_ANCHOR = L - HL * 0.5
for sg in (1, -1):
  with anchored(HEAD_ANCHOR, 2):
    ue = 0.52; ang = math.radians(90 - 50 * sg) if sg > 0 else math.radians(90 + 50)
    p = head_point(ue, math.radians(38) if sg > 0 else math.radians(142))
    ellipsoid(p, (0.12, 0.16, 0.11), 3, op=1.0)                               # œil rouge
    ellipsoid(p + Vector((0.075 * sg, 0.0, 0.005)), (0.014, 0.085, 0.07), 7, op=1.0)   # pupille fendue
    pn = head_point(0.93, math.radians(70) if sg > 0 else math.radians(110))
    ellipsoid(pn + Vector((0, 0, 0.01)), (0.05, 0.07, 0.025), 7, op=1.0)      # narine
    # crochets (2 grands, recourbés vers l'arrière) et dents du haut, pointés vers le bas (avant ouverture)
    pf = head_point(0.8, math.radians(-50) if sg > 0 else math.radians(-130)) + Vector((-0.08 * sg, 0, 0.02))
    cone(pf, Vector((0, 0.12, -1)), 0.5, 0.065, 5, bend=Vector((0, -0.14, 0)), op=1.0)
    for i in range(11):
        u = 0.36 + 0.4 * i / 10
        pt = head_point(u, math.radians(-38) if sg > 0 else math.radians(-142)) + Vector((-0.05 * sg, 0, 0.02))
        cone(pt, Vector((0, 0.05, -1)), 0.13 - 0.04 * (i % 2), 0.026, 5, sides=5, op=1.0, steps=2)
    # dents du bas, sur le rebord de la mâchoire (partie « mâchoire »)
    with anchored(None, 1):
      for i in range(9):
        j = int(4 + 26 * i / 8)
        ring = jr[j]
        best = max(ring, key=lambda v: v.co.z if (v.co.x * sg) > 0 else -9)
        cone(best.co + Vector((-0.04 * sg, 0, -0.01)), Vector((0, 0.05, 1)), 0.11, 0.024, 5, sides=5, steps=2)
    # cornes de cristal à l'arrière du crâne, balayées vers l'arrière et vers le haut
    ph = head_point(0.22, math.radians(60) if sg > 0 else math.radians(120))
    crystal(ph, Vector((0.35 * sg, -1.0, 0.45)), 1.5, 0.13, sides=6, op=0.6)
    ph2 = head_point(0.34, math.radians(48) if sg > 0 else math.radians(132))
    crystal(ph2, Vector((0.5 * sg, -1.0, 0.25)), 0.8, 0.08, sides=5, core=False, op=1.0)
    # épines de joue le long de la mâchoire
    for i in range(4):
        u = 0.18 + 0.12 * i
        pc = head_point(u, math.radians(8) if sg > 0 else math.radians(172))
        crystal(pc, Vector((1.0 * sg, -0.7, 0.15)), 0.45 - 0.07 * i, 0.06, sides=5, core=False, op=sstep(S_HINGE - 0.4, S_HINGE + 0.1, S_NECK + HL * u))

# langue fourchue, posée sur le plancher de la gueule et qui dépasse du menton
TN = 22
prev = None
CTX['part'] = 1
for i in range(TN + 1):
    t = i / TN
    s = S_HINGE + 0.15 + (JS1 + 0.55 - S_HINGE - 0.15) * t
    jj = min(NJ, max(0, int((s - JS0) / (JS1 - JS0) * NJ)))
    floor_z = min(v.co.z for v in jr[jj] if abs(v.co.x) < 0.08) + 0.02 if s <= JS1 else 0.03
    w = 0.09 * (1 - 0.4 * t)
    if t < 0.75:
        ring = [nv((-w, s, floor_z)), nv((0, s, floor_z + 0.03)), nv((w, s, floor_z))]
        if prev:
            face((prev[0], ring[0], ring[1], prev[1]), 8, 4); face((prev[1], ring[1], ring[2], prev[2]), 8, 4)
        prev = ring
    else:
        k = (t - 0.75) / 0.25
        for sg in (1, -1):
            xo = sg * (0.03 + 0.12 * k)
            a1 = nv((xo - 0.025, s, floor_z)); a2 = nv((xo, s, floor_z + 0.02)); a3 = nv((xo + 0.025, s, floor_z))
            tipv = nv((sg * 0.2, s + 0.25, 0.03))
            face((a1, a2, tipv), 8, 4); face((a2, a3, tipv), 8, 4)
        break
CTX['part'] = 0

# ------------------------------------------------------------------ capuchon : nervures de cristal + membrane
S_HOOD = S_NECK - 0.35
CTX['anchor'] = S_HOOD
r_h = body_r(S_HOOD); zc_h = 0.62 * r_h * 0.9 + 0.004
NR = 13; TILT = math.radians(55)   # couché vers l'arrière : le capuchon se lit vu de dessus
ribs = []
for i in range(NR):
    ang = math.radians(-86 + 172 * i / (NR - 1))
    radial = Vector((math.sin(ang), 0, math.cos(ang)))
    dirv = (radial * math.cos(TILT) + Vector((0, -math.sin(TILT), 0))).normalized()
    ln = 1.5 + 1.9 * abs(math.sin(ang)) ** 1.3
    base = Vector((0, S_HOOD, zc_h)) + radial * (r_h * 0.95)
    ribs.append((base, dirv, ln))
    crystal(base, dirv, ln * 1.08, 0.1 + 0.03 * abs(math.sin(ang)), sides=6)
K = 7; M = 5; TH = 0.018
for i in range(NR - 1):
    (b0, d0, l0), (b1, d1, l1) = ribs[i], ribs[i + 1]
    grid_f = []; grid_b = []
    for kk in range(K + 1):
        rowf = []; rowb = []
        for m in range(M + 1):
            f = m / M
            base = b0.lerp(b1, f); dv = d0.lerp(d1, f).normalized(); ln = l0 + (l1 - l0) * f
            edge = 1 - 0.28 * math.sin(math.pi * f)
            p = base + dv * (ln * edge * kk / K)
            nrm = (d1 - d0).cross(dv).normalized() if (d1 - d0).length > 1e-5 else Vector((0, 1, 0))
            uu = (i + f) / (NR - 1); vv = ln * edge * kk / K / TILE
            rowf.append(nv(p + nrm * TH, uu, vv)); rowb.append(nv(p - nrm * TH, uu, vv))
        grid_f.append(rowf); grid_b.append(rowb)
    for kk in range(K):
        for m in range(M):
            face((grid_f[kk][m], grid_f[kk][m + 1], grid_f[kk + 1][m + 1], grid_f[kk + 1][m]), 6, 2)
            face((grid_b[kk][m], grid_b[kk + 1][m], grid_b[kk + 1][m + 1], grid_b[kk][m + 1]), 6, 2)
    for m in range(M):   # bord libre fermé
        face((grid_f[K][m], grid_b[K][m], grid_b[K][m + 1], grid_f[K][m + 1]), 6, 2)

CTX['anchor'] = None
# ------------------------------------------------------------------ crête dorsale, épines des flancs, excroissances
def surface_point(s, a, out=0.0):
    r = body_r(s); w, h = r * 1.22, r * 0.9; zc = 0.62 * h + 0.004
    x, z = superellipse(a, w + out, h + out)
    return Vector((x, s, zc + z)), Vector((x, 0, z)).normalized()

s = S_HOOD - 0.7
while s > 0.9:
    size = 0.25 + 0.75 * (s / S_NECK)
    p, _ = surface_point(s, math.pi / 2, 0.03)
    crystal(p, Vector((0.1 * (rnd.random() - 0.5), -0.5, 1.0)), 1.0 * size + 0.2, 0.1 * size + 0.04, sides=5, core=size > 0.5)
    for sg in (1, -1):
        crystal(p + Vector((0.12 * sg * size, 0.05, -0.02)), Vector((0.45 * sg, -0.4, 1.0)), 0.45 * size + 0.08, 0.05 * size + 0.02, sides=4, core=False)
    s -= 0.5 + 0.25 * (1 - s / S_NECK)
s = S_HOOD - 1.4
while s > 2.5:
    size = 0.3 + 0.7 * (s / S_NECK)
    for sg in (1, -1):
        a = math.pi / 2 - sg * math.radians(58)
        p, n = surface_point(s, a, 0.02)
        crystal(p, n + Vector((0, -0.6, 0.2)), 0.38 * size + 0.05, 0.045 * size + 0.015, sides=4, core=False)
    s -= 0.85
for s0, sg in ((7.5, 1), (11.5, -1), (15.5, 1), (19.0, -1), (22.0, 1)):
    a = math.pi / 2 - sg * math.radians(62)
    p, n = surface_point(s0, a, -0.02)
    for c in range(6):
        jitter = Vector((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), rnd.uniform(0.1, 0.8)))
        crystal(p + Vector((0, rnd.uniform(-0.25, 0.25), 0)), n + jitter, rnd.uniform(0.45, 1.15), rnd.uniform(0.06, 0.12), sides=rnd.choice((5, 6)))
# lame de la queue, dentelée
crystal(Vector((0, 0.45, 0.12)), Vector((0, -1.0, 0.06)), 2.6, 0.24, sides=4, flat=0.35)
for i in range(4):
    for sg in (1, -1):
        crystal(Vector((0.1 * sg, 0.1 - 0.45 * i, 0.12 + 0.01 * i)), Vector((0.8 * sg, -0.6, 0.05)), 0.4 - 0.06 * i, 0.04, sides=4, core=False, flat=0.5)

# ------------------------------------------------------------------ ouverture de la gueule
hinge = Vector((0, S_HINGE, Z_HINGE))
for v in bm.verts:
    w = v[OPENV]
    if w <= 0: continue
    rot = Matrix.Rotation(OPEN * w, 4, 'X')
    v.co = hinge + (rot @ (v.co - hinge))

bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
minz = min(v.co.z for v in bm.verts)
# sécurité sol : remonte le peu qui dépasserait (épines du bas de capuchon, etc.)
if minz < 0:
    for v in bm.verts:
        if v.co.z < 0.0: v.co.z = 0.0

# ------------------------------------------------------------------ maillage, UV, couleurs
name = 'SK_Serpent_Flat'
me = bpy.data.meshes.new(name + '_tmp')
kinds = {}
bm.faces.ensure_lookup_table()
uvl = bm.loops.layers.uv.new('UVMap')
for f in bm.faces:
    if f[FT] in (0, 1, 2):
        us = [l.vert[UL] for l in f.loops]
        if f[FT] != 2 and max(us) - min(us) > 0.5:
            us = [u + 1.0 if u < 0.5 else u for u in us]
        for l, u in zip(f.loops, us): l[uvl].uv = (u, l.vert[VL])
    else:
        for l in f.loops: l[uvl].uv = (0.3, 0.37)   # zone d'écailles du flanc (bouts du museau, du menton, de la queue)
kinds = [f[FT] for f in bm.faces]
bm.to_mesh(me); bm.free()
for p in me.polygons: p.use_smooth = True
col = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
for p in me.polygons:
    li = list(p.loop_indices)
    if kinds[p.index] == 3:
        p.use_smooth = False
        fid = rnd.random()
        if len(li) == 4:
            for k, l in enumerate(li): col.data[l].color = ((0, 1, 1), (1, 0, 1), (1, 0, 1), (0, 1, 1))[k] + (fid,)
        else:
            for k, l in enumerate(li): col.data[l].color = ((1, 0, 0), (0, 1, 0), (0, 0, 1))[k % 3] + (fid,)
    else:
        for l in li: col.data[l].color = (1, 1, 1, 1)
mats = [('MS_Skin', (0.05, 0.05, 0.09, 1)), ('MS_Belly', (0.12, 0.1, 0.15, 1)), ('MS_Crystal', (0.02, 0.02, 0.04, 1)),
        ('MS_Glow', (1, 0.12, 0.08, 1)), ('MS_Mouth', (0.25, 0.03, 0.05, 1)), ('MS_Bone', (0.78, 0.74, 0.66, 1)),
        ('MS_Membrane', (0.07, 0.05, 0.1, 1)), ('MS_Black', (0.0, 0.0, 0.0, 1)), ('MS_Tongue', (0.45, 0.06, 0.1, 1))]
for mn, c in mats:
    m = bpy.data.materials.get(mn) or bpy.data.materials.new(mn); m.diffuse_color = c; me.materials.append(m)
ob = bpy.data.objects.get(name)
if ob is None:
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
else:
    old = ob.data; ob.data = me; bpy.data.meshes.remove(old)
me.name = name; ob.location = (-30, -40, 0)
# lissage des normales seulement entre faces proches (arêtes vives sur les cristaux et les écailles marquées)
try:
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
except Exception as e:
    print('autosmooth', e)
bpy.ops.wm.save_mainfile()
print('verts', len(me.vertices), 'faces', len(me.polygons), 'dims', tuple(round(v, 2) for v in ob.dimensions), 'minz', round(min(v.co.z for v in me.vertices), 4))
