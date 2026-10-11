using UnityEngine;
using UnityEngine.InputSystem;
namespace Chase
{
    // Separate testing scene only. Uses the production action source and motor.
    public sealed class ChaseMovementGym : MonoBehaviour
    {
        [SerializeField] ChaseTuning tuning = new ChaseTuning();
        [SerializeField] Transform spawn;
        ChaseMotor motor;
        ChibiVisualDriver driver;
        Transform headBone, pelvisBone;
        ChaseKeyboard keyboard;
        Camera fixedCamera;
        Transform inputFrame;
        float clock;
        bool sideView;
        Vector3 motionStart;
        ChaseMovementState previousState;
        public ChaseMotor Motor => motor;
        void Awake()
        {
            Time.fixedDeltaTime=.02f;
            fixedCamera=Camera.main;
            // Keep the gym control basis constant as the inspection camera
            // swivels. Otherwise W would change direction on pressing V and
            // a headfirst projectile would remain foreshortened in every view.
            inputFrame=new GameObject("GYM / fixed original camera input basis").transform;
            inputFrame.SetParent(transform,false);
            inputFrame.rotation=Quaternion.Euler(0f,45f,0f);
            keyboard=gameObject.AddComponent<ChaseKeyboard>();
            var root=new GameObject("GYM / Shared motor actor");
            root.transform.SetParent(transform,true);
            root.transform.position=spawn!=null?spawn.position:new Vector3(-9,.9f,-8f);
            var body=root.AddComponent<Rigidbody>();
            var capsule=root.AddComponent<CapsuleCollider>();
            var prefab=Resources.Load<GameObject>("ChibiVisual");
            if(prefab==null)throw new System.InvalidOperationException("Production chibi missing");
            var visual=Instantiate(prefab,root.transform,false);
            visual.transform.localPosition=Vector3.down*(tuning.standingHeight*.5f);
            motor=root.AddComponent<ChaseMotor>();
            motor.Initialize(tuning,visual.transform);
            driver=visual.GetComponent<ChibiVisualDriver>();
            driver.BindMotor(motor);
            foreach(var t in visual.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="head")headBone=t;
                if(t.name=="pelvis")pelvisBone=t;
            }
            ResetActor();
            keyboard.ResetSource();
        }
        void ResetActor()
        {
            ResetActorAt(spawn!=null?spawn.position:new Vector3(-9,.9f,-8f));
        }
        void ResetActorAt(Vector3 start)
        {
            clock=0f;
            motor.ResetMotor(start);
            motionStart=motor.Body.position;
            previousState=ChaseMovementState.Standing;
            if(fixedCamera!=null)
                fixedCamera.transform.position=motor.Body.position+Vector3.up*.40f-
                    fixedCamera.transform.forward*6.8f;
            Physics.SyncTransforms();
        }
        void Update()
        {
            Keyboard k=Keyboard.current;
            // Inspection-only angle. The chase camera and motor are untouched.
            if(k!=null && k.vKey.wasPressedThisFrame)sideView=!sideView;
            if(k!=null && k.rKey.wasPressedThisFrame)
            {
                ResetActor();
                keyboard.ResetSource();
                return;
            }
            // Gym-only shortcut: stage a precisely aligned 3m shelf approach.
            // Up launches the real shared motor. This is NOT in a match.
            if(k!=null && k.gKey.wasPressedThisFrame)
            {
                ResetActorAt(new Vector3(0f,tuning.standingHeight*.5f,1f));
                keyboard.ResetSource();
                return;
            }
            if(fixedCamera!=null)keyboard.Poll(inputFrame);
        }
        void FixedUpdate()
        {
            clock+=Time.fixedDeltaTime;
            motor.Simulate(keyboard.ReadAction(clock),Time.fixedDeltaTime,clock);
            if(motor.State!=previousState)
            {
                if(motor.State==ChaseMovementState.Leaping ||
                    motor.State==ChaseMovementState.DiveProne ||
                    motor.State==ChaseMovementState.Sliding ||
                    motor.State==ChaseMovementState.Jumping)
                    motionStart=motor.Body.position;
                previousState=motor.State;
            }
        }
        // A local fixed-angle follow camera makes animation and roof clearance
        // inspectable at human scale without changing the production camera.
        void LateUpdate()
        {
            if(fixedCamera==null||motor==null)return;
            Quaternion targetRotation=Quaternion.Euler(sideView?12f:55f,
                sideView?-45f:45f,0f);
            fixedCamera.transform.rotation=Quaternion.Slerp(fixedCamera.transform.rotation,
                targetRotation,1f-Mathf.Exp(-12f*Time.deltaTime));
            Vector3 target=motor.Body.position+Vector3.up*(sideView ? 0.18f : 0.40f)-
                fixedCamera.transform.forward*(sideView ? 5.8f : 6.8f);
            fixedCamera.transform.position=Vector3.Lerp(fixedCamera.transform.position,
                target,1f-Mathf.Exp(-8f*Time.deltaTime));
        }
        void OnGUI()
        {
            if(motor==null)return;
            GUI.Box(new Rect(12,12,350,264),"CHASE v0.5.1 / BALLISTIC DIVE TEST");
            GUI.Label(new Rect(24,39,340,25),"WASD MOVE   SPACE JUMP   UP SUPERMAN");
            GUI.Label(new Rect(24,61,340,25),"DOWN SLIDE  SHIFT CROUCH  R RESET  G SHELF  V VIEW");
            GUI.Label(new Rect(24,86,330,25),"STATE: "+motor.State);
            GUI.Label(new Rect(24,108,330,25),"SPEED: "+
                motor.HorizontalVelocity.magnitude.ToString("F2")+" m/s   VY: "+
                motor.Velocity.y.ToString("F2"));
            GUI.Label(new Rect(24,130,330,25),"GROUNDED: "+motor.Grounded+
                "    BODY: "+(motor.DiveColliderActive ? "PRONE BOX" : "UPRIGHT CAPSULE"));
            GUI.Label(new Rect(24,152,330,25),"RECOVERY: "+
                motor.RecoveryRemaining.ToString("F2")+" s");
            var d=motor.Body.position-motionStart;d.y=0f;
            GUI.Label(new Rect(24,174,330,25),"ACTION DISTANCE: "+d.magnitude.ToString("F2")+" m");
            if(driver!=null && driver.Animator!=null)
            {
                var info=driver.Animator.GetCurrentAnimatorStateInfo(0);
                GUI.Label(new Rect(24,197,330,25),"VISUAL: "+driver.CurrentVisualState+
                    "  CLIP LEAP: "+info.IsName("Leap"));
            }
            if(motor.DiveBlockedTimeLeft>0f)
                GUI.Label(new Rect(24,239,330,25),"DIVE BLOCKED: CLEAR YOUR BODY");
            if(headBone!=null && pelvisBone!=null)
            {
                Vector3 axis=(headBone.position-pelvisBone.position).normalized;
                GUI.Label(new Rect(24,218,330,25),"HEAD AXIS  UP: "+
                    Vector3.Dot(axis,Vector3.up).ToString("F2")+
                    "  FWD: "+Vector3.Dot(axis,motor.Body.rotation*Vector3.forward).ToString("F2"));
            }
        }
    }
}
