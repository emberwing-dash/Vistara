import bpy, json
objs = []
for o in bpy.data.objects:
    d = {"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else None, "loc": [round(x, 3) for x in o.location], "dim": [round(x, 3) for x in o.dimensions], "hide": o.hide_viewport}
    if o.type == 'MESH':
        d["verts"] = len(o.data.vertices); d["mats"] = [m.name if m else None for m in o.data.materials]; d["mods"] = [m.type for m in o.modifiers]
        d["shapekeys"] = len(o.data.shape_keys.key_blocks) if o.data.shape_keys else 0; d["vgroups"] = len(o.vertex_groups)
    objs.append(d)
arm = [(a.name, len(a.bones)) for a in bpy.data.armatures]
imgs = [(i.name, list(i.size), i.filepath) for i in bpy.data.images]
mats = []
for m in bpy.data.materials:
    tex = []
    if m.use_nodes:
        for n in m.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image: tex.append(n.image.name)
    mats.append((m.name, tex))
print("INSPECT" + json.dumps({"objects": objs, "armatures": arm, "images": imgs, "materials": mats, "collections": [c.name for c in bpy.data.collections], "unit": bpy.context.scene.unit_settings.scale_length}))
