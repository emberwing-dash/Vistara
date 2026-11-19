exec(open(r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\_build\build_props.py").read())   # props + helpers (pmat, ell, ptube, attach, build)

TEAL, TEAL2, MAG, YEL, PUR, GEM = pmat('OutTeal', '#22c4b6'), pmat('OutTealDk', '#169c93'), pmat('OutMagenta', '#e8408a'), pmat('OutYellow', '#ffd23a'), pmat('OutPurple', '#8a52d8'), pmat('GemPink', '#ff7ad9')
OW = pmat('OutWhite', '#f8f6f4')

for o in list(bpy.data.objects):
    if o.name.startswith('O_'): bpy.data.objects.remove(o, do_unlink=True)

def star_emblem(c, R, mat, rot=(0, 0, 0)):
    bm = bmesh.new(); vs = []
    for i in range(10):
        a = math.pi / 2 + i * math.pi / 5; r = R if i % 2 == 0 else R * 0.45
        vs.append(bm.verts.new((math.cos(a) * r, 0, math.sin(a) * r)))
    cv = bm.verts.new((0, 0, 0))
    for i in range(10): bm.faces.new((cv, vs[i], vs[(i + 1) % 10]))
    ret = bmesh.ops.extrude_face_region(bm, geom=bm.faces[:])
    bmesh.ops.translate(bm, vec=(0, -0.008, 0), verts=[v for v in ret['geom'] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new('t'); bm.to_mesh(me); bm.free()
    o = new_obj('t', me); o.location = V(c); o.rotation_euler = rot; o.data.materials.append(mat)
    return o

def commit(name, parts, groups):
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = name; o.data.name = name
    attach(o, groups); return o

# ---- arms: teal sleeves with a yellow stripe + star emblem, white gloves with teal cuffs (hang from the two side points)
parts = []
for sx in (1, -1):
    parts.append(ptube([(0.405 * sx, -0.005, 0.665), (0.455 * sx, -0.008, 0.56), (0.50 * sx, -0.012, 0.46)], [0.047, 0.052, 0.050], TEAL))
    parts.append(ptube(ring_pts(0.455 * sx, -0.008, 0.555, 0.054, 0.054, 24, plane='xy'), [0.007] * 25, YEL))
    parts.append(ptube([(0.495 * sx, -0.012, 0.475), (0.505 * sx, -0.012, 0.445)], [0.058, 0.056], TEAL2))
    parts.append(ell((0.508 * sx, -0.012, 0.395), (0.064, 0.060, 0.072), OW))
    parts.append(ell((0.548 * sx, -0.030, 0.405), (0.026, 0.024, 0.036), OW, rot=(0, math.radians(-20 * sx), 0)))
    parts.append(star_emblem((0.46 * sx, -0.056, 0.585), 0.030, YEL, rot=(0, math.radians(-8 * sx), 0)))
for _p in parts: bpy.data.objects.remove(_p, do_unlink=True)

# ---- legs: striped leg warmers, magenta boots with yellow toe caps and white soles
parts = []
for sx in (1, -1):
    parts.append(ptube([(0.272 * sx, 0.0, 0.205), (0.285 * sx, 0.0, 0.085)], [0.047, 0.045], OW))
    parts.append(ptube(ring_pts(0.279 * sx, 0.0, 0.155, 0.049, 0.049, 24, plane='xy'), [0.008] * 25, TEAL))
    parts.append(ptube(ring_pts(0.282 * sx, 0.0, 0.118, 0.048, 0.048, 24, plane='xy'), [0.008] * 25, TEAL))
    parts.append(ell((0.292 * sx, -0.028, 0.052), (0.068, 0.100, 0.054), MAG))
    parts.append(ell((0.292 * sx, -0.100, 0.040), (0.052, 0.040, 0.040), YEL))
    parts.append(ell((0.292 * sx, -0.030, 0.013), (0.072, 0.108, 0.013), OW))
    parts.append(star_emblem((0.281 * sx, -0.050, 0.188), 0.028, YEL))
for _p in parts: bpy.data.objects.remove(_p, do_unlink=True)

# ---- chest gem
gem = ell((0, -0.250, 0.855), (0.036, 0.014, 0.052), GEM, segs=4, rings=3)
commit('O_Gem', [gem], BODY)

# ---- scarf bones (chain behind the body) + two streamers skinned to them
bpy.context.view_layer.objects.active = arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); eb = arm.data.edit_bones
chain = [('Scarf1', (0, 0.17, 0.80), (0, 0.19, 0.58), 'Body'), ('Scarf2', (0, 0.19, 0.58), (0, 0.21, 0.40), 'Scarf1'), ('Scarf3', (0, 0.21, 0.40), (0, 0.23, 0.20), 'Scarf2')]
for n, h, t, par in chain:
    if n not in eb:
        b = eb.new(n); b.head = h; b.tail = t; b.parent = eb[par]; b.use_connect = False
bpy.ops.object.mode_set(mode='OBJECT')

def streamer(x0, mat, phase):
    bm = bmesh.new(); N = 12; cols = []
    for k in range(N + 1):
        u = k / N; z = 0.80 - 0.60 * u; y = 0.17 + 0.07 * u + 0.012 * math.sin(u * 7 + phase)
        w = (0.060 - 0.022 * u); xc = x0 + 0.03 * math.sin(u * 5 + phase) * u
        cols.append((bm.verts.new((xc - w, y, z)), bm.verts.new((xc + w, y, z))))
    for a, b in zip(cols[:-1], cols[1:]):
        bm.faces.new((a[0], a[1], b[1], b[0]))
    bm.normal_update()
    me = bpy.data.meshes.new('t'); bm.to_mesh(me); bm.free()
    o = new_obj('t', me); o.data.materials.append(mat)
    s = o.modifiers.new('Thick', 'SOLIDIFY'); s.thickness = 0.006; s.offset = 0
    for p in o.data.polygons: p.use_smooth = True
    return o
sc1, sc2 = streamer(-0.05, MAG, 0.0), streamer(0.05, PUR, 1.4)
for o in (sc1, sc2):
    bpy.context.view_layer.objects.active = o; bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
    bpy.ops.object.modifier_apply(modifier='Thick')
bpy.ops.object.select_all(action='DESELECT'); sc1.select_set(True); sc2.select_set(True); bpy.context.view_layer.objects.active = sc1
bpy.ops.object.join(); sc = bpy.context.view_layer.objects.active; sc.name = sc.data.name = 'O_Scarf'
for g in ('Body', 'Scarf1', 'Scarf2', 'Scarf3'): sc.vertex_groups.new(name=g)
def sm(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)
for v in sc.data.vertices:
    z = v.co.z
    w3 = sm(0.46, 0.30, z); w2 = sm(0.66, 0.50, z) * (1 - w3); w1 = sm(0.84, 0.70, z) * (1 - w2 - w3); w0 = max(0.0, 1 - w1 - w2 - w3)
    for g, w in (('Body', w0), ('Scarf1', w1), ('Scarf2', w2), ('Scarf3', w3)):
        if w > 0.001: sc.vertex_groups[g].add([v.index], w, 'REPLACE')
m = sc.modifiers.new('Armature', 'ARMATURE'); m.object = arm; sc.parent = arm
for c in sc.users_collection: c.objects.unlink(sc)
collection().objects.link(sc)
result = {"objects": [o.name for o in collection().objects if o.name.startswith(('O_', 'P_'))]}
