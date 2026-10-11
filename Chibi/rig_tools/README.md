# CHASE production character workflow

**One authoritative, editable character source:** `Chibi/Chibi_Rigged.blend`.

Use that file for every subsequent Blender rig, mesh, material, and animation edit. It is already normalized. **Never regenerate it from `armor.blend` or apply another corrective axis rotation.** The obsolete +X source and one-time migration scripts have been removed from the active project, archived outside the repository, and retained in Git history.

## Permanent coordinate contract

- Blender: **-Y forward**, +Z up, feet at world (0, 0, 0), 1 unit = 1 meter.
- Unity: **+Z forward**, +Y up, imported model and prefab use local position zero, rotation identity, scale one.
- Standing character height: 1.8 m. The one intentional `-standingHeight/2` offset belongs to `ChaseMatch`'s visual child under the physics actor, not the FBX or model prefab.
- Generic Unity rig, 17 visual skinned meshes, eight saved animation actions with Fake User enabled. Animator root motion stays disabled.

Read [`docs/CHARACTER_COORDINATE_CONTRACT.md`](../../docs/CHARACTER_COORDINATE_CONTRACT.md) before altering the rig or exporter.

## Normal production workflow

1. Open **`Chibi/Chibi_Rigged.blend`** in Blender. Edit and save this file only. Keep all production Actions saved with persistent data-block users.
2. Verify source files and normalized character data from repository root:
   ```sh
   python3 Chibi/rig_tools/check_source_layout.py
   blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/validate_asset.py
   ```
3. Back up the existing FBX and its `.meta` before intentionally re-exporting it, then run:
   ```sh
   CHASE_ALLOW_REGENERATE=1 blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/export_unity_fbx.py
   ```
4. Import into Unity using the existing Generic importer configuration. **Do not add any prefab correction rotation, height offset, or non-unit scale.** Run `ChibiAssetConventionTests`, `ChibiRigPlayTests`, and the full gameplay test suite; verify both roles and applicable builds.

The Blender exporter **refuses to overwrite** the FBX without explicit `CHASE_ALLOW_REGENERATE=1`, and refuses incorrectly oriented or incomplete rigs. Blender's `--python-exit-code 1` is important: without it, a Python traceback might otherwise return a successful shell exit status.

## Historical material

`Chibi/armor.blend` was the +X-facing original and is **not** an active source. Historical `.blend1` files and the one-time `build_rig.py`, `animate_validation.py`, and `normalize_asset.py` scripts are archived outside the repository. Their previously committed versions remain recoverable through Git history; none belongs in the normal export process.

The committed `Chibi/chibi_objs/` files are **historical mesh snapshots**, not a second rig or authoritative editing input. Unity's FBX and `Assets/Resources/ChibiVisual.prefab` are generated downstream assets. **Do not independently remodel or animate them.**

Blender may create a new ignored `.blend1` when saving. Move such working backups outside the active project before a production handoff; they are never an alternative source of truth.
