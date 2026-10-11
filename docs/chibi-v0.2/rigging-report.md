**Historical rigging report (v0.2/v0.2.1).** The authored-axis, prefab
offset/scale and rotation details below describe the *old* character build.
**They were superseded by the v0.3 normalization.** For the current
production facts, see [CHARACTER_COORDINATE_CONTRACT.md](../CHARACTER_COORDINATE_CONTRACT.md).
The original armor.blend remains the +X-facing historical reference; the
current Chibi_Rigged.blend, FBX and prefab use normalized authoring.

# CHASE v0.2: Chibi rig validation report

## Source integrity and scope

- Development branch: main.
- Original, unchanged source: Chibi/armor.blend.
- Original source SHA-256: a9ed114aa07063026eb8ad7272d2fdc8f6254c63028a3537fc91d0e31a6b8849.
- Independent, editable rig: Chibi/Chibi_Rigged.blend.
- Unity FBX: Assets/Art/Characters/Chibi/Models/Chibi_Rigged.fbx.
- This is a rigging-and-integration milestone. It does not add parkour mechanics, retune physics, introduce ML-Agents, or choose final textures.
- Source backups remain outside the Git project in /home/dtdav/chase-rig-backup.

## Inspection and rigging decision

The source already contained a 17-bone exploratory armature, separate approved visual meshes, and hidden physics-shell geometry. The arms were posed in an A shape. The head, upper/lower limbs, torso, pelvis, and hands/feet form separate rigid-style pieces rather than a continuous organic skin. The torso, spine, pelvis and face had incomplete or absent skin weights.

Blender uses +X as the face direction and +Z as up. The approved head reaches approximately z=1.58 units and the visible foot bottom is near z=0.17. Standing arm span is approximately 1.0 unit. The character is not a conventional adult-proportioned body, and its geometry was not redesigned.

The existing armature was refined, rather than replaced by a stock humanoid rig:

- Root (non-deforming), pelvis, spine, torso, neck, head.
- Clavicle_left and clavicle_right (non-deforming shoulder pivots).
- Each side: upper_arm, lower_arm, hand, thigh, shin, foot.
- Total: 20 bones, including the original articulated bones.
- Consistent parent hierarchy Root > pelvis > spine > torso > neck > head, with limbs descending from pelvis/shoulder.
- The animator works with simple FK bone rotations and the Root/pelvis controls. IK, pole targets and IK/FK switching were deliberately omitted to avoid an unstable half-finished control rig.

All 17 actual _vis mesh parts have an explicit rigid vertex weight of 1.0 to their intended deform bone. Face geometry is bound to the head bone, not simply parented to the undeformed head object. The original hidden physics-shell parts are preserved within the rigged .blend, but the FBX exports only the 17 visible parts and skeleton.

A Blender 5.2 binary comparison of raw local mesh coordinates and topology between the original and rigged files confirmed EXACT equality for 17 objects, 24,684 vertices and 25,414 faces. The appearance/proportions were not edited.

The original file referenced four missing external images. The rigged working copy uses self-contained neutral material nodes instead, with no external missing-file references. This is provisional shading, not a final texture decision. The original materials/source are untouched.

## Proof-of-function Blender animations

Eight editable CHASE validation actions are included in the saved rig:

| Action | Purpose | Frame range (30 fps) |
| --- | --- | --- |
| CHASE_Idle | In-place subtle breathing/bobbing | 1 to 41 |
| CHASE_Run | Alternating legs and opposing arm swing | 1 to 25 |
| CHASE_Jump | Anticipation, takeoff, flight, landing preparation | 1 to 33 |
| CHASE_Crouch | Torso pitch, bent knees and lowered pelvis | 1 to 21 |
| CHASE_Slide | Low forward posture, extended legs | 1 to 26 |
| CHASE_Vault | Raised arms and tucked legs over a hurdle | 1 to 31 |
| CHASE_Leap | Test of forward-reaching articulation, not a gameplay ability | 1 to 25 |
| CHASE_Turn | Torso/head counter-rotation and turning | 1 to 25 |

The Run action was actually played in the interactive Blender GUI and verified advancing to frame 23 before stopping. Pose snapshots were inspected from the character front and side. The rigged working file reopens on Idle frame 1. Run and Idle loop in Unity, the others are provisional non-looping clips.

