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
            Assert.AreEqual(.86f,beam.bounds.min.y,.006f);
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
