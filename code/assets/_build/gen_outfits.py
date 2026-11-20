"""Category outfits for the Vistara girl (runs inside Blender with Vistara_Girl.blend open).
Each outfit = a collection 'Outfit_<Cat>' of separate meshes named '<Piece>__<bone>' (custom prop 'bone'), in rest-pose coordinates,
exported to assets/character/outfits/<Cat>/Outfit_<Cat>.fbx. Unity parents every piece to the named bone (SetParent worldPositionStays) while the rig is in rest pose.
"""
import sys, bpy, bmesh, math, os, io, contextlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_common import b, cy, co, tc, sp, wd, rod, make_mesh, mat
from mathutils import Vector

arm = bpy.data.objects['metarig']; sweat = bpy.data.objects['Sweatshirt']
OUT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "character", "outfits"))


def coll_for(cat):
    n = 'Outfit_' + cat; c = bpy.data.collections.get(n)
    if c is None: c = bpy.data.collections.new(n); bpy.context.scene.collection.children.link(c)
    for o in list(c.objects): bpy.data.objects.remove(o, do_unlink=True)
    return c


def piece(cat, name, bone, parts, col):
    ob = bpy.data.objects.new('%s__%s' % (name, bone.replace(' ', '_')), make_mesh('%s_%s' % (cat, name), parts)); col.objects.link(ob); ob['bone'] = bone; return ob


def patch(cat, name, bone, rows, hw_fn, nx, hexc, col, off, thick=0.006, y0=-0.4):
    """A cloth panel: a grid in front of the body projected onto the tee (so it hugs the body) and given a little thickness."""
    bm = bmesh.new(); grid = []
    for z in rows:
        w = hw_fn(z); grid.append([bm.verts.new((-w + 2 * w * i / nx, y0, z)) for i in range(nx + 1)])
    for j in range(len(rows) - 1):
        for i in range(nx): bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); me = bpy.data.meshes.new(cat + '_' + name); bm.to_mesh(me); bm.free(); me.materials.append(mat(hexc))
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new('%s__%s' % (name, bone), me); col.objects.link(ob); ob['bone'] = bone
    m = ob.modifiers.new('Wrap', 'SHRINKWRAP'); m.target = sweat; m.wrap_method = 'PROJECT'; m.use_project_x = False; m.use_project_y = True; m.use_project_z = False
    m.use_positive_direction = True; m.use_negative_direction = False; m.offset = off
    bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier='Wrap')
    s = ob.modifiers.new('Solid', 'SOLIDIFY'); s.thickness = thick; s.offset = 1; bpy.ops.object.modifier_apply(modifier='Solid'); return ob


