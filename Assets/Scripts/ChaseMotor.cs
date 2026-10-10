using UnityEngine;

namespace Chase
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class ChaseMotor : MonoBehaviour
    {
        public ChaseTuning Settings { get; private set; }
        public Rigidbody Body { get; private set; }
        public CapsuleCollider Capsule { get; private set; }
        public ChaseMovementState State { get; private set; }
        public bool Grounded { get; private set; }
        public bool CanJump => Grounded && State != ChaseMovementState.Crouching && State != ChaseMovementState.Sliding && State != ChaseMovementState.Vaulting;
        public Vector3 Velocity => Body.linearVelocity;
        public Vector3 HorizontalVelocity => Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up);
        public bool CanStand => HasStandingClearance();
        public bool CanVault => Grounded && State != ChaseMovementState.Sliding && State != ChaseMovementState.Crouching;

        float lastGrounded = -100f;
        float jumpQueuedUntil = -100f;
        float lastJumpAt = -100f;
        float slideLeft;
        float vaultLeft;
        Vector3 committedDirection;
        bool previousDuck;
        Transform visual;
        Vector3 visualInitialScale;
        Vector3 standingVisualLocalPosition;
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
            slideLeft = vaultLeft = 0f;
            lastGrounded = jumpQueuedUntil = lastJumpAt = -100f;
            previousDuck = false;
            SetHeight(false);
            State = ChaseMovementState.Standing;
            Grounded = true;
            visualYaw = 0f;
            if (visual != null) visual.localRotation = Quaternion.identity;
            Body.Sleep();
        }

        public void Simulate(ChaseAction action, float dt, float clock)
        {
            if (Settings == null || dt <= 0f) return;
            action.Move = Vector2.ClampMagnitude(action.Move, 1f);
            Vector3 desiredDirection = new Vector3(action.Move.x, 0f, action.Move.y);
            Vector3 velocity = Body.linearVelocity;
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            Grounded = DetectGround() && (clock - lastJumpAt > 0.10f || velocity.y <= 0f);
            if (Grounded) lastGrounded = clock;
            if (action.JumpOrVault) jumpQueuedUntil = clock + Settings.jumpBuffer;
            bool duckPressed = action.DuckOrSlide && !previousDuck;
            previousDuck = action.DuckOrSlide;

            if (State == ChaseMovementState.Vaulting)
            {
                vaultLeft -= dt;
                if (vaultLeft <= 0f) State = ChaseMovementState.Airborne;
                // The vault retains entry momentum and a fixed direction. Solid
                // collision and gravity are left enabled at every instant.
                planar = Vector3.MoveTowards(planar, committedDirection * Mathf.Min(6f, planar.magnitude), 1f * dt);
            }
            else if (State == ChaseMovementState.Sliding)
            {
                slideLeft -= dt;
                planar = Vector3.MoveTowards(planar, Vector3.zero, Settings.slideDeceleration * dt);
                if (slideLeft <= 0f || planar.magnitude < 0.2f)
                    State = ChaseMovementState.Crouching;
            }
            else
            {
                if (duckPressed && Grounded && planar.magnitude >= Settings.slideThreshold)
                {
                    State = ChaseMovementState.Sliding;
                    committedDirection = planar.normalized;
                    slideLeft = Settings.slideDuration;
                    SetHeight(true);
                    planar = Vector3.MoveTowards(planar, Vector3.zero, Settings.slideDeceleration * dt);
                }
                else if (action.DuckOrSlide)
                {
                    State = ChaseMovementState.Crouching;
                    SetHeight(true);
                }
                else if (HasStandingClearance())
                {
                    SetHeight(false);
                    State = Grounded ? ChaseMovementState.Standing : ChaseMovementState.Airborne;
                }
                else
                {
                    SetHeight(true);
                    State = ChaseMovementState.Crouching;
                }

                if (State != ChaseMovementState.Sliding)
                {
                    float topSpeed = State == ChaseMovementState.Crouching ? Settings.crouchSpeed : Settings.runSpeed;
                    Vector3 desired = desiredDirection * topSpeed;
                    bool braking = desiredDirection.sqrMagnitude < 0.001f ||
                        (planar.sqrMagnitude > 0.01f && Vector3.Dot(planar, desiredDirection) < -0.05f);
                    float accel = Grounded ? (braking ? Settings.groundBraking : Settings.groundAcceleration) : Settings.airAcceleration;
                    planar = Vector3.MoveTowards(planar, desired, accel * dt);
                }

                bool buffered = clock <= jumpQueuedUntil;
                bool coyote = clock - lastGrounded <= Settings.coyoteTime;
                if (buffered && coyote && clock - lastJumpAt > 0.12f &&
                    State != ChaseMovementState.Crouching && State != ChaseMovementState.Sliding)
                {
                    jumpQueuedUntil = -100f;
                    if (TryBeginVault(planar))
                    {
                        State = ChaseMovementState.Vaulting;
                        vaultLeft = Settings.vaultDuration;
                        committedDirection = planar.normalized;
                        velocity.y = Settings.vaultVerticalSpeed;
                    }
                    else
                    {
                        State = ChaseMovementState.Airborne;
                        velocity.y = Settings.jumpSpeed;
                    }
                    lastJumpAt = clock;
                    Grounded = false;
                }
            }
            if (State == ChaseMovementState.Sliding)
                planar = Vector3.ClampMagnitude(planar, Settings.runSpeed);
            if (!Grounded || velocity.y > 0.01f)
                velocity.y -= Settings.gravity * dt;
            else if (velocity.y < 0f) velocity.y = -0.35f;
            Body.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
            OrientVisual(planar, dt);
        }

        void SetHeight(bool crouched)
        {
            float h = crouched ? Settings.crouchingHeight : Settings.standingHeight;
            Capsule.height = h;
            Capsule.center = new Vector3(0f, (h - Settings.standingHeight) * 0.5f, 0f);
            if (visual == null) return;
            visual.localScale = new Vector3(visualInitialScale.x,
                visualInitialScale.y * h / Settings.standingHeight, visualInitialScale.z);
            visual.localPosition = standingVisualLocalPosition +
                Vector3.down * (Settings.standingHeight - h) * 0.5f;
        }

        bool HasStandingClearance()
        {
            if (Capsule == null) return true;
            if (Capsule.height >= Settings.standingHeight - 0.01f) return true;
            float halfLine = Settings.standingHeight * 0.5f - Settings.radius - 0.02f;
            Vector3 center = Body.position;
            Collider[] hits = Physics.OverlapCapsule(center + Vector3.up * halfLine,
                center - Vector3.up * halfLine, Settings.radius * 0.98f,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
                if (hit != Capsule && !hit.transform.IsChildOf(transform) && hit.GetComponent<ChaseMotor>() == null)
                {
                    // Floor collision at the bottom of the standing capsule is valid.
                    if (hit.bounds.max.y < Body.position.y - Settings.standingHeight * 0.5f + 0.06f) continue;
                    return false;
                }
            return true;
        }

        bool DetectGround()
        {
            float bottomY = Body.position.y + Capsule.center.y - Capsule.height * 0.5f;
            Vector3 feet = new Vector3(Body.position.x, bottomY + 0.045f, Body.position.z);
            Collider[] hits = Physics.OverlapSphere(feet, Settings.radius * 0.63f, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
                if (hit != Capsule && !hit.transform.IsChildOf(transform) && hit.GetComponent<ChaseMotor>() == null
                    && hit.bounds.min.y <= bottomY + 0.10f) return true;
            return false;
        }

        bool TryBeginVault(Vector3 planar)
        {
            if (!Grounded || planar.magnitude < Settings.vaultApproachSpeed) return false;
            Vector3 direction = planar.normalized;
            Vector3 origin = Body.position + Vector3.up * 0.21f;
            if (!Physics.Raycast(origin, direction, out RaycastHit hit,
                Settings.vaultReach + Settings.radius + 0.08f, ~0, QueryTriggerInteraction.Ignore))
                return false;
            ChaseVaultSurface surface = hit.collider.GetComponent<ChaseVaultSurface>();
            if (surface == null || hit.distance - Settings.radius > Settings.vaultReach
                || hit.distance < Settings.radius * 0.5f) return false;
            if (Vector3.Dot(direction, -hit.normal) < 0.7f) return false;
            if (surface.TopHeight > 1.25f || surface.DepthAlong(direction) > 0.6f) return false;
            Vector3 end = Body.position + direction * (surface.DepthAlong(direction) + 1.40f);
            float footY = Body.position.y - Settings.standingHeight * 0.5f;
            Vector3 landing = new Vector3(end.x, footY + Settings.standingHeight * 0.5f, end.z);
            if (Mathf.Abs(landing.x) > 5.55f || Mathf.Abs(landing.z) > 5.55f) return false;
            if (!ClearAt(landing + Vector3.up * 0.07f)) return false;
            // Test an elevated crossing volume before allowing a vault.
            Vector3 high = (Body.position + landing) * 0.5f + Vector3.up * (surface.TopHeight + 0.2f);
            return ClearAt(high);
        }

        bool ClearAt(Vector3 center)
        {
            float half = Settings.standingHeight * 0.5f - Settings.radius - 0.02f;
            Collider[] hits = Physics.OverlapCapsule(center + Vector3.up * half,
                center - Vector3.up * half, Settings.radius * 0.95f, ~0,
                QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
                if (hit != Capsule && !hit.transform.IsChildOf(transform) &&
                    hit.GetComponent<ChaseMotor>() == null &&
                    hit.bounds.max.y > center.y - Settings.standingHeight * 0.5f + 0.08f)
                    return false;
            return true;
        }

        void OrientVisual(Vector3 planar, float dt)
        {
            if (visual == null || planar.sqrMagnitude < 0.09f) return;
            float heading = Mathf.Atan2(planar.x, planar.z) * Mathf.Rad2Deg;
            visualYaw = Mathf.LerpAngle(visualYaw, heading, 1f - Mathf.Exp(-12f * dt));
            visual.localRotation = Quaternion.Euler(0f, visualYaw, 0f);
        }
    }
}
