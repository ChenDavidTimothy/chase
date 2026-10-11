# CHASE

Single-player, keyboard-only, physics-based 3D chase-tag game, Unity **6000.3.26f1** (Unity 6.3 LTS), browser-first and also buildable for Linux. The fixed-angle camera shows a 12 x 12 m arena with two differently colored chibi competitors. Blue evades for 20 seconds; Red wins by ordinary Rigidbody/collider contact. A three-second countdown precedes each round.

**Authoritative current movement:** [v0.5.1 reliable ballistic Superman dive](docs/chase-v0.5.1/README.md). Superseded: [v0.5 initial dive](docs/chase-v0.5/README.md). Superseded baseline: [v0.4 movement](docs/MOVEMENT_CONTRACT.md) and [character coordinate/source](docs/CHARACTER_COORDINATE_CONTRACT.md). Historical [v0.4 physics calibration](docs/chase-v0.4/movement-calibration.md) and [v0.4 mesh audit](docs/chase-v0.4/rig-clearance.md) predate the Superman replacement. Tuning remains preliminary until the owner playtests it.

## Play and controls

Open `Assets/Scenes/SampleScene.unity` in Unity and press Play, or launch a served WebGL build in Chrome.

| Input | Explicit action |
|---|---|
| W, A, S, D | Camera-relative movement, accel/braking determine speed |
| Left Shift (hold) | Low crouched movement, 2 m/s top speed |
| Space | Normal vertical jump, buffered rising edge |
| Up Arrow | **Headfirst Superman dive**, 9 m/s launch, long horizontal solid collider, chest impact and committed get-up |
| Down Arrow | Momentum-based **power slide**, even from rest |
| R | Restart the round |
| Enter | Start the next round after a result |
| Tab | Switch human role between rounds |
| Escape | Pause/resume |

There is no contextual vault, automatic climb, speed-triggered action conversion, mouse aiming, or RL agent. In Chrome the focused game canvas prevents the movement keys from scrolling the page. To regain focus, click the canvas. The Up Arrow has fully replaced the old upright forward leap. The dive is a committed ballistic maneuver: solid horizontal collision and 9 m/s launch happen on the **same takeoff physics frame**, with no upright jump fallback or delayed morph. W/A/S/D cannot steer it in the air; the chest-first impact and get-up remain mandatory. It is slower than sustained running when spammed in the measured 10-second fixture.

## Arena and calibration gym

Production: `Assets/Scenes/SampleScene.unity`, an enclosed 12 x 12 m arena with tall solid barriers, pillars, genuinely low 0.35 m hurdles and one supported **1.64 m-clearance** underpass and one **1.30 m-high airborne Superman-dive shelf slot** over a solid 0.55 m sill. Running, jumping, headfirst diving, crouching and sliding are independent moves; geometry cannot switch one into another. Both roles use the same physical capabilities. The northeast roof is crouchable; the southwest shelving has an elevated opening that requires a correctly aligned headfirst dive. The simple scripted NPC uses standard `ChaseAction` and avoids solid obstacles, with no special collision exemptions.

Testing only: `Assets/Scenes/MovementGym.unity` (excluded from build scenes). `Chase > Create Separate Movement Gym` regenerates it on explicit request. It has distance markers, jumpable hurdle, blocking wall, open dive lane, short slide roof, longer crouch passage and dedicated elevated dive shelf. Press **V** there to toggle a low side-on inspection view without changing the production camera. Press **G** in this gym to stage a repeatable 3 m aligned shelf approach, then Up Arrow to execute the real move. An on-screen readout in Editor and WebGL displays state, speed, grounding, recovery, active physical collider and action travel. `Chase > Rebuild Arena` **overwrites** the production scene and materials only after a confirmation dialog; back up authored content first.

## Shared motor and observations

`IChaseActionSource -> ChaseAction { Move, Jump, Leap, Slide, Crouch } -> ChaseMotor` is used for player, bot and future policy controllers alike. Real movement is always authoritative in the dynamic `Rigidbody`; `ChibiVisualDriver` only selects Animator actions and does not apply root motion. `ChaseMatch` binds animation to the motor explicitly after actor construction. The normalized policy vector is now **66 bounded floats** (schema in the movement contract); RL training is intentionally deferred.

