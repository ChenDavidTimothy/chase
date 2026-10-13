using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chase.Tests
{
    // End-to-end PhysX routes. Geometry is identical to the authored modular rail.
    // Neither route can succeed using triggers, teleporting or a special obstacle mode.
    public sealed class ChaseDualRouteObstacleTests
    {
        const float Dt = .02f;
        readonly List<GameObject> created = new List<GameObject>();
        SimulationMode oldMode;
        float clock;

        [SetUp] public void SetUp()
        {
            oldMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            clock = 40f;
        }
        [TearDown] public void TearDown()
        {
            Physics.simulationMode = oldMode;
            foreach (var o in created)
                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }
        GameObject Solid(string name, Vector3 position, Vector3 size)
        {
            GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name = name;
            o.transform.position = position;
            o.transform.localScale = size;
            created.Add(o);
            return o;
        }
        void Ground() => Solid("ground",new Vector3(0,-.1f,0),
            new Vector3(25,.2f,25));
        void UnderOverRail()
        {
            // Exactly the same scale and positions as the reusable prefab.
            Solid("solid dual-route beam",new Vector3(0,.90f,0),
                new Vector3(2.30f,.08f,.26f));
            for (int side = -1; side <= 1; side += 2)
                Solid("side rail post",new Vector3(side*1.21f,.6f,0),
                    new Vector3(.12f,1.20f,.26f));
            Physics.SyncTransforms();
        }
        void NarrowBench()
        {
            Solid("solid narrow bench crossing",new Vector3(0,.885f,0),
                new Vector3(1.70f,.07f,.34f));
            for(int side=-1;side<=1;side+=2)
                Solid("solid narrow bench foot",new Vector3(side*.91f,.615f,0),
                    new Vector3(.12f,1.23f,.34f));
            Physics.SyncTransforms();
        }

        void OriginalShelf()
        {
            Solid("lower sill",new Vector3(0,.275f,0),new Vector3(2.2f,.55f,.6f));
            Solid("upper shelf",new Vector3(0,2.525f,0),new Vector3(2.2f,.35f,.6f));
            Physics.SyncTransforms();
        }
        ChaseMotor Actor(Vector3 position,float diveVertical = 7.25f)
        {
            var a = new GameObject("physics athlete");
            created.Add(a);
            a.transform.position = position;
            a.AddComponent<Rigidbody>();
            a.AddComponent<CapsuleCollider>();
            var visual = new GameObject("visual");
            visual.transform.SetParent(a.transform, false);
            var motor = a.AddComponent<ChaseMotor>();
            var tuning = new ChaseTuning { leapVerticalSpeed = diveVertical };
            motor.Initialize(tuning, visual.transform);
            Physics.SyncTransforms();
            return motor;
        }
        void Step(ChaseMotor motor, Vector2 move = default,
            bool dive = false, bool slide = false, bool jump = false)
        {
            motor.Simulate(new ChaseAction(move,jump,dive,slide,false),Dt,clock);
            Physics.Simulate(Dt);
            clock += Dt;
        }

        [UnityTest] public IEnumerator SupermanClearsBeamAboveWithoutSolverLift()
        {
            Ground(); UnderOverRail();
            var m = Actor(new Vector3(0,.9f,-2.70f));
            bool overlap = false, cleared = false;
            float minClearance = float.PositiveInfinity;
            for (int i = 0; i < 140; i++)
            {
                Step(m,Vector2.up,dive:i==0);
                // Check the complete long capsule, including its trailing end.
                if (m.DiveColliderActive &&
                    m.Body.position.z+1.135f > -.13f &&
                    m.Body.position.z-1.135f < .13f)
                {
                    overlap = true;
                    float bottom = m.Body.position.y+m.Capsule.center.y-
                        m.Capsule.radius;
                    minClearance = Mathf.Min(minClearance,bottom-.94f);
                    Assert.Greater(bottom,.943f,
                        "Full dive capsule must genuinely travel OVER the solid rail");
                    Assert.Greater(m.Body.linearVelocity.z,8.0f,
                        "Cannot use PhysX contact to launch upward or boost across");
                }
                if (m.Body.position.z > 1.5f) cleared = true;
            }
            Assert.IsTrue(overlap && cleared,"Dive must traverse entire rail");
            Debug.Log("CHASE_DUAL_ROUTE_DIVE_PASS min_clearance="+minClearance);
            yield return null;
        }

        [UnityTest] public IEnumerator FootballSlideThenForwardCrawlPassesUnderSameSolidRail()
        {
            Ground(); UnderOverRail();
            var m=Actor(new Vector3(0,.9f,-1.15f));
            m.Body.linearVelocity=Vector3.forward*6f;
            bool beneath=false, recovered=false, cleared=false;
            for(int i=0;i<220;i++)
            {
                Step(m,Vector2.up,slide:i==0);
                if(m.Body.position.z>-.3f && m.Body.position.z<.5f)
                {
                    beneath=true;
                    Assert.IsTrue(m.SlideColliderActive,
                        "The rail must keep an actor physically low, without phasing");
                    float top=m.Body.position.y+m.SlideBox.center.y+
                        m.SlideBox.size.y*.5f;
                    Assert.Less(top,.861f,"Slide must fit under underside");
                }
                if(m.Body.position.z>1.5f)cleared=true;
                if(cleared && m.State==ChaseMovementState.Standing)
                    recovered=true;
            }
            Assert.IsTrue(beneath && cleared && recovered,
                "Under-route must pass and return to standing with normal input");
            yield return null;
        }

        [UnityTest] public IEnumerator NarrowBenchAlsoAllowsBothPhysicalRoutes()
        {
            Ground();NarrowBench();
            var diver=Actor(new Vector3(0,.9f,-2.70f));
            float narrowMargin=float.PositiveInfinity;
            int overlap=0;
            for(int i=0;i<90;i++)
            {
                Step(diver,Vector2.up,dive:i==0);
                if(diver.DiveColliderActive &&
                    diver.Body.position.z+1.135f>-.17f &&
                    diver.Body.position.z-1.135f<.17f)
                {
                    overlap++;
                    float bottom=diver.Body.position.y+diver.Capsule.center.y-
                        diver.Capsule.radius;
                    narrowMargin=Mathf.Min(narrowMargin,bottom-.92f);
                    Assert.Greater(bottom,.924f,
                        "Narrow bench cannot be crossed by a collision boost");
                    Assert.Greater(diver.Body.linearVelocity.z,8.0f);
                }
            }
            Assert.Greater(overlap,8);
            Assert.Greater(diver.Body.position.z,1.5f);
            Debug.Log("CHASE_DUAL_ROUTE_BENCH_DIVE min_clearance="+narrowMargin);
            UnityEngine.Object.DestroyImmediate(diver.gameObject);
            var slider=Actor(new Vector3(0,.9f,-1.15f));
            slider.Body.linearVelocity=Vector3.forward*6f;
            bool crossed=false,stood=false,under=false;
            for(int i=0;i<220;i++)
            {
                Step(slider,Vector2.up,slide:i==0);
                if(slider.Body.position.z>-.3f && slider.Body.position.z<.5f)
                {
                    under=true;
                    Assert.IsTrue(slider.SlideColliderActive);
                    float high=slider.Body.position.y+slider.SlideBox.center.y+
                        slider.SlideBox.size.y*.5f;
                    Assert.Less(high,.851f);
                }
                crossed|=slider.Body.position.z>1.5f;
                stood|=crossed&&slider.State==ChaseMovementState.Standing;
            }
            Assert.IsTrue(under&&crossed&&stood,
                "Narrow bench must permit ordinary slide/crawl under-route");
            yield return null;
        }

        [UnityTest] public IEnumerator NaturalRunUpCanBecomeUnderRailSlideWithoutStagingImpulse()
        {
            Ground(); UnderOverRail();
            var m=Actor(new Vector3(0,.9f,-3f));
            bool launched=false,beneath=false,cleared=false,stood=false;
            for(int i=0;i<240;i++)
            {
                bool slide=!launched && m.Body.position.z>=-1.65f;
                if(slide)
                {
                    Assert.That(m.HorizontalVelocity.magnitude,
                        Is.GreaterThan(5.7f),
                        "Natural W acceleration must supply tackle momentum");
                    launched=true;
                }
                Step(m,Vector2.up,slide:slide);
                if(m.State==ChaseMovementState.Sliding &&
                    m.Body.position.z>-.3f && m.Body.position.z<.5f)
                    beneath=true;
                cleared|=m.Body.position.z>1.5f;
                stood|=cleared&&m.State==ChaseMovementState.Standing;
            }
            Assert.IsTrue(launched&&beneath&&cleared&&stood,
                "A player-generated run-up must slide under and stand beyond the rail");
            yield return null;
        }

        [UnityTest] public IEnumerator UprightSprintCannotIgnoreDualRouteBeam()
        {
            Ground();UnderOverRail();
            var m=Actor(new Vector3(0,.9f,-2f));
            float maxZ=-2;
            for(int i=0;i<120;i++)
            {
                Step(m,Vector2.up);
                maxZ=Mathf.Max(maxZ,m.Body.position.z);
            }
            Assert.Less(maxZ,-.35f,"Uncrouched sprint cannot ghost through rail");
            yield return null;
        }

        [UnityTest] public IEnumerator OriginalAerialShelfRemainsAValidTimedDiveRoute()
        {
            Ground();OriginalShelf();
            bool anyCrossed=false;
            foreach(float start in new[]{-3.5f,-3.0f,-2.5f,-2.0f})
            {
                var m=Actor(new Vector3(0,.9f,start));
                for(int i=0;i<155;i++)Step(m,Vector2.up,dive:i==0);
                bool crossed=m.Body.position.z>1.5f;
                Debug.Log("CHASE_SHELF_TUNING start="+start+" crossed="+crossed+
                    " z="+m.Body.position.z+" state="+m.State);
                anyCrossed|=crossed;
                UnityEngine.Object.DestroyImmediate(m.gameObject);
            }
            Assert.IsTrue(anyCrossed,
                "New dive must not eliminate the existing shelf route");
            yield return null;
        }
    }
}
