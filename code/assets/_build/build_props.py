exec(open(r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\_build\charlib.py").read())
COL = "COL_VStar"
V = Vector
arm = bpy.data.objects['ARM_VStar']
ZC = 0.55

for o in list(bpy.data.objects):
    if o.name.startswith('P_'): bpy.data.objects.remove(o, do_unlink=True)

def hexc(h):
    h = h.lstrip('#'); return tuple(int(h[i:i+2], 16) / 255 for i in (0, 2, 4))
def pmat(name, hexcol):
    key = 'flat_' + name
    m = bpy.data.materials.get(key)
    if m is None:
        m = bpy.data.materials.new(key); m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes): nt.nodes.remove(n)
        out = nt.nodes.new('ShaderNodeOutputMaterial'); em = nt.nodes.new('ShaderNodeEmission')
        em.inputs['Color'].default_value = tuple(c ** 2.2 for c in hexc(hexcol)) + (1,)
        nt.links.new(em.outputs[0], out.inputs[0])
        m.diffuse_color = hexc(hexcol) + (1.0,)
    return m
MAIN, ACC, DARK, WHITE, GLOW = pmat('PropMain', '#e03c3c'), pmat('PropAccent', '#ffffff'), pmat('PropDark', '#2a2630'), pmat('PropWhite', '#f6f3ee'), pmat('PropGlow', '#ffe58a')

def ell(c, r, mat, rot=(0, 0, 0), cut_below=None, segs=24, rings=14):
    o = ellipsoid('t', V(c), r, segs=segs, rings=rings, rot=rot)
    if cut_below is not None:
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < cut_below], context='VERTS')
        bm.to_mesh(o.data); bm.free()
    o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth = True
    return o

def ptube(pts, radii, mat, squash=1.0, segs=12):
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
    closed = (pts[0] - pts[-1]).length < 1e-5
    last = n - 1 if not closed else n - 1
    for A, Bb in zip(layers[:-1], layers[1:]):
        for i in range(segs):
            j = (i + 1) % segs; bm.faces.new((A[i], A[j], Bb[j], Bb[i]))
    if not closed:
        bm.faces.new(layers[0]); bm.faces.new(layers[-1][::-1])
    bm.normal_update(); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new('t'); bm.to_mesh(me); bm.free()
    o = new_obj('t', me); o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth = True
    return o

def ring_pts(cx, cy, cz, rx, ry, n=36, plane='xy', power=2.0):
    pts = []
    for i in range(n + 1):
        a = 2 * math.pi * (i % n) / n; c, s = math.cos(a), math.sin(a)
        x = rx * math.copysign(abs(c) ** (2 / power), c); y = ry * math.copysign(abs(s) ** (2 / power), s)
        pts.append((cx + x, cy + y, cz) if plane == 'xy' else (cx + x, cy, cz + y))
    return pts

def finalize(name, parts, group):
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = name; o.data.name = name
    for g in group if isinstance(group, list) else [group]:
        pass
    return o

def attach(o, groups):
    """groups: {bone: predicate(x)}; every vertex goes to exactly one bone group."""
    for g in groups: o.vertex_groups.new(name=g)
    for v in o.data.vertices:
        for g, pred in groups.items():
            if pred(v.co.x): o.vertex_groups[g].add([v.index], 1.0, 'REPLACE'); break
    m = o.modifiers.new('Armature', 'ARMATURE'); m.object = arm; o.parent = arm
    for c in o.users_collection: c.objects.unlink(o)
    collection().objects.link(o)

def build(name, parts, groups):
    bpy.ops.object.select_all(action='DESELECT')
    o = finalize(name, parts, None); attach(o, groups); return o
TOP = {'Top': lambda x: True}; BODY = {'Body': lambda x: True}

# ---------- head wear sits on the top point (tip at z = 1.05)
def dome(c, r, mat_main, stripe=True):
    ps = [ell(c, r, mat_main, cut_below=c[2] - 0.015)]
    return ps

