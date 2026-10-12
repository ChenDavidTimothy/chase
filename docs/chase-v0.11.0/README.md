# CHASE Arena 01: mastered routes (v0.11.0)

## Design intent

This release prioritizes one recognizable 12 × 12 m competitive course over procedural generation or more movement primitives. A route should be useful because of approach, speed, angle, opponent position, and recovery commitment, not because an obstacle commands a predetermined animation or accepts one correct button press. The nine-piece production layout remains recognizable so players can learn routes and invent counters. These are measured *opportunities* for mastery, not claims of optimal balance without human competitive playtesting.

## Three contest areas

| Area | Elements | Purpose |
| --- | --- | --- |
| **Crossline** | Central `UnderOverRail` at (0,0) | Superman above, football slide below, two grounded exterior bypasses; a chaser who predicts commitment can approach the landing side |
| **West Switchback** | `UnderOverBench` (-3.55, 0.55), `TurnFrame` (-1.55,-3.55), `LowCanopy` (-3.20,4.35), low hurdle (0,-4.90) | Shorter crossing, reversal lines, and protected low route; open floor and edge returns allow interception rather than perpetual evasion |
| **East Elevation** | `AerialSlot` (3.35,2.45), `LowDeck` (2.35,-3.35), `OffsetGates` (3.75,-0.55), `InclineRun` (1.65,4.20) | Elevated jump and leap approaches, visually legible gates, broad lane transitions and side routes |

Production prefabs remain in `Assets/Obstacles/ArenaV1`; the builder remains `Assets/Editor/ChaseArenaV1Builder.cs`. Their geometry is solid, non-trigger PhysX, with decorative meshes separate from colliders. Obstacle collisions never teleport, boost, disable, or instruct the player. No new animation state, root motion, vault, wall climb, or slide-to-dive was added.

## Removed legacy authoring

The old v0.9 `UnderOverRail.prefab` / `UnderOverBench.prefab` duplicates, `ChaseModularObstacleBuilder`, and obsolete `ChaseSceneBuilder` compatibility entrypoint are removed. The test Gym **continues** to contain standalone physics measuring lanes, old hurdle/wall/canopy targets, and the reference shelf for historical regressions. Its two named crossing fixtures now reference the **current production prefabs**, not divergent copies. Both production and Gym authoring are maintained by the one Arena V1 builder; release builds fail if Gym fixtures or production source mappings drift.

The currently intended production geometry is deliberate rather than generated, and future revisions must clear the production scene contract before release. The approved `Chibi/Chibi_Rigged.blend` is the sole authoritative Blender character source. No Blender content was modified.

## Measured player-envelope findings

Strict headless Blender verification confirms 13 approved saved actions and 17 skinned mesh parts. In its slide, the actual pose measures roughly 0.74–0.75 m tall and 0.64 m wide, compared with the physical box (0.82 m high, 0.84 m wide). In a settled Superman pose, extended arms span approximately **1.18 m**, wider than the solid dive capsule's **0.86 m diameter**. The offset-gate post gap was widened from a narrower proof-of-concept gap to **1.37 m** between the inside faces, leaving visual arm margin. The runtime test now bakes the *imported* skinned pose in Unity to guard against an animation import regression; it does not merely inspect the collider.

### Timed crossings and counterplay (actual production scene, PhysX)

| Metric | Crossline | West Switchback |
| --- | --- | --- |
| Solid crossing top | 0.94 m | 0.92 m |
| Underside | 0.86 m | 0.85 m |
| Slide-box minimum gap at crossing | 0.030 m, both directions | 0.020 m, both directions |
| Safe Superman launch samples | 4/5 in either direction | 4/5 in either direction |
| Shorter standing bypass with 0.70 m body | 6.50 m, each side | 6.00 m, each side |
| Standing bypass with conservative 0.98 m body | 7.00 m, each side | 6.50 m left, 6.00 m right |

Superman timing samples were drawn from five discrete standing start distances (2.4, 2.55, 2.70, 2.85 and 3.0 m), under 9 m/s horizontal launch and existing 6.8 m/s vertical launch. Each success required crossing the *entire actual solid beam*, retaining normal airborne velocity, physically clearing the top without a solver boost, and making forward progress. These measurements demonstrate a multi-sample timing range, **not** a continuous timing guarantee or measured win-rate. Underpasses require the actual slide box, ordinary movement input, passage, then standing recovery; no phasing. Bypass lengths are conservative four-neighbor grid estimates and not optimized human free-running trajectories.

## Bot navigation

Before release, a concrete spawn-pair simulation exposed the bot repeatedly approaching the raised incline's vertical side. The bot's existing obstacle sampling was extended from a single waist-height sphere to a *standing-height capsule* so it can classify low raised obstructions, and the incline is tagged as a blocker **for bot grid routing** while remaining a real physically climbable ramp for player locomotion. No bot-only collision or movement mode was added. A new real-scene test confirms the pursuer can reach a stationary opponent from all four spawn pairs in both directions. This establishes navigation safety, not an advanced strategic opponent.

## Verification (2026-10-12)

- **85/85 PlayMode** and **31/31 EditMode** tests pass in independent Unity 6.3 LTS CLI, corroborating editor runs. New tests include both routes through current saved production prefabs, input timing variety, conservative Chibi body bypasses, connected spawn paths, navigable incline and bot paths from all spawn pairs.
- Rebuilt **production WebGL, independent Gym WebGL, and Linux native** all report `Succeeded`, `errors=0`. A pre-existing Unity Pipeline runtime package warning appears once per build and is non-blocking. All release-time Blender animation, movement, Gym prefab, and Arena 01 geometry contracts pass.
- Real Chrome on newly rebuilt production WebGL confirms render, in-canvas Up, Down and Space input, no browser scrolling, and zero console errors. See [production verification](evidence/production/browser-production-verification.json) and [render](evidence/production/production-fresh-arena.png).
- Real Chrome on newly rebuilt Gym confirms **Superman over** and **football slide underneath** the exact production `UnderOverRail` asset, with controlled real keyboard events, landing, recovery and standing. See [Gym verification](evidence/gym/verification.json), [airborne](evidence/gym/03-dive-descent.png), and [slide](evidence/gym/07-slide-under-rail.png).
- Native Linux player launched, accepted input, rendered the current course and produced no fatal errors or runtime exceptions in its player log. See [native input frame](evidence/native/linux-production-input.png) and [match frame](evidence/native/linux-production-initial.png).
- Headless Blender source-of-truth and rig contract passed: **13 actions, 17 skinned mesh parts**, current Blender file SHA-256 `59c735e5f835596acdc0329082d936a3d0f34f5c35c5c4710ef80dc0a5eeb371` (unchanged).

## Competitive-play limitation

The new arena is a *validated competitive baseline*, not a statistically balanced multiplayer arena. Both-player human testing, real streamer viewing tests, spatial callouts, and chase replay analysis are still needed to determine whether the west reversals are sufficiently unpredictable, whether pursuit traps occur too frequently, and whether certain lines dominate. Bots do not yet employ the full human motion primitives. Avoid a second map or procedural generator until genuine competitive observation supports the map's strengths and reveals its weaknesses.
