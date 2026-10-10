# CHASE v0.1

A single-player, keyboard-only, physics-based 3D chase-tag prototype for Unity 6.3 LTS.

## Play

Open `Assets/Scenes/SampleScene.unity` in Unity 6000.3.26f1 and press **Play**.
The human begins as the blue Evader and must survive for 20 seconds. The red Chaser
uses a simple non-learning pathfinding controller. A successful physical tag ends
the round immediately. A 3-second countdown precedes each round.

| Key | Action |
| --- | --- |
| W, A, S, D | Move relative to the camera's fixed screen orientation |
| Up Arrow or Space | Jump, or vault a suitable shelf |
| Down Arrow or Left Shift (hold) | Crouch, or slide when running |
| R | Retry the current spawn and role |
| Enter | New round after a win/loss |
| Tab | Swap human Chaser/Evader role after a win/loss |
| Escape | Pause / resume |

The camera never rotates or zooms, and there are no mouse controls.

## Arena

A 12 x 12 m enclosed arena containing two tall barriers, two pillars, two
hurdles, and two supported shelf obstacles. Each shelf offers over, under, and
around routes. Characters share a dynamic-Rigidbody, solid-CapsuleCollider motor
and the same acceleration, jumping, sliding, vaulting, and gravity parameters.

## Architecture

- `Assets/Scripts/ChaseAction.cs`: input-independent action and state definitions.
- `ChaseMotor.cs`: shared collision-preserving movement physics.
- `ChaseKeyboard.cs`: Input System adapter with screen-relative WASD.
- `ChaseBot.cs`: grid pathfinding and simple pursuit/escape action adapter.
- `ChaseTagContact.cs`, `ChaseMatch.cs`: physical tagging, countdown, timer, score, roles.
- `ChaseCameraRig.cs`, `ChaseHUD.cs`: fixed camera, occlusion fading, keyboard HUD.
- `ChaseObservation.cs`: read-only motor/opponent/obstacle state for future RL use.
- `Assets/Editor/ChaseSceneBuilder.cs`: **Chase > Rebuild Arena** menu command.
- `Assets/Tests/EditMode/ChaseRuleTests.cs`: EditMode rule and motor tests.

Tuning lives in the serializable `ChaseTuning` on the `CHASE / Game systems`
object in the scene. The authored `SampleScene` is registered in Build Settings.

## Tests and builds

Run **Window > General > Test Runner**, then the EditMode tests. Build target:
Linux x86_64. Local build output is deliberately ignored by Git.

Unity Pipeline `0.8.0-exp.1` remains installed for Editor MCP access. It is not
configured to expose its server inside standalone Player builds; gameplay does
not depend on a runtime Pipeline server.

ML-Agents integration is not included in v0.1. Future policy implementations
should provide `ChaseAction` values to the existing `ChaseMotor`, with observations
from `ChaseObservationSensor`.
