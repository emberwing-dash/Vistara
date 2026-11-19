"""Stylised low-poly prop engine for the Vistara category worlds (runs inside Blender).
Z-up metres. Every prop is ONE mesh with a material per colour (material name = M_rrggbb, so Unity can recover the exact colour), origin at the base centre.
Per category: <cat>/<cat>.blend, <cat>/export/<cat>_<Prop>.fbx (one per prop) and <cat>/export/<cat>_World.fbx (props already placed = the environment).
"""
import bpy, bmesh, math, random, os, json
from mathutils import Matrix, Vector, Euler

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "categories"))      # code/assets/categories
_mats = {}


def hexc(h):
    h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def mat(h):
    h = h.lstrip('#').lower()
    if h in _mats and _mats[h].name in bpy.data.materials: return _mats[h]
    m = bpy.data.materials.new('M_' + h); m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF'); c = hexc(h)
    b.inputs['Base Color'].default_value = (c[0] ** 2.2, c[1] ** 2.2, c[2] ** 2.2, 1.0)
    b.inputs['Roughness'].default_value = 0.75
    m.diffuse_color = (c[0] ** 2.2, c[1] ** 2.2, c[2] ** 2.2, 1.0)
    _mats[h] = m; return m


# ------------------------------------------------------------------ part constructors  (pos = centre of the part)
def b(pos, size, col, rot=(0, 0, 0), bev=True): return ('b', pos, size, col, rot, {'bev': bev})
def cy(pos, r, h, col, rot=(0, 0, 0), seg=10): return ('c', pos, (r, r, h), col, rot, {'seg': seg})
def co(pos, r, h, col, rot=(0, 0, 0), seg=10): return ('c', pos, (r, 0.0, h), col, rot, {'seg': seg})
def tc(pos, r1, r2, h, col, rot=(0, 0, 0), seg=10): return ('c', pos, (r1, r2, h), col, rot, {'seg': seg})
def sp(pos, size, col, rot=(0, 0, 0), sub=2, zmin=None): return ('s', pos, size if isinstance(size, tuple) else (size, size, size), col, rot, {'sub': sub, 'zmin': zmin})
def wd(pos, size, col, rot=(0, 0, 0)): return ('w', pos, size, col, rot, {})        # wedge / ramp rising towards +Y


def rod(p0, p1, t, col, bev=False):
    p0, p1 = Vector(p0), Vector(p1); d = p1 - p0; L = d.length
    e = Vector((0, 1, 0)).rotation_difference(d).to_euler()
    return ('b', tuple((p0 + p1) / 2), (t, L, t), col, tuple(math.degrees(a) for a in e), {'bev': bev})


def wheel(x, y, z, r=0.34, tire='#2a2a2e', rim='#d9e0e6'):
    return [cy((x, y, z), r, 0.07, tire, (0, 90, 0), 14), cy((x, y, z), r * 0.72, 0.08, rim, (0, 90, 0), 14), cy((x, y, z), 0.05, 0.14, '#7a828c', (0, 90, 0), 8)]


# ------------------------------------------------------------------ mesh building
def _part_bm(part):
    kind, pos, size, col, rot, ex = part
    t = bmesh.new()
    if kind == 'b':
        v = bmesh.ops.create_cube(t, size=1.0)['verts']; bmesh.ops.scale(t, vec=Vector(size), verts=v)
        bw = min(size) * 0.16
        if ex.get('bev', True) and min(size) > 0.04:
            bmesh.ops.bevel(t, geom=list(t.edges), offset=min(bw, 0.06), segments=2, profile=0.5, affect='EDGES')
    elif kind == 'c':
        r1, r2, h = size
        bmesh.ops.create_cone(t, cap_ends=True, cap_tris=False, segments=ex.get('seg', 10), radius1=r1, radius2=r2, depth=h)
    elif kind == 's':
        v = bmesh.ops.create_icosphere(t, subdivisions=ex.get('sub', 2), radius=1.0)['verts']; bmesh.ops.scale(t, vec=Vector(size), verts=v)
        if ex.get('zmin') is not None: bmesh.ops.delete(t, geom=[vv for vv in t.verts if vv.co.z < ex['zmin']], context='VERTS')
    elif kind == 'w':
        v = bmesh.ops.create_cube(t, size=1.0)['verts']; bmesh.ops.scale(t, vec=Vector(size), verts=v)
        for vv in t.verts:
            if vv.co.y < 0 and vv.co.z > 0: vv.co.z = -size[2] / 2
    rm = Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4()
    bmesh.ops.transform(t, matrix=Matrix.Translation(pos) @ rm, verts=list(t.verts))
    return t


