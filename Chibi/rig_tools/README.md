# CHASE character production pipeline (normalized v0.3)

**Authoritative editable animation/rig source:** `Chibi/Chibi_Rigged.blend`.
**Immutable historic approved-shape reference:** `Chibi/armor.blend`.

The reference model faces Blender +X, an old exception. Every new *production*
Blender rig and animation must face Blender -Y, with +Z up, a ground-level
origin, 1 unit = 1 meter, and object rotation 0/scale 1.

The committed normalized chibi is ready to use. Do not regenerate it merely
to edit animations. The following instructions describe reproduction from
the historical reference if explicitly required.

## Safe one-time reproduction from the reference

Back up edited `.blend`, FBX, prefab and Unity `.meta` files before proceeding.
The tools deliberately refuse accidental overwrite where relevant.

Run from the repository root with Blender 5.2:

1. `CHASE_ALLOW_REGENERATE=1 blender -b Chibi/armor.blend --python-exit-code 1 --python Chibi/rig_tools/build_rig.py`
2. `blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/animate_validation.py`
3. `CHASE_ALLOW_REGENERATE=1 blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/normalize_asset.py`
4. `blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/validate_asset.py`
5. `CHASE_ALLOW_REGENERATE=1 blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/export_unity_fbx.py`

**Always use `--python-exit-code 1`.** Without it Blender may exit with code 0 even after an assertion traceback, breaking CI/failure detection.

**Order matters:** Step 2 authors animation in the original intermediate +X
basis. Step 3 performs the one-time baked basis conversion, preserving key poses
and scaling Blender 5.2 pose-location F-curves with the new skeleton. Steps
2 and 3 explicitly reject a normalized input so no one can rotate it twice.

All eight validation actions have Fake User enabled; they persist after a
fresh save/reopen, even when not the active action. New production actions
must likewise be retained before saving. Step 4 is a read-only Blender test.

FBX exporter uses `axis_forward='-Z'`, `axis_up='Y'`, a **normalized -Y model**,
and no Unity corrective yaw. The exporter fails fast if orientation, root,
or clip persistence is wrong. Unity's prefab/model child must remain
position (0,0,0), rotation (0,0,0), scale (1,1,1).

Unity's *gameplay* actor transform is intentionally centered at the 1.8 m
capsule midpoint, 0.9 m above the floor. ChaseMatch places the normalized
foot-pivot visual **-standingHeight/2** below that origin. This is an
explicit physics/visual hierarchy relationship, not an axis conversion.

See `docs/CHARACTER_COORDINATE_CONTRACT.md` and automated
`ChibiAssetConventionTests`/PlayMode tests before exporting changes.
