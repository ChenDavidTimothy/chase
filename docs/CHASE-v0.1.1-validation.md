# CHASE v0.1.1: physics and gameplay validation

## Scope and baseline

- Repository: `ChenDavidTimothy/chase`.
- Baseline branch: `feature/chase-v0.1-playable`.
- Baseline HEAD: `6b2171145c71a215e563f2f83cdf43fbff3d7e35`.
- Fix branch: `fix/chase-v0.1.1-physics-validation`.
- Unity Editor: `6000.3.26f1`.
- Baseline working tree: clean; original **8/8 EditMode tests passed**.
- The existing `Assets/Scenes/SampleScene.unity`, packages, and movement constants were preserved.
- Baseline PlayMode investigation initially produced **3 failures** with real `Physics.Simulate` steps: wall false-grounding, held-jump repetition, and vault crossing. During WebGL verification two additional runtime/visual defects were reproduced and corrected.

## Confirmed defects, corrections and evidence

| Component / method | Confirmed defect | Correction | Verification |
| --- | --- | --- | --- |
| `ChaseMotor.DetectGround` | Airborne collider contacting a tall wall falsely registered as grounded; original test reproduced it at a wall-normal side contact. | Downward `SphereCastNonAlloc`, upward-normal threshold 0.65, self/opponent exclusions; valid support only. | `WallContactCannotRefreshGround`, `GroundedJumpCannotRefreshAgainstPillar`, coyote and landing tests. |
| `ChaseMotor.Simulate` jump queue | Continuous `JumpOrVault=true` generated **5 distinct takeoff impulses in 100 physics steps**. | Rising-edge latch in the motor itself, cleared on restart; buffered new presses only. Keyboard maintains button state and queues short taps. | `ContinuousJumpButtonDoesNotAutoRepeat`, `ShortJumpPressCanBeBufferedBeforeLanding`, `CoyoteWindowPermitsOneJumpThenExpires`. |
| `ChaseMotor.TryBeginVault` / Vaulting | First physical crossing test entered Vaulting but stopped against the roof face, at approximately z=-0.625 rather than traversing it. | Frictionless, solid shared capsule material; actual slab exit distance; swept path checks; collision-preserving, acceleration-limited forward commitment and recovery. | Complete traversal tests at 2.5, 4 and 6 m/s from both directions; blocked landing and physical tag while vaulting. |
| `ChaseMotor.Simulate` vault timeout | Timeout was calculated before `vaultEntrySpeed` was assigned, which could yield a non-finite commitment duration. | Assign entry speed before the duration calculation; bounded fallback for interrupted vaults. | Compilation, PlayMode traversal, blocked vault tests. |
| `ChaseMotor.HasStandingClearance` | The previous query explicitly ignored other `ChaseMotor` colliders when checking expansion, admitting unsafe standing overlap. | Non-alloc overlap and `Physics.ComputePenetration` against real geometry **including the opponent**. | Under-shelf clearance, capsule-overhead clearance, crouch/release tests. |
| `ChaseMatch.CreateCompetitor` | Unity's deferred destruction briefly left extra visual capsule/sphere colliders enabled. In an immediate vault-contact integration fixture, this prevented vault activation until a frame passed. | Disable primitive visual colliders immediately before deferred destruction. The actor's actual CapsuleCollider remains solid. | `PhysicalTagRemainsValidDuringVault` passes without a frame delay. |
| `ChaseCameraRig.RecalculateFraming` | The 0.8 minimum aspect ratio incorrectly modeled narrow viewports. | Calculate using actual aspect down to an explicit small safety floor. | `FixedCameraFramesEntireArenaAcrossAspectRatios` at 16:9, 16:10, 4:3, 1:1 and 1:2. |

## Refinements (not necessarily reproduced defects)

