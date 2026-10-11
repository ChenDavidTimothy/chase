using UnityEngine;

namespace Chase
{
    // Animation is a one-way projection of the authoritative motor state.
    // Nothing here modifies Rigidbody, CapsuleCollider, ChaseAction, or tag rules.
    [DisallowMultipleComponent]
    public sealed class ChibiVisualDriver : MonoBehaviour
    {
        [SerializeField] Animator animator;
        ChaseMotor motor;
        int previousState = -1;
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

        // The visual prefab is instantiated before the match attaches ChaseMotor.
        // Awake cannot resolve that component yet. Binding must be explicit.
        public void BindMotor(ChaseMotor sharedMotor)
        {
            if (sharedMotor == null) throw new System.ArgumentNullException(nameof(sharedMotor));
            motor = sharedMotor;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            ResetAnimation();
        }

        public void ResetAnimation()
        {
            previousState = -1;
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.Play("Idle", 0, 0f);
                animator.Update(0f);
            }
        }

        void LateUpdate()
        {
            if (motor == null || animator == null || !animator.isActiveAndEnabled) return;
            string state;
            switch (motor.State)
            {
                case ChaseMovementState.Crouching: state = "Crouch"; break;
                case ChaseMovementState.Sliding: state = "Slide"; break;
                case ChaseMovementState.Vaulting: state = "Vault"; break;
                case ChaseMovementState.Airborne: state = "Jump"; break;
                default: state = motor.HorizontalVelocity.sqrMagnitude > .55f ? "Run" : "Idle"; break;
            }
            int next = Animator.StringToHash(state);
            if (next != previousState)
            {
                animator.CrossFadeInFixedTime(next, .10f, 0);
                previousState = next;
            }
            // Intentionally no animator root-motion handling.
        }
    }
}