# chef hat
parts = [ptube([(0, 0, 0.955), (0, 0, 1.005)], [0.090, 0.090], WHITE), ell((0, 0, 1.10), (0.115, 0.105, 0.085), WHITE)]
for dx, dy in ((0.07, 0), (-0.07, 0), (0, 0.07), (0, -0.07)): parts.append(ell((dx, dy, 1.065), (0.065, 0.065, 0.065), WHITE))
build('P_Chef', parts, TOP)
# helmet (bike / bike polo / motor): dome + stripe + small visor
parts = [ell((0, 0, 0.985), (0.128, 0.122, 0.118), MAIN, cut_below=0.968), ell((0, 0, 0.99), (0.026, 0.126, 0.121), ACC, cut_below=0.968),
         ell((0, -0.115, 0.962), (0.10, 0.07, 0.016), DARK, rot=(math.radians(-10), 0, 0))]
build('P_Helmet', parts, TOP)
# cowboy hat
parts = [ell((0, 0, 0.985), (0.235, 0.205, 0.013), MAIN), ptube([(0, 0, 0.985), (0, 0, 1.085)], [0.088, 0.074], MAIN), ell((0, 0, 1.087), (0.075, 0.068, 0.014), MAIN),
         ptube([(0, 0, 0.995), (0, 0, 1.012)], [0.092, 0.092], DARK)]
build('P_Cowboy', parts, TOP)
# headband with two tails
parts = [ptube(ring_pts(0, 0, 0.935, 0.095, 0.085, 36), [0.013] * 37, MAIN, squash=0.8)]
for sx in (1, -1):
    parts.append(ptube([(0.05 * sx, 0.085, 0.935), (0.09 * sx, 0.13, 0.90), (0.12 * sx, 0.16, 0.85)], [0.014, 0.012, 0.007], MAIN, squash=0.4))
build('P_Headband', parts, TOP)
# cap (forward / backward brim)
for nm, side in (('P_CapFwd', -1), ('P_CapBack', 1)):
    parts = [ell((0, 0, 0.985), (0.118, 0.115, 0.108), MAIN, cut_below=0.968), ell((0, side * 0.125, 0.963), (0.085, 0.085, 0.012), ACC),
             ell((0, 0, 1.09), (0.014, 0.014, 0.010), ACC)]
    build(nm, parts, TOP)
# beanie with pom-pom
parts = [ell((0, 0, 0.98), (0.122, 0.115, 0.122), MAIN, cut_below=0.955), ptube(ring_pts(0, 0, 0.96, 0.118, 0.110, 36), [0.020] * 37, ACC, squash=0.9),
         ell((0, 0, 1.115), (0.042, 0.042, 0.042), WHITE)]
build('P_Beanie', parts, TOP)
# aviator cap with side flaps
parts = [ell((0, 0, 0.985), (0.118, 0.115, 0.105), MAIN, cut_below=0.96), ell((0.115, 0, 0.935), (0.03, 0.07, 0.06), MAIN), ell((-0.115, 0, 0.935), (0.03, 0.07, 0.06), MAIN),
         ptube(ring_pts(0, 0, 0.955, 0.12, 0.112, 36), [0.008] * 37, DARK)]
build('P_AviatorCap', parts, TOP)
# headlamp helmet (rock climbing)
parts = [ell((0, 0, 0.985), (0.128, 0.122, 0.118), WHITE, cut_below=0.968), ell((0, 0, 0.99), (0.026, 0.126, 0.121), MAIN, cut_below=0.968),
         ell((0, -0.122, 0.975), (0.036, 0.028, 0.03), DARK), ell((0, -0.147, 0.975), (0.026, 0.012, 0.022), GLOW)]
build('P_Headlamp', parts, TOP)

