using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Chase;

/// <summary>
/// Bakes a short three-shot showcase using the real ChaseMotor + authored
/// production PhysX course. Rebuildable, audited source. Runtime never
/// executes these scripted actions or physics during menu playback.
/// </summary>
public static class ChaseMenuReplayBaker
{
    const string Output="Assets/Resources/MenuChaseReplay.bytes";
    const int Hz=50;
    const float Dt=1f/Hz;
    const int Magic=0x43485231; // CHR1
    const int Frames=170;
    const int Shots=3;

    struct Athlete
    {
        public GameObject root;
        public ChaseMotor motor;
        public float lastYaw;
        public byte lastPose;
        public float poseTime;
    }
    static Athlete Spawn(Vector3 position)
    {
        var root=new GameObject("RECORDING / genuine physics actor");
        root.transform.position=position;
        root.AddComponent<Rigidbody>();
        root.AddComponent<CapsuleCollider>();
        var dummy=new GameObject("animation pivot only").transform;
        dummy.SetParent(root.transform,false);
        var motor=root.AddComponent<ChaseMotor>();
        motor.Initialize(new ChaseTuning(),dummy);
        Physics.SyncTransforms();
        return new Athlete { root=root,motor=motor };
    }
    static byte Pose(ChaseMotor m)
    {
        switch(m.State)
        {
            case ChaseMovementState.Leaping:return 3;
            case ChaseMovementState.Sliding:return m.SlideCompactContact?(byte)8:(byte)4;
            case ChaseMovementState.SlideRecovery:
                return m.SlideGettingUp?(byte)5:
                    m.SlideColliderActive?(byte)4:(byte)8;
            case ChaseMovementState.DiveProne:
                return m.ProneCrawling?(byte)10:(byte)6;
            case ChaseMovementState.LeapRecovery:return 7;
            case ChaseMovementState.Jumping:
            case ChaseMovementState.Airborne:return 2;
            case ChaseMovementState.Crouching:return 8;
            default:return m.HorizontalVelocity.sqrMagnitude>.55f?(byte)1:(byte)0;
        }
    }
    static void Write(BinaryWriter w,ref Athlete athlete)
    {
        var m=athlete.motor;
        byte pose=Pose(m);
        float dt=Dt;
        if(athlete.lastPose!=pose)athlete.poseTime=pose==3?.22f:0f;
        else athlete.poseTime+=dt;
        athlete.lastPose=pose;
        Vector3 facing=m.FacingDirection;
        float yaw=Mathf.Atan2(facing.x,facing.z)*Mathf.Rad2Deg;
        athlete.lastYaw=yaw;
        Vector3 p=m.Body.position;
        w.Write(p.x);w.Write(p.y);w.Write(p.z);
        w.Write(yaw);w.Write(pose);
        // Real authored animation is driven from this recorded motor state.
        w.Write(athlete.poseTime);
    }

