using UnityEngine;

namespace Chase
{
    // One physical motor for every source. Animations only read these states.
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class ChaseMotor : MonoBehaviour
    {
        public ChaseTuning Settings { get; private set; }
        public Rigidbody Body { get; private set; }
        public CapsuleCollider Capsule { get; private set; }
        public BoxCollider DiveBox { get; private set; }
        // At least one solid Collider always remains active; neither is a trigger.
        public ChaseMovementState State { get; private set; }
        public bool Grounded { get; private set; }
        public Vector3 Velocity => Body.linearVelocity;
        public Vector3 HorizontalVelocity => Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up);
        public bool CanStand => Capsule != null && Capsule.enabled && !DiveColliderActive &&
            Capsule.direction == 1 && HasStandingClearance();
        public bool CanJump => Grounded && IsUpright && !Recovering;
        public bool CanLeap => Grounded && IsUpright && !Recovering;
        public bool CanSlide => Grounded && !Recovering &&
            State != ChaseMovementState.Leaping && State != ChaseMovementState.Sliding &&
            State != ChaseMovementState.DiveProne;
        public bool IsCommitted => State == ChaseMovementState.Leaping || State == ChaseMovementState.Sliding ||
            State == ChaseMovementState.DiveProne;
        public bool Recovering => State == ChaseMovementState.LeapRecovery || State == ChaseMovementState.SlideRecovery ||
            State == ChaseMovementState.DiveProne;
        public float RecoveryRemaining => Recovering ? recoveryLeft : 0f;
        public float CommittedRemaining => State == ChaseMovementState.Sliding ? slideLeft : 0f;
        public bool DiveColliderActive => DiveBox != null && DiveBox.enabled;
        public Vector3 FacingDirection => new Vector3(Mathf.Sin(visualYaw * Mathf.Deg2Rad), 0f,
            Mathf.Cos(visualYaw * Mathf.Deg2Rad));
        bool IsUpright => Capsule != null && Capsule.enabled && Capsule.direction == 1 && Capsule.height >= Settings.standingHeight - .01f &&
            State != ChaseMovementState.Crouching && State != ChaseMovementState.Sliding &&
            State != ChaseMovementState.SlideRecovery;

        float lastGrounded = -100f, lastTakeoff = -100f, jumpQueuedUntil = -100f;
        float slideLeft, recoveryLeft;
        bool diveShapeCommitted, diveGettingUp;
        Vector3 committedDirection;
        bool previousJump, previousLeap, previousSlide;
        static PhysicsMaterial sharedCapsuleMaterial;
        readonly RaycastHit[] groundHits = new RaycastHit[16];
        readonly Collider[] clearanceHits = new Collider[48];
        Transform visual;
        Vector3 visualInitialScale, standingVisualLocalPosition;
        float visualYaw;

