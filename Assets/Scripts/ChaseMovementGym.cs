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
        ChaseKeyboard keyboard;
        Camera fixedCamera;
        float clock;
        Vector3 motionStart;
        ChaseMovementState previousState;
        public ChaseMotor Motor => motor;
        void Awake()
        {
            Time.fixedDeltaTime=.02f;
            fixedCamera=Camera.main;
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
            visual.GetComponent<ChibiVisualDriver>().BindMotor(motor);
            ResetActor();
            keyboard.ResetSource();
        }
        void ResetActor()
        {
            clock=0f;
            motor.ResetMotor(spawn!=null?spawn.position:new Vector3(-9,.9f,-8f));
            motionStart=motor.Body.position;
            previousState=ChaseMovementState.Standing;
            if(fixedCamera!=null)
                fixedCamera.transform.position=motor.Body.position+Vector3.up*.40f-
                    fixedCamera.transform.forward*11.5f;
            Physics.SyncTransforms();
        }
        void Update()
        {
            Keyboard k=Keyboard.current;
            if(k!=null && k.rKey.wasPressedThisFrame)
            {
                ResetActor();
                keyboard.ResetSource();
                return;
            }
            if(fixedCamera!=null)keyboard.Poll(fixedCamera.transform);
        }
        void FixedUpdate()
        {
            clock+=Time.fixedDeltaTime;
            motor.Simulate(keyboard.ReadAction(clock),Time.fixedDeltaTime,clock);
            if(motor.State!=previousState)
            {
                if(motor.State==ChaseMovementState.Leaping ||
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
            Vector3 target=motor.Body.position+Vector3.up*.40f-
                fixedCamera.transform.forward*11.5f;
            fixedCamera.transform.position=Vector3.Lerp(fixedCamera.transform.position,
                target,1f-Mathf.Exp(-8f*Time.deltaTime));
        }
#if UNITY_EDITOR
        void OnGUI()
        {
            if(motor==null)return;
            GUI.Box(new Rect(12,12,350,212),"CHASE v0.4 / MOVEMENT GYM");
            GUI.Label(new Rect(24,39,340,25),"WASD MOVE   SPACE JUMP   UP LEAP");
            GUI.Label(new Rect(24,61,340,25),"DOWN SLIDE   SHIFT CROUCH   R RESET");
            GUI.Label(new Rect(24,86,330,25),"STATE: "+motor.State);
            GUI.Label(new Rect(24,108,330,25),"SPEED: "+
                motor.HorizontalVelocity.magnitude.ToString("F2")+" m/s   VY: "+
                motor.Velocity.y.ToString("F2"));
            GUI.Label(new Rect(24,130,330,25),"GROUNDED: "+motor.Grounded+
                "    CAP: "+motor.Capsule.height.ToString("F2")+" m");
            GUI.Label(new Rect(24,152,330,25),"RECOVERY: "+
                motor.RecoveryRemaining.ToString("F2")+" s");
            var d=motor.Body.position-motionStart;d.y=0f;
            GUI.Label(new Rect(24,174,330,25),"ACTION DISTANCE: "+d.magnitude.ToString("F2")+" m");
        }
#endif
    }
}