    [MenuItem("Chase/Bake Real-Physics Menu Showcase")]
    public static void Bake()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Bake in Edit Mode only");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open the production Arena 01 first");
        ChaseArenaV1Contract.Validate();
        SimulationMode prior=Physics.simulationMode;
        var root1=default(GameObject);
        var root2=default(GameObject);
        var previousGravity=Physics.gravity;
        float clock=31f;

        try
        {
            Physics.simulationMode=SimulationMode.Script;
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream))
            {
                writer.Write(Magic);writer.Write(Hz);writer.Write(Shots);
                for(int sceneIndex=0;sceneIndex<Shots;sceneIndex++)
                {
                    // The jump cuts occur only between shots, analogous to
                    // a video editor switching between authentic recordings.
                    Vector3 evader, chaser;
                    switch(sceneIndex)
                    {
                        case 0: // Central Superman crossing.
                            evader=new Vector3(0,.9f,-2.7f);
                            chaser=new Vector3(3.1f,.9f,-2.4f);
                            break;
                        case 1: // West bench feet-first slide.
                            evader=new Vector3(-3.55f,.9f,-.60f);
                            chaser=new Vector3(.2f,.9f,-1.0f);
                            break;
                        default: // Near-miss routes around the switching frame.
                            evader=new Vector3(-3.6f,.9f,-3.55f);
                            chaser=new Vector3(-4.95f,.9f,-3.30f);
                            break;
                    }
                    var e=Spawn(evader);
                    var c=Spawn(chaser);
                    root1=e.root;root2=c.root;
                    if(sceneIndex==1)
                        e.motor.Body.linearVelocity=Vector3.forward*6f;
                    writer.Write(Frames);
                    int leapTicks=0,slideTicks=0,runTicks=0,contacts=0;
                    float minimumSeparation=float.PositiveInfinity;
                    int minimumFrame=-1;
                    for(int i=0;i<Frames;i++)
                    {
                        bool evaderDive=sceneIndex==0&&i==0;
                        bool evaderSlide=sceneIndex==1&&i==0;
                        Vector2 eMove,cMove;
                        if(sceneIndex==0)
                        {
                            eMove=i<140?Vector2.up:
                                new Vector2(-.5f,.8f).normalized;
                            cMove=i<80?new Vector2(-.50f,.95f).normalized:
                                new Vector2(.95f,-.40f).normalized;
                        }
                        else if(sceneIndex==1)
                        {
                            eMove=i<110?Vector2.up:
                                new Vector2(.75f,.65f).normalized;
                            cMove=i<50?new Vector2(-.55f,.85f).normalized:
                                new Vector2(.82f,.45f).normalized;
                        }
                        else
                        {
                            eMove=i<70?new Vector2(.8f,.45f).normalized:
                                i<144?new Vector2(.55f,-.75f).normalized:
                                new Vector2(-.85f,-.35f).normalized;
                            cMove=i<70?new Vector2(.8f,.45f).normalized:
                                i<144?new Vector2(.55f,-.75f).normalized:
                                new Vector2(-.85f,-.35f).normalized;
                        }
                        e.motor.Simulate(new ChaseAction(eMove,
                            false,evaderDive,evaderSlide,false),Dt,clock);
                        c.motor.Simulate(new ChaseAction(
                            sceneIndex==2?cMove*.70f:cMove,
                            false,false,false,false),Dt,clock);
                        Physics.Simulate(Dt);
                        clock+=Dt;
                        if(e.motor.State==ChaseMovementState.Leaping)leapTicks++;
                        if(e.motor.State==ChaseMovementState.Sliding)slideTicks++;
                        if(c.motor.HorizontalVelocity.magnitude>2)runTicks++;
                        if(e.motor.DiveCompactContact)contacts++;
                        float separation=Vector3.Distance(e.motor.Body.position,
                            c.motor.Body.position);
                        if(separation<minimumSeparation)
                        {minimumSeparation=separation;minimumFrame=i;}
                        Write(writer,ref e);
                        Write(writer,ref c);
                    }
                    if(sceneIndex==0&&leapTicks<20)
                        throw new InvalidOperationException("Showcase recorded no authentic dive");
                    if(sceneIndex==1&&slideTicks<12)
                        throw new InvalidOperationException("Showcase recorded no authentic slide");
                    if(runTicks<25)
                        throw new InvalidOperationException("Showcase lacks chasing motion");
                    if(minimumSeparation<.76f)
                        throw new InvalidOperationException(
                            "Recorded chase would likely tag rather than narrowly evade "+
                            "shot="+sceneIndex+" frame="+minimumFrame+
                            " minimum="+minimumSeparation.ToString("F3"));
                    Debug.Log("CHASE_MENU_RECORDED shot="+sceneIndex+
                        " real_leap_ticks="+leapTicks+
                        " real_slide_ticks="+slideTicks+
                        " chaser_run_ticks="+runTicks+" contact_ticks="+contacts+
                        " minimum_athlete_separation="+minimumSeparation.ToString("F3")+
                        " evader_finish="+e.motor.Body.position);
                    UnityEngine.Object.DestroyImmediate(root1);
                    UnityEngine.Object.DestroyImmediate(root2);
                    root1=root2=null;
                    Physics.SyncTransforms();
                }
                writer.Flush();
                File.WriteAllBytes(Output,stream.ToArray());
            }
        }
        finally
        {
            if(root1!=null)UnityEngine.Object.DestroyImmediate(root1);
            if(root2!=null)UnityEngine.Object.DestroyImmediate(root2);
            Physics.gravity=previousGravity;
            Physics.simulationMode=prior;
            Physics.SyncTransforms();
        }
        AssetDatabase.ImportAsset(Output,ImportAssetOptions.ForceUpdate);
        Debug.Log("CHASE_MENU_PHYSICS_REPLAY_BAKED: "+Output);
    }
}