def export(cat, col):
    d = os.path.join(OUT, cat); os.makedirs(d, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in col.objects: o.select_set(True)
    bpy.context.view_layer.objects.active = list(col.objects)[0]
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        bpy.ops.export_scene.fbx(filepath=os.path.join(d, 'Outfit_%s.fbx' % cat), use_selection=True, object_types={'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', bake_space_transform=False, mesh_smooth_type='FACE', add_leaf_bones=False)


def hand(sd, sx):
    p0 = Vector((0.446 * sx, 0.026, 0.789)); p1 = Vector((0.535 * sx, 0.02, 0.757)); return p0, p1


# ---------------------------------------------------------------- KITCHEN: apron
def Kitchen():
    K = coll_for('Kitchen'); TEAL, TRIM, DARK = '#4fc3b0', '#f4f1ea', '#2f9a8a'
    def hw(z): return 0.085 + (0.985 - z) * 0.12 if z > 0.80 else 0.095 + (0.80 - z) * 0.33
    patch('Kitchen', 'Apron', 'spine.002', [0.985 - k * 0.0215 for k in range(0, 18)], hw, 14, TEAL, K, 0.016)
    patch('Kitchen', 'ApronPocket', 'spine.002', [0.70 - k * 0.012 for k in range(0, 8)], lambda z: 0.062, 6, DARK, K, 0.026)
    piece('Kitchen', 'ApronStrap', 'spine.004', [rod((0.088, -0.115, 0.985), (0.05, 0.06, 1.045), 0.022, TEAL), rod((-0.088, -0.115, 0.985), (-0.05, 0.06, 1.045), 0.022, TEAL), rod((-0.05, 0.06, 1.045), (0.05, 0.06, 1.045), 0.022, TEAL)], K)
    piece('Kitchen', 'ApronTie', 'spine', [rod((0.19, -0.02, 0.70), (0.09, 0.14, 0.70), 0.02, TEAL), rod((-0.19, -0.02, 0.70), (-0.09, 0.14, 0.70), 0.02, TEAL), b((0.03, 0.15, 0.71), (0.07, 0.025, 0.045), TEAL, (0, 0, 15)), b((-0.03, 0.15, 0.71), (0.07, 0.025, 0.045), TEAL, (0, 0, -15)), sp((0, 0.15, 0.705), 0.02, DARK)], K)
    piece('Kitchen', 'ChefHat', 'Head', [tc((0, 0.03, 1.40), 0.20, 0.21, 0.10, TRIM, seg=16), sp((0, 0.03, 1.50), (0.23, 0.23, 0.13), TRIM, sub=3), sp((0.10, 0.03, 1.55), (0.11, 0.11, 0.09), TRIM, sub=2), sp((-0.10, 0.03, 1.55), (0.11, 0.11, 0.09), TRIM, sub=2)], K)
    export('Kitchen', K); return K


# ---------------------------------------------------------------- PARKOUR: cycle gear
def Parkour():
    P = coll_for('Parkour'); OR, DK, WH = '#ff8a1f', '#22232b', '#f4f1ea'
    piece('Parkour', 'Helmet', 'Head', [sp((0, 0.045, 1.36), (0.255, 0.24, 0.215), OR, sub=3, zmin=-0.06), b((0, -0.205, 1.318), (0.19, 0.075, 0.012), DK, (-22, 0, 0)), b((0, 0.045, 1.572), (0.045, 0.24, 0.010), WH, bev=False)] +
          [b((x, 0.04, 1.566), (0.028, 0.15, 0.010), DK, bev=False) for x in (-0.075, 0.075)] + [rod((s * 0.225, 0.03, 1.30), (s * 0.075, -0.07, 1.10), 0.016, DK) for s in (-1, 1)] + [b((0, -0.09, 1.09), (0.065, 0.028, 0.032), DK), tc((0, 0.045, 1.302), 0.248, 0.248, 0.02, DK, seg=20)], P)
    for sd, sx in (('L', 1), ('R', -1)):
        p0, p1 = hand(sd, sx)
        piece('Parkour', 'Glove' + sd, 'hand.' + sd, [rod(tuple(p0 - (p1 - p0) * 0.1), tuple(p1 + (p1 - p0) * 0.15), 0.066, DK, True), sp(tuple(p0 + (p1 - p0) * 0.62), (0.05, 0.05, 0.05), OR, sub=2), rod(tuple(p0 - (p1 - p0) * 0.25), tuple(p0 + (p1 - p0) * 0.1), 0.078, OR, True)], P)
        kx = 0.088 * sx
        piece('Parkour', 'KneePad' + sd, 'shin.' + sd, [sp((kx, -0.078, 0.325), (0.058, 0.03, 0.062), DK, sub=3), sp((kx, -0.092, 0.325), (0.04, 0.012, 0.04), OR, sub=3), cy((kx, -0.011, 0.30), 0.066, 0.04, OR, seg=14), cy((kx, -0.011, 0.36), 0.064, 0.03, DK, seg=14)], P)
        piece('Parkour', 'Reflector' + sd, 'shin.' + sd, [cy((kx, 0.0, 0.18), 0.058, 0.025, '#d9ff3c', seg=14)], P)
    export('Parkour', P); return P


BUILDERS = {'Kitchen': Kitchen, 'Parkour': Parkour}
