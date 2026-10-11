# CHASE v0.5.1 — reliable ballistic Superman dive

**This document supersedes the v0.5 delayed-dive mechanics.** The user's real-time report was that **W + Up** sometimes looked like an ordinary jump, sometimes jumped vertically before snapping to the Superman pose, and WASD seemed to steer a supposedly committed projectile. All three symptoms had a single design-level problem: the v0.5 motor launched in an upright capsule and attempted to switch to a horizontal body after a fixed 0.14-second delay. A failed switch silently fell back to the jumping animation. This was neither predictable nor physically legible.

## Fixed action contract

- **W + Up, or Up from a standing idle:** **the current WASD direction is the aim at takeoff**, including simultaneous keypresses from idle. Without WASD, the current facing supplies the aim. This direction then locks for the entire airborne flight. The body clearance check occurs **before** the vertical/horizontal velocity is assigned. When the 2.30 × 1.20 × 0.95 m solid non-trigger full-body BoxCollider fits at the actor's current location, its orientation, shape, and 9.0 m/s forward / 5.2 m/s upward launch velocity are committed **atomically in the very first physics step**. There is no intermediate upright jumping phase. The animation also selects the real `Leap` state immediately and crossfades into its headfirst takeoff frames in 0.04 seconds.
- **No delayed shape-morph:** if the full body is blocked by a nearby wall, shelf or opponent, Up is **rejected at takeoff**: no vertical launch, no fake ordinary Jump, no late switch halfway through descent, no bypass of collision. The existing key release/rising-edge semantics still apply. Stand clear and press Up again; pressing W while next to an obstacle cannot teleport the full body through it.
- **Real ballistic flight:** the Rigidbody receives one velocity impulse equivalent, and afterward the motor preserves the actual physics horizontal velocity. W/A/S/D, Up, Space, Shift, and Down **cannot steer, re-accelerate, slide, reverse, or spawn a second jump while the diver is airborne**. Only gravity (22 m/s²) and PhysX contact can change the projectile trajectory or velocity. A wall can slow or halt it; there are no hidden rebounds, speed resets, or steering.
- **Landing/get-up commitment:** impact enters `DiveProne` and applies the existing 0.40 s mandatory brake; if the player is underneath a shelf, they may **press forward (W relative to their committed heading)** to crawl slowly out after that impact phase. Sideways/reverse keys cannot steer the prone capsule. The 0.35 s `LeapRecovery` get-up phase now also **ignores directional input**, avoiding the impression of gliding around while still in a landing pose. Full running control returns only when recovery completes.
- **Same obstacle physics and role parity:** the long full-body collider still physically hits shelves, side supports, walls, opponents, and ground. The original jump, slide, crouch, role/tag rules, unchanged 66-float observation vector length and 12 genuine Blender animations are preserved. The authoritative FBX/prefab and face-forward rig contract are unchanged.

The solution intentionally retains the full-body **solid BoxCollider** rather than using the standing 0.35 m-radius capsule throughout the dive: the latter would let the oversized head, arms, and body visually pass through shelves without equivalent physical contact. The critical simplification is **one physically committed collider/velocity choice at launch**, not a midair shape change.

## Regression checks (actual Unity PhysX, 0.02-second steps)

Fresh independent Unity 6000.3.26f1 command-line Test Runner results, October 11, 2026:

| Suite | Passed | Failed |
|---|---:|---:|
| EditMode | 15 | 0 |
| PlayMode | 55 | 0 |

The expanded PlayMode suite includes a direct motor-input regression for simultaneous WASD+Up from completely idle: the dive immediately aligns to the requested direction, not the outdated idle facing; opposite-direction keys during flight have no effect. It specifically asserts that **holding W and Up together enables the long horizontal collider on the launch frame**, immediately enters `Leaping`, produces the 9 m/s heading-locked launch and a positive vertical velocity, and never enters `Jumping`. Separate repeated-physics tests alternate A/D/S/W, Space, Slide and crouch while airborne and assert exactly **zero sideways acceleration or changes to heading/forward speed**. A deliberately blocked wide body must never receive upward jump velocity or later morph into a dive. Existing tests verify real shelf traversal, wall and side-post collision, grounded prone/get-up, tag contact, the exact animation state, and mesh envelope.

