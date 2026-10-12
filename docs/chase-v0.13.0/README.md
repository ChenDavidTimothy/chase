# CHASE v0.13.0: Recorded arena chase main menu

## Design decision

First launch should demonstrate what CHASE *is* without making a player read or start a match. The main menu and difficulty / How to Play / Settings screens now keep an **opaque, centered, accessible menu card** while two full Chibi characters perform a faint, authentic, recorded chase along the left and right margins. The card remains the visual focal point. No timers, scoreboards or development telemetry appear over the footage. A small `RECORDED CHASE` caption distinguishes it from live gameplay.

**Main Menu → Opponent Difficulty → Arena 01 → Play Arena → real countdown and match.** Difficulty and general menu pages share the looping demo. Arena selection **stops it** and shows a clearer, static, unobstructed Arena 01 preview beside its left-aligned selection card. Play Arena hides the replay, restores the normal gameplay camera and activates the real match competitors.

## Authentic recorded movement without simulation

`ChaseMenuReplayBaker.cs` authors exactly three scripted highlight segments **offline**, driving two actual `ChaseMotor` physics bodies with the official collider, gravity, obstacles and locomotion speeds. It samples position, physical body facing, movement/animation state and pose phase at 50 Hz. A recorded action includes the real central **dive**, an authentic feet-first **slide** through the west bench, and evasive turns around the course. Cuts occur **between** clips, like a video editor; there are no illegal mid-shot teleports. Nothing in the approved Blender rig is modified.

To regenerate the recording deliberately, open `Assets/Scenes/SampleScene.unity` in Unity Edit Mode and run **Chase → Bake Real-Physics Menu Showcase**. It re-simulates the three exact, 50 Hz real-motor shots and fails if dive/slide content disappears. Regenerate the menu still from that same arena whenever a future arena redesign invalidates the image; this still is decorative reference artwork, not a collider or level definition.

`Assets/Resources/MenuChaseReplay.bytes` is the small compiled binary recording (approximately 31 KiB). `Assets/Resources/MenuArenaStill.jpg` is an actual production-arena still (approximately 87 KiB), shown at low opacity in the opposite background margin. This fills empty negative space without rendering a second camera or adding moving gameplay objects.

At runtime `ChaseMenuShowcase.cs` reads and validates the recording, instantiates **two renderer/Animator-only ChibiVisual prefabs**, and samples the recorded frames. No Rigidbody, Collider, `ChaseBot`, navigation, action source or ongoing physics simulation is created for the decorative showcase. The real gameplay competitors are deactivated while the front end is open; in Arena preview both the real competitors and replay actors are hidden. The menu camera follows recorded footage with cinematic left/right offsets and a brief masked cut between segments, independent of the normal in-match camera. Returning to Menu restores the demonstration; leaving the menu stops rendering it. Both menu actors use existing approved animation states and normal chaser/evader colors.

### Performance contract

- One tiny binary recorded source, one compressed static image, existing ChibiVisual art, no video decoder, no streaming or external network dependency, no background AI or duplicate live physics bodies.
- All clips use the shared existing 13-action character controller. No new animation asset or alternative Blender source.
- Playback runs at 50 sampled pose updates per second, with no per-frame GC-heavy data loading and no dynamic physics queries.
- The static arena preview does not continue recording playback.
- `ChaseMenuReplayContract.Validate` runs as part of **every** official release build and verifies shot count, 50 Hz frame envelope, valid finite poses, in-bounds coordinates, plausible per-frame displacement, authentic dive/slide/recovery content, compressed file budget and present image dimensions.

## Evidence and tests

- Dedicated Unity tests assert the menu requires explicit Play, countdown remains frozen, normal character colliders are inactive, replay characters have two Animators but **zero colliders, rigidbodies or bots**, and the replay disables/enables correctly on difficulty, arena select, real match launch, and return to menu.
- **91/91 PlayMode and 31/31 EditMode** tests pass in isolated Unity 6000.3.26f1 CLI, including a new test that the 14.94-second three-shot replay loops to its original position without drift. The full original physics/arena suite runs alongside the UI tests. The independent production WebGL, Movement Gym WebGL and native Linux builds use the complete scene, Gym, animation and new menu-recording contracts.
- **All three builds succeeded with zero build errors** (production WebGL, independent Gym WebGL, Linux native); each passed the menu recording, geometry, Gym and movement contracts. The existing Unity Pipeline package warning is non-blocking.
- A real Chrome WebGL reproduction script in `verify_menu_showcase_browser.py` drives actual clicks and keyboard input. It captures the centered menu before/after recorded motion, checks that the arena preview stays static, follows menu/difficulty/arena/playing/pause/results/return sequence, and checks that the menu recording resumes after returning. See [browser report](evidence/webgl/verification.json), [menu](evidence/webgl/00-main-menu.png), [moving chase](evidence/webgl/00a-showcase-moving.png), and [static arena preview](evidence/webgl/05-arena-select.png).
- Browser evidence measures **91,966 changed pixels** in the menu's recorded action over 0.85 seconds, **0 changed pixels** while the Arena screen is static, and **76,652 changed pixels** after returning from gameplay to the menu; 15 real pointer interactions, no browser console errors or JS exceptions.
- Native Linux player renders the menu and loops its footage (more than 680,000 changed pixels in two captures two seconds apart) with no runtime errors. X11 synthetic pointer injection did not activate the native menu, so that specific interaction test is **inconclusive**; WebGL pointer navigation is independently verified. See [native menu](evidence/native/00-menu-native.png) and [native moving loop](evidence/native/01-menu-moving-native.png).
- Production Editor captures: [recorded menu](evidence/editor/main-menu-recorded-action.png) and [static arena preview](evidence/editor/arena-preview-static.png).

## Not claimed

This is a **pre-recorded, scripted physics replay**, not an actual AI match running in the background. Two recorded actors move from authentic collision simulations, but no live decisions are made during menu playback. Its purpose is a truthful taste of real movement with a controlled presentation and low runtime cost, not a substitute for competitive human playtesting. Sound, networking, alternate arenas and progression are outside this UI-focused milestone.
