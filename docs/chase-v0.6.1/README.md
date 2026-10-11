# CHASE v0.6.1 — Superman dive cannot remain prone at a shelf edge

## Observed failure

The actual user-opened Movement Gym (`http://127.0.0.1:8766/`, previous build `bfea217`) showed **`STATE: Leaping`, `SPEED: 0.00`, `VY: 0.00`, `GROUNDED: False`, `BODY: DIVE BOX`**, with the chibi lying horizontally near the dive shelf. This is a stuck physical **state machine**, not merely an Animator blending mistake. Resetting/restarting used to be the only dependable escape.

## Root causes and minimal behavior fix

1. **Missed ledge support.** The old `DetectGround` cast a small downward sphere only below the **center** of the horizontal 2.30 m-long collider. If the *front* was supported by the shelf sill but the center was over open space, a resting collision produced **no grounded transition**. The new detector checks **center, front, and rear** of the true solid body and also recognizes actual upward PhysX contact normals. It rejects triggers, teammates/other athlete bodies as "ground," and support contacts cached at a different actor position (such as after teleport/reset). Its probe tolerance remains tight; it must not turn genuine air steering into ground acceleration.
2. **Motionless impact could stay `Leaping` forever.** A real forward collision stops the projectile instantly. If that contact yields zero horizontal and vertical velocity but the ground probe is unable to classify the geometry, after an actual wall hit and a short 0.18-second settling interval the motor now **enters the same prone impact/recovery path**. It does not reapply launch velocity, steer, pass through shelves, or grant early free movement.
3. **Repeated impossible get-up.** The old `DiveProne` get-up applied a 2.8 m/s upward velocity to lift the **still-enabled 2.30 m horizontal box** until the standing origin reached 0.90 m. Physical contact could repeatedly cancel this velocity or projected headspace could reject the rise, leaving recovery waiting at the same point forever. The new get-up checks the **full prone-box vertical sweep** and destination crouching capsule *before* raising the actor the small required height and creating the **solid capsule+box union** for the existing 0.35-second recovery animation. No floor, shelf, wall, or other player collision is disabled to force the lift. The 0.40-second impact commitment remains.
4. **If the current point has no clearance**, a completed impact now gently **recoils backward along its original facing direction** under the real rigidbody/PhysX instead of waiting motionless. A player intentionally holding forward can still perform the slow crawl under an opening. Both are physically blocked by solid obstacles, never teleported through them. The same logic applies to open-space full-box and cramped-approach compact-body dives.

This preserves the v0.6 **feet-first football slide** and its saved Blender animation, the headfirst Superman flight, and both competitor roles' identical physics. **No production rig geometry or animation was modified.** There is no timer that arbitrarily frees a truly physically enclosed character by phasing through a wall; the motor instead makes physical recovery progress when a route exists.

## Regression proof

Unity 6000.3.26f1 real `Physics.Simulate(0.02)` PlayMode tests include a faithful G-key gym shelf recreation and a second **seven-distance shelf approach sweep** (starting 0.7–2.4 m along the gym's shelf lane). These tests require the athlete to leave `Leaping`, complete `DiveProne` and `LeapRecovery`, restore the normal capsule, and finish standing without any button input during 7.2 seconds. The original 100-match stress, opponent tag parity, jump/slide, grounded air steering and wall-contact tests also remain in the full suite. **69/69 PlayMode tests and 15/15 EditMode tests passed** after the final fix.

The script [`verify_recovery_browser.py`](verify_recovery_browser.py) uses real focused Chrome keyboard events and a new cache-disabled WebGL load, repeats the **G shelf** and **H point-blank wall** situations **twice each**, and captures launch, impact, and the player after 7 seconds without input. Its [16 actual browser screenshots](evidence/) and [machine-readable Chrome log](evidence/browser-recovery-verification.json) report no browser console errors. The captured 7-second states were manually inspected as **`Standing`, `Grounded: True`, `BODY: CAPSULE`, visual `Idle`** rather than simply trusting image differences.

### Limitations

Collision envelopes remain approximate rather than multi-joint ragdoll geometry. The clearance-checked vertical adjustment at get-up is deliberately small and occurs only after an actual collision and full impact commitment; it should not be mistaken for a new jump impulse. An entirely sealed enclosure with no exit cannot be escaped physically without breaking collision, and CHASE intentionally does not phase characters through solid walls. The fix addresses realistic shelf and wall collisions with room to retreat or rise.

## Final release checks — October 12, 2026

- **Unity PlayMode:** 69 passed, zero failed, zero skipped; includes the seven-distance no-input shelf recovery sweep, the original gym shelf, and the human/bot movement, tag, jump, slide, and air-control regressions.
- **Unity EditMode:** 15 passed, zero failed, zero skipped.
- **Fresh rebuilt standalone players:** Movement Gym WebGL, production WebGL, and Linux desktop all **Succeeded**; zero BuildReport errors and one existing Unity Pipeline warning per build.
- **Chrome:** real canvas-focused key events, live Unity scene rendering, zero browser console errors in both the repeated gym recovery and production movement smoke. The four repeated G/H 7-second aftermaths were inspected in a composite [nine-stage audit image](evidence/recovery-nine-stage-audit.png), and showed standing, grounded, ordinary capsule bodies rather than the former stuck prone state.
- **Linux native player:** rebuilt player launched in its own native window, with no reported runtime exceptions or missing assets.
- **Blender:** strict validation passed again, 12 animation actions, original 17 meshes, unchanged topology/source file. Both authored Unity scenes and production Animation Controller remained unchanged.
- **Release evidence:** [machine-readable checks](evidence/verification-summary.json), [browser keyboard/console log](evidence/browser-recovery-verification.json), [production WebGL report](evidence/production/browser-production-verification.json), and 16 browser screenshots of G/H launches and recovery.

The tester browser now loads a fresh v0.6.1 Gym at `http://127.0.0.1:8766/`; **G** stages the successful/difficult shelf lane and **H** stages the direct wall. Both now return to standing after a failed or completed dive, without a reset. Source and rebuild outputs are committed separately; local `Builds/` binaries remain Git-ignored.
