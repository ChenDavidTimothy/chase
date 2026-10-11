# CHASE v0.3: production coordinate migration and verification

## Migration goals

Replace implicit imported-model yaw, scale, and vertical offsets with:
- Blender -Y forward / +Z up, unit=1m;
- Unity +Z forward / +Y up, FBX and prefab transform identity;
- ground-level foot pivot, authored standing height=1.8m;
- one explicit visual-to-physics-origin relationship in gameplay code.

No modification to character geometry proportions, original armor.blend,
collision tuning, match rules, action interfaces, or camera. The original +X-facing Blender reference was preserved during this historical
migration. It has since been moved to an archive **outside the active repository**
and remains recoverable from Git history. It is not an active production input.

## Backups and integrity

Before modification the working source, FBX, Unity prefab, source reference
and importer .meta were backed up *outside* the Git repository under
`/home/dtdav/chase-normalization-backup-20261011-090106`.
The original `Chibi/armor.blend` remained byte-identical before and after,
SHA-256 `a9ed114aa07063026eb8ad7272d2fdc8f6254c63028a3537fc91d0e31a6b8849`.

The original production working copy had a separate major latent problem:
when reopened in a new Blender process, only CHASE_Idle was retained;
seven other actions were lost because they had no saved data-block users.
The original exported FBX happened to contain the other clips, obscuring
the issue. All eight actions were regenerated with **Fake User**, then
verified on fresh save/reopen before normalization.

## Mathematical conversion and rig behavior

The migration is a **single affine transform** applied consistently to the
armature and all 17 bound visual mesh parts: Blender Z rotation of -90 degrees,
uniform unit conversion and vertical shift to set the lowest idle geometry at
Z=0. No remeshing and no individual part resculpting.

- Original evaluated idle Z bounds: `0.1663788855` to `1.5800000429`.
- Uniform scale used: `1.2733255940`.
- Output geometry: foot bottom Z=0.0, top Z=1.8.
- After baking: armature matrix identity, each mesh object rotation=0 and scale=1.
- Root control bone begins precisely at the ground origin.
- CHASE_Run now steps toward Blender -Y by default.
- Physical face-to-head direction in Blender: dot(-Y)=1.0.
- Pose-location animation curves (Blender 5.2 layered Action channelbags)
  were explicitly scaled by the same factor as the rest bones. Blender's
  Apply Transform operation alone would **not** do this correctly.

Conversion was verified against the pre-migration rig at **24 sampled keyframe
poses covering eight actions** with five evaluated vertex samples from each
of 17 mesh parts, using the expected transformed world positions.
Maximum observed discrepancy was `4.924151790695877e-7` m.
Pose and rigid geometry remain identical up to the uniform basis transform.

The stored Blender file was reopened, its eight CHASE actions and Root bone
checked, and the native Blender Front View verified with the real viewport.

## Unity import and runtime mapping

Unchanged FBX exporter axes: `axis_forward='-Z'`, `axis_up='Y'`.
The *actual* normalized imported facial geometry now points toward Unity +Z,
not simply a presumed Euler label. Importer remains Generic with valid Avatar.

Imported model and reusable Unity prefab child have position=(0,0,0),
rotation=(0,0,0), scale=(1,1,1), with 17 skinned renderers and eight clips.
Unity observed model feet approximately -0.004m and top approximately 1.800m
in idle, within the import-contract tolerance.

The gameplay Rigidbody transform was intentionally **not** moved from its
existing capsule-center convention. `ChaseMatch.CreateCompetitor` now places
the foot-pivot **visual root** at local Y `-tuning.standingHeight/2`
(currently -0.9m), once, explicitly. No corrective yaw or scale exists on
the imported model, prefab or character root. `ChaseMotor.OrientVisual`
continues to rotate that visual only when gameplay movement changes direction.

Animations have no root-motion authority and preserve shared physics
for human, NPC and future action sources.

## Regression prevention

