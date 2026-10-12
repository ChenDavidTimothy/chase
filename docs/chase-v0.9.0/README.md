# CHASE v0.9.0: modular, physically multi-route obstacles

## Design objective

The World Chase Tag-inspired arena should offer meaningful physical choices rather than color-coded, one-action obstacle puzzles. CHASE retains the same four movement primitives: running, jumping, feet-first football sliding and committed headfirst Superman diving. Obstacle geometry creates the choices; obstacle objects do **not** detect the current move and teleport, boost, clip, disable collisions, or select an animation.

## Implemented geometry

Two reusable solid Unity prefab modules live in `Assets/Obstacles/`:

| Module | Beam width | Depth | Beam underside | Beam top | Intended routes |
|---|---:|---:|---:|---:|---|
| `UnderOverRail.prefab` | 2.30 m | 0.26 m | 0.86 m | 0.94 m | Sliding beneath or timed Superman dive above |
| `UnderOverBench.prefab` | 1.70 m | 0.34 m | 0.85 m | 0.92 m | Same choices, tighter width and thicker crossing |

The source prefab is the geometry contract. The modules contain ordinary non-trigger BoxColliders, existing `ChaseBlocker` and `ChaseOverheadPassage` sensor tags, and existing `ChaseOccluder` camera-visibility tags. Their decorative highlights have no colliders. Parenting and rotating a module does not require modifying the shared motor or creating new state machines.

Two instances are authored in each scene without rebuilding existing obstacles. The production arena gains a central crossing and a narrower west bench; the independent Movement Gym gains repeatable test placements. Source installation/validation tool: `Chase/Install Reusable Dual-Route Obstacles`, in `Assets/Editor/ChaseModularObstacleBuilder.cs`. It preserves existing prefab files and already-installed objects, refusing inconsistent same-name replacements. Release builds validate prefab physical dimensions, support colliders, camera markers, scene placement and intact prefab links. Procedural layouts are **not** implemented or validated yet. Future placements must still check approach clearance, neighboring obstacles, width, rotation, spawn positions, and collision-free reachability.

## Minimal movement balance change

`ChaseTuning.leapVerticalSpeed` is increased from **5.2 to 6.8 m/s**, retaining **9.0 m/s horizontal speed** and **22 m/s² gravity**. This provides enough physical altitude for a long solid dive capsule to clear the top of either module, given a correctly timed approach. It does not implement a slide-to-dive conversion. The foot-first slide, capsule/box dimensions, jumping, tag contact, crouch, animation state machine and Blender authoring source were not changed.

This is a **balance proposal validated for these geometries**, not a claim that the higher dive will feel ideal in every match. Headfirst aerial commitment and recovery remain the tradeoff for the high route. The original aerial-shelf dive is also verified as traversable after the change.

## Gym procedure

Open the independent Movement Gym or its rebuilt WebGL page at `http://127.0.0.1:8766/` in the development environment. `B` places the player on the rail's 2.7 m Superman approach: press W+Up immediately. `N` places the player at rest on a longer 3 m run-up: hold W to accelerate, then press Down before reaching the rail; continue W to crawl forward if low recovery is constrained. `V` changes view angle, `R` resets. These are staging/inspection shortcuts, not movement or collider bypasses.

## Completed verification, October 12, 2026

- **83/83 PlayMode** and **15/15 EditMode** tests pass in both Editor testing and independent Unity CLI tests. Includes the two genuinely different over/under prefab footprints, wall contact, recovery, opponent tags, hundreds of simulated matches, and original aerial-shelf traversal.
- All three standalone CLI builds succeeded with **zero BuildReport errors**: production WebGL, Movement Gym WebGL, and Linux standalone. There is one existing Unity Pipeline warning in each build, related to the installed Pipeline runtime package; it does not block these builds.
- A rebuilt Gym **real Chrome/WebGL replay** drove B/W+Up and N/run-up/W+Down with real keyboard events. It captured midair dive, long horizontal slide under the rail, protected recovery, standing and continued movement. Zero browser console errors and no JavaScript exceptions were reported. See [Gym browser verification](evidence/gym/verification.json) and frames [diving](evidence/gym/03-dive-descent.png), [sliding](evidence/gym/07-slide-under-rail.png), [protected recovery](evidence/gym/08-slide-low-recovery.png), and [crawling out](evidence/gym/09-slide-crawl-out.png). Precise physical clearance is established by Unity PhysX tests, not screenshots alone.
- Rebuilt production **Chrome/WebGL** loaded the scene, accepted real Up, Down and Space inputs, prevented browser scroll and emitted zero console errors. See [production browser verification](evidence/production/browser-production-verification.json) and [production frame](evidence/production/production-fresh-arena.png).
- The **native Linux build** launched and rendered, reached the end of a live match, and produced no runtime exception in its player log. See [native screenshot](evidence/native/linux-production.png).
- The sole approved `Chibi/Chibi_Rigged.blend` is unchanged at SHA-256 `59c735e5f835596acdc0329082d936a3d0f34f5c35c5c4710ef80dc0a5eeb371`; strict Blender headless validation passes for **13 actions** and **17 visible mesh parts**.

## Limitations and next design tasks

The module library now supports a verifiable **two-way movement choice**, not a complete procedural arena generator. Human playtesting is still required to judge whether 6.8 m/s is appropriately telegraphed and balanced in a competitive match, whether additional route families are needed, and whether placements provide enough approach room without trivializing the current arena. The procedural system should validate route approach/departure space and spawn safety before random generation, not just scatter modules and hope physics will resolve overlaps.

The only semantic change to the existing motor balance is the Superman vertical impulse. All new obstacle geometry remains ordinary solid physics.
