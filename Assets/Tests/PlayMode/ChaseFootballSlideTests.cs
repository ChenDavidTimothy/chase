using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chase.Tests
{
    // True PhysX stepping with the same motor, Rigidbody, and solid colliders
    // as human and NPC. Includes an independently imported Blender silhouette.
    public sealed class ChaseFootballSlideTests
    {
        const float Dt=.02f;
        readonly List<GameObject> created=new List<GameObject>();
        SimulationMode before;
        float clock;
        [SetUp] public void Setup()
        {
            before=Physics.simulationMode;
            Physics.simulationMode=SimulationMode.Script;
            clock=23f;
        }
        [TearDown] public void TearDown()
        {
            Physics.simulationMode=before;
            foreach(GameObject o in created)
                if(o!=null)UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }
        GameObject Box(string name,Vector3 position,Vector3 size)
        {
            GameObject o=GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name=name;o.transform.position=position;o.transform.localScale=size;
            created.Add(o);return o;
        }
        void Floor()=>Box("real PhysX floor",new Vector3(0,-.1f,0),
            new Vector3(40,.2f,40));
        ChaseMotor Actor(Vector3 location)
        {
            var o=new GameObject("shared physical football slider");
            created.Add(o);o.transform.position=location;
            o.AddComponent<Rigidbody>();o.AddComponent<CapsuleCollider>();
            var visual=new GameObject("visual");
            visual.transform.SetParent(o.transform,false);
            var motor=o.AddComponent<ChaseMotor>();
            motor.Initialize(new ChaseTuning(),visual.transform);
            Physics.SyncTransforms();return motor;
        }
        void Step(ChaseMotor motor,Vector2 direction=default,
            bool slide=false,bool jump=false,bool leap=false,bool crouch=false)
        {
            motor.Simulate(new ChaseAction(direction,jump,leap,slide,crouch),
                Dt,clock);
            Physics.Simulate(Dt);clock+=Dt;
        }

        [UnityTest] public IEnumerator SlideIsReallyLowLongSolidAndCommitted()
        {
            Floor();
            var m=Actor(new Vector3(0,.9f,0));
            m.Body.linearVelocity=Vector3.forward*6f;
            Step(m,Vector2.up,true);
            Assert.AreEqual(ChaseMovementState.Sliding,m.State);
            Assert.IsTrue(m.SlideColliderActive,
                "Feet-first long low body should replace standing capsule immediately");
            Assert.IsFalse(m.Capsule.enabled);
            Assert.IsFalse(m.SlideBox.isTrigger);
            Assert.That(m.SlideBox.size.z,Is.GreaterThan(1.85f));
            Assert.That(m.SlideBox.size.y,Is.LessThan(.85f));
            Assert.That(m.SlideBox.center.y,Is.LessThan(-.40f));
            Assert.That(Vector3.Dot(m.Body.rotation*Vector3.forward,
                Vector3.forward),Is.GreaterThan(.99f));
            float initial=m.Velocity.z;
            for(int i=0;i<21;i++)
            {
                var adversary=i%2==0?Vector2.left:Vector2.right;
                Step(m,adversary,i%4==0,i%3==0,i%5==0);
                Assert.AreEqual(ChaseMovementState.Sliding,m.State);
                Assert.IsTrue(m.SlideColliderActive && !m.Capsule.enabled);
                Assert.That(Mathf.Abs(m.Velocity.x),Is.LessThan(.002f),
                    "No A/D steering while body is horizontal");
                Assert.That(m.Velocity.z,Is.LessThanOrEqualTo(initial+.002f),
                    "No slide acceleration from repeated buttons");
                initial=m.Velocity.z;
            }
            Assert.Greater(m.Body.position.z,1.35f);
            Assert.Less(m.Body.position.z,2.1f);
            bool sawGetup=false,sawColliderUnion=false;
            for(int i=0;i<30;i++)
            {
                Step(m);
                sawGetup|=m.State==ChaseMovementState.SlideRecovery;
                sawColliderUnion|=m.SlideColliderActive && m.Capsule.enabled;
                Assert.IsTrue(m.SlideColliderActive||m.Capsule.enabled,
                    "Never disable all physical collision on transition");
            }
            Assert.IsTrue(sawGetup&&sawColliderUnion,
                "Body must remain solid through the protected get-up");
            Assert.AreEqual(ChaseMovementState.Standing,m.State);
            Assert.IsFalse(m.SlideColliderActive);
            Assert.IsTrue(m.Capsule.enabled);
            Assert.AreEqual(1.8f,m.Capsule.height,.003f);
            yield return null;
        }

        [UnityTest] public IEnumerator SlidingWithoutRunMomentumDoesNotInventSpeed()
        {
            Floor();var m=Actor(new Vector3(0,.9f,0));
            Step(m,Vector2.up,true);
            Assert.AreEqual(ChaseMovementState.Sliding,m.State);
            Assert.IsTrue(m.SlideColliderActive);
            for(int i=0;i<27;i++)Step(m,Vector2.up);
            Assert.That(m.Body.position.z,Is.LessThan(.035f));
            Assert.That(m.Velocity.z,Is.LessThan(.01f));
            yield return null;
        }

        [UnityTest] public IEnumerator SolidFootballSlideCannotTunnelThroughWall()
        {
            Floor();
            Box("immovable wall",new Vector3(0,1f,2.45f),
                new Vector3(5,2f,.2f));
            var m=Actor(new Vector3(0,.9f,0));
            m.Body.linearVelocity=Vector3.forward*6f;
            Step(m,Vector2.up,true);
            Assert.IsTrue(m.SlideColliderActive);
            float maximum=m.Body.position.z;
            for(int i=0;i<55;i++)
            {
                Step(m,Vector2.up);
                maximum=Mathf.Max(maximum,m.Body.position.z);
                Assert.IsTrue(m.SlideColliderActive||m.Capsule.enabled);
            }
            // Wall's near face is 2.35m; standing capsule may later approach
            // to center 2.0m. A long box must stop even farther away.
            Assert.Less(maximum,2.07f,
                "Standing capsule may approach physical wall after get-up");
            Assert.AreEqual(ChaseMovementState.Standing,m.State);
            yield return null;
        }

        [UnityTest] public IEnumerator ExtremelyLowRoofKeepsBodyHorizontalUntilCrawlingClear()
        {
            Floor();
            Box("low solid canopy underside at 1m",
                new Vector3(0,1.15f,0),new Vector3(2.7f,.3f,3f));
            var m=Actor(new Vector3(0,.9f,-2.5f));
            m.Body.linearVelocity=Vector3.forward*6f;
            Step(m,Vector2.up,true);
            Assert.IsTrue(m.SlideColliderActive);
            bool heldLow=false;
            for(int i=0;i<42;i++)
            {
                Step(m);
                if(m.State==ChaseMovementState.SlideRecovery &&
                    m.SlideColliderActive && !m.Capsule.enabled)
                    heldLow=true;
            }
            Assert.IsTrue(heldLow,"Under an extremely low shelf no upright recovery");
            Assert.IsTrue(m.SlideColliderActive);
            Assert.IsFalse(m.Capsule.enabled);
            bool escaped=false;
            for(int i=0;i<250;i++)
            {
                Step(m,Vector2.up);
                if(m.State==ChaseMovementState.Standing)
                {escaped=true;break;}
                Assert.IsTrue(m.SlideColliderActive || m.Capsule.enabled);
                if(m.Body.position.z<1.8f)
                    Assert.IsFalse(m.Capsule.enabled && !m.SlideColliderActive,
                        "No expansion through a roof while crawling");
            }
            Assert.IsTrue(escaped,"W should crawl low body out and then get up; " +
                "state="+m.State+" at="+m.Body.position+" grounded="+m.Grounded+
                " canStand="+m.CanStand+" box="+m.SlideColliderActive+
                " rising="+m.SlideGettingUp+" velocity="+m.Velocity);
            Assert.IsFalse(m.SlideColliderActive);
            yield return null;
        }

        [UnityTest] public IEnumerator LowTackleAgainstImmediateSideWallKeepsCollisionAndRecovery()
        {
            Floor();
            Box("solid wall inside future long slide volume",
                new Vector3(0,1f,.65f),new Vector3(4,2f,.18f));
            var m=Actor(new Vector3(0,.9f,0));
            m.Body.linearVelocity=Vector3.forward*6f;
            Step(m,Vector2.up,true);
            Assert.AreEqual(ChaseMovementState.Sliding,m.State,
                "Slide button is not ignored next to wall");
            Assert.IsTrue(m.Capsule.enabled && !m.SlideColliderActive,
                "A long box must not spawn inside a wall");
            float maximum=m.Body.position.z;
            bool recovered=false;
            for(int i=0;i<55;i++)
            {
                Step(m);
                maximum=Mathf.Max(maximum,m.Body.position.z);
                recovered|=m.State==ChaseMovementState.SlideRecovery;
            }
            Assert.IsTrue(recovered);
            Assert.AreEqual(ChaseMovementState.Standing,m.State);
            Assert.Less(maximum,.40f,"Capsule cannot pass the wall either");
            yield return null;
        }

        [UnityTest] public IEnumerator ImportedFootballPoseIsFeetFirstHorizontalAndInsideBox()
        {
            var actor=new GameObject("football tackle visual audit");
            var baked=new Mesh();
            try
            {
                created.Add(actor);
                actor.transform.position=new Vector3(0,.9f,0);
                actor.AddComponent<Rigidbody>();actor.AddComponent<CapsuleCollider>();
                var visual=UnityEngine.Object.Instantiate(
                    Resources.Load<GameObject>("ChibiVisual"),actor.transform,false);
                visual.transform.localPosition=Vector3.down*.9f;
                var m=actor.AddComponent<ChaseMotor>();
                m.Initialize(new ChaseTuning(),visual.transform);
                var animator=visual.GetComponentInChildren<Animator>();
                var transforms=visual.GetComponentsInChildren<Transform>(true);
                var head=Array.Find(transforms,t=>t.name=="head");
                var pelvis=Array.Find(transforms,t=>t.name=="pelvis");
                var frontFoot=Array.Find(transforms,t=>t.name=="foot_left");
                Assert.IsNotNull(head);
                Assert.IsNotNull(pelvis);
                Assert.IsNotNull(frontFoot);
                foreach(float t in new[]{0f,.20f,.50f,.75f,.96f})
                {
                    animator.Play("Slide",0,t);animator.Update(.02f);
                    Vector3 torso=(head.position-pelvis.position).normalized;
                    Assert.Less(Vector3.Dot(torso,Vector3.forward),-.94f,
                        "Head must lean BACK behind the hip, not sit upright");
                    Assert.Less(Mathf.Abs(torso.y),.26f,
                        "Football sliding tackle torso must be horizontal");
                    Assert.Greater(Vector3.Dot(frontFoot.position-pelvis.position,
                        Vector3.forward),.42f,
                        "Extended football tackle foot must lead the slide");
                    var low=new Bounds(Vector3.zero,Vector3.zero);
                    bool started=false;
                    foreach(var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        renderer.BakeMesh(baked,true);
                        foreach(var v in baked.vertices)
                        {
                            Vector3 pt=actor.transform.InverseTransformPoint(
                                renderer.transform.TransformPoint(v));
                            if(!started){low=new Bounds(pt,Vector3.zero);started=true;}
                            else low.Encapsulate(pt);
                        }
                    }
                    Assert.IsTrue(started);
                    Vector3 min=m.SlideBox.center-m.SlideBox.size*.5f;
                    Vector3 max=m.SlideBox.center+m.SlideBox.size*.5f;
                    Assert.That(low.max.y,Is.LessThan(max.y+.055f),
                        "Real sliding head may not exceed low solid collider");
                    Assert.That(low.min.y,Is.GreaterThan(min.y-.055f));
                    Assert.That(low.max.z,Is.LessThan(max.z+.10f));
                    Assert.That(low.min.z,Is.GreaterThan(min.z-.10f));
                    Assert.That(low.max.x,Is.LessThan(max.x+.10f));
                    Assert.That(low.min.x,Is.GreaterThan(min.x-.10f));
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(baked);}
            yield return null;
        }
    }
}
