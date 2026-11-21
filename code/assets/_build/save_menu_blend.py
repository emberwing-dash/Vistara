import sys, bpy
sys.path.insert(0, r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\_build")
import gen_menu_outfit as g
g.Menu()
for col in bpy.data.collections:
    if col.name.startswith('Outfit_') and col.name != 'Outfit_Menu':
        col.hide_viewport = True; col.hide_render = True
bpy.ops.wm.save_as_mainfile(filepath=r"L:\Hackathons\college\new2\Samsung XR Hackathon\assets\character\Vistara_Girl_MenuOutfit.blend")
print("SAVED")
