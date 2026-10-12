# CHASE v0.8 — universal prone crawling after every chest landing

**Current movement contract.** The existing Superman projectile is unchanged in flight. Every chest landing uses one shared prone state and automatic get-up rule. There is no separate shelf mode, no automatic backward retreat, and no player toggle to remain prone once upright clearance is available.

## The single rule

1. A real chest impact enters **DiveProne** for the existing **0.40-second minimum**. Even during that timer, WASD can crawl slowly in all four camera-relative directions.
2. Horizontal prone motion is bounded by **1.50 m/s**. Contact removes excess airborne speed when the chest lands, so a held sideways or backward key responds instead of fighting the former 9 m/s projectile. Zero movement keys brakes naturally to rest.
3. Once the minimum impact time ends, the motor **always tries to get up**, irrespective of buttons held.
4. If a real roof, wall, or competitor prevents the clearance-checked crouched body from fitting, the athlete stays physically prone; WASD remains active, and no automatic backward motion occurs.
5. As soon as crawling reaches real clearance, the game automatically enters the existing **0.35-second LeapRecovery** animation and returns to normal standing or crouched locomotion. The player cannot choose to remain prone on open floor.

The physical capsule stays low, solid, non-triggering and aligned with the original dive heading throughout crawling. A and D move the prone body laterally without rotating its long collider through obstacles. W and S move forward or backward. No Jump, Leap, Slide, or WASD input cancels the recovery commitment.

A point-blank attempted dive may retain the upright compact capsule to avoid spawning horizontal collision inside a wall; it still uses the same movement and clearance rules when entering the prone motor state. Its upright impact visual avoids displaying a stretched body intersecting a wall.

## New real production animation

One small wiggling **CHASE_DiveCrawl** loop is authored directly in the single approved Blender source, Chibi/Chibi_Rigged.blend. It uses alternating arms/legs and restrained torso/head movement, with matching first and last poses; no root motion or fictitious physics impulses.

There are now **13 Fake User Blender actions, 13 real exported FBX animation takes, and 13 real production Animator states**. The import and pre-build contracts fail on stale frame ranges, missing states, incorrect loop flags, or temporary preview assets. The animation driver plays **DiveCrawl** for physically moving prone poses and **DiveProne** at rest.

The original **17 character meshes and all 24,684 source vertices are unchanged**. See the independent [before/after geometry digests](evidence/rig/approved-mesh-topology-digests.json). Blender validates intermediate crawl bounds, a seamless loop, forearm direction, and all original football slide and Superman actions.

## What the environment decides

| Situation | Behavior |
|---|---|
| Ordinary flat-ground dive landing | May crawl briefly during impact, but **automatically gets up** even with WASD held |
| Landing beneath low shelf | Must remain prone because clearance genuinely fails; player crawls using **W, A, S, D** |
| No keys beneath shelf | **Stationary** after physical braking; no invisible retreat or escape automation |
| Crawl clear of roof | Automatic get-up; no dedicated stand button |
| Wall/opponent in crawling path | Real solid PhysX collision prevents penetration; tag contact rules remain |
| Too-tight permanently sealed space | Cannot walk or phase through wall; must move through an actual opening |

## Tests and playable verification

The Unity real-physics test suite includes four-direction crawling during impact under a real solid low canopy, fixed prone collider heading, low-speed and no-input stopping, side-exit automatic recovery, ordinary open-floor automatic recovery even with held buttons, seven real gym shelf approaches, wall and opponent tag contact, and all previous football sliding-tackle tests.

An additional test invokes the **actual imported Animator** and checks that real prone movement selects DiveCrawl, then stops into DiveProne without root motion.

The standalone Movement Gym is served locally at http://127.0.0.1:8766/ and production at http://127.0.0.1:8765/ on the development desktop only. **K** stages a tight shelf approach; press **Up** to physically dive and land under the shelf, then try four-way WASD crawling. **G** stages the ordinary shelf dive; **H** stages wall contact; **J** stages the football slide; **R** resets to open space; **V** switches the inspection camera.

The [real Chrome eight-frame crawl sequence](evidence/gym/prone-crawl-eight-stage.png) and [keyboard/console report](evidence/gym/browser-prone-crawl-verification.json) were collected with the [reproducible script](verify_prone_crawl_browser.py), waiting for genuinely rendered Unity gameplay. The separate [production Chrome smoke](evidence/production/browser-production-verification.json) and [football tackle regression](evidence/tackle/browser-tackle-verification.json) guard existing gameplay.

## Physical and artistic limits

The chibi remains an animated skeleton driven by a shared Rigidbody motor, not a ragdoll with individually simulated hand and foot contacts. The one wiggle loop is deliberately generic rather than reproducing different biomechanical hand movements in each direction. The underlying low collider never turns sideways or moves through obstacles for a more decorative animation.


## Final release verification — October 12, 2026

- **74/74 PlayMode tests passed**, including four-direction prone crawling during the chest-impact timer beneath a real low ceiling, no automatic retreat on zero input, physical sideways exit and get-up, automatic standing in open space even with keys held, real imported Animator wiggle/still selection, and all previous jumping, Superman dive, football slide, and tag regressions. **15/15 EditMode tests passed** after importing the thirteenth action.
- **All three player builds succeeded:** Movement Gym WebGL, production WebGL, and native Linux standalone. Each reported zero errors and the known existing Unity Pipeline warning. The rebuilt Linux player launched without runtime exceptions or missing assets.
- **Three independent Chrome replay checks passed** with real keyboard input and no console errors: [shelf landing, no-input prone, lateral movement, exit and standing](evidence/gym/prone-crawl-eight-stage.png), the [production movement game](evidence/production/browser-production-verification.json), and the [football sliding tackle](evidence/tackle/browser-tackle-verification.json). The production and tackle tests loaded explicit current-build URLs with browser caching disabled. The open-floor automatic-stand check explicitly reset to the ordinary spawn, rather than accidentally reusing the shelf staging position.
- **Blender production validation passed:** 13 persistent Fake User-backed actions, 13 real FBX/controller clips without preview references, exact closed crawl loop, original 17 skinned mesh topologies and all 24,684 approved source vertices preserved.
- **Reproducibility:** [Machine-readable audit](evidence/verification-summary.json), [Chrome keyboard/console report](evidence/gym/browser-prone-crawl-verification.json), and all three replay scripts linked above. WebGL and native player binaries remain in the development computer's ignored Builds directory.

The movement implementation is complete against the user-specified rules. The subjective quality of the small crawl wiggle remains appropriate for human playtesting; automated tests cannot establish a perfect artistic feel.
