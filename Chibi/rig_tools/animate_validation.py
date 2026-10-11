import bpy,math,json
from mathutils import Vector,Quaternion
arm=bpy.data.objects['Chibi_Armature']
assert bpy.data.filepath.endswith('Chibi_Rigged.blend')
for act in list(bpy.data.actions):
    if act.name.startswith('CHASE_'):bpy.data.actions.remove(act)
arm.animation_data_create()
for pb in arm.pose.bones:pb.rotation_mode='QUATERNION'
def qworld(name,rotations):
    result=Quaternion((1,0,0,0))
    for axis,angle in rotations:
        result=result @ Quaternion(Vector(axis),float(angle))
    rest=arm.data.bones[name].matrix_local.to_quaternion()
    return rest.inverted() @ result @ rest
X=(1,0,0);Y=(0,1,0);Z=(0,0,1)
def pose(leg=0,elbow=.12,knee=.08, armswing=0,lean=0,headturn=0,crouch=0,
         legspread=0,up=0,twist=0,armraise=0,wrist=0):
    # Negative Y rotation swings downward-pointing leg toward +X (forward).
    rot={
      'upper_arm_left':[(X,.72+armraise),(Y,-armswing)],
      'upper_arm_right':[(X,-.72-armraise),(Y,armswing)],
      'lower_arm_left':[(Y,elbow)],
      'lower_arm_right':[(Y,elbow)],
      'hand_left':[(Y,wrist)],
      'hand_right':[(Y,wrist)],
      'thigh_left':[(Y,-leg),(X,legspread)],
      'thigh_right':[(Y,leg),(X,-legspread)],
      'shin_left':[(Y,knee)],
      'shin_right':[(Y,knee)],
      'foot_left':[(Y,-knee*.55)],
      'foot_right':[(Y,-knee*.55)],
      'spine':[(Y,lean*.45),(Z,twist*.4)],
      'torso':[(Y,lean*.55),(Z,twist*.6)],
      'neck':[(Z,headturn*.30)],
      'head':[(Z,headturn*.7)]
    }
    return {'rot':rot,'pelvis_z':crouch+up,'root_x':0}
poses={
 'CHASE_Idle':[(1,pose()),(11,pose(up=.015,headturn=.04)),(21,pose(up=.028,headturn=0)),(31,pose(up=.015,headturn=-.04)),(41,pose())],
 'CHASE_Run':[(1,pose(leg=.85,armswing=-.68,elbow=.55,knee=.15,lean=.17,up=.055)),
    (7,pose(leg=0,armswing=0,elbow=.40,knee=.42,lean=.15,up=-.020)),
    (13,pose(leg=-.85,armswing=.68,elbow=.55,knee=.15,lean=.17,up=.055)),
    (19,pose(leg=0,armswing=0,elbow=.4,knee=.42,lean=.15,up=-.020)),
    (25,pose(leg=.85,armswing=-.68,elbow=.55,knee=.15,lean=.17,up=.055))],
 'CHASE_Jump':[(1,pose(leg=.13,elbow=.33,knee=.2,crouch=-.10,lean=.20)),
    (7,pose(leg=.25,armswing=.4,armraise=.35,elbow=.4,knee=.50,up=.05,lean=-.09)),
    (15,pose(leg=.42,armswing=.1,armraise=.60,elbow=.5,knee=.8,up=.08,lean=-.05)),
    (23,pose(leg=.24,armswing=.25,elbow=.5,knee=.65,up=.02,lean=.06)),
    (29,pose(leg=.25,knee=.5,elbow=.3,crouch=-.15,lean=.16)),
    (33,pose(leg=.13,knee=.20,elbow=.25,lean=.06))],
 'CHASE_Crouch':[(1,pose()),(5,pose(knee=.48,leg=.12,elbow=.3,lean=.12,crouch=-.13)),
    (12,pose(knee=.85,leg=.28,elbow=.45,lean=.36,crouch=-.25)),
    (21,pose(knee=.85,leg=.28,elbow=.45,lean=.36,crouch=-.25))],
 'CHASE_Slide':[(1,pose(knee=.75,leg=.4,lean=.40,crouch=-.26,elbow=.32)),
    (8,pose(knee=.32,leg=1.1,lean=.67,crouch=-.32,armswing=.15,elbow=.40)),
    (18,pose(knee=.28,leg=1.12,lean=.61,crouch=-.31,armswing=.1,elbow=.40)),
    (26,pose(knee=.58,leg=.47,lean=.35,crouch=-.22,elbow=.32))],
 'CHASE_Vault':[(1,pose(leg=.18,lean=.19,elbow=.35,armraise=.2)),
    (8,pose(leg=.68,knee=.68,lean=.63,crouch=-.11,armraise=.78,elbow=.8)),
    (15,pose(leg=1.00,knee=.88,lean=.76,up=.06,armraise=.90,elbow=1.1)),
    (23,pose(leg=.63,knee=.55,lean=.38,armraise=.28,elbow=.40)),
    (31,pose())],
 'CHASE_Leap':[(1,pose(knee=.55,leg=.30,lean=.55,crouch=-.10,elbow=.52)),
    (9,pose(leg=1.0,armswing=.32,lean=.62,knee=.24,armraise=.50,elbow=.60,up=.10)),
    (17,pose(leg=1.14,armswing=-.22,lean=.73,knee=.12,armraise=.53,elbow=.4,up=.09)),
    (25,pose(leg=.35,knee=.61,lean=.48,crouch=-.12,elbow=.35))],
 'CHASE_Turn':[(1,pose()),(8,pose(twist=.42,headturn=.55,leg=.12)),
    (16,pose(twist=-.42,headturn=-.55,leg=-.12)),(25,pose())],
}
for name,keyposes in poses.items():
    act=bpy.data.actions.new(name)
    act.use_frame_range=True;act.frame_start=keyposes[0][0];act.frame_end=keyposes[-1][0]
    arm.animation_data.action=act
    for frame,config in keyposes:
        bpy.context.scene.frame_set(frame)
        for bone in arm.pose.bones:
            bone.rotation_quaternion=qworld(bone.name,config['rot'].get(bone.name,[]))
            bone.location=(arm.data.bones['pelvis'].matrix_local.to_quaternion().inverted() @ Vector((0,0,config['pelvis_z']))) if bone.name=='pelvis' else (0,0,0)
            bone.scale=(1,1,1)
            bone.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=bone.name)
            if bone.name=='pelvis':bone.keyframe_insert(data_path='location',frame=frame,group=bone.name)
        bpy.context.view_layer.update()
    act['purpose']='Rig validation only; not final authored gameplay motions'
# Idle is default active action; frame1
arm.animation_data.action=bpy.data.actions['CHASE_Idle']
bpy.context.scene.frame_set(1)
bpy.context.scene.render.fps=30
bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath,check_existing=False)
result={'actions':[{ 'name':a.name,'start':int(a.frame_range[0]),'end':int(a.frame_range[1]), 'slots':len(a.slots)} for a in bpy.data.actions if a.name.startswith('CHASE_')],
 'default_action':arm.animation_data.action.name,'pose_bones':len(arm.pose.bones)}
