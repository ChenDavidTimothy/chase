# CHASE: WCT reference-layout trial

**Status:** Separate playable comparison scene, not a production replacement.

## Intent

This is an authored reconstruction of the uploaded chase-tag obstacle reference, translated into the current CHASE slate, cream, sky-blue, mint, coral, and gold palette. The photograph provides only relative placement. World-space dimensions are *estimates*, not surveyed World Chase Tag specifications. No official logos or brand graphics were imported.

## Implementation

- Playable comparison scene: Assets/Scenes/ArenaWCTTrial.unity, cloned from the real production Arena 01 match scene.
- Source-of-truth layout/build command: Assets/Editor/ChaseReferenceArenaBuilder.cs, Unity menu **Chase > Arena > Build Reference Trial**.
- Separate geometry guard: Assets/Editor/ChaseReferenceArenaContract.cs, Unity menu **Chase > Arena > Validate Reference Trial**.
- Reusable source-linked prefabs: Assets/Obstacles/WCTTrial/, **21 trial landmark prefabs** (including five skill-crossing links), plus two certified original UnderOverRail/UnderOverBench crossings. **23 modules total**.
- Geometry features the image's rear metal frame spans, two open-sided high piers, reoriented east-west incline, raised passable tables, crossing beams, a foreground lattice, and four side-wall interruption points. All **23 placed assemblies** are constrained to 0°/90°/180°/270° cardinal yaw. Both world-axis directions are used; no diagonal obstacle orientation is allowed by the authoring contract.
- Physics uses ordinary solid classified BoxColliders, with non-colliding rounded decorative meshwork and preexisting CHASE materials.
- Trial uses a static view of its own arena in menus instead of the old physical menu recording, which would conflict with the new geometry. CHASE movement, collisions and animations are unchanged. The 21-cell production bot planner has a **trial-only** allowance for real passable under-deck/under-rail skill links and uses the original physical slide to attempt them. No scripted traversal or bot-only motion.
- Production Arena 01, its nine-prefab placement authority, public Practice, the technical Gym, and the sole Blender character source are preserved.

## Verification

The trial validator checks source prefab links, deterministic placement, floor and match retention, original scene separation, renderer/solid separation, bounds, role spawn safety, and zero inter-module penetrations. Eleven dedicated EditMode tests check source-linked geometry, strict cardinal alignment, three genuine joined obstacle assemblies, open podium interiors, interrupted side-wall running, a measurable standing-only detour versus direct travel, bot navigation via genuine low passages, and both slide and dive over/under crossings using the actual motor in both directions. The existing PlayMode motor physics suite remains unchanged.

**Limitations:** Exact reproduction is impossible from this one perspective image. The bot planner recognizes passage links, but a real Editor Runner match against a stationary Evader confirmed that the bot can still **stall in SlideRecovery under WestBridge** after crossing the first west-side rail. This issue is unresolved; it is not competitively acceptable and is a blocker to replacing production Arena 01. Human-versus-human balance, exhaustive approach windows, and measured shortcut-vs-detour times also remain unproven. These are reasons to keep this as a trial.

## How to play

In Unity, open Assets/Scenes/ArenaWCTTrial.unity and press Play. It retains Main Menu, difficulty, arena preview, countdown, scores, result screens, and public Practice access.

For a separate browser comparison, use the WebGL trial output in Builds/ChaseReferenceTrialWebGL. Its two scenes are the reference trial plus public Practice. It uses the existing Unity .unityweb decompression fallback. This directory is **not** the deployed production build and is not automatically uploaded to Cloudflare.

## Original reference

User-provided perspective render of an open, scaffold-heavy chase-tag quad, supplied 2026-10-14. The uploaded image was used as the arrangement reference but is not embedded into shipped assets.

## Evidence from this implementation

- Current EditMode suite: **44/44 passed** (33 pre-existing + 11 trial-specific).
- Current PlayMode suite: **103/103 passed**, without modifying the production motor.
- Trial and production Editor scene contracts: **both passed**.
- Unity Editor trial round: menu, arena selection, countdown, physical tag, and results observed.
- WebGL trial player: **Succeeded**, 0 errors, 1 known optional Pipeline runtime configuration warning; 17 deployment files using `.unityweb` fallback.
- Real Google Chrome browser flow: Main Menu > difficulty > Arena Trial preview > competition start > advancing round. **0 JavaScript exceptions, 0 browser errors**. See evidence/webgl/verification.json and screenshots.
- Edited Scene View illustration: evidence/editor/arena-overview.png.