        public void Initialize(ChaseTuning settings, Transform visualRoot)
        {
            Settings = settings;
            Body = GetComponent<Rigidbody>();
            Capsule = GetComponent<CapsuleCollider>();
            DiveBox = GetComponent<BoxCollider>();
            if (DiveBox == null) DiveBox = gameObject.AddComponent<BoxCollider>();
            DiveBox.center = new Vector3(0f, settings.diveBodyCenterY,
                settings.diveBodyCenterForward);
            DiveBox.size = new Vector3(settings.diveBodyWidth,
                settings.diveBodyHeight, settings.diveBodyLength);
            DiveBox.isTrigger = false;
            DiveBox.enabled = false;
            Capsule.enabled = true;
            Body.mass = settings.mass;
            Body.useGravity = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Body.linearDamping = 0f;
            Body.angularDamping = 0f;
            Capsule.direction = 1;
            Capsule.radius = settings.radius;
            Capsule.height = settings.standingHeight;
            Capsule.center = Vector3.zero;
            if (sharedCapsuleMaterial == null)
            {
                sharedCapsuleMaterial = new PhysicsMaterial("Chase frictionless capsule")
                {
                    dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            Capsule.sharedMaterial = sharedCapsuleMaterial;
            DiveBox.sharedMaterial = sharedCapsuleMaterial;
            visual = visualRoot;
            visualInitialScale = visual.localScale;
            standingVisualLocalPosition = visual.localPosition;
            State = ChaseMovementState.Standing;
        }

        public void ResetMotor(Vector3 position)
        {
            Body.position = position;
            Body.rotation = Quaternion.identity;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            slideLeft = recoveryLeft = 0f;
            lastGrounded = lastTakeoff = jumpQueuedUntil = -100f;
            previousJump = previousLeap = previousSlide = false;
            committedDirection = Vector3.zero;
            diveShapeCommitted = diveGettingUp = false;
            Body.rotation = Quaternion.identity;
            Capsule.enabled = true;
            DiveBox.enabled = false;
            Capsule.direction = 1;
            Capsule.radius = Settings.radius;
            SetHeight(Settings.standingHeight);
            State = ChaseMovementState.Standing;
            Grounded = true;
            visualYaw = 0f;
            if (visual != null)
            {
                visual.localRotation = Quaternion.identity;
                var driver = visual.GetComponent<ChibiVisualDriver>();
                if (driver != null) driver.ResetAnimation();
            }
            Body.Sleep();
        }

        public void Simulate(ChaseAction action, float dt, float clock)
        {
            if (Settings == null || dt <= 0f) return;
            action.Move = Vector2.ClampMagnitude(action.Move, 1f);
            Vector3 direction = new Vector3(action.Move.x, 0f, action.Move.y);
            Vector3 velocity = Body.linearVelocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            Grounded = DetectGround() && (clock - lastTakeoff > .10f || velocity.y <= 0f);
            if (Grounded) lastGrounded = clock;

            // Latch *button states* even when actions are ineligible.
            bool jumpPressed = action.Jump && !previousJump;
            bool leapPressed = action.Leap && !previousLeap;
            bool slidePressed = action.Slide && !previousSlide;
            previousJump = action.Jump;
            previousLeap = action.Leap;
            previousSlide = action.Slide;
            if (jumpPressed && !action.Crouch && !Recovering &&
                State != ChaseMovementState.Crouching && State != ChaseMovementState.Sliding)
                jumpQueuedUntil = clock + Settings.jumpBuffer;

            if (State == ChaseMovementState.Leaping)
            {
                // The takeoff begins upright. Change to the long horizontal
                // collider only after launch and ONLY if the full solid swept
                // body can occupy that space. Never disable collisions or tunnel.
                float timeSinceTakeoff = clock - lastTakeoff;
                // Only the early takeoff window may switch body shape. After
                // that, a missed/obstructed dive stays upright and pays for
                // the failure. A late flip mid-descent would be an exploit.
                if (!diveShapeCommitted &&
                    timeSinceTakeoff >= Settings.diveShapeDelay &&
                    timeSinceTakeoff <= Settings.diveShapeDelay + .12f)
                    TryEnterDiveShape();
                // Never reapply the initial boost, even after a wall impact.
                if (Grounded && clock - lastTakeoff > .12f)
                {
                    jumpQueuedUntil = -100f;
                    if (diveShapeCommitted)
                    {
                        State = ChaseMovementState.DiveProne;
                        recoveryLeft = Settings.diveImpactRecovery;
                    }
                    else
                    {
                        // Failed/blocked takeoff retains an upright collider.
                        // It still pays for the committed mistake.
                        State = ChaseMovementState.LeapRecovery;
                        recoveryLeft = Settings.leapRecovery + Settings.diveImpactRecovery;
                    }
                }
            }
            else if (State == ChaseMovementState.DiveProne)
            {
                // Physically chest-first on the ground; held buttons cannot
                // cancel this recovery. Existing momentum brakes rather than
                // being replaced by a new horizontal impulse.
                recoveryLeft = Mathf.Max(0f, recoveryLeft - dt);
                planar = Vector3.MoveTowards(planar, committedDirection *
                    (recoveryLeft > 0f || diveGettingUp ? 0f : Settings.proneCrawlSpeed),
                    Settings.groundBraking * dt);
                if (recoveryLeft <= 0f)
                {
                    // A horizontal body contacts the floor with the actor
                    // origin at ~0.57 m, while the standing reference is 0.9 m.
                    // Physically push up BEFORE extending the collider; never
                    // expand into the floor or any overhead shelf.
                    Vector3 target = new Vector3(Body.position.x,
                        Settings.standingHeight * .5f, Body.position.z);
                    bool spaceToRise = CanFitVerticalShape(
                        Settings.crouchingHeight, target) && CanFitDiveShape(target);
                    if (!spaceToRise)
                    {
                        diveGettingUp = false;
                    }
                    else
                    {
                        diveGettingUp = true;
                        planar = Vector3.MoveTowards(planar, Vector3.zero,
                            Settings.groundBraking * dt);
                        if (Body.position.y < target.y - .045f)
                            velocity.y = Mathf.Max(velocity.y, 2.8f);
                        else if (CanFitVerticalShape(Settings.crouchingHeight,
                                     Body.position))
                        {
                            BeginGetupColliderUnion(Settings.crouchingHeight);
                            State = ChaseMovementState.LeapRecovery;
                            recoveryLeft = Settings.leapRecovery;
                            diveGettingUp = false;
                        }
                    }
                }
            }
            else if (State == ChaseMovementState.Sliding)
            {
                slideLeft -= dt;
                planar = Vector3.MoveTowards(planar, Vector3.zero, Settings.slideDeceleration * dt);
                if (!Grounded)
                {
                    // Leaving an edge interrupts the grounded slide as a normal fall.
                    // Do not play a grounded get-up timer while airborne.
                    State = ChaseMovementState.Airborne;
                    slideLeft = 0f;
                    jumpQueuedUntil = -100f;
                }
                else if (slideLeft <= 0f)
                {
                    State = ChaseMovementState.SlideRecovery;
                    recoveryLeft = Settings.slideRecovery;
                    jumpQueuedUntil = -100f;
                }
            }
            else if (Recovering)
            {
                recoveryLeft = Mathf.Max(0f, recoveryLeft - dt);
                // Braking must respect acceleration, not snap velocity to 2 m/s.
                float top = Settings.recoverySpeed;
                Vector3 target = direction * top;
                float rate = planar.magnitude > top + .01f ? Settings.groundBraking :
                    Settings.groundAcceleration * .5f;
                planar = Vector3.MoveTowards(planar, target, rate * dt);
                if (recoveryLeft <= 0f)
                {
                    // During the chest->hands->crouch get-up animation BOTH
                    // colliders remain solid. Only at the end can the fully
                    // upright capsule take over. No invisible horizontal hands
                    // or body can pass through a shelf during interpolation.
                    if (State == ChaseMovementState.LeapRecovery && DiveColliderActive)
                        FinishDiveShape();
                    if (action.Crouch || !HasStandingClearance())
                    {
                        SetHeight(Settings.crouchingHeight);
                        State = ChaseMovementState.Crouching;
                    }
                    else
                    {
                        SetHeight(Settings.standingHeight);
                        State = Grounded ? ChaseMovementState.Standing : ChaseMovementState.Airborne;
                    }
                }
            }
            else
            {
                // Explicit input priority: Jump > Leap > Slide > held Crouch.
                // An ineligible higher-priority press does not transform into another ability.
                bool jumpRequested = clock <= jumpQueuedUntil;
                bool canUpright = !action.Crouch &&
                    State != ChaseMovementState.Crouching && IsUpright;
                bool coyote = clock - lastGrounded <= Settings.coyoteTime;
                bool doJump = jumpRequested && coyote && canUpright &&
                    clock - lastTakeoff > .12f;
                bool doLeap = !doJump && !jumpPressed && leapPressed && Grounded &&
                    canUpright && clock - lastTakeoff > .12f;
                bool doSlide = !doJump && !doLeap && !jumpPressed && !leapPressed &&
                    slidePressed && Grounded;
                if (doJump)
                {
                    jumpQueuedUntil = -100f;
                    State = ChaseMovementState.Jumping;
                    velocity.y = Settings.jumpSpeed;
                    lastTakeoff = clock;
                    Grounded = false;
                }
                else if (doLeap)
                {
                    jumpQueuedUntil = -100f;
                    committedDirection = FacingDirection;
                    planar = committedDirection * Settings.leapHorizontalSpeed;
                    velocity.y = Settings.leapVerticalSpeed;
                    State = ChaseMovementState.Leaping;
                    diveShapeCommitted = diveGettingUp = false;
                    lastTakeoff = clock;
                    Grounded = false;
                }
                else if (doSlide)
                {
                    jumpQueuedUntil = -100f;
                    committedDirection = planar.sqrMagnitude > .01f ? planar.normalized : FacingDirection;
                    SetHeight(Settings.slideHeight);
                    State = ChaseMovementState.Sliding;
                    slideLeft = Settings.slideDuration;
                    planar = Vector3.MoveTowards(planar, Vector3.zero, Settings.slideDeceleration * dt);
                }
                else
                {
                    bool low = action.Crouch || !HasStandingClearance();
                    SetHeight(low ? Settings.crouchingHeight : Settings.standingHeight);
                    State = low && Grounded ? ChaseMovementState.Crouching :
                        Grounded ? ChaseMovementState.Standing : ChaseMovementState.Airborne;
                    float maxSpeed = low ? Settings.crouchSpeed : Settings.runSpeed;
                    Vector3 target = direction * maxSpeed;
                    bool braking = direction.sqrMagnitude < .001f ||
                        (planar.sqrMagnitude > .01f && Vector3.Dot(planar, direction) < -.05f);
                    float acceleration = Grounded ?
                        (braking ? Settings.groundBraking : Settings.groundAcceleration) :
                        Settings.airAcceleration;
                    planar = Vector3.MoveTowards(planar, target, acceleration * dt);
                }
            }
            if (State == ChaseMovementState.Sliding)
                planar = Vector3.ClampMagnitude(planar, Settings.runSpeed);
            if (!Grounded || velocity.y > .01f)
                velocity.y -= Settings.gravity * dt;
            else if (velocity.y < 0f) velocity.y = -.35f;
            Body.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
            if (State != ChaseMovementState.Leaping &&
                State != ChaseMovementState.DiveProne &&
                !(State == ChaseMovementState.LeapRecovery && DiveColliderActive))
                OrientVisual(planar, dt);
        }

        // Test the real horizontal Box volume BEFORE enabling it. Queries must
        // not rely on ComputePenetration with an intentionally disabled shape;
        // an overlap is conservatively blocked. The live body remains solid.
        bool CanFitDiveShape(Vector3 actorPosition)
        {
            Quaternion orientation = Quaternion.LookRotation(committedDirection, Vector3.up);
            Vector3 center = actorPosition +
                orientation * new Vector3(0f, Settings.diveBodyCenterY,
                    Settings.diveBodyCenterForward);
            Vector3 half = new Vector3(Settings.diveBodyWidth,
                Settings.diveBodyHeight, Settings.diveBodyLength) * .5f;
            int count = Physics.OverlapBoxNonAlloc(center, half, clearanceHits,
                orientation, ~0, QueryTriggerInteraction.Ignore);
            if (count == clearanceHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider hit = clearanceHits[i];
                if (hit == null || hit == Capsule || hit == DiveBox ||
                    hit.transform.IsChildOf(transform)) continue;
                // OverlapBox accurately represents an oriented full-body
                // collision envelope even while DiveBox itself is disabled.
                // Reject any real solid shelf/support/opponent intersection.
                return false;
            }
            return true;
        }

        bool CanFitVerticalShape(float height, Vector3 actorPosition)
        {
            Vector3 offset = Vector3.down * (Settings.standingHeight - height) * .5f;
            Vector3 center = actorPosition + offset;
            float half = Mathf.Max(0f, height * .5f - Settings.radius);
            return CanOccupy(center + Vector3.up * half,
                center - Vector3.up * half, Settings.radius,
                Quaternion.identity, 1, height, offset, actorPosition);
        }

        bool CanOccupy(Vector3 a, Vector3 b, float radius,
            Quaternion orientation, int direction, float height, Vector3 center,
            Vector3 actorPosition)
        {
            // A slightly inset overlap query avoids false positives from
            // perfectly tangent floor contact. Unlike ComputePenetration on a
            // disabled capsule, this never silently treats a shelf as empty.
            float testRadius = Mathf.Max(.01f, radius - .007f);
            int count = Physics.OverlapCapsuleNonAlloc(a, b, testRadius,
                clearanceHits, ~0, QueryTriggerInteraction.Ignore);
            if (count == clearanceHits.Length) return false;
            float bottom = Mathf.Min(a.y, b.y) - radius;
            for (int i = 0; i < count; i++)
            {
                Collider hit = clearanceHits[i];
                if (hit == null || hit == Capsule || hit == DiveBox ||
                    hit.transform.IsChildOf(transform)) continue;
                // Only the actual floor directly below our feet is exempt;
                // a raised shelf / wall / opponent may never be ignored.
                if (hit.bounds.max.y <= bottom + .020f) continue;
                return false;
            }
            return true;
        }

        void TryEnterDiveShape()
        {
            if (!CanFitDiveShape(Body.position)) return;
            Body.rotation = Quaternion.LookRotation(committedDirection, Vector3.up);
            // Enable the alternate full-body Collider BEFORE disabling the
            // upright one. There is never a collision-free physics step.
            DiveBox.enabled = true;
            Capsule.enabled = false;
            if (visual != null) visual.localRotation = Quaternion.identity;
            diveShapeCommitted = true;
        }

        void BeginGetupColliderUnion(float lowHeight)
        {
            // Safe projected vertical envelope was already checked.
            // Enable upright crouch collider while retaining the prone Box.
            // Their union covers each intermediate animation silhouette.
            Capsule.direction = 1;
            Capsule.radius = Settings.radius;
            SetHeight(lowHeight);
            Capsule.enabled = true;
            // DiveBox remains enabled until the visual get-up ends.
            diveGettingUp = false;
        }

        void FinishDiveShape()
        {
            Body.rotation = Quaternion.identity;
            // The crouch collider already protects this body. Removing the
            // extra horizontal box at animation completion only reduces the
            // occupied volume. The collider is never fully disabled.
            DiveBox.enabled = false;
            if (visual != null)
                visual.localRotation = Quaternion.Euler(0f, visualYaw, 0f);
            diveShapeCommitted = false;
            diveGettingUp = false;
        }

        void SetHeight(float h)
        {
            // Fixed feet: capsule midpoint moves down inside the actor as it bends.
            Capsule.height = h;
            Capsule.center = new Vector3(0f, (h - Settings.standingHeight) * .5f, 0f);
            if (visual == null) return;
            if (visual.GetComponent<ChibiVisualDriver>() != null)
            {
                visual.localScale = visualInitialScale;
                visual.localPosition = standingVisualLocalPosition;
                return;
            }
            visual.localScale = new Vector3(visualInitialScale.x,
                visualInitialScale.y * h / Settings.standingHeight, visualInitialScale.z);
            visual.localPosition = standingVisualLocalPosition +
                Vector3.down * (Settings.standingHeight - h) * .5f;
        }

        bool HasStandingClearance()
        {
            if (Capsule == null || Capsule.height >= Settings.standingHeight - .01f) return true;
            float height = Settings.standingHeight;
            float half = Mathf.Max(0f, height * .5f - Settings.radius);
            Vector3 center = Body.position;
            int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * half,
                center - Vector3.up * half, Settings.radius,
                clearanceHits, ~0, QueryTriggerInteraction.Ignore);
            if (count == clearanceHits.Length) return false;
            float oldHeight = Capsule.height;
            Vector3 oldCenter = Capsule.center;
            Capsule.height = height;
            Capsule.center = Vector3.zero;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Collider hit = clearanceHits[i];
                    if (hit == null || hit == Capsule || hit.transform.IsChildOf(transform)) continue;
                    if (Physics.ComputePenetration(Capsule, center, Quaternion.identity,
                            hit, hit.transform.position, hit.transform.rotation,
                            out Vector3 normal, out float depth) && depth > .015f)
                        return false;
                }
                return true;
            }
            finally
            {
                Capsule.height = oldHeight;
                Capsule.center = oldCenter;
            }
        }

        bool DetectGround()
        {
            float bottom = Capsule.enabled ?
                Body.position.y + Capsule.center.y - Capsule.height * .5f :
                float.PositiveInfinity;
            if (DiveColliderActive)
                bottom = Mathf.Min(bottom,Body.position.y +
                    DiveBox.center.y - DiveBox.size.y * .5f);
            Vector3 start = new Vector3(Body.position.x,
                bottom + Settings.radius + .11f, Body.position.z);
            int count = Physics.SphereCastNonAlloc(start, Settings.radius * .86f,
                Vector3.down, groundHits, .21f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.distance <= .0001f || hit.distance > .177f || hit.collider == null || (hit.collider == Capsule || hit.collider == DiveBox) ||
                    hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<ChaseMotor>() != null) continue;
                if (Vector3.Dot(hit.normal, Vector3.up) >= .65f) return true;
            }
            return false;
        }

        void OrientVisual(Vector3 planar, float dt)
        {
            if (visual == null || planar.sqrMagnitude < .09f) return;
            float heading = Mathf.Atan2(planar.x, planar.z) * Mathf.Rad2Deg;
            visualYaw = Mathf.LerpAngle(visualYaw, heading, 1f - Mathf.Exp(-12f * dt));
            visual.localRotation = Quaternion.Euler(0f, visualYaw, 0f);
        }
    }
}
