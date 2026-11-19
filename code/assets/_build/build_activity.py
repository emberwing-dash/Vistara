exec(open(r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\_build\build_props.py").read())      # helpers + category props (re-built so everything is consistent)
V = Vector
GOLD = pmat('PropGold', '#f0b23a'); GLASS = pmat('PropGlass', '#bfe8ff'); BROWN = pmat('PropBrown', '#8a5a34'); WOOD = pmat('PropWood', '#c58f5a')

# ---------------------------------------------------------------- magnifying glass held in the right hand (skinned to ArmR)
rc = V((0.395, -0.235, 0.80))
parts = [ptube(ring_pts(rc.x, rc.y, rc.z, 0.082, 0.082, 36, plane='xz'), [0.012] * 37, GOLD, squash=0.9),
         ell((rc.x, rc.y + 0.004, rc.z), (0.074, 0.004, 0.074), GLASS), ell((rc.x - 0.025, rc.y - 0.006, rc.z + 0.03), (0.018, 0.003, 0.01), ACC, rot=(0, math.radians(-35), 0)),
         ptube([(rc.x + 0.055, rc.y, rc.z - 0.058), (0.46, -0.04, 0.70)], [0.014, 0.018], BROWN)]
bpy.ops.object.select_all(action='DESELECT')
mag = finalize('P_Magnifier', parts, None); attach(mag, {'ArmR': lambda x: True})

# ---------------------------------------------------------------- activity props (static, exported separately; origin on the floor)
bpy.ops.object.select_all(action='DESELECT')
actcol = bpy.data.collections.get('COL_Activities') or bpy.data.collections.new('COL_Activities')
if actcol.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(actcol)
for o in list(bpy.data.objects):
    if o.name.startswith('A_'): bpy.data.objects.remove(o, do_unlink=True)
C = {k: pmat('Act' + k, v) for k, v in dict(Steel='#b8bcc6', Dark='#3a3a44', Red='#e04848', Blue='#3b78e0', Green='#3dbb6a', Yellow='#ffd23a', Orange='#ff8a2a', White='#f4f1ec', Brown='#8a5a34', Teal='#26b8b0', Pink='#ff7aa8', Sand='#d9b27c', Gray='#8a8a94').items()}
def box(c, s, m, rot=(0, 0, 0)):
    o = ell(c, (1, 1, 1), m, segs=4, rings=2, rot=rot)     # replaced below by a true box
    bpy.data.objects.remove(o, do_unlink=True)
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = V((v.co.x * s[0], v.co.y * s[1], v.co.z * s[2]))
    me = bpy.data.meshes.new('t'); bm.to_mesh(me); bm.free()
    o = new_obj('t', me); o.location = V(c); o.rotation_euler = rot; o.data.materials.append(m); return o
def cyl(c, r, h, m, axis='z', segs=24):
    if axis == 'z': p0, p1 = (c[0], c[1], c[2] - h / 2), (c[0], c[1], c[2] + h / 2)
    elif axis == 'x': p0, p1 = (c[0] - h / 2, c[1], c[2]), (c[0] + h / 2, c[1], c[2])
    else: p0, p1 = (c[0], c[1] - h / 2, c[2]), (c[0], c[1] + h / 2, c[2])
    return ptube([p0, p1], [r, r], m, segs=segs)
def act(name, parts, children=None):
    bpy.ops.object.select_all(action='DESELECT')
    o = finalize(name, parts, None)
    for c in o.users_collection: c.objects.unlink(o)
    actcol.objects.link(o); o.parent = None
    return o
def wheel(name, center, r, m=None):
    parts = [ptube(ring_pts(0, 0, 0, r, r, 28, plane='xz'), [r * 0.10] * 29, C['Dark'], segs=8), cyl((0, 0, 0), r * 0.12, r * 0.25, C['Steel'], 'x', 12)]
    for k in range(3): parts.append(box((0, 0, 0), (0.006, r * 1.9, 0.006), C['Steel'], rot=(math.radians(60 * k), 0, 0)))
    o = act(name, parts); o.rotation_euler = (0, 0, math.radians(90))
    bpy.context.view_layer.objects.active = o; o.select_set(True); bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    o.location = V(center); return o

# kitchen things (nouns)
act('A_Pan', [cyl((0, 0, 0.03), 0.17, 0.05, C['Dark']), ptube([(0.17, 0, 0.05), (0.40, 0, 0.07)], [0.016, 0.016], C['Brown'])])
act('A_Pot', [cyl((0, 0, 0.09), 0.14, 0.18, C['Steel']), cyl((0, 0, 0.19), 0.15, 0.025, C['Gray']), ell((0, 0, 0.215), (0.03, 0.03, 0.02), C['Dark']), ptube([(-0.14, 0, 0.12), (-0.21, 0, 0.12)], [0.012, 0.012], C['Dark']), ptube([(0.14, 0, 0.12), (0.21, 0, 0.12)], [0.012, 0.012], C['Dark'])])
act('A_Plate', [cyl((0, 0, 0.012), 0.19, 0.024, C['White']), cyl((0, 0, 0.026), 0.12, 0.012, C['Sand'])])
act('A_Bowl', [ell((0, 0, 0.10), (0.15, 0.15, 0.10), C['Teal'], cut_below=0.10)])
act('A_Cup', [cyl((0, 0, 0.055), 0.06, 0.11, C['Pink']), ptube(ring_pts(0.07, 0, 0.055, 0.035, 0.04, 16, plane='xz'), [0.009] * 17, C['Pink'], segs=8)])
act('A_Bottle', [cyl((0, 0, 0.11), 0.055, 0.22, C['Green']), cyl((0, 0, 0.255), 0.025, 0.07, C['Green']), cyl((0, 0, 0.30), 0.03, 0.03, C['White'])])
act('A_Knife', [box((0, 0, 0.012), (0.24, 0.035, 0.008), C['Steel']), box((-0.19, 0, 0.012), (0.12, 0.03, 0.022), C['Brown'])])
act('A_Spoon', [ptube([(-0.2, 0, 0.02), (0.08, 0, 0.02)], [0.011, 0.011], C['Steel'], segs=8), ell((0.13, 0, 0.02), (0.06, 0.04, 0.012), C['Steel'])])
act('A_Board', [box((0, 0, 0.02), (0.45, 0.28, 0.04), C['Sand']), box((0.1, 0.05, 0.045), (0.14, 0.1, 0.012), C['Green'])])
act('A_Box', [box((0, 0, 0.09), (0.18, 0.18, 0.18), C['Orange']), box((0, 0, 0.18), (0.20, 0.20, 0.02), C['Brown'])])
# sports / places
fr = [ptube([(0, 0.25, 0.28), (0, 0, 0.40), (0, -0.25, 0.28)], [0.012] * 3, C['Red']), ptube([(0, 0, 0.40), (0, 0.10, 0.16)], [0.012] * 2, C['Red']), ptube([(0, 0.10, 0.16), (0, 0.25, 0.28)], [0.012] * 2, C['Red']),
      ptube([(0, 0.10, 0.16), (0, -0.04, 0.20), (0, -0.25, 0.28)], [0.012] * 3, C['Red']), ptube([(0, 0.10, 0.16), (0, 0.10, 0.50)], [0.011] * 2, C['Steel']), box((0, 0.10, 0.52), (0.10, 0.12, 0.02), C['Dark']),
      ptube([(-0.2, -0.25, 0.60), (0.2, -0.25, 0.60)], [0.010] * 2, C['Dark']), ptube([(0, -0.25, 0.28), (0, -0.25, 0.60)], [0.010] * 2, C['Steel'])]
bike = act('A_Bicycle', fr)
for n, y in (('A_Bicycle_WheelF', -0.25), ('A_Bicycle_WheelB', 0.25)):
    w = wheel(n, (0, y, 0.28), 0.28); w.parent = bike; w.matrix_parent_inverse = bike.matrix_world.inverted(); w.location = V((0, y, 0.28))
mb = act('A_Motorbike', [box((0, 0, 0.36), (0.14, 0.5, 0.14), C['Blue']), box((0, 0.06, 0.48), (0.12, 0.22, 0.05), C['Dark']), ell((0, -0.14, 0.42), (0.12, 0.16, 0.10), C['Blue']), ptube([(0, -0.24, 0.30), (0, -0.28, 0.62)], [0.014] * 2, C['Steel']), ptube([(-0.2, -0.28, 0.64), (0.2, -0.28, 0.64)], [0.012] * 2, C['Dark']), ptube([(0, 0.2, 0.34), (0, 0.46, 0.22)], [0.020] * 2, C['Steel'])])
for n, y in (('A_Motorbike_WheelF', -0.32), ('A_Motorbike_WheelB', 0.30)):
    w = wheel(n, (0, y, 0.24), 0.24); w.parent = mb; w.matrix_parent_inverse = mb.matrix_world.inverted(); w.location = V((0, y, 0.24))
sk = act('A_Skateboard', [box((0, 0, 0.09), (0.20, 0.62, 0.025), C['Teal']), ell((0, -0.31, 0.105), (0.10, 0.04, 0.02), C['Teal']), ell((0, 0.31, 0.105), (0.10, 0.04, 0.02), C['Teal']), box((0, 0, 0.1), (0.14, 0.20, 0.004), C['Yellow'])] +
         [cyl((x, y, 0.045), 0.04, 0.035, C['White'], 'x', 14) for x in (-0.11, 0.11) for y in (-0.22, 0.22)])
act('A_Longboard', [box((0, 0, 0.09), (0.24, 0.95, 0.03), C['Sand']), ell((0, -0.47, 0.105), (0.12, 0.05, 0.02), C['Sand']), ell((0, 0.47, 0.105), (0.12, 0.05, 0.02), C['Sand']), box((0, 0, 0.108), (0.18, 0.3, 0.004), C['Orange'])] +
    [cyl((x, y, 0.05), 0.05, 0.04, C['Red'], 'x', 14) for x in (-0.13, 0.13) for y in (-0.3, 0.3)])
act('A_Surfboard', [ell((0, 0, 0.45), (0.13, 0.55, 0.025), C['Yellow'], rot=(math.radians(-8), 0, 0)), ell((0, 0.4, 0.37), (0.010, 0.06, 0.07), C['Dark']), box((0, -0.05, 0.47), (0.02, 0.8, 0.006), C['Blue'], rot=(math.radians(-8), 0, 0))])
act('A_Skis', [box((-0.10, 0, 0.012), (0.07, 0.9, 0.02), C['Red']), box((0.10, 0, 0.012), (0.07, 0.9, 0.02), C['Red']), ptube([(-0.26, -0.1, 0.0), (-0.26, -0.1, 0.62)], [0.008] * 2, C['Dark']), ptube([(0.26, -0.1, 0.0), (0.26, -0.1, 0.62)], [0.008] * 2, C['Dark']), ell((-0.26, -0.1, 0.01), (0.04, 0.04, 0.012), C['Dark']), ell((0.26, -0.1, 0.01), (0.04, 0.04, 0.012), C['Dark'])])
act('A_PunchBag', [cyl((0, 0, 0.55), 0.13, 0.5, C['Red']), ptube([(0, 0, 0.8), (0, 0, 1.15)], [0.008] * 2, C['Steel']), box((0, 0, 1.17), (0.15, 0.15, 0.03), C['Dark']), ell((0, 0, 0.30), (0.13, 0.13, 0.06), C['Red'])])
act('A_Mallet', [ptube([(0, 0, 0.0), (0, 0, 0.7)], [0.016, 0.016], C['Brown']), cyl((0, 0, 0.72), 0.045, 0.2, C['Blue'], 'x', 14), ell((0.35, 0.0, 0.05), (0.05, 0.05, 0.05), C['White'])])
act('A_HobbyHorse', [ptube([(0, 0, 0), (0, 0, 0.6)], [0.014] * 2, C['Brown']), ell((0, -0.05, 0.72), (0.07, 0.15, 0.10), C['Brown'], rot=(math.radians(25), 0, 0)), ell((0, -0.16, 0.66), (0.05, 0.07, 0.06), C['Sand']), ell((0.03, 0.0, 0.85), (0.018, 0.02, 0.05), C['Brown']), ell((-0.03, 0.0, 0.85), (0.018, 0.02, 0.05), C['Brown']), ptube([(0, 0.03, 0.82), (0, 0.10, 0.60)], [0.02, 0.012], C['Dark'])])
act('A_Hurdle', [ptube([(-0.35, 0, 0), (-0.35, 0, 0.42)], [0.02] * 2, C['White']), ptube([(0.35, 0, 0), (0.35, 0, 0.42)], [0.02] * 2, C['White']), box((0, 0, 0.40), (0.76, 0.04, 0.06), C['Red']), box((-0.35, 0, 0.01), (0.08, 0.30, 0.02), C['Gray']), box((0.35, 0, 0.01), (0.08, 0.30, 0.02), C['Gray'])])
act('A_ParkourBox', [box((0, 0, 0.18), (0.55, 0.40, 0.36), C['Gray']), box((0, 0, 0.375), (0.58, 0.43, 0.03), C['Orange']), ptube([(-0.29, -0.25, 0.0), (-0.29, -0.25, 0.55), (0.29, -0.25, 0.55), (0.29, -0.25, 0.0)], [0.014] * 4, C['Steel'])])
act('A_PaperPlane', [ell((0, 0, 0.0), (0.3, 0.012, 0.01), C['White'], segs=4, rings=2), box((0, -0.1, 0.0), (0.02, 0.5, 0.02), C['Blue']), box((0.14, 0.04, 0.0), (0.22, 0.34, 0.006), C['White'], rot=(0, 0, math.radians(-12))), box((-0.14, 0.04, 0.0), (0.22, 0.34, 0.006), C['White'], rot=(0, 0, math.radians(12))), box((0, 0.2, 0.05), (0.01, 0.10, 0.10), C['Red'])])
holds = [box((x, -0.025, z), (0.07, 0.05, 0.05), C[col], rot=(0, math.radians(10 * i), math.radians(20 * i))) for i, (x, z, col) in enumerate(((-0.25, 0.2, 'Red'), (0.1, 0.35, 'Yellow'), (-0.1, 0.55, 'Blue'), (0.28, 0.7, 'Green'), (-0.28, 0.85, 'Orange'), (0.05, 1.0, 'Pink')))]
act('A_ClimbWall', [box((0, 0, 0.55), (0.9, 0.05, 1.1), C['Gray'])] + holds)
act('A_Coral', [ptube([(x, 0, 0), (x * 1.3, 0, 0.35 + 0.1 * abs(x) * 5), (x * 1.6, 0, 0.55 + 0.1 * abs(x) * 5)], [0.035, 0.03, 0.012], C[c], segs=8) for x, c in ((-0.15, 'Pink'), (0.0, 'Orange'), (0.15, 'Red'))] + [ell((0.3, -0.05, 0.6), (0.12, 0.03, 0.06), C['Yellow']), ell((0.42, -0.05, 0.6), (0.03, 0.02, 0.05), C['Yellow'], rot=(0, 0, math.radians(30)))])
# make the category props + magnifier invisible in the working scene (Unity toggles them)
for o in bpy.data.objects:
    if o.name.startswith('P_'): o.hide_viewport = True; o.hide_render = True
result = {"activities": sorted(o.name for o in actcol.objects)}
