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
        public BoxCollider SlideBox { get; private set; }
        public bool SlideColliderActive => SlideBox != null && SlideBox.enabled;
        public bool SlideCompactContact => State == ChaseMovementState.Sliding && slideCompact;
        public bool SlideGettingUp => State == ChaseMovementState.SlideRecovery && slideRising;
        // At least one solid Collider always remains active; neither is a trigger.
        public ChaseMovementState State { get; private set; }
        public bool Grounded { get; private set; }
        public Vector3 Velocity => Body.linearVelocity;
        public Vector3 HorizontalVelocity => Vector3.ProjectOnPlane(Body.linearVelocity, Vector3.up);
        public bool CanStand => Capsule != null && Capsule.enabled &&
            !DiveColliderActive && !SlideColliderActive &&
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
        // The same committed move always starts. Where the long body cannot
        // safely fit, it starts with the already-solid compact contact shape.
        // No takeoff is refused for a nearby wall or another athlete.
        public bool DiveCompactContact => State == ChaseMovementState.Leaping &&
            compactDive;
        // Any forward wall/competitor impact ends the projectile flight, for
        // BOTH physical body shapes. Grounded recovery is the same move.
        public bool DiveFlightEnded => State == ChaseMovementState.Leaping && forwardContact;
        public bool DiveImpactBraced => DiveCompactContact && forwardContact;
        public bool DiveCompactRecovery => State == ChaseMovementState.LeapRecovery &&
            compactDive;
        public bool DiveCompactProne => State == ChaseMovementState.DiveProne &&
            compactDive;
        public Vector3 FacingDirection => new Vector3(Mathf.Sin(visualYaw * Mathf.Deg2Rad), 0f,
            Mathf.Cos(visualYaw * Mathf.Deg2Rad));
        bool IsUpright => Capsule != null && Capsule.enabled && Capsule.direction == 1 && Capsule.height >= Settings.standingHeight - .01f &&
            State != ChaseMovementState.Crouching && State != ChaseMovementState.Sliding &&
            State != ChaseMovementState.SlideRecovery;

        float lastGrounded = -100f, lastTakeoff = -100f, jumpQueuedUntil = -100f;
        float slideLeft, recoveryLeft;
        bool diveShapeCommitted, diveGettingUp, compactDive, forwardContact;
        bool slideCompact, slideRising;
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
            SlideBox = gameObject.AddComponent<BoxCollider>();
            SlideBox.center = new Vector3(0f,settings.slideBodyCenterY,
                settings.slideBodyCenterForward);
            SlideBox.size = new Vector3(settings.slideBodyWidth,
                settings.slideBodyHeight,settings.slideBodyLength);
            SlideBox.isTrigger = false;
            SlideBox.enabled = false;
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
            SlideBox.sharedMaterial = sharedCapsuleMaterial;
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
            diveShapeCommitted = diveGettingUp = compactDive = forwardContact = false;
            slideCompact = slideRising = false;
            Body.rotation = Quaternion.identity;
            Capsule.enabled = true;
            DiveBox.enabled = false;
            SlideBox.enabled = false;
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
                State != ChaseMovementState.Leaping &&
                State != ChaseMovementState.Crouching && State != ChaseMovementState.Sliding)
                jumpQueuedUntil = clock + Settings.jumpBuffer;

            if (State == ChaseMovementState.Leaping)
            {
                // One ballistic flight; collision is its physical endpoint.
                // The full box in open space and the non-overlapping compact
                // collider next to a wall have identical impact consequences.
                // Never reapply launch velocity or accept midair steering.
                if (forwardContact)
                {
                    planar = Vector3.zero;
                    velocity.y = Mathf.Min(velocity.y,0f);
                }
                if (Grounded && clock - lastTakeoff > .12f)
                {
                    jumpQueuedUntil = -100f;
                    State = ChaseMovementState.DiveProne;
                    recoveryLeft = Settings.diveImpactRecovery;
                    // Keep the collision body solid all through recovery;
                    // a compact contact never expands through an obstacle.
                }
            }
            else if (State == ChaseMovementState.DiveProne)
            {
                // Physically chest-first on the ground; held buttons cannot
                // cancel this recovery. Existing momentum brakes rather than
                // being replaced by a new horizontal impulse.
                recoveryLeft = Mathf.Max(0f, recoveryLeft - dt);
                // Once impact is over, W permits a slow crawl toward the
                // shelf exit. Sideways or reverse input cannot steer the
                // headfirst body; no input means no automatic ground glide.
                bool crawlForward = Vector3.Dot(direction,committedDirection) > .5f;
                planar = Vector3.MoveTowards(planar, committedDirection *
                    (recoveryLeft > 0f || diveGettingUp || !crawlForward ? 0f :
                        Settings.proneCrawlSpeed),Settings.groundBraking * dt);
                if (recoveryLeft <= 0f && compactDive)
                {
                    // The compact collision body stayed upright on impact.
                    // It does not need the horizontal body's push-up phase.
                    // Nevertheless it has paid the SAME prone-impact timer,
                    // followed by the SAME .35s committed get-up timer.
                    State = ChaseMovementState.LeapRecovery;
                    recoveryLeft = Settings.leapRecovery;
                }
                else if (recoveryLeft <= 0f)
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
                slideLeft = Mathf.Max(0f, slideLeft - dt);
                // The committed direction is fixed at takeoff. WASD cannot
                // steer or add speed, and a solid wall may reduce it sooner.
                planar = Vector3.MoveTowards(planar,Vector3.zero,
                    Settings.slideDeceleration * dt);
                if (slideLeft <= 0f && Grounded)
                {
                    State = ChaseMovementState.SlideRecovery;
                    slideRising = false;
                    recoveryLeft = Settings.slideRecovery;
                    jumpQueuedUntil = -100f;
                }
                // Off the ledge: continue falling in the same SOLID slide
                // body. Do not expand an upright capsule or play a grounded
                // recovery clip while airborne.
            }
            else if (State == ChaseMovementState.SlideRecovery)
            {
                bool wantsForward = Vector3.Dot(direction,committedDirection)>.5f;
                if (!slideRising)
                {
                    if (Grounded && (slideCompact ||
                        CanFitVerticalShape(Settings.crouchingHeight,Body.position)))
                    {
                        BeginSlideGetup();
                        planar=Vector3.MoveTowards(planar,Vector3.zero,
                            Settings.groundBraking*dt);
                        slideRising = true;
                        recoveryLeft = Settings.slideRecovery;
                    }
                    else if (Grounded && wantsForward)
                    {
                        // A roof lower than even crouch clearance cannot
                        // force us to expand through it. Crawl forward in the
                        // committed heading until there's space to rise.
                        planar = Vector3.MoveTowards(planar,
                            committedDirection*Settings.slideCrawlSpeed,
                            Settings.groundAcceleration*dt);
                    }
                    else planar=Vector3.MoveTowards(planar,Vector3.zero,
                        Settings.groundBraking*dt);
                }
                else
                {
                    planar=Vector3.MoveTowards(planar,Vector3.zero,
                        Settings.groundBraking*dt);
                    recoveryLeft = Mathf.Max(0f,recoveryLeft-dt);
                    if (recoveryLeft<=0f)
                    {
                        FinishSlideGetup();
                        if (action.Crouch || !HasStandingClearance())
                        {
                            SetHeight(Settings.crouchingHeight);
                            State = ChaseMovementState.Crouching;
                        }
                        else
                        {
                            SetHeight(Settings.standingHeight);
                            State = Grounded ? ChaseMovementState.Standing :
                                ChaseMovementState.Airborne;
                        }
                    }
                }
            }
            else if (Recovering)
            {
                recoveryLeft = Mathf.Max(0f, recoveryLeft - dt);
                // Braking must respect acceleration, not snap velocity to 2 m/s.
                float top = Settings.recoverySpeed;
                // A dive get-up is still a commitment. Holding W/A/S/D
                // cannot move the full-body collider until the athlete stands.
                Vector3 target = State == ChaseMovementState.LeapRecovery ?
                    Vector3.zero : direction * top;
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
                    compactDive = false;
                    forwardContact = false;
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
                    // Input sets aim at takeoff, even when W and Up are
                    // pressed on the exact same frame from an idle start.
                    // Without WASD, dive in the character's facing direction.
                    // After this moment, WASD cannot steer a single degree.
                    committedDirection = direction.sqrMagnitude > .001f ?
                        direction.normalized : FacingDirection;
                    // Decide the safe COLLISION shape, not whether the
                    // player's move is accepted. Nearby walls or other
                    // competitors can block the long box but cannot cancel
                    // the headfirst attempt. The compact rigidbody still
                    // hits those obstacles and causes legitimate contact.
                    compactDive = !TryEnterDiveShape();
                    forwardContact = false;
                    if (compactDive && visual != null)
                    {
                        // An immediate W+Up/A+Up also turns the *compact*
                        // attempt in the requested heading on the launch
                        // frame, just like the full horizontal box.
                        visualYaw = Mathf.Atan2(committedDirection.x,
                            committedDirection.z) * Mathf.Rad2Deg;
                        visual.localRotation = Quaternion.Euler(0f,visualYaw,0f);
                    }
                    planar = committedDirection * Settings.leapHorizontalSpeed;
                    velocity.y = Settings.leapVerticalSpeed;
                    State = ChaseMovementState.Leaping;
                    diveGettingUp = false;
                    lastTakeoff = clock;
                    Grounded = false;
                }
                else if (doSlide)
                {
                    jumpQueuedUntil = -100f;
                    committedDirection = planar.sqrMagnitude > .01f ?
                        planar.normalized : direction.sqrMagnitude > .001f ?
                        direction.normalized : FacingDirection;
                    // The Animator faces the physical sliding heading,
                    // including a diagonal moving slide, before the solid
                    // body swaps shape. Exit restores exactly this yaw.
                    visualYaw = Mathf.Atan2(committedDirection.x,
                        committedDirection.z)*Mathf.Rad2Deg;
                    slideCompact = !TryEnterSlideShape();
                    slideRising = false;
                    if (slideCompact)
                    {
                        SetHeight(Settings.slideHeight);
                        if (visual != null)
                            visual.localRotation = Quaternion.Euler(0f,visualYaw,0f);
                    }
                    State = ChaseMovementState.Sliding;
                    slideLeft = Settings.slideDuration;
                    planar = Vector3.MoveTowards(planar,Vector3.zero,
                        Settings.slideDeceleration*dt);
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
                State != ChaseMovementState.Sliding &&
                State != ChaseMovementState.SlideRecovery &&
                !(State == ChaseMovementState.LeapRecovery && DiveColliderActive))
                OrientVisual(planar, dt);
        }

        void OnCollisionEnter(Collision collision) => RegisterDiveContact(collision);
        void OnCollisionStay(Collision collision) => RegisterDiveContact(collision);

        void RegisterDiveContact(Collision collision)
        {
            if (State != ChaseMovementState.Leaping || forwardContact) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                // Real forward wall/opponent contact, regardless of which
                // solid body shape is in use. Ordinary floor contact does
                // not prematurely stop a dive, but a ceiling hit does.
                if ((Mathf.Abs(contact.normal.y) < .58f &&
                    Vector3.Dot(contact.normal, committedDirection) < -.20f) ||
                    contact.normal.y < -.70f)
                {
                    forwardContact = true;
                    // The impact is the END of the projectile, not a flag
                    // delaying its stop until the next motor tick. Reconcile
                    // actual Rigidbody velocity directly in the PhysX contact
                    // callback. Gravity then handles the fall. No thrust
                    // can be regenerated in subsequent frames.
                    Vector3 v = Body.linearVelocity;
                    Body.linearVelocity = new Vector3(0f,Mathf.Min(0f,v.y),0f);
                    return;
                }
            }
        }

        bool TryEnterSlideShape()
        {
            Quaternion heading=Quaternion.LookRotation(committedDirection,Vector3.up);
            Vector3 size=SlideBox.size;
            Vector3 worldCenter=Body.position+heading*SlideBox.center;
            int count=Physics.OverlapBoxNonAlloc(worldCenter,size*.5f,
                clearanceHits,heading,~0,QueryTriggerInteraction.Ignore);
            if (count==clearanceHits.Length)return false;
            for (int i=0;i<count;i++)
            {
                Collider hit=clearanceHits[i];
                if (hit==null || hit==Capsule || hit==DiveBox || hit==SlideBox ||
                    hit.transform.IsChildOf(transform))continue;
                // Refuse only the unsafe shape switch, NEVER the player's
                // Down action. Near a wall/opponent a compact SOLID capsule
                // still makes genuine momentum-based contact and recovery.
                return false;
            }
            Body.rotation=heading;
            SlideBox.enabled=true;
            Capsule.enabled=false;
            if (visual!=null)visual.localRotation=Quaternion.identity;
            return true;
        }

        void BeginSlideGetup()
        {
            if (!SlideColliderActive)return; // compact capsule already solid
            // Crouch envelope was queried without intersections first.
            // The union protects the transition from reclined torso to knee.
            Capsule.direction=1;
            Capsule.radius=Settings.radius;
            SetHeight(Settings.crouchingHeight);
            Capsule.enabled=true; // keep SlideBox until animation finishes
        }

        void FinishSlideGetup()
        {
            if (SlideColliderActive)
            {
                // Capsule remains solid while Box is retired.
                Body.rotation=Quaternion.identity;
                SlideBox.enabled=false;
                if (visual!=null)
                    visual.localRotation=Quaternion.Euler(0f,visualYaw,0f);
            }
            slideRising=false;
            slideCompact=false;
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
                if (hit == null || hit == Capsule || hit == DiveBox || hit == SlideBox ||
                    hit.transform.IsChildOf(transform)) continue;
                // At takeoff the Box is disabled: be conservative, never
                // create a newly active Collider intersecting an obstacle.
                if (!DiveBox.enabled) return false;
                // While getting up the Box is ALREADY an active collider.
                // A wall touching its front face can be returned by the broad-
                // phase OverlapBox even when it is not truly penetrating.
                // Exact enabled-box penetration separates a valid tangential
                // contact from a real wall overlap at the projected rise.
                if (Physics.ComputePenetration(DiveBox, actorPosition, orientation,
                    hit, hit.transform.position, hit.transform.rotation,
                    out Vector3 normal, out float depth) && depth > .025f)
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
                if (hit == null || hit == Capsule || hit == DiveBox || hit == SlideBox ||
                    hit.transform.IsChildOf(transform)) continue;
                // Only the actual floor directly below our feet is exempt;
                // a raised shelf / wall / opponent may never be ignored.
                if (hit.bounds.max.y <= bottom + .020f) continue;
                return false;
            }
            return true;
        }

        bool TryEnterDiveShape()
        {
            if (!CanFitDiveShape(Body.position)) return false;
            Body.rotation = Quaternion.LookRotation(committedDirection, Vector3.up);
            // Enable the alternate full-body Collider BEFORE disabling the
            // upright one. There is never a collision-free physics step.
            DiveBox.enabled = true;
            Capsule.enabled = false;
            if (visual != null) visual.localRotation = Quaternion.identity;
            diveShapeCommitted = true;
            return true;
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
            if (SlideColliderActive)
                bottom = Mathf.Min(bottom,Body.position.y+
                    SlideBox.center.y-SlideBox.size.y*.5f);
            Vector3 start = new Vector3(Body.position.x,
                bottom + Settings.radius + .11f, Body.position.z);
            int count = Physics.SphereCastNonAlloc(start, Settings.radius * .86f,
                Vector3.down, groundHits, .21f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundHits[i];
                if (hit.distance <= .0001f || hit.distance > .177f || hit.collider == null || (hit.collider == Capsule || hit.collider == DiveBox || hit.collider == SlideBox) ||
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
