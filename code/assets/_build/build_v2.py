exec(open(r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\_build\charlib.py").read())
COL = "COL_Mascot"
V = Vector

def hexc(h):
    h = h.lstrip('#'); r, g, b = [int(h[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return tuple(c ** 2.2 for c in (r, g, b))

# ============================================================== scene reset (COL_ hierarchy per the pipeline)
for o in list(bpy.data.objects):
    if o.type not in ('CAMERA', 'LIGHT'): bpy.data.objects.remove(o, do_unlink=True)
for c in list(bpy.data.collections):
    if c.name in ('Mascot', 'COL_Mascot'): bpy.data.collections.remove(c)
for m in list(bpy.data.materials): bpy.data.materials.remove(m)
import importlib
_mats.clear()

# ============================================================== palette sampled from the model sheet
P = dict(skin='#ffe3d8', hair='#7a6760', hair2='#5e4e48', hairhi='#a08d86', gray='#8b8b96', graydk='#767682', white='#f8f8fc', stripe='#b9b9c6',
         orange='#f89a2c', sock='#59505f', shoe='#7b4e3d', shoedk='#4a2f27', tie='#3a3436', amber='#e8a526', amberdk='#a8641a', pupil='#2e1b0e')
def M(key, shade=0.74, tint=(0.84, 0.80, 1.0)):
    return toon('MAT_' + key, hexc(P[key]), shade=shade, tint=tint)
mskin = toon('MAT_Skin', hexc(P['skin']), shade=0.84, tint=(1.0, 0.80, 0.84))

def part(obj, name, mat, **kw):
    obj.name = 'SM_Char_Vistara_' + name; obj.data.name = obj.name
    finish(obj, mat, **kw); return obj

def path_tube(name, pts, radii, squash=1.0, segs=12):
    pts = [V(p) for p in pts]; n = len(pts)
    T = [(pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(n)]
    ref = V((1, 0, 0)) if abs(T[0].x) < 0.9 else V((0, 1, 0))
    prev = (ref - T[0] * ref.dot(T[0])).normalized(); Ns = []
    for i in range(n):
        nn = prev - T[i] * prev.dot(T[i])
        if nn.length < 1e-6: nn = ref
        nn.normalize(); Ns.append(nn); prev = nn
    bm = bmesh.new(); layers = []
    for i, p in enumerate(pts):
        B = T[i].cross(Ns[i]); ring = []
        for k in range(segs):
            a = 2 * math.pi * k / segs
            ring.append(bm.verts.new(p + Ns[i] * math.cos(a) * radii[i] + B * math.sin(a) * radii[i] * squash))
        layers.append(ring)
    for A, Bb in zip(layers[:-1], layers[1:]):
        for i in range(segs):
            j = (i + 1) % segs; bm.faces.new((A[i], A[j], Bb[j], Bb[i]))
    bm.faces.new(layers[0]); bm.faces.new(layers[-1][::-1])
    bm.normal_update(); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return from_bm(name, bm)

def tri(x):
    return 2.0 * abs(x - math.floor(x + 0.5)) * 2.0 - 1.0 if False else (abs((x % 1.0) - 0.5) * 4.0 - 1.0)

# ============================================================== TIER 1: proportions (measured from the sheet: 598 px = 1.60 m, ~5.5 heads)
# ---------------------------------------------------------------- legs, socks, shoes (leg centres +-0.092 -> 0.108, set back +0.07)
for s, sx in (('R', 1), ('L', -1)):
    prof = [(0.70, 0.056, 0.052), (0.60, 0.054, 0.050), (0.50, 0.046, 0.045), (0.42, 0.043, 0.044), (0.356, 0.043, 0.043), (0.25, 0.037, 0.038), (0.14, 0.031, 0.033), (0.10, 0.030, 0.032)]
    def cx(z): return sx * (0.092 + (0.70 - z) / 0.60 * 0.016)
    rings = [(V((cx(z), 0.07, z)), rx, ry, 0) for (z, rx, ry) in prof]
    leg = loft('x', rings, segs=22, close_bottom=True)
    part(leg, 'Leg_' + s, mskin, subsurf=1, outline=0.003)
    sock_rings = [(V((cx(z), 0.07, z)), rx + 0.0035, ry + 0.0035, 0) for (z, rx, ry) in prof if z <= 0.357]
    sock_rings = [(V((cx(0.356), 0.07, 0.356)), 0.0425, 0.0425, 0)] + sock_rings[1:]
    sock = loft('x', sock_rings, segs=22, close_bottom=True)
    part(sock, 'Sock_' + s, M('sock'), subsurf=1, outline=0.003)
    ax = cx(0.10); yaw = math.radians(-9 * sx)
    shoe = ellipsoid('x', V((ax, -0.010, 0.036)), (0.043, 0.118, 0.034), segs=26, rings=14, rot=(0, 0, yaw))
    part(shoe, 'Shoe_' + s, M('shoe', 0.68), subsurf=1, outline=0.003)
    sole = ellipsoid('x', V((ax, -0.010, 0.010)), (0.046, 0.123, 0.012), segs=26, rings=8, rot=(0, 0, yaw))
    part(sole, 'ShoeSole_' + s, M('shoedk'), subsurf=0, outline=0.002)
    strap = ellipsoid('x', V((ax - 0.0 * sx, -0.048, 0.064)), (0.040, 0.026, 0.0075), segs=18, rings=6, rot=(0, 0, yaw))
    part(strap, 'ShoeStrap_' + s, M('shoedk', 0.7), subsurf=0)

# ---------------------------------------------------------------- skirt: 24 box pleats, hem 0.64 m, waist 0.935 m
segs = 144; bm = bmesh.new(); layers = []
SK = [(0.935, 0.104), (0.84, 0.132), (0.74, 0.166), (0.64, 0.200)]
for li, (z, r) in enumerate(SK):
    ring = []
    for i in range(segs):
        a = 2 * math.pi * i / segs - math.pi / 2
        depth = [0.0, 0.35, 0.8, 1.0][li]
        pl = 1 + depth * 0.055 * tri(24 * a / (2 * math.pi))
        zz = z + (0.002 * tri(24 * a / (2 * math.pi) + 0.25) if li == 3 else 0.0)
        ring.append(bm.verts.new(V((math.cos(a) * r * pl, -0.025 + math.sin(a) * r * pl * 0.93, zz))))
    layers.append(ring)
for A, B in zip(layers[:-1], layers[1:]):
    for i in range(segs):
        j = (i + 1) % segs; bm.faces.new((A[i], A[j], B[j], B[i]))
bm.normal_update(); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
skirt = from_bm('x', bm); part(skirt, 'Skirt', M('gray', 0.62), smooth=False, outline=0.003, solidify=0.004)

# ---------------------------------------------------------------- jacket (cropped sailor top) + waist band + chest skin under the V
J = [(0.915, 0.108, 0.098), (0.935, 0.114, 0.103), (1.00, 0.117, 0.106), (1.10, 0.122, 0.108), (1.19, 0.128, 0.102), (1.24, 0.122, 0.090), (1.282, 0.056, 0.054)]
jacket = loft('x', [(V((0, 0, z)), rx, ry, 0) for (z, rx, ry) in J], segs=36, close_bottom=True)
part(jacket, 'Jacket', M('gray', 0.62), subsurf=1, outline=0.003, solidify=0.004)
band = loft('x', [(V((0, -0.025, 0.905)), 0.108, 0.100, 0), (V((0, -0.025, 0.940)), 0.111, 0.103, 0)], segs=36)
part(band, 'WaistBand', M('graydk', 0.6), outline=0.002, solidify=0.003)
chest = ellipsoid('x', V((0, -0.070, 1.262)), (0.050, 0.030, 0.040), segs=18, rings=10)
part(chest, 'ChestSkin', mskin, subsurf=1)
bib = ellipsoid('x', V((0, -0.098, 1.175)), (0.050, 0.016, 0.050), segs=18, rings=10)
part(bib, 'InnerBib', M('graydk', 0.62), subsurf=1)

# ---------------------------------------------------------------- neck
neck = tube('x', (0, 0.008, 1.285), (0, 0.008, 1.372), [(0, 0.036), (1, 0.032)], segs=18)
part(neck, 'Neck', mskin, subsurf=1)

# ---------------------------------------------------------------- sleeves, cuffs, hands
ARMS = {'L': (V((-0.140, 0.005, 1.225)), V((-0.186, 0.020, 0.815))), 'R': (V((0.140, 0.005, 1.225)), V((0.232, 0.010, 0.812)))}
for s, (sh, wr) in ARMS.items():
    d = wr - sh; dn = d.normalized(); sg = 1 if s == 'R' else -1
    sl = tube('x', sh, wr, [(0, 0.049), (0.15, 0.051), (0.5, 0.045), (0.78, 0.048), (0.9, 0.057), (0.955, 0.050)], segs=22)
    part(sl, 'Sleeve_' + s, M('gray', 0.62), subsurf=1, outline=0.003)
    c0 = wr - d * 0.085
    cf = tube('x', c0, wr + d * 0.02, [(0, 0.0455), (1, 0.0425)], segs=22)
    part(cf, 'Cuff_' + s, M('white', 0.8), subsurf=1, outline=0.0025)
    for k, f in enumerate((0.30, 0.62)):
        a0 = c0 + d * 0.105 * f * 0.9
        st = tube('x', a0, a0 + d * 0.012, [(0, 0.0472), (1, 0.0472)], segs=22)
        part(st, 'CuffStripe_%s%d' % (s, k), M('orange', 0.8), smooth=True)
    side = V((0, 1, 0)).cross(dn).normalized(); nrm = dn.cross(side).normalized()
    hc = wr + dn * 0.052
    palm = ellipsoid('x', hc, (0.024, 0.0105, 0.034), segs=18, rings=10)
    m3 = Matrix(((side.x, nrm.x, dn.x), (side.y, nrm.y, dn.y), (side.z, nrm.z, dn.z)))
    palm.rotation_euler = m3.to_euler(); part(palm, 'Palm_' + s, mskin, subsurf=1, outline=0.0022)
    spread = 1.0 if s == 'R' else 0.2
    for fi in range(4):
        off = (fi - 1.5) * 0.0125; L_ = 0.047 - abs(fi - 1.5) * 0.005; ang = (fi - 1.5) * 0.20 * spread
        base = hc + side * off + dn * 0.034
        tip = base + dn * (L_ * math.cos(ang)) + side * (math.sin(ang) * L_) + nrm * (0.012 if s == 'L' else 0.0)
        f = tube('x', base, tip, [(0, 0.0092), (0.8, 0.0078), (1, 0.0058)], segs=8)
        part(f, 'Finger_%s%d' % (s, fi), mskin, subsurf=1, outline=0.0016)
    thb = hc + side * (0.026 * sg) + dn * 0.006
    th = tube('x', thb, thb + side * (0.014 * sg) + dn * 0.030 + nrm * (0.010 if s == 'L' else -0.004), [(0, 0.011), (1, 0.0072)], segs=8)
    part(th, 'Thumb_' + s, mskin, subsurf=1, outline=0.0016)

# ============================================================== TIER 2: collar (V front, shoulder wings, square back flap) + bow
N = 72; bm = bmesh.new(); vi = []; vo = []
for i in range(N):
    a = 2 * math.pi * i / N; c, sn = math.cos(a), math.sin(a); front = max(0.0, -sn)
    xi = 0.054 * c * (1 - 0.88 * front ** 3); yi = 0.054 * sn - 0.062 * front; zi = 1.300 - 0.150 * front ** 1.5
    xo = 0.158 * c; yo = 0.118 * sn if sn > 0 else 0.126 * sn
    zo = 1.246 - 0.125 * front ** 1.25 * (1 - abs(c)) ** 0.6 - 0.105 * max(0.0, sn) ** 1.3
    vi.append(bm.verts.new(V((xi, yi, zi)))); vo.append(bm.verts.new(V((xo, yo, zo))))
for i in range(N):
    j = (i + 1) % N; bm.faces.new((vi[i], vi[j], vo[j], vo[i]))
bm.normal_update(); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bmesh.ops.reverse_faces(bm, faces=bm.faces)
collar = from_bm('x', bm); part(collar, 'Collar', M('white', 0.8), subsurf=1, outline=0.003, solidify=0.008)
# two thin grey trim lines along the collar edge (as in the sheet)
for k, off in enumerate((0.012, 0.024)):
    bm = bmesh.new(); ring_a = []; ring_b = []
    for i in range(N):
        a = 2 * math.pi * i / N; c, sn = math.cos(a), math.sin(a); front = max(0.0, -sn)
        xo = (0.158 - off) * c; yo = (0.118 if sn > 0 else 0.126) * sn * (1 - off * 2.2)
        zo = 1.246 - 0.125 * front ** 1.25 * (1 - abs(c)) ** 0.6 - 0.105 * max(0.0, sn) ** 1.3 + 0.0
        ring_a.append(bm.verts.new(V((xo, yo, zo + 0.0045)))); ring_b.append(bm.verts.new(V((xo, yo, zo + 0.0075))))
    for i in range(N):
        j = (i + 1) % N; bm.faces.new((ring_a[i], ring_a[j], ring_b[j], ring_b[i]))
    bm.normal_update()
    tr = from_bm('x', bm); part(tr, 'CollarTrim%d' % k, M('stripe', 0.9), smooth=True, solidify=0.004)

bc = V((0, -0.122, 1.100))
for s, sx in (('R', 1), ('L', -1)):
    lp = ellipsoid('x', bc + V((sx * 0.034, -0.004, 0.002)), (0.035, 0.010, 0.020), segs=18, rings=10, rot=(0, math.radians(-8 * sx), math.radians(14 * sx)))
    part(lp, 'BowLoop_' + s, M('orange'), subsurf=1, outline=0.002)
    tl = ribbon('x', [bc + V((sx * 0.010, -0.004, -0.012)), bc + V((sx * 0.024, -0.005, -0.050)), bc + V((sx * 0.033, -0.004, -0.090))], [0.011, 0.014, 0.008], V((1, 0, 0)), subdiv_cols=3)
    part(tl, 'BowTail_' + s, M('orange'), smooth=True, solidify=0.003, outline=0.0015)
kn = ellipsoid('x', bc + V((0, -0.006, 0)), (0.014, 0.012, 0.015), segs=14, rings=8)
part(kn, 'BowKnot', M('orange'), subsurf=1, outline=0.002)
for k, z in enumerate((1.020, 0.955)):
    b = ellipsoid('x', V((0, -0.116, z)), (0.0095, 0.0045, 0.0095), segs=12, rings=8)
    part(b, 'Button_%d' % k, M('white', 0.85))

# ============================================================== head: skull + chin taper (face width 0.156, chin at z 1.317)
HC = V((0, 0, 1.415)); RX, RY, RZ = 0.078, 0.088, 0.098
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=28, radius=1.0)
for v in bm.verts:
    x, y, z = v.co; k = 1.0 + 0.50 * min(0.0, z)
    v.co = V((x * RX * k, y * RY * k * (1.0 + 0.06 * min(0.0, z)), z * RZ))
head = from_bm('x', bm); head.location = HC
part(head, 'Head', mskin, subsurf=1, outline=0.003)

def head_pt(x, zo, off=0.0):
    t = zo / RZ; k = 1.0 + 0.50 * min(0.0, t); ky = RY * k * (1.0 + 0.06 * min(0.0, t))
    a = x / (RX * k); b = zo / RZ; s = max(0.0, 1 - a * a - b * b); y = -ky * math.sqrt(s)
    n = V((x / (RX * k) ** 2, y / ky ** 2, zo / RZ ** 2)).normalized()
    return V((x, y, HC.z + zo)) + n * off, n

def disc(name, x, zo, sx, sz, off, rgb, spin=0.0):
    p, n = head_pt(x, zo, off)
    o = ellipsoid('x', p, (sx, sz, 0.0006), segs=28, rings=6)
    q = n.to_track_quat('Z', 'Y'); o.rotation_euler = (q @ Quaternion((0, 0, 1), spin)).to_euler()
    o.name = 'SM_Char_Vistara_Face_' + name; o.data.name = o.name
    o.data.materials.append(flat(name, rgb))
    for pg in o.data.polygons: pg.use_smooth = True
    return o

EZ = -0.008
for s, sx in (('R', 1), ('L', -1)):
    cx_ = 0.049 * sx
    disc('Sclera' + s, cx_, EZ, 0.0340, 0.0295, 0.0010, hexc('#fff8f4'))
    disc('IrisDk' + s, cx_ - 0.001 * sx, EZ - 0.001, 0.0272, 0.0290, 0.0016, hexc(P['amberdk']))
    disc('IrisLt' + s, cx_ - 0.001 * sx, EZ - 0.0085, 0.0245, 0.0175, 0.0021, hexc(P['amber']))
    disc('Pupil' + s, cx_ - 0.001 * sx, EZ - 0.0015, 0.0105, 0.0185, 0.0026, hexc(P['pupil']))
    disc('HiBig' + s, cx_ + 0.0095 * sx, EZ + 0.0130, 0.0085, 0.0085, 0.0033, (1, 1, 1))
    disc('HiSm' + s, cx_ - 0.0085 * sx, EZ - 0.0150, 0.0038, 0.0038, 0.0033, (1, 1, 1))
    top = [(-0.041, 0.003), (-0.030, 0.030), (-0.005, 0.039), (0.022, 0.034), (0.040, 0.016), (0.052, 0.010)]
    l1 = bez_line('Lash_' + s, [head_pt(cx_ + dx * sx, EZ + dz, 0.0030)[0] for dx, dz in top], 0.0024, hexc('#2c1912'))
    l1.data.bevel_depth = 0.0020
    l1.name = 'SM_Char_Vistara_Face_LashTop_' + s
    disc('Blush' + s, 0.058 * sx, EZ - 0.040, 0.014, 0.008, 0.0014, hexc('#ffc4bd'))
    ear = ellipsoid('x', V((0.078 * sx, 0.012, 1.405)), (0.009, 0.014, 0.020), segs=14, rings=8)
    part(ear, 'Ear_' + s, mskin, subsurf=1)
disc('Mouth', 0.0, EZ - 0.066, 0.0075, 0.0045, 0.0020, hexc('#b25a55'))
disc('Nose', 0.0, EZ - 0.040, 0.0030, 0.0018, 0.0016, hexc('#eba490'))

# ============================================================== hair (masses, per the anime-style / hair-groom notes)
HCAP = V((0, 0.012, 1.485)); hr = (0.118, 0.110, 0.120)
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=24, radius=1.0)
for v in bm.verts: v.co = V((v.co.x * hr[0], v.co.y * hr[1], v.co.z * hr[2]))
dele = []
for f in bm.faces:
    c = f.calc_center_median(); wz = HCAP.z + c.z; wy = HCAP.y + c.y
    if wy < -0.025 and wz < 1.515: dele.append(f)
    elif wz < 1.405 and wy < 0.03: dele.append(f)
    elif wz < 1.365: dele.append(f)
bmesh.ops.delete(bm, geom=dele, context='FACES')
cap = from_bm('x', bm); cap.location = HCAP
part(cap, 'HairCap', M('hair', 0.62), subsurf=1, outline=0.004, solidify=0.005)

def hair_front(x, z, off=0.012):
    a = x / (hr[0] + off); b = (z - HCAP.z) / (hr[2] + off); s = max(0.02, 1 - a * a - b * b)
    return V((x, HCAP.y - (hr[1] + off) * math.sqrt(s), z))

def lock(name, pts, widths, mat, lat=V((1, 0, 0)), thick=0.004, ol=0.0025):
    o = ribbon('x', pts, widths, lat, subdiv_cols=3); part(o, name, mat, subsurf=1, outline=ol, solidify=thick); return o

bangs = [(-0.012, -0.056, 1.450, 0.044), (-0.034, -0.082, 1.436, 0.042), (-0.060, -0.102, 1.418, 0.038), (-0.086, -0.108, 1.385, 0.032),
         (0.012, 0.054, 1.454, 0.044), (0.034, 0.082, 1.442, 0.042), (0.060, 0.104, 1.422, 0.038), (0.086, 0.110, 1.390, 0.032)]
for i, (xr, xt, zt, w) in enumerate(bangs):
    pts = [hair_front(xr, 1.572), hair_front(xr + (xt - xr) * 0.35, 1.535), hair_front(xr + (xt - xr) * 0.75, 1.495, 0.016), hair_front(xt, zt, 0.015)]
    lock('Bang_%d' % i, pts, [w * 0.55, w * 0.62, w * 0.45, 0.0035], M('hair' if i % 2 else 'hair2', 0.62), thick=0.004, ol=0.002)
mid = [hair_front(-0.004, 1.555, 0.014), hair_front(-0.005, 1.500, 0.017), hair_front(-0.007, 1.440, 0.020), hair_front(-0.008, 1.385, 0.020)]
lock('BangMid', mid, [0.010, 0.0105, 0.008, 0.002], M('hair2', 0.62), thick=0.004, ol=0.0015)
for s, sx in (('R', 1), ('L', -1)):      # long side locks framing the face down to the chest
    pts = [V((0.108 * sx, -0.015, 1.510)), V((0.112 * sx, -0.040, 1.470)), V((0.112 * sx, -0.050, 1.390)), V((0.106 * sx, -0.054, 1.312)), V((0.098 * sx, -0.050, 1.235))]
    lock('SideLock_' + s, pts, [0.032, 0.034, 0.031, 0.024, 0.003], M('hair'), thick=0.006)

root = V((0.108, 0.070, 1.548))
pts = [root, V((0.122, 0.098, 1.497)), V((0.142, 0.112, 1.410)), V((0.154, 0.113, 1.300)), V((0.158, 0.108, 1.170)), V((0.152, 0.104, 1.050)), V((0.146, 0.100, 0.975))]
for k, (dx, dy, sc) in enumerate(((0, 0, 1.0), (-0.012, 0.014, 0.78), (0.012, 0.024, 0.70))):
    pp = [p + V((dx * (i / 6.0), dy, 0)) for i, p in enumerate(pts)]
    rad = [r * sc for r in (0.021, 0.034, 0.041, 0.040, 0.032, 0.019, 0.003)]
    t = path_tube('x', pp, rad, squash=0.62, segs=14)
    part(t, 'Ponytail_%d' % k, M('hair' if k != 1 else 'hair2', 0.60), subsurf=1, outline=0.0035)
tie = ellipsoid('x', root + V((0.004, 0.002, -0.004)), (0.027, 0.024, 0.013), segs=18, rings=8)
part(tie, 'HairTie', M('tie'), subsurf=1, outline=0.002)
# crown highlights (the glossy bands in the sheet)
for k, (x0, x1) in enumerate(((-0.060, -0.040), (-0.010, 0.012), (0.040, 0.058))):
    pts = [HCAP + V((x0 * 0.9, -0.040, 0.100)), HCAP + V(((x0 + x1) / 2, -0.080, 0.070)), HCAP + V((x1, -0.100, 0.040))]
    pts = [hair_front(p.x, p.z, 0.0125) for p in pts]
    hl = ribbon('x', pts, [0.008, 0.010, 0.003], V((1, 0, 0)), subdiv_cols=3)
    part(hl, 'HairShine_%d' % k, M('hairhi', 0.9), smooth=True)

# ============================================================== organise: root empty + collection (COL_ / SM_ naming)
root_e = bpy.data.objects.new('Vistara_Mascot_Root', None); collection().objects.link(root_e)
for o in collection().objects:
    if o is not root_e and o.parent is None:
        o.parent = root_e
result = {"objects": len(collection().objects), "tris_est": sum(len(o.data.polygons) for o in collection().objects if o.type == 'MESH')}