# ---------- face wear (follows the body); eyes sit at x = +-0.105, z = 0.595, front plane y ~ -0.18
FY = -0.285
def eye_frame(sx, mat, rx=0.060, rz=0.066, minor=0.009):
    return ptube(ring_pts(0.128 * sx, FY, 0.575, rx, rz, 32, plane='xz'), [minor] * 33, mat, squash=0.9)
# goggles (ski / plane / motor): frames + bridge + side strap stubs, eyes stay visible
parts = [eye_frame(1, DARK), eye_frame(-1, DARK), eye_frame(1, MAIN, 0.068, 0.074, 0.005), eye_frame(-1, MAIN, 0.068, 0.074, 0.005),
         ptube([(-0.045, FY, 0.60), (0.0, FY - 0.006, 0.605), (0.045, FY, 0.60)], [0.010] * 3, DARK)]
for sx in (1, -1): parts.append(ptube([(0.165 * sx, FY + 0.02, 0.60), (0.22 * sx, FY + 0.07, 0.60), (0.26 * sx, FY + 0.13, 0.60)], [0.012, 0.011, 0.010], DARK, squash=0.4))
build('P_Goggles', parts, BODY)
# sunglasses (surfing): opaque dark lenses with a glint
parts = [ell((0.128 * sx, FY - 0.004, 0.575), (0.066, 0.010, 0.056), DARK) for sx in (1, -1)]
parts += [ell((0.090 * sx, FY - 0.014, 0.610), (0.014, 0.004, 0.008), WHITE) for sx in (1, -1)]
parts += [ptube([(-0.04, FY, 0.61), (0, FY - 0.008, 0.615), (0.04, FY, 0.61)], [0.008] * 3, DARK)]
for sx in (1, -1): parts.append(ptube([(0.168 * sx, FY + 0.02, 0.60), (0.24 * sx, FY + 0.10, 0.60)], [0.009, 0.008], DARK, squash=0.4))
build('P_Sunglasses', parts, BODY)
# dive mask + snorkel (scuba)
parts = [ptube(ring_pts(0.0, FY - 0.004, 0.595, 0.200, 0.092, 48, plane='xz', power=3.2), [0.014] * 49, MAIN, squash=0.9),
         ptube(ring_pts(0.0, FY - 0.004, 0.595, 0.188, 0.080, 48, plane='xz', power=3.2), [0.006] * 49, DARK, squash=0.9),
         ptube([(0.0, FY, 0.600), (0.0, FY - 0.01, 0.600)], [0.012, 0.012], MAIN)]
for sx in (1, -1): parts.append(ptube([(0.20 * sx, FY + 0.03, 0.60), (0.25 * sx, FY + 0.10, 0.60)], [0.012, 0.011], DARK, squash=0.4))
parts.append(ptube([(0.215, FY + 0.01, 0.64), (0.245, FY + 0.05, 0.74), (0.255, FY + 0.06, 0.88), (0.225, FY + 0.06, 1.0), (0.205, FY + 0.05, 1.02)], [0.016, 0.014, 0.014, 0.014, 0.014], ACC))
parts.append(ptube([(0.215, FY + 0.01, 0.64), (0.205, FY - 0.01, 0.60)], [0.016, 0.02], DARK))
build('P_DiveMask', parts, BODY)
# boxing gloves on the two side points
parts = []
for sx in (1, -1):
    parts += [ell((0.415 * sx, -0.03, 0.675), (0.085, 0.078, 0.078), MAIN), ell((0.455 * sx, -0.06, 0.700), (0.040, 0.034, 0.045), MAIN),
              ptube([(0.355 * sx, -0.01, 0.64), (0.37 * sx, -0.01, 0.652)], [0.048, 0.048], WHITE)]
bpy.ops.object.select_all(action='DESELECT')
o = finalize('P_Gloves', parts, None)
attach(o, {'ArmR': lambda x: x >= 0, 'ArmL': lambda x: x < 0})
# apply the category colours' defaults and mark the export
result = {"props": [o.name for o in collection().objects if o.name.startswith('P_')]}
