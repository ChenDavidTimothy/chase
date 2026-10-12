using System;
using System.IO;
using UnityEngine;

namespace Chase
{
    /// <summary>
    /// Presentation-only replay of recorded real-motor action. No Rigidbody,
    /// bot planner, collider, gameplay timer, or simulation in menu playback.
    /// Capture source is the editor-only ChaseMenuReplayBaker.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class ChaseMenuShowcase : MonoBehaviour
    {
        const int Magic = 0x43485231; // CHR1
        const int Hz = 50;
        struct Pose
        {
            public Vector3 position;
            public float yaw;
            public byte action;
            public float phase;
        }
        sealed class Shot
        {
            public Pose[] evader, chaser;
            public int Length => evader.Length;
        }

        Shot[] shots;
        Transform[] visuals;
        Animator[] animators;
        float[,] clipLengths;
        ChaseMatch match;
        Camera arenaCamera;
        Transform root;
        ChaseMenuPage lastPage;
        bool initialized;
        float elapsed;
        int lastSample=-1;
        int currentShot=-1;
        float cutOpacity;
        public float CutOpacity => cutOpacity;
        public int CurrentShotIndex => currentShot;

        static readonly string[] States = {
            "Idle","Run","Jump","Leap","Slide","SlideRecovery","DiveProne",
            "LeapRecovery","Crouch","CrouchWalk","DiveCrawl","JumpLanding"
        };

        public bool Loaded => shots!=null && shots.Length==3;
        public bool Playing => initialized && lastPage!=ChaseMenuPage.Arena &&
            lastPage!=ChaseMenuPage.InMatch;

        public static string AnimationName(byte id)
        {
            return id<States.Length?States[id]:"Idle";
        }

        public void Initialize(Camera camera)
        {
            arenaCamera=camera;
            match=GetComponent<ChaseMatch>();
            var resource=Resources.Load<TextAsset>("MenuChaseReplay");
            if(resource==null)
                throw new InvalidOperationException("CHASE missing validated menu physics recording");
            using(var stream=new MemoryStream(resource.bytes,false))
            using(var reader=new BinaryReader(stream))
            {
                if(reader.ReadInt32()!=Magic || reader.ReadInt32()!=Hz ||
                   reader.ReadInt32()!=3)
                    throw new InvalidDataException("CHASE menu replay header invalid");
                shots=new Shot[3];
                for(int s=0;s<shots.Length;s++)
                {
                    int count=reader.ReadInt32();
                    if(count<100 || count>360)
                        throw new InvalidDataException("CHASE menu recording bad shot length");
                    var shot=new Shot {
                        evader=new Pose[count],chaser=new Pose[count]
                    };
                    for(int i=0;i<count;i++)
                    {
                        shot.evader[i]=Read(reader);
                        shot.chaser[i]=Read(reader);
                        if(shot.evader[i].action>=States.Length ||
                           shot.chaser[i].action>=States.Length)
                            throw new InvalidDataException("Invalid recorded animation");
                    }
                    shots[s]=shot;
                }
                if(stream.Position!=stream.Length)
                    throw new InvalidDataException("Trailing data in menu replay");
            }

            root=new GameObject("MENU SHOWCASE / visual-only recorded chase").transform;
            root.SetParent(transform,false);
            root.localPosition=Vector3.zero;
            visuals=new Transform[2];
            animators=new Animator[2];
            clipLengths=new float[2,States.Length];
            var prefab=Resources.Load<GameObject>("ChibiVisual");
            if(prefab==null)throw new InvalidOperationException("ChibiVisual missing");
            for(int i=0;i<2;i++)
            {
                var visual=Instantiate(prefab,root);
                visual.name=i==0?"SHOWCASE / Evader (render only)":
                    "SHOWCASE / Chaser (render only)";
                var driver=visual.GetComponent<ChibiVisualDriver>();
                if(driver!=null)driver.enabled=false;
                if(visual.GetComponentInChildren<Collider>(true)!=null ||
                   visual.GetComponentInChildren<Rigidbody>(true)!=null)
                    throw new InvalidOperationException(
                        "Menu animation must have no physical objects");
                var animator=visual.GetComponentInChildren<Animator>();
                if(animator==null || animator.runtimeAnimatorController==null)
                    throw new InvalidOperationException("Missing approved Chibi animations");
                animator.applyRootMotion=false;
                animator.speed=0f;
                visuals[i]=visual.transform;
                animators[i]=animator;
                foreach(var clip in animator.runtimeAnimatorController.animationClips)
                    for(int j=0;j<States.Length;j++)
                        if(clip.name.EndsWith("CHASE_"+States[j],
                           StringComparison.OrdinalIgnoreCase))
                            clipLengths[i,j]=clip.length;
                for(int j=0;j<States.Length;j++)
                    if(clipLengths[i,j]<=0f && States[j]!="DiveProne")
                        throw new InvalidOperationException(
                            "Replay action missing official Blender clip "+States[j]);
                var renderers=visual.GetComponentsInChildren<Renderer>(true);
                // Match the real competitors' role tints, without touching
                // the single approved Blender source or authored prefab.
                var tint=i==0?new Color(.16f,.55f,1f):
                    new Color(.92f,.22f,.25f);
                foreach(var r in renderers)
                {
                    var color=r.name.StartsWith("face")?
                        new Color(.075f,.09f,.11f):
                        r.name.StartsWith("head")?
                            new Color(.80f,.83f,.86f):tint;
                    var block=new MaterialPropertyBlock();
                    r.GetPropertyBlock(block);
                    block.SetColor("_Color",color);
                    block.SetColor("_BaseColor",color);
                    r.SetPropertyBlock(block);
                }
            }
            initialized=true;
            SetPage(ChaseMenuPage.MainMenu);
        }

        static Pose Read(BinaryReader reader) => new Pose {
            position=new Vector3(reader.ReadSingle(),reader.ReadSingle(),
                reader.ReadSingle()),
            yaw=reader.ReadSingle(),action=reader.ReadByte(),
            phase=reader.ReadSingle()
        };

        public void SetPage(ChaseMenuPage page)
        {
            if(!initialized)return;
            bool cinematic=page!=ChaseMenuPage.Arena &&
                page!=ChaseMenuPage.InMatch;
            if(lastPage==page && root.gameObject.activeSelf==cinematic)return;
            lastPage=page;
            root.gameObject.SetActive(cinematic);
            if(cinematic && currentShot<0)
            {
                elapsed=0f;
                currentShot=0;
                lastSample=-1;
                Apply(0,0f);
            }
            if(page==ChaseMenuPage.Arena || page==ChaseMenuPage.InMatch)
                arenaCamera.fieldOfView=40f;
            if(page==ChaseMenuPage.Arena)
            {
                // The card stays to the left; the arena is first shown
                // clearly on the right as the actual arena choice.
                arenaCamera.transform.rotation=Quaternion.Euler(55f,45f,0f);
                var previewCenter=new Vector3(-.6f,1.15f,0f);
                arenaCamera.transform.position=previewCenter-
                    arenaCamera.transform.forward*18.4f-
                    arenaCamera.transform.right*3.3f;
            }
        }

        void LateUpdate()
        {
            if(!initialized || !root.gameObject.activeSelf ||
               (match!=null && !match.ApplicationFocused))return;
            elapsed+=Time.unscaledDeltaTime;
            int shotIndex=0;
            float remaining=elapsed;
            // Exact repeat across three recorded clips, with no per-frame
            // allocations and no background physics.
            float total=0f;
            foreach(var shot in shots)total+=(shot.Length-1)/(float)Hz;
            if(total<=0f)return;
            remaining%=total;
            for(int s=0;s<shots.Length;s++)
            {
                float duration=(shots[s].Length-1)/(float)Hz;
                if(remaining<duration || s==shots.Length-1)
                {shotIndex=s;break;}
                remaining-=duration;
            }
            int sample=Mathf.Clamp(Mathf.FloorToInt(remaining*Hz),0,
                shots[shotIndex].Length-2);
            if(sample==lastSample && shotIndex==currentShot)return;
            currentShot=shotIndex;
            lastSample=sample;
            Apply(sample,remaining*Hz-sample);
            float shotDuration=(shots[currentShot].Length-1)/(float)Hz;
            // Only the footage fades at a cut. Text and buttons stay static.
            cutOpacity=1f-Mathf.Clamp01(Mathf.Min(
                remaining,shotDuration-remaining)/.22f);
        }

        void Apply(int i,float subframe)
        {
            var shot=shots[currentShot];
            Draw(0,shot.evader,i,subframe);
            Draw(1,shot.chaser,i,subframe);
            // The camera presents *actual recorded motor coordinates*
            // at either side of the opaque centered menu. Camera-only
            // framing never changes their physics or authored route.
            arenaCamera.fieldOfView=38f;
            arenaCamera.transform.rotation=Quaternion.Euler(49f,37f,0f);
            Vector3 midpoint=(visuals[0].position+visuals[1].position)*.5f+
                Vector3.up*1.10f;
            // Fit both athletes into the side of the frame even as the chase
            // stretches out. The centered menu remains unobstructed, and
            // the real recorded action is never repositioned or reanimated.
            float widthPerMetre=2f*Mathf.Tan(19f*Mathf.Deg2Rad)*
                Mathf.Max(.8f,arenaCamera.aspect);
            float separation=Mathf.Abs(Vector3.Dot(
                visuals[0].position-visuals[1].position,
                arenaCamera.transform.right));
            float distance=Mathf.Clamp(
                separation/(widthPerMetre*.17f),14.6f,25f);
            float horizontalView=distance*widthPerMetre;
            float sideTarget=currentShot==1?.88f:.12f;
            Vector3 center=midpoint+arenaCamera.transform.right*
                ((.5f-sideTarget)*horizontalView);
            arenaCamera.transform.position=center-
                arenaCamera.transform.forward*distance;
        }

        void Draw(int actor,Pose[] poses,int i,float fraction)
        {
            var a=poses[i];var b=poses[Mathf.Min(i+1,poses.Length-1)];
            var t=visuals[actor];
            t.position=Vector3.Lerp(a.position,b.position,fraction)+Vector3.down*.9f;
            t.rotation=Quaternion.Euler(0f,
                Mathf.LerpAngle(a.yaw,b.yaw,fraction),0f);
            var anim=animators[actor];
            string state=AnimationName(a.action);
            float duration=clipLengths[actor,a.action];
            // Baker stores seconds, Animator.Play expects normalized time.
            // Wrap looped actions, clamp one-shot recoveries to their end.
            float normalized=duration>0f?a.phase/duration:0f;
            if(a.action==5 || a.action==7 || a.action==11)
                normalized=Mathf.Clamp01(normalized);
            else normalized=Mathf.Repeat(normalized,1f);
            anim.Play(state,0,normalized);
            anim.Update(0f);
        }

        public int ShotCount => shots?.Length ?? 0;
        public int FrameCount(int shot) =>
            shots!=null && shot>=0 && shot<shots.Length?shots[shot].Length:0;

        void OnDestroy()
        {
            if(root!=null)Destroy(root.gameObject);
        }
    }
}
