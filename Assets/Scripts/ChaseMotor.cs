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
        bool previousJump;
        float vaultEntrySpeed;
        Vector3 vaultLanding;
        static PhysicsMaterial sharedCapsuleMaterial;
        readonly RaycastHit[] groundHits = new RaycastHit[16];
        readonly RaycastHit[] sweepHits = new RaycastHit[32];
        readonly Collider[] clearanceHits = new Collider[48];
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
            // Side contact friction must not pin a rising capsule against a shelf.
            // Both competitors receive precisely the same physics material.
            if (sharedCapsuleMaterial == null)
            {
                sharedCapsuleMaterial = new PhysicsMaterial("Chase frictionless capsule")
                {
                    dynamicFriction=0f, staticFriction=0f, bounciness=0f,
                    frictionCombine=PhysicsMaterialCombine.Minimum,
                    bounceCombine=PhysicsMaterialCombine.Minimum,
                    hideFlags=HideFlags.HideAndDontSave
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
            slideLeft = vaultLeft = 0f;
            lastGrounded = jumpQueuedUntil = lastJumpAt = -100f;
            previousDuck = previousJump = false;
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
            // A source provides a button state, never an independently repeatable command.
            if (action.JumpOrVault && !previousJump)
                jumpQueuedUntil = clock + Settings.jumpBuffer;
            previousJump = action.JumpOrVault;
            bool duckPressed = action.DuckOrSlide && !previousDuck;
            previousDuck = action.DuckOrSlide;

            if (State == ChaseMovementState.Vaulting)
            {
                vaultLeft -= dt;
                // Collision can stop forward progress while the body rises past the
                // lip. Re-accelerate within the SAME ground acceleration budget.
                // No positional writes, kinematic mode, or disabled collisions.
                planar = Vector3.MoveTowards(planar, committedDirection * vaultEntrySpeed,
                    Settings.groundAcceleration * dt);
                bool beyondShelf = Vector3.Dot(vaultLanding - Body.position, committedDirection) <= .05f;
                if (beyondShelf || (vaultLeft <= 0f && Grounded))
                    State = Grounded ? ChaseMovementState.Standing : ChaseMovementState.Airborne;
                else if (vaultLeft <= -0.55f)
                    State = ChaseMovementState.Airborne; // obstructed: regain normal control
            }
            else if (State == ChaseMovementState.Sliding)
            {
                slideLeft -= dt;
                planar = Vector3.MoveTowards(planar, Vector3.zero, Settings.slideDeceleration * dt);
                if (!Grounded)
                    State = ChaseMovementState.Airborne;
                else if (slideLeft <= 0f || planar.magnitude < 0.2f)
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
                else if (action.DuckOrSlide && (Grounded || State == ChaseMovementState.Crouching))
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
                    // An overhead obstruction blocks standing, including a competitor.
                    SetHeight(true);
                    State = Grounded ? ChaseMovementState.Crouching : ChaseMovementState.Airborne;
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
                        committedDirection = planar.normalized;
                        vaultEntrySpeed = Mathf.Min(Settings.runSpeed, planar.magnitude);
                        vaultLeft = Mathf.Max(Settings.vaultDuration,
                            Vector3.Distance(new Vector3(Body.position.x, 0f, Body.position.z),
                                new Vector3(vaultLanding.x, 0f, vaultLanding.z)) /
                                Mathf.Max(.01f, vaultEntrySpeed) + .22f);
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
            if (visual.GetComponent<ChibiVisualDriver>() != null)
            {
                // The independent Animator bends the chibi into a crouch.
                // Never shrink or move the whole production skeleton.
                visual.localScale = visualInitialScale;
                visual.localPosition = standingVisualLocalPosition;
                return;
            }
            // Legacy capsule fallback for projects without the art prefab.
            visual.localScale = new Vector3(visualInitialScale.x,
                visualInitialScale.y * h / Settings.standingHeight, visualInitialScale.z);
            visual.localPosition = standingVisualLocalPosition +
                Vector3.down * (Settings.standingHeight - h) * 0.5f;
        }

        bool HasStandingClearance()
        {
            if (Capsule == null || Capsule.height >= Settings.standingHeight - .01f) return true;
            return CapsuleClear(Body.position, true, false);
        }

        // Shape overlap, rather than AABB overlap, rejects false positives and
        // includes the other competitor. Ignore only the supporting floor plane.
        bool CapsuleClear(Vector3 center, bool standing, bool allowFloor)
        {
            float height = standing ? Settings.standingHeight : Capsule.height;
            float half = Mathf.Max(0f, height * .5f - Settings.radius);
            Vector3 low = center + Vector3.down * half;
            Vector3 high = center + Vector3.up * half;
            int count = Physics.OverlapCapsuleNonAlloc(high, low, Settings.radius,
                clearanceHits, ~0, QueryTriggerInteraction.Ignore);
            if (count == clearanceHits.Length) return false; // never accept truncated queries
            float oldHeight = Capsule.height;
            Vector3 oldCenter = Capsule.center;
            Capsule.height = height;
            Capsule.center = Vector3.zero;
            try
            {
                for (int i=0; i<count; i++)
                {
                    Collider hit = clearanceHits[i];
                    if (hit == null || hit == Capsule || hit.transform.IsChildOf(transform)) continue;
                    if (allowFloor && hit.bounds.max.y <= center.y - height * .5f + .03f)
                        continue;
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
            // Cast the lower hemisphere toward a support surface. A wall normal
            // cannot refresh jump/coyote time, irrespective of its bounds.
            float bottom = Body.position.y + Capsule.center.y - Capsule.height * .5f;
            Vector3 center = new Vector3(Body.position.x,
                bottom + Settings.radius + .11f, Body.position.z);
            int count = Physics.SphereCastNonAlloc(center, Settings.radius * .86f,
                Vector3.down, groundHits, .21f, ~0, QueryTriggerInteraction.Ignore);
            for (int i=0; i<count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.distance <= .0001f || hit.collider == null || hit.collider == Capsule ||
                    hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<ChaseMotor>() != null) continue;
                if (Vector3.Dot(hit.normal, Vector3.up) >= .65f) return true;
            }
            return false;
        }

        bool TryBeginVault(Vector3 planar)
        {
            if (!Grounded || planar.magnitude < Settings.vaultApproachSpeed) return false;
            Vector3 dir = planar.normalized;
            Vector3 origin = Body.position + Vector3.up * .21f;
            if (!Physics.Raycast(origin, dir, out RaycastHit hit,
                    Settings.vaultReach + Settings.radius + .08f,
                    ~0, QueryTriggerInteraction.Ignore)) return false;
            ChaseVaultSurface roof = hit.collider.GetComponent<ChaseVaultSurface>();
            if (roof == null || hit.distance - Settings.radius > Settings.vaultReach ||
                hit.distance < Settings.radius * .5f ||
                Vector3.Dot(dir, -hit.normal) < .7f ||
                roof.TopHeight > 1.25f || roof.DepthAlong(dir) > .60f) return false;
            // Solve the exact horizontal exit of the slab from the entry point.
            if (!roof.TryExitDistance(hit.point, dir, out float exitDistance)) return false;
            float travel = hit.distance + exitDistance + Settings.radius + .15f;
            Vector3 destination = Body.position + dir * travel;
            if (Mathf.Abs(destination.x) > 5.5f || Mathf.Abs(destination.z) > 5.5f ||
                !CapsuleClear(destination, true, true)) return false;

            // Validate a swept capsule at clearance altitude, rather than a
            // single midpoint. This rejects pillars/characters across the roof.
            float rise = roof.TopHeight + .18f;
            Vector3 elevatedStart = Body.position + Vector3.up * rise;
            if (!CapsuleClear(elevatedStart, true, false) ||
                !CapsuleClear(destination + Vector3.up * rise, true, false))
                return false;
            float half = Settings.standingHeight * .5f - Settings.radius;
            Vector3 low = elevatedStart - Vector3.up * half;
            Vector3 high = elevatedStart + Vector3.up * half;
            int count = Physics.CapsuleCastNonAlloc(high, low, Settings.radius * .98f,
                dir, sweepHits, travel, ~0, QueryTriggerInteraction.Ignore);
            if (count == sweepHits.Length) return false;
            for (int i=0;i<count;i++)
            {
                Collider candidate = sweepHits[i].collider;
                if (candidate == null || candidate == Capsule ||
                    candidate.transform.IsChildOf(transform)) continue;
                return false;
            }
            // The launch point must permit ascent with collisions still enabled.
            if (Physics.CapsuleCastNonAlloc(
                    Body.position + Vector3.up * half, Body.position - Vector3.up * half,
                    Settings.radius * .97f, Vector3.up, sweepHits, rise,
                    ~0, QueryTriggerInteraction.Ignore) == sweepHits.Length) return false;
            vaultLanding = destination;
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
