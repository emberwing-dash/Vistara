import bpy, math
from mathutils import Vector, Euler
arm = bpy.data.objects['ARM_VStar']
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')
pb = arm.pose.bones
for b in pb: b.rotation_mode = 'QUATERNION'
arm.animation_data_create()
for _a in list(bpy.data.actions):
    if _a.name.startswith('AN_VStar_'): bpy.data.actions.remove(_a)
FPS = 24
bpy.context.scene.render.fps = FPS

def reset():
    for b in pb:
        b.location = (0, 0, 0); b.rotation_quaternion = (1, 0, 0, 0); b.scale = (1, 1, 1)

def key(frame, spec):
    """spec channels are given in ARMATURE (global) axes: rot = degrees about X/Y/Z, sca = scale along X/Y/Z, loc = offset.
    They are converted into each bone's local axes so a swing about global Y really swings the star point in its plane."""
    reset()
    for name, ch in spec.items():
        if name not in pb: continue
        b = pb[name]; M = b.bone.matrix_local.to_3x3()
        b.rotation_mode = 'QUATERNION'
        if 'loc' in ch: b.location = M.inverted() @ Vector(ch['loc'])
        if 'rot' in ch:
            Rg = Euler(tuple(math.radians(v) for v in ch['rot'])).to_matrix()
            b.rotation_quaternion = (M.inverted() @ Rg @ M).to_quaternion()
        if 'sca' in ch:
            gs = ch['sca']
            b.scale = tuple(sum(abs(M[j][i]) * gs[j] for j in range(3)) / max(1e-6, sum(abs(M[j][i]) for j in range(3))) for i in range(3))
    for name in spec:
        if name not in pb: continue
        b = pb[name]
        b.keyframe_insert('location', frame=frame); b.keyframe_insert('rotation_quaternion', frame=frame); b.keyframe_insert('scale', frame=frame)