The previous production WebGL output and public Cloudflare Pages deployment were not touched. The comparison player is local and requires the user's approval before any public deployment.

## Iteration 2: competitive circulation rationale

The first comparison was visually aligned but had nearly parallel frames, an opaque-looking low podium, and clear side-wall jogging loops. This revision rotates key landmark modules to several axes, removes the decorative podium skirt, increases under-deck clearance for the actual slide body, and inserts four low under/over wall baffles. The central cross-fork acts as a skill gate while grounded counter-routes remain locally available. Distinct choices are physical dives OVER, slides/crawls UNDER, or longer spatial detours around an end. The skill-aware trial bot can plan through the genuine low-passage links, while the ground-only route between the role starts is measurably longer than their straight-line separation. This is not a substitute for measuring human route selection or bot completion reliability over entire matches.

### Known trial blocker: AI progress

During an Editor Runner chase with stationary blue Evader, the red Chaser initially moved from (-5.10, -2.10) through the widened west-wall rail, then stopped near (-2.70, 0.14) in SlideRecovery beneath/along the WestBridge table. The winner was the Evader at timeout, but this is not evidence of balanced defensive play. Passing pathfinding reachability, PhysX crossing fixtures, and browser smoke tests does **not** establish that the bot can complete complex routes. Do not promote this trial or claim its AI is complete without fixing and rerunning end-to-end bot movement tests.

## Iteration 3: orthogonal, connected course

The scattered diagonals from iteration 2 have been replaced by only **cardinal X/Z alignment**. Every one of the 23 authored module placements has a Y rotation that is an exact multiple of 90°. Two families face across X and Z, rather than one axis being dominant. Their coordinates remain authored, not an imported recreation of the WCT quad.

Three modules now contain structural **joined physical subassemblies**, rather than unrelated floating pieces:

1. **NorthLinks**: a coherent two-bay scaffold with **two portal frames connected by two solid overhead side spines**, plus a high central under/over deck.
2. **FrontGrid**: an ordered two-bay rail rack with **two independent low passages joined by upper solid lateral beams**.
3. **WestBridge**: an elevated under/over table with a **solid connected 90-degree turning extension and two support posts**, offering a change-of-direction line rather than another isolated table.

These compounds intentionally create identifiable route landmarks rather than adding five more isolated obstacles. The four wall interruptions and the two original certified crossing modules remain. The actual Collider bodies stay non-trigger and physically verified.

**Trade-off:** orthogonalizing the layout reopens a **longer standing-only counter-route** between role starts. We now validate that it is at least 1.38 times their straight-line distance, alongside actual physically traversable skill links. This is intentional counterplay, not a claim that walking is impossible. Tests do not yet prove that the over/under links are faster than *every* possible counter-route during a human chase. The earlier WestBridge bot stall should still be treated as an unresolved issue, not a certified fix. An Editor Runner observation of this iteration showed movement into the course but no tag by timeout; that is not proof of competitive AI readiness.


## Iteration 4: reachable podium entrances

Both high podiums (CrownTower and SouthEastTower) formerly had four overhead perimeter rails. Their deck height also made the Crown podium difficult to access with CHASE's normal jump. The new source prefab geometry retains four solid corner posts and **three solid upper rails**, leaving exactly one visibly open, arena-facing approach. CrownTower opens toward **local -Z / center arena**, and SouthEastTower opens toward **local -X / center arena**. A thin mint edge marking identifies each entrance; this art is non-colliding.

Both deck tops are now at **1.02 m**, reachable using the real 7 m/s jump (maximum ideal vertical rise approximately 1.11 m), while their lower face remains **0.89 m** above the floor to preserve real sliding below. No movement tuning, capsule collisions, scripts, cameras, or prefabs outside the trial have been changed.

Two new EditMode PhysX tests instantiate each saved tower prefab alongside the real ChaseMotor, run in at physical sprint speed, execute a genuine jump, and require the actor to become grounded *on* its deck. The existing underpass, source-link, layout and competition tests continue to pass. Editor evidence: evidence/editor/podium-crown-open.png and evidence/editor/podium-southeast-open.png. The isolated trial WebGL build and Chrome menu-to-round smoke were repeated successfully.

This fixes platform ingress. It is not evidence that every platform approach is equally viable, or that the existing trial bot's independently documented WestBridge recovery stall is fixed.
