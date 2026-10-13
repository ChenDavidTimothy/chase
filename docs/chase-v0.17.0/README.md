# CHASE v0.17.0: Competitive athletic movement rebalance

## Intent

Make CHASE feel like a fast, deliberate 20-second athletic chase rather than a series of long recoveries. Reward clean obstacle traversal and skillful cuts. Do not turn obstacles into deterministic, single-frame button puzzles. Preserve real non-trigger physics, a single authoritative Blender source, existing actions, third-person camera, role-based spawn sides, competition round flow, and public free-roam Practice.

The rebalance uses **the same ChaseMotor and ChaseTuning in Arena 01, public Practice, and technical Movement Gym**. It introduces no power-ups, auto-aim, new movement primitives, collision cheats, position snapping, or runtime procedural difficulty.

## Exact movement change

| Value | v0.16.1 | v0.17.0 |
|---|---:|---:|
| Top run speed | 6.0 m/s | 6.0 m/s |
| Ground acceleration | 20 m/s² | 36 m/s² |
| Ground braking/reversal | 24 m/s² | 44 m/s² |
| Air acceleration | 4 m/s² | 5 m/s² |
| Vertical jump launch | 6.3 m/s | 7.0 m/s |
| Jump press buffer | 0.08 s | 0.13 s |
| Ground coyote time | 0.08 s | 0.12 s |
| Dive horizontal launch | 9.0 m/s | 8.2 m/s |
| Dive vertical launch | 6.8 m/s | 7.25 m/s |
| Prone phase following dive | 0.40 s | 0.20 s |
| Solid-collider get-up | 0.35 s | 0.27 s |
| Recovery movement speed | 2.0 m/s | 3.4 m/s |
| Prone crawl speed | 1.5 m/s | 2.6 m/s |
| Slide crawl speed | 1.1 m/s | 2.7 m/s |
| Committed slide duration | 0.45 s | 0.42 s |
| Slide deceleration | 8 m/s² | 6 m/s² |
| Slide recovery | 0.20 s | 0.14 s |
| Action input buffer (Dive/Slide) | none | 0.11 s |

**Direction remains committed for the actual dive and slide**, and the physics body remains solid throughout. Short buffered presses during the final part of recovery are recognized once; holding a key does not chain automatic moves. Successful get-up retains controlled directional velocity instead of forcing a dead stop. Stationary slides still do not invent momentum.

At a constant 22 m/s² gravity and equal-height ballistic estimate, the ordinary jump rises about **1.11 m** rather than 0.90 m, and the dive rises about **1.19 m** rather than 1.05 m. Those are *ballistic estimates*, not collision guaranteed clearance values. Straight-line running reaches speed in about 0.17 seconds, with a full opposite-direction reversal taking about 0.30 seconds in the basic ideal case (previously about 0.55 seconds). Actual movement uses real collision physics.

## Obstacles and source integrity

The **single AerialSlot** upper deck and both supporting posts are 0.50 m taller to preserve a genuine two-tier opening for the taller dive. The changed solid geometry and matching decorative visual meshes belong to its **one existing authoritative modular prefab**. The existing rail, bench, low deck, turn frame, gates, and other prefab dimensions remain unchanged. Physical collision and visual decoration remain separate. The release contract rejects outdated dimensions or a mismatched production asset.

The baked, low-compute front-page choreography was re-recorded using the new production motor and actual PhysX. The cinematic menu still runs only on a lightweight recorded animation. The Blender rig/source was restored and its repository checksum confirmed unchanged.

## Empirical traversal results

Independent real-PhysX tests load the **actual saved public Practice scene** with production prefab instances and real solid colliders.

For each of the two principal dual-route obstacles (rail and bench), the tester varies three lateral offsets (−0.23 m, 0 m, +0.23 m) and three launch distances *for each move*. Results:

| Production crossing | Dive-over | Slide-under | Average time from launch to standing beyond obstacle |
|---|---:|---:|---|
| UnderOverRail | 9/9 | 9/9 | Dive: 1.20 s; Slide: 0.63 s |
| UnderOverBench | 9/9 | 9/9 | Dive: 1.20 s; Slide: 0.63 s |

These are repeatable **test cases with consistent approach direction and controlled initial speed**, not a guarantee that every angle, bot encounter, or arbitrary initial state succeeds. They demonstrate that a valid route is no longer limited to a single perfectly timed center launch. The slide is a faster grounded shortcut; the dive retains a meaningful aerial commitment. Additional player testing should evaluate whether either shortcut beats every relevant detour under live pursuit.

Other independent tests cover the AerialSlot's genuinely solid two-tier trajectory, overhead protection, physical body transitions, head animation continuity, full arena route connectivity, bot navigation, and 300 match simulations.

## Verification and reproducibility

- **103/103 PlayMode tests** and **33/33 EditMode tests**, from isolated Unity 6000.3.26f1 runs.
- Both crossing approach envelopes pass on the authoritative Practice scene.
- **Production WebGL, separate technical Gym WebGL, and native Linux release builds** succeed with zero build errors. All animation, Blender, collider, arena placement, start markings, replay, and Practice contracts pass.
- **Actual Chrome mouse/keyboard input** verifies the recorded main menu, public Practice, sprint, jump, dive, slide, direct exit, arena selection, normal match countdown, active movement, and Pause. No JavaScript exceptions or browser-console errors.
- **Native Linux player** opens and renders the animated main menu with no startup exceptions. Synthetic native mouse controls were not independently evaluated.

Evidence is under [evidence](evidence/release-verification.json). [Practice dive](evidence/webgl/05-practice-dive.png), [Practice slide](evidence/webgl/06-practice-slide.png), [competitive match](evidence/webgl/11-match-active.png), [native Linux startup](evidence/native/linux-menu.png). The reproducible Chrome script is `verify_movement_browser.py`.

## Deliberate scope and remaining human evaluation

This is a competitive movement-feel **baseline**, not a claim that gameplay is ready for professional esports without playtests. Skill perception, relative incentive to detour versus use each obstacle, actual difficulty for new players, online latency, and balance of the current bot all need independent human evaluation. No multiplayer networking or predictive movement was added. The bot still primarily navigates around obstacles; it has not been taught new exploit-only maneuvers.

Do not restore slow recovery values merely to imitate human injury time. Keep real collision and predictable committed actions. Balance from measured traversals, action visibility, and human chase sessions.