- `IChaseActionSource`: independently assignable controllers for either competitor; both are constrained by `ChaseMotor`. No ML-Agents installation or training.
- `ChaseMatch`: per-round physics clock for consistent action timing and deterministic manual physics simulations; sources reset on a new round.
- `ChaseObservationSensor`: 40 ordered, normalized obstacle samples (waist, low, overhead, vaultable and hurdle in eight directions), plus a 59-float reusable normalized policy vector.
- `ChaseBot`: reuses fixed pathfinding arrays and physics-query buffers, avoiding per-plan array allocation; no new tactical advantages.
- `ChaseCameraRig`: reuses raycast buffers and avoids per-frame arrays in occlusion checks. Obstacle collision is never disabled.
- `ChaseSceneBuilder`: interactive confirmation and a save prompt before regenerating the scene or CHASE material assets; the committed scene was not regenerated.
- Visual colliders disabled immediately; one shared nonbouncy, low-friction physical capsule material is used for both roles.

## Test results

- Baseline: eight EditMode tests passed before modifications.
- Final EditMode: **10 passed, 0 failed**.
- Final PlayMode: **25 passed, 0 failed**. These tests call `Physics.Simulate(0.02f)` and use real Rigidbody motion and contacts.
- Full vault traversal: **24 successful trials** (two shelf locations, two approach sides, three speeds, with physically valid landing).
- Underpass crouching: eight side/location trials. Sliding traversed both shelves in both directions.
- Collision tests: actual physical tag, countdown rejection, no tag across a solid separating barrier, final-step tag priority, tags during slide/vault.
- Additional cases: buffered jump, held-button semantics, coyote-window expiration, momentum and acceleration bounds, standing and collision clearance, vault landing obstruction, camera framing, normalized reusable observation buffers.
- Test names and implementation: `Assets/Tests/EditMode/ChaseRuleTests.cs`, `Assets/Tests/PlayMode/ChasePhysicsTests.cs`.

### Deterministic automated matches

The tests regenerate a **fixed test fixture** matching the committed arena's dimensions and obstacle coordinates; they do not introduce procedural game levels. Spawn pairs use the production seed formula.

| Scenario | Matches | Chaser tags | Evader timeouts | Mean duration | Two-second stuck windows |
| --- | ---: | ---: | ---: | ---: | ---: |
| Scripted Chaser vs stationary human-controlled Evader | 100 | 100 | 0 | 0.58 s | 0 |
| Scripted Chaser vs moving deterministic Evader action provider | 100 | 100 | 0 | 1.90 s | 0 |
| Scripted Evader vs a moving straightforward human Chaser action provider | 100 | 100 | 0 | 1.18 s | 0 |

A preliminary scripted-Evader test with a **stationary** Chaser produced 100 legitimate Evader timeouts and 816 nearly stationary two-second windows. That was not evidence of broken navigation: the pursuer never approached, so the scenario was replaced with a moving-pursuer stress test. Both data points are retained, rather than hidden.

All final stress trials resolved with a valid winner. The test suite checks finite positions/velocities and player confinement. These numbers measure functional correctness, **not** difficulty balance or a strong AI opponent.

## Builds and visual checks

**Final Linux standalone:** Unity 6.3.26f1 successfully built
`Builds/ChaseLinux/Chase.x86_64`, approximately 86 MB installed in the
ignored local build directory; **0 build errors, 1 known Pipeline warning**.
Launched the **actual player** at 1280x720 using X11 and captured the
running window through GUI screenshots. Verified: arena and both capsules
visible; a red physical tag ends the game; Tab changes the human role;
Enter starts round 2; timer progresses and scripted Evader moves. No C#
exception/error appeared in `/tmp/chase-standalone.log`.
Unity logged missing-display-DPI messages, which were informational here.