The separate geometry remains rigid and does not stretch or collapse. As a tradeoff, deep poses can intersect neighboring shell pieces or leave small gaps around joints. These actions are functional rig validation, not final production-quality motion capture.

## Unity import findings

The imported FBX contains 17 SkinnedMeshRenderers, eight named clips and no colliders.

Both Human and Generic Avatar creation report valid, but an in-Editor bone-transform comparison showed that Humanoid retargeting effectively cancels the authored crouch translation: the head stays near world y=1.48. Under Generic, the pelvis drops from y=0.68 to about y=0.33 and the head from y=1.53 to about y=1.14. Therefore the final import intentionally uses Generic. The Avatar is valid, and the actual clip transforms play.

The reusable visual prefab is Assets/Resources/ChibiVisual.prefab. Its model child has an adjustable scale of about 1.2766, a reversible floor-level offset, and a +90 degree yaw. Empirical Unity SkinnedMeshRenderer inspection established that Blender +X facing becomes imported model-local -X, so the corrected +90 degree offset converts the actual face to CHASE +Z forward. Neither Blender geometry nor the physical gameplay root is rescaled. The exact final artistic/world scale remains open.

The Animator references Assets/Art/Characters/Chibi/Animations/Chibi_Validation.controller. ChibiVisualDriver is explicitly bound after ChaseMotor is added to the gameplay root (the visual prefab Awake executes first). It observes the shared ChaseMotor movement state, cross-fades to Idle, Run, Jump, Crouch, Slide or Vault, and never writes physics velocities or positions. Turn and Leap clips are proof assets only and are not additional gameplay abilities.

ChaseMatch loads the prefab for both players, using temporary red/blue property-block coloring for role readability. The existing primitive capsule visual remains a fallback if the prefab is unavailable. ChaseMotor.SetHeight continues to change the real CapsuleCollider identically for both roles, but no longer shrinks/scales or translates the entire production skeleton. Root motion is explicitly disabled. No mesh colliders are introduced.

## Tests and real runtime validation

- Unity EditMode: 10 passed, zero failed.
- Unity PlayMode: 29 passed, zero failed.
- New tests assert: 17 separately bound meshes with rigid weights, eight valid playable clips, Generic Avatar, root-motion disabled, no visual colliders, face/head alignment, crouch bone lowering while visual scale stays fixed, and animation-driver/motor-state parity.
- The existing PlayMode physics suite still covers run/braking, jumping, sliding, vaulting, valid tags, timeout priority and repeated matches.
- Unity Linux x86_64 final player build: succeeded, zero errors; launched in GUI and displayed the actual chibi actors and valid match outcome. No C# exception observed in standalone log.
- Unity WebGL final player build: succeeded, zero errors; tested in real Chrome over localhost HTTP with the custom responsive template. The arena, chibi actors, HUD, scoring, role switching, and a completed match rendered.
- Both builds report the pre-existing optional Unity Pipeline warning regarding missing RuntimePipelineConfig. This is unrelated to character rendering.
- Chrome also produces WebGL INVALID_ENUM and browser audio-autoplay diagnostic warnings, without a reported gameplay C# exception in the checked console.
- The FBX is approximately 2.6 MB. Local ignored build folders are approximately 87 MB Linux and 11 MB WebGL. No detailed FPS/profiler benchmark or cross-browser matrix was performed.

## Known limitations, not hidden

