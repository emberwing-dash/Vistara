"""Finishing pass on assets/character/Vistara_Girl_MenuOutfit.blend (the hand-edited file; run it IN PLACE, it only touches what is listed):
 1. merge the near-identical gradient materials of the jacket pieces into a few (fewer draw calls in Unity)
 2. shoes: a dark copy of her shoes (white -> dark, dark toe cap and sole) as an outfit piece, skinned to the rig, with a green ring embedded in each heel
 3. rig: JacketBody / SleeveL / SleeveR get the sweatshirt's bone weights (nearest vertex) + an Armature modifier, parented to metarig, so they bend with the arms and spine
 4. save, and export Outfit_Menu.fbx (jacket pieces skinned to metarig, the other pieces rigid with their '<Piece>__<bone>' names)
Run:  blender -b assets/character/Vistara_Girl_MenuOutfit.blend --python assets/_build/finish_menu_outfit.py"""
import sys, os, math, bpy, bmesh
sys.path.insert(0, r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\_build")
from gen_common import mat
from mathutils import kdtree

COL = bpy.data.collections['Outfit_Menu']; arm = bpy.data.objects['metarig']; sweat = bpy.data.objects['Sweatshirt']
SKINNED = ['JacketBody__spine.002', 'SleeveL__upper_arm.L', 'SleeveR__upper_arm.R']
GREEN = '#3dff7a'
SHOE_SOLE = '#16204a'
SHOE_UPPER = [(0.0, '#1b2a66'), (0.45, '#2f7fd0'), (1.0, '#6fb4f0')]          # stylised sci-fi gradient: deep blue at the sole fading to light blue at the ankle
SHOE_TOE = [(0.0, '#1c8a8a'), (1.0, '#48d8c0')]                                 # toe cap: teal gradient


def lerp_hex(stops, t, levels=8):
    t = round(max(0.0, min(1.0, t)) * levels) / levels
    for (t0, h0), (t1, h1) in zip(stops, stops[1:]):
        if t <= t1:
            k = (t - t0) / (t1 - t0); a = [int(h0[i:i + 2], 16) for i in (1, 3, 5)]; b = [int(h1[i:i + 2], 16) for i in (1, 3, 5)]
            return '#%02x%02x%02x' % tuple(int(a[i] + (b[i] - a[i]) * k) for i in range(3))
    return stops[-1][1]


def hex_of(name): return [int(name[2:][i:i + 2], 16) for i in (0, 2, 4)]


def merge_materials(ob, step=22):
    """Snap every material colour to a coarse grid (greens untouched) and merge the slots that end up equal."""
    new_names = []; remap = {}
    for i, m in enumerate(ob.data.materials):
        r, g, b = hex_of(m.name)
        if not (g > 200 and r < 140): r, g, b = [min(255, int(round(c / step)) * step) for c in (r, g, b)]
        nn = 'M_%02x%02x%02x' % (r, g, b)
        if nn not in new_names: new_names.append(nn)
        remap[i] = new_names.index(nn)
    idx = [remap[p.material_index] for p in ob.data.polygons]          # keep the per-face result before the slots are rebuilt (Blender resets face indices when slots change)
    ob.data.materials.clear()
    for nn in new_names: ob.data.materials.append(mat('#' + nn[2:]))
    for p, k in zip(ob.data.polygons, idx): p.material_index = k
    return len(new_names)


def torus(name, bone, center, R, r, col, nmaj=40, nmin=8):
    """Ring whose hole points along the X axis (it sits flat on the side of the heel)."""
    bm = bmesh.new(); g = []
    for i in range(nmaj):
        a = 2 * math.pi * i / nmaj; row = []
        for j in range(nmin):
            t = 2 * math.pi * j / nmin; lx, ly, lz = (R + r * math.cos(t)) * math.cos(a), (R + r * math.cos(t)) * math.sin(a), r * math.sin(t)
            row.append(bm.verts.new((center[0] + lz, center[1] + lx, center[2] + ly)))
        g.append(row)
    for i in range(nmaj):
        for j in range(nmin): bm.faces.new((g[i][j], g[(i + 1) % nmaj][j], g[(i + 1) % nmaj][(j + 1) % nmin], g[i][(j + 1) % nmin]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat(col))
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new('%s__%s' % (name, bone), me); COL.objects.link(ob); ob['bone'] = bone; return ob


# 1 + 2 ---------------------------------------------------------------------------------
for n in SKINNED:
    print('MERGED', n, merge_materials(bpy.data.objects[n]))
for n in ('SneakerL__foot.L', 'SneakerR__foot.R'):          # the earlier small sneaker shells are replaced by the dark copy of her real shoes
    if n in bpy.data.objects: bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
for o in [o for o in COL.objects if o.name.startswith(('ShoeRing', 'ShoesDark'))]: bpy.data.objects.remove(o, do_unlink=True)


def dark_shoes():
    sh = bpy.data.objects['Shoes']; dg = bpy.context.evaluated_depsgraph_get(); me = bpy.data.meshes.new_from_object(sh.evaluated_get(dg)); me.transform(sh.matrix_world)
    me.calc_loop_triangles(); bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table(); bm.normal_update()
    names = [m.name if m else '' for m in sh.data.materials]
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if names[min(f.material_index, len(names) - 1)] == 'Emission'], context='FACES')          # the ink outline shell is not needed
    for v in bm.verts: v.co = v.co + v.normal * 0.0025
    cols = []; seq = []
    for f in bm.faces:
        c = f.calc_center_median(); t = (c.z - 0.022) / (0.14 - 0.022)
        hx = SHOE_SOLE if c.z < 0.022 else (lerp_hex(SHOE_TOE, t) if c.y < -0.07 else lerp_hex(SHOE_UPPER, t))
        if hx not in cols: cols.append(hx)
        seq.append(cols.index(hx))
    bm.to_mesh(me); bm.free(); me.materials.clear()
    for hx in cols: me.materials.append(mat(hx))
    for p, k in zip(me.polygons, seq): p.material_index = k
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new('ShoesDark__foot.L', me); COL.objects.link(ob); ob['bone'] = 'foot.L'
    # skin weights from the original shoes (nearest vertex), then an Armature modifier
    kd2 = kdtree.KDTree(len(sh.data.vertices))
    for i, v in enumerate(sh.data.vertices): kd2.insert(sh.matrix_world @ v.co, i)
    kd2.balance(); gn = {g.index: g.name for g in sh.vertex_groups}; groups = {}
    for v in me.vertices:
        _c, i, _d = kd2.find(v.co); w = {gn[e.group]: e.weight for e in sh.data.vertices[i].groups if e.weight > 0.001}
        top = sorted(w.items(), key=lambda kv: -kv[1])[:4]; tot = sum(x for _n, x in top) or 1.0
        for bn, x in top:
            if bn not in groups: groups[bn] = ob.vertex_groups.new(name=bn)
            groups[bn].add([v.index], x / tot, 'REPLACE')
    mod = ob.modifiers.new('Armature', 'ARMATURE'); mod.object = arm; ob.parent = arm; ob.matrix_parent_inverse = arm.matrix_world.inverted()
    # a green ring embedded in each heel (flush with the outer wall of the heel)
    for sd, sx in (('L', 1), ('R', -1)):
        wall = [sx * (v.co.x - 0.092 * sx) for v in me.vertices if sx * v.co.x > 0 and abs(v.co.y - 0.06) < 0.02 and abs(v.co.z - 0.06) < 0.02 and abs(v.co.x - 0.092 * sx) < 0.09]
        edge = (max(wall) if wall else 0.05)
        torus('ShoeRing' + sd, 'foot.' + sd, (0.092 * sx + sx * (edge - 0.001), 0.06, 0.06), 0.026, 0.007, GREEN)
        print('RING', sd, round(edge, 3))
    return ob


