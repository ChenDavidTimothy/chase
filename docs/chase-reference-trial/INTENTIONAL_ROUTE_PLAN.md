# CHASE trial: authored route strategy and acceptance gate

Status: implementation plan, not a claim that human chase balance is proven. Production Arena 01 remains separate.

## Character-derived dimensions

Source: Assets/Scripts/ChaseTuning.cs. PhysX-only movement, not animation steering.

| Envelope | Source value | Authoring implication |
|---|---|---|
| Upright capsule | 1.80 m high, 0.35 m radius | Standing routes should be readable and not accidentally snag. |
| Slide solid | 1.98 m long, 0.84 m wide, 0.82 m high | Target >=0.92 m overhead clearance for 0.10 m vertical margin; approach window needs width margin. |
| Dive solid | 1.95 m long, 0.43 m radius | Do not build low overhead rails inside a dive's flight path. |
| Jump | 7.0 m/s launch, 22 m/s² gravity | Ideal flat-to-flat rise 1.11 m; landings below ~1.03 m unless intentionally launched from a ramp. |
| Dive | 8.2 m/s planar, 7.25 m/s vertical | Higher/faster than jumping but committed and more punishable if mistimed. |
| Slide | 0.42 s committed, then shape-aware recovery and crawl | Solid underpasses must have a safe exit and no posts narrowing the 0.84 m body path. |

## Intentional routes

**Eastern run-up (elevation and switch).** Make the east incline lead into a continuous 0.62 m runout, an orthogonal shelf turn and a 1.29 m raised landing. The level change requires a physical jump from the shelf, not a script. From the ground players can approach the landing or bypass the base, and approaching the ramp backward is an alternative with a cost.

**South rack (timing and counter).** Keep two parallel low beams physically connected as a recognizable rack, but eliminate their overhead jump ceiling. Its beams should accept timed slide underneath and dive over, from either direction, with lateral timing tolerance. Walking around the rack costs space, preserving mind games.

**Center/east skill crossings (short cuts).** Raised slide-friendly low beams, avoiding a low roof above a beam intended to be leapt over. Full standing traversal remains an alternative over a longer route. Existing certified under/over modules stay untouched.

**West switchback (corner prediction).** Keep the orthogonal turn but widen the under-deck recovery corridor and put its supporting posts outside the main sliding channel. The alternate standing path should still be viable. Do not promise that the current trial AI can exploit this until end-to-end pursuer verification.

**North gallery (route continuity).** The two-bay scaffold should have a walkable or jumpable sequence to its adjacent table, with enough room to leave the elevated route. Avoid decorative rails that make a dead end.

## Acceptance gates

1. Prefab and scene contracts: no penetrating *independent modules*, no blocked fixed spawns, all solid collider metadata valid and all modules on cardinal axes.
2. Repeatable actual ChaseMotor simulation: successful over/under both directions on representative beams. Measure lateral offset and launch timing variants, not a single perfect frame. Test the connected ramp sequence.
3. No fake passage: real collision remains on; failures are detectable; posts/barriers do not become triggers or vanish.
4. Full EditMode and PlayMode suites, trial WebGL build, and real Chrome menu-to-match smoke.
5. Direct Editor observation of the produced scene and a round. Any remaining trial-bot stall is explicitly disclosed.
6. Do not modify the original production scene, original reusable arena obstacle prefabs, Blender source, or public Cloudflare deployment.

Human-vs-human balance, all possible trajectories, and exact WCT layout fidelity require human playtesting and are not proven by automated checks.

## Earlier iteration-5 measurements and verification (2026-10-14)

