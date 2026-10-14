# CHASE: WCT reference-layout trial

**Status:** Separate playable comparison scene, not a production replacement.

## Intent

This is an authored reconstruction of the uploaded chase-tag obstacle reference, translated into the current CHASE slate, cream, sky-blue, mint, coral, and gold palette. The photograph provides only relative placement. World-space dimensions are *estimates*, not surveyed World Chase Tag specifications. No official logos or brand graphics were imported.

## Implementation

- Playable comparison scene: Assets/Scenes/ArenaWCTTrial.unity, cloned from the real production Arena 01 match scene.
- Source-of-truth layout/build command: Assets/Editor/ChaseReferenceArenaBuilder.cs, Unity menu **Chase > Arena > Build Reference Trial**.
- Separate geometry guard: Assets/Editor/ChaseReferenceArenaContract.cs, Unity menu **Chase > Arena > Validate Reference Trial**.
- Reusable source-linked prefabs: Assets/Obstacles/WCTTrial/, **16 new landmarks**, with two additional certified original UnderOverRail/UnderOverBench crossings. **18 modules total**.
- Geometry features the image's rear metal frame spans, raised crown tower, right-side incline, connected tables and decks, low crossbeams, and foreground open lattice.
- Physics uses ordinary solid classified BoxColliders, with non-colliding rounded decorative meshwork and preexisting CHASE materials.
- Trial uses a static view of its own arena in menus instead of the old physical menu recording, which would conflict with the new geometry. CHASE movement and animations are unchanged.
- Production Arena 01, its nine-prefab placement authority, public Practice, the technical Gym, and the sole Blender character source are preserved.

## Verification

The trial validator checks source prefab links, deterministic placement, floor and match retention, original scene separation, renderer/solid separation, bounds, role spawn safety, and zero inter-module penetrations. Two EditMode tests check saved scene/prefab linkage and standing-body ground connectivity across the course, including bypasses. The existing PlayMode motor physics suite remains unchanged.

**Limitations:** Exact reproduction is impossible from this one perspective image. Validation does not yet establish human-versus-human balance, exhaustive slide/dive approach windows for every new landmark, or whether the current bots exploit its routes. These are reasons to keep this as a trial.

## How to play

In Unity, open Assets/Scenes/ArenaWCTTrial.unity and press Play. It retains Main Menu, difficulty, arena preview, countdown, scores, result screens, and public Practice access.

For a separate browser comparison, use the WebGL trial output in Builds/ChaseReferenceTrialWebGL. Its two scenes are the reference trial plus public Practice. It uses the existing Unity .unityweb decompression fallback. This directory is **not** the deployed production build and is not automatically uploaded to Cloudflare.

## Original reference

User-provided perspective render of an open, scaffold-heavy chase-tag quad, supplied 2026-10-14. The uploaded image was used as the arrangement reference but is not embedded into shipped assets.

## Evidence from this implementation

- Current EditMode suite: **35/35 passed** (33 pre-existing + 2 trial-specific).
- Current PlayMode suite: **103/103 passed**, without modifying the production motor.
- Trial and production Editor scene contracts: **both passed**.
- Unity Editor trial round: menu, arena selection, countdown, physical tag, and results observed.
- WebGL trial player: **Succeeded**, 0 errors, 1 known optional Pipeline runtime configuration warning; 17 deployment files using `.unityweb` fallback.
- Real Google Chrome browser flow: Main Menu > difficulty > Arena Trial preview > competition start > advancing round. **0 JavaScript exceptions, 0 browser errors**. See evidence/webgl/verification.json and screenshots.
- Edited Scene View illustration: evidence/editor/arena-overview.png.

The previous production WebGL output and public Cloudflare Pages deployment were not touched. The comparison player is local and requires the user's approval before any public deployment.
