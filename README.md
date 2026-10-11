# CHASE

Single-player, keyboard-only, physics-based 3D chase-tag game, Unity **6000.3.26f1** (Unity 6.3 LTS), browser-first and also buildable for Linux. The fixed-angle camera shows a 12 x 12 m arena with two differently colored chibi competitors. Blue evades for 20 seconds; Red wins by ordinary Rigidbody/collider contact. A three-second countdown precedes each round.

**Authoritative contracts:** [v0.4 movement](docs/MOVEMENT_CONTRACT.md) and [character coordinate/source](docs/CHARACTER_COORDINATE_CONTRACT.md). Detailed [physics calibration](docs/chase-v0.4/movement-calibration.md) and [evaluated mesh clearance audit](docs/chase-v0.4/rig-clearance.md). Tuning remains preliminary until the owner playtests it.

## Play and controls

Open `Assets/Scenes/SampleScene.unity` in Unity and press Play, or launch a served WebGL build in Chrome.

| Input | Explicit action |
|---|---|
| W, A, S, D | Camera-relative movement, accel/braking determine speed |
| Left Shift (hold) | Low crouched movement, 2 m/s top speed |
| Space | Normal vertical jump, buffered rising edge |
| Up Arrow | Committed, low **forward leap**, 9 m/s launch |
| Down Arrow | Momentum-based **power slide**, even from rest |
| R | Restart the round |
| Enter | Start the next round after a result |
| Tab | Switch human role between rounds |
| Escape | Pause/resume |

There is no contextual vault, automatic climb, speed-triggered action conversion, mouse aiming, or RL agent. In Chrome the focused game canvas prevents the movement keys from scrolling the page. To regain focus, click the canvas. A leap's 0.50-second grounded recovery deliberately prevents repeated leap spam from exceeding sustained running over 10 seconds in the measured fixture.

## Arena and calibration gym

Production: `Assets/Scenes/SampleScene.unity`, an enclosed 12 x 12 m arena with tall solid barriers, pillars, genuinely low 0.35 m hurdles and two supported **1.64 m-clearance** underpasses. Running, jumping, leaping, crouching and sliding are independent moves; geometry cannot switch one into another. Both roles use the same physical capabilities. The two low passages are not jumpable shelves. The simple scripted NPC uses standard `ChaseAction` and avoids solid obstacles, with no special collision exemptions.

Testing only: `Assets/Scenes/MovementGym.unity` (excluded from build scenes). `Chase > Create Separate Movement Gym` regenerates it on explicit request. It has distance markers, jumpable hurdle, blocking wall, open leap lane, short slide roof and longer crouch passage. An Editor-only on-screen readout displays state, speed, grounding, recovery, capsule and action travel. `Chase > Rebuild Arena` **overwrites** the production scene and materials only after a confirmation dialog; back up authored content first.

## Shared motor and observations

`IChaseActionSource -> ChaseAction { Move, Jump, Leap, Slide, Crouch } -> ChaseMotor` is used for player, bot and future policy controllers alike. Real movement is always authoritative in the dynamic `Rigidbody`; `ChibiVisualDriver` only selects Animator actions and does not apply root motion. `ChaseMatch` binds animation to the motor explicitly after actor construction. The normalized policy vector is now **66 bounded floats** (schema in the movement contract); RL training is intentionally deferred.

Physics baseline: standing 1.80 m capsule, 0.35 m radius, crouch/slide 1.55 m collider based on real chibi shell measurements, run 6 m/s, jump 6.3 m/s upward, leap 9 m/s horizontal and 3.9 m/s upward, gravity 22 m/s², slide deceleration 8 m/s². Tests recorded jump apex 0.84 m, leap distance 3.24 m and 6 m/s slide distance 1.88 m. See measured target-versus-outcome table for all calibration details and remaining visual limitations.

## Character source and build workflow

**One authoritative Blender asset:** `Chibi/Chibi_Rigged.blend` (Blender -Y forward/+Z up, 1.80 m tall, feet at origin). The working 20-bone Generic rig uses 17 rigid skinned shells with **11 persistent animation actions**; approved raw meshes remain unchanged. Unity FBX/prefab face +Z, scale one and identity rotation. The actor centers its capsule at 0.9 m and places the visual child at -0.9 m. The oversized head/limb shell is wider than the capsule and some transition poses still clip floor/roof; these limitations are quantified, not concealed.

Validate before export:

```bash
python3 Chibi/rig_tools/check_source_layout.py
blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/validate_asset.py
# First back up the existing production FBX and .meta.
CHASE_ALLOW_REGENERATE=1 blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/export_unity_fbx.py
```

Production export and other rig guidance: [Chibi rig tools README](Chibi/rig_tools/README.md). Never reconstruct the character from historical OBJ snapshots, restore `armor.blend`, or apply obsolete +X orientation workarounds. Blender `.blend1` backups belong outside the active source tree.

## Testing and builds

Run EditMode and PlayMode in Unity's Test Runner. The new PlayMode calibration fixture uses actual `Physics.Simulate(.02)`, checks action precedence, momentum, wall collisions, standing clearance and measured travel. Linux output is local and Git-ignored; WebGL uses `Assets/WebGLTemplates/ChaseResponsive` and **must** be served over HTTP with the correct `Content-Encoding: gzip` for compressed resources. The Unity Pipeline Editor plugin enables MCP access and is not needed in shipped gameplay. Neither ML-Agents nor an active vault mechanic is installed.

Historical v0.1/v0.2 reports in `docs/` describe superseded mechanics, including the former contextual vault. Do not use their 59-float policy schema, 0.85 m crouch capsule, or old input mapping as production contracts.
