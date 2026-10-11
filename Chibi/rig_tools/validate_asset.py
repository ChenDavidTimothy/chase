"""Read-only contract check: blender -b Chibi/Chibi_Rigged.blend --python Chibi/rig_tools/validate_asset.py"""
import bpy,math,json
from mathutils import Vector,Matrix
arm=bpy.data.objects["Chibi_Armature"]
expected={"CHASE_Idle","CHASE_Run","CHASE_Jump","CHASE_JumpLanding","CHASE_DiveProne","CHASE_Leap","CHASE_LeapRecovery","CHASE_Crouch","CHASE_CrouchWalk","CHASE_Slide","CHASE_SlideRecovery","CHASE_Turn"}
assert {a.name for a in bpy.data.actions if a.name.startswith("CHASE_")}==expected
assert all(bpy.data.actions[name].use_fake_user for name in expected)
assert "Blender -Y forward" in str(arm.get("basis",""))
assert arm.matrix_world==Matrix.Identity(4)
assert arm.data.bones["Root"].head_local.length<.00001
assert abs(float(arm.get("asset_height_m",0))-1.8)<.0001
meshes=[o for o in bpy.data.objects if o.type=="MESH" and o.name.endswith("_vis")]
assert len(meshes)==17
for obj in meshes:
    assert max(abs(v) for v in obj.rotation_euler)<.00001,obj.name
    assert max(abs(v-1.) for v in obj.scale)<.00001,obj.name
    mods=[m for m in obj.modifiers if m.type=="ARMATURE"]
    assert len(mods)==1 and mods[0].object==arm,obj.name
    assert len(obj.vertex_groups)==1 and len(obj.data.vertices)>0,obj.name
    group=obj.vertex_groups[0]
    for v in obj.data.vertices:
        weights=[w for w in v.groups if w.group==group.index]
        assert len(weights)==1 and abs(weights[0].weight-1.)<.0001,(obj.name,v.index)
def center(name):
    o=bpy.data.objects[name].evaluated_get(bpy.context.evaluated_depsgraph_get())
    return sum((o.matrix_world@Vector(c) for c in o.bound_box),Vector())/8
def bone(name):
    return arm.matrix_world@arm.pose.bones[name].matrix.translation
arm.animation_data.action=bpy.data.actions["CHASE_Idle"]
bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
forward=center("face_vis")-center("head_vis");forward.z=0
dot=forward.normalized().dot(Vector((0,-1,0)))
assert dot>.98,dot
dg=bpy.context.evaluated_depsgraph_get();zlo=float("inf");zhi=float("-inf")
for o in meshes:
    e=o.evaluated_get(dg);m=e.to_mesh()
    try:
        for v in m.vertices:
            z=(e.matrix_world@v.co).z;zlo=min(zlo,z);zhi=max(zhi,z)
    finally:e.to_mesh_clear()
assert abs(zlo)<.015,zlo
assert abs(zhi-1.8)<.015,zhi
idle=[]
for frame in (1,11,21,31,41):
    bpy.context.scene.frame_set(int(frame),subframe=frame%1);bpy.context.view_layer.update()
    idle.append((bone("pelvis").z,bone("foot_left").z,bone("foot_right").z))
assert max(max(abs(p[i]-idle[0][i]) for i in range(3)) for p in idle)<.005,idle
run=[]
arm.animation_data.action=bpy.data.actions["CHASE_Run"]
for frame in (1,13):
    bpy.context.scene.frame_set(int(frame),subframe=frame%1);bpy.context.view_layer.update()
    run.append(bone("shin_left").y-bone("shin_right").y)
assert run[0]*run[1]<-.01,run
for name in expected:
    a=bpy.data.actions[name]
    for f in (int(a.frame_range[0]),int(a.frame_range[1])):
        arm.animation_data.action=a;bpy.context.scene.frame_set(f);bpy.context.view_layer.update()
        for pb in arm.pose.bones:
            assert all(math.isfinite(x) for row in pb.matrix for x in row),(name,pb.name)
