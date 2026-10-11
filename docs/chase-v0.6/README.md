# CHASE v0.6 — grounded feet-first football tackle

**Current movement contract.** Replaces the old Down Arrow upright-looking crouch glide. The approved actor meshes are unchanged, but the two Blender slide actions and shared physical slide envelope have been rebuilt together.

## What Down Arrow does

- **Sprinting or moving:** Slide commits to the **existing actual momentum** and locks that initial heading. Maximum ordinary sprint is 6 m/s. Grounded braking is 8 m/s² over 0.45 seconds. W/A/S/D, Jump or Up cannot steer or add momentum during the tackle. Pressing Down from rest makes a low pose but does not invent forward movement.
- **Clear space:** The motor immediately switches from the standing capsule to a real solid, forward-aligned **1.98 m long × 0.84 m wide × 0.82 m high BoxCollider**. Its center is 0.48 m below and 0.24 m behind the standing actor's center, so the bottom is near the floor. This is the same dynamic 70 kg Rigidbody, with continuous collisions, frictionless contact material, and no trigger or tunneling.
- **Pose:** The saved real Blender slide action has hips down, torso almost horizontal and reclined **behind** the pelvis, left foot extended forward and opposite knee flexed. It is a footballer's feet-first sliding tackle, not a headfirst Superman dive. Its settled evaluated mesh top is approximately **0.77 m**, versus **1.49 m** before. The 17 production mesh topologies and 20-bone rig remain unchanged.
- **Near a wall or another athlete:** The slide button remains accepted. If the long box cannot be created without *already overlapping* something, it keeps a physically solid compact crouch collider/pose for the same committed slide and actual collision; the large box never teleports through a wall.
- **Get-up:** After the 0.45 s slide, the 0.20 s recovery begins only when grounded and there is actual space for the crouched vertical capsule. The motor enables the crouched capsule **before removing the low horizontal box**; both are solid while the low animation returns to a crouch. This protects the transition, and a full-height standing capsule returns only if overhead clearance is available.
- **Extremely low shelves:** If even the 1.55 m crouch cannot stand under the roof, the athlete stays low, retaining the box and the slide pose. Hold forward to crawl at up to 1.10 m/s along the committed heading until fully clear; neither reverse nor strafe can steer. Walking into open space then triggers a protected get-up. Leaving a ledge does not cause an invisible tall collider to expand midair.

The existing Space jump, Up Arrow ballistic Superman dive, role-based tag contact, keyboard bindings, human/bot motor parity, and fixed production camera remain unchanged.

## Measured Blender and Unity evidence

The rig is still the **single authoritative Chibi/Chibi_Rigged.blend** with 12 saved production animations. The Blender geometry audit evaluates the skinned vertex *shell*, not just bone locations.

| Evaluated pose / physical envelope | Height |
|---|---:|
| Old settled slide's visual mesh | ~1.49 m |
| New slide shell (including head and hands) | ≤0.773 m |
| New low physical box | 0.820 m |
| Fractional slide-recovery visual mesh | ≤1.604 m |
| Original canopy underside | 1.640 m |
| Settled slide shell floor penetration | none greater than 0.02 m |

The low recovery floor and ceiling are checked at **1/8-frame increments**, including the formerly troublesome middle of get-up. The original 17 meshes with 24,684 total source vertices have byte-for-byte equivalent vertex-coordinate digests compared with the pre-slide Blender file; see [topology proof](evidence/rig/unchanged-topology-digests.json) and [pose measurements](evidence/rig/blender-low-pose-measurements.tsv).

**Actual Unity 6000.3.26f1 tests:** The dedicated ChaseFootballSlideTests validates the solid aligned box, true momentum-only physics, locked heading under contradictory key spam, physical wall impact, nearby wall compact fallback, inability to expand beneath a roof, slow crawl and eventual get-up, no frame without an enabled solid collider, and imported production FBX mesh bounds and head/foot orientation. The existing jumping, dive, tags and animation-stuck regressions also run.

## Inspect in real WebGL

Production: http://127.0.0.1:8765/ . Separate Gym: http://127.0.0.1:8766/ (both URLs are local to the development desktop, not public websites).

In the **Gym**, press **J** to stage an actual 6 m/s tackle approaching the short canopy, then **Down Arrow**. Use **V** for the low side camera. G still stages the headfirst Superman shelf; H stages its wall impact. The HUD reads **LOW TACKLE BOX** while the real long physical body is active, together with its Animator state.

The reproducible [Chrome capture script](verify_tackle_browser.py) uses genuine browser key events to press W+Down and conflicting A/D during the slide, captures the tackle, canopy, recovery and standing frames, rejects the Unity loading splash, verifies focused inputs do not scroll, and records console errors. Pixel differences prove a rendered transition, while the physics tests and actual skinned-vertex tests prove clearance and authentic poses.

## Limitation

CHASE uses a keyed chibi skeleton, not a physical ragdoll. The **0.035 second** Animator transition from an upright run to a horizontal tackle can momentarily blend a few taller mesh frames while the physics body has already gone low. The full settled slide and recovery are verified against their collision envelopes. That very short run-to-slide blend, and subjective football-tackle impact feel, should still be evaluated by the player. No contextual vault or physics bypass has been added.

## Final verification of v0.6

The post-migration release was independently checked with **67/67 passing PlayMode tests** and **15/15 passing EditMode tests**. The saved Unity production and gym scenes explicitly contain the matching 1.98 × 0.84 × 0.82 m low body values. The `ChaseSlideSceneContract` validates both scenes before **every Player build** and fails closed on mismatches. Fresh gym WebGL, production WebGL, and Linux builds each completed with **zero BuildReport errors** and the known Unity Pipeline warning. The Linux standalone launched without reported runtime exceptions.

Chrome successfully rendered both WebGL players from a cache-disabled reload with no reported console errors. The gym [nine-stage real-browser tackle sequence](evidence/gym/football-slide-nine-stage.png) shows a tall athlete dropping to a feet-first, reclined horizontal slide, sliding beneath the canopy, rising, and running again; [the keyboard/browser report](evidence/gym/browser-tackle-verification.json) captures actual Down and direction-key events. [Production WebGL evidence](evidence/production/browser-production-verification.json) independently confirms normal movement input and earlier dive functionality. An additional [Unity Editor Play Mode screenshot](evidence/unity-editor-play-mode.png) was captured after opening `SampleScene.unity` in Unity 6000.3.26f1. The Editor was left with the production scene clean and Play Mode stopped. The source Blender rig was reopened cleanly with all 12 actions intact.

The [machine-readable release report](evidence/verification-summary.json) is the authoritative verification record for this milestone. It contains test counts, build status, both browser checks, original rig SHA-256, and screenshot counts. These checks support mechanical and asset consistency, but **subjective tackle feeling and run-to-slide blend quality must still be judged through hands-on playtesting**; no claim of artistic perfection is made from automated tests alone.
