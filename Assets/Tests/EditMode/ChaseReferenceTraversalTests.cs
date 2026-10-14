using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chase.Tests
{
    // Deliberately isolated: real production ChaseMotor+PhysX, not a camera
    // animation, scene-scale collider hack, or authored crossing trigger.
    public sealed class ChaseReferenceTraversalTests
    {
        const float X=40f,Dt=.02f;
        readonly List<GameObject> spawned=new List<GameObject>();
        SimulationMode prior;
        float clock;
        [SetUp] public void Start()
        {
            prior=Physics.simulationMode;
            Physics.simulationMode=SimulationMode.Script;
            clock=31f;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name="isolated reference crossing floor";
            floor.transform.position=new Vector3(X,-.10f,0);
            floor.transform.localScale=new Vector3(12f,.2f,16f);
            spawned.Add(floor);
        }
        [TearDown] public void End()
        {
            foreach(var g in spawned)
                if(g!=null)Object.DestroyImmediate(g);
            spawned.Clear();
            Physics.simulationMode=prior;
            Physics.SyncTransforms();
        }
        ChaseMotor Actor(float z)
        {
            var go=new GameObject("reference real motor");
            go.transform.position=new Vector3(X,.9f,z);
            spawned.Add(go);
            go.AddComponent<Rigidbody>();
            go.AddComponent<CapsuleCollider>();
            var visual=new GameObject("nonphysical visual");
            visual.transform.SetParent(go.transform,false);
            var motor=go.AddComponent<ChaseMotor>();
            motor.Initialize(new ChaseTuning(),visual.transform);
            Physics.SyncTransforms();
            return motor;
        }
        void Step(ChaseMotor motor,int sign,bool leap=false,bool slide=false)
        {
            motor.Simulate(new ChaseAction(new Vector2(0,sign),
                false,leap,slide,false),Dt,clock);
            Physics.Simulate(Dt);
            clock+=Dt;
        }
        [TestCase(-1,-.12f)]
        [TestCase(-1,0f)]
        [TestCase(-1,.12f)]
        [TestCase(1,-.12f)]
        [TestCase(1,0f)]
        [TestCase(1,.12f)]
        public void ReducedArenaSouthRackHasRealSlideApproachAndExit(
            int direction,float lateralOffset)
        {
            const string scenePath="Assets/Scenes/ArenaWCTTrial.unity";
            var arena=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            bool already=arena.IsValid()&&arena.isLoaded;
            if(!already)
                arena=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    scenePath,UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                // Full assembled course, not an isolated prefab. Slide
                // from the open lane across BOTH physical rack beams,
                // leave enough room for a real recovery and exit.
                var motor=Actor(0f);
                Vector3 center=new Vector3(.06f,0f,-3.13f);
                Vector3 forward=direction*Vector3.right;
                motor.Body.position=new Vector3(
                    center.x-forward.x*1.77f,.9f,
                    center.z+lateralOffset);
                motor.Body.linearVelocity=forward*6f;
                Physics.SyncTransforms();
                bool madeIt=false;
                float peak=0f;
                string trace="";
                for(int tick=0;tick<190;tick++)
                {
                    motor.Simulate(new ChaseAction(
                        new Vector2(forward.x,forward.z),false,false,
                        tick==0,false),Dt,clock);
                    Physics.Simulate(Dt);clock+=Dt;
                    var p=motor.Body.position;
                    peak=Mathf.Max(peak,direction*(p.x-center.x));
                    if(tick<30 && tick%3==0)
                        trace+=tick+":"+p.ToString("F2")+
                            "/"+motor.State+
                            "/slide="+motor.SlideColliderActive+"; ";
                    if(direction*(p.x-center.x)>1.75f &&
                        Mathf.Abs(p.z-center.z)<.54f &&
                        motor.Grounded)
                    {madeIt=true;break;}
                }
                Assert.IsTrue(madeIt,
                    "Full course blocks rack slide with direction "+
                    direction+", offset "+lateralOffset+
                    "; progress "+peak+
                    "; final "+motor.Body.position+
                    "; motor "+motor.State+"; "+trace);
            }
            finally
            {
                if(!already)
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(arena,true);
            }
        }

        [TestCase("WallSwitchWestNorth",-1)]
        [TestCase("WallSwitchWestNorth",1)]
        [TestCase("WallSwitchWestSouth",-1)]
        [TestCase("WallSwitchWestSouth",1)]
        [TestCase("WallSwitchEastNorth",-1)]
        [TestCase("WallSwitchEastNorth",1)]
        [TestCase("WallSwitchEastSouth",-1)]
        [TestCase("WallSwitchEastSouth",1)]
        public void ReducedArenaOtherCrossingsHaveRoomForActualSlides(
            string name,int sign)
        {
            const string path="Assets/Scenes/ArenaWCTTrial.unity";
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool already=scene.IsValid()&&scene.isLoaded;
            if(!already)
                scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    path,UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var root=scene.GetRootGameObjects().Single(o=>o.name.StartsWith("ARENA  /"));
                var group=root.GetComponentsInChildren<Transform>(true)
                    .Single(t=>t.name=="CHASE / WCT REFERENCE TRIAL");
                var assembly=group.Cast<Transform>()
                    .Single(t=>t.name=="REFERENCE / "+name);
                // Each perimeter skill beam crosses world Z.
                // The linked southern rack has its own full-scene tests.
                Vector3 advance=sign*Vector3.forward;
                Vector3 sideways=Vector3.Cross(Vector3.up,advance);
                foreach(float offset in new[]{-.12f,0f,.12f})
                {
                    var m=Actor(0f);
                    var center=assembly.position;
                    var start=center-advance*1.64f+
                        sideways*offset+Vector3.up*.9f;
                    m.Body.position=start;
                    m.Body.linearVelocity=advance*6f;
                    Physics.SyncTransforms();
                    bool crossed=false,compact=false;
                    float progress=-100f;
                    for(int tick=0;tick<165;tick++)
                    {
                        m.Simulate(new ChaseAction(
                            new Vector2(advance.x,advance.z),false,false,
                            tick==0,false),Dt,clock);
                        Physics.Simulate(Dt);clock+=Dt;
                        compact|=m.SlideColliderActive;
                        progress=Mathf.Max(progress,
                            Vector3.Dot(m.Body.position-center,advance));
                        if(progress>1.25f && m.Grounded)
                        {crossed=true;break;}
                    }
                    Assert.IsTrue(crossed&&compact,
                        name+" full-scene approach fails sign "+sign+
                        ", lateral "+offset+", progress "+progress+
                        ", compact="+compact+
                        ", motor="+m.State+" final="+m.Body.position);
                    spawned.Remove(m.gameObject);
                    Object.DestroyImmediate(m.gameObject);
                }
            }
            finally
            {
                if(!already)
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
            }
        }

        [TestCase("SouthWestRails",1.00f)]
        [TestCase("EastCrossbar",1.00f)]
        [TestCase("NorthInnerRail",1.00f)]
        public void LowCrossingHasNoPunitiveCeilingAndForgivingSlideEnvelope(
            string name,float centerHeight)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/WCTTrial/"+name+".prefab");
            Assert.NotNull(asset);
            var fixture=Object.Instantiate(asset);
            fixture.transform.position=new Vector3(X,0,0);
            spawned.Add(fixture);
            var colliders=fixture.GetComponentsInChildren<BoxCollider>();
            var beam=colliders.Single(c=>c.name.Contains("dual-choice crossing / solid"));
            Assert.AreEqual(centerHeight,beam.bounds.center.y,.006f);
            Assert.GreaterOrEqual(beam.bounds.min.y-.82f,.10f,
                "Low gate has less than 10 cm slide clearance");
            Assert.IsFalse(colliders.Any(c=>c.name.Contains("top sightline")),
                "Do not place a solid low ceiling above a jump/dive obstacle");

            foreach(int direction in new[]{-1,1})
                foreach(float offset in new[]{-.14f,0f,.14f})
                {
                    var athlete=Actor(-direction*1.25f);
                    athlete.Body.position+=Vector3.right*offset;
                    athlete.Body.linearVelocity=direction*Vector3.forward*5.6f;
                    Physics.SyncTransforms();
                    bool achieved=false;
                    for(int tick=0;tick<165;tick++)
                    {
                        Step(athlete,direction,slide:tick==0);
                        achieved|=direction*athlete.Body.position.z>1.32f &&
                            Mathf.Abs(athlete.Body.position.x-X)<.55f;
                    }
                    Assert.IsTrue(achieved,
                        name+" slide could not cross from "+direction+
                        " at lateral error "+offset);
                    spawned.Remove(athlete.gameObject);
                    Object.DestroyImmediate(athlete.gameObject);
                }
        }

        [TestCase(-.12f)]
        [TestCase(0f)]
        [TestCase(.12f)]
        public void EastRunUpGenuinelyConnectsRampTurnAndUnreachableGroundShelf(
            float laneOffset)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/WCTTrial/EastRamp.prefab");
            Assert.NotNull(asset);
            var obstacle=Object.Instantiate(asset);
            obstacle.transform.position=new Vector3(X,0,0);
            spawned.Add(obstacle);
            var boxes=obstacle.GetComponentsInChildren<BoxCollider>();
            var ramp=boxes.Single(c=>c.name.Contains("inclined walking surface / solid"));
            var runout=boxes.Single(c=>c.name.Contains("level runout / solid"));
            var corner=boxes.Single(c=>c.name.Contains("flat corner turn / solid"));
            var landing=boxes.Single(c=>c.name.Contains("jump landing / solid"));
            Assert.Greater(landing.bounds.max.y,1.20f,
                "High landing must require an elevation path, not a ground jump");
            Assert.Greater(landing.bounds.min.y,1.15f,
                "Final landing must be open underneath for another route");
            Assert.AreEqual(runout.bounds.max.y,corner.bounds.max.y,.005f);
            Assert.AreEqual(.62f,runout.bounds.max.y,.01f);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(
                ramp.transform.eulerAngles.x,0f)),10f,
                "This route requires a REAL angled solid walking incline");
            // Follow a genuine dynamic capsule uphill, turn 90 degrees
            // on the runout, then jump to the above-ground-only ledge.
            var athlete=Actor(-2.25f);
            athlete.Body.position+=Vector3.right*laneOffset;
            Physics.SyncTransforms();
            bool stoodOnRunout=false;
            bool turnedOnCorner=false;
            bool landedHigh=false;
            float peak=0f;
            int turnTick=-1,jumpTick=-1;
            int runoutFrames=0,cornerFrames=0;
            string trace="";
            for(int i=0;i<230;i++)
            {
                Vector3 pos=athlete.Body.position;
                bool turn=pos.z>1.55f && turnTick<0;
                if(turn)turnTick=i;
                bool turnPhase=turnTick>=0;
                if(turnPhase && athlete.Body.position.x>X+.30f &&
                    jumpTick<0)jumpTick=i;
                Vector2 input=turnPhase?Vector2.right:Vector2.up;
                athlete.Simulate(new ChaseAction(input,i==jumpTick,
                    false,false,false),Dt,clock);
                Physics.Simulate(Dt);clock+=Dt;
                pos=athlete.Body.position;
                peak=Mathf.Max(peak,pos.y);
                if(pos.z>1.35f && pos.z<3.03f &&
                   Mathf.Abs(pos.x-X)<.46f)runoutFrames++;
                if(turnPhase && pos.x>X+.15f && pos.z>1.75f &&
                   pos.z<3.56f)cornerFrames++;
                if(i%12==0)
                    trace+=i+":"+pos.ToString("F2")+" "+athlete.State+
                        " ground="+athlete.Grounded+";";
                if(pos.z>1.35f && pos.z<3.03f &&
                   Mathf.Abs(pos.x-X)<.46f && athlete.Grounded && pos.y>1.25f)
                    stoodOnRunout=true;
                if(turnPhase && pos.x>X+.15f && pos.z>1.75f &&
                   pos.z<3.56f && athlete.Grounded && pos.y>1.25f)
                    turnedOnCorner=true;
                if(pos.x>X+1.10f && pos.x<X+3.36f &&
                   pos.z>1.87f && pos.z<3.73f &&
                   athlete.Grounded && pos.y>2.05f)landedHigh=true;
            }
            Assert.IsTrue(stoodOnRunout,"Failed to physically run onto flat ramp continuation; maxY="+peak+
                " frames="+runoutFrames+"/"+cornerFrames+" turn="+turnTick+
                " jump="+jumpTick+" trace="+trace);
            Assert.IsTrue(turnedOnCorner,"Failed to follow physical 90-degree corner turn");
            Assert.GreaterOrEqual(jumpTick,0,
                "Never reached launch position on corner; turn="+turnTick+
                " peak="+peak+" trace="+trace);
            Assert.IsTrue(landedHigh,
                "Ramp route did not physically land on high deck; peak="+peak+
                " final="+athlete.Body.position+" trace="+trace);
        }

        [TestCase("SouthWestRails")]
        [TestCase("EastCrossbar")]
        public void DiveCrossingToleratesLateralAndLaunchTimingError(string name)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/WCTTrial/"+name+".prefab");
            Assert.NotNull(asset);
            var fixture=Object.Instantiate(asset);
            fixture.transform.position=new Vector3(X,0,0);
            spawned.Add(fixture);
            var beam=fixture.GetComponentsInChildren<BoxCollider>()
                .Single(c=>c.name.Contains("dual-choice crossing / solid"));
            foreach(int sign in new[]{-1,1})
                foreach(float offset in new[]{-.14f,0f,.14f})
                    foreach(float takeoff in new[]{2.55f,2.85f})
                    {
                        var m=Actor(-sign*takeoff);
                        m.Body.position+=Vector3.right*offset;
                        Physics.SyncTransforms();
                        bool actuallyClearedTop=false;
                        float progress=-100f;
                        for(int tick=0;tick<130;tick++)
                        {
                            Step(m,sign,leap:tick==0);
                            progress=Mathf.Max(progress,sign*m.Body.position.z);
                            if(m.DiveColliderActive &&
                                Mathf.Abs(m.Body.position.z)<.23f &&
                                m.Body.position.y+m.Capsule.center.y-
                                m.Capsule.radius>beam.bounds.max.y+.015f)
                                actuallyClearedTop=true;
                        }
                        Assert.IsTrue(actuallyClearedTop && progress>1.32f,
                            name+" dive failed after lateral offset "+offset+
                            ", takeoff "+takeoff+", sign "+sign+
                            ", progress "+progress+
                            ", cleared="+actuallyClearedTop);
                        spawned.Remove(m.gameObject);
                        Object.DestroyImmediate(m.gameObject);
                    }
        }

        [Test]
        public void EastElevationActuallyConnectsIntoCrownPodium()
        {
            // Isolate the two REAL saved prefabs in the same relative
            // cardinal placements as the authored arena. Walk the
            // elevated 2.29m athlete-center route into the 1.92m crown
            // landing with no collider disable, teleport or scripted vault.
            var east=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/WCTTrial/EastRamp.prefab");
            var crown=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/WCTTrial/CrownTower.prefab");
            Assert.NotNull(east);Assert.NotNull(crown);
            var e=Object.Instantiate(east);
            var t=Object.Instantiate(crown);
            e.transform.position=new Vector3(X,0,.4f);
            e.transform.rotation=Quaternion.Euler(0,270,0);
            t.transform.position=new Vector3(X-1.30f,0,4.74f);
            spawned.Add(e);spawned.Add(t);
            var runner=Actor(3.10f);
            runner.Body.position=new Vector3(X-2.25f,2.19f,3.10f);
            runner.Body.linearVelocity=Vector3.forward*4.5f;
            Physics.SyncTransforms();
            bool landedOnCrown=false;
            for(int i=0;i<130;i++)
            {
                Step(runner,1);
                if(runner.Grounded && runner.Body.position.z>4.0f &&
                   runner.Body.position.z<5.2f &&
                   runner.Body.position.y>1.83f)
                {
                    landedOnCrown=true;break;
                }
            }
            Assert.IsTrue(landedOnCrown,
                "Elevated ramp route does not lead onto accessible Crown "+
                "surface with the real motor. End="+runner.Body.position);
        }

        [Test]
        public void NorthernGalleryIsOneContinuousRealWalkingRoute()
        {
            var gallery=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/WCTTrial/NorthLinks.prefab");
            var link=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/WCTTrial/NorthGallerySpan.prefab");
            var bench=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/WCTTrial/BackBench.prefab");
            Assert.NotNull(gallery);Assert.NotNull(link);Assert.NotNull(bench);
            var g=Object.Instantiate(gallery);
            var l=Object.Instantiate(link);
            var b=Object.Instantiate(bench);
            g.transform.position=new Vector3(X-2.34f,0,4.35f);
            g.transform.rotation=Quaternion.Euler(0,90f,0);
            l.transform.position=new Vector3(X-1.61f,0,4.71f);
            b.transform.position=new Vector3(X-.51f,0,4.74f);
            spawned.Add(g);spawned.Add(l);spawned.Add(b);
            var athlete=Actor(4.71f);
            athlete.Body.position=new Vector3(X-2.51f,1.97f,4.71f);
            Physics.SyncTransforms();
            bool crossedLink=false,arrived=false;
            for(int i=0;i<75;i++)
            {
                athlete.Simulate(new ChaseAction(Vector2.right,
                    false,false,false,false),Dt,clock);
                Physics.Simulate(Dt);clock+=Dt;
                var pos=athlete.Body.position;
                if(athlete.Grounded && pos.x>X-1.60f &&
                   pos.x<X-1.25f && pos.y>1.83f)
                    crossedLink=true;
                if(athlete.Grounded && pos.x>X-.60f &&
                   pos.x<X-.20f && pos.y>1.83f)
                {arrived=true;break;}
            }
            Assert.IsTrue(crossedLink,
                "Gallery has an impassable step or unsupported gap at span");
            Assert.IsTrue(arrived,
                "Gallery does not continue into BackBench on real PhysX");
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void SouthRackAllowsPhysicalFullLengthPassage(int direction)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/WCTTrial/FrontGrid.prefab");
            Assert.NotNull(asset);
            var rack=Object.Instantiate(asset);
            rack.transform.position=new Vector3(X,0,0);
            spawned.Add(rack);
            foreach(float lateral in new[]{-.12f,0f,.12f})
            {
                var m=Actor(-direction*1.55f);
                m.Body.position+=Vector3.right*lateral;
                m.Body.linearVelocity=Vector3.forward*(direction*6f);
                Physics.SyncTransforms();
                bool crossedBoth=false;
                for(int tick=0;tick<195;tick++)
                {
                    Step(m,direction,slide:tick==0);
                    crossedBoth|=direction*m.Body.position.z>1.48f &&
                        Mathf.Abs(m.Body.position.x-X)<.55f;
                }
                Assert.IsTrue(crossedBoth,
                    "Linked south rack trapped real slide/crawl at offset "+
                    lateral+" from direction "+direction+" final "+
                    m.Body.position+" movement="+m.State);
                spawned.Remove(m.gameObject);
                Object.DestroyImmediate(m.gameObject);
            }
        }

        [TestCase("CrownTower", false)]
        [TestCase("SouthEastTower", true)]
        public void PodiumHasRealJumpAccessibleOpening(string name,bool westFacing)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/WCTTrial/"+name+".prefab");
            Assert.NotNull(asset);
            var fixture=Object.Instantiate(asset);
            fixture.transform.position=new Vector3(X,0,0);
            spawned.Add(fixture);
            var deck=fixture.GetComponentsInChildren<BoxCollider>()
                .Single(c=>c.name.Contains("full elevated deck / solid"));
            var guards=fixture.GetComponentsInChildren<BoxCollider>()
                .Where(c=>c.name.Contains("upper bridge") ||
                          c.name.Contains("tower side crown")).ToArray();
            Assert.AreEqual(3,guards.Length,"Podium should have exactly one open side");
            Assert.Greater(deck.bounds.min.y,.82f,
                "Podium must also preserve the genuine underpass");
            // Certified production jump is ~1.11m high. The real top must
            // fit that reach, with some margin for imperfect timing.
            Assert.Less(deck.bounds.max.y,1.07f);
            Vector3 approach=westFacing?Vector3.right:Vector3.forward;
            float startDistance=westFacing?2.85f:2.65f;
            var m=Actor(0f);
            m.Body.position=new Vector3(X,.9f,0)-approach*startDistance;
            m.Body.linearVelocity=approach*6f;
            Physics.SyncTransforms();
            bool entered=false;
            for(int i=0;i<85;i++)
            {
                m.Simulate(new ChaseAction(
                    new Vector2(approach.x,approach.z),i==0,false,false,false),
                    Dt,clock);
                Physics.Simulate(Dt);
                clock+=Dt;
                // Contact with the REAL top, inside the actual platform,
                // rather than jumping beside it or flying above it.
                Vector3 pos=m.Body.position;
                bool inside=Mathf.Abs(pos.x-X)<.55f && Mathf.Abs(pos.z)<.55f;
                if(inside && m.Grounded && pos.y>1.65f)
                    entered=true;
            }
            Assert.IsTrue(entered,
                name+" has no physically reachable entry to its upper deck");
        }

        [TestCase("WallSwitchWestNorth")]
        [TestCase("CrossFork")]
        public void SkillChoiceHasTwoRealTraversalsInBothDirections(string name)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Obstacles/WCTTrial/"+name+".prefab");
            Assert.NotNull(asset);
            var fixture=Object.Instantiate(asset);
            fixture.transform.position=new Vector3(X,0,0);
            spawned.Add(fixture);
            var beam=fixture.GetComponentsInChildren<BoxCollider>()
                .First(c=>c.name.Contains("genuine under-over"));
            Assert.AreEqual(.94f,beam.bounds.min.y,.006f);
            foreach(int sign in new[]{-1,1})
            {
                // A committed leap can pass OVER the solid bar. Acceleration
                // and gravity are those of the genuine player motor.
                var dive=Actor(-sign*2.7f);
                bool high=false;
                float progress=-100f;
                for(int i=0;i<150;i++)
                {
                    Step(dive,sign,leap:i==0);
                    progress=Mathf.Max(progress,sign*dive.Body.position.z);
                    if(dive.DiveColliderActive&&Mathf.Abs(dive.Body.position.z)<.20f)
                        high|=dive.Body.position.y+
                            dive.Capsule.center.y-dive.Capsule.radius>
                            beam.bounds.max.y+.005f;
                }
                Assert.Greater(progress,1.40f,name+" dive cannot clear solid beam "+sign);
                Assert.IsTrue(high,name+" did not actually clear beam from above "+sign);
                spawned.Remove(dive.gameObject);Object.DestroyImmediate(dive.gameObject);

                var under=Actor(-sign*1.15f);
                under.Body.linearVelocity=Vector3.forward*(sign*6f);
                bool crossed=false;
                for(int i=0;i<200;i++)
                {
                    Step(under,sign,slide:i==0);
                    if(sign*under.Body.position.z>1.45f)crossed=true;
                }
                Assert.IsTrue(crossed,
                    name+" feet-first PhysX slide failed to cross "+sign);
                spawned.Remove(under.gameObject);Object.DestroyImmediate(under.gameObject);
            }
        }
    }
}