1. The original one-time `normalize_asset.py`, `animate_validation.py`,
   and `build_rig.py` performed the historical +X conversion. **These are
   archived outside the active project and removed from the production
   pipeline.** Their original code remains in Git history for audit.
2. Active `Chibi/rig_tools/check_source_layout.py` enforces exactly one
   `.blend` source in the project and no retired scripts or Blender backups.
3. Active `Chibi/rig_tools/validate_asset.py` checks ground pivot, dimensions,
   actual face direction, 17 rigidly bound meshes, 8 persisted actions,
   run stride direction and grounded idle in a fresh Blender process.
4. Active `Chibi/rig_tools/export_unity_fbx.py` rejects unnormalized assets,
   absent clips, nonzero transform compensation, incorrect facial direction,
   and overwrites without explicit opt-in.
5. **Use `--python-exit-code 1`** in background Blender commands so a
   Python failure results in a failing shell exit code.
6. Unity EditMode `ChibiAssetConventionTests` asserts actual FBX import,
   unit prefab transforms, +Z face, scale, 1.8m ground pivot and clips.
   Unity PlayMode `ChibiRigPlayTests` validates both roles, real foot anchor,
   unrotated meshes, forward run stride and movement/animation independence.

See [the coordinate contract](../CHARACTER_COORDINATE_CONTRACT.md) for the
permanent engineering rule and complete reproduction instructions.

## Known, separate issues

The chibi's oversized head can still visibly exceed the 0.85m crouching
collider. Rigid shell joints and temporary run/parkour clips need visual
refinement. Asset orientation normalization was deliberately not used as an
excuse to alter collision mechanics or conceal visual clearance problems.

## Test and player build evidence

Automated checks completed: **13 Unity EditMode and 34 PlayMode tests passed**;
the independent Blender contract validator also passed.

The existing Unity Pipeline runtime configuration warning is unrelated to
the normalized character.

## Final production validation

- Blender 5.2.2 saved and freshly reopened the normalized production file:
  all eight CHASE actions retained, Fake User active on every action.
- Independent strict CLI validation:
  `CHASE_ASSET_CONTRACT_PASS`, 17 parts, actual face dot(-Y)=1.0,
  feet=0.0m and standing top=1.8m.
- Unity import and prefab: face dot(+Z)≈0.9913, imported foot minimum
  about -0.004m and top 1.800m. No prefab corrective yaw, location, or scale.
- Unity EditMode: **13 passed, zero failed** (including new import contract).
- Unity PlayMode: **34 passed, zero failed** (including spawn anchoring and
  physical run orientation; existing 100-round stress scenarios still execute).
- WebGL production build: **Succeeded, zero errors**, one unchanged Unity
  Pipeline configuration warning. Reloaded and rendered in Google Chrome.
- Linux x86_64 production build: **Succeeded, zero errors**, same optional
  Pipeline warning. Launched the actual Linux executable; the arena and
  characters rendered, and keyboard role switching and round transition worked.
  No gameplay exception was found in the checked native player log.
- Real Blender Front View, Linux native player and Chrome browser captures are
  included in this folder as `blender-front-normalized.jpg`,
  `linux-normalized.jpg`, `chrome-normalized.jpg`.

**Scope preserved:** `Assets/Scenes/SampleScene.unity`, game rules, physics
tuning, animation action semantics and project packages were not changed.
The original source file `Chibi/armor.blend` is unchanged by SHA-256.
No ML infrastructure was introduced. Normalization is not a substitute for
future collision/silhouette playtesting of the oversized crouching head.

## Production source consolidation (after normalization)

Only `Chibi/Chibi_Rigged.blend` remains as an active editable Blender
character. The legacy `armor.blend`, both `.blend1` backups, and all three
one-time migration scripts were copied with SHA-256 verification to an
external archive and removed from the active checkout. The authoritative
rigged file was not edited by this cleanup. The Git history retains the
original tracked source for provenance; historical OBJ mesh snapshots remain
non-authoritative references. Export only from the normalized rig.