def new_action(name, frames, keys):
    act = bpy.data.actions.new('AN_VStar_' + name); act.use_fake_user = True
    arm.animation_data.action = act
    for f0 in (1, frames): key(f0, {b.name: {} for b in pb})      # every bone gets a rest key, so actions never inherit a stale pose
    for f, spec in keys: key(f, spec)
    # scarf streamers flutter in every action
    step = max(2, frames // 8)
    for f in list(range(1, frames, step)) + [frames]:
        ph = 2 * math.pi * (f - 1) / max(1, frames - 1)
        key(f, {'Scarf1': dict(rot=(6 * math.sin(ph), 5 * math.sin(ph + 1), 0)), 'Scarf2': dict(rot=(12 * math.sin(ph - 0.6), 9 * math.sin(ph + 0.4), 0)), 'Scarf3': dict(rot=(18 * math.sin(ph - 1.2), 14 * math.sin(ph), 0))})
    act.frame_range = (1, frames)
    act.use_frame_range = True
    for fc in getattr(act, 'fcurves', []):
        for kp in fc.keyframe_points: kp.interpolation = 'BEZIER'
    return act

sq = lambda s: dict(sca=(1 + (1 - s) * 0.55, 1, s))          # squash: z scale s, x widens
# ---- Idle: breathing squash / stretch, tips sway, slow blink --------------------------------------------------
new_action('Idle', 48, [
    (1,  {'Body': sq(1.0), 'Top': dict(rot=(0, 0, 0)), 'ArmL': dict(rot=(0, 0, 0)), 'ArmR': dict(rot=(0, 0, 0))}),
    (13, {'Body': sq(0.95), 'Top': dict(rot=(0, 4, 0)), 'ArmL': dict(rot=(0, -6, 0)), 'ArmR': dict(rot=(0, 6, 0)), 'EyeL': dict(sca=(1, 1, 1))}),
    (25, {'Body': sq(1.03), 'Top': dict(rot=(0, -3, 0)), 'ArmL': dict(rot=(0, 5, 0)), 'ArmR': dict(rot=(0, -5, 0))}),
    (36, {'Body': sq(0.97), 'Top': dict(rot=(0, 3, 0)), 'EyeL': dict(sca=(1, 1, 1)), 'EyeR': dict(sca=(1, 1, 1))}),
    (39, {'Body': sq(0.97), 'EyeL': dict(sca=(1, 1, 0.08)), 'EyeR': dict(sca=(1, 1, 0.08))}),
    (42, {'Body': sq(0.98), 'EyeL': dict(sca=(1, 1, 1)), 'EyeR': dict(sca=(1, 1, 1))}),
    (48, {'Body': sq(1.0), 'Top': dict(rot=(0, 0, 0))}),
])
# ---- Blink -----------------------------------------------------------------------------------------------------------
new_action('Blink', 10, [
    (1, {'EyeL': dict(sca=(1, 1, 1)), 'EyeR': dict(sca=(1, 1, 1))}),
    (4, {'EyeL': dict(sca=(1, 1, 0.06)), 'EyeR': dict(sca=(1, 1, 0.06))}),
    (10, {'EyeL': dict(sca=(1, 1, 1)), 'EyeR': dict(sca=(1, 1, 1))}),
])
# ---- Wave: right point swings, body leans ----------------------------------------------------------------------------
new_action('Wave', 48, [
    (1,  {'Body': dict(rot=(0, 0, 0)), 'ArmR': dict(rot=(0, 0, 0))}),
    (8,  {'Body': dict(rot=(0, -5, 0)), 'ArmR': dict(rot=(0, 55, 0)), 'Mouth': dict(sca=(1, 1, 0.9))}),
    (16, {'Body': dict(rot=(0, -5, 0)), 'ArmR': dict(rot=(0, 20, 0))}),
    (24, {'Body': dict(rot=(0, -5, 0)), 'ArmR': dict(rot=(0, 55, 0))}),
    (32, {'Body': dict(rot=(0, -5, 0)), 'ArmR': dict(rot=(0, 20, 0))}),
    (40, {'Body': dict(rot=(0, -5, 0)), 'ArmR': dict(rot=(0, 55, 0))}),
    (48, {'Body': dict(rot=(0, 0, 0)), 'ArmR': dict(rot=(0, 0, 0))}),
])
# ---- Jump: anticipation squash -> stretch up -> land squash -> settle -----------------------------------------------------
new_action('Jump', 40, [
    (1,  {'Root': dict(loc=(0, 0, 0)), 'Body': sq(1.0)}),
    (6,  {'Root': dict(loc=(0, 0, 0)), 'Body': sq(0.72), 'ArmL': dict(rot=(0, 12, 0)), 'ArmR': dict(rot=(0, -12, 0))}),
    (11, {'Root': dict(loc=(0, 0, 0.18)), 'Body': dict(sca=(0.84, 1, 1.22)), 'ArmL': dict(rot=(0, -25, 0)), 'ArmR': dict(rot=(0, 25, 0)), 'Top': dict(rot=(0, 0, 0))}),
    (17, {'Root': dict(loc=(0, 0, 0.42)), 'Body': dict(sca=(0.95, 1, 1.06)), 'ArmL': dict(rot=(0, -35, 0)), 'ArmR': dict(rot=(0, 35, 0))}),
    (24, {'Root': dict(loc=(0, 0, 0.10)), 'Body': dict(sca=(0.90, 1, 1.12)), 'ArmL': dict(rot=(0, -10, 0)), 'ArmR': dict(rot=(0, 10, 0))}),
    (28, {'Root': dict(loc=(0, 0, 0)), 'Body': sq(0.68), 'ArmL': dict(rot=(0, 18, 0)), 'ArmR': dict(rot=(0, -18, 0))}),
    (34, {'Root': dict(loc=(0, 0, 0)), 'Body': sq(1.06)}),
    (40, {'Root': dict(loc=(0, 0, 0)), 'Body': sq(1.0)}),
])
# ---- Talk: mouth opens and closes, body bobs --------------------------------------------------------------------------------
new_action('Talk', 24, [
    (1,  {'Mouth': dict(sca=(1, 1, 0.25)), 'Body': sq(1.0)}),
    (4,  {'Mouth': dict(sca=(1.05, 1, 1.0)), 'Body': sq(0.98)}),
    (8,  {'Mouth': dict(sca=(0.9, 1, 0.45)), 'Body': sq(1.01)}),
    (12, {'Mouth': dict(sca=(1.0, 1, 0.9)), 'Body': sq(0.98)}),
    (16, {'Mouth': dict(sca=(0.95, 1, 0.3)), 'Body': sq(1.01)}),
    (20, {'Mouth': dict(sca=(1.05, 1, 0.8)), 'Body': sq(0.98)}),
    (24, {'Mouth': dict(sca=(1, 1, 0.25)), 'Body': sq(1.0)}),
])
# ---- Happy: two little hops with all points up ------------------------------------------------------------------------------------
new_action('Happy', 48, [
    (1,  {'Root': dict(loc=(0, 0, 0)), 'Body': sq(1.0)}),
    (5,  {'Root': dict(loc=(0, 0, 0)), 'Body': sq(0.82), 'ArmL': dict(rot=(0, 10, 0)), 'ArmR': dict(rot=(0, -10, 0))}),
    (10, {'Root': dict(loc=(0, 0, 0.20)), 'Body': dict(sca=(0.92, 1, 1.12)), 'ArmL': dict(rot=(0, -40, 0)), 'ArmR': dict(rot=(0, 40, 0)), 'Top': dict(rot=(0, 8, 0)), 'Mouth': dict(sca=(1, 1, 1.0))}),
    (15, {'Root': dict(loc=(0, 0, 0)), 'Body': sq(0.82), 'ArmL': dict(rot=(0, 10, 0)), 'ArmR': dict(rot=(0, -10, 0))}),
    (20, {'Root': dict(loc=(0, 0, 0)), 'Body': sq(1.0)}),
    (25, {'Root': dict(loc=(0, 0, 0)), 'Body': sq(0.82), 'ArmL': dict(rot=(0, 10, 0)), 'ArmR': dict(rot=(0, -10, 0))}),
    (30, {'Root': dict(loc=(0, 0, 0.24)), 'Body': dict(sca=(0.92, 1, 1.12)), 'ArmL': dict(rot=(0, -45, 0)), 'ArmR': dict(rot=(0, 45, 0)), 'Top': dict(rot=(0, -8, 0)), 'Mouth': dict(sca=(1, 1, 1.0))}),
    (36, {'Root': dict(loc=(0, 0, 0)), 'Body': sq(0.82)}),
    (42, {'Root': dict(loc=(0, 0, 0)), 'Body': sq(1.04)}),
    (48, {'Root': dict(loc=(0, 0, 0)), 'Body': sq(1.0)}),
])

# ============ activity animations (used to act out what happens in the highlighted video parts) ============
def cyc(name, frames, fn, step=3):
    """fn(phase 0..2pi) -> spec dict; keys every `step` frames over one loop."""
    ks = []
    for f in list(range(1, frames, step)) + [frames]:
        ph = 2 * math.pi * (f - 1) / max(1, frames - 1)
        ks.append((f, fn(ph)))
    return new_action(name, frames, ks)
S = math.sin; Cc = math.cos
cyc('Inspect', 48, lambda p: {'Body': dict(rot=(8 + 3 * S(p), 0, 0)), 'ArmR': dict(rot=(0, 48 + 6 * S(p * 2), -12 * Cc(p))), 'Top': dict(rot=(0, 5 * S(p), 0))})
cyc('Punch', 24, lambda p: {'ArmR': dict(rot=(0, 5, -70 * max(0.0, S(p)))), 'ArmL': dict(rot=(0, 5, -70 * max(0.0, -S(p)))), 'Body': dict(rot=(0, 0, 8 * S(p)), sca=(1, 1, 1 - 0.04 * abs(S(p))))}, 2)
cyc('Stir', 24, lambda p: {'ArmR': dict(rot=(0, 28 + 14 * S(p), -22 * Cc(p))), 'Body': dict(rot=(0, 0, 3 * S(p)))})
cyc('Chop', 16, lambda p: {'ArmR': dict(rot=(0, 50 * max(0.0, S(p)) - 8, 0)), 'Body': dict(sca=(1, 1, 1 - 0.05 * max(0.0, -S(p))))}, 2)
cyc('Pour', 36, lambda p: {'ArmR': dict(rot=(0, 40, -10)), 'Body': dict(rot=(0, -18 * S(p / 2) ** 2, 0)), 'Top': dict(rot=(0, -10 * S(p / 2) ** 2, 0))})
cyc('Wash', 24, lambda p: {'ArmR': dict(rot=(0, 20 + 14 * S(p * 2), 0)), 'ArmL': dict(rot=(0, 20 - 14 * S(p * 2), 0)), 'Body': dict(sca=(1 + 0.02 * S(p * 2), 1, 1 - 0.03 * S(p * 2)))})
cyc('Reach', 36, lambda p: {'ArmR': dict(rot=(0, 28 * S(p / 2), -60 * S(p / 2))), 'Body': dict(rot=(14 * S(p / 2), 0, 0))})
cyc('Open', 36, lambda p: {'ArmR': dict(rot=(0, 20, -60 * (1 - S(p / 2) ** 2) + 10)), 'Body': dict(rot=(0, 0, -6 * S(p / 2)))})
cyc('Ride', 24, lambda p: {'Body': dict(rot=(0, 10 * S(p), 0), sca=(1, 1, 1 - 0.04 * abs(S(p * 2)))), 'FootL': dict(rot=(0, 25 * S(p), 0)), 'FootR': dict(rot=(0, -25 * S(p), 0)), 'ArmL': dict(rot=(0, 10, -30)), 'ArmR': dict(rot=(0, 10, -30))})
cyc('Climb', 32, lambda p: {'ArmR': dict(rot=(0, 30 + 40 * S(p), 0)), 'ArmL': dict(rot=(0, 30 - 40 * S(p), 0)), 'FootL': dict(rot=(0, 18 * S(p), 0)), 'FootR': dict(rot=(0, -18 * S(p), 0)), 'Body': dict(rot=(0, 5 * S(p), 0))})
cyc('Fly', 48, lambda p: {'Root': dict(loc=(0, 0, 0.08 * S(p))), 'Body': dict(rot=(-14, 6 * S(p), 0)), 'ArmL': dict(rot=(0, -35 + 8 * S(p * 2), 0)), 'ArmR': dict(rot=(0, 35 + 8 * S(p * 2), 0)), 'Top': dict(rot=(0, 8 * S(p), 0))})
cyc('Dive', 48, lambda p: {'Body': dict(rot=(-22, 0, 4 * S(p))), 'ArmL': dict(rot=(0, 30 * S(p), 0)), 'ArmR': dict(rot=(0, -30 * S(p), 0)), 'FootL': dict(rot=(0, 20 * S(p * 2), 0)), 'FootR': dict(rot=(0, -20 * S(p * 2), 0)), 'Root': dict(loc=(0, 0, 0.04 * S(p)))})
reset(); bpy.ops.object.mode_set(mode='OBJECT')
arm.animation_data.action = bpy.data.actions['AN_VStar_Idle']
bpy.context.scene.frame_start = 1; bpy.context.scene.frame_end = 48
result = {"actions": [a.name for a in bpy.data.actions]}
