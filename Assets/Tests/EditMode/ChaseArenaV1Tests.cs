using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chase.Tests
{
    // Test the actual prefab assets, not copies of their intended dimensions.
    // Static visuals never decide physics or provide additional solid edges.
    public sealed class ChaseArenaV1Tests
    {
        readonly List<GameObject> spawned=new List<GameObject>();
        SimulationMode prior;
        const float Dt=.02f;
        const float X=40f;
        float clock;

        [SetUp] public void Setup()
        {
            prior=Physics.simulationMode;
            Physics.simulationMode=SimulationMode.Script;
            clock=41f;
        }
        [TearDown] public void Teardown()
        {
            foreach(var o in spawned)
                if(o!=null)UnityEngine.Object.DestroyImmediate(o);
            spawned.Clear();
            Physics.simulationMode=prior;
            Physics.SyncTransforms();
        }
        GameObject Prefab(string name)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/ArenaV1/"+name+".prefab");
            Assert.NotNull(asset,"Production prefab missing: "+name);
            var g=UnityEngine.Object.Instantiate(asset);
            g.transform.position=new Vector3(X,0,0);
            spawned.Add(g);
            Physics.SyncTransforms();
            return g;
        }
        void Floor()
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name="isolated test floor";
            obj.transform.position=new Vector3(X,-.1f,0);
            obj.transform.localScale=new Vector3(12,.2f,20);
            spawned.Add(obj);
            Physics.SyncTransforms();
        }
        ChaseMotor Actor(float z)
        {
            var g=new GameObject("production prefab route athlete");
            spawned.Add(g);
            g.transform.position=new Vector3(X,.9f,z);
            g.AddComponent<Rigidbody>();g.AddComponent<CapsuleCollider>();
            var visual=new GameObject("nonphysical test visual");
            visual.transform.SetParent(g.transform,false);
            var m=g.AddComponent<ChaseMotor>();
            m.Initialize(new ChaseTuning(),visual.transform);
            Physics.SyncTransforms();
            return m;
        }
        void Step(ChaseMotor m,int dir=0,bool leap=false,bool slide=false,
            bool jump=false,bool crouch=false)
        {
            m.Simulate(new ChaseAction(new Vector2(0,dir),jump,leap,slide,crouch),
                Dt,clock);
            Physics.Simulate(Dt);
            clock+=Dt;
        }
        void Discard(ChaseMotor actor)
        {
            spawned.Remove(actor.gameObject);
            UnityEngine.Object.DestroyImmediate(actor.gameObject);
            Physics.SyncTransforms();
        }
        [Test] public void SavedPrefabContractsAndSpawnSafety()
        {
            const string path="Assets/Scenes/SampleScene.unity";
            var loaded=SceneManager.GetSceneByPath(path);
            bool wasOpen=loaded.IsValid()&&loaded.isLoaded;
            var scene=wasOpen?loaded:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var arena=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("ARENA  /"));
                var group=arena.GetComponentsInChildren<Transform>(true)
                    .Single(t=>t.name=="CHASE ARENA V1 / modular course");
                Assert.AreEqual(9,group.childCount);
                foreach(Transform t in group)
                {
                    Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(t.gameObject));
                    Assert.AreEqual(Vector3.one,t.localScale);
                }
                Assert.IsNull(arena.transform.Cast<Transform>()
                    .FirstOrDefault(t=>t.name=="Tall barrier A"));
            }
            finally {if(!wasOpen)EditorSceneManager.CloseScene(scene,true);}
        }
        [TestCase("UnderOverRail")]
        [TestCase("UnderOverBench")]
        public void BothDirectionsAreGenuineSolidOverAndUnderRoutes(string name)
        {
            Floor();
            var root=Prefab(name);
            var beam=root.GetComponentsInChildren<BoxCollider>(true)
                .Single(c=>c.name=="Crossing beam / stand-blocking solid");
            float top=beam.bounds.max.y, underside=beam.bounds.min.y;
            Assert.IsFalse(beam.isTrigger);
            for(int sign=-1;sign<=1;sign+=2)
            {
                var leap=Actor(-sign*2.7f);
                bool crossing=false;
                float clearance=float.PositiveInfinity;
                float maxProgress=0;
                for(int i=0;i<125;i++)
                {
                    Step(leap,sign,leap:i==0);
                    float p=sign*leap.Body.position.z;
                    maxProgress=Mathf.Max(maxProgress,p);
                    float halfLength=leap.Settings.diveCapsuleLength*.5f+
                        Mathf.Abs(leap.Settings.diveCapsuleCenterForward);
                    if(leap.DiveColliderActive &&
                        p+halfLength>-(beam.bounds.extents.z) &&
                        p-halfLength<beam.bounds.extents.z)
                    {
                        crossing=true;
                        float low=leap.Body.position.y+
                            leap.Capsule.center.y-leap.Capsule.radius;
                        clearance=Mathf.Min(clearance,low-top);
                        Assert.Greater(low,top+.003f,
                            "Long solid Superman body must stay ABOVE the beam");
                        Assert.Greater(sign*leap.Body.linearVelocity.z,7.95f,
                            "No PhysX collision boost or movement mode cheating");
                    }
                }
                Assert.IsTrue(crossing);
                Assert.Greater(maxProgress,1.5f,name+" high route from sign "+sign);
                Assert.Greater(clearance,.003f);
                Discard(leap);

                var slide=Actor(-sign*1.15f);
                slide.Body.linearVelocity=Vector3.forward*(sign*6f);
                bool under=false,passed=false,standing=false;
                for(int i=0;i<220;i++)
                {
                    Step(slide,sign,slide:i==0);
                    float p=sign*slide.Body.position.z;
                    if(p>-.3f&&p<.5f)
                    {
                        under=true;
                        Assert.IsTrue(slide.SlideColliderActive,
                            "Only real slide/crawl collider can pass below");
                        float high=slide.Body.position.y+slide.SlideBox.center.y+
                            slide.SlideBox.size.y*.5f;
                        Assert.Less(high,underside+.003f);
                    }
                    if(p>1.5f)passed=true;
                    if(p>1.5f && slide.State==ChaseMovementState.Standing)
                        standing=true;
                }
                Assert.IsTrue(under&&passed&&standing,
                    name+" failed reversible physical under-route "+sign);
                Discard(slide);
            }
        }
        [Test] public void LowCanopyCrouchesAndExitsInBothDirections()
        {
            Floor();Prefab("LowCanopy");
            foreach(int sign in new[]{-1,1})
            {
                var actor=Actor(-sign*1.4f);
                bool reached=false,stood=false;
                for(int i=0;i<155;i++)
                {
                    Step(actor,sign,crouch:i<115);
                    if(actor.Body.position.z*sign>.88f)reached=true;
                    if(reached&&actor.State==ChaseMovementState.Standing)
                        stood=true;
                }
                Assert.IsTrue(reached&&stood,"Canopy route "+sign);
                Discard(actor);
            }
        }
        [Test] public void AerialSlotHasPhysicallyValidDivePassageBothWays()
        {
            Floor();Prefab("AerialSlot");
            foreach(int sign in new[]{-1,1})
            {
                bool success=false;
                foreach(float distance in new[]{3.5f,3f,2.5f,2f})
                {
                    var m=Actor(-sign*distance);
                    float max=0;
                    for(int i=0;i<130;i++)
                    {
                        Step(m,sign,leap:i==0);
                        max=Mathf.Max(max,sign*m.Body.position.z);
                    }
                    success|=max>1.6f;
                    Discard(m);
                }
                Assert.IsTrue(success,"Original slot family must be traversable "+sign);
            }
        }
        [Test] public void LowHurdleIsARealJumpNotAnInvisibleTrigger()
        {
            Floor();Prefab("LowHurdle");
            var m=Actor(-1.7f);
            m.Body.linearVelocity=Vector3.forward*6f;
            float most=0;
            for(int i=0;i<110;i++)
            {
                Step(m,1,jump:i==0);
                most=Mathf.Max(most,m.Body.position.z);
            }
            Assert.Greater(most,1.5f,"Ordinary physical jump must clear hurdle");
        }
        [Test] public void TurnFrameBlocksStraightRunning()
        {
            Floor();Prefab("TurnFrame");
            var m=Actor(-1.7f);
            float max=-1.7f;
            for(int i=0;i<120;i++)
            {
                Step(m,1);
                max=Mathf.Max(max,m.Body.position.z);
            }
            Assert.Less(max,-.28f,"Turn frame rails must be physical blockers");
        }
        [Test] public void InclineIsWalkableWithoutSpecialClimbingPhysics()
        {
            Floor();Prefab("InclineRun");
            var m=Actor(-2.3f);
            bool supportedWhileClimbing=false;
            float maxRampHeight=.9f;
            for(int i=0;i<125;i++)
            {
                Step(m,1);
                if(m.Body.position.z>0&&m.Body.position.z<1)
                {
                    maxRampHeight=Mathf.Max(maxRampHeight,m.Body.position.y);
                    supportedWhileClimbing|=m.Grounded;
                }
            }
            Assert.Greater(maxRampHeight,1.35f,
                "Current shared motor must actually ascend the solid ramp");
            Assert.IsTrue(supportedWhileClimbing,
                "Ramp support must count as floor contact, not flying");
            Assert.Greater(m.Body.position.z,3f);
        }

        [Test] public void ProductionPlacementsKeepBothRealSupermanRoutesOpen()
        {
            const string path="Assets/Scenes/SampleScene.unity";
            var existing=SceneManager.GetSceneByPath(path);
            bool open=existing.IsValid()&&existing.isLoaded;
            var scene=open?existing:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                // These tests include every other obstacle in the saved
                // production scene. Isolated prefab clearance is insufficient.
                var routes=new[]{
                    new {x=0f,z=0f,sign=1,dist=2.7f,top=.94f},
                    new {x=0f,z=0f,sign=-1,dist=2.7f,top=.94f},
                    new {x=-3.55f,z=.55f,sign=1,dist=2.7f,top=.92f},
                    new {x=-3.55f,z=.55f,sign=-1,dist=2.7f,top=.92f}
                };
                foreach(var route in routes)
                {
                    var actor=new GameObject("real production dive route");
                    spawned.Add(actor);
                    actor.transform.position=new Vector3(route.x,.9f,
                        route.z-route.sign*route.dist);
                    actor.AddComponent<Rigidbody>();actor.AddComponent<CapsuleCollider>();
                    var visual=new GameObject("visual");visual.transform.SetParent(actor.transform,false);
                    var m=actor.AddComponent<ChaseMotor>();
                    m.Initialize(new ChaseTuning(),visual.transform);
                    Physics.SyncTransforms();
                    float maxProgress=-99f,minBottom=99f;
                    bool fullyHorizontal=false,everCompact=false;
                    for(int i=0;i<110;i++)
                    {
                        m.Simulate(new ChaseAction(new Vector2(0,route.sign),
                            false,i==0,false,false),Dt,clock);
                        Physics.Simulate(Dt);clock+=Dt;
                        float p=route.sign*(m.Body.position.z-route.z);
                        maxProgress=Mathf.Max(maxProgress,p);
                        everCompact|=m.DiveCompactContact;
                        if(m.DiveColliderActive&&Mathf.Abs(p)<1.1f)
                        {
                            fullyHorizontal=true;
                            float low=m.Body.position.y+m.Capsule.center.y-
                                m.Capsule.radius;
                            minBottom=Mathf.Min(minBottom,low);
                        }
                    }
                    Assert.IsTrue(fullyHorizontal,
                        "Must cross module in the actual elongated dive collider: x="+
                        route.x+" sign="+route.sign+" progress="+maxProgress+
                        " compact="+everCompact+" state="+m.State);
                    Assert.IsFalse(everCompact,
                        "Another obstacle interrupted launch at "+route.x+
                        " dir "+route.sign);
                    Assert.Greater(minBottom,route.top+.003f,
                        "Never rely on a collision boost to clear the production rail");
                    Assert.Greater(maxProgress,1.5f,
                        "In-situ dive route blocked at "+route.x+
                        " direction "+route.sign);
                    spawned.Remove(actor);
                    UnityEngine.Object.DestroyImmediate(actor);
                }
            }
            finally {if(!open)EditorSceneManager.CloseScene(scene,true);}
        }

        [Test] public void ProductionBotRoutesAroundUnusableSupermanRail()
        {
            const string path="Assets/Scenes/SampleScene.unity";
            var existing=SceneManager.GetSceneByPath(path);
            bool open=existing.IsValid()&&existing.isLoaded;
            var scene=open?existing:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                // The bot cannot dive or slide. Its grid must route around
                // the beam rather than repeatedly charging into it upright.
                var owner=new GameObject("bot navigation test owner");
                spawned.Add(owner);
                var b=owner.AddComponent<ChaseBot>();
                var selfObj=new GameObject("chaser probe");
                spawned.Add(selfObj);
                selfObj.transform.position=new Vector3(0,.9f,-1.9f);
                selfObj.AddComponent<Rigidbody>();selfObj.AddComponent<CapsuleCollider>();
                var art=new GameObject("visual");art.transform.SetParent(selfObj.transform,false);
                var m=selfObj.AddComponent<ChaseMotor>();
                m.Initialize(new ChaseTuning(),art.transform);
                var opponentObj=new GameObject("static target");
                spawned.Add(opponentObj);
                opponentObj.transform.position=new Vector3(0,.9f,1.85f);
                opponentObj.AddComponent<Rigidbody>();opponentObj.AddComponent<CapsuleCollider>();
                var targetVisual=new GameObject("visual");
                targetVisual.transform.SetParent(opponentObj.transform,false);
                var target=opponentObj.AddComponent<ChaseMotor>();
                target.Initialize(new ChaseTuning(),targetVisual.transform);
                Physics.SyncTransforms();
                b.Initialize(m,target,ChaseRole.Chaser);
                float near=99f,furthestZ=-1.9f;
                for(int i=0;i<320;i++)
                {
                    var action=b.Decide(clock);
                    m.Simulate(action,Dt,clock);
                    Physics.Simulate(Dt);clock+=Dt;
                    near=Mathf.Min(near,Vector3.Distance(
                        m.Body.position,target.Body.position));
                    furthestZ=Mathf.Max(furthestZ,m.Body.position.z);
                }
                Assert.Greater(furthestZ,.5f,
                    "Production bot must navigate around the rail supports");
                Assert.Less(near,1.5f,
                    "Bot should reach the opposite side without obstacle-specific moves");
            }
            finally {if(!open)EditorSceneManager.CloseScene(scene,true);}
        }

        [Test] public void PursuerCanNavigateEverySpawnPairInFlagshipArena()
        {
            const string path="Assets/Scenes/SampleScene.unity";
            var existing=SceneManager.GetSceneByPath(path);
            bool open=existing.IsValid()&&existing.isLoaded;
            var scene=open?existing:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var pairs=new[]{(ChaseMatch.ChaserStart,ChaseMatch.EvaderStart)};
                for(int pair=0;pair<pairs.Length;pair++)
                    for(int reverse=0;reverse<2;reverse++)
                    {
                        var self=reverse==0?pairs[pair].Item1:pairs[pair].Item2;
                        var dest=reverse==0?pairs[pair].Item2:pairs[pair].Item1;
                        var owner=new GameObject("live navigation probe");
                        spawned.Add(owner);
                        var bot=owner.AddComponent<ChaseBot>();
                        var actors=new List<GameObject>();
                        ChaseMotor Make(Vector3 position)
                        {
                            var obj=new GameObject("probe body");
                            spawned.Add(obj);actors.Add(obj);
                            obj.transform.position=position;
                            obj.AddComponent<Rigidbody>();
                            obj.AddComponent<CapsuleCollider>();
                            var visual=new GameObject("visual");
                            visual.transform.SetParent(obj.transform,false);
                            var motor=obj.AddComponent<ChaseMotor>();
                            motor.Initialize(new ChaseTuning(),visual.transform);
                            return motor;
                        }
                        var actor=Make(self);
                        var target=Make(dest);
                        Physics.SyncTransforms();
                        bot.Initialize(actor,target,ChaseRole.Chaser);
                        float nearest=float.PositiveInfinity;
                        for(int tick=0;tick<520;tick++)
                        {
                            var action=bot.Decide(clock);
                            actor.Simulate(action,Dt,clock);
                            Physics.Simulate(Dt);
                            clock+=Dt;
                            nearest=Mathf.Min(nearest,
                                Vector3.Distance(actor.Body.position,target.Body.position));
                            if(nearest<1.2f)break;
                        }
                        Assert.Less(nearest,1.5f,
                            "Chaser unable to reach static evader in spawn "+
                            pair+" direction "+reverse+" min="+nearest+
                            " final="+actor.Body.position+" state="+actor.State+
                            " grounded="+actor.Grounded);
                        spawned.Remove(owner);
                        UnityEngine.Object.DestroyImmediate(owner);
                        foreach(var obj in actors)
                        {
                            spawned.Remove(obj);UnityEngine.Object.DestroyImmediate(obj);
                        }
                    }
            }
            finally {if(!open)EditorSceneManager.CloseScene(scene,true);}
        }

        // Skill should have a timing window. A route requiring one exact
        // input frame would be a button puzzle, not a competitive line.
        // The Chibi's visual Leap spans 1.18m laterally (measured from the
        // approved Blender action), wider than its 0.86m physical capsule.
        [Test] public void ProductionCrossingsPermitMultipleTakeoffTimings()
        {
            const string path="Assets/Scenes/SampleScene.unity";
            var existing=SceneManager.GetSceneByPath(path);
            bool open=existing.IsValid()&&existing.isLoaded;
            var scene=open?existing:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                foreach(var route in new[]{
                    new {x=0f,z=0f,width=2.3f,top=.94f},
                    new {x=-3.55f,z=.55f,width=1.7f,top=.92f}})
                    foreach(int sign in new[]{-1,1})
                    {
                        var distances=new[]{2.4f,2.55f,2.7f,2.85f,3f};
                        int valid=0;
                        float shortest=float.PositiveInfinity;
                        foreach(float startDistance in distances)
                        {
                            var obj=new GameObject("live layout timing probe");
                            spawned.Add(obj);
                            obj.transform.position=new Vector3(route.x,.9f,
                                route.z-sign*startDistance);
                            obj.AddComponent<Rigidbody>();obj.AddComponent<CapsuleCollider>();
                            var visual=new GameObject("visual");
                            visual.transform.SetParent(obj.transform,false);
                            var m=obj.AddComponent<ChaseMotor>();
                            m.Initialize(new ChaseTuning(),visual.transform);
                            Physics.SyncTransforms();
                            bool crossed=false,clean=true;
                            float maxForward=-100f,minBottom=100f;
                            for(int t=0;t<90;t++)
                            {
                                m.Simulate(new ChaseAction(
                                    new Vector2(0,sign),false,t==0,false,false),Dt,clock);
                                Physics.Simulate(Dt);clock+=Dt;
                                float p=sign*(m.Body.position.z-route.z);
                                maxForward=Mathf.Max(maxForward,p);
                                if(m.DiveCompactContact)clean=false;
                                if(m.State==ChaseMovementState.Leaping&&
                                    m.DiveColliderActive && Mathf.Abs(p)<1.14f)
                                {
                                    crossed=true;
                                    float bottom=m.Body.position.y+
                                        m.Capsule.center.y-m.Capsule.radius;
                                    minBottom=Mathf.Min(minBottom,bottom);
                                    if(bottom<route.top+.003f||
                                        sign*m.Body.linearVelocity.z<7.95f)
                                        clean=false;
                                }
                            }
                            if(clean&&crossed&&maxForward>1.5f&&
                                minBottom>route.top+.003f)
                            {
                                valid++;shortest=Mathf.Min(shortest,startDistance);
                            }
                            spawned.Remove(obj);UnityEngine.Object.DestroyImmediate(obj);
                        }
                        Assert.GreaterOrEqual(valid,2,
                            "Only a single or no usable takeoff timing at "+
                            route.x+" sign "+sign+" valid="+valid);
                        Debug.Log("CHASE_MASTERED_LINE x="+route.x+" direction="+sign+
                            " safe_timing_samples="+valid+"/5 first="+shortest);
                    }
            }
            finally {if(!open)EditorSceneManager.CloseScene(scene,true);}
        }

        [Test] public void ProductionCrossingsPreserveActualFeetFirstUnderpasses()
        {
            const string path="Assets/Scenes/SampleScene.unity";
            var existing=SceneManager.GetSceneByPath(path);
            bool open=existing.IsValid()&&existing.isLoaded;
            var scene=open?existing:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                foreach(var route in new[]{
                    new {x=0f,z=0f,underside=.86f},
                    new {x=-3.55f,z=.55f,underside=.85f}})
                    foreach(int sign in new[]{-1,1})
                    {
                        var obj=new GameObject("live layout underpass probe");
                        spawned.Add(obj);
                        obj.transform.position=new Vector3(route.x,.9f,
                            route.z-sign*1.15f);
                        obj.AddComponent<Rigidbody>();obj.AddComponent<CapsuleCollider>();
                        var visual=new GameObject("visual");
                        visual.transform.SetParent(obj.transform,false);
                        var m=obj.AddComponent<ChaseMotor>();
                        m.Initialize(new ChaseTuning(),visual.transform);
                        m.Body.linearVelocity=Vector3.forward*(sign*6f);
                        Physics.SyncTransforms();
                        bool under=false,exited=false,stood=false;
                        float minGap=100f;
                        for(int t=0;t<220;t++)
                        {
                            m.Simulate(new ChaseAction(new Vector2(0,sign),
                                false,false,t==0,false),Dt,clock);
                            Physics.Simulate(Dt);clock+=Dt;
                            float p=sign*(m.Body.position.z-route.z);
                            if(p>-.3f&&p<.5f)
                            {
                                under=true;
                                Assert.IsTrue(m.SlideColliderActive);
                                float top=m.Body.position.y+m.SlideBox.center.y+
                                    m.SlideBox.size.y*.5f;
                                float gap=route.underside-top;
                                minGap=Mathf.Min(minGap,gap);
                                Assert.Greater(gap,.003f,
                                    "Sliding Chibi cannot visibly pass through a low crossing");
                            }
                            exited|=p>1.5f;
                            stood|=exited&&m.State==ChaseMovementState.Standing;
                        }
                        Assert.IsTrue(under&&exited&&stood,
                            "Production underpass failed at "+route.x+" dir "+sign);
                        Debug.Log("CHASE_UNDERPASS x="+route.x+" dir="+sign+
                            " minimum_box_clearance="+minGap.ToString("F3"));
                        spawned.Remove(obj);UnityEngine.Object.DestroyImmediate(obj);
                    }
            }
            finally {if(!open)EditorSceneManager.CloseScene(scene,true);}
        }

        [Test] public void DeckAndGatesClassifyBlockedVsJumpableRoutes()
        {
            var podium=Prefab("LowDeck");
            var deck=podium.GetComponentsInChildren<ChaseHurdle>();
            Assert.IsNotEmpty(deck,"Deck should be known jumpable geometry");
            var gates=Prefab("OffsetGates");
            Assert.IsTrue(gates.GetComponentsInChildren<ChaseBlocker>().Length>=4,
                "Bot must see the upright-blocked narrow gates");
            Assert.IsTrue(gates.GetComponentsInChildren<ChaseOverheadPassage>().Length>=2,
                "Human may still slide beneath the shallow upper crossbars");
        }
    }
}
