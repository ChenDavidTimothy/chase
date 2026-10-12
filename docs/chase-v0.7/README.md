# CHASE v0.7 — simpler Superman dive with one rounded collider

**Current movement contract.** This deliberately replaces the technical-debt mechanisms introduced in v0.5–v0.6.1. A player still presses **Up** to immediately launch in their current facing direction; holding W/A/S/D with Up selects the initial heading. From that moment the motion is a committed ballistic arc (9 m/s forward, 5.2 m/s upward, 22 m/s² gravity) with **zero airborne WASD steering**, no extra impulse, real solid collision, tag contact, mandatory prone impact (0.40 s), and 0.35 s get-up recovery.

## What was removed

The former dive installed a separate **2.3 m × 1.2 m × 0.95 m solid BoxCollider** next to the ordinary upright CapsuleCollider. Getting up then required predicting the entire long box's upward sweep, moving it vertically, temporarily having both box and crouched capsule enabled, and later retiring the box. That design snagged against shelf edges, repeatedly needed extra contact-detection and recoil exceptions, and increased the number of collision states that had to be synchronized with the animation.

**The dive now has only one solid CapsuleCollider.** The football tackle continues to have its separate, independently validated solid slide box, but the dive never uses it. The capsule itself simply changes between two safe configurations:

| State | The same capsule |
|---|---|
| Standing | Upright, 1.80 m tall, radius 0.35 m |
| Clear Superman takeoff / flight / prone landing | Rounded, **1.95 m long, radius 0.43 m**, along locked heading, center lowered 0.29 m and offset forward 0.16 m |
| Close wall/opponent takeoff | Existing upright capsule remains solid; the committed launch is still attempted and physical contact interrupts it |
| Clearance-checked get-up | **Same capsule** changes directly to crouched vertical shape (1.55 m), followed by ordinary standing once safe |

There is **no dive-only BoxCollider**, **no collider-union recovery**, no temporarily disabled capsule, and no late morph after takeoff. The one football-slide BoxCollider remains disabled throughout the dive. The transition preserves the existing Rigidbody's continuous collision detection, frictionless surface, non-trigger status, and normal gravity. The simple rounded capsule trades a slightly smaller silhouette around extended hands and feet for fewer shelf-corner snags; the stylized chibi Superman pose remains fully headfirst and unchanged.

## One predictable state machine

1. **Launch.** One clearance query tests the prospective horizontal rounded capsule. If safe, the existing capsule immediately rotates horizontally. If not, it stays upright but the dive launches anyway. Both share the same initial speed, contact rules, and timers.
2. **Flight.** Gravity governs the Rigidbody. Walls and the opponent interrupt flight; their contact cannot be canceled by WASD. The existing center/end ground probe and actual contact normals detect floor/shelf landings, so an actor resting on a shelf tip cannot remain forever in zero-speed `Leaping`.
3. **Impact.** `DiveProne` brakes for 0.40 seconds, and the original `DiveProne` animation stays active where the body is physically horizontal. With no room to rise, a held forward input crawls through a low passage; without input the athlete slowly recoils toward open space, still physically colliding with obstacles.
4. **Get-up.** Only one transition is needed: verify the crouched capsule at the intended ground-aligned position **and the rounded capsule's small vertical travel**. Then adjust the actor origin by the measured small height difference and convert the same capsule to upright crouch. The existing 0.35 s `LeapRecovery` animation plays, and the ordinary standing check remains conservative. There is no sustained upward force, no late collider switch or second physics collider in get-up.

**Inherent limitation:** During the short 0.35-second get-up animation, the visible limbs interpolate between the horizontal pose and crouch while the single collider is already crouched. The silhouette is an approximation, not per-limb ragdoll contact. That is the deliberate, documented compromise in exchange for substantially fewer physical states; no invisible collider passes through a wall or opponent. A fully sealed solid enclosure remains physically inescapable without violating collision authority.

## No stale scene dimensions

Both authored scenes, `Assets/Scenes/SampleScene.unity` and `Assets/Scenes/MovementGym.unity`, explicitly serialize the four new capsule fields: length 1.95 m, radius 0.43 m, center local Y −0.29 m, center forward +0.16 m. The unified `ChaseMovementSceneContract` audits **both** the unchanged football-slide dimensions and the new dive dimensions on **every WebGL/Linux build**. A stale scene aborts the build; there is no fallback to silently inconsistent default collider settings.

