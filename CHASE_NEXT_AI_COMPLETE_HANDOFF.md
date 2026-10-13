# CHASE: COMPLETE DEVELOPMENT HANDOFF FOR THE NEXT AI
**Revision:** 2026-10-13, after CHASE v0.17.1 and Cloudflare Pages setup
**Purpose:** Give a fresh AI the product intent, latest verified code and repository state, physics/animation/arena architecture, implementation history, release operations, prior mistakes, and future boundaries. This document supersedes the previous handoff dated 2026-10-12 (which stopped at v0.8.1). It is working context, **not permission to start a new feature or redesign the game**.

> **For the incoming AI:** You are taking over a functioning, iteratively playtested Unity game, not a greenfield prototype. Use the user's connected **Terminal, Unity, GUI, and Blender MCP** tools. First inspect the real project and Git state, then follow the user's NEW request. Respect the single authoritative Blender character source, the tested physical movement feel, the deliberately authored Arena 01, and the goal of simple, emergent, streamer-visible competitive chase. Do not rebuild existing systems merely because you have tools. Distinguish verified facts, historical records, personal playtest judgments, and still-unimplemented intentions.

---

## 0. Five-minute orientation: newest verified state

- **Name:** CHASE. Keyboard-first, third-person, World Chase Tag-inspired 3D chase/tag game with stylized/chibi competitors and real Unity PhysX traversal.
- **Engine:** Unity **6000.3.26f1**, project at `/home/dtdav/dev/chase`; Unity executable `/home/dtdav/Unity/Hub/Editor/6000.3.26f1/Editor/Unity`.
- **GitHub:** `https://github.com/ChenDavidTimothy/chase.git`, branch **`main`**.
- **Last fully verified gameplay/source release commit:** **`20e947322edffedfbefd032d99f59640bb54413f`** (short `20e9473`), **“Remove decorative floor grid from Arena 01”**, pushed to `origin/main` on 2026-10-13. This handoff is documentation-only and may itself be committed afterward, so the next Git HEAD may be newer. Working tree was clean at release inspection. Recheck it.
- **Product version:** **0.17.1** in `ProjectSettings/ProjectSettings.asset`.
- **Most recent release report:** `docs/chase-v0.17.1/README.md`; best detailed movement certification: `docs/chase-v0.17.0/README.md`.
- **Latest tests:** **103/103 PlayMode**, **33/33 EditMode** passing, independent Unity CLI. Latest production **WebGL build succeeded, zero errors** (one known non-blocking Unity Pipeline package warning); actual Chrome menu → difficulty → Arena 01 preview → start chase passed with zero browser/JS exceptions. Full three-platform suite (production WebGL, separate Gym WebGL, Linux) last passed at **v0.17.0**; v0.17.1 was a visual-only change and only the production WebGL was rebuilt. Do not claim v0.17.1 Gym/Linux builds were run if they were not.
- **Browser deployment files:** `/home/dtdav/dev/chase/Builds/ChaseWebGL`, ignored by Git; **17 files, about 11 MiB**, using Unity `.unityweb` **Decompression Fallback**. The previous `.gz` plus `_headers` hosting approach failed on Cloudflare; do not restore it.
- **Cloudflare Pages:** existing project **`chase`**, expected URL **`https://chase-3bl.pages.dev/`**. The user confirmed a previous corrected version *works* publicly. The **v0.17.1 no-grid output was prepared for user self-upload**; its eventual public deployment was NOT confirmed within this handoff. Verify the site's live state if needed. No purchased domain. No automatic GitHub → Cloudflare deployment workflow yet.
- **Single character Blender source:** `Chibi/Chibi_Rigged.blend`, SHA-256 **`59c735e5f835596acdc0329082d936a3d0f34f5c35c5c4710ef80dc0a5eeb371`**, unchanged after v0.17.1.
- **Public production scene:** `Assets/Scenes/SampleScene.unity` (Arena 01 / Mastered Routes). **22 decorative floor grid marker meshes were deleted in v0.17.1**, with a release contract rejecting their return. Floor collider, nine modules, entry marks and physics preserved.
- **Public free-roam Practice scene:** `Assets/Scenes/PracticePlayground.unity`. **Separate developer-only calibration Gym:** `Assets/Scenes/MovementGym.unity`.
- **Current Editor/Chrome/process state is ephemeral.** The Unity MCP previously reported no reachable Editor instance until the CLI was used. Do not infer that the Editor, Linux app, local webserver, or Chrome debugging connection is open now. Launch or reconnect as needed, without overwriting unsaved work.

Immediate fact check:
```bash
cd /home/dtdav/dev/chase
git status --short --branch
git rev-parse HEAD
git ls-remote origin refs/heads/main
git log -8 --oneline
cat ProjectSettings/ProjectVersion.txt
grep -E 'bundleVersion:|webGLCompressionFormat:|webGLDecompressionFallback:' ProjectSettings/ProjectSettings.asset
sha256sum Chibi/Chibi_Rigged.blend
find Builds/ChaseWebGL -type f -printf '%P %s bytes\n' | sort
```

**If the user requests brainstorming, do not implement.** If the user asks to implement, make a concrete, scoped, fully tested change, preserve proven behavior, and finish. If the user merely supplies this handoff, read it and await instruction.

## 1. The actual creative brief and target audience

CHASE is meant to be **watchable, fast, readable, skill-based competitive tag**, inspired by real **World Chase Tag (WCT)** arenas, but built around a distinctive, stylized chibi world and satisfying keyboard control. The target is **streamers and viewers who appreciate mastery**, not a simulation-grade sports federation rulebook.

The user described the desired audience reaction: *“He is good at this arena. He can do this trick, that trick. You can't catch him here.”* The arena should become learnable, with named corners and recognizable routes, so repeated matches show improvement, bluffing, clever escapes, interception, timing, and player expression. A player should become good at **a space**, not solve a stream of single-button obstacle prompts.

**Central design philosophy, developed together:**

1. **A small set of universal physical movement primitives, many emergent routes.** Run/cut, ordinary jump, feet-first slide, headfirst dive, crouch, and prone crawl. A bench or beam should often permit *over, under, around*, depending on trajectory, angle, approach speed, opponent position and recovery risk. Never make an obstacle say “this is a slide obstacle.”
2. **Real geometry causes outcomes.** Ordinary solid collision and shared locomotion resolve passage. No per-obstacle teleport, move-specific trigger, secret speed boost, temporary collision deactivation, staged combo, contextual “press X to vault,” auto-climb, or animation-based phasing.
3. **Mastery with reasonable forgiveness.** Avoid a single launch pixel/frame and harsh failure penalties that make running around every obstacle mathematically superior. Create several viable approach windows; keep stakes for committed moves. The goal is **high athleticism, fast responsive cuts, and quick but real recovery**, not maximum plausible real-world injury/recovery time.
4. **Player agency.** A dive always attempts to happen, including into a solid wall; no “DIVE BLOCKED.” No automatic backward crawl to undo a player’s progress. If trapped chest-down under a low roof, the player can deliberately WASD-crawl out. On open ground the same rule automatically gets up when clearance allows.
5. **Clarity for first-time viewers.** Use a visually coherent obstacle family, differentiated chaser/evader colors, clean UI, legible centered menu, visual keycaps, normal words like **Dive** rather than internal “Superman Leap,” unobstructed arena camera and repeatable starting positions.
6. **One meticulously handcrafted map before random maps.** We explicitly debated authored arenas vs seed/procedural generation. Chose **curated, stable, masterable Arena 01 first**. Procedural placement might be a later tool, but only after geometry contracts and human playtests; purely random scatter destroys route continuity and streamer mastery.
7. **Simple systems over layers of technical debt.** The user repeatedly requested “fix everything known” and implement exhaustively, **while avoiding over-engineering**. A previous assistant admitted an overcomplicated collider design was unnecessary and simplified it. Avoid recreating complexity without measured evidence.
8. **Play feel and competitive observation are crucial.** 100 passing tests do not prove top-tier match balance or viewer retention. They show reproducible physics/UX cases. Use the actual browser and, eventually, real humans.

