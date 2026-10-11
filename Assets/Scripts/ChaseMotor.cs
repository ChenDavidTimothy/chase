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
        public ChaseMovementState State { get; private set; }
        public bool Grounded { get; private set; }
        public Vector3 Velocity => Body.linearVelocity;
        public Vector3 HorizontalVelocity => Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up);
        public bool CanStand => HasStandingClearance();
        public bool CanJump => Grounded && IsUpright && !Recovering;
        public bool CanLeap => Grounded && IsUpright && !Recovering;
        public bool CanSlide => Grounded && !Recovering &&
            State != ChaseMovementState.Leaping && State != ChaseMovementState.Sliding;
        public bool IsCommitted => State == ChaseMovementState.Leaping || State == ChaseMovementState.Sliding;
        public bool Recovering => State == ChaseMovementState.LeapRecovery || State == ChaseMovementState.SlideRecovery;
        public float RecoveryRemaining => Recovering ? recoveryLeft : 0f;
        public float CommittedRemaining => State == ChaseMovementState.Sliding ? slideLeft : 0f;
        public Vector3 FacingDirection => new Vector3(Mathf.Sin(visualYaw * Mathf.Deg2Rad), 0f,
            Mathf.Cos(visualYaw * Mathf.Deg2Rad));
        bool IsUpright => Capsule != null && Capsule.height >= Settings.standingHeight - .01f &&
            State != ChaseMovementState.Crouching && State != ChaseMovementState.Sliding &&
            State != ChaseMovementState.SlideRecovery;

        float lastGrounded = -100f, lastTakeoff = -100f, jumpQueuedUntil = -100f;
        float slideLeft, recoveryLeft;
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
            Body.mass = settings.mass;
            Body.useGravity = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Body.linearDamping = 0f;
            Body.angularDamping = 0f;
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
                // Preserve actual physics velocity, including wall collision losses.
                // In particular, never replenish horizontal speed after impact.
                if (Grounded && clock - lastTakeoff > .10f)
                {
                    State = ChaseMovementState.LeapRecovery;
                    recoveryLeft = Settings.leapRecovery;
                    jumpQueuedUntil = -100f;
                }
                // No directional steering during a committed airborne leap.
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
            if (State != ChaseMovementState.Leaping) OrientVisual(planar, dt);
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
            float bottom = Body.position.y + Capsule.center.y - Capsule.height * .5f;
            Vector3 start = new Vector3(Body.position.x,
                bottom + Settings.radius + .11f, Body.position.z);
            int count = Physics.SphereCastNonAlloc(start, Settings.radius * .86f,
                Vector3.down, groundHits, .21f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.distance <= .0001f || hit.distance > .177f || hit.collider == null || hit.collider == Capsule ||
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
