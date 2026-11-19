import bpy, bmesh, math
from mathutils import Vector, Matrix, Quaternion, Euler

COL = "Mascot"


def collection():
    c = bpy.data.collections.get(COL)
    if c is None:
        c = bpy.data.collections.new(COL)
        bpy.context.scene.collection.children.link(c)
    return c


def clear_all():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for d in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.node_groups):
        for b in list(d):
            if b.users == 0:
                d.remove(b)


# ---------------------------------------------------------------- toon materials
_mats = {}


def toon(name, rgb, shade=0.70, tint=(0.80, 0.76, 1.0), rim=0.0):
    """Two-tone cel material: Diffuse -> Shader-to-RGB -> hard ramp, tinted shadows, emission output."""
    if name in _mats:
        return _mats[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emi = nt.nodes.new("ShaderNodeEmission")
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
    s2r = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    mixc = nt.nodes.new("ShaderNodeMix"); mixc.data_type = 'RGBA'; mixc.blend_type = 'MULTIPLY'
    mixc.inputs[0].default_value = 1.0
    base = nt.nodes.new("ShaderNodeRGB"); base.outputs[0].default_value = (*rgb, 1)
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    diff.inputs["Color"].default_value = (1, 1, 1, 1)
    ramp.color_ramp.interpolation = 'CONSTANT'
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (*[shade * t for t in tint], 1)
    ramp.color_ramp.elements[1].position = 0.34
    ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
    nt.links.new(diff.outputs["BSDF"], s2r.inputs["Shader"])
    sepn = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(s2r.outputs["Color"], sepn.inputs["Color"])
    nt.links.new(sepn.outputs["Red"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], mixc.inputs[6])
    nt.links.new(base.outputs[0], mixc.inputs[7])
    nt.links.new(mixc.outputs[2], emi.inputs["Color"])
    nt.links.new(emi.outputs["Emission"], out.inputs["Surface"])
    _mats[name] = m
    return m


def flat(name, rgb):
    """Unlit flat colour (eyes, lines)."""
    key = "flat_" + name
    if key in _mats:
        return _mats[key]
    m = bpy.data.materials.new(key)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emi = nt.nodes.new("ShaderNodeEmission")
    emi.inputs["Color"].default_value = (*rgb, 1)
    nt.links.new(emi.outputs["Emission"], out.inputs["Surface"])
    _mats[key] = m
    return m


def outline_mat(rgb=(0.10, 0.05, 0.05)):
    key = "outline"
    if key in _mats:
        return _mats[key]
    m = flat("outline_" + str(rgb), rgb)
    m.use_backface_culling = True
    _mats[key] = m
    return m


# ---------------------------------------------------------------- object helpers
def finish(obj, mat, smooth=True, subsurf=0, outline=0.0, solidify=0.0, mats_extra=None):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    if smooth:
        for p in obj.data.polygons:
            p.use_smooth = True
    if solidify > 0:
        s = obj.modifiers.new("Thick", 'SOLIDIFY'); s.thickness = solidify; s.offset = -1; s.use_rim = True
    if subsurf:
        s = obj.modifiers.new("Subsurf", 'SUBSURF'); s.levels = subsurf; s.render_levels = subsurf
    if outline > 0:
        obj.data.materials.append(outline_mat())
        s = obj.modifiers.new("Outline", 'SOLIDIFY'); s.thickness = outline; s.offset = 1; s.use_flip_normals = True
        s.use_rim = False; s.material_offset = 1
    return obj


def new_obj(name, me):
    o = bpy.data.objects.new(name, me)
    collection().objects.link(o)
    return o


def from_bm(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    return new_obj(name, me)


def ellipsoid(name, center, scale, segs=24, rings=14, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
    o = from_bm(name, bm)
    o.location = center
    o.rotation_euler = Euler(rot)
    return o


def loft(name, rings, segs=24, close_top=False, close_bottom=False):
    """rings: list of (center Vector, rx, ry, rotation_z) in order; builds a quad skin. ring points start at -Y (front)."""
    bm = bmesh.new()
    layers = []
    for (c, rx, ry, rz) in rings:
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs - math.pi / 2
            x = math.cos(a) * rx; y = math.sin(a) * ry
            cz, sz = math.cos(rz), math.sin(rz)
            ring.append(bm.verts.new(Vector((c.x + x * cz - y * sz, c.y + x * sz + y * cz, c.z))))
        layers.append(ring)
    for a, b in zip(layers[:-1], layers[1:]):
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if close_top:
        bm.faces.new(layers[-1][::-1])
    if close_bottom:
        bm.faces.new(layers[0])
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return from_bm(name, bm)


def tube(name, p0, p1, profile, segs=16, squash=1.0, cap=True):
    """Round tube from p0 to p1; profile = [(t, radius), ...]."""
    p0 = Vector(p0); p1 = Vector(p1)
    d = (p1 - p0); L = d.length; d.normalize()
    q = d.to_track_quat('Z', 'Y')
    bm = bmesh.new(); layers = []
    for (t, r) in profile:
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            local = Vector((math.cos(a) * r, math.sin(a) * r * squash, 0))
            ring.append(bm.verts.new(p0 + d * (t * L) + q @ local))
        layers.append(ring)
    for a, b in zip(layers[:-1], layers[1:]):
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((a[i], a[j], b[j], b[i]))
    if cap:
        bm.faces.new(layers[-1][::-1]); bm.faces.new(layers[0])
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return from_bm(name, bm)


def ribbon(name, pts, widths, lateral, thick=0.0, subdiv_cols=3):
    """Hair lock: a tapered strip along pts (list of Vector) with half-widths and a lateral direction (Vector or list per point)."""
    bm = bmesh.new(); cols = []
    for k, (p, w) in enumerate(zip(pts, widths)):
        lat = lateral[k] if isinstance(lateral, list) else lateral
        lat = Vector(lat).normalized()
        row = []
        for c in range(subdiv_cols):
            f = (c / (subdiv_cols - 1)) * 2 - 1
            # slight curl: the centre line bulges outwards so the lock looks rounded
            bulge = (1 - f * f) * w * 0.28
            normal = Vector((0, -1, 0)) if abs(lat.y) < 0.9 else Vector((1, 0, 0))
            row.append(bm.verts.new(Vector(p) + lat * (f * w) + normal * bulge))
        cols.append(row)
    for a, b in zip(cols[:-1], cols[1:]):
        for c in range(subdiv_cols - 1):
            bm.faces.new((a[c], a[c + 1], b[c + 1], b[c]))
    bm.normal_update()
    o = from_bm(name, bm)
    return o


def bez_line(name, pts, radius, rgb, close=False):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'
    cu.bevel_depth = radius; cu.bevel_resolution = 3; cu.use_fill_caps = True
    sp = cu.splines.new('BEZIER'); sp.bezier_points.add(len(pts) - 1)
    for bp, p in zip(sp.bezier_points, pts):
        bp.co = p; bp.handle_left_type = 'AUTO'; bp.handle_right_type = 'AUTO'
    if close:
        sp.use_cyclic_u = True
    o = new_obj(name, cu)
    o.data.materials.append(flat(name, rgb))
    return o


def parent_all(root):
    for o in collection().objects:
        if o is not root and o.parent is None:
            o.parent = root
            o.matrix_parent_inverse = root.matrix_world.inverted()