**What is *not* agreed as immediate next work:** procedural arena generation, networking, RL policies, advanced bots, combat moves, ragdoll simulation, more arenas, a full audio system, voice, monetization, or dedicated domains. These may be considered after user instruction and evidence. The user was evaluating polish, first-time onboarding, deployment, and esports feel, not authorizing every possible feature.

## 2. Current player experience, game rules and UX

Current actual flow:

**Main Menu → PLAY → Opponent Difficulty → Arena 01 / Mastered Routes → PLAY ARENA → 3-second countdown → 20-second chase → results → NEXT ROUND / SWAP ROLE / MAIN MENU.**

From Main Menu: **HOW TO PLAY → ENTER PRACTICE** for unrestricted practice, plus settings. New sessions begin only after explicit Play. Main Menu, Difficulty, How To, and Settings show a **centered opaque readable card** and faint, authentic recorded two-chibi chase motion in the margins. This is **offline-baked physical movement replay**, not two live bots consuming physics time. Arena selection stops the replay and shows a clear static preview. Play Arena starts the real match; the replay disappears.

- **Human defaults to Evader (blue)**, opponent to Chaser (red). Real Chaser-Evader body contact determines a tag, regardless of who initiated contact.
- **20-second active chase** after **3-second countdown**. Score separates **YOU** and **RIVAL** across role switches; role is always visible. Scores and round count survive Next Round and Swap Role.
- **No “retry”, “restart”, or “rematch” in production**, including no hidden R shortcut. This was an explicit WCT-inspired design correction (v0.16.1). Every ended chase is final. Pause offers **RESUME / MAIN MENU** only; results offer **NEXT ROUND / SWAP ROLE / MAIN MENU** only. NEXT ROUND increments the number and resets both role positions/countdown; SWAP ROLE does the same once and changes which role human controls. Returning to Menu and starting PLAY again creates a new session.
- Fixed designated side spawns **by role**, not human vs NPC: Chaser west **(-5.10, 0.90, -2.10)**, Evader east **(5.10, 0.90, 2.10)**. Thin color-coded visual plates identify them, no added collision. Actual WCT rules require the chaser plate but allow freer evader position; CHASE intentionally has fixed opposing sides **as a game-specific rule**, not a claim about WCT.
- **Four bot profiles**: Rookie, Runner, Rival, Phantom. These alter decision/movement policy frequency/anticipation, not enemy collision or hidden superhuman physics. Even Phantom is **not certified a human-quality pursuer** and bots primarily route around obstacles, not exploit all diving/slide primitives. Check `ChaseBot.cs` before claims.
- **Camera:** fixed elevated **third-person**, *not isometric* now: arena 55° pitch, **0° yaw**, 40° FOV, smooth limited follow and visual obstacle fading. Practice 53° pitch, 0° yaw, 53° FOV. W points **world +Z**, D points **world +X** through camera-relative normalization. The menu's cinematic camera is intentionally different, and game camera resets on gameplay entry. No first-person or freely orbiting competitor camera.
- **In-game broadcast HUD:** minimal Chaser/Evader YOU/RIVAL score, round, role, timer and optional translucent key images. Dark slate / cream / mint / coral / gold / sky-blue consistent with pastel athletic course. Controls should be visual, not big diagnostic panels. Desktop has Quit; WebGL hides it.
- **Keyboard menu navigation:** Up/Down or Tab cycle focus, Enter/Space activates, Escape backs/resumes; mouse works. Input prevents the browser scrolling while canvas is focused. PlayerPrefs persist **Show Keys** and **Highlight Keys**, and menus are focus-loss safe. On blur active play pauses and does not resume itself. Touch-only mobile shows a keyboard-required notice rather than pretending it is playable.
- **Public Practice:** `Assets/Scenes/PracticePlayground.unity`, 24 × 24 m, shared real motor/Chibi assets, six source-linked production obstacles, no NPC, score, timer, stages, “Next”, “Retry”, R/Tab station jumps or mandated sequence. Explore any route; no involuntary position reset except true out-of-world fall. Visible **MAIN MENU** button and Escape pause with **RESUME / MAIN MENU**. Optional visual keycaps; no developer diagnostics. `ChasePracticePlaygroundBuilder.cs` and `ChasePracticePlaygroundContract.cs` enforce this. This was deliberately simplified in v0.15.1 after the user rejected station/checkpoint semantics.
- **Technical Movement Gym:** `Assets/Scenes/MovementGym.unity` is **not the public Practice**. It has labeled/staged measurement obstacles, debug HUD, calibration shortcuts and independent Gym WebGL build. Preserve technical test affordances there without contaminating public UI. Its useful reproducible staging keys are: **V** toggles side inspection view (input basis stays fixed 45°), **G** stages a 3 m original aerial shelf approach, **H** stages point-blank wall contact, **K** stages the difficult prone shelf landing, **J** stages a 6 m/s feet-first tackle, **B** stages the current rail's dive launch, **N** stages the current rail's slide run-up at rest, and **R** resets only this debug actor. These staging commands are NOT exposed in a production match or public Practice; after positioning, actual Up/Down/W inputs still control the true physical motor.

Current gameplay keys:

| Input | Actual behavior |
|---|---|
| `W A S D` | Camera-relative horizontal movement, fast reversals; while chest-prone enables 4-direction player-selected crawl |
| `Space` | Ordinary vertical **Jump**, buffered, coyote time |
| `Up Arrow` | Committed head-first ballistic **Dive** (legacy code says `Leap`/`Leaping`), aim locks on launch |
| `Down Arrow` | Genuine feet-first nearly horizontal football **Slide** |
| `Hold Left Shift` | Crouch/low move, **not** the Slide |
| `Escape` | Pause/resume/back according to context |
| `Enter`, arrows, `Tab` | Menus/results navigation as applicable; **Tab is not a hidden role switch** |
| `R` in public match/Practice | **No retry/reset action.** Retired, intentionally. |

`ChaseKeyboard` polls actual Unity Input System `Keyboard.current` each rendered frame and preserves short press edges until physics consumes them. High-priority ineligible presses must not silently become another move. Menu and action inputs clear on transitions.

## 3. Exact current authoritative movement tuning: v0.17.1

Values in `Assets/Scripts/ChaseTuning.cs` and verified scene contracts, **not** the retired v0.8.1 values:

