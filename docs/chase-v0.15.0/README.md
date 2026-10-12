# CHASE v0.15.0: Public Practice Playground

## Player flow

**Main Menu → HOW TO PLAY → ENTER PRACTICE** opens an actual playable training area. The existing controls diagram remains visible on How to Play. Practice is entirely optional and the normal PLAY → Difficulty → Arena 01 flow is unchanged.

In practice, there is one real Chibi, **no pursuer, no round timer, no score, and no compulsory challenges**. Players can explore the freely connected obstacles, repeat moves, test whether the same obstacle can be solved over or under, or exit at any time.

The unobtrusive practice HUD shows a station name, compact translucent visual keycaps, and **NEXT [TAB]**, **RETRY [R]**. ESC opens Pause with RESUME, RETRY and MAIN MENU. Keyboard control of the pause choices is supported with Up/Down, Tab, Enter, Space and Escape. A retry resets the real motor at a safe marked practice starting pad, not partway through a physics action. The character is not teleported automatically by reaching an obstacle.

## Four repeatable starting places

- **OPEN FLOOR:** unrestricted movement and recovery, near a low hurdle.
- **OVER OR UNDER?:** start on the approach to the certified production UnderOverRail.
- **BENCH ROUTES:** try different approaches to the smaller production UnderOverBench.
- **LINK YOUR MOVES:** experiment with a low deck, turning frame, and aerial slot.

The playground is an intentionally authored 24 × 24 m floor with solid perimeter guards, painted non-colliding floor indicators, and six production obstacle prefabs: UnderOverRail, UnderOverBench, LowHurdle, LowDeck, TurnFrame and AerialSlot. There is no randomly generated layout and no hidden move-specific instructions on the obstacles.

## Technical authority

The playable scene is `Assets/Scenes/PracticePlayground.unity`, authored by `ChasePracticePlaygroundBuilder` and verified by `ChasePracticePlaygroundContract`.

- Its character is the authoritative `ChibiVisual`, instantiated with the same `ChaseMotor`, `ChibiVisualDriver`, `ChaseKeyboard`, body dimensions, action rules and `ChaseTuning` as the actual match.
- The obstacle instances are **real links to the production prefabs**, with unit scale and verified placements. No alternate collision approximations, trigger-only routes or animation shortcuts exist.
- A dedicated local camera follows the character at human scale, using the same fixed 45-degree world-input basis as the game. No enemy bot or scoring authority is instantiated in practice.
- The independent, measurement-heavy developer `MovementGym.unity` is deliberately **preserved unchanged**, including its debugging overlays and deterministic measurement shortcuts. Practice is not built from that debug scene.
- The production Unity player and the editor build-scene list now contain `SampleScene.unity` and `PracticePlayground.unity`. The independent Gym build contains its original separate technical scene, and all three builds use the same release-time geometry, animation and physics contracts.

The sole approved Blender source `Chibi/Chibi_Rigged.blend` was not altered. The original Arena 01 and character movement implementation were not redesigned.

## Verification

Independent Unity 6000.3.26f1 CLI:

- **97/97 PlayMode** and **33/33 EditMode** tests passed. New tests check the saved practice prefab source links, pad clearance for the large Chibi head, authentic shared motor, station reset and pause behavior, and real PhysX dive-over and slide-under routes through both authored crossings.
- Production WebGL, technical Movement Gym WebGL and native Linux builds all returned **Succeeded, zero errors**, including the new public-practice contract.
- Real Chrome WebGL replay navigates HOW TO PLAY → ENTER PRACTICE, moves the character, switches station with Tab, retries with R, pauses with Escape, returns to the main menu, and launches a normal chase match afterward. It checks actual visual changes and no browser console exceptions. See [browser report](evidence/webgl/verification.json), [how-to](evidence/webgl/01-how-to-practice.png), [open floor](evidence/webgl/02-practice-open-floor.png), [over/under station](evidence/webgl/04-practice-rail-station.png), and [pause](evidence/webgl/07-practice-pause.png).
- The native Linux player launched and rendered the public CHASE main menu without runtime exceptions. Its practice-menu mouse interaction is not independently verified through native synthetic input; browser practice interaction is verified.

The browser script `verify_practice_browser.py` uses genuine Chrome pointer and keyboard events, not Unity method calls. A reproducible summary is in `evidence/release-verification.json`.

## Boundaries

This is a small free practice playground, not a mandatory tutorial campaign, achievement system, multiplayer lobby, or alternate competitive arena. Players discover routes by trial and error. No audio, ghost recording, coach bot or new movement ability was introduced. Human observation and further platform checks remain valuable before wider release.