# v0.4 grounded low poses: verify real evaluated shell vertices, not just bone keys.
def pose_bounds(action, frame):
    arm.animation_data.action=bpy.data.actions[action]
    bpy.context.scene.frame_set(int(frame),subframe=frame%1);bpy.context.view_layer.update()
    depsgraph=bpy.context.evaluated_depsgraph_get()
    z_min=float("inf");z_max=float("-inf")
    for part in meshes:
        evaluation=part.evaluated_get(depsgraph)
        geometry=evaluation.to_mesh()
        try:
            for vert in geometry.vertices:
                z=(evaluation.matrix_world@vert.co).z
                z_min=min(z_min,z);z_max=max(z_max,z)
        finally:evaluation.to_mesh_clear()
    return z_min,z_max
for frame in (1,7,13,19,25):
    bottom,top=pose_bounds("CHASE_CrouchWalk",frame)
    assert bottom>=-.025 and top<=1.55,("crouch walk clearance",frame,bottom,top)
# Football tackle: near-horizontal, FEET FIRST and low throughout the
# committed slide. Never accept the old 1.49m upright-looking crouch glide.
for tick in range(4,14*4+1):
    frame=tick/4
    bottom,top=pose_bounds("CHASE_Slide",frame)
    assert bottom>=-.022,("slide floor penetration",frame,bottom)
    assert top<=.83,("slide must be genuinely horizontal",frame,top)
    arm.animation_data.action=bpy.data.actions["CHASE_Slide"]
    bpy.context.scene.frame_set(int(frame),subframe=frame%1)
    bpy.context.view_layer.update()
    head=bone("head");pelvis=bone("pelvis")
    front=bone("foot_left")
    assert head.y-pelvis.y>.70,("head must recline behind hips",frame)
    assert front.y-pelvis.y<-.43,("extended foot must lead",frame)
    assert abs(head.z-pelvis.z)<.25,("torso must be near horizontal",frame)
# Fully evaluated recovery, including fractional frames: prevent the
# intermediate animated feet sinking through the floor or head rising through
# a standard 1.64m canopy while the crouch collider protects the get-up.
for tick in range(8,7*8+1):
    frame=tick/8
    bottom,top=pose_bounds("CHASE_SlideRecovery",frame)
    assert bottom>=-.02,("slide recovery floor penetration",frame,bottom)
    assert top<=1.64,("slide recovery overhead protrusion",frame,top)
# A settled chest-first dive must fit the physically grounded low Box.
for frame in (1,5,10,15,19):
    bottom,top=pose_bounds("CHASE_DiveProne",frame)
    assert .20<=bottom<=.32 and top<=1.05,("prone mesh clearance",frame,bottom,top)
assert pose_bounds("CHASE_Idle",1)[0]>=-.015
# Forearms hinge forward in character coordinates (-Y), rather than folding
# behind the elbows. Check every integer frame, not just exported end points.
for action_name in sorted(expected):
    action=bpy.data.actions[action_name]
    arm.animation_data.action=action
    for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        for side in ("left","right"):
            forearm=arm.pose.bones["lower_arm_"+side]
            elbow=arm.matrix_world@forearm.head
            wrist=arm.matrix_world@forearm.tail
            assert elbow.y-wrist.y>.045,("backwards elbow",action_name,frame,side,elbow.y-wrist.y)
# Superman launch and get-up articulate through intermediate 1/8-frame keys;
# detect an elbow flipping briefly between authored whole-frame poses.
for action_name in ("CHASE_Leap","CHASE_DiveProne","CHASE_LeapRecovery"):
    action=bpy.data.actions[action_name]
    arm.animation_data.action=action
    for step in range(round(action.frame_range[0]*16),
                      round(action.frame_range[1]*16)+1):
        frame=step/16
        bpy.context.scene.frame_set(int(frame),subframe=frame%1)
        bpy.context.view_layer.update()
        for side in ("left","right"):
            forearm=arm.pose.bones["lower_arm_"+side]
            e=arm.matrix_world@forearm.head
            w=arm.matrix_world@forearm.tail
            assert e.y-w.y>.040,("fractional backward elbow",action_name,frame,side,e.y-w.y)
arm.animation_data.action=bpy.data.actions["CHASE_Idle"]
bpy.context.scene.frame_set(1)
print("CHASE_ASSET_CONTRACT_PASS",json.dumps({"actions":len(expected),"meshes":len(meshes),"face_dot":round(dot,6),"feet_z":round(zlo,6),"height":round(zhi,6),"run_swings":run}))