Actual physics/human judgment remain distinct: simulated collision tests do not fully determine whether the athlete's posing or camera angle feels exciting to a person.

## Real WebGL inspection

The isolated WebGL Movement Gym (`http://127.0.0.1:8766`) provides **G** to stage the 3 m shelf approach, **Up** for an aligned dive, **W + Up** for an immediately committed forward projectile, and **V** to toggle a smooth *inspection-only* low, side-on camera angle. The original production camera is not modified. The gym is still the real `ChaseMotor`/`ChibiVisualDriver`, not a special animation-only simulation.

Reproducible direct Chrome WebGL inputs and recorded screenshots are in [`verify_projectile_browser.py`](verify_projectile_browser.py) and [`evidence/`](evidence/). The script holds W, presses Up, issues opposing movement keys during the flight and records takeoff, arc, impact and recovery frames. It waits for a rendered gameplay frame **after** the Unity splash and rejects browser console errors or page scrolling. Physics tests, not moving-camera screenshot differences, establish exact midair steering behavior.

Linux and production WebGL builds use the repo's reproducible `ChaseReleaseBuild` commands if the interactive Editor or connected test interface is unavailable. All outputs under `Builds/` are intentionally Git-ignored; refresh the browser after rebuilding to avoid inspecting an older Unity canvas instance.

### Live animation-orientation verification (not a screenshot guess)

The browser-side diagnostic in the isolated Movement Gym reports the **actual imported FBX Animator state and actual world-space joint axis** while the physics body is in flight. The final Chrome build displayed `VISUAL: Leap`, `CLIP LEAP: True`, `HEAD AXIS UP: 0.24`, `HEAD AXIS FWD: 0.97`, `STATE: Leaping` and `BODY: PRONE BOX` during real movement. Thus the animation is genuinely headfirst and mostly horizontal in 3D; a camera looking along the athlete's travel direction can foreshorten this into a vertical-looking silhouette. The `V` inspection view uses a side-on camera **perpendicular to the original camera-relative W heading**, while preserving the gym's original control basis so changing the camera cannot itself steer the character. The production fixed-angle camera has not changed.

This is additionally guarded in Unity by `SupermanClipReallyTiltsHeadAndTorsoHorizontally`: five intermediate frames must have the head-to-pelvis axis at least 93% forward and less than 36% vertical. Dedicated control tests verify that W+Up launches a full solid projectile in the first physics frame and subsequent A/D/S/W/Jump/Slide spam cannot alter forward or sideways velocity before physical impact. A rejected takeoff now shows a 0.70-second, informational `DIVE BLOCKED — CLEAR SPACE FOR YOUR BODY` overlay rather than an unannounced fallback jump.

## Final tested release and audit evidence

Verified against the **final simultaneous-WASD-and-Up takeoff** revision, not a previous cached runtime:

- [Machine-readable release checks, both Unity test suites, all three build results and browser checks](evidence/verification-summary.json)
- [Final real Chrome 8-stage, low-angle headfirst flight/impact/get-up sequence](evidence/sideview/release-projectile-sequence.png)
- [Gym Chrome's actual input, focus, and render readiness report](evidence/sideview/browser-superman-verification.json)
- [Production Chrome input/focus/console report](evidence/production/browser-production-verification.json)
- [Source for directly dispatching W+Up and A/D/S during the actual browser flight](verify_projectile_browser.py)

The isolated gym control **V** changes only the diagnostic viewing angle (to see the character side-on); the keyboard aim basis stays fixed. `G` stages a repeatable shelf approach. A normal production match retains its fixed-angle arena camera and role/tag rules. **Both browser windows were reloaded with cache disabled after the final rebuild**. The independently rebuilt Linux player also launched with zero matching `Exception`/`MissingReference`/`Failed to load` log errors.

All three final player builds are reproducible via `ChaseReleaseBuild.WebGLGym`, `ChaseReleaseBuild.WebGLProduction`, and `ChaseReleaseBuild.LinuxProduction` using Unity's batchmode command. The stored results are zero build errors and one existing Unity Pipeline warning per build. Local Unity Test Runner XML logs are under `/tmp/chase-v051-{playmode,editmode}-aimfinal.xml` on the development workstation; the durable compact summary is versioned above.