| Parameter | Current |
|---|---:|
| Standing capsule height / radius | **1.80 m / 0.35 m** |
| Crouched capsule height | **1.55 m** |
| Run max / crouch max | **6.0 / 2.0 m/s** |
| Ground acceleration / braking | **36 / 44 m/s²** |
| Air acceleration | **5 m/s²** |
| Mass / explicit movement gravity | **70 kg / 22 m/s²** |
| Ordinary jump launch speed | **7.0 m/s vertically** |
| Jump input buffer / coyote | **0.13 / 0.12 s** |
| Dive horizontal / vertical launch | **8.2 / 7.25 m/s** |
| Dive solid horizontal capsule length / radius | **1.95 / 0.43 m** |
| Dive collider local center, Y / forward | **−0.29 / +0.16 m** |
| Chest-down impact commitment | **0.20 s** |
| Dive get-up interval / movement during get-up | **0.27 s / 3.4 m/s** |
| Prone crawling speed | **2.6 m/s** |
| Football slide box (length × width × height) | **1.98 × 0.84 × 0.82 m** |
| Football slide box center, Y / forward | **−0.48 / −0.24 m** |
| Slide duration / deceleration | **0.42 s / 6 m/s²** |
| Slide get-up / crawl | **0.14 s / 2.7 m/s** |
| Dive/Slide press buffer | **0.11 s** |
| Countdown / active chase | **3.0 / 20.0 s** |
| Unity fixed simulation timestep | **0.02 s** |

Do not restore older 20/24 ground accel/braking, 9/6.8 dive, 0.4 s chest pause, 0.35 s recovery or 0.45 s slide simply because an older document says “current.” Those were **deliberately rebalanced** in v0.17.0 after the user said slow, punitive obstacle traversal made running around everything safer. The new values preserve a **6 m/s top run cap** while making first input, cuts, landings, jump and dive more athletic.

Empirical v0.17.0 crossing envelope: **both** UnderOverRail and UnderOverBench each passed **9/9 slide-under and 9/9 dive-over** across three launch distances × three lateral offsets (−0.23, 0, +0.23 m), **36/36 combined** with actual loaded public Practice prefabs and PhysX solids. Average test time from launch until standing past crossing: **dive 1.20 s**, **slide 0.63 s** for either obstacle. *These are selected repeatable approach windows, not arbitrary-angle guarantees or win-rate data.* Ordinary ballistic rise estimates, absent obstacles, are ~1.11 m Jump and ~1.19 m Dive; actual collider clearance needs real simulation.

### Physics behavior and the simplified collision architecture

`IChaseActionSource → ChaseAction { Move, Jump, Leap, Slide, Crouch } → ChaseMotor.Simulate(action,dt,clock)`. `ChaseMotor` uses dynamic `Rigidbody`, collision callbacks, live support and clearance tests, genuine gravity/momentum; `ChibiVisualDriver` only depicts the movement with approved clips. No physics root-motion, magical contextual moves or per-limb ragdoll.

- **Dive (Up, code name `Leap`):** immediate heading-locked, rising, headfirst ballistic attack/escape. Short up/down buffered taps may be accepted near recovery once; holding will not auto-repeat. No airborne steering or repeated thrust. Genuine forward wall/opponent impact ends projectile velocity. It cannot start with a long horizontal capsule if that would overlap a solid wall/body at point blank; then the **same requested Dive is accepted in a compact already-solid upright envelope**, with visually braced landing as needed. No refusal popup, no wall phasing.
- **One dive collider:** the actor's *existing single solid CapsuleCollider* is reshaped/rotated to horizontal when safe, then becomes upright crouch only after clearance. Do **not** revive the old long dive BoxCollider and elaborate union of colliders. Capsule is ~1.95 m long, 0.86 m diameter and may be smaller than outstretched visual arms.
- **Landing and universal prone crawl:** chest contact enters `DiveProne`. WASD move the low physical body in four directions, initially facing locked heading, even while it is still completing the minimum impact interval. No input means brake, **not automatic backward retreat**. After 0.20 s, automatically *attempt* to get up; blocked actual overhead/competitor collision means stay prone and keep crawling until upright clearance exists. Open floor means automatic recovery. No “special shelf escape mode.”
- **Smooth get-up:** `TryFinishProneDive` and `AdvanceProneGetup` derive floor contact from the actual horizontal capsule, preserve body world pose on the first recovery frame, keep physical feet anchored while capsule center and body pivot ease toward upright, and blend `LeapRecovery` visuals. Avoid hardcoding body world Y=0.90 or yaw=0: on raised floors or diagonal landings this caused visibly bad snaps. Movement is now maintained through recovery rather than being forced to zero.
- **Slide (Down):** distinct **feet-first reclining football tackle**, low, nearly horizontal, uses current physical momentum and ~6 m/s² deceleration; does not conjure speed from standing. Locked slide heading, no per-frame steering. Uses independent 1.98 × 0.84 × 0.82 m solid slide BoxCollider in open space and compact fallback at walls. During protected slide recovery, a **temporary solid capsule+box union** is still intentionally used, unlike the *dive*. Under tight canopy may crawl forward until it can stand. Do not accidentally merge these two collision designs.
- **Jump:** ordinary upward grounded/coyote/buffered action, not the dive. Spring height increase in v0.17 gives more forgiving genuine traversals. No wall-climb or assisted vault.
- **World-space visual hierarchy:** actor `Rigidbody` origin is at standing capsule center, usually Y≈0.9. The approved character visual child lives around local Y=−0.9 so its feet meet the floor. **This is intentional, not a hierarchy bug.** Recovery transitions preserve world-contact orientation. The large chibi head, arms and feet can visually extend outside approximate collider: acknowledged fidelity/performance compromise, not fake no-clipping.

Sources: `Assets/Scripts/ChaseMotor.cs`, `ChaseTuning.cs`, `ChaseAction.cs`, `ChaseKeyboard.cs`, `ChibiVisualDriver.cs`, the serialized scenes, and live PhysX tests. For questionable behavior read the *actual code*.

## 4. Single authoritative Blender/animation source: never casually change

**Only approved Blender source:** `Chibi/Chibi_Rigged.blend`. Validated checksum:
```text
59c735e5f835596acdc0329082d936a3d0f34f5c35c5c4710ef80dc0a5eeb371
```
Blender installed at `/snap/bin/blender` (historically Blender 5.2.2 LTS). Original art/rig contract:

- Blender authoring: **−Y forward, +Z up**, feet at world origin, height **1.80 m**. Normalized Unity import: **+Z forward, +Y up**, identity local facing and unit scale. Do not apply a secondary −90/+90 degree fix or hidden prefab correction rotation.
- **17 approved original skinned shells / 24,684 vertices; rigid 20-bone Generic rig.** Preserve mesh geometry and rig invariants. Original clothes/head/hands/feet are stylized and deliberately oversized.
- **13 real saved Blender animation actions (Fake User) → 13 exported FBX animation takes → 13 production Animator states.** Includes `CHASE_DiveCrawl` looping subtle four-way prone wiggle, `CHASE_DiveProne` chest idle and `CHASE_LeapRecovery` get-up. Current production clips come from real FBX, **not Unity `__preview__` artifacts**; importer/Animator looping is contract-checked.
- No root motion for movement. `ChibiVisualDriver` selects visual states and blends with the physics state; `Animator.applyRootMotion=false`. Do not turn collision into an animation-driven illusion.
- **Do not export/rebuild FBX just because you touched camera, UI, floor or obstacles.** Never create a second authoritative source, change transforms, re-pose original character or purge saved animation actions without a specific user-authorized rig change and source validation.

