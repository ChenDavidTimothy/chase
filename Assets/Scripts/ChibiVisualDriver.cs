using UnityEngine;
namespace Chase
{
    // Animation is presentation only. Physical state/contact drives transitions.
    [DisallowMultipleComponent]
    public sealed class ChibiVisualDriver : MonoBehaviour
    {
        [SerializeField] Animator animator;
        ChaseMotor motor;
        int previousState=-1;
        ChaseMovementState lastPhysicalState;
        float landingLeft;
        public string CurrentVisualState { get; private set; } = "Idle";
        public Animator Animator => animator;
        public ChaseMotor Motor => motor;
        void Awake()
        {
            motor=GetComponentInParent<ChaseMotor>();
            if(animator==null)animator=GetComponentInChildren<Animator>(true);
            if(animator!=null)
            {
                animator.applyRootMotion=false;
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode=AnimatorUpdateMode.Normal;
            }
        }
        public void BindMotor(ChaseMotor sharedMotor)
        {
            if(sharedMotor==null)throw new System.ArgumentNullException(nameof(sharedMotor));
            motor=sharedMotor;
            if(animator==null)animator=GetComponentInChildren<Animator>(true);
            ResetAnimation();
        }
        public void ResetAnimation()
        {
            previousState=-1;
            landingLeft=0f;
            lastPhysicalState=ChaseMovementState.Standing;
            CurrentVisualState="Idle";
            if(animator!=null&&animator.isActiveAndEnabled)
            {animator.speed=1f;animator.Play("Idle",0,0f);animator.Update(0f);}
        }
        void LateUpdate()
        {
            if(motor==null||animator==null||!animator.isActiveAndEnabled)return;
            var state=motor.State;
            bool landed=motor.Grounded &&
                (lastPhysicalState==ChaseMovementState.Jumping||
                 lastPhysicalState==ChaseMovementState.Airborne) &&
                (state==ChaseMovementState.Standing||state==ChaseMovementState.Crouching);
            if(landed)landingLeft=.14f;
            else landingLeft=Mathf.Max(0f,landingLeft-Time.deltaTime);
            lastPhysicalState=state;
            string name;
            switch(state)
            {
                case ChaseMovementState.Leaping:name="Leap";break;
                case ChaseMovementState.LeapRecovery:name="LeapRecovery";break;
                case ChaseMovementState.Sliding:name="Slide";break;
                case ChaseMovementState.SlideRecovery:name="SlideRecovery";break;
                case ChaseMovementState.Jumping:
                case ChaseMovementState.Airborne:name="Jump";break;
                case ChaseMovementState.Crouching:
                    name=motor.HorizontalVelocity.magnitude>.35f?"CrouchWalk":"Crouch";break;
                default:
                    name=landingLeft>0f?"JumpLanding":
                        motor.HorizontalVelocity.sqrMagnitude>.55f?"Run":"Idle";
                    break;
            }
            CurrentVisualState=name;
            int hash=Animator.StringToHash(name);
            animator.speed=name=="Run" ?
                Mathf.Clamp(motor.HorizontalVelocity.magnitude/5.0f,.65f,1.25f) :
                name=="CrouchWalk"?.9f:1f;
            if(hash!=previousState)
            {
                animator.CrossFadeInFixedTime(hash,.075f,0);
                previousState=hash;
            }
        }
    }
}