def make_mesh(name, parts):
    bm = bmesh.new(); slots = []
    for p in parts:
        col = p[3].lstrip('#').lower()
        if col not in slots: slots.append(col)
        t = _part_bm(p); mi = slots.index(col)
        for f in t.faces: f.material_index = mi
        tm = bpy.data.meshes.new('tmp'); t.to_mesh(tm); t.free(); bm.from_mesh(tm); bpy.data.meshes.remove(tm)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    for f in bm.faces: f.smooth = False
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for c in slots: me.materials.append(mat(c))
    return me


# ------------------------------------------------------------------ per-category builder
class Builder:
    def __init__(self, cat):
        self.cat = cat; self.dir = os.path.join(ROOT, cat); self.exp = os.path.join(self.dir, 'export'); os.makedirs(self.exp, exist_ok=True)
        for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
        for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)
        for m in list(bpy.data.materials): bpy.data.materials.remove(m)
        for c in list(bpy.data.collections): bpy.data.collections.remove(c)
        _mats.clear()
        sc = bpy.context.scene; sc.unit_settings.system = 'METRIC'; sc.unit_settings.scale_length = 1.0
        self.cprops = bpy.data.collections.new(cat + '_Props'); self.cworld = bpy.data.collections.new(cat + '_World')
        sc.collection.children.link(self.cprops); sc.collection.children.link(self.cworld)
        self.props = {}; self.n = 0; self.rng = random.Random(hash(cat) & 0xffff); self.names = []; self.counts = {}

    def _export(self, objs, path):
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs: o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False, use_mesh_modifiers=True)

    def prop(self, name, parts):
        me = make_mesh(self.cat + '_' + name, parts); ob = bpy.data.objects.new(name, me); self.cprops.objects.link(ob)
        self._export([ob], os.path.join(self.exp, '%s_%s.fbx' % (self.cat, name)))
        ob.location = ((self.n % 12) * 6.0, -60.0 - (self.n // 12) * 9.0, 0); self.n += 1
        self.props[name] = ob; self.names.append(name); return ob

    def place(self, name, pos=(0, 0, 0), rotz=0.0, scale=1.0, tag=None):
        src = self.props[name]; ob = bpy.data.objects.new(tag or name, src.data.copy()); self.cworld.objects.link(ob)       # own mesh copy: shared meshes make the FBX exporter drop material slots
        ob.location = pos; ob.rotation_euler = (0, 0, math.radians(rotz)); ob.scale = scale if isinstance(scale, tuple) else (scale,) * 3
        self.counts[name] = self.counts.get(name, 0) + 1; return ob

    def scatter(self, name, n, rmin, rmax, smin=0.8, smax=1.3, zr=0.0, avoid=None, ang=(0, 360)):
        for _ in range(n):
            for _try in range(12):
                a = math.radians(self.rng.uniform(*ang)); r = self.rng.uniform(rmin, rmax); x, y = math.cos(a) * r, math.sin(a) * r
                if avoid is None or not avoid(x, y): break
            else: continue
            self.place(name, (x, y, zr), self.rng.uniform(0, 360), self.rng.uniform(smin, smax))

    def finish(self):
        objs = [o for o in self.cworld.objects]
        self._export(objs, os.path.join(self.exp, '%s_World.fbx' % self.cat))
        json.dump({'category': self.cat, 'props': self.names, 'world_instances': self.counts}, open(os.path.join(self.dir, 'manifest.json'), 'w'), indent=1)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(self.dir, self.cat + '.blend'))
        return {'cat': self.cat, 'props': len(self.names), 'instances': len(objs)}