Validation in root:
```bash
cd /home/dtdav/dev/chase
python3 Chibi/rig_tools/check_source_layout.py
/snap/bin/blender -b Chibi/Chibi_Rigged.blend --python-exit-code 1 --python Chibi/rig_tools/validate_asset.py
sha256sum Chibi/Chibi_Rigged.blend
```
**Only after intentional Blender edits**, back up exported rig FBX **and .meta** first, then:
```bash
CHASE_ALLOW_REGENERATE=1 /snap/bin/blender -b Chibi/Chibi_Rigged.blend \
  --python-exit-code 1 --python Chibi/rig_tools/export_unity_fbx.py
```
Then Unity menu **Chase → Repair Production Character Animation Imports** and **Chase → Validate Production Character Animation**, test all dynamic states/builds. Historical `armor.blend`, OBJ snapshots, one-off rig migration scripts and `.blend1` are NOT approved alternative sources. `*.blend1` should not enter repo. Some earlier `README.md` and rig docs mention 8 or 12 clips, obsolete axes or older heights: **current validator and 13-taking FBX controller take precedence**.

## 5. Production Arena 01: authored mastery course and measurement contract

Arena is **12 × 12 m**, solid floor/boundary, camera aligned to axis, a fixed chaser/evader side gate and **nine** physically real modular prefab families. **One recognisable course** with three strategically distinct regions, and open lanes to allow pursuit/interception. Source of prefab geometry, deterministic layout and materials: `Assets/Editor/ChaseArenaV1Builder.cs`; prefabs: `Assets/Obstacles/ArenaV1/`; validation: `Assets/Editor/ChaseArenaV1Contract.cs`. Runtime obstacles are normal non-trigger `BoxCollider` geometry, existing `ChaseBlocker`/`ChaseHurdle`/`ChaseOverheadPassage` classification, separate noncollision rounded/enamel meshes and `ChaseOccluder` rendering tags. Prefab instance transforms at scale 1. No new Blender arena source.

**Source-of-truth placements, as checked against v0.17.1 `ChaseArenaV1Builder.Layout`:**

| Zone | Prefab | Authored world X,Z | Intended choices |
|---|---|---|---|
| Crossline | `UnderOverRail` | **0, 0** | 0.86 m beam underside, 0.94 m beam top, **slide under / headfirst dive over / outside bypass** |
| West Switchback | `UnderOverBench` | **−3.55, 0.55** | 0.85 m underside, 0.92 m top, 0.34 m depth; tighter **under/over/around** |
| East Elevation | `AerialSlot` | **3.35, 2.45** | Original 0.55 m lower sill; two-tier overhead slot raised **+0.50 m** in v0.17.0 for taller dive. Timed open slot or outside route |
| West Switchback | `LowCanopy` | **−3.20, 4.35** | 1.64 m underside, crouch/slide or bypass |
| South floor | `LowHurdle` | **0, −4.90** | ~0.38 m solid hurdle, Jump or run around |
| West Switchback | `TurnFrame` | **−1.55, −3.55** | Corner reversals, route choices and cutoffs |
| East Elevation | `LowDeck` | **2.35, −3.35** | 0.44 m raised deck, jump onto/over or bypass |
| East Elevation | `OffsetGates` | **3.75, −0.55** | Offset physical posts, inside-face gap ~1.37 m, route/side cuts |
| East Elevation | `InclineRun` | **1.65, 4.20** | **13°** walkable incline, elevation/launch/running around |

**Testing / authoring priorities:** Any route must fit the real *standing*, *slide* and *dive* collision envelopes **and** look acceptable for the chibi head/arms, not just a point-sized actor. The 1.37 m gate gap was widened because approved settled dive animation arms span ~1.18 m whereas physical capsule diameter is 0.86 m. Competing modules must not intersect. Every start location, launch zone and route departure needs clear space. Counterplay matters: an escape route that only the evader can use freely could trivialize pursuit; detours and catches are deliberately available.

**Do not restore old objects:** “Tall barrier A/B”, “Pillar A/B”, “Hurdle A/B”, “Southwest Superman shelf slot”, “Northeast low passage”, `MODULAR OBSTACLES / reusable prefabs`, the old duplicate v0.9 prefabs/builders or old cube stacks. Legacy module build code was removed in v0.11. Existing technical Gym measuring geometry is not a production obstacle set. `ChaseArenaV1Contract` enforces nine source prefab links, placements, penetrations, spawn safety, approach volume and legacy removal.

**v0.17.1 clean-floor change:** production `SampleScene.unity` used to contain **22** `Floor marker X *` / `Floor marker Z *` meshes. The user called the grid ugly. They were identified as ordinary nonphysical decorative GameObjects (not a shader); all 22 and parent references were removed from the saved scene. **No change** to `Ground 12x12` solid collider, entries or obstacles. `ChaseArenaV1Contract` now rejects their return. Production floor is plain, as proven by real Chrome screenshot in `docs/chase-v0.17.1/evidence/webgl/arena-preview-no-grid.png`. Do not casually call a builder that would re-create grid art or other deprecated debug features. The separate technical Gym may still have measurement lines, as intended.

**Rebuild caution:** `Chase/Arena/... Rebuild` uses `ChaseArenaV1Builder` and regenerates the owned prefab group. Although designed to preserve environment, camera, match and boundaries, do not run it merely to inspect a prefab; inspect saved scene and contract first. Rebuilding anything may change serialized Scene YAML ordering/materials. If user wants placement changes, first define goals and physical tests, modify the one layout authority, refresh its matching scene/prefabs, revalidate **actual saved instance positions** and re-bake the menu recording when motor/arena choreography becomes stale. Do not let code and scene silently diverge.

**Procedural generation** is *deferred*: the prefabs are reusable and the authoritative layout deterministic, but the project has NO validated seed-based arena placer that guarantees navigability, diverse feasible maneuvers, protected heads, interception routes and clearance. Treat procedural as future R&D after stable handcrafted maps and human observation; not an instruction to implement.

## 6. Main menu replay: authentic yet zero runtime gameplay compute

Public first launch should **immediately look like a game about chasing**, not an empty menu nor a running hidden live match. User requested **two chibis chasing in a faint loop behind a centered and genuinely legible menu**, but asked that the background not waste AI/physics compute.

- `Assets/Editor/ChaseMenuReplayBaker.cs` records actual two `ChaseMotor` bodies navigating the official course with genuine PhysX into exactly **three authored cinematic segments** at **50 Hz**, including actual dive, football slide and changes of direction. **Cuts are between shots**, not fake teleports during physical shots. Real physics baking is performed offline in Unity Edit Mode via **Chase → Bake Real-Physics Menu Showcase**.
- `Assets/Resources/MenuChaseReplay.bytes` is the ~31 KiB recorded binary; `Assets/Resources/MenuArenaStill.jpg` is a small decorative static image. `ChaseMenuReplayContract.cs` checks shot timing, displacement, authenticity, avoidance of fake tagging, format, bounds and content. An old animation sampling bug treated seconds as normalized time; v0.14 fixed this with correct clip durations.
- At runtime `ChaseMenuShowcase.cs` creates **visual-only** two `ChibiVisual` animators: NO rigidbodies, colliders, bot pathfinding or live simulation for menu decoration. The real two competitors are hidden/deactivated before starting the actual game. It is a small loop, not embedded MP4 or second-camera multiplayer.
- Main Menu / Difficulty / How To / Settings shows that faint replay, Arena selection instead shows *static unobstructed real course preview*, Start Arena restores real aligned camera and actors, then begins actual countdown. Exit to Menu returns the decorative replay. The opaque centered card intentionally wins the contrast hierarchy over background movement.
- **If changing animations, movement physics or course** such that the baked event no longer corresponds, **re-bake** with the canonical menu command, check physically plausible separation, then rerun `ChaseMenuReplayContract` and real Chrome menu smoke. Don't remove the loop just because it looks “extra”; the user specifically approved it.

