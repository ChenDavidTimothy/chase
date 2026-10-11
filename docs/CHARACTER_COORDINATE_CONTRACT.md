# CHASE production character coordinate contract (v0.3)

**This document is authoritative for new character assets and future AI-assisted development.**

| System | Right | Up | Forward | Foot pivot |
| --- | --- | --- | --- | --- |
| Blender production rig | +X | +Z | **-Y** | world origin (0,0,0) |
| Unity imported FBX/prefab | +X | +Y | **+Z** | local origin (0,0,0) |
| CHASE gameplay motor | +X | +Y | **+Z** | physics actor at capsule *center* |
| FBX export convention | `axis_forward='-Z'` | `axis_up='Y'` | verified in Unity | no per-prefab yaw |

The Blender reference file `Chibi/armor.blend` originally faced +X. It is a
**historical, unchanged shape reference**, not a template for new animation.
The production file `Chibi/Chibi_Rigged.blend` is normalized. The approved
geometry was preserved exactly under a rigid 90-degree axis rotation,
**uniform** scale and ground-level translation. No remodeling or mesh
proportion changes were made.

## Production invariants

1. **Blender authoring:** View > Front (Numpad 1) shows the character's face.
   The rig Armature object has identity location/rotation/scale. All 17 visual
   mesh parts have rotation 0 and scale 1. Root non-deforming control begins
   at the ground origin. Blender scene scale is 1 meter per unit.
2. **Unity importer:** Generic rig, 17 skinned renderers, valid Avatar, all 8
   original clips. Model and prefab local position=0, rotation=identity,
   scale=1. Facing is checked by actual facial geometry, not a guessed
   import-axis label. The imported model's face must point toward +Z.
3. **Scale:** Standing geometry extends from Z≈0 to Z≈1.8 in Blender and
   from Y≈0 to Y≈1.8 in Unity at scale 1. Collider tuning remains 1.8m
   height / 0.35m radius. Scale belongs to the authored model, not a hidden
   prefab adjustment. A future artistic height change requires simultaneous
   visual/collision review.
4. **Gameplay pivot:** `ChaseMotor` physics actor sits at the capsule
   midpoint (spawn Y=0.9); `ChaseMatch.CreateCompetitor` deliberately
   sets its visual child local Y to **`-tuning.standingHeight * 0.5f`**.
   This **one documented offset** aligns the model's foot pivot to the
   arena floor. It is not a hidden model-space correction. Do not move
   the physics origin merely to eliminate this legitimate relationship.
5. **Actions:** All clips use the same rig with the same native basis.
   There is no per-clip forward-axis compensation. Run's first/halfway
   frames alternate along forward; Idle pelvis and feet stay planted.
   Blender actions must have Fake User enabled or a persistent NLA user,
   otherwise unselected actions can vanish on reopening.
6. **Simulation:** The model/Animator is cosmetic and must not control
   Rigidbody, shared ChaseMotor movement, collision volume, or role abilities.
   Root motion is disabled. No dependence on the Blender/Unity visual
   transform is introduced into ChaseAction or IChaseActionSource.

## Export, validation and anti-regression checks

From project root:

```sh
blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/validate_asset.py
```

Then use the exporter described in `Chibi/rig_tools/README.md`.
It rejects an incorrectly oriented mesh, nonzero rig object transforms,
missing persisted clips or an unnormalized production file.

Unity EditMode `ChibiAssetConventionTests` checks the **real imported FBX**
and visual prefab; PlayMode `ChibiRigPlayTests` checks facing, animation,
run striding, Idle grounding and both roles' runtime foot-to-physics anchor.
These tests should be run after any rig, importer, prefab or motor change.

**Migration proof:** 24 sampled animation frames across eight actions,
17 skinned mesh parts, sampled vertices per mesh. The verified conversion
baked a -90 degree Blender Z rotation, uniform scale and feet-to-origin
translation while scaling pose-bone translation keys. Maximum sampled
world-position discrepancy from applying the equivalent world transform
to the previous rig was `4.9242e-7` m. A fresh Blender reopen verified
all eight actions persisted. Blender's independent validator measured
feet at Z=0, standing top Z=1.8, face direction (-Y) dot=1.0.
Unity EditMode measured an identity imported model facing +Z with ~1.8m height.

## What is deliberately *not* normalized

- The untouched historical `armor.blend` remains +X for source audit.
- Existing CHASE physics motor center origin remains at the standing capsule
  midpoint, not the feet.
- The model's large head may visually exceed the crouching 0.85m collider;
  normalizing axes and scale does **not** solve pose/clearance design.
  Do not conceal that known limitation with collision bypasses or ad hoc
  model shrinkage. Review it separately before polishing crouch/slide.

Never apply another -90/+90 rotation to a *normalized* character merely
because a package's export setting looks unfamiliar. Run the actual Blender
and Unity facing checks and fix the asset at the authoring boundary.
