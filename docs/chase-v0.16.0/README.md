# CHASE v0.16.0: Aligned camera and fixed role starts

## Player-facing changes

CHASE remains **third person**, but both playable camera orientations now align with the square arena. Competition uses **55° pitch, 0° yaw**, 40° perspective field of view and the existing smooth positional follow/full-arena occlusion system. Practice uses **53° pitch, 0° yaw** and its closer follow. W maps to world +Z and D to world +X through the unchanged camera-relative ChaseKeyboard logic, so straight obstacles no longer require an unnecessary diagonal input.

The real match camera reapplies its fixed orientation whenever gameplay starts or the player returns from the decorative cinematic menu. The arena-selection preview uses that aligned orientation. The menu's passive recorded showcase may still use cinematic framing; it is not a control view.

Every round, rematch and role swap now has the same side entry **for each role**, with no random seed or rotating spawn pair:

- Chaser: west side, coordinates (-5.10, 0.90, -2.10).
- Evader: east side, coordinates (5.10, 0.90, 2.10).

These starts belong to roles, never to the human or bot. The east start was offset to avoid the actual OffsetGates solid structure. Thin, color-coded markings in the production scene identify both starting places without adding colliders, triggers, altered geometry or new gameplay rules. Source-of-truth marks can be rebuilt via **Chase → Arena → Refresh Fixed Side Entries**, and a new release gate verifies marker locations and nonphysical construction.

### World Chase Tag clarification

The 2026 WCT rulebook requires a fixed **Chaser Plate**, but allows the evader to position freely within the Quad before the chase (Articles 1.2.4 and 1.3.1). CHASE deliberately starts both roles at designated opposite sides **for now**, so practice and repeated opening strategies stay comparable. This is CHASE's rule, not a claim that WCT mandates a fixed evader position. Source: https://www.worldchasetag.com/the-rulebook

## Authority and verification

- Original Blender source, all certified Arena 01 obstacle prefabs, geometry, movement tuning, dive/slide physics, AI navigation code and the technical Movement Gym remain unchanged.
- Isolated Unity 6000.3.26f1 CLI: **99 PlayMode and 33 EditMode tests passed**. New tests assert world-axis alignment after menu transitions, fixed role starts across multiple rounds, and Practice camera alignment.
- Full arena framing tests pass for wide, 4:3, square and portrait aspect ratios. Existing arena route, bot navigation and 100-match physics stress tests pass.
- Production WebGL, independent technical Gym WebGL, and native Linux builds all succeed with **zero build errors**, including the fixed-entry, production-scene camera, Practice, obstacle and animation contracts.
- Actual Chrome clicks and keyboard navigation cover How to Play → Practice → moving forward → Menu → difficulty → aligned Arena 01 selection → match → pause → menu, without browser errors. Evidence: [browser verification](evidence/webgl/verification.json), [practice](evidence/webgl/02-free-roam.png), [arena preview](evidence/webgl/10-arena.png), [match countdown](evidence/webgl/11-countdown.png).
- Native Linux starts and renders the public menu. [Native screenshot](evidence/native/linux-menu.png). This checks the native render and startup, not synthetic mouse interaction.
- Machine-readable [release verification](evidence/release-verification.json) and reproducible Chrome script verify_aligned_camera_browser.py are included.

## Deliberate limits

This is not a first-person camera, a freely rotating chase camera, or a tighter over-the-shoulder view. The fixed elevated camera preserves a wide view of both athletes and their possible routes. Start placement is intentionally consistent; a future evader-selection phase would require separate design and playtesting. The final sense of control and camera zoom still need hands-on feedback.