## 7. Source layout and ownership map

```text
/home/dtdav/dev/chase
  Chibi/Chibi_Rigged.blend            ← ONE authoritative character source
  Chibi/rig_tools/                   ← Blender source validator and FBX export
  Assets/
    Scenes/
      SampleScene.unity              ← PUBLIC Arena 01, menu/match
      PracticePlayground.unity       ← PUBLIC unlimited free-roam practice
      MovementGym.unity              ← DEBUG measuring Gym only
    Scripts/
      ChaseAction.cs                 ← action/state/role enums
      IChaseActionSource.cs          ← input/AI abstraction
      ChaseKeyboard.cs               ← Input System, action edge buffers
      ChaseMotor.cs                  ← single authoritative physical actor
      ChaseTuning.cs                 ← latest verified constants, v0.17.1
      ChibiVisualDriver.cs           ← only Animator representation
      ChaseMatch.cs                  ← UI flow, score, rules, phases, round transition
      ChaseBot.cs                    ← simple grid-routing shared-motor bot
      ChaseTagContact.cs             ← actual physical collision tagging
      ChaseCameraRig.cs              ← arena camera follow/framing/occlusion
      ChaseHUD.cs                    ← public menus, keycaps and broadcast HUD
      ChaseMenuShowcase.cs           ← lightweight recorded menu replay
      ChasePracticePlayground.cs     ← public free-roam and exit/pause UI
      ChaseMovementGym.cs            ← technical Gym and shortcuts
      ChaseObservation.cs            ← deferred-policy observation vector
      ChaseBlocker/Hurdle/OverheadPassage/Occluder.cs
    Editor/
      ChaseReleaseBuild.cs           ← authoritative build entrypoints/contracts
      ChaseArenaV1Builder.cs         ← nine prefab families + placements
      ChaseArenaV1Contract.cs        ← full course release-gate, no floor grid
      ChaseArenaStartPlates.cs       ← approved role-side entry marks
      ChasePracticePlaygroundBuilder.cs
      ChasePracticePlaygroundContract.cs
      ChaseMovementGymBuilder.cs
      ChaseMovementSceneContract.cs  ← serialized tuning/scene fidelity
      ChaseMenuReplayBaker.cs
      ChaseMenuReplayContract.cs
      ChibiAnimationAssetContract.cs ← 13 real animator/FBX contract
    Obstacles/ArenaV1/               ← authoritative reusable solid modules
    Materials/ArenaV1/               ← arena palette
    Resources/ChibiVisual.prefab     ← approved imported visual hierarchy
    Resources/MenuChaseReplay.bytes  ← approved recorded menu action
    Resources/MenuArenaStill.jpg     ← decorative still
    Art/Characters/Chibi/Models/Chibi_Rigged.fbx
    Art/Characters/Chibi/Animations/Chibi_Validation.controller
    Tests/EditMode/                  ← serialized geometry/scene/input/rig tests
    Tests/PlayMode/                  ← actual motor PhysX, UI and browser-related tests
    WebGLTemplates/ChaseResponsive/  ← responsive browser host template
  ProjectSettings/ProjectSettings.asset
  Tools/serve_webgl.py
  docs/chase-v0.*/                    ← preserved milestone/verification evidence
  Builds/ChaseWebGL/                 ← current WebGL DEPLOY DIRECTORY (Git-ignored)
  Builds/ChaseAnimationGymWebGL/     ← separate technical Gym build (Git-ignored)
  Builds/ChaseLinux/                 ← native development build (Git-ignored)
```

`ChaseObservation` exposes a bounded observation vector (historically **66 floats**), but policy/RL tooling is **not installed or running**. Do not mistake scaffolding for a trained opponent or working online AI service. Exact vector indices should be checked in the current source if used.

## 8. Working with MCP and developing without regressions

The user grants **Terminal, GUI, Blender, Unity MCP**. Use them, not an isolated assistant-side container when the repo actually lives on the connected desktop. A previous assistant temporarily asserted it could not access files because it checked **its own isolated local container** instead of the MCP Terminal; the user rightly corrected it. Connected `mcp__Terminal__start_process` does access `/home/dtdav/dev/chase`. **Be precise about what a tool accesses.**

Typical workflow for a user-authorized implementation:

1. Confirm Git branch/remote/diff/last release, actual code and relevant saved scene; inspect software and processes. Don't assume an earlier assistant's notes describe current state.
2. If user requests design/brainstorm only, **do not code**. If code requested, explain intended minimal change, identify invariants and regression cases.
3. Make focused changes in authoritative file(s). Reuse proven shared motor and modular colliders. Don't increase state/action complexity without showing why. No “just a visual fake” for physical fixes.
4. For physics and geometry, load *actual production prefab instances* and use dynamic PhysX simulation, multiple approach distances/lateral offsets, collision/progress/fall/recovery checks. For UI, run real pointer/keyboard input on the built player.
5. Validate saved serialized scene values vs C# defaults using the Editor release contracts. Unity's scene YAML can have stale serialized fields despite new defaults, and Unity batch editor can rewrite unrelated `ProjectSettings/DynamicsManager.asset` and `TimeManager.asset`. Compare/restore unintended changes after builds.
6. Stop/close any live Unity Editor cleanly **before running isolated batchmode Unity on the same project/Library**; two Editor processes conflict, and recompiles may disconnect Unity MCP. If Editor is already disconnected, CLI can still run. Check logs/output and avoid killing healthy instances without reason. Open Editor again afterward only if needed, leaving saved production scene in Edit Mode, stopped.
7. Run tests sequentially, then appropriate builds. Inspect each **XML result**, build summary/errors/warnings, physical browser result, native startup where relevant. A CLI process exit by itself isn't all proof. Keep evidence for significant releases.
8. Confirm source SHA and `git diff --check`, no unintended Blender/physics/scene/prefab drift, no temporary backup assets, and Git clean after commit/push. Commit/release notes only after verifying.
9. For deployments, create/rebuild `Builds/ChaseWebGL` using the **current .unityweb decompression-fallback configuration**, not old .gz/headers. User currently prefers to upload Cloudflare manually.
10. Report what's **actually** verified, limitations and exact upload steps. Do not say “maximally satisfied” as a substitute for human test evidence.

Example isolated test/build commands (**only once Editor has been closed and no other Unity process owns the project**):

