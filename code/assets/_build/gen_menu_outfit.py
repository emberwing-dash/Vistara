"""Main-menu outfit for Vee: her black shirt with the play button stays; a stylised sci-fi zip hoodie is worn OPEN over it.
Run in Blender with Vistara_Girl.blend open:  import gen_menu_outfit as g; g.Menu()
Built from the sweatshirt's own surface (so it fits exactly), pushed outwards, with the front opened (a straight edge) so the shirt and its play icon show,
split into torso + two sleeves so the sleeves follow the arms, and coloured by zone (grey-blue body, navy shoulder yokes, slate cuffs and hem, mint hood, violet and teal stripes) with teal neon piping.
Contract as gen_outfits.py: collection 'Outfit_Menu' of '<Piece>__<bone>' meshes -> assets/character/outfits/Menu/Outfit_Menu.fbx."""
import sys, importlib, math
sys.path.insert(0, r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\_build")
import gen_outfits2
importlib.reload(gen_outfits2)
from gen_outfits2 import *
from mathutils import Vector

BODY, TRIMDARK = '#4f5a76', '#27314a'
GREEN, GREEN_DK, GREEN_CORE, TEAL = '#3dff7a', '#16b85c', '#d4ffe2', '#3dff7a'          # neon green glow
RING_BLUE = '#2347c9'
GRADIENT = [(0.0, '#1b2750'), (0.4, '#3d5290'), (1.0, '#a3aec6')]          # sci-fi gradient: deep navy at the shoulders fading to grey-blue
SHIRT_BLACK = '#101119'
VSTAR = "L:/Hackathons/college/new2/Samsung XR Hackathon/assets/VStar.blend"
OPEN_HALF = 0.078          # half-width of the open front (the play button on the shirt sits inside it)
PUSH = 0.016               # how far the hoodie stands off the shirt


def _build(name, faces, P, bone, solid=0.01):
    """faces: [(coords, '#hex')]; one object, one material per colour, shaded smooth, with thickness."""
    bm = bmesh.new(); cache = {}; cols = []
    for coords, hx in faces:
        if hx not in cols: cols.append(hx)
        vs = []
        for co in coords:
            k = tuple(round(v, 5) for v in co)
            if k not in cache: cache[k] = bm.verts.new(co)
            vs.append(cache[k])
        try: f = bm.faces.new(vs); f.material_index = cols.index(hx)
        except ValueError: pass
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for hx in cols: me.materials.append(mat(hx))
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new('%s__%s' % (name, bone), me); P.objects.link(ob); ob['bone'] = bone
    if solid:
        bpy.context.view_layer.objects.active = ob
        s = ob.modifiers.new('Solid', 'SOLIDIFY'); s.thickness = solid; s.offset = 1; bpy.ops.object.modifier_apply(modifier='Solid')
    return ob


def _split(pts, n=2):
    """Cut a face into 4n small quads (a quad per corner, each cut n x n) so the colour zones get clean edges."""
    k = len(pts); cen = sum((Vector(p) for p in pts), Vector()) / k; out = []
    for i in range(k):
        a = Vector(pts[i]); b = (Vector(pts[i]) + Vector(pts[(i + 1) % k])) * 0.5; d = (Vector(pts[i]) + Vector(pts[i - 1])) * 0.5
        q = (a, b, cen, d)
        def at(u, v): return q[0] * (1 - u) * (1 - v) + q[1] * u * (1 - v) + q[2] * u * v + q[3] * (1 - u) * v
        for iu in range(n):
            for iv in range(n):
                u0, u1, v0, v1 = iu / n, (iu + 1) / n, iv / n, (iv + 1) / n
                out.append([at(u0, v0), at(u1, v0), at(u1, v1), at(u0, v1)])
    return out


def _lerp_hex(stops, t):
    t = max(0.0, min(1.0, t))
    for (t0, h0), (t1, h1) in zip(stops, stops[1:]):
        if t <= t1:
            k = (t - t0) / (t1 - t0); a = [int(h0[i:i + 2], 16) for i in (1, 3, 5)]; b = [int(h1[i:i + 2], 16) for i in (1, 3, 5)]
            return '#%02x%02x%02x' % tuple(int(a[i] + (b[i] - a[i]) * k) for i in range(3))
    return stops[-1][1]


def zone(ctr):
    """Sci-fi gradient colouring: the body fades from deep navy (shoulders) to grey-blue (hem), each sleeve from navy (shoulder) to grey-blue (cuff);
    ribbed cuffs and hem are dark, with neon-green accents (opening piping, a stripe round each sleeve, the hood lining)."""
    ax = abs(ctr.x)
    if ctr.y < -0.02 and 0.46 < ctr.z < 1.03 and OPEN_HALF - 0.004 <= ax < OPEN_HALF + 0.02 and ax < 0.12: return GREEN
    if ax > 0.385 or ctr.z < 0.545: return TRIMDARK
    if 0.345 < ax <= 0.368: return GREEN
    if ctr.z > 0.985 and ax < 0.075: return GREEN_DK
    if ax > 0.135: return _lerp_hex(GRADIENT, (ax - 0.135) / (0.385 - 0.135))
    return _lerp_hex(GRADIENT, (0.995 - ctr.z) / (0.995 - 0.545))


def torus(name, bone, center, axis, R, r, col, P, nmaj=56, nmin=8):
    """A glowing ring floating over the hoodie; `axis` is the direction of the ring's hole ('y' faces the viewer, 'x' wraps an arm)."""
    bm = bmesh.new(); g = []
    for i in range(nmaj):
        a = 2 * math.pi * i / nmaj; row = []
        for j in range(nmin):
            t = 2 * math.pi * j / nmin; lx, ly, lz = (R + r * math.cos(t)) * math.cos(a), (R + r * math.cos(t)) * math.sin(a), r * math.sin(t)
            w = (lx, lz, ly) if axis == 'y' else (lz, lx, ly)
            row.append(bm.verts.new((center[0] + w[0], center[1] + w[1], center[2] + w[2])))
        g.append(row)
    for i in range(nmaj):
        for j in range(nmin): bm.faces.new((g[i][j], g[(i + 1) % nmaj][j], g[(i + 1) % nmaj][(j + 1) % nmin], g[i][(j + 1) % nmin]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat(col))
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new('%s__%s' % (name, bone), me); P.objects.link(ob); ob['bone'] = bone; return ob


def add_stara(P, bone, center, width):
    """A small badge of the star mascot (Stara) on the chest: a puffy yellow five-point star with her purple eyes, cheeks and smile, facing the viewer (-Y)."""
    cx, cy, cz = center; R = width / 2; depth = width * 0.26
    rings = [(1.0, 0.0), (0.97, 0.16), (0.86, 0.36), (0.66, 0.58), (0.38, 0.77), (0.12, 0.92), (0.0, 1.0)]
    outline = [(math.sin(2 * math.pi * i / 10) * (1.0 if i % 2 == 0 else 0.5), math.cos(2 * math.pi * i / 10) * (1.0 if i % 2 == 0 else 0.5)) for i in range(10)]
    bm = bmesh.new(); grid = []
    for sc, h in rings:
        grid.append([bm.verts.new((cx + R * sc * ox, cy - depth * h, cz + R * sc * oz)) for ox, oz in outline] if sc > 0 else [bm.verts.new((cx, cy - depth * h, cz))])
    for k in range(len(rings) - 1):
        for i in range(10):
            j = (i + 1) % 10
            if len(grid[k + 1]) == 1: bm.faces.new((grid[k][i], grid[k][j], grid[k + 1][0]))
            else: bm.faces.new((grid[k][i], grid[k][j], grid[k + 1][j], grid[k + 1][i]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); me = bpy.data.meshes.new('StaraBody'); bm.to_mesh(me); bm.free(); me.materials.append(mat('#ffbd2e'))
    for pl in me.polygons: pl.use_smooth = True
    ob = bpy.data.objects.new('StaraBody__%s' % bone, me); P.objects.link(ob); ob['bone'] = bone
    f = cy - depth - 0.001; u = width / 0.115
    parts = []
    for sx in (-1, 1):
        parts += [sp((cx + sx * 0.0135 * u, f, cz + 0.002 * u), (0.0072 * u, 0.0036 * u, 0.0092 * u), '#3a2a9c', sub=2), sp((cx + sx * 0.0135 * u + 0.0018 * u, f - 0.003 * u, cz + 0.006 * u), (0.0026 * u, 0.0014 * u, 0.003 * u), '#ffffff', sub=1),
                  sp((cx + sx * 0.0255 * u, f, cz - 0.010 * u), (0.0055 * u, 0.0026 * u, 0.0032 * u), '#ff8f86', sub=1)]
    parts.append(sp((cx, f, cz - 0.0135 * u), (0.0062 * u, 0.0028 * u, 0.0038 * u), '#e04a6a', sub=1))
    piece('Menu', 'StaraFace', bone, parts, P)


def patch_x(cat, name, bone, rows, hw, nx, col, P, off, xc, thick=0.006, y0=-0.4):
    """gen_outfits.patch, but centred on x = xc: a cloth panel projected onto the shirt so it hugs the body, with a little thickness."""
    bm = bmesh.new(); g = []
    for z in rows:
        g.append([bm.verts.new((xc - hw + 2 * hw * i / nx, y0, z)) for i in range(nx + 1)])
    for j in range(len(rows) - 1):
        for i in range(nx): bm.faces.new((g[j][i], g[j][i + 1], g[j + 1][i + 1], g[j + 1][i]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); me = bpy.data.meshes.new(cat + '_' + name); bm.to_mesh(me); bm.free(); me.materials.append(mat(col))
    for pl in me.polygons: pl.use_smooth = True
    ob = bpy.data.objects.new('%s__%s' % (name, bone), me); P.objects.link(ob); ob['bone'] = bone
    m = ob.modifiers.new('Wrap', 'SHRINKWRAP'); m.target = sweat; m.wrap_method = 'PROJECT'; m.use_project_x = False; m.use_project_y = True; m.use_project_z = False
    m.use_positive_direction = True; m.use_negative_direction = False; m.offset = off
    bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier='Wrap')
    pb = bmesh.new(); pb.from_mesh(ob.data); bad = [v for v in pb.verts if v.co.y < -0.2]; bmesh.ops.delete(pb, geom=bad, context='VERTS'); pb.to_mesh(ob.data); pb.free()          # parts that found no surface
    sd = ob.modifiers.new('Solid', 'SOLIDIFY'); sd.thickness = thick; sd.offset = 1; bpy.ops.object.modifier_apply(modifier='Solid'); return ob


def Menu():
    P = coll_for('Menu'); c = 'Menu'
    M = sweat.matrix_world; N3 = M.to_3x3()
    bm = bmesh.new(); bm.from_mesh(sweat.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index != 0], context='FACES')          # slot 1 = the play-icon decal: not part of the jacket
    bm.normal_update()
    for v in bm.verts: v.co = M @ v.co + (N3 @ v.normal).normalized() * PUSH
    # cut exact straight lines (opening edges, piping band, sleeve split, stripes, cuffs, hem) so every colour band and the opening edge are clean
    xs = []
    for x0 in [OPEN_HALF, OPEN_HALF + 0.018, 0.135, 0.345, 0.368, 0.385] + [0.135 + 0.025 * k for k in range(1, 10)]:
        xs += [x0, -x0]
    for x0 in xs:
        bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), dist=0.0001, plane_co=(x0, 0, 0), plane_no=(1, 0, 0))
    for z0 in [0.545] + [0.545 + 0.04 * k for k in range(1, 12)]:
        bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces), dist=0.0001, plane_co=(0, 0, z0), plane_no=(0, 0, 1))
    bm.normal_update()
    torso, slv_l, slv_r = [], [], []
    for f in bm.faces:
        ctr = f.calc_center_median(); n = f.normal
        if ctr.y < -0.03 and abs(ctr.x) < OPEN_HALF and 0.46 < ctr.z < 1.02: continue          # the open front (everything in front, zip teeth included)
        item = ([tuple(v.co) for v in f.verts], zone(ctr))
        if ctr.x > 0.135: slv_l.append(item)
        elif ctr.x < -0.135: slv_r.append(item)
        else: torso.append(item)
    bm.free()
    _build('JacketBody', torso, P, 'spine.002')
    _build('SleeveL', slv_l, P, 'upper_arm.L')
    _build('SleeveR', slv_r, P, 'upper_arm.R')
    # zip area: the shirt's own zip teeth are hidden under a clean black placket, with a neon-green zip tape and a pull
    # zip: a slim raised ridge (real thickness, not a flat sheet) with a small pull, along the line of the shirt's own zip
    patch_x(c, 'ZipRidge', 'spine.002', [0.955 - k * 0.0365 for k in range(0, 13)], 0.0085, 2, GREEN_DK, P, 0.012, -0.05, thick=0.012)
    piece(c, 'ZipPull', 'spine.002', [b((-0.05, -0.112, 0.79), (0.0075, 0.007, 0.022), GREEN, bev=True)], P)
    # the play icon on the shirt is hidden behind the star mascot (Stara) badge
    add_stara(P, 'spine.002', (0.006, -0.125, 0.86), 0.17)
    # big glowing rings floating round the arms (not on the front of the hoodie) and on the back: one neon green, the other dark blue
    for sd, sx in (('L', 1), ('R', -1)):
        bone = 'upper_arm.' + sd
        torus('RingArmA' + sd, bone, (0.305 * sx, 0.03, 0.845), 'x', 0.128, 0.014, GREEN, P)
        torus('RingArmB' + sd, bone, (0.355 * sx, 0.03, 0.822), 'x', 0.12, 0.012, RING_BLUE, P)
    torus('RingBackA', 'spine.002', (0, 0.14, 0.82), 'y', 0.08, 0.014, GREEN, P)
    torus('RingBackB', 'spine.002', (0, 0.14, 0.82), 'y', 0.052, 0.012, RING_BLUE, P)
    torus('RingBackC', 'spine.002', (0, 0.14, 0.82), 'y', 0.027, 0.009, GREEN, P)
    # sci-fi sneakers over her shoes: dark upper, white toe cap, glowing green sole, ankle collar, light laces
    for sd, sx in (('L', 1), ('R', -1)):
        x = 0.092 * sx; bone = 'foot.' + sd
        piece(c, 'Sneaker' + sd, bone, [sp((x, -0.042, 0.04), (0.058, 0.112, 0.05), '#1c2438', sub=3, zmin=0.006), sp((x, -0.105, 0.034), (0.052, 0.046, 0.038), '#eef3fa', sub=2, zmin=0.006),
                                          b((x, -0.04, -0.005), (0.066, 0.124, 0.012), GREEN, bev=False), b((x, -0.04, 0.0015), (0.062, 0.12, 0.003), GREEN_CORE, bev=False),
                                          tc((x, 0.02, 0.082), 0.044, 0.048, 0.018, '#2e3a58', seg=16), b((x, -0.07, 0.078), (0.03, 0.05, 0.006), GREEN_CORE, (-28, 0, 0))], P)
    export(c, P); return P
