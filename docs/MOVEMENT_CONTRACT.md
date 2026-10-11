# CHASE v0.4 production movement contract

> **Historical contract.** v0.5 completely replaces the upright leap with a headfirst Superman dive, including a solid full-body collider and prone/get-up states. Current authority is [v0.5 Superman-dive specification](chase-v0.5/README.md). The v0.4 measurements below are retained strictly for reference.

Status: implemented baseline for owner playtesting, **not** final feel/balance approval. This contract supersedes the active movement semantics in historical v0.1/v0.2 documents. [Character coordinates](CHARACTER_COORDINATE_CONTRACT.md) remain authoritative and unchanged.

## Explicit controls

| Input | Meaning |
|---|---|
| W/A/S/D | Camera-relative normalized, analog-compatible horizontal move direction |
| Left Shift, held | Grounded crouch and controlled low movement, **never** slide |
| Space (rising edge) | Vertical jump, **never** vault |
| Up Arrow (rising edge) | Physically launched forward leap, **never** jump or vault |
| Down Arrow (rising edge) | Momentum-only power slide, **never** automatic crouch |
| R / Enter / Tab / Escape | Restart / next completed round / switch role after round / pause-resume |

No sprint, separate walking, mouse aiming, auto climb, vaulting, ledge snapping, teleport, or collision bypass. Keyboard polling retains short taps until a physics step. Key release is required before repeating a committed action. Round reset and countdown clear pending action edges and block already-held launch keys. The WebGL responsive template focuses the game canvas and prevents Space/ArrowUp/ArrowDown page scrolling only when that canvas is focused. Ordinary physics may block any requested move.

## Shared action architecture

`IChaseActionSource.ReadAction(clock) -> ChaseAction { Move, Jump, Leap, Slide, Crouch } -> ChaseMotor.Simulate(action, dt, clock)` is the only ability pathway. Both competitor roles and the scripted NPC use this motor with identical scene tuning, dynamic Rigidbody, continuous collision detection and a solid CapsuleCollider. Animations never drive Rigidbody displacement (`Animator.applyRootMotion=false`). The fixed camera defines WASD. Future RL policies can implement an action source without changing gameplay authority. No ML-Agents package is installed.

Simultaneous actions use **Jump > Leap > Slide > held Crouch**. A pressed but ineligible higher-priority key does not turn into a lower-priority move. If currently crouched, release Shift, establish standing clearance and wait until upright before jumping or leaping. A blocked stand stays low rather than penetrating geometry. Buffered Jump: 0.08 s, coyote: 0.08 s. Neither leap nor slide queues for use after recovery.

## Motor tuning (meter, second, kg)

| Quantity | v0.4 value |
|---|---:|
| Fixed time step | 0.020 s |
| Mass / standing capsule radius / height | 70 kg / 0.35 m / 1.80 m |
| Measured-pose crouch and slide capsule heights | 1.55 m / 1.55 m |
| Run / crouch top speed | 6.0 / 2.0 m/s |
| Ground acceleration / braking / air acceleration | 20 / 24 / 4 m/s² |
| Gravity | 22 m/s² |
| Jump vertical launch | 6.3 m/s |
| Leap horizontal and vertical launch | 9.0 / 3.9 m/s |
| Leap landing recovery / recovery speed target | **0.50 s** / 2.0 m/s |
| Slide duration / deceleration / get-up recovery | 0.45 s / 8 m/s² / 0.20 s |
| Jump input buffer / coyote time | 0.08 s / 0.08 s |

Why 0.50 s instead of preliminary 0.35 s: the actual ten-second repeated-leap calibration reached 62.70 m at 0.35 s, versus 59.16 m for running. At 0.50 s, repeated leap attempts reached 57.11 m. The proposed baseline explicitly required a measured change if spam outperformed running; see [calibration](chase-v0.4/movement-calibration.md).

## Explicit finite states and eligibility