**Final WebGL:** Unity successfully built
`Builds/ChaseWebGL`, around 11 MB on disk; **0 build errors, 1 known
Pipeline warning**. Served locally at `http://127.0.0.1:8765/` with
correct `Content-Encoding: gzip` and tested in real Google Chrome using
the GUI connection. The first build **failed at runtime**, despite building:
`SphereCollider` was stripped in WebGL and runtime creation of the
decorative facing sphere raised a null-reference exception. Replacing that
visual sphere with a small cube (which uses the included `BoxCollider`,
disabled immediately) fixed the failure. The later build loaded the arena,
both capsules, countdown and a complete match in Chrome.

A second **browser-only visual defect** was confirmed: Unity's built-in
960x600 HTML canvas was centered with negative top coordinates on this
high-DPI, short viewport, hiding anchored HUD panels. A custom
`Assets/WebGLTemplates/ChaseResponsive` template now fills the viewport,
automatically focuses its keyboard-accessible canvas, and leaves the
fixed-angle Unity camera unchanged. The gameplay HUD controls were moved
beneath the score panel to ensure they remain visible. Browser screenshots
confirm the role, score, timer, controls and arena simultaneously.

**Chrome keyboard verification:** Tab switched human Evader to Chaser
between rounds, Enter began the next round, R restarted, and a one-second
physical W key hold moved the human-controlled capsule visibly. A real
Evader-timeout outcome updated the session score. No gameplay C# exception
recurred after the visual collider correction. Chrome did log WebGL
`INVALID_ENUM` diagnostics and an autoplay `AudioContext` warning; the
game still rendered, moved and resolved rounds. Browser-specific vault/slide
maneuvers have not each been manually reproduced, although their identical
motor is covered by PlayMode physics tests.

**Visual evidence:** a 1280x720 running standalone screenshot is included
as [CHASE-standalone-gameplay.jpg](CHASE-standalone-gameplay.jpg). Actual
Unity Editor Game view and real Chrome screenshots were inspected via GUI.

The sole common build warning is the unchanged Unity Pipeline package's
lack of a `RuntimePipelineConfig` asset. Editor MCP works, and the
standalone players do not need Pipeline networking. This warning is not a
gameplay compile failure.

Neither a detailed FPS profile nor a cross-browser performance matrix has
been completed. High-frequency camera, pathfinding and observation queries
were revised to reuse buffers where practical; direct performance metrics
are a future task.

## Remaining limitations and next-stage criteria

- The scripted opponent is intentionally simple and very easy for its own direct-pursuit counterpart to catch. Competitive challenge against experienced human players is **not validated**.
- Physics repeatability is tested at the specified timestep, not bitwise cross-platform determinism.
- Automated route tests use an isolated geometry fixture copied from the shipped scene. The actual authored scene was inspected and played in Unity Editor, Chrome WebGL and native Linux, but each physical shelf route was not individually executed by keyboard in all three runtimes.
- No RL runtime, ML-Agents, training, networking, multiplayer, or new gameplay abilities are included.
- Chrome WebGL boot, WASD, round transition, score, timer and HUD were observed in a real browser. Browser keyboard jump/slide/vault timing was not each demonstrated manually; PlayMode physics verifies the motor.
- The custom responsive browser template is selected through `PlayerSettings.WebGL.template = PROJECT:ChaseResponsive`. No Unity gameplay package was added.
- Both final build outputs are Git-ignored and can be regenerated locally.

## Readiness assessment

- **Physical stability for RL integration:** appropriate for the next controlled
  integration step, with normal-validated support, edge-triggered jump and
  demonstrated obstacle traversal. Continue exploit-oriented testing during RL work.
- **Repeated automated simulation:** suitable for controlled repeated matches;
  300 fixed-seed matches completed. Actual parallel ML training infrastructure
  is intentionally absent.
- **Human playtesting:** ready as a capsule prototype in Unity and native Linux;
  challenging AI balance and human ergonomics remain unproven.
- **Browser compatibility testing:** real Chrome startup, keyboard input, HUD,
  and match outcomes verified after template and collider corrections. Other
  browsers and repeated real browser parkour inputs remain to be assessed.

No claim is made of rare-edge-case exploit immunity or final competitive
gameplay balance.
