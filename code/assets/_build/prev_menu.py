import sys, bpy, math
sys.path.insert(0, r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\_build")
import gen_menu_outfit as g
P = g.Menu()
BASE = r"L:\Hackathons\college\new2\Samsung XR Hackathon\poj\blender_preview"
# preview only: show the garment as it will look in game (steel-blue body, black cuffs), hide other outfits, render front / 3-4 / back
for col in bpy.data.collections:
    if col.name.startswith('Outfit_') and col.name != 'Outfit_Menu':
        col.hide_viewport = True; col.hide_render = True
sc = bpy.context.scene; sc.render.engine = 'BLENDER_EEVEE'
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sc.collection.objects.link(sun); sun.data.energy = 3.0; sun.rotation_euler = (math.radians(55), 0, math.radians(-25))
sc.world.use_nodes = True; sc.world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.7, 0.75, 0.9, 1); sc.world.node_tree.nodes['Background'].inputs['Strength'].default_value = 1.2
sc.render.resolution_x = 900; sc.render.resolution_y = 900; sc.render.film_transparent = False
cam = bpy.data.objects.new('pc', bpy.data.cameras.new('pc')); sc.collection.objects.link(cam); cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.75; sc.camera = cam
for name, loc, rot in (('front', (0, -3, 0.72), (math.radians(90), 0, 0)), ('back', (0, 3, 0.72), (math.radians(90), 0, math.radians(180))), ('side34', (-2.1, -2.1, 0.72), (math.radians(90), 0, math.radians(-45)))):
    cam.location = loc; cam.rotation_euler = rot; sc.render.filepath = BASE + r"\menu_outfit_%s.png" % name
    bpy.ops.render.render(write_still=True)
print("DONE")