1. **Crouch and shelf clearance:** The 1.8 m high gameplay capsule becomes 0.85 m when crouching. With the current reversible full-height visual scaling, the Blender crouch proof pose would still extend approximately 1.45 m above the floor, roughly 0.60 m above the crouched collider's top. The slide proof pose reaches approximately 1.28 m, about 0.43 m higher. This makes visual head clipping into low shelves possible even when the authoritative collision fits. Extreme crouch frames may also sink some foot geometry below floor level. These are artistic/kinematic limitations, not collision bypasses. A physically believable fit for the oversized rigid head cannot be obtained just by smoothly blending skin weights. Once final character scale is decided, refine crouch/slide with carefully placed joint motion and optional foot IK, and review obstacle clearance and collider implications in the same change. The existing physics and approved shape were not silently changed.
2. **Hands and rigid-shell joints:** Extreme elbow/knee positions may show overlaps or tiny separations between independent components. The rig supports correct pivots, but the actual armor has no continuous skin to deform.
3. **Animator controls:** FK only; no Blender IK controls or IK/FK switching yet. This is an intentionally stable, editable control foundation rather than a feature-complete film animation rig.
4. **Animation quality:** Clips are provisional in-place proof assets, not professionally polished run, parkour, or landing animations. Jump/slide/vault gameplay works through the unchanged motor, but dedicated live browser keyboard demonstrations for every move were not recorded.
5. **Visual hitbox overhang:** The arms and head visibly extend beyond the capsule radius. Hit detection remains capsule-to-capsule, by design; any resulting perceived-contact mismatch requires human playtesting once final scale/materials are selected.
6. **Performance:** Runtime rendering uses 17 skinned mesh parts per player. WebGL boot and match success are validated, but sustained GPU/CPU frame-time profiling has not been measured.
7. **Texture decisions:** Original images were missing; the new working rig uses neutral placeholders. Final production colors, materials and textures are intentionally undecided.

## Evidence and editability

Evidence images in this directory:

- neutral_front.jpg and neutral_side.jpg
- running.jpg, jumping.jpg, crouching.jpg, sliding.jpg, vaulting.jpg, turning.jpg, leaping.jpg
- unity_arena.png, browser_chibi.jpg, linux_chibi.jpg

Reproducibility scripts live under Chibi/rig_tools. They are included for audit and maintenance, not required to use the saved rig. Save and review modifications in Blender before replacing the FBX.

## RL compatibility

CHASE action definitions, ChaseAction, IChaseActionSource, ChaseMatch, ChaseObservationSensor and physics rules remain independent of the new Animator. A future headless or RL-controlled simulation can drive ChaseMotor without the visual prefab. No ML-Agents package, reward functions, neural network or training infrastructure was introduced.


## CHASE v0.2.1 animation and facing corrections (2026-10-11)

A player-visible audit reproduced three defects in v0.2 and fixed them without changing the approved geometry or the physics rules:

1. **Inactive animation driver.** ChaseMatch instantiated the ChibiVisual prefab before adding ChaseMotor, but ChibiVisualDriver.Awake immediately attempted to resolve ChaseMotor. Live Unity inspection confirmed the driver Motor reference was NULL, preventing all movement-state animation transitions. ChaseMatch now calls ChibiVisualDriver.BindMotor immediately after ChaseMotor.Initialize. The driver resets its Idle pose when a match resets. A regression test recreates this exact initialization order, confirms explicit binding, shows the running leg rotating significantly during playback, and verifies the state transitions.
2. **180-degree facing mismatch.** Blender's geometry faces positive X. The FBX importer maps this to negative X in the imported Unity model. The v0.2 visual prefab incorrectly rotated the model by -90 degrees, making the face point along the gameplay root's negative Z, while ChaseMotor.OrientVisual uses positive Z as forward. The prefab model child now rotates by +90 degrees. Unity PlayMode tests explicitly check the face-to-head direction against four movement vectors with dot products above 0.92. Meshes and motor rotations are not changed.
3. **Floating idle.** Blender's CHASE_Idle keyframes translated the pelvis by +0.015 to +0.028 source units. The validation action now keeps the pelvis's height exactly constant across all five keyframes. Idle retains only a very subtle head turn. Blender measured equal pelvis and both foot Z values at frames 1, 11, 21, 31 and 41. Unity tests sample the imported idle clip across its duration and verify grounded feet/pelvis within 0.003 Unity units.

Authoring direction check: the Blender +X face-offset dot product ranges from 0.81 (vault) to 0.999 (run) for the movement validation clips; the intentionally sideways turning pose has a lower dot product. Running keyframes put the left knee ahead and right knee behind at frame 1, reversing at frame 13, with opposite arm swing. These actions were not authored backward.

The new Blender file and FBX were re-exported from the existing approved rigid meshes, with Generic rigging unchanged. The chibi's full-size head still does not visually fit entirely within CHASE's 0.85 m crouched physics capsule. Dedicated animation polish, foot contacts during the running cycle, and shoulder/knee intersections are separate remaining tasks. No ML packages or mechanics were added.

Verified tests and build details for the correction are recorded in the v0.2.1 Git commit.
