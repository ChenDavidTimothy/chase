# Chibi rig build scripts

These scripts document the repeatable Blender 5.2 rigging process.
The saved Chibi_Rigged.blend and Unity FBX are already complete, so
users do NOT need to regenerate them merely to use the character.

Always back up hand-authored changes before running any script.

From the repository root:

1. CHASE_ALLOW_REGENERATE=1 blender -b Chibi/armor.blend --python Chibi/rig_tools/build_rig.py
2. blender -b Chibi/Chibi_Rigged.blend --python Chibi/rig_tools/animate_validation.py
3. CHASE_ALLOW_REGENERATE=1 blender -b Chibi/Chibi_Rigged.blend --python Chibi/rig_tools/export_unity_fbx.py

Step 1 creates a separate rigged copy without modifying armor.blend.
It preserves the approved mesh vertices and generates self-contained
provisional materials. Step 2 replaces the eight CHASE_* validation
actions and therefore must not be run over later hand-authored clips.
Step 3 exports only the visible 17 bound parts and the armature, not
the original hidden physics-shell reference meshes.

The rebuild and FBX export commands refuse overwriting their outputs
unless the CHASE_ALLOW_REGENERATE flag is supplied.
