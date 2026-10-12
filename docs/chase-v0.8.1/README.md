# CHASE v0.8.1 — smooth, floor-anchored Superman get-up

This release fixes the sudden change in position and facing after a chest-first landing. The simple v0.8 dive, four-direction prone crawling, football slide, tags, and automatic get-up rules remain intact.

## Root cause

The previous prone-to-get-up transition immediately assigned a fixed world Y position of 0.90 m to the Rigidbody, reset its rotation to identity, and restored the visual child rotation without preserving the actual world facing. That teleported the player/camera reference and produced visible jumps, especially on raised surfaces and dives launched sideways. The approved Blender LeapRecovery clip already starts prone and ends low; the problem was primarily the physics/visual transition.

## Fix

1. At the moment get-up clearance becomes available, derive actual surface height from the bottom of the prone capsule. Test whether the complete vertical crouch collider can fit at the destination.
2. Convert the same solid CapsuleCollider to the upright crouched configuration with its bottom exactly at the previous contact height. Do not instantly change the Rigidbody origin or visible character world pose. Preserve visual position and orientation using the physical Rigidbody pose (safe with interpolation).
3. Over the existing 0.35-second LeapRecovery state, ease the Rigidbody's reference height toward its standing basis using a zero-velocity-endpoint smoothstep curve. Continuously compensate capsule.center so its bottom remains floor-anchored. Smoothly ease the visual child offset and orientation to its normal attachment.
4. Save and use the real committed dive yaw, including simultaneous WASD + Up launches, instead of a stale earlier facing direction.
5. Blend the real production Animator into LeapRecovery over 0.10 seconds, then blend toward ordinary standing/run/crouch over 0.14 seconds, allowing the authored pose to change without instant bone jumps.

No new physical body, permanent prone state, ragdoll, auto-backwards crawl, or shelf-specific escape rule was added. The same solid, non-trigger capsule remains active.

## Regression testing

Real Unity 6000.3.26f1 physics tests cover:

- Three launch headings: no abrupt Rigidbody-height change or visual world-position/yaw change at the first get-up frame; small per-20-ms vertical increments, consistent facing, stable capsule feet.
- A real 0.55 m raised platform: the athlete recovers to the correct elevated standing height without warping toward world-ground height.
- The actual imported, skinned production rig: the animated head advances continuously through physical prone recovery and transition back to standing.
- A genuinely low solid shelf: sideways crawling to an exit triggers get-up without instantaneous position or rotation change.
- Existing shelf approaches, wall impacts, opposing-player tagging, low-speed prone WASD, the rounded Superman projectile, and the horizontal football tackle.

The unchanged Blender file is the sole approved source, with 13 validated animation actions and 17 original mesh parts; neither the rig nor imported FBX was modified for this fix.

## Browser and native validation

Movement Gym: http://127.0.0.1:8766/ on the development desktop. Press R for open ground, Up for a Superman dive, V for the side view; K stages the tight shelf, J stages the football slide. Production: http://127.0.0.1:8765/.

A reproducible real Chrome script captures 30 consecutive rendered frames across the open-floor dive, landing, low kneeling recovery and standing. See the 12-stage contact sheet at [smooth get-up sequence](evidence/gym/smooth-getup-contact-sheet.png) and the critical [eight consecutive recovery frames](evidence/gym/getup-eight-critical-frames.png). Unlike static animation tests, these show the actual physics motor and production WebGL rig.

Additional independent real-browser replays check [shelf crawl](evidence/gym/browser-prone-crawl-verification.json), [football sliding tackle](evidence/tackle/browser-tackle-verification.json), and [production movement](evidence/production/browser-production-verification.json). All rebuilt players use the validated production and gym scenes.

## Qualification

Per-limb ragdoll physics is not used; the visual pose interpolates while a single, already-clear crouch capsule remains physically solid. The automated tests and browser frames establish continuous movement for the tested open-floor, raised-platform, sideways-heading and low-shelf situations; subjective motion feel in every imaginable obstacle arrangement still requires hands-on play.

## Final verified release — October 12, 2026

The independent post-fix Unity test suite passed **78/78 PlayMode and 15/15 EditMode tests**. The new cases explicitly check unbroken world-space visual position/yaw at get-up, frame-by-frame upright elevation, physical foot-ground contact throughout interpolation, a genuine raised platform, actual imported-rig head motion across the animation transition, and smooth standing after crawling out of a low shelf.

Both fresh WebGL Players and the Linux standalone succeeded with **zero BuildReport errors** and one existing Unity Pipeline warning each. The combined movement-scene validator passed for both saved scenes in all three builds. Four fresh Chrome replays (dense get-up sequence, low shelf crawl, football tackle, and production movement) reported **zero browser console errors**. The native Linux Player launched without reported runtime exceptions.

Following the editor restart, Unity 6000.3.26f1 was reopened through the terminal, `SampleScene.unity` loaded cleanly, Play Mode was entered and exited successfully, and a [real editor Game View screenshot](evidence/unity-editor-play.png) was captured. The editor is left open on the production scene with Play Mode stopped; Blender remains closed as requested. All 13 Blender actions and all 17 original mesh objects passed strict headless validation without source changes.

[Machine-readable release report](evidence/verification-summary.json) | [Dense 30-frame Chrome capture report](evidence/gym/browser-smooth-getup-verification.json) | [Eight critical transition frames](evidence/gym/getup-eight-critical-frames.png) | [Full get-up contact sheet](evidence/gym/smooth-getup-contact-sheet.png)

The combination of Unity physical invariants, actual imported-rig frame checks and screenshots establishes continuous motion in the tested cases. As with any authored character animation, subjective quality across every imaginable contact position still benefits from live playtesting; the implementation intentionally does not add ragdoll or collision bypasses.
