exec(open(r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\_build\charlib.py").read())
COL = "COL_VStar"
V = Vector

def hexc(h):
    h = h.lstrip('#'); r, g, b = [int(h[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return tuple(c ** 2.2 for c in (r, g, b))

for o in list(bpy.data.objects):
    if o.type not in ('CAMERA', 'LIGHT'): bpy.data.objects.remove(o, do_unlink=True)
for c in list(bpy.data.collections): bpy.data.collections.remove(c)
for m in list(bpy.data.materials): bpy.data.materials.remove(m)
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
_mats.clear()

slime_m = toon('MAT_Star', hexc('#6fa0c8'), shade=0.78, tint=(0.8, 0.85, 1.0))      # "MAT_Star" keeps the Unity jelly-material hook
def fl(n, h): return flat(n, hexc(h))
ring_m, iris_m, hi_m = fl('EyeRing', '#e9e2cf'), fl('Eye', '#1c4a44'), fl('EyeHi', '#ffffff')
mouth_m, tongue_m, cheek_m = fl('Mouth', '#1f4d57'), fl('Tongue', '#ff8fa6'), fl('Cheek', '#f0a8a0')
shine_m, belly_m = fl('Shine', '#e8f3f4'), fl('Belly', '#a9c6e6')

# ---------------------------------------------------------------- body: teardrop slime (tip at z = 1.05, wide soft base)
HT = 1.05; RXB = 0.46; RYB = 0.21
def prof(t):                         # width factor along the height t in [0, 1]
    s = math.sqrt(max(0.0, 1 - (2 * t - 1) ** 2)) if t > 0.5 else math.sqrt(max(0.0, 1 - ((0.5 - t) * 2.0 / 1.0) ** 2))
    teardrop = (1 - 0.72 * t ** 2.1)
    return s * teardrop * (1.0 + (0.10 if t < 0.2 else 0.0))
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=56, v_segments=40, radius=1.0)
for v in bm.verts:
    x, y, z = v.co; t = (z + 1) / 2; ph = math.atan2(y, x); w = prof(t)
    r = math.sqrt(x * x + y * y)
    v.co = V((math.cos(ph) * RXB * w * (1 if r > 1e-6 else 0), math.sin(ph) * RYB * w * (1 if r > 1e-6 else 0), 0.045 + HT * (t ** 0.92)))
bm.normal_update(); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
body = from_bm('SM_VStar_Body', bm)
finish(body, slime_m, subsurf=1, outline=0.006)

def surf_y(x, z):
    t = max(0.0, min(1.0, ((z - 0.045) / HT)) ** (1 / 0.92)); w = prof(t)
    rx, ry = RXB * w, RYB * w
    return -ry * math.sqrt(max(0.0, 1 - (x / max(rx, 1e-4)) ** 2))

def fpart(name, x, z, sx, sy, sz, mat, dy=0.006, rot=(0, 0, 0)):
    o = ellipsoid(name, V((x, surf_y(x, z) - dy, z)), (sx, sy, sz), segs=24, rings=14, rot=rot)
    o.name = o.data.name = name; o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth = True
    return o

EX, EZ = 0.105, 0.58
parts = {}
for s, sx in (('L', -1), ('R', 1)):
    ring = fpart('SM_VStar_EyeRing_' + s, EX * sx, EZ, 0.074, 0.030, 0.088, ring_m, 0.004)
    eye = fpart('SM_VStar_Eye_' + s, EX * sx, EZ, 0.056, 0.032, 0.072, iris_m, 0.010)
    h1 = fpart('SM_VStar_EyeHi_' + s, EX * sx + 0.016 * sx * -1, EZ + 0.030, 0.017, 0.012, 0.020, hi_m, 0.034)
    h2 = fpart('SM_VStar_EyeHi2_' + s, EX * sx + 0.020 * sx, EZ - 0.030, 0.009, 0.008, 0.010, hi_m, 0.030)
    cheek = fpart('SM_VStar_Cheek_' + s, 0.225 * sx, EZ - 0.115, 0.050, 0.012, 0.032, cheek_m, 0.0)
    parts['Eye' + s] = [ring, eye, h1, h2]; parts['Cheek' + s] = [cheek]
mouth = fpart('SM_VStar_Mouth', 0.0, EZ - 0.125, 0.044, 0.018, 0.034, mouth_m, 0.004)
bmm = bmesh.new(); bmm.from_mesh(mouth.data)
for v in bmm.verts:
    if v.co.z > 0: v.co.z *= 0.10
bmm.to_mesh(mouth.data); bmm.free()
tongue = fpart('SM_VStar_Tongue', 0.0, EZ - 0.140, 0.026, 0.012, 0.012, tongue_m, 0.012)
parts['Mouth'] = [mouth, tongue]
shine = fpart('SM_VStar_Shine', 0.0, 0.84, 0.020, 0.008, 0.075, shine_m, 0.004, rot=(0, math.radians(8), 0))
belly = fpart('SM_VStar_Belly', 0.0, 0.10, 0.20, 0.010, 0.050, belly_m, 0.004)
parts['Body_extra'] = [shine, belly]

# little hand stubs on the sides (they hold the boxing gloves and wave)
for s, sx in (('L', -1), ('R', 1)):
    h = ellipsoid('SM_VStar_Hand_' + s, V((0.43 * sx, -0.01, 0.30)), (0.085, 0.075, 0.085), segs=20, rings=12)
    h.name = h.data.name = 'SM_VStar_Hand_' + s; h.data.materials.append(slime_m)
    for p in h.data.polygons: p.use_smooth = True
    finish(h, slime_m, outline=0.005)
    parts['Hand' + s] = [h]

# ---------------------------------------------------------------- armature (same bone names the animations use)
arm_data = bpy.data.armatures.new('ARM_VStar'); arm = bpy.data.objects.new('ARM_VStar', arm_data); collection().objects.link(arm)
bpy.context.view_layer.objects.active = arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); eb = arm_data.edit_bones
def bone(name, head, tail, parent=None):
    b = eb.new(name); b.head = head; b.tail = tail
    if parent: b.parent = eb[parent]
bone('Root', (0, 0, 0), (0, 0, 0.12)); bone('Body', (0, 0, 0.12), (0, 0, 0.60), 'Root'); bone('Top', (0, 0, 0.60), (0, 0, 1.05), 'Body')
bone('ArmR', (0.30, 0, 0.30), (0.46, 0, 0.30), 'Body'); bone('ArmL', (-0.30, 0, 0.30), (-0.46, 0, 0.30), 'Body')
bone('EyeL', (-EX, -0.17, EZ), (-EX, -0.17, EZ + 0.05), 'Body'); bone('EyeR', (EX, -0.17, EZ), (EX, -0.17, EZ + 0.05), 'Body')
bone('Mouth', (0, -0.17, EZ - 0.125), (0, -0.17, EZ - 0.085), 'Body')
bpy.ops.object.mode_set(mode='OBJECT')

def smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)
for n in ('Body', 'Top'): body.vertex_groups.new(name=n)
for v in body.data.vertices:
    wt = smooth(0.55, 1.0, v.co.z / HT)
    body.vertex_groups['Top'].add([v.index], wt, 'REPLACE'); body.vertex_groups['Body'].add([v.index], 1 - wt, 'REPLACE')
mod = body.modifiers.new('Armature', 'ARMATURE'); mod.object = arm
bpy.context.view_layer.objects.active = body
for _ in range(3):
    if body.modifiers[0].type != 'ARMATURE': bpy.ops.object.modifier_move_to_index(modifier='Armature', index=0)
for grp, objs in parts.items():
    bname = {'EyeL': 'EyeL', 'EyeR': 'EyeR', 'Mouth': 'Mouth', 'CheekL': 'Body', 'CheekR': 'Body', 'Body_extra': 'Body', 'HandL': 'ArmL', 'HandR': 'ArmR'}[grp]
    for o in objs:
        vg = o.vertex_groups.new(name=bname); vg.add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
        m = o.modifiers.new('Armature', 'ARMATURE'); m.object = arm; o.parent = arm
body.parent = arm
result = {"objs": len(collection().objects)}