dark_shoes()

# 3 -------------------------------------------------------------------------------------
M = sweat.matrix_world; kd = kdtree.KDTree(len(sweat.data.vertices))
for i, v in enumerate(sweat.data.vertices): kd.insert(M @ v.co, i)
kd.balance(); gname = {g.index: g.name for g in sweat.vertex_groups}
for n in SKINNED:
    ob = bpy.data.objects[n]
    for g in list(ob.vertex_groups): ob.vertex_groups.remove(g)
    for m in list(ob.modifiers): ob.modifiers.remove(m)
    groups = {}
    for v in ob.data.vertices:
        w = {}
        near = kd.find_n(ob.matrix_world @ v.co, 3)
        tot = sum(1.0 / (d + 1e-4) for _c, _i, d in near)
        for _c, i, d in near:
            k = (1.0 / (d + 1e-4)) / tot
            for e in sweat.data.vertices[i].groups: w[gname[e.group]] = w.get(gname[e.group], 0) + e.weight * k
        top = sorted(w.items(), key=lambda kv: -kv[1])[:4]; s = sum(x for _n, x in top) or 1.0
        for bn, x in top:
            if bn not in groups: groups[bn] = ob.vertex_groups.new(name=bn)
            groups[bn].add([v.index], x / s, 'REPLACE')
    mod = ob.modifiers.new('Armature', 'ARMATURE'); mod.object = arm
    ob.parent = arm; ob.matrix_parent_inverse = arm.matrix_world.inverted()
    print('RIGGED', n, len(groups), 'groups')

# every other piece (rings, zip pull, Stara badge, shoe rings ...) is skinned 100 % to its bone, so it moves with the arms / spine / feet like part of the outfit
for o in list(COL.objects):
    if o.type != 'MESH' or any(m.type == 'ARMATURE' for m in o.modifiers) or not o.get('bone'): continue
    vg = o.vertex_groups.new(name=o['bone']); vg.add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')
    mod = o.modifiers.new('Armature', 'ARMATURE'); mod.object = arm; o.parent = arm; o.matrix_parent_inverse = arm.matrix_world.inverted()
    print('BONEBOUND', o.name, '->', o['bone'])

bpy.ops.wm.save_mainfile()
print('SAVED')

# 4 -------------------------------------------------------------------------------------
out = r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\character\outfits\Menu\Outfit_Menu.fbx"; os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.object.select_all(action='DESELECT')
for o in COL.objects: o.select_set(True)
arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'MESH', 'ARMATURE'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=False, mesh_smooth_type='FACE', add_leaf_bones=False, use_armature_deform_only=False, bake_anim=False)
print('EXPORTED', out)
