using System;
using UnityEngine;

namespace Chase
{
    // Strictly presentation-only. Never changes physics, collider or velocity.
    // Reconciles the real Animator state with the motor on every frame rather
    // than trusting a cached request, which previously allowed stuck slide poses.
    [DisallowMultipleComponent]
    public sealed class ChibiVisualDriver : MonoBehaviour
    {
        [SerializeField] Animator animator;
        ChaseMotor motor;
        ChaseMovementState lastPhysicalState;
        float landingLeft;
        int requestedHash = -1;
        public string CurrentVisualState { get; private set; } = "Idle";
        public Animator Animator => animator;
        public ChaseMotor Motor => motor;

        void Awake()
        {
            motor = GetComponentInParent<ChaseMotor>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
            }
        }

        public void BindMotor(ChaseMotor sharedMotor)
        {
            if (sharedMotor == null) throw new ArgumentNullException(nameof(sharedMotor));
            motor = sharedMotor;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
                throw new InvalidOperationException("CHASE character missing production Animator Controller");
            ResetAnimation();
        }

        public void ResetAnimation()
        {
            requestedHash = -1;
            landingLeft = 0f;
            lastPhysicalState = ChaseMovementState.Standing;
            CurrentVisualState = "Idle";
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.speed = 1f;
                animator.Play("Idle", 0, 0f);
                animator.Update(0f);
            }
        }

        void LateUpdate()
        {
            if (motor == null || animator == null || !animator.isActiveAndEnabled ||
                !animator.isInitialized) return;

            ChaseMovementState state = motor.State;
            bool landed = motor.Grounded &&
                (lastPhysicalState == ChaseMovementState.Jumping ||
                 lastPhysicalState == ChaseMovementState.Airborne) &&
                (state == ChaseMovementState.Standing || state == ChaseMovementState.Crouching);
            landingLeft = landed ? .14f : Mathf.Max(0f, landingLeft - Time.deltaTime);
            lastPhysicalState = state;
            string name;
            switch (state)
            {
                case ChaseMovementState.Leaping: name = "Leap"; break;
                case ChaseMovementState.LeapRecovery: name = "LeapRecovery"; break;
                case ChaseMovementState.Sliding: name = "Slide"; break;
                case ChaseMovementState.SlideRecovery: name = "SlideRecovery"; break;
                case ChaseMovementState.Jumping:
                case ChaseMovementState.Airborne: name = "Jump"; break;
                case ChaseMovementState.Crouching:
                    name = motor.HorizontalVelocity.magnitude > .35f ? "CrouchWalk" : "Crouch";
                    break;
                default:
                    name = landingLeft > 0f ? "JumpLanding" :
                        motor.HorizontalVelocity.sqrMagnitude > .55f ? "Run" : "Idle";
                    break;
            }
            CurrentVisualState = name;
            int desired = Animator.StringToHash(name);
            if (!animator.HasState(0, desired))
            {
                Debug.LogError("CHASE missing Animator state: " + name, this);
                return;
            }
            animator.speed = name == "Run" ?
                Mathf.Clamp(motor.HorizontalVelocity.magnitude / 5f, .65f, 1.25f) :
                name == "CrouchWalk" ? .9f : 1f;

            bool inTransition = animator.IsInTransition(0);
            int current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            int next = inTransition ? animator.GetNextAnimatorStateInfo(0).shortNameHash : 0;
            // Reassert a motor-owned state if another Animator state, a reset,
            // or a broken transition moved the visual elsewhere. In particular,
            // never retain Slide after the physical motor has exited Sliding.
            if (desired != requestedHash || (current != desired && next != desired))
            {
                animator.CrossFadeInFixedTime(desired, .06f, 0);
                requestedHash = desired;
            }
        }
    }
}
