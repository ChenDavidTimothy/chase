import bpy, os
from pathlib import Path
rig=Path(__file__).resolve().parents[1]/'Chibi_Rigged.blend'
assert Path(bpy.data.filepath).resolve()==rig, 'Open Chibi_Rigged.blend before exporting'
arm=bpy.data.objects['Chibi_Armature']
assert "Blender -Y forward" in str(arm.get('basis', '')), (
    "Export requires a normalized Blender -Y production character. "
    "Correct the only production source Chibi_Rigged.blend directly in Blender. "
    "Do not reintroduce the retired +X normalization scripts or Unity yaw compensation.")
assert max(abs(float(v)) for v in arm.location)<.00001
assert max(abs(float(v)) for v in arm.rotation_euler)<.00001
assert max(abs(float(v)-1.) for v in arm.scale)<.00001
assert arm.data.bones['Root'].head_local.length < .00001
expected={'CHASE_Idle','CHASE_Run','CHASE_Jump','CHASE_Crouch',
          'CHASE_Slide','CHASE_Vault','CHASE_Leap','CHASE_Turn'}
assert {a.name for a in bpy.data.actions if a.name.startswith('CHASE_')}==expected
assert all(bpy.data.actions[name].use_fake_user for name in expected), (
    "Every action must survive a fresh Blender file reopen")
from mathutils import Vector
arm.animation_data.action=bpy.data.actions['CHASE_Idle']
bpy.context.scene.frame_set(1)
bpy.context.view_layer.update()
dg=bpy.context.evaluated_depsgraph_get()
def midpoint(mesh_name):
    obj=bpy.data.objects[mesh_name].evaluated_get(dg)
    return sum((obj.matrix_world@Vector(v) for v in obj.bound_box),Vector())/8
f=midpoint('face_vis')-midpoint('head_vis')
f.z=0.
assert f.normalized().dot(Vector((0,-1,0)))>.98, (
    "Model's actual facial geometry must point toward Blender -Y")
for o in bpy.context.selected_objects:o.select_set(False)
arm.select_set(True);bpy.context.view_layer.objects.active=arm
parts=[o for o in bpy.data.objects if o.type=='MESH' and o.name.endswith('_vis')]
assert len(parts)==17
for o in parts:
    assert max(abs(float(v)) for v in o.rotation_euler)<.00001
    assert max(abs(float(v)-1.) for v in o.scale)<.00001
    o.select_set(True)
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
print('Exported normalized -Y asset',outfile,'with',len(parts),'bound meshes')
