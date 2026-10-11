# CHASE v0.4 numerical movement calibration

**Source of numbers:** fresh `ChaseV04MovementTests.RealPhysicsCalibrationOutputsMeasurements` PlayMode test, actual `Rigidbody` positions and velocities with `Physics.simulationMode=Script`, `ChaseMotor.Simulate`, `Physics.Simulate(0.020f)`, flat colliding floor and identical shared `ChaseTuning`. Raw TSV: [`movement-measurements.tsv`](movement-measurements.tsv). These are **not** theoretical equations, Blender root displacement, or values inferred from keyframed actions. Values measured on October 11, 2026. Times reflect discrete physics steps.

| Metric | Preliminary intention | Physics measured | Difference | Comment |
|---|---:|---:|---:|---|
| Run full speed | 6 m/s | 6.000 m/s | 0.000 | 0.300 s rest to full speed |
| Run acceleration | 20 m/s² | 20.000 m/s² | 0.000 | Acceleration from first fixed step |
| Ground braking | 24 m/s² | 24.000 m/s² | 0.000 | 0.6912 m braking distance |
| Direction reversal | Physically limited | 0.560 s | n/a | +6 to -6 m/s |
| Jump launch VY | 6.3 m/s | 6.300 m/s | 0.000 | Velocity sampled with gravity increment restored |
| Jump apex | ~0.90 m | **0.840 m** | -0.060 m | Discrete gravity applied on launch/physics step |
| Jump time to apex | ~0.286 s ideal | 0.280 s | -0.006 s | 0.02 s step resolution |
| Jump total airtime | ~0.57 s | 0.580 s | +0.010 s | Physical floor contact |
| Jump from rest distance | 0 m | 0.000 m | 0.000 | No forward impulse |
| Running jump distance | ~3.44 m | 3.480 m | +0.040 m | Initial 6 m/s horizontal, held W |
| Leap horizontal launch | 9 m/s | 9.000 m/s | 0.000 | Same heading from rest/running |
| Leap vertical launch | 3.9 m/s | 3.900 m/s | 0.000 | Physically simulated |
| Leap apex | ~0.35 m | 0.3072 m | -0.0428 m | Gravity/timestep integration |
| Leap time to apex | ~0.177 s ideal | 0.160 s | -0.017 s | Discrete step |
| Leap airtime to recovery | ~0.35 s | 0.360 s | +0.010 s | Actual contact gating |
| Leap distance to recovery | ~3.2 m | 3.240 m | +0.040 m | No hidden targeting or snapping |
| Leap recovery | 0.35 s proposed | **0.500 s** | +0.150 s | Deliberate anti-spam correction |
| Slide travel from 0 m/s | 0 m | 0.0000 m | 0 | Real slide action, zero burst |
| Slide travel from 2 m/s | Momentum-based | 0.2304 m | n/a | Near-zero exit velocity |
| Slide travel from 4 m/s | Momentum-based | 0.9568 m | n/a | Deceleration 8 m/s² |
| Slide travel from 6 m/s | ~1.9 m | **1.8768 m** | -0.0232 m | 0.45 s commanded slide |
| Slide duration | 0.45 s | ~0.46 s (23 fixed ticks) | +0.01 s | Tick quantization |
| Slide recovery | 0.20 s | 0.20 s (10 fixed ticks) | 0 | Recovery blocks committed moves |
| Crouch horizontal limit | 2 m/s | 2.000 m/s | 0 | Controller slows with acceleration |
| Crouch collision height | ~1.1 m provisional | **1.550 m** | +0.45 m | Raised to match visible shell |
| Slide collision height | 0.85-0.95 m provisional | **1.550 m** | +0.60-0.70 m | Do not fake head clearance |
| Low crouch-walk shell height | Must measure | **1.531 m** | n/a | Five Blender frames of evaluated meshes |
| Slide shell height, all tested frames | Must measure | **1.628 m max** | n/a | Five frames; passage underside 1.64 m |

### Repeated leap exploitation measurement

Continuous 10 s running: **59.15985 m**. Repeated launch attempts with the same physical motor yielded these measured distances (no obstacles):

| Landing recovery | Repeated-leap distance / 10 s | Leap attempts | Advantage versus running |
|---|---:|---:|---:|
| Preliminary 0.35 s | 62.70250 m | 11 | +3.54265 m, **exploit** |
| 0.45 s | 58.66812 m | 10 | -0.49173 m |
| **Production 0.50 s** | **57.11213 m** | 10 | **-2.04772 m** |
| 0.55 s | 54.86414 m | 9 | -4.29571 m |
| 0.60 s | 53.77215 m | 9 | -5.38770 m |

This fixture simulates continuous W, presses leap only when it is actually eligible, maintains release edges, and uses 500 physics steps. A short tactical leap still has a 9 m/s launch, but repeating it is inferior to sustained 6 m/s running over 10 s. This specific fixture is not a universal proof against all obstacle-based exploits; actual playtesting remains necessary.

### Physics, obstacles, and failure cases

Test fixtures measure collisions against a tall wall and real solid low passages. Leap into wall stays dynamic and loses speed rather than replenishing acceleration. Stationary slide does not travel, and keeping Up/Down held does not retrigger committed actions. The 0.35 m physical hurdle is below measured 0.84 m jump apex, but successful traversal also requires adequate approach distance and capsule sweep clearance, not just comparative heights. The old 1.125 m shelf was invalid as a combined jump/crouch obstacle and was replaced. Current arena has two roofs at center 1.715 m with thickness .15 m and underside **1.64 m**, posts 1.71 m tall, 1.84 m clear inner width, 0.55 m depth, separate 0.35 m hurdles and 2.3 m barriers. The 0.55 m passage leaves ample room within ~1.88 m slide travel at 6 m/s if approached at a reasonable distance.

### Sampling limits and tolerance

Measurements are 0.02 s quantized, with numerical tolerances in Unity tests. Running/jump distances include one or more contact-resolution steps and therefore may differ from ideal analytic ballistics. Geometry data comes from five representative keyed frames per action, not an exhaustive continuous-time collision sweep. The worst **transitional animation** overlap and oversized horizontal head/limb extent are still documented in [`rig-clearance.md`](rig-clearance.md). Numeric success is not equivalent to human-approved handling or full mesh-level collision equivalence.