## Actual regression coverage

The new Unity `RoundedDiveUsesOneAlwaysSolidCapsuleWithoutExtraDiveCollider` test checks the true component count (one CapsuleCollider, one unrelated disabled slide BoxCollider), no trigger, no disabled-collider frame, zero capsule/box collision unions, horizontal first-frame heading and body length, and crouched capsule before recovery animation. Existing physics PlayMode tests verify the shelf slot traversal, seven near-lip approach positions that previously deadlocked, immediate wall impact, role-aware contact tagging, gravity-driven trajectory, no midair steering and repowering, low overhead crawl, and compatibility with the now-stable feet-first football tackle. The imported FBX test continues to require an authentically horizontal headfirst torso; it documents a larger allowed limb overhang around the rounded collider.

The unified design removes approximately 70 lines from `ChaseMotor.cs` alone (797 → 726), one dynamically generated collision component, two state flags, and the entire former collider-union recovery. More important than line count: **one live solid capsule now owns every moment of the dive**.

## Reproduction

The isolated Movement Gym (`http://127.0.0.1:8766/`) supports **G** for the shelf approach and **H** for point-blank wall contact, with **V** for low side profile. Press Up alone, or W+Up, to dive. **J** continues to stage the football tackle. Production is locally served on `http://127.0.0.1:8765/`.

Versioned Chrome scripts and run reports are in `docs/chase-v0.7/evidence/`, separate from old v0.6.1 and v0.6 captures so a cached older build cannot be mistaken for this simplified implementation. Results must be supported by tests, actual freshly rebuilt browsers, and Linux builds before release. Subjective feel needs user playtesting; automated tests cannot prove artistic satisfaction.

## Completed release audit — October 12, 2026

- **70/70 real Unity PlayMode tests passed**, including the new single-collider/no-union/no-gap invariant, the seven shelf-edge approaches without additional input, all wall and opponent impacts, standard jump and crouch, and the complete v0.6 football sliding tackle regressions. **15/15 Unity EditMode tests passed.**
- **All three fresh Player builds succeeded:** Movement Gym WebGL, production WebGL, and native Linux64. Each BuildReport recorded **zero errors**, one existing Unity Pipeline warning, and passed the production and gym **serialized scene contract** for both slide and new dive collider dimensions.
- **Real Chrome browser proof:** both shelf approach and point-blank wall impact were repeated **twice each**, seven seconds of post-impact waiting without intervention. The character was inspected upright after recovery. Chrome reported **zero console errors** and focused keyboard input without page scrolling. The production player was independently loaded using **a cache-disabled navigation to its v0.7 URL** and successfully handled Up, Down, and Space. The football tackle was also replayed on the new WebGL with real Down and conflicting A/D input, and retained its reclined, low silhouette and standing recovery.
- **Native Linux Player:** freshly rebuilt binary launched in a real window; no matching runtime exceptions or missing asset messages in the player log.
- **Blender:** untouched original `Chibi/Chibi_Rigged.blend` passed the strict source and rig validators, with 12 actions and all 17 original meshes. No FBX or Animator edits were made.
- **Technical debt removed:** `ChaseMotor.cs` shrank from **797 to 722 lines**, eliminated the second physical dive collider and its setup, removed the two `diveShapeCommitted`/`diveGettingUp` flags, replaced the repeated multi-collider get-up with one validated change to the same capsule, and replaced the slide-specific build-only validator with a combined slide+dive scene contract. The Unity motor still implements both slide and dive, and contains necessary real-contact and overhead-clearance checks; simplification does **not** imply removing these safety guarantees.

The [machine-readable verification record](evidence/verification-summary.json), [nine-stage rounded dive shelf/wall browser evidence](evidence/dive-recovery-rounded-capsule.png), [foot-first tackle regression sequence](evidence/gym/football-tackle-unaffected.png), and three reproducible versioned Chrome scripts in this folder are the release evidence. Local WebGL builds and Linux players are intentionally ignored by Git, but are served/run on the development computer.

**Remaining artistic compromise:** the keyed Superman animation extends some head, hand, and foot geometry beyond the rounded collision capsule. This is deliberate, not mechanically exact body-to-body collision. If fully accurate appendage collision is required in the future, the system would need a more complex physical model; this version prioritizes snappy, reliable movement and collision consistency. Subjective user playtesting is still the final judgment of feel.
