"""One-time, mathematically verified conversion from legacy +X to native Blender -Y.
Run after animate_validation.py and before export_unity_fbx.py.
Retains the exact approved silhouette and every animation pose up to a uniform
coordinate transform. Rejects reruns and corrupted/missing action libraries.
"""
import bpy, math, json, os
from mathutils import Matrix, Vector

assert bpy.data.filepath.endswith("/Chibi_Rigged.blend")
arm = bpy.data.objects["Chibi_Armature"]
meshes = sorted([o for o in bpy.data.objects if o.type == "MESH" and o.name.endswith("_vis")], key=lambda o:o.name)
assert len(meshes) == 17
expected_actions = {"CHASE_Idle","CHASE_Run","CHASE_Jump","CHASE_Crouch","CHASE_Slide","CHASE_Vault","CHASE_Leap","CHASE_Turn"}
assert {a.name for a in bpy.data.actions if a.name.startswith("CHASE_")} == expected_actions, "All eight original actions must be present"
assert "Blender +X forward" in str(arm.get("basis", "")), "The asset is already normalized or the input is unexpected. Refusing to rotate twice."
assert bpy.context.scene.unit_settings.scale_length == 1.0
assert os.getenv("CHASE_ALLOW_REGENERATE") == "1", "Protect hand editing: back up the .blend and set CHASE_ALLOW_REGENERATE=1"

samples = []
for a in sorted(expected_actions):
    action = bpy.data.actions[a]
    start, end = [int(round(x)) for x in action.frame_range]
    samples.extend((a, f) for f in sorted(set((start,(start+end)//2,end))))
def snapshots():
    output={}
    for action_name,frame in samples:
        arm.animation_data.action=bpy.data.actions[action_name]
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        deps=bpy.context.evaluated_depsgraph_get()
        poses={}
        for obj in meshes:
            evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh()
            try:
                indices=sorted(set((0,len(mesh.vertices)//4,len(mesh.vertices)//2,3*len(mesh.vertices)//4,len(mesh.vertices)-1)))
                poses[obj.name]=[evaluated.matrix_world @ mesh.vertices[i].co for i in indices]
            finally:evaluated.to_mesh_clear()
        output[action_name,frame]=poses
    return output
before=snapshots()

arm.animation_data.action=bpy.data.actions["CHASE_Idle"];bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
deps=bpy.context.evaluated_depsgraph_get()
zmin=float("inf");zmax=float("-inf")
for obj in meshes:
    evaluated=obj.evaluated_get(deps);mesh=evaluated.to_mesh()
    try:
        for v in mesh.vertices:
            z=(evaluated.matrix_world @ v.co).z
            zmin=min(z,zmin);zmax=max(z,zmax)
    finally:evaluated.to_mesh_clear()
assert 0.12<zmin<.20 and 1.4<zmax<1.7,(zmin,zmax)

# Preserve the existing 1.8m gameplay silhouette at Unity scale=1.
height_m=1.8
scale=height_m/(zmax-zmin)
convert=(Matrix.Translation((0,0,-zmin*scale))
         @ Matrix.Rotation(-math.pi/2,4,"Z")
         @ Matrix.Diagonal((scale,scale,scale,1)))
original={obj.name:obj.matrix_world.copy() for obj in [arm,*meshes]}
arm.matrix_world=convert @ original[arm.name]
bpy.context.view_layer.update()
for obj in meshes: obj.matrix_world=convert @ original[obj.name]
bpy.context.view_layer.update()

bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True);bpy.context.view_layer.objects.active=arm
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)

# Blender's armature transform application scales rest bones but does NOT scale
# animated pose-bone location F-curves. This is a required data migration.
curve_count=0
for action in bpy.data.actions:
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                bag=strip.channelbag(slot,ensure=False)
                if bag is None: continue
                for fcurve in bag.fcurves:
                    if fcurve.data_path != 'pose.bones["pelvis"].location':continue
                    curve_count+=1
                    for point in fcurve.keyframe_points:
                        point.co.y *= scale
                        point.handle_left.y *= scale
                        point.handle_right.y *= scale
assert curve_count==3*len(expected_actions),curve_count

for obj in meshes:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)

# Root is a non-deforming control. Put the Root bone's head exactly on the
# ground pivot without changing the child-pelvis bind pose.
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True);bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode="EDIT")
root=arm.data.edit_bones["Root"]
root.head=(0.,0.,0.)
root.tail=(0.,0.,.13)
bpy.ops.object.mode_set(mode="OBJECT")
bpy.context.view_layer.update()

after=snapshots()
max_error=0.
for key,objects in before.items():
    for mesh_name,points in objects.items():
        for original_point,actual_point in zip(points,after[key][mesh_name]):
            max_error=max(max_error,(convert@original_point-actual_point).length)
assert max_error < .0001, "Pose migration lost geometry at "+str(max_error)
assert arm.matrix_world == Matrix.Identity(4)
assert all((max(abs(x) for x in obj.rotation_euler)<.00001 and all(abs(s-1.)<.00001 for s in obj.scale)) for obj in meshes)

arm.animation_data.action=bpy.data.actions["CHASE_Idle"]
bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
dg=bpy.context.evaluated_depsgraph_get()
def center(name):
    o=bpy.data.objects[name].evaluated_get(dg)
    return sum((o.matrix_world@Vector(v) for v in o.bound_box),Vector())/8
facing=center("face_vis")-center("head_vis")
facing.z=0
assert Vector((0,-1,0)).dot(facing.normalized())>.98,facing
assert abs(arm.data.bones["Root"].head_local.z)<1e-5

for action in bpy.data.actions:
    if action.name in expected_actions: action.use_fake_user=True
arm["basis"]="Blender -Y forward, +Z up; 1 Blender unit = 1 metre; world origin = feet on ground"
arm["rig_version"]="CHASE v0.3 normalized production rig"
arm["source_forward"]="Legacy approved armor.blend faces +X; no edits to original source"
arm["normalization_scale"]=scale
arm["original_foot_z"]=zmin
arm["asset_height_m"]=height_m
arm["animation_pose_transform_max_error_m"]=max_error
bpy.context.scene.unit_settings.system="METRIC"
bpy.context.scene.unit_settings.scale_length=1
bpy.context.scene.frame_start=1
bpy.context.scene.frame_end=41
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,check_existing=False)
print("CHASE_NORMALIZED "+json.dumps({"actions":len(expected_actions),"pose_samples":len(samples),"mesh_parts":len(meshes),"scale":scale,"bottom":zmin,"top":zmax,"max_pose_error":max_error,"face_xy":[facing.x,facing.y],"root_head":list(arm.data.bones["Root"].head_local)}))