Physics baseline: standing 1.80 m capsule, 0.35 m radius, crouch/slide 1.55 m collider based on real chibi shell measurements, run 6 m/s, jump 6.3 m/s upward, Superman dive 9 m/s horizontal and 5.2 m/s upward, gravity 22 m/s², slide deceleration 8 m/s². Tests recorded jump apex 0.84 m, unobstructed dive travel roughly 6.3 m before get-up and 6 m/s slide distance 1.88 m. See measured target-versus-outcome table for all calibration details and remaining visual limitations.

## Character source and build workflow

**One authoritative Blender asset:** `Chibi/Chibi_Rigged.blend` (Blender -Y forward/+Z up, 1.80 m tall, feet at origin). The working 20-bone Generic rig uses 17 rigid skinned shells with **12 persistent animation actions**; approved raw meshes remain unchanged. Unity FBX/prefab face +Z, scale one and identity rotation. The actor centers its capsule at 0.9 m and places the visual child at -0.9 m. The oversized head/limb shell is wider than the capsule and some transition poses still clip floor/roof; these limitations are quantified, not concealed.

Validate before export:

```bash
python3 Chibi/rig_tools/check_source_layout.py
blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/validate_asset.py
# First back up the existing production FBX and .meta.
CHASE_ALLOW_REGENERATE=1 blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/export_unity_fbx.py
```

Production export and other rig guidance: [Chibi rig tools README](Chibi/rig_tools/README.md). Never reconstruct the character from historical OBJ snapshots, restore `armor.blend`, or apply obsolete +X orientation workarounds. Blender `.blend1` backups belong outside the active source tree.

## Animation consistency across Editor and WebGL

The current production Animator must reference **real FBX motion clips**, never Unity's transient `__preview__` clips. Twelve current clip frame ranges are validated before every Player build, with looping enabled only on Idle, Run, CrouchWalk and the prone-impact hold (DiveProne). After each intentional Blender animation re-export, use **Chase > Repair Production Character Animation Imports**, then **Chase > Validate Production Character Animation** and run both Unity test suites. `ChibiVisualDriver` reconciles the Animator's actual state with the physics motor every frame, so moving after an earlier Slide cannot leave the visual stuck in a low pose. Both forearm hinges bend in the character's forward direction (-Y in Blender, +Z in Unity); the Blender validator checks whole and fractional frames throughout the new dive. The FBX exporter bakes the new clips at 1/8-frame intervals to avoid brief interpolated elbow reversals.

See the fresh build/root-cause evidence in [`docs/chase-v0.4/animation-repair-report.md`](docs/chase-v0.4/animation-repair-report.md). A separately built, locally served WebGL Movement Gym permits close-up browser/Editor animation comparison without chase-tag interruptions.

## Testing and builds

Run EditMode and PlayMode in Unity's Test Runner. The new PlayMode calibration fixture uses actual `Physics.Simulate(.02)`, checks action precedence, momentum, wall collisions, standing clearance and measured travel. Linux output is local and Git-ignored; WebGL uses `Assets/WebGLTemplates/ChaseResponsive` and **must** be served over HTTP with the correct `Content-Encoding: gzip` for compressed resources. For deterministic local browser testing, serve WebGL with no-cache, gzip-aware headers:

```bash
python3 Tools/serve_webgl.py --build Builds/ChaseWebGL --port 8765
# separate optional test build:
python3 Tools/serve_webgl.py --build Builds/ChaseAnimationGymWebGL --port 8766
```

**Reload or reopen the browser after every build.** An existing Unity WebGL runtime remains the older build until its page reloads, even if the files on disk are newer. The provided server prevents browser reuse of stale `.data.gz` / `.wasm.gz` responses on reload.

The Unity Pipeline Editor plugin enables MCP access and is not needed in shipped gameplay. Neither ML-Agents nor an active vault mechanic is installed.

Historical v0.1/v0.2 reports in `docs/` describe superseded mechanics, including the former contextual vault. Do not use their 59-float policy schema, 0.85 m crouch capsule, or old input mapping as production contracts.
