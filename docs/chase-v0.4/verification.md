# CHASE v0.4 verification and handoff, October 11, 2026

Production source is `Chibi/Chibi_Rigged.blend`, scene is `Assets/Scenes/SampleScene.unity`, and authoritative movement details are in [`MOVEMENT_CONTRACT.md`](../MOVEMENT_CONTRACT.md). This report distinguishes passing checks from physical, artistic and editor limitations. Measurement is captured in [TSV](movement-measurements.tsv) and [calibration analysis](movement-calibration.md).

## Validated checks

| Check | Outcome | Direct basis |
|---|---|---|
| Git baseline | `main` matched remote `21a781d` before work | `git status`, `git rev-parse`, remote ref comparison |
| Blender source layout | PASS | `check_source_layout.py`, exactly one editable `.blend` under `Chibi/` |
| Blender 5.2 strict source asset contract | PASS | `validate_asset.py` on a fresh headless reopen; checks 11 Fake User clips, 17 bound meshes, 1.8 m height, -Y forward, feet at zero, evaluated crouch/slide shell clearance |
| Approved original geometry | UNCHANGED | Computed combined raw vertex/topology SHA-256 `447ea6c601e05f189f4091111121a7dc207964f5c27257bc2fcf48809880aab4` equals independent pre-v0.4 backup |
| Unity assembly compilation | PASS | Unity 6000.3.26f1, Editor ready, no compile failures |
| EditMode tests | **14 passed, 0 failed** | Includes explicit action rules, spawn separation, asset tests |
| PlayMode tests | **38 passed, 0 failed** | Includes real `Physics.Simulate(.02)`, collision rules, contact tags, navigation, role, observation, and movement calibration |
| Separate gym | PASS | `Assets/Scenes/MovementGym.unity` loaded in Unity; human-size follow camera and live motor readout; excluded from Player builds |
| Linux x86-64 Player build | **Succeeded**, 0 report errors, 1 Pipeline warning | `Builds/ChaseLinux/Chase.x86_64` |
| Linux Player launched | PASS | Actual native process/window, fresh movement screenshots and seven-second capture, character+arena+HUD visible |
| WebGL build | **Succeeded**, 0 report errors, 1 Pipeline warning | `Builds/ChaseWebGL` generated after final CSS/inline canvas fix |
| WebGL HTTP server | PASS | `127.0.0.1:8765` serves compressed `.wasm.gz` and `.data.gz` with `Content-Encoding: gzip` and appropriate content types |
| Fresh Chrome WebGL | PASS | Separate Chrome debug profile, complete Unity loading, game renders 1920×961 viewport, focused canvas, zero browser errors sampled by DevTools |
| Browser keyboard and focus | PASS | DevTools sent W/A/D/R, Space/Up/Down/Shift, several overlapping input chords; five observed Space/arrow keydown events had `defaultPrevented=true`, focused canvas, no page scroll |
| WebGL animation snapshots | CAPTURED | Arena, running, running-jump, forward leap, landing recovery, slide, crouch PNG frames. DOM keyboard/focus tests pass; captured frames supplement but do **not** replace feel-based playtesting |

Source tests and local build reports are the primary result for runtime correctness. The browser test script is [`run_browser_check.py`](run_browser_check.py) and its machine-readable outcome is [`browser-input-verification.json`](browser-input-verification.json). Run it only against a local CHASE server and a Chrome instance started with `--remote-debugging-port=9222`; it sends keyboard commands and can restart a round. For browser screenshots, see [`evidence/`](evidence/); recorded Linux play is [`linux-controls-demo.mp4`](evidence/linux-controls-demo.mp4). The original exported Blender mesh did not change.

## Actual tuning decisions

The 6 m/s continuous run covers 59.15985 m in 10 seconds. A 0.35 s leap recovery allowed repeated 9 m/s burst launches to travel 62.70250 m, a measurable exploit. After the 0.50 s grounded recovery change, repeating leap requests covered 57.11213 m, less than running. Jump apex is 0.840 m, leap travels 3.240 m, and a six-meter-per-second slide travels 1.8768 m, all from actual physics. The collision-preserving explicit action state machine replaced contextual vaults; 1.55 m low collider and 1.64 m roof underside replace unverified low canopy dimensions. Spawn pairs now start at least 8 m apart; a dedicated regression test enforces this.

## Known limitations and cautions

- **Character mesh-level clearance is not perfect.** The oversized chibi head and articulated hands/feet reach up to approximately 0.705 m horizontally beyond the actor center, wider than the 0.35 m collision radius. Transitional crouch and slide poses still intersect the floor and exceed the effective capsule silhouette. All major bounds and a conservative low-pose adjustment are documented in [`rig-clearance.md`](rig-clearance.md). Neither source geometry nor scale was altered.
- **Unity Editor diagnostic noise:** editor console records `Assertion failed on expression: 'false'` originating in `UnityEditor.BuildPipeline.BuildPlayer` on recent builds. Despite these Editor messages, both actual BuildReports **succeeded with zero reported build errors and one Pipeline warning**, and the built Players launched. These diagnostics are not presented as a clean zero-error Editor console. Recheck after a future Pipeline/Editor package upgrade.
- **Balance and polish remain subject to human judgment.** Local simulator tests validate collisions, launch physics, spam limits and repeatability, but do not prove that chase AI, landing pose aesthetics, all low-passage approaches, control comfort or browser GPU performance are ideal on every machine.
- WebGL screenshots and the browser page URL require the development machine and local server. Built distributions are intentionally Git-ignored and are not published as external hosting links.

## Continuation rules

Stay on `main`, preserve Blender -Y forward/+Z up and Unity +Z/+Y without a corrective rig rotation, retain all original 17 shell meshes, use one authoritative `.blend` and treat FBX/controller/prefab as downstream artifacts. Keep both roles' physics identical through `ChaseMotor`. Do not reintroduce contextual vault, mesh collider teleportation, unrestricted root motion or premature ML-Agents work. Always back up source assets before export, run all tests, and inspect each new build in a real window.