```bash
cd /home/dtdav/dev/chase
UNITY=/home/dtdav/Unity/Hub/Editor/6000.3.26f1/Editor/Unity

"$UNITY" -batchmode -nographics -projectPath "$PWD" \
  -acceptSoftwareTermsForThisRunOnly -runTests -testPlatform editmode \
  -testResults /tmp/chase-editmode.xml -logFile /tmp/chase-editmode.log

"$UNITY" -batchmode -nographics -projectPath "$PWD" \
  -acceptSoftwareTermsForThisRunOnly -runTests -testPlatform playmode \
  -testResults /tmp/chase-playmode.xml -logFile /tmp/chase-playmode.log

"$UNITY" -batchmode -nographics -projectPath "$PWD" \
  -acceptSoftwareTermsForThisRunOnly -buildTarget WebGL \
  -executeMethod ChaseReleaseBuild.WebGLProduction -quit \
  -logFile /tmp/chase-production-webgl.log

"$UNITY" -batchmode -nographics -projectPath "$PWD" \
  -acceptSoftwareTermsForThisRunOnly -buildTarget WebGL \
  -executeMethod ChaseReleaseBuild.WebGLGym -quit \
  -logFile /tmp/chase-technical-gym-webgl.log

"$UNITY" -batchmode -nographics -projectPath "$PWD" \
  -acceptSoftwareTermsForThisRunOnly -buildTarget Linux64 \
  -executeMethod ChaseReleaseBuild.LinuxProduction -quit \
  -logFile /tmp/chase-linux.log
```

**Check build target syntax against installed CLI if it changes.** Actual `ChaseReleaseBuild` uses WebGL production `SampleScene.unity` **plus** `PracticePlayground.unity` (scene loaded when player enters practice); Gym build includes its separate `MovementGym.unity`. `BuildPipeline.BuildPlayer` executes a stack of guard contracts including approved FBX/Animator, saved motor tuning, Gym asset fixtures, the authored arena, baked replay, fixed starts, and public Practice. Stale serialized tuning or misplaced geometry should fail rather than silently ship.

Tests to know: `ChaseCompetitiveMovementTests`, `ChaseDualRouteObstacleTests`, `ChaseSupermanDiveTests`, `ChaseFootballSlideTests`, `ChasePhysicsTests`, `ChibiRigPlayTests`, `ChaseV04MovementTests`, `ChaseArenaV1Tests`, `ChaseArenaRouteGraphTests`, `ChasePracticePlaygroundTests`, `ChasePracticePlaygroundPlayTests`, `ChaseRuleTests`. The PlayMode suite also includes many scripted scenarios including 300 physics stress-case rounds; **exact test counts may grow**. If changing code, determine expected counts afresh.

### Browser, CDP and real render checks

```bash
cd /home/dtdav/dev/chase
python3 Tools/serve_webgl.py --build Builds/ChaseWebGL --port 8765
# optional separate Gym:
python3 Tools/serve_webgl.py --build Builds/ChaseAnimationGymWebGL --port 8766
```

WebGL test scripts use **real Google Chrome**, native canvas mouse/keyboard events over Chrome DevTools Protocol (CDP), and capture actual rendered Unity frames. Use fresh Chrome profile and free `--remote-debugging-port`; inspect `ss -ltnp` and `http://127.0.0.1:<port>/json`. `docs/chase-v0.17.0/verify_movement_browser.py` is the latest detailed public flow test. Older useful scripts: `docs/chase-v0.16.1/verify_competitive_rounds_browser.py`, `docs/chase-v0.16.0/verify_aligned_camera_browser.py`, `docs/chase-v0.15.1/verify_free_roam_browser.py`, `docs/chase-v0.14.0/verify_stability_browser.py`, `docs/chase-v0.8.1/verify_smooth_getup_browser.py`. See each release README for precise paths. In v0.17.1 the browser flow verified current menu → difficulty → Arena preview → active countdown with screenshots, zero errors.

For local tests always disable cache and use fresh `?build=<variant_or_SHA>`; browser tabs keep stale Unity instances after files have been rebuilt. Do not confuse Chrome debugger connection timeout with gameplay regression. If game appears to freeze, compare current game build, CDP responses, player log and scene. Browser page loading progress alone is NOT proof: wait for actual CHASE menu logo and active renderer.

UI smoke: verify centered legible main menu, background lightweight moving replay, difficulty selection, Arena preview static and clean, actual countdown, active score/timer/keycaps, real movement, pause/menu, direct Practice exit, Next Round/Swap Role semantics, focus-loss pause, and no real gameplay movement while menu open.

### Native checks

The Linux build entrypoint is `ChaseReleaseBuild.LinuxProduction`, output `Builds/ChaseLinux/Chase.x86_64`. Launch in actual graphical desktop environment, inspect player log for Unity exceptions and visually verify. Native menu startup was confirmed on v0.17.0, **not complete native mouse-control certification**. Screen and graphical GUI control are different evidence from a headless build passing.

## 9. Cloudflare Pages: current real public hosting and hard-won hosting lessons

The user selected **Cloudflare Pages Free** over Vercel Hobby for a publicly accessible **Unity WebGL static game** (no custom domain purchased). Cloudflare account and GitHub exist. They navigated Pages screens and chose **Direct Upload**. Project name `chase`, public address `https://chase-3bl.pages.dev/`. DO NOT create another project unless user specifically wants one. The user prefers to upload releases manually for now.

Crucial deployment discovery on 2026-10-13:

- An initial production WebGL build used `Build/ChaseWebGL.framework.js.gz`, `.wasm.gz`, `.data.gz`. The assistant added a Cloudflare Pages `_headers` file expecting `Content-Encoding: gzip`. All **18 files** were uploaded successfully.
- **Actual public deployment FAILED**: Unity displayed red banner: `Unable to parse Build/ChaseWebGL.framework.js.gz`, missing `Content-Encoding: gzip`. Live `curl -I` confirmed Cloudflare delivered raw gzipped bytes without gzip content-encoding for the framework and WASM, despite the uploaded `_headers`. **Do not assume a file exists means its headers are applied.**
- Simply uploading uncompressed `.wasm` was **not viable**: original uncompressed WebAssembly expands to **32.3 MiB**, above Cloudflare Pages' **25 MiB per-asset** limit.
- **Confirmed working fix:** in `ProjectSettings/ProjectSettings.asset`, **`webGLDecompressionFallback: 1`** while `webGLCompressionFormat: 1` (gzip). Unity rebuilt output as `Build/ChaseWebGL.data.unityweb`, `.framework.js.unityweb`, `.wasm.unityweb` with browser-side decompression in loader. Remove `_headers`. `index.html` points to `.unityweb`. All three are smaller than Pages' 25 MiB asset limit. This is committed in **`efcdf4e`**.
- The fallback version was tested using a **plain Python HTTP server that sends NO `Content-Encoding`**, with Chrome CDP confirming all three assets loaded correctly, rendered actual CHASE menu, zero browser errors. The user subsequently uploaded it and **confirmed “it works.”**
- v0.17.1 recompiled production WebGL with same fallback and removed grid art; the user was instructed to upload **17/17** files from `Builds/ChaseWebGL`. No `_headers` file is needed and **do not add one by habit**.
- In the Cloudflare dashboard: **Workers & Pages → existing `chase` Pages project → Deployments → Create deployment → Production → upload WebGL output folder**. Upload the CONTENTS of `Builds/ChaseWebGL` so the website root serves `index.html` and `Build/`. Cloudflare UI may show a top-level `ChaseWebGL` folder for grouping, which did *not* prevent reaching `index.html` at site root. Never upload entire Unity source `Assets`/`Library`.
- After deploy, verify `https://chase-3bl.pages.dev/` and do hard refresh (`Ctrl+Shift+R`). To independently check file types use `curl -I` / Network tab and verify actual menu, not just HTTP 200. Treat public version as unknown until verified.

