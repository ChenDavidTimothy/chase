import bpy, os
from pathlib import Path
rig=Path(__file__).resolve().parents[1]/'Chibi_Rigged.blend'
assert Path(bpy.data.filepath).resolve()==rig, 'Open Chibi_Rigged.blend before exporting'
arm=bpy.data.objects['Chibi_Armature']
arm.animation_data.action=bpy.data.actions['CHASE_Idle']
bpy.context.scene.frame_set(1)
for obj in bpy.context.selected_objects:obj.select_set(False)
arm.select_set(True)
bpy.context.view_layer.objects.active=arm
parts=[obj for obj in bpy.data.objects if obj.type=='MESH' and obj.name.endswith('_vis')]
assert len(parts)==17
for obj in parts:obj.select_set(True)
outfile=Path(__file__).resolve().parents[2]/'Assets/Art/Characters/Chibi/Models/Chibi_Rigged.fbx'
outfile.parent.mkdir(parents=True,exist_ok=True)
if outfile.exists() and os.environ.get('CHASE_ALLOW_REGENERATE')!='1':
    raise RuntimeError('Back up existing FBX; set CHASE_ALLOW_REGENERATE=1 to overwrite')
bpy.ops.export_scene.fbx(filepath=str(outfile),use_selection=True,
    object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',
    global_scale=1,apply_unit_scale=True,add_leaf_bones=False,
    use_armature_deform_only=True,bake_anim=True,bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
    use_mesh_modifiers=True,path_mode='AUTO')
print('Exported',outfile,'with',len(parts),'bound meshes')
