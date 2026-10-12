# CHASE v0.14.0: stability, fairness and replay correction

This correction cycle retains the authoritative Blender character, Arena 01's measured physical obstacles, locomotion primitives, and the centered public menu. No physics or Blender authoring was changed.

## Confirmed issues resolved

1. **Player and rival score attribution.** The broadcast scoreboard now tracks YOU and RIVAL across role swaps. CHASER and EVADER win counters are retained separately for existing regression tools; the player's current role is always visible.
2. **Unexpected shortcuts removed from the public game.** Pressing R cannot silently restart a round or bypass Pause. Tab no longer silently changes roles at results. Explicit Pause, Restart, Swap Role and Next Round buttons still work.
3. **Focus-loss safety.** Switching away from the app pauses an active chase or countdown, clears held inputs, and never resumes automatically when focus returns. A menu showcase stops advancing in the background.
4. **Keyboard menu navigation.** Arrow keys or Tab cycle visibly highlighted options, Enter/Space activates, Escape backs out or resumes. The existing mouse buttons still work. No second UI manager or game action system was introduced.
5. **Approved animation timing.** The replay baker stores seconds but Animator.Play expects normalized clip time. Runtime playback now divides by the actual approved Blender clip durations and clamps finite recovery motions.
6. **Honest replay shots.** The physics baker rejects footage in which the athletes approach closely enough to imply a tag. The release contract independently checks every sample. Three approximately 3.38-second shots demonstrate real dive, slide, and routes with credible separation. They remain lightweight, visual-only recordings at runtime.
7. **Persistent settings.** Preferences explicitly flush via PlayerPrefs.Save and survive real WebGL browser navigation/reload.
8. **Mobile compatibility.** Unsupported Android/iOS touch-only visitors see a clear keyboard-required screen instead of loading an unusable Unity player.
9. **Correct release metadata.** Unity bundle version now reads 0.14.0.

## Evidence

Independent Unity 6000.3 LTS CLI: **95/95 PlayMode, 31/31 EditMode** tests pass. WebGL production, independent Movement Gym WebGL and native Linux release builds succeed with **zero errors**. All release-time movement, rig, Gym, Arena 01 and replay geometry contracts pass.

Real Chrome WebGL verifies the full 15-click public menu flow plus a keyboard-only Play → difficulty → Arena → countdown path; the disabled R shortcut leaves Pause unchanged, settings persist after an actual reload, and Android user-agent simulation displays the intentional compatibility message with no Unity canvas. Chrome reports no JavaScript exceptions or browser-console errors. The three-shot recorded menu moves, while the Arena select preview reports zero motion.

Native Linux renders the corrected menu and moving showcase without runtime exceptions. Native synthetic mouse injection remains inconclusive and merits a manual check.

See evidence/release-verification.json and evidence/webgl/verification.json, and reproduce Chrome checks with verify_stability_browser.py. Key images: evidence/webgl/00-main-menu.png, evidence/webgl/07-active-hud.png, evidence/webgl/14-focused-play.png, evidence/webgl/18-paused-before-r.png, evidence/webgl/22-settings-persisted.png, evidence/webgl/23-mobile-unsupported.png, and evidence/native/00-main-menu.png.

## Source-of-truth workflow

To regenerate the recording, open Assets/Scenes/SampleScene.unity in Unity Edit Mode and select **Chase → Bake Real-Physics Menu Showcase**. Rejection of insufficiently separated or physically invalid recorded action is intentional. Only Assets/Resources/MenuChaseReplay.bytes stores the approved recording. The Arena still is decorative and does not replace real obstacle geometry. Never duplicate or modify the authoritative Chibi/Chibi_Rigged.blend merely to polish UI.

## Still outside this milestone

No audio exists yet, no human-versus-human networking, and the difficulty presets have not been calibrated against human skill. Long-session low-end device FPS, real public hosting, Safari, and comprehensive native keyboard/mouse testing are not yet certified. Firefox's sandbox launcher failed in this environment, so no Firefox compatibility claim is made. These are future release-gating checks, not grounds to remodel the tested movement system.