**GitHub automatic Cloudflare deployments:** NOT implemented. The repository has GitHub `main`, and Cloudflare Pages initially offered Connect to Git, but Unity WebGL binaries are ignored by Git. Connecting Pages to source without installing a Unity build runner does **not** compile the game. Direct Upload can later be automated via **GitHub Actions or another Unity-capable runner + Wrangler Cloudflare Pages API**, requiring Cloudflare authorization/secrets and Unity licensing/minutes planning. Hosting free does **not imply Unity builds free**. Do not promise “every GitHub push deploys” until the whole pipeline is installed, authenticated, tested and verified. Never disclose or commit Cloudflare API tokens to GitHub. Vercel Hobby was not chosen, partly because of its personal/noncommercial terms; Cloudflare is the current user-approved provider.

## 10. Chronological history since the original handoff

**Historical v0.1–v0.4:** Unity project, input abstraction, dynamic tagging, 3D scene and character normalization; corrected Blender-to-Unity axes and rig, used real imported FBX animations. Earlier upright “leap”/context vault implementation is retired.

| Milestone / commit | What changed and *why* |
|---|---|
| v0.5 `4aea69b`, `df83670`, `d10d09a`, `ec119da` | Replace upright leap with actual ballistic head-first Superman, no midair steering or secretly blocked inputs; end on real wall/opponent impact. |
| v0.6 `bfea217` | User demanded a real horizontal **feet-first** football slide, not crouch drifting. Pose + low solid box under canopies. |
| v0.6.1 `4be60d3` | Correct permanently stuck dive at ledge when grounded detection inspected the wrong physical support. Three-point support and stalled-contact resolution. |
| v0.7 `4d46e57` | Remove overcomplicated dive box/unions, simplify **dive** to the actor's existing rounded capsule. Slide deliberately keeps its different solid box. |
| v0.8 `4e9850d` | Universal player-driven WASD prone crawl and automatic stand-on-clearance; eliminate offensive automatic backward crawl. Author `CHASE_DiveCrawl` in the sole Blender source. |
| v0.8.1 `fa1ad1e` | Fix snap during prone→stand: preserve world pose/heading, derive actual floor contact, ease body origin/collider and animation. Original October 12 handoff ended here. |
| v0.9 `b99fc76` | Add real prefabs UnderOverRail and UnderOverBench. WCT design insight: same solid obstacle should allow **slide under, Superman over**. Raise dive vertical 5.2→6.8 m/s, test approaches. |
| v0.10 `4df78dc` | Replace ugly stacked placeholder blocks with **nine stylistically coherent reusable arena prefab families**, solid geometry + nonphysical rounded decorative art. |
| v0.11 `701d6da` | **Craft Arena 01** with Crossline, West Switchback, East Elevation, viable measured route options, approach buffers, navigation, removed old duplicate prefab builders/placeholders. Single authored arena before procedural. |
| v0.12 `438b766` | Public menu, difficulty presets, Arena select, accessible visual keycaps, clean streamer HUD; technical data removed from public HUD. |
| v0.13 `15872ec` | Legible centered menu + authentic faint 2-chibi scripted physical chase **baked offline**, no live gameplay CPU. Clear arena preview when selected. |
| v0.14 `900b3f9` | Fix score attached to YOU/RIVAL, focus-loss, keyboard UI, unexpected R/Tab shortcuts, actual replay animation time and false implied contact, settings/browser behavior. |
| v0.15 `f317312` | Add public practice based on actual production assets (original staging UX was too structured). |
| v0.15.1 `f019227` | **Remove Next/Retry/stations** from Practice; make it truly free-roam and give direct Main Menu exit. |
| v0.16 `d25fb57` | Remove isometric diagonal hassle, use **fixed arena-axis aligned third-person** with consistent **role-specific side spawns**. |
| v0.16.1 `5d4063c` | Remove Restart/Retry/Rematch; every chase final, Next Round/Swap Role advances real numbered competition. |
| v0.17 `09690a0` | User called movement sluggish/obstacle punishment excessive. **Faster acceleration/reverse, higher jump, slightly shorter/higher dive, quicker recoveries and buffered actions**, same honest physics. Re-raise sole AerialSlot overhead tier and re-bake menu footage, certify 36/36 dual-route tests. |
| Cloudflare fix `efcdf4e` | Replace broken `.gz` header-dependent Pages upload with **browser-side `.unityweb` decompression fallback**, verified public hosting by user. |
| v0.17.1 `20e9473` | Delete **22 cosmetic grid lines** from production floor, add release contract, preserve collider/layout. Tests and real browser checked. Latest verified HEAD. |

Prior detailed evidence is deliberately versioned at `docs/chase-v0.*/README.md`. Older UI/retry/geometry notes document **what used to be true**, not present behavior. Do not “fix” an apparent mismatch in old v0.12 screenshot by changing current implementation to match it.

## 11. Mistakes, debugging lessons, and dangerous simplifications

The previous assistants made mistakes. They matter because repeating them would harm the approved feel:

- **Confusing current and historical state:** original October 12 `CHASE_NEXT_AI_COMPLETE_HANDOFF.md` named v0.8.1, a rotated/isometric view, old controls and 0.4 s landings. That was accurate *then*, **not now**. Prioritize v0.17.1 and current C# and serialized scene.
- **Too many colliders/flags:** old dive BoxCollider and multi-collider union snagged at ledges. Simplify physical states instead of piling on special recovery rules. Avoid regressions to “always standing” or “no collision while diving.”
- **Animation-only tricks instead of honest physics:** never ghost, teleport, trigger a shelf scripted traversal, directly change animation instead of actually moving rigidbody, or invent midair boost.
- **Preventing diving near walls:** initially added `DIVE BLOCKED`; user rejected it. Accept commanded action, safely compact collision at overlap, and let impact resolve.
- **Automatic recoil:** initial shelf rescue crawled backward after successful headfirst dive; user rejected. Player controls crawl and true automatic stand attempts.
- **World-position snapping:** hardcoded Y=0.9 when getting up visibly snapped pose/facing and broke raised platforms; derive true contact, preserve pivots, ease.
- **Misleading “slide”:** earlier slide torso upright despite lowered capsule; physically and visually actual feet-first recline is essential.
- **Movement too sluggish:** prior realistic recovery cost made obstacles lose against straightforward running; v0.17 corrected this. Do not equate realism with fun.
- **One-frame puzzle obstacles:** a route “works” at a perfectly chosen launch doesn't make it competitively usable. Test launch **windows**, offsets, both directions, catch-up economics and actual rendering.
- **Menu background accidentally real gameplay:** desired demonstration is visual only, no simulated hidden two-bot chase while browsing. Use approved recorded loop, centered contrast; keep game frozen until PLAY ARENA.
- **Fake or time-wrong replay:** Blender Animator state playback expects normalized sample time, actual recording stores seconds. Properly normalize clip duration. Avoid baked shots that visually imply a tag when there is none.
- **UI font glyph failure:** default Unity WebGL fonts didn't render arrow icons properly. `ChaseHUD` generates tiny raster key icons internally; don't replace with untested Unicode arrows.
- **Hidden retry/role shortcuts:** the user rejected random role swapping/retrying during competition; public `R` and `Tab` shortcuts were intentionally removed. Don’t re-add them for convenience.
- **Practice as checklist:** user wants open floor, explore freely, no suggested mandatory stations or progressing Next/Retry. Keep developer Gym separate.
- **Shotgun scene regeneration:** Unity `OpenScene/SaveScene` may normalize thousands of YAML lines and alter `ProjectSettings`; v0.17.1 used careful verified deletion of 22 decorative meshes and parent refs. Commit minimal changes, inspect full diff and guard exact scene values.
- **Cloudflare `_headers` fix didn't actually work:** The file existed, the 18-file upload looked correct, but **live** HTTP missed `Content-Encoding: gzip`. Never equate a successful upload with a successful Unity load. The permanent fallback fix lives in the Unity project settings.
- **Wrong execution environment:** ChatGPT's own `/mnt/data` filesystem is not the same as user's remote `/home/dtdav/dev/chase`. Use MCP Terminal to inspect the actual repository. An assistant once said the repo was unavailable when it merely consulted the wrong sandbox.
- **Unity crashed/disconnected during development:** the user encountered Unity Editor crashes. CLI build/test is usable without live Editor; never run concurrent Unity instances against one Library, restore Editor when appropriate and save work. Don't claim an Editor is healthy without checking.
- **Screenshots alone insufficient:** a rendered final standing pose doesn't prove no mid-transition snap. Use consecutive frames/physical pose sampling. A build returned success does not mean real Chrome navigates the menu.
- **GitHub push is distinct from Cloudflare deploy:** binaries are Git ignored, Direct Upload is manual; no connected Git push pipeline exists. State this explicitly.

