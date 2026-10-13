# CHASE v0.15.1: Free-roam Practice

The public playground is now a **single continuous space**. It is not a set of tasks, checkpoints, retry stages, or a sequence of prescribed solutions.

## Player-facing behavior

- Main Menu → How to Play → Enter Practice starts the normal physical Chibi in the open playground, with its existing certified obstacles available to explore in any order.
- No `NEXT`, `RETRY`, station name, station switching, or `R`/`Tab` shortcut remains. The player's position, momentum, recovery state and chosen route persist while practicing.
- A clearly visible **MAIN MENU** button is always at the upper-right during free play. It returns directly to the existing main menu without requiring Pause.
- Escape opens a minimal two-option pause screen: **RESUME** or **MAIN MENU**. Mouse and keyboard selection are supported. A focus-loss pause still requires explicit resumption.
- The visual movement keycaps remain as optional reminders. No timer, score, bot, tutorial progression or speed telemetry exists in the practice experience.
- Only an actual out-of-bounds fall can reset the character to the initial safe spawn, as a noninteractive recovery safeguard.

## Source and design integrity

Removed the three additional start pads, all colored checkpoint marks and the painted run-up lanes from the saved scene and its rebuild code. The stage-free 24 × 24 m floor retains all six **real production** obstacle prefabs, original colliders and meaningful multiple-route geometry. The certified production motor, tuning, imported Blender Chibi, Arena 01 and independent developer Movement Gym remain unchanged.

`ChasePracticePlaygroundContract` now rejects any saved scene that reintroduces staged checkpoint pads or approach markings. Tests verify one clear player spawn, prefab validity, real physical dive/slide crossings, and position/velocity retention through pause/resume.

## Verification

- Full isolated Unity 6000.3.26f1 tests: **97 PlayMode + 33 EditMode passed**.
- Official release checks: production WebGL, technical Gym WebGL and native Linux builds; zero build errors and all existing movement/rig/arena/practice contracts.
- Real Chrome browser: How to Play → Practice, physical free movement, removed `R`/`Tab` actions, direct MAIN MENU exit, keyboard selection of pause exit, return to normal gameplay, no page exceptions.

See [verification summary](evidence/release-verification.json), [browser results](evidence/webgl/verification.json), [free-play screenshot](evidence/webgl/02-free-roam.png), and [pause screenshot](evidence/webgl/07-pause-resume-exit.png). The Chrome script is `verify_free_roam_browser.py`.

Native practice clicking and broader browsers still require manual platform validation; this change is limited to the already-tested WebGL/Unity practice flow.