- `Standing`: grounded, upright, ground acceleration and ordinary braking.
- `Crouching`: held Shift (or overhead prevents standing), grounded, 2 m/s speed target, 1.55 m capsule, no boost.
- `Jumping`: upright and grounded/coyote eligible; launches exactly +6.3 m/s vertically, preserves planar momentum, then `Airborne` once airborne. Normal air acceleration is limited to 4 m/s²; no second jump.
- `Airborne`: ordinary falling or non-special air phase; contact determines landing and return to ground state.
- `Leaping`: upright and grounded launch, horizontal velocity set to `9 * FacingDirection`, vertical velocity +3.9, committed heading, **zero steering/repropulsion** until actual grounded contact. A wall can remove velocity; it is never restored during this action.
- `LeapRecovery`: begins only on grounded contact following leap, lasts 0.50 s independent of animation, applies controlled braking and up-to-2 m/s movement, blocks fresh jump/leap/slide. After recovery the standing-clearance test determines normal/crouched state.
- `Sliding`: explicit grounded down-arrow press even at zero speed, preserves current planar velocity and decelerates by 8 m/s², no steering/boost, fixed 0.45 s time limit; real wall collisions remain active. Walking off a ledge becomes a normal physical fall.
- `SlideRecovery`: 0.20 s grounded get-up, blocks committed moves; Shift requests crouch afterward, otherwise stand only when physically clear.

A higher-priority button pressed while invalid is discarded, not executed later. All timers, queued input, momentum, orientation, animations, action sources and countdown are reset for each round. Chaser/Evader physical tag eligibility is symmetric except for chase role; tag contact has priority over terminal timeout once the final physics collision callback has had a chance to run.

**Facing:** the motor tracks a smoothly interpolated planar velocity heading; leap uses the most recent motor visual heading, initially +Z. The motor neither targets obstacles nor instantly turns 180 degrees on a single reverse input. Visual rotation is presentation only. The launch heading commits until contact. A stopped character retains its last valid heading.

## Collision and rig contract

Actor origin is at the 1.8 m standing capsule midpoint; one visual child anchor is at local Y = -0.9 m. The Blender production source is `Chibi/Chibi_Rigged.blend`, -Y forward/+Z up with 1.8 m height and feet at origin; imported FBX +Z/+Y at rotation zero and scale one, Generic rig, 17 skinned shells, no imported mesh colliders. Full character root is never scaled for crouch. The effective low envelope is intentionally **1.55 m rather than the unverified 0.85/1.1 m proposals**. The source's 0.60 to 0.70 m horizontal visible limb/head reach can exceed the 0.35 m capsule radius, and transitional crouch/slide frames still have limited foot-floor and clearance mismatch. These are measured limitations, not claims of perfect full-body collision: see [mesh audit](chase-v0.4/rig-clearance.md).

## Observation schema (66 floats)

`ChaseObservationSensor.FillPolicyVector(self, opponent, role, remaining, target)` writes exactly 66 bounded values to a reusable buffer. Indices: 0-2 normalized own XYZ; 3-5 own XYZ velocity (9 m/s horizontal capacity); 6-8 opponent relative XYZ; 9-11 opponent relative XYZ velocity (18 m/s horizontal); 12 grounded; 13 jump available; 14 leap available; 15 slide available; 16 standing clearance; 17 role signed; 18 normalized remaining round time; 19 state enum / 7; 20-21 planar facing; 22 recovery seconds / 0.50; 23 committed indicator; 24 collider height / 1.8; 25 planar speed / 9. Indices 26-65 are five consecutive eight-direction distance channels: waist-height geometry, low geometry, overhead geometry, hurdles, solid walls; each normalized to [0,1] at 4 m detection range. No CanVault flag or category exists. Geometry sensing never changes action physics.

## Geometry and editing restrictions

The competitive field stays 12 x 12 m and uses a fixed camera. Jumpable barriers are independent 0.35 m-high colliders, while tall 2.3 m barriers require routing around. Supported low passages have underside 1.64 m, 1.84 m between support posts, and 0.55 m canopy depth. The verified slide travels 1.88 m from 6 m/s; the verified jump reaches 0.84 m apex. Thus a single shelf is deliberately **not** simultaneously jumpable and crouchable. The 25 x 26 m `MovementGym.unity` is separate and excluded from production build settings. `Chase > Rebuild Arena` prompts for confirmation and must reproduce the current explicit movement geometry if used.

Additional playtest review is required for running precision, path choice, the scripted bot around low roofs, head/limb horizontal collision mismatch, crouch entry silhouette, and key behavior at varied Chrome refresh rates. Do not call this tuning artistically approved without owner testing.
