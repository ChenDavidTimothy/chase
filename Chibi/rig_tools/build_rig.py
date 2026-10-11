import bpy, math, json, os
from mathutils import Vector, Quaternion
from pathlib import Path
arm=bpy.data.objects.get("Chibi_Armature")
assert arm and arm.type=='ARMATURE'
src=str(Path(__file__).resolve().parents[1] / 'armor.blend')
assert bpy.data.filepath==src, bpy.data.filepath
faces=[o for o in bpy.data.objects if o.name.endswith('_vis') and o.type=='MESH']
assert len(faces)==17,len(faces)
# Clear the original exploratory arm splay while updating rest bone layout.
for p in arm.pose.bones:
    p.rotation_mode='QUATERNION';p.rotation_quaternion=(1,0,0,0)
    p.location=(0,0,0);p.scale=(1,1,1)
bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT')
bones=arm.data.edit_bones
def reposition(name,head,tail,parent=None):
    b=bones[name];b.head=head;b.tail=tail;b.use_connect=False
    b.parent=bones[parent] if parent else None
reposition('pelvis',(0,0,.68),(0,0,.81),None)
reposition('spine',(0,0,.81),(0,0,.96),'pelvis')
reposition('torso',(0,0,.96),(0,0,1.15),'spine')
reposition('neck',(0,0,1.15),(0,0,1.34),'torso')
reposition('head',(0,0,1.34),(0,0,1.55),'neck')
root=bones.new('Root')
root.head=(0,0,.05);root.tail=(0,0,.18);root.use_deform=False
bones['pelvis'].parent=root
for side,sgn in [('left',1),('right',-1)]:
    name='clavicle_'+side
    b=bones.new(name)
    b.head=(0,sgn*.10,1.10);b.tail=(0,sgn*.18,1.04)
    b.parent=bones['torso'];b.use_deform=False;b.use_connect=False
    bones['upper_arm_'+side].parent=b
    bones['thigh_'+side].parent=bones['pelvis']
bpy.ops.object.mode_set(mode='OBJECT')
arm.show_in_front=True
arm.display_type='WIRE'
arm['rig_version']='CHASE v0.2 FK rigid-shell rig'
arm['basis']='Blender +X forward, Z up; visual scale set by Unity visual prefab, never physical root'
arm['weights']='Rigid 1.0 assigned per separate geometry part; all surface vertices maintain shape'
# Existing _vis pieces represent approved character. Never import the redundant physics shell.
mapping={'torso_geom_vis':'torso','spine_geom_vis':'spine','pelvis_geom_vis':'pelvis','face_vis':'head'}
for obj in faces:
    if obj.name not in mapping:
        if obj.name.endswith('_vis'):
            mapping[obj.name]=obj.name[:-4]
    groupname=mapping[obj.name]
    assert groupname in arm.data.bones, (obj.name,groupname)
    current=obj.matrix_world.copy()
    obj.parent=arm
    obj.matrix_parent_inverse=arm.matrix_world.inverted()
    obj.matrix_world=current
    # Override incomplete legacy groups with one rigid influence per part.
    for g in list(obj.vertex_groups):obj.vertex_groups.remove(g)
    group=obj.vertex_groups.new(name=groupname)
    group.add(list(range(len(obj.data.vertices))),1.0,'REPLACE')
    mods=[m for m in obj.modifiers if m.type=='ARMATURE']
    if not mods:mods=[obj.modifiers.new('Chibi Skin','ARMATURE')]
    for m in mods:m.object=arm;m.use_deform_preserve_volume=False
    obj['deform_bone']=groupname
    obj.hide_render=False;obj.hide_set(False)
# physics shell is untouched, retained as hidden documentation in file.
for o in bpy.data.collections['Chibi_PhysicsShell'].objects:
    o.hide_render=True
# Pose neutral with A-like shoulder angle to preserve the starting silhouette.
def world_rot(bone,axis,angle):
    r=arm.data.bones[bone].matrix_local.to_quaternion()
    return r.inverted() @ Quaternion(Vector(axis),angle) @ r
for side,sgn in [('left',1),('right',-1)]:
    arm.pose.bones['upper_arm_'+side].rotation_quaternion=world_rot('upper_arm_'+side,(1,0,0),sgn*.85)
bpy.context.view_layer.update()
# Bounding after skin evaluation, to check the original silhouette.
dg=bpy.context.evaluated_depsgraph_get()
bounds=[]
for o in faces:
    evalo=o.evaluated_get(dg)
    bounds += [evalo.matrix_world @ Vector(v) for v in evalo.bound_box]
bb={'min':[round(min(v[i] for v in bounds),4) for i in range(3)], 'max':[round(max(v[i] for v in bounds),4) for i in range(3)]}
# Save first durable foundation; no overwrite of source.
output=str(Path(__file__).resolve().parents[1] / 'Chibi_Rigged.blend')
if os.path.exists(output) and os.environ.get('CHASE_ALLOW_REGENERATE') != '1':
    raise RuntimeError('Back up existing rig and set CHASE_ALLOW_REGENERATE=1 before rebuilding')
palette={'mat_gray':(.62,.65,.70,1.),'mat_orange':(.77,.77,.74,1.),'mat_white':(.88,.89,.91,1.)}
for material in bpy.data.materials:
    if material.name not in palette:continue
    color=palette[material.name]
    material.diffuse_color=color
    material.use_nodes=True
    for node in list(material.node_tree.nodes):
        if node.type=='TEX_IMAGE':material.node_tree.nodes.remove(node)
    shader=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if shader:
        shader.inputs['Base Color'].default_value=color
        shader.inputs['Roughness'].default_value=.83
for img in list(bpy.data.images):
    if img.source=='FILE' and img.users==0:bpy.data.images.remove(img)
bpy.ops.wm.save_as_mainfile(filepath=output,check_existing=False)
result={'file':bpy.data.filepath,'bones':[(b.name,b.parent.name if b.parent else None) for b in arm.data.bones],
  'mesh_count':len(faces),'weights':{o.name:[g.name for g in o.vertex_groups] for o in faces},
  'bounds':bb,'actions':len(bpy.data.actions)}
