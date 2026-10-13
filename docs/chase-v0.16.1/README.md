# CHASE v0.16.1: Every chase counts

## Player-facing behavior

The competitive match no longer offers **Restart**, **Retry**, or **Rematch**.

**During Pause:** RESUME or MAIN MENU. Pausing does not reset the ongoing countdown, athlete positions, or score. Returning to the main menu abandons the current session.

**After a completed chase:** NEXT ROUND, SWAP ROLE, or MAIN MENU. NEXT ROUND is the primary action. Both NEXT ROUND and SWAP ROLE start a **new numbered round**, resetting the countdown and both role-based starting positions while preserving the accumulated YOU/RIVAL wins. SWAP ROLE additionally changes the human's controlled role. A round cannot be advanced while active or paused.

These semantics match CHASE's intended WCT-inspired competitive flow: a completed chase is final, not replayable. Returning to the main menu and intentionally starting another session is still possible, but resets that session's score and round counter.

## Source cleanup

- Deleted the entire public `Rematch()` entry point.
- Replaced `BeginRound(bool restart)` with a parameterless private round-initialization method. The obsolete false/true split no longer exists.
- Deleted the legacy non-public developer `R` restart keyboard shortcut. Physics stress fixtures still call the parameterless private initializer directly to construct deterministic simulation cases, not from player input.
- `SwitchRole()` now delegates to the **same guarded NEXT ROUND transition**, so there is one authoritative way to advance the round number and clear stale keyboard-menu selections.
- Reduced UI/keyboard menu option counts (Pause 2, Results 3), updated focus markers and centered the two secondary Results actions.

Movement physics, bot decisions, arena layout, camera, saved Chibi rig, practice free-roam, and scores are otherwise unchanged. No new gameplay system was introduced.

## Verification

Isolated Unity 6000.3.26f1 CLI:

- **100/100 PlayMode** and **33/33 EditMode** tests pass.
- New tests verify that active chases cannot advance, a scored outcome cannot be erased, NEXT ROUND preserves scores while incrementing the round number, SWAP ROLE also increments exactly once, fixed role start positions remain stable, and returning to the main menu creates a new session only upon deliberate launch.
- Pause and Results keyboard option counts and actions are regression-tested. All 300 physics stress-case rounds continue to run with the parameterless internal setup fixture.
- Production WebGL, independent Movement Gym WebGL, and native Linux builds succeed with **zero build errors** and all original asset, obstacle, rig, Gym, fixed-start and Practice contracts passed.
- Real Chrome WebGL, using actual mouse and keyboard input, checks a paused R key (zero visual changes), the first chase's completed Results UI, NEXT ROUND starting Round 02 with scores intact, the next chase's Results, SWAP ROLE starting Round 03 as Chaser with scores intact, and Return to Menu without an exception. Screenshots: [pause](evidence/webgl/04-pause-resume-or-menu.png), [Round 01 result](evidence/webgl/06-result-round-1.png), [Round 02 countdown](evidence/webgl/07-new-countdown-round-2.png), [Round 02 result](evidence/webgl/08-result-round-2.png), [Round 03 swapped role](evidence/webgl/09-swapped-role-round-3.png).
- Native Linux player launched, rendered the menu, and reported no runtime exceptions in its startup log. Native mouse-button interactions were not separately certified.

See the [machine-readable release record](evidence/release-verification.json) and [Chrome interaction report](evidence/webgl/verification.json). Reproduce the browser checks with `verify_competitive_rounds_browser.py`.

Prior historical release records remain unchanged, even where they describe the retired Rematch behavior.