## 12. Current limitations and recommended future decision order

**Known NOT YET CERTIFIED:**

- Competitive balance under many **actual human-versus-human** chases. Even 36/36 dual-route scripted physics cases cannot establish match fairness, map dominance or high-skill depth. Streamer discovery/retention also unknown.
- Bot strategy: simple grid navigation and predictive difficulty heuristics, **does not master diving/sliding or counterplay like a human**. Higher preset is not guaranteed human-pro skill. No trained RL policy.
- No real online multiplayer, matchmaking, chat, voice, ghosts, spectators, replay recording from public sessions or tournament backend. Current public site is a **single-player static WebGL game**.
- Audio/voice, art polish outside current coherent course, second Arena, telemetry, mobile touch controls, comprehensive Firefox/Safari/multi-device/performance/accessibility certification, analytics/privacy planning, automated CI release and Github→Pages continuous deployment are not proven delivered.
- Some visual chibi mesh extremities exceed physics collider; no per-limb ragdoll or perfect animation/collision silhouettes at all contacts. Extremely sealed solid geometry can physically trap a character rather than magically releasing them.
- Real-time UI/keyboard readiness across *all* browsers, lower-end hardware performance and latency have not been exhaustively certified. Native desktop startup was tested, not all native mouse interactions.
- User has to **manually deploy v0.17.1** if not yet done. Never assert public site updated solely because code was pushed to GitHub.

**Suggested discussion order, not an authorized task list:** (1) confirm latest no-grid public build with user's browser and play feel; (2) focused human balance/playtesting with route usage, pursuit decisions, failures, camera readability and first-time onboarding; (3) decide whether next user-facing blocker is audio/feedback, bot intelligence, accessibility/performance, or second carefully crafted arena; (4) add analytics/privacy and deployment automation deliberately if the user wants broad distribution; (5) consider multiplayer/esports infrastructure only with a clear networking/latency/competitive integrity architecture. **Procedural layouts last**, after reusable geometry contracts and strong authored map foundations.

The broad aspiration is **an enjoyable streamer-ready competitive chase game where route mastery, not obscure controls, is the spectacle**. No specific paid release date, revenue target or multiplayer launch has been promised.

## 13. How to interact with the user as the successor

The user prefers direct, objective and precise assessments; they will tell you if movement feels wrong, an obstacle is boring, an interface is confusing or a change is overbuilt. They explicitly ask the AI to push back on weak plans and to think before implementing. Prefer evidence and meaningful disagreements to agreement for its own sake.

- They distinguish **brainstorm first** vs **go implement exhaustively**. Respect both instructions literally.
- When implementing, verify rather than keep asking permission for routine local Git/tests/build fixes. For user authentication/cloud credentials or publishing, respect explicit authorization and don't invent account access.
- The goal is for players to become **visibly masterful at an authored environment**; “best version” means gameplay pleasure **plus** architectural correctness, without unnecessary systems.
- No requirement to add novelty when fundamentals already work. Avoid premature first-person camera switch, procedural content, AI training, network stack or Blender re-rig merely to show productivity.
- Communicate concrete progress during long test/build jobs, then report real commits, test totals, browser evidence, public deployment uncertainty and exact remaining action.
- **At handoff intake, read this document, confirm the exact current state from tools, summarize your understanding, and WAIT for the user's next instruction.** Do not run a wholesale rebuild.

## 14. Useful source links and commands for the next AI

- Repository: **https://github.com/ChenDavidTimothy/chase**
- Most recent verified source commit: **https://github.com/ChenDavidTimothy/chase/commit/20e9473**
- v0.17.1 clean arena: **https://github.com/ChenDavidTimothy/chase/blob/20e9473/docs/chase-v0.17.1/README.md**
- Actual clean floor screenshot: **https://github.com/ChenDavidTimothy/chase/blob/20e9473/docs/chase-v0.17.1/evidence/webgl/arena-preview-no-grid.png**
- Full v0.17.0 movement/balance report: **https://github.com/ChenDavidTimothy/chase/blob/20e9473/docs/chase-v0.17.0/README.md**
- WCT rulebook (for inspiration, not automatic rules adoption): **https://www.worldchasetag.com/the-rulebook**
- Cloudflare public site: **https://chase-3bl.pages.dev/** (check public deployed revision before making version claims).
- Core release entrypoint: **`Assets/Editor/ChaseReleaseBuild.cs`**. Scene: **`Assets/Scenes/SampleScene.unity`**. Blender: **`Chibi/Chibi_Rigged.blend`**. Current tuning: **`Assets/Scripts/ChaseTuning.cs`**.
- Inspection: `git status --short --branch`, `git diff --check`, `git log -1 --oneline`, `git ls-remote origin refs/heads/main`, `sha256sum Chibi/Chibi_Rigged.blend`.
- Important latest screenshots and browser scripts are under `docs/chase-v0.17.0/evidence/`, `docs/chase-v0.17.1/evidence/`. Each previous milestone keeps its own evidence as historical proof.

**END OF COMPLETE HANDOFF.** The next AI's first response should demonstrate comprehension of **v0.17.1**, **Git `20e9473`**, the **solid emergent Arena 01**, **v0.17 responsive movement and collider rules**, **fixed third-person angle and role starts**, **public free-roam Practice and no retry**, **recorded low-cost centered main menu**, **unchanged sole Blender source**, and **Cloudflare Pages direct-upload `.unityweb` fallback**. The incoming AI should then await the user's concrete new task.
