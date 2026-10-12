using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Chase
{
    // Public practice facility. It uses the production motor and action
    // source. The calibration Gym remains independent, with its instrumentation.
    [DefaultExecutionOrder(-100)]
    public sealed class ChasePracticePlayground : MonoBehaviour
    {
        [SerializeField] Transform start;
        [SerializeField] Camera practiceCamera;
        [SerializeField] ChaseTuning tuning = new ChaseTuning();

        static readonly Vector3[] Stations = {
            new Vector3(0f,.9f,-8.5f),
            new Vector3(-5f,.9f,-4.8f),
            new Vector3(5f,.9f,-4.8f),
            new Vector3(0f,.9f,1.7f)
        };
        static readonly string[] Names = {
            "OPEN FLOOR", "OVER OR UNDER?", "BENCH ROUTES", "LINK YOUR MOVES"
        };

        ChaseMotor motor;
        ChaseKeyboard keyboard;
        Transform visual;
        float clock;
        int station;
        bool paused, hasFocus = true;
        int pauseChoice;
        Vector3 cameraVelocity;
        Quaternion viewRotation;
        bool showControls;
        public ChaseMotor Motor => motor;
        public bool Paused => paused;
        public int StationIndex => station;
        public string StationName => Names[station];
        public int StationCount => Stations.Length;
        public Vector3 CurrentStation => Stations[station];

        void Awake()
        {
            Time.timeScale=1f;
            Time.fixedDeltaTime=.02f;
            if(practiceCamera==null)practiceCamera=Camera.main;
            if(practiceCamera==null)throw new System.InvalidOperationException("Practice camera missing");
            keyboard=gameObject.AddComponent<ChaseKeyboard>();
            var body=new GameObject("PRACTICE / real Chibi motor");
            body.transform.SetParent(transform,true);
            body.transform.position=start!=null?start.position:Stations[0];
            body.AddComponent<Rigidbody>();
            body.AddComponent<CapsuleCollider>();
            var asset=Resources.Load<GameObject>("ChibiVisual");
            if(asset==null)throw new System.InvalidOperationException("Approved ChibiVisual missing");
            var instance=Instantiate(asset,body.transform,false);
            instance.name="PRACTICE / approved Blender visual";
            instance.transform.localPosition=Vector3.down*(tuning.standingHeight*.5f);
            visual=instance.transform;
            // Practice retains the visible evader's identity.
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var tint=renderer.name.StartsWith("face")?
                    new Color(.075f,.09f,.11f):
                    renderer.name.StartsWith("head")?
                        new Color(.80f,.83f,.86f):new Color(.16f,.55f,1f);
                var block=new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color",tint);
                block.SetColor("_BaseColor",tint);
                renderer.SetPropertyBlock(block);
            }
            motor=body.AddComponent<ChaseMotor>();
            motor.Initialize(tuning,instance.transform);
            var driver=instance.GetComponent<ChibiVisualDriver>();
            if(driver!=null)driver.BindMotor(motor);
            showControls=PlayerPrefs.GetInt("CHASE_Hints",1)!=0;
            viewRotation=Quaternion.Euler(53f,45f,0f);
            practiceCamera.fieldOfView=53f;
            practiceCamera.transform.rotation=viewRotation;
            ResetStation();
        }

        public void ResetStation()
        {
            Time.timeScale=1f;
            paused=false;
            clock=0f;
            keyboard.ResetSource();
            motor.ResetMotor(Stations[station]);
            cameraVelocity=Vector3.zero;
            SnapCamera();
            Physics.SyncTransforms();
        }

        public void SelectStation(int index)
        {
            if(index<0||index>=Stations.Length) return;
            station=index;
            ResetStation();
        }

        public void NextStation() => SelectStation((station+1)%Stations.Length);

        public void TogglePause()
        {
            paused=!paused;
            pauseChoice=0;
            Time.timeScale=paused?0f:1f;
            keyboard.Clear();
        }

        public void Exit()
        {
            Time.timeScale=1f;
            keyboard.Clear();
            SceneManager.LoadScene("SampleScene",LoadSceneMode.Single);
        }

        void OnApplicationFocus(bool focused)
        {
            hasFocus=focused;
            if(!focused && !paused && motor!=null)TogglePause();
            if(keyboard!=null)keyboard.Clear();
        }
        void OnApplicationPause(bool suspended)
        {
            hasFocus=!suspended;
            if(suspended && !paused && motor!=null)TogglePause();
            if(keyboard!=null)keyboard.Clear();
        }

        void Update()
        {
            Keyboard kb=Keyboard.current;
            if(kb==null)return;
            if(kb.escapeKey.wasPressedThisFrame)
            {TogglePause();return;}
            if(paused||!hasFocus)
            {
                keyboard.Clear();
                if(paused)
                {
                    if(kb.downArrowKey.wasPressedThisFrame ||
                       kb.tabKey.wasPressedThisFrame)
                        pauseChoice=(pauseChoice+1)%3;
                    if(kb.upArrowKey.wasPressedThisFrame)
                        pauseChoice=(pauseChoice+2)%3;
                    if(kb.enterKey.wasPressedThisFrame ||
                       kb.spaceKey.wasPressedThisFrame)
                    {
                        if(pauseChoice==0)TogglePause();
                        else if(pauseChoice==1)ResetStation();
                        else Exit();
                    }
                }
                return;
            }
            if(kb.rKey.wasPressedThisFrame){ResetStation();return;}
            if(kb.tabKey.wasPressedThisFrame){NextStation();return;}
            keyboard.Poll(practiceCamera.transform);
        }

        void FixedUpdate()
        {
            if(paused || !hasFocus)return;
            clock+=Time.fixedDeltaTime;
            motor.Simulate(keyboard.ReadAction(clock),Time.fixedDeltaTime,clock);
            // Ordinary recovery if the player falls off the practice floor.
            if(motor.Body.position.y<-3f)ResetStation();
        }

        Vector3 DesiredCamera() =>
            motor.Body.position+Vector3.up*.30f-
            practiceCamera.transform.forward*9.7f;

        void SnapCamera()
        {
            practiceCamera.transform.rotation=viewRotation;
            practiceCamera.transform.position=DesiredCamera();
        }
        void LateUpdate()
        {
            if(practiceCamera==null||motor==null)return;
            practiceCamera.transform.rotation=viewRotation;
            practiceCamera.transform.position=Vector3.SmoothDamp(
                practiceCamera.transform.position,DesiredCamera(),
                ref cameraVelocity,.17f);
        }

        // A small broadcast-style key/controls HUD, separate from developer
        // measurement telemetry. No score, no timer, no compulsory objectives.
        static readonly Dictionary<(int,TextAnchor,Color,bool),GUIStyle> Styles=
            new Dictionary<(int,TextAnchor,Color,bool),GUIStyle>();
        static GUIStyle Style(int size,TextAnchor align,Color color,bool strong=true)
        {
            var key=(size,align,color,strong);
            if(Styles.TryGetValue(key,out var cached))return cached;
            var style=new GUIStyle(GUI.skin.label);
            style.fontSize=size;
            style.alignment=align;
            style.fontStyle=strong?FontStyle.Bold:FontStyle.Normal;
            style.normal.textColor=color;
            Styles.Add(key,style);
            return style;
        }
        static readonly Color Cream=new Color(.96f,.95f,.87f);
        static readonly Color Slate=new Color(.035f,.10f,.15f,.94f);
        static readonly Color Mint=new Color(.26f,.82f,.68f);
        void Panel(Rect r,Color color)
        {
            Color old=GUI.color;
            GUI.color=color;
            GUI.DrawTexture(r,Texture2D.whiteTexture);
            GUI.color=old;
        }
        bool Choice(Rect r,string text,System.Action action)
        {
            Panel(r,new Color(.16f,.32f,.41f,.98f));
            if(GUI.Button(r,text,Style(17,TextAnchor.MiddleCenter,Cream))) {
                action();return true;
            }
            return false;
        }

        void Keycap(Rect r,string glyph,bool active)
        {
            Panel(r,active?Mint:new Color(.16f,.32f,.40f,.98f));
            var text=active?new Color(.035f,.11f,.16f):Cream;
            GUI.Label(r,glyph,Style(15,TextAnchor.MiddleCenter,text));
        }
        void KeyLabel(float x,float y,string glyph,string caption,
            float width,bool active)
        {
            Keycap(new Rect(x,y,width,31),glyph,active);
            GUI.Label(new Rect(x-6,y+34,width+12,18),caption,
                Style(11,TextAnchor.MiddleCenter,
                    new Color(.67f,.81f,.83f)));
        }

        void OnGUI()
        {
            if(motor==null)return;
            float factor=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            Matrix4x4 previous=GUI.matrix;
            GUI.matrix=Matrix4x4.Scale(new Vector3(factor,factor,1));
            float w=Screen.width/factor,h=Screen.height/factor;
            Panel(new Rect(20,18,510,80),Slate);
            Panel(new Rect(20,18,6,80),Mint);
            GUI.Label(new Rect(38,23,450,31),"PRACTICE  /  "+StationName,
                Style(22,TextAnchor.MiddleLeft,Cream));
            GUI.Label(new Rect(39,61,480,25),
                "Explore freely. Every route is your choice.",
                Style(14,TextAnchor.MiddleLeft,new Color(.71f,.85f,.86f),false));

            Choice(new Rect(w-182,20,158,47),"NEXT  [TAB]",NextStation);
            Choice(new Rect(w-182,77,158,47),"RETRY  [R]",ResetStation);

            if(showControls)
            {
                float y=h-140f;
                Panel(new Rect(20,y,494,118),Slate);
                var kb=Keyboard.current;
                bool pressed=kb!=null;
                GUI.Label(new Rect(31,y+5,105,18),"MOVE",
                    Style(11,TextAnchor.MiddleLeft,Mint));
                Keycap(new Rect(73,y+21,32,31),"W",pressed&&kb.wKey.isPressed);
                Keycap(new Rect(38,y+55,32,31),"A",pressed&&kb.aKey.isPressed);
                Keycap(new Rect(73,y+55,32,31),"S",pressed&&kb.sKey.isPressed);
                Keycap(new Rect(108,y+55,32,31),"D",pressed&&kb.dKey.isPressed);
                KeyLabel(161,y+32,"SPACE","JUMP",70,
                    pressed&&kb.spaceKey.isPressed);
                KeyLabel(240,y+32,"UP","DIVE",42,
                    pressed&&kb.upArrowKey.isPressed);
                KeyLabel(290,y+32,"DN","SLIDE",42,
                    pressed&&kb.downArrowKey.isPressed);
                KeyLabel(340,y+32,"SHIFT","CROUCH",65,
                    pressed&&kb.leftShiftKey.isPressed);
                GUI.Label(new Rect(33,y+94,454,19),
                    "ESC  PAUSE                 R  RETRY                 TAB  NEXT",
                    Style(12,TextAnchor.MiddleLeft,new Color(.67f,.79f,.82f)));
            }
            if(paused)
            {
                Panel(new Rect(0,0,w,h),new Color(.01f,.03f,.05f,.62f));
                float x=(w-460)*.5f,y=(h-300)*.5f;
                Panel(new Rect(x,y,460,300),Slate);
                GUI.Label(new Rect(x+30,y+23,400,60),"PRACTICE PAUSED",
                    Style(33,TextAnchor.MiddleCenter,Cream));
                if(pauseChoice==0)Panel(new Rect(x+53,y+102,5,52),Mint);
                Choice(new Rect(x+58,y+102,344,52),"RESUME",TogglePause);
                if(pauseChoice==1)Panel(new Rect(x+53,y+166,5,52),Mint);
                Choice(new Rect(x+58,y+166,344,52),"RETRY",ResetStation);
                if(pauseChoice==2)Panel(new Rect(x+53,y+230,5,52),Mint);
                Choice(new Rect(x+58,y+230,344,52),"MAIN MENU",Exit);
            }
            GUI.matrix=previous;
        }
    }
}
