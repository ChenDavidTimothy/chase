# CHASE v0.10.0: production Arena V1

## Intent and scope

Replace the temporary production arena's intersecting plain blocks with a coherent, legible, reusable set of compact athletic obstacles. The environment creates multiple physical choices; there are **no** obstacle-specific collision bypasses, animation commands, player boosts or added movement state machines.

Nine portable prefab families now form the production quad. The original gym and its v0.9.0 calibrated over/under fixtures remain intact. The 12 × 12 m floor, boundaries, camera direction, matchup, controls and spawns remain in the saved production scene. This is one deliberately authored course, **not** a procedural generator or an assurance that every player will choose all routes competitively.

## Production modules and gameplay role

| Prefab (`Assets/Obstacles/ArenaV1/`) | Production placement (X, Z) | Intended options and geometry |
| --- | --- | --- |
| `UnderOverRail` | (0, 0) | Two physical choices: slide underneath the 0.86 m underside, or launch a correctly timed Superman dive over the 0.94 m top. Remains a blocking upright path for the bot. |
| `UnderOverBench` | (-4.65, 0) | Narrower crossing, 0.85 m underside, 0.92 m top, 0.34 m depth. Both routes usable in either direction, or navigate around. |
| `AerialSlot` | (3.35, 2.45) | Reuses measured 0.55 m sill and overhead tier with 1.85 m underside. Timed solid horizontal dive threads the opening; side bypass remains possible. |
| `LowCanopy` | (-3.10, 3.15) | 1.64 m clear under a 2.2 m wide roof. Crouch/slide beneath, or travel around the exterior. |
| `LowHurdle` | (0.85, -4.15) | 0.38 m solid short low obstacle, normal grounded jump or circumnavigation. |
| `TurnFrame` | (-3, -2.30) | Two-armed corner frame: cuts sightlines and creates reversals around endpoints. Thin actual blockers, not a teleport. |
| `LowDeck` | (2.35, -3.35) | 0.44 m physical raised deck, with jump-up/over and floor-level bypass options. |
| `OffsetGates` | (3.75, -0.55) | Two offset gates allowing route choice around posts and through gaps. Bot sees blockers; human has ordinary physical movement. |
| `InclineRun` | (1.60, -1.50) | 13-degree shallow solid incline supporting uninterrupted grounded running and a higher launch surface without modifying the motor. |

All production modules are prefab assets, placed at unit scale and checked by `ChaseArenaV1Contract`. Every physical collider is an actual non-trigger `BoxCollider` with an existing `ChaseBlocker`, `ChaseOverheadPassage` or `ChaseHurdle` classification. Cosmetic surfaces are separate nonphysical meshes. A shared rounded box mesh and clean enamel/porcelain color palette make the silhouettes read from CHASE's elevated camera. Nothing is imported into the character rig and no additional Blender source of truth is created.

## How to maintain

`Assets/Editor/ChaseArenaV1Builder.cs` is the intended authoring source for the nine prefab families and the deterministic production layout; use `Chase/Rebuild Arena` in Edit Mode to update it. This operation replaces *only* the owned arena-obstacle group and removes the obsolete placeholder objects, leaving floor, walls, match systems, camera and spawn markers intact. It is repeatable. The prior `ChaseSceneBuilder` is deliberately reduced to a compatibility entrypoint to the new builder, so it cannot restore cube placeholders. Legacy dual-route fixture authoring has been moved to the independent Gym menu.

`Assets/Editor/ChaseArenaV1Contract.cs` validates each prefab's physical structure and key numeric dimensions, exactly nine prefab links and their transform placements, legacy-object removal, inter-module collider penetrations, key run-up body occupancy and spawn safety. That contract runs before both production and Gym release builds. In-situ PhysX tests go beyond a geometry contract and check real body movement through the assembled, colliding scene.

The current `ChaseBot` remains intentionally simple and uses only the human's shared motor. New beam and gate colliders carry blocking tags so its current grid pathfinding does not mistakenly classify them as freely walkable. An independent actual-production-scene navigation test verifies that it can route around the central rail to an opponent. This is not evidence of advanced bot strategy or competent use of slides/dives by bots.

## Release verification, 12 October 2026

- Independent Unity 6000.3.26f1 CLI: **84/84 PlayMode** and **26/26 EditMode** tests passed. Editor reruns agree. New EditMode integration tests instantiate actual saved prefabs, use shared `ChaseMotor` + PhysX at 20 ms ticks, check both slide and Superman routes in both directions, tight canopy, aerial slot, ordinary hurdle, blocked straight-running line, grounded ramp ascent, and safe bot navigation. In-situ launch checks cover four directions across both rail/bench modules in the complete production course.
- All **three fresh builds** passed with zero BuildReport errors: production WebGL, Gym WebGL and native Linux. All three have one non-blocking Unity Pipeline package warning. Both the new arena contract and the existing movement-scene, Gym fixture and Blender animation contracts were accepted before build.
- Rebuilt production in **real Chrome/WebGL**: rendered gameplay, accepted actual Up, Down and Space input, preserved canvas focus and prevented browser scrolling, zero JavaScript exceptions or browser console errors. Evidence: [production verification](evidence/production/browser-production-verification.json) and [fresh playable arena](evidence/production/production-fresh-arena.png).
- Rebuilt Gym in **real Chrome/WebGL**: repeated original B/W+Up over the rail and N/run-up/W+Down under the same rail, including low recovery and standing. Browser replay uses actual keyboard events rather than calling motor internals. Evidence: [Gym verification](evidence/gym/verification.json), [airborne frame](evidence/gym/03-dive-descent.png) and [low sliding frame](evidence/gym/07-slide-under-rail.png).
- Native Linux Player launched with the new arena and reported no runtime exception. Evidence: [native screenshot](evidence/native/linux-production.png). Also see the [editor arena ready frame](evidence/editor/arena-v1-ready.png).
- Chibi rig is **unchanged**: SHA-256 `59c735e5f835596acdc0329082d936a3d0f34f5c35c5c4710ef80dc0a5eeb371`. Source of truth check and Blender 5.2.2 headless strict contract both pass, including the original **13 actions** and **17 skinned mesh parts**.

## Deliberate boundaries

No slide-to-dive transition, terrain climbing state machine, vaulting, new rig, ragdoll, collision phasing or procedurally randomized placements. The existing jump/slide/dive physics values and animation assets are preserved. Only camera framing (8% closer) and presentation-only collider-to-renderer occlusion indexing change outside the prefab library. This provides a clear Arena V1 baseline for real competitive playtesting. Quantitative tests establish safety and reproducible opportunities, **not** that one layout is already the optimal competitive map. The next design validation should be human playtesting with observed route frequencies, stuck cases and underused locations before adding more content.
