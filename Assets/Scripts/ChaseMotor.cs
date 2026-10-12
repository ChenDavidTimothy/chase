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
        public bool DiveColliderActive => Capsule != null && Capsule.enabled &&
            Capsule.direction == 2 && State != ChaseMovementState.Standing &&
            State != ChaseMovementState.Crouching;
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
        // Actual horizontal crawl motion, not input intent or an animation
        // command. The character keeps the locked diving body orientation.
        public bool ProneCrawling => State == ChaseMovementState.DiveProne &&
            Grounded && HorizontalVelocity.sqrMagnitude > .10f;
        public Vector3 FacingDirection => new Vector3(Mathf.Sin(visualYaw * Mathf.Deg2Rad), 0f,
            Mathf.Cos(visualYaw * Mathf.Deg2Rad));
        bool IsUpright => Capsule != null && Capsule.enabled && Capsule.direction == 1 && Capsule.height >= Settings.standingHeight - .01f &&
            State != ChaseMovementState.Crouching && State != ChaseMovementState.Sliding &&
            State != ChaseMovementState.SlideRecovery;

        float lastGrounded = -100f, lastTakeoff = -100f, jumpQueuedUntil = -100f;
        float slideLeft, recoveryLeft;
        bool compactDive, forwardContact;
        bool slideCompact, slideRising;
        bool solidSupportContact;
        Vector3 supportActorPosition;
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
            // Exactly one CapsuleCollider handles upright, airborne and
            // horizontal dive. The only BoxCollider is the football slide.
            SlideBox = GetComponent<BoxCollider>();
            if (SlideBox == null) SlideBox = gameObject.AddComponent<BoxCollider>();
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
            compactDive = forwardContact = false;
            slideCompact = slideRising = solidSupportContact = false;
            Body.rotation = Quaternion.identity;
            Capsule.enabled = true;
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
            // The horizontal body can be supported at its head or feet
            // while its center hangs over a shelf edge. A single center ray
            // misses that contact and previously left Leaping forever.
            // A Rigidbody may be moved/reset between physics ticks. Never
            // reuse a stale floor contact from the previous position.
            bool supported = solidSupportContact &&
                (Body.position-supportActorPosition).sqrMagnitude < .01f;
            solidSupportContact = false;
            Grounded = (supported || DetectGround()) &&
                (clock - lastTakeoff > .10f || velocity.y <= 0f);
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
                // The rounded capsule and compact upright fallback share one
                // impact-to-recovery path without extra collision shapes.
                // Never reapply launch velocity or accept midair steering.
                if (forwardContact)
                {
                    planar = Vector3.zero;
                    velocity.y = Mathf.Min(velocity.y,0f);
                }
                // A wall can also wedge the rounded body at its front without
                // generating a support normal directly under the actor.
                // Treat a genuine, motionless impact as a landing attempt
                // rather than maintaining an eternal zero-speed Leaping pose.
                bool wedgedAfterImpact = forwardContact &&
                    clock-lastTakeoff > .18f &&
                    planar.sqrMagnitude < .015f &&
                    Mathf.Abs(velocity.y) < .20f;
                if ((Grounded || wedgedAfterImpact) &&
                    clock - lastTakeoff > .12f)
                {
                    jumpQueuedUntil = -100f;
                    State = ChaseMovementState.DiveProne;
                    recoveryLeft = Settings.diveImpactRecovery;
                    // Chest contact dissipates airborne projectile momentum.
                    // Enter the prone state at crawl speed or below so WASD
                    // responds immediately rather than fighting 9m/s thrust.
                    planar = Vector3.ClampMagnitude(planar,
                        Settings.proneCrawlSpeed);
                    // The same solid capsule stays enabled throughout.
                }
            }
            else if (State == ChaseMovementState.DiveProne)
            {
                recoveryLeft = Mathf.Max(0f,recoveryLeft-dt);
                // ONE universal rule. Every chest landing permits modest
                // WASD crawling (including sideways/backwards), with no
                // automatic recoil, no turning the prone collider and no
                // context-specific shelf behavior. No input -> brake to rest.
                // Impact must still pay its 0.40s commitment. The first tick
                // after it ends automatically attempts to stand if clear.
                Vector3 crawlTarget = Grounded ?
                    direction * Settings.proneCrawlSpeed : Vector3.zero;
                float crawlRate = direction.sqrMagnitude > .001f && Grounded ?
                    Settings.groundAcceleration : Settings.groundBraking;
                planar = Vector3.MoveTowards(planar,crawlTarget,crawlRate*dt);
                if (recoveryLeft<=0f &&
                    (compactDive ? TryFinishCompactDive() : TryFinishProneDive()))
                {
                    // Automatic get-up takes precedence over continued
                    // crawling whenever the real volume is clear. There is
                    // no prone toggle or ability to camp beneath open sky.
                    State=ChaseMovementState.LeapRecovery;
                    recoveryLeft=Settings.leapRecovery;
                    planar=Vector3.zero;
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
                    // The single capsule was already changed to crouch at
                    // recovery entry. Return to normal locomotion now.
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
                    // The launch ALWAYS happens. The same existing solid
                    // capsule rotates horizontally in open space; beside a
                    // wall/competitor it stays upright to avoid starting
                    // inside another physical body.
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
                State != ChaseMovementState.LeapRecovery)
                OrientVisual(planar, dt);
        }

        void OnCollisionEnter(Collision collision) => RegisterDiveContact(collision);
        void OnCollisionStay(Collision collision) => RegisterDiveContact(collision);

        void RegisterDiveContact(Collision collision)
        {
            bool otherActor=collision.collider!=null &&
                collision.collider.GetComponentInParent<ChaseMotor>()!=null;
            // Count contact on the front/rear of the rounded capsule as support.
            for (int i=0;i<collision.contactCount;i++)
            {
                var contact=collision.GetContact(i);
                if (!otherActor && contact.normal.y >= .65f)
                {
                    solidSupportContact=true;
                    supportActorPosition=Body.position;
                }
                if (State != ChaseMovementState.Leaping || forwardContact)continue;
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
                if (hit==null || hit==Capsule || hit==SlideBox ||
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

        // One rounded capsule is safer at shelf corners than the previous
        // long cuboid. Query the exact prospective rounded envelope before
        // rotating the live collider; no overlaps, no collider gap.
        bool CanFitDiveShape(Vector3 position)
        {
            Quaternion rotation=Quaternion.LookRotation(committedDirection,Vector3.up);
            Vector3 c=position+rotation*new Vector3(0f,
                Settings.diveCapsuleCenterY,Settings.diveCapsuleCenterForward);
            float half=Settings.diveCapsuleLength*.5f-Settings.diveCapsuleRadius;
            Vector3 axis=rotation*Vector3.forward*half;
            int n=Physics.OverlapCapsuleNonAlloc(c-axis,c+axis,
                Settings.diveCapsuleRadius-.006f,clearanceHits,
                ~0,QueryTriggerInteraction.Ignore);
            if(n==clearanceHits.Length)return false;
            for(int i=0;i<n;i++)
            {
                var other=clearanceHits[i];
                if(other==null || other==Capsule || other==SlideBox ||
                    other.transform.IsChildOf(transform))continue;
                return false;
            }
            return true;
        }

        bool TryFinishCompactDive()
        {
            // A wall-close dive launches on the existing upright capsule.
            // It follows the same clearance rule as every other chest
            // landing; never force get-up because it was compact at launch.
            if (!Grounded || !CanFitVerticalShape(
                Settings.crouchingHeight,Body.position))return false;
            SetHeight(Settings.crouchingHeight);
            return true;
        }

        // A single transition at recovery entry replaces the previous box
        // union, repeated upward push, and per-frame complex morph checks.
        // Check a small vertical sweep and the COMPLETE upright crouch body
        // before moving or resizing any live collider.
        bool TryFinishProneDive()
        {
            if (!Grounded) return false;
            Vector3 start=Body.position;
            Vector3 goal=new Vector3(start.x,
                Settings.standingHeight*.5f,start.z);
            if (!CanFitVerticalShape(Settings.crouchingHeight,goal))
                return false;
            for(int i=1;i<=4;i++)
                if(!CanFitDiveShape(Vector3.Lerp(start,goal,i*.25f)))
                    return false;
            // A previously verified, small center-height adjustment to
            // place a feet-grounded crouch. No upward velocity and no
            // force capable of lifting through an obstruction.
            Body.position=goal;
            Body.rotation=Quaternion.identity;
            SetHeight(Settings.crouchingHeight);
            if (visual!=null)
                visual.localRotation=Quaternion.Euler(0f,visualYaw,0f);
            return true;
        }

        bool CanFitVerticalShape(float height, Vector3 actorPosition)
        {
            Vector3 offset = Vector3.down * (Settings.standingHeight - height) * .5f;
            Vector3 center = actorPosition + offset;
            float half = Mathf.Max(0f, height * .5f - Settings.radius);
            return CanOccupy(center+Vector3.up*half,center-Vector3.up*half,
                Settings.radius);
        }

        bool CanOccupy(Vector3 a,Vector3 b,float radius)
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
                if (hit == null || hit == Capsule || hit == SlideBox ||
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
            if(!CanFitDiveShape(Body.position))return false;
            Body.rotation=Quaternion.LookRotation(committedDirection,Vector3.up);
            Capsule.direction=2;
            Capsule.radius=Settings.diveCapsuleRadius;
            Capsule.height=Settings.diveCapsuleLength;
            Capsule.center=new Vector3(0f,Settings.diveCapsuleCenterY,
                Settings.diveCapsuleCenterForward);
            if (visual!=null)visual.localRotation=Quaternion.identity;
            return true;
        }

        void SetHeight(float h)
        {
            // Restore the ordinary vertical shape at the single well-defined
            // recovery transition; slide and normal crouch reuse it.
            Capsule.direction=1;
            Capsule.radius=Settings.radius;
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
                Body.position.y + Capsule.center.y -
                (Capsule.direction==1?Capsule.height*.5f:Capsule.radius) :
                float.PositiveInfinity;
            if (DiveColliderActive)
                bottom=Mathf.Min(bottom,Body.position.y+
                    Capsule.center.y-Capsule.radius);
            if (SlideColliderActive)
                bottom = Mathf.Min(bottom,Body.position.y+
                    SlideBox.center.y-SlideBox.size.y*.5f);
            // Sample center and both ends of long physical bodies: support
            // beneath an edge-contacting shoulder
            // would be invisible to the old center-only probe.
            int probes=(DiveColliderActive || SlideColliderActive)?3:1;
            float reach=DiveColliderActive ? Settings.diveCapsuleLength*.34f :
                SlideColliderActive ? SlideBox.size.z*.35f : 0f;
            for(int probe=0;probe<probes;probe++)
            {
                Vector3 offset=probe==0?Vector3.zero:
                    Body.rotation*Vector3.forward*(probe==1?reach:-reach);
                Vector3 start=Body.position+offset;
                start.y=bottom+Settings.radius+.11f;
                int count=Physics.SphereCastNonAlloc(start,Settings.radius*.86f,
                    Vector3.down,groundHits,.21f,~0,QueryTriggerInteraction.Ignore);
                for (int i=0;i<count;i++)
                {
                    RaycastHit hit=groundHits[i];
                    if (hit.distance<=.0001f || hit.distance>.177f ||
                        hit.collider==null || hit.collider==Capsule ||
                        hit.collider==SlideBox ||
                        hit.collider.transform.IsChildOf(transform) ||
                        hit.collider.GetComponentInParent<ChaseMotor>()!=null)
                        continue;
                    if (Vector3.Dot(hit.normal,Vector3.up)>=.65f)return true;
                }
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