- 24 source-linked authored placements: 22 trial prefabs (including the new NorthGallerySpan), plus two untouched certified production under/over prefabs. Every Y-facing is cardinal.
- Eastern linked chain: genuine 13-degree inclined solid, 0.62 m level runout, same-height perpendicular turn, a high 1.29 m landing, and an adjacent (separated) Crown platform. The uphill actor must jump 0.67 m from the intermediate shelf; a standard level-ground jump rises ideally about 1.11 m, insufficient for a 1.29 m landing. The real motor reached the high landing in three tests with -0.12/0/+0.12 m lane offsets, and a separate motor fixture descended into the Crown.
- Southern two-bay skill rack: paired low crossing beam undersides at 0.935 and 0.975 m, leaving 0.115/0.155 m over the 0.82 m solid slide body. The 1.36m punitive top crossbeam was removed; connecting members are lateral. Six real-motor slide/crawl traversals completed both bays from both directions, with lane offsets -0.12/0/+0.12 m.
- Single low skill crossings: center at 1.00m, bottom at 0.955m, yielding 0.135m headroom. Actual slide trials from both directions with 0.14m lateral errors passed. Separate dive cases used two launch distances and each of three lateral offsets in both directions (24 trials total on two prefab families). The overhead dive ceiling was removed.
- Four long-wall controls: beam center 0.98m, bottom 0.94m, 0.12m over slide height, genuine solid posts. The wider 1.76m openings remain physically solid.
- Other trial decks use 0.10m solids. A 1.07m tabletop leaves 0.97m underneath for a slide, rather than the earlier 0.94m. For WestBridge, outside supports are separated from the main low recovery corridor.
- North gallery continuity: two high frame bays, explicit level NorthGallerySpan at the same 1.07m deck height, and the BackBench cantilever. A real PhysX motor walked all three without falling through the joins.
- Both trial and approved Arena 01 geometry contracts passed: zero inter-module solid penetrations, safe fixed spawns, source prefab links, valid physical colliders and unmodified production layout.
- The ground-only actor graph cannot connect the role starts, but the trial bot's skill-aware navigation graph can. Each role retains a reachable ground-level local route. A physically reachable athletic line and a graph path are not proof the bot will reliably execute either.
- Historical iteration-5 regression: 56/56 EditMode and 103/103 PlayMode. Trial WebGL build succeeded with 0 errors (1 optional Pipeline configuration warning), and a real Chrome smoke test entered play with 0 JS/browser errors.

**Unresolved, not swept under testing:** In a live Runner round against a stationary Evader, the red bot moved to approximately (-4.24, 0.85) and then remained stopped until timeout. The AI needs a separate obstacle-aware steering/recovery validation pass. We have not measured human skill-shortcut advantage, match fairness, or production multiplayer balance. The design is a tested physical trial, not a certified competitive arena.


## Iteration 6: deliberate simplification (2026-10-14)

**Final layout decision**: reduce 24 placed source-linked assemblies to **13**. Remove eleven overlapping-purpose modules: WestPortal, NorthEastTruss, EastDeck, EastCrossbar, CenterDeck, FrontBench, WestSnake, SouthWestRails, NorthInnerRail, CrossFork, and UnderOverRail. Their source prefabs are retained only as dormant reusable assets; the scene no longer instantiates them.

**Keep**: the four-member Northern Gallery and Crown connection, the EastRamp to raised landing, Southeast open podium, WestBridge corner with the one certified UnderOverBench alternative, South FrontGrid paired passages, and four physical perimeter gates. The two redundant central obstacles were removed because they interrupted full-body slides through the rack. X/Z placement, existing movement tuning and physical solids stay authoritative.

**Gate for adoption**: verify scene and player/bot navigability after removing clutter. Also inspect slide launch and recovery areas at real athlete dimensions; no standing-only one-step direct line, no narrow mandatory corridor with only a single frame-perfect movement, and multiple readable choices. If the standing-only role-start route reappears, judge its length against viable physical shortcuts instead of filling the court indiscriminately.


### Measured results of the lighter layout

- **13/24 placed** (45.8% fewer); only 12 trial prefabs and the certified UnderOverBench are instantiated. Unplaced source assets remain available.
- Standing-only role-to-role path: **17.64 m** sampled at 0.245 m, compared with **11.03 m** direct (1.60x longer). This preserves counterplay without an easy straight wall circuit.
- Full arena, not isolated test prefabs: both travel directions × offsets (-0.12 m, 0, +0.12 m) through the *entire* connected FrontGrid (6 successful PhysX traversals); the same matrix for each of the four perimeter gates (24 successful traversals). Failures during development revealed colliding run-ins despite initially passing prefab-only tests. These were corrected geometrically and the final matrix passed.
- Both ArenaWCTTrial and untouched production Arena 01 contracts pass with no penetrating inter-module colliders.
- Complete regression: **70/70 EditMode**, **103/103 PlayMode** (see live test output). Chrome/WebGL verification records are stored in the adjacent README evidence folder.
- In two successive Editor Runner rounds, the bot tagged a stationary Evader with 17.58 s remaining in each. Repeated human or bot matches are still needed before competitive certification.
