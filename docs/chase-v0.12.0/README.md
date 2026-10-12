# CHASE v0.12.0: streamer-focused public UI

## Player experience

**Main Menu → Opponent Difficulty → Arena 01 / Mastered Routes → Play Arena → Countdown → Chase**

The actual Arena 01 remains visible behind the menus. The public menu is not an automatic match countdown. How to Play presents visual keycaps; Settings offers functional Show Keys and Highlight Keys toggles. Desktop has Quit, while WebGL hides Quit.

Four distinct bot profiles use the same real physics and collision rules:
- Rookie: lower movement input and slower replanning/decisions.
- Runner: original baseline movement and planning cadence.
- Rival: baseline physical speed, faster planning and more anticipation.
- Phantom: baseline physical speed, most frequent planning and greatest anticipation.

Profiles are genuine control-policy changes, not guarantees of competitive skill. Human playtesting is needed to calibrate them.

## Streamer HUD

Active gameplay shows only a compact Chaser/Evader score strip, round, timer, YOU role indicator, and translucent visual keycaps:
- WASD: Move
- SPACE: Jump
- Up arrow: Dive
- Down arrow: Slide
- SHIFT: Crouch

The arrow icons are tiny self-contained raster textures because Unity WebGL's default font failed to display Unicode arrows. No imported font, texture dependency, external source, extra UI manager or new collider was added.

The countdown says 3 / 2 / 1 and briefly CHASE! on activation. Pause is Resume / Restart / Main Menu. End-of-round says TAGGED! or ESCAPED! with Next Round, Rematch, Swap Role and Menu. Copy is brief and viewer-readable. A simple nine-sliced soft-corner visual system uses dark slate, cream, mint, coral, gold and blue matching the stylized arena. GUI styles are cached.

## Authority and compatibility

- The production ChaseMatch serializes showFrontEnd=true. That setting is checked by the existing movement-scene contract on every release build. Test-created matches retain automatic start, preserving physics regressions.
- Menu screens do not advance countdown or physics. Selecting Arena 01 starts the normal countdown, resets scores and roles, and applies the selected bot policy. Menu returns unpause the game and clear input.
- All original core physics, Chibi Blender rig, collision measurements, animation bindings, Arena 01 placements and standalone Movement Gym remain unchanged.
- The WebGL browser title is CHASE | Mastered Routes. Unity project metadata and version are deliberate, and native desktop windows are resizable.

## Verification

- Independent Unity 6.3 LTS CLI: **89/89 PlayMode** and **31/31 EditMode** tests passed, including four new front-end tests for menu state, genuinely different bot behavior, settings, pause/resume, next round, rematch and role switching.
- Production WebGL, Movement Gym WebGL and Linux native builds: **Succeeded, zero BuildReport errors**. The pre-existing Unity Pipeline package warning remains non-blocking.
- Real Chrome proof uses actual pointer and keyboard events, not Unity SendMessage, to visit the rendered menu, controls, settings, difficulty, arena, countdown, active game, pause, resumed game, return menu, round result and next round. The replay waits until the actual CHASE logo appears, not merely until Unity's loading bar disappears.
- Evidence: [main menu](evidence/webgl/00-main-menu.png), [difficulty](evidence/webgl/04-difficulty.png), [arena](evidence/webgl/05-arena-select.png), [HUD](evidence/webgl/07-active-hud.png), [pause](evidence/webgl/09-pause.png), [result](evidence/webgl/12-round-result.png), [browser interaction report](evidence/webgl/verification.json).
- Authoritative Blender file was not edited.

## Scope limits

This is public-facing **UI production polish**, not a finished multiplayer or competitive release. It does not add networking, sound, analytics, replay, extra arenas or new movement states. Human-facing bot difficulty and streamer retention still require external playtests.
