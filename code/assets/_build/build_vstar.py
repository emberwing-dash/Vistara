exec(open(r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\_build\charlib.py").read())
COL = "COL_VStar"
V = Vector
import numpy as np

def hexc(h):
    h = h.lstrip('#'); r, g, b = [int(h[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return tuple(c ** 2.2 for c in (r, g, b))

# ------------------------------------------------------------------ clean scene (keep camera + light)
for o in list(bpy.data.objects):
    if o.type not in ('CAMERA', 'LIGHT'): bpy.data.objects.remove(o, do_unlink=True)
for c in list(bpy.data.collections): bpy.data.collections.remove(c)
for m in list(bpy.data.materials): bpy.data.materials.remove(m)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
_mats.clear()

# ------------------------------------------------------------------ materials (cel / toon, same stack as the pipeline's anime-style notes)
star_m = toon('MAT_Star', hexc('#ffd23a'), shade=0.78, tint=(1.0, 0.72, 0.62))
shine_m = flat('Shine', (1.0, 0.97, 0.80))
eye_m = flat('Eye', hexc('#1d1233')); hi_m = flat('EyeHi', (1, 1, 1))
mouth_m = flat('Mouth', hexc('#7d1f33')); tongue_m = flat('Tongue', hexc('#ff7fa0')); cheek_m = flat('Cheek', hexc('#ff9a86'))

# ------------------------------------------------------------------ body: pillowy rounded star (outline -> inflate), centre at z = 0.55
ZC = 0.55; NPTS = 5; ROUT, RIN = 0.50, 0.31; H = 0.25
def outline_r(th):
    k = ((1 + math.cos(NPTS * (th - math.pi / 2))) / 2.0) ** 0.95
    return RIN + (ROUT - RIN) * k
OUT = 220
poly = [(outline_r(2 * math.pi * i / OUT) * math.cos(2 * math.pi * i / OUT), outline_r(2 * math.pi * i / OUT) * math.sin(2 * math.pi * i / OUT)) for i in range(OUT)]
poly = np.array(poly)
bm = bmesh.new()
vs = [bm.verts.new((x, 0, z)) for (x, z) in poly]
cv = bm.verts.new((0, 0, 0))
for i in range(OUT):
    bm.faces.new((cv, vs[i], vs[(i + 1) % OUT]))
for _ in range(3):
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=1, use_grid_fill=True)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
bm.verts.ensure_lookup_table()
# distance from each vertex to the outline
def dist_to_outline(p):
    a = poly; b = np.roll(poly, -1, axis=0); ab = b - a; ap = p - a
    t = np.clip((ap * ab).sum(1) / (ab * ab).sum(1), 0, 1)
    return np.min(np.linalg.norm(ap - ab * t[:, None], axis=1))
D0 = 0.31
for v in bm.verts:
    d = dist_to_outline(np.array([v.co.x, v.co.z]))
    u = min(d / D0, 1.0)
    v.co.y = -H * math.sqrt(max(0.0, 1 - (1 - u) ** 2))
front = bm.verts[:]
# mirror to the back and weld the rim
ret = bmesh.ops.duplicate(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:])
newv = [e for e in ret['geom'] if isinstance(e, bmesh.types.BMVert)]
for v in newv: v.co.y = -v.co.y
newf = [e for e in ret['geom'] if isinstance(e, bmesh.types.BMFace)]
bmesh.ops.reverse_faces(bm, faces=newf)
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0008)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
body = from_bm('SM_VStar_Body', bm)
body.location = (0, 0, ZC)
finish(body, star_m, subsurf=0, outline=0.006)
for p in body.data.polygons: p.use_smooth = True

# ------------------------------------------------------------------ face parts (flat cel colours), parented to the rig later
def surface_y(x, z):
    d = dist_to_outline(np.array([x, z])); u = min(d / D0, 1.0)
    return -H * math.sqrt(max(0.0, 1 - (1 - u) ** 2))
def face_part(name, kind, x, z, sx, sy, sz, mat, ydelta=0.006, rot=(0, 0, 0)):
    y = surface_y(x, z) - ydelta
    o = ellipsoid(name, V((x, y, ZC + z)), (sx, sy, sz), segs=24, rings=14, rot=rot); o.name = o.data.name = name
    o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth = True
    return o
EX, EZ = 0.128, 0.020
iris_m = flat('Iris', hexc('#6a3fb5')); iris2_m = flat('IrisLt', hexc('#b98cff'))
parts = {}
for s_, sx in (('L', -1), ('R', 1)):
    e = face_part('SM_VStar_Eye_' + s_, 'eye', EX * sx, EZ, 0.064, 0.030, 0.086, eye_m, 0.010)
    ir = face_part('SM_VStar_Iris_' + s_, 'iris', EX * sx, EZ - 0.012, 0.052, 0.030, 0.068, iris_m, 0.022)
    il = face_part('SM_VStar_IrisLt_' + s_, 'iris', EX * sx, EZ - 0.040, 0.040, 0.028, 0.032, iris2_m, 0.034)
    h1 = face_part('SM_VStar_EyeHi_' + s_, 'hi', EX * sx - 0.018 * sx, EZ + 0.034, 0.020, 0.012, 0.024, hi_m, 0.046)
    h2 = face_part('SM_VStar_EyeHi2_' + s_, 'hi', EX * sx + 0.020 * sx, EZ - 0.036, 0.010, 0.010, 0.012, hi_m, 0.042)
    c = face_part('SM_VStar_Cheek_' + s_, 'cheek', 0.222 * sx, -0.058, 0.044, 0.010, 0.026, cheek_m, 0.0)
    parts['Eye' + s_] = [e, ir, il, h1, h2]; parts['Cheek' + s_] = [c]
mouth = face_part('SM_VStar_Mouth', 'mouth', 0.0, -0.092, 0.040, 0.016, 0.034, mouth_m, 0.004)
bmm = bmesh.new(); bmm.from_mesh(mouth.data)
for v in bmm.verts:
    if v.co.z > 0: v.co.z *= 0.12
bmm.to_mesh(mouth.data); bmm.free()
tongue = face_part('SM_VStar_Tongue', 'tongue', 0.0, -0.108, 0.024, 0.012, 0.012, tongue_m, 0.012)
parts['Mouth'] = [mouth, tongue]
shine = face_part('SM_VStar_Shine', 'shine', -0.17, 0.215, 0.056, 0.010, 0.030, shine_m, 0.004, rot=(0, math.radians(-30), 0))
shine2 = face_part('SM_VStar_Shine2', 'shine', 0.20, 0.175, 0.020, 0.008, 0.012, shine_m, 0.004)
parts['Body_extra'] = [shine, shine2]

# ------------------------------------------------------------------ armature ARM_VStar
arm_data = bpy.data.armatures.new('ARM_VStar'); arm = bpy.data.objects.new('ARM_VStar', arm_data); collection().objects.link(arm)
bpy.context.view_layer.objects.active = arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
def bone(name, head, tail, parent=None, roll=0):
    b = eb.new(name); b.head = head; b.tail = tail; b.roll = roll
    if parent: b.parent = eb[parent]
    return b
bone('Root', (0, 0, 0), (0, 0, 0.12))
bone('Body', (0, 0, ZC - 0.05), (0, 0, ZC + 0.10), 'Root')
tips = {'Top': 90, 'ArmR': 18, 'FootR': -54, 'FootL': -126, 'ArmL': 162}
for n, ang in tips.items():
    a = math.radians(ang)
    bone(n, (0.10 * math.cos(a), 0, ZC + 0.10 * math.sin(a)), (0.47 * math.cos(a), 0, ZC + 0.47 * math.sin(a)), 'Body')
bone('EyeL', (-EX, -H, ZC + EZ), (-EX, -H, ZC + EZ + 0.05), 'Body')
bone('EyeR', (EX, -H, ZC + EZ), (EX, -H, ZC + EZ + 0.05), 'Body')
bone('Mouth', (0, -H, ZC - 0.085), (0, -H, ZC - 0.045), 'Body')
bpy.ops.object.mode_set(mode='OBJECT')

# ------------------------------------------------------------------ skinning (weights computed from distance to each bone: no operator context needed)
def seg_t_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / ab.dot(ab))); return t, (p - (a + ab * t)).length
tipvec = {n: (V((0.10 * math.cos(math.radians(a)), 0, 0.10 * math.sin(math.radians(a)))), V((0.47 * math.cos(math.radians(a)), 0, 0.47 * math.sin(math.radians(a))))) for n, a in tips.items()}
def smooth(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t)
for n in ['Body'] + list(tips): body.vertex_groups.new(name=n)
mw = body.matrix_world
for v in body.data.vertices:
    p = V((v.co.x, 0, v.co.z)); w = {}
    for n, (a, b) in tipvec.items():
        t, dseg = seg_t_dist(p, a, b)
        w[n] = smooth(0.10, 1.0, t) * math.exp(-(dseg / 0.24) ** 2)
    tot = sum(w.values())
    if tot > 1.0:
        for n in w: w[n] /= tot
        tot = 1.0
    body.vertex_groups['Body'].add([v.index], 1.0 - tot, 'REPLACE')
    for n, val in w.items():
        if val > 0.002: body.vertex_groups[n].add([v.index], val, 'REPLACE')
mod = body.modifiers.new('Armature', 'ARMATURE'); mod.object = arm
# move the armature modifier first (before the outline hull)
while body.modifiers[0].type != 'ARMATURE':
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.modifier_move_up(modifier=body.modifiers[-1].name) if False else None
    break
bpy.context.view_layer.objects.active = body
for _ in range(3):
    if body.modifiers[0].type != 'ARMATURE':
        bpy.ops.object.modifier_move_to_index(modifier='Armature', index=0)
for grp, objs in parts.items():
    bname = {'EyeL': 'EyeL', 'EyeR': 'EyeR', 'Mouth': 'Mouth', 'CheekL': 'Body', 'CheekR': 'Body', 'Body_extra': 'Body'}[grp]
    for o in objs:
        vg = o.vertex_groups.new(name=bname); vg.add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
        m = o.modifiers.new('Armature', 'ARMATURE'); m.object = arm
        o.parent = arm
for o in [body]:
    o.parent = arm
result = {"objs": len(collection().objects)}
