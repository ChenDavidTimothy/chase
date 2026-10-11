using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chase.Tests
{
    public sealed class ChasePhysicsTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        SimulationMode previousSimulation;
        float t;

        [SetUp] public void SetUp()
        {
            previousSimulation = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            t = 10f;
        }
        [TearDown] public void TearDown()
        {
            Physics.simulationMode = previousSimulation;
            foreach (GameObject o in objects)
                if (o != null) Object.DestroyImmediate(o);
            objects.Clear();
        }
        GameObject Box(string name, Vector3 pos, Vector3 size)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name = name; o.transform.position = pos; o.transform.localScale = size;
            objects.Add(o);
            return o;
        }
        ChaseMotor Actor(Vector3 pos)
        {
            var o = new GameObject("physics actor"); objects.Add(o);
            o.transform.position = pos;
            o.AddComponent<Rigidbody>(); o.AddComponent<CapsuleCollider>();
            var visual = new GameObject("visual"); visual.transform.SetParent(o.transform, false);
            ChaseMotor motor = o.AddComponent<ChaseMotor>();
            motor.Initialize(new ChaseTuning(), visual.transform);
            Physics.SyncTransforms();
            return motor;
        }
        void Step(ChaseMotor motor, Vector2 move, bool jump = false, bool duck = false, bool leap = false, bool slide = false)
        {
            motor.Simulate(new ChaseAction(move, jump,leap,slide, duck), .02f, t);
            Physics.Simulate(.02f);
            t += .02f;
        }
        [UnityTest] public IEnumerator WallContactCannotRefreshGround()
        {
            Box("vertical wall", new Vector3(0f, 1.5f, 0f), new Vector3(.2f, 3f, 4f));
            var motor = Actor(new Vector3(.29f, 1.8f, 0f));
            Step(motor, Vector2.zero);
            Assert.IsFalse(motor.Grounded, "Side contact at height must not count as ground");
            yield return null;
        }
        [UnityTest] public IEnumerator ContinuousJumpButtonDoesNotAutoRepeat()
        {
            Box("floor", new Vector3(0,-.13f,0), new Vector3(10,.26f,10));
            var motor = Actor(new Vector3(0,.9f,0));
            int risingImpulses = 0;
            float oldVy = 0f;
            for (int i=0;i<100;i++)
            {
                float before = motor.Velocity.y;
                motor.Simulate(new ChaseAction(Vector2.zero,true,false,false,false),.02f,t);
                if (motor.Velocity.y > 4.5f && motor.Velocity.y > before + 3f) risingImpulses++;
                Physics.Simulate(.02f);
                t += .02f;

            }
            Assert.AreEqual(1,risingImpulses,"Held jump must be a single press");
            yield return null;
        }

        [UnityTest] public IEnumerator NortheastLowPassageCanBeCrouchedUnderFromEitherSide()
        {
            Box("floor",new Vector3(0,-.13f,0),new Vector3(12,.26f,12));
            foreach(float centerX in new[]{2.8f})
                foreach(float centerZ in new[]{3.55f})
                    foreach(int sign in new[]{-1,1})
                    {
                        var roof=Box("shelf",new Vector3(centerX,1.715f,centerZ),
                            new Vector3(2.2f,.15f,.55f));
                        var left=Box("post",new Vector3(centerX-1.01f,.6f,centerZ),
                            new Vector3(.18f,1.71f,.55f));
                        var right=Box("post",new Vector3(centerX+1.01f,.6f,centerZ),
                            new Vector3(.18f,1.71f,.55f));
                        var motor=Actor(new Vector3(centerX,.9f,centerZ-sign*1.25f));
                        bool passed=false;
                        for(int i=0;i<120;i++)
                        {
                            Step(motor,new Vector2(0,sign),false,true);
                            Assert.IsFalse(motor.Capsule.isTrigger);
                            Assert.LessOrEqual(motor.Capsule.height,1.551f);
                            if((motor.Body.position.z-centerZ)*sign>.83f) {passed=true;break;}
                        }
                        Assert.IsTrue(passed,"Crouch route: x="+centerX+" z="+centerZ+" sign="+sign);
                        // With the roof behind us, standing must become available.
                        for(int i=0;i<18;i++) Step(motor,new Vector2(0,sign),false,false);
                        Assert.AreEqual(1.8f,motor.Capsule.height,.001f);
                        Object.DestroyImmediate(motor.gameObject);
                        Object.DestroyImmediate(roof);Object.DestroyImmediate(left);Object.DestroyImmediate(right);
                    }
            yield return null;
        }





        [UnityTest] public IEnumerator StandingUnderShelfNeverPenetratesRoofOrOpponent()
        {
            Box("floor",new Vector3(0,-.13f,0),new Vector3(10,.26f,10));
            var roof=Box("roof",new Vector3(0,1.715f,0),new Vector3(2.2f,.15f,.55f));
            var motor=Actor(new Vector3(0,.9f,0));
            for(int i=0;i<10;i++)Step(motor,Vector2.zero,false,true);
            Assert.AreEqual(1.55f,motor.Capsule.height,.001f);
            for(int i=0;i<12;i++)Step(motor,Vector2.zero,false,false);
            Assert.AreEqual(1.55f,motor.Capsule.height,.001f,
                "Releasing crouch underneath roof must not expand the collider");
            Assert.IsFalse(motor.CanStand);
            Assert.IsTrue(roof.GetComponent<BoxCollider>().enabled);
            yield return null;
        }

        [UnityTest] public IEnumerator GroundedJumpCannotRefreshAgainstPillar()
        {
            Box("pillar",new Vector3(0,1.25f,0),new Vector3(.65f,2.5f,.65f));
            var motor=Actor(new Vector3(.59f,2f,0));
            for(int i=0;i<40;i++) Step(motor,new Vector2(-1f,0f),true);
            Assert.IsFalse(motor.Grounded);
            Assert.IsFalse(motor.CanJump);
            yield return null;
        }

        ChaseMatch MatchFixture()
        {
            Box("floor",new Vector3(0,-.13f,0),new Vector3(12,.26f,12));
            var camera=new GameObject("test camera");
            camera.tag="MainCamera";
            camera.AddComponent<Camera>();
            objects.Add(camera);
            var host=new GameObject("Chase physics match");
            objects.Add(host);
            ChaseMatch match=host.AddComponent<ChaseMatch>();
            return match;
        }

        void SetMatchActive(ChaseMatch match)
        {
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            typeof(ChaseMatch).GetField("<Phase>k__BackingField",flags)
                .SetValue(match,ChasePhase.Active);
        }

        [UnityTest] public IEnumerator RealColliderContactTagsOnce_AndCountdownCannotTag()
        {
            ChaseMatch match=MatchFixture();
            match.Chaser.Body.position=new Vector3(-.64f,.9f,0);
            match.Evader.Body.position=new Vector3(.64f,.9f,0);
            match.Chaser.Body.linearVelocity=Vector3.right*4f;
            match.Evader.Body.linearVelocity=Vector3.left*4f;
            Physics.SyncTransforms();
            for(int i=0;i<7;i++) Physics.Simulate(.02f);
            Assert.AreEqual(ChasePhase.Countdown,match.Phase);
            Assert.AreEqual(0,match.ChaserWins);
            match.Chaser.ResetMotor(new Vector3(-.7f,.9f,0));
            match.Evader.ResetMotor(new Vector3(.7f,.9f,0));
            Physics.SyncTransforms();
            SetMatchActive(match);
            match.Chaser.Body.linearVelocity=Vector3.right*4f;
            match.Evader.Body.linearVelocity=Vector3.left*4f;
            for(int i=0;i<12;i++) Physics.Simulate(.02f);
            Assert.AreEqual(ChaseRole.Chaser,match.Winner.Value);
            Assert.AreEqual(1,match.ChaserWins);
            for(int i=0;i<8;i++) Physics.Simulate(.02f);
            Assert.AreEqual(1,match.ChaserWins);
            yield return null;
        }

        [UnityTest] public IEnumerator SolidBarrierPreventsFalseTagEvenAtHighVelocity()
        {
            ChaseMatch match=MatchFixture();
            Box("separator",new Vector3(0,1,0),new Vector3(.2f,2f,3f));
            match.Chaser.ResetMotor(new Vector3(-1.1f,.9f,0));
            match.Evader.ResetMotor(new Vector3(1.1f,.9f,0));
            Physics.SyncTransforms();
            SetMatchActive(match);
            match.Chaser.Body.linearVelocity=Vector3.right*6f;
            match.Evader.Body.linearVelocity=Vector3.left*6f;
            for(int i=0;i<35;i++) Physics.Simulate(.02f);
            Assert.IsFalse(match.Winner.HasValue);
            Assert.Less(match.Chaser.Body.position.x,-.38f);
            Assert.Greater(match.Evader.Body.position.x,.38f);
            yield return null;
        }

        [UnityTest] public IEnumerator FinalPhysicsStepCollisionBeatsTimeout()
        {
            ChaseMatch match=MatchFixture();
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            SetMatchActive(match);
            typeof(ChaseMatch).GetField("<Remaining>k__BackingField",flags).SetValue(match,.01f);
            typeof(ChaseMatch).GetMethod("FixedUpdate",flags).Invoke(match,null);
            Assert.IsFalse(match.Winner.HasValue);
            match.Chaser.ResetMotor(new Vector3(-.69f,.9f,0));
            match.Evader.ResetMotor(new Vector3(.69f,.9f,0));
            match.Chaser.Body.linearVelocity=Vector3.right*5f;
            match.Evader.Body.linearVelocity=Vector3.left*5f;
            Physics.SyncTransforms();
            for(int i=0;i<6;i++) Physics.Simulate(.02f);
            Assert.AreEqual(ChaseRole.Chaser,match.Winner.Value,
                "Actual final-step collision must precede timeout");
            typeof(ChaseMatch).GetMethod("FixedUpdate",flags).Invoke(match,null);
            Assert.AreEqual(1,match.ChaserWins);
            Assert.AreEqual(0,match.EvaderWins);
            yield return null;
        }

        [UnityTest] public IEnumerator SensorChannelsDescribeShelfAndHurdleWithoutAllocatingPerSample()
        {
            Box("floor",new Vector3(0,-.13f,0),new Vector3(12,.26f,12));
            var roof=Box("roof",new Vector3(0,1.715f,1.2f),new Vector3(2.2f,.15f,.55f));
            roof.AddComponent<ChaseOverheadPassage>();
            var hurdle=Box("hurdle",new Vector3(1.2f,.175f,0),
                new Vector3(.2f,.35f,.7f));
            hurdle.AddComponent<ChaseHurdle>();
            var motor=Actor(new Vector3(0,.9f,0));
            float[] features=new float[ChaseObservationSensor.LocalSize];
            ChaseObservationSensor.FillLocal(motor,features);
            Assert.AreEqual(40,features.Length);
            foreach(float value in features)
                Assert.That(value,Is.InRange(0f,1f));
            // Direction 2 is +Z; index 0 is +X.
            Assert.Less(features[ChaseObservationSensor.Directions*2+2],1f,
                "Overhead geometry is observable without contextual traversal");
            Assert.Less(features[ChaseObservationSensor.Directions*3],1f,
                "Hurdle must be observable from its low geometry");
            Assert.AreEqual(1f,features[ChaseObservationSensor.Directions*2+4],.001f,
                "Opposite empty direction should show no overhead roof");
            yield return null;
        }
        sealed class StillSource : IChaseActionSource
        {
            public ChaseAction ReadAction(float clock) =>
                new ChaseAction(Vector2.zero,false,false,false,false);
            public void ResetSource() {}
        }

        void StressArena()
        {
            Box("North wall",new Vector3(0,1.3f,6.15f),new Vector3(12.6f,2.6f,.3f))
                .AddComponent<ChaseBlocker>();
            Box("South wall",new Vector3(0,1.3f,-6.15f),new Vector3(12.6f,2.6f,.3f))
                .AddComponent<ChaseBlocker>();
            Box("East wall",new Vector3(6.15f,1.3f,0),new Vector3(.3f,2.6f,12.6f))
                .AddComponent<ChaseBlocker>();
            Box("West wall",new Vector3(-6.15f,1.3f,0),new Vector3(.3f,2.6f,12.6f))
                .AddComponent<ChaseBlocker>();
            Box("Tall barrier A",new Vector3(-2.1f,1.15f,-.7f),
                new Vector3(.55f,2.3f,2.5f)).AddComponent<ChaseBlocker>();
            Box("Tall barrier B",new Vector3(2.15f,1.15f,.75f),
                new Vector3(.55f,2.3f,2.5f)).AddComponent<ChaseBlocker>();
            Box("Pillar A",new Vector3(-3.9f,1.15f,3.25f),
                new Vector3(.85f,2.3f,.85f)).AddComponent<ChaseBlocker>();
            Box("Pillar B",new Vector3(3.8f,1.15f,-3f),
                new Vector3(.85f,2.3f,.85f)).AddComponent<ChaseBlocker>();
            Box("Hurdle A",new Vector3(-.2f,.175f,-3.5f),
                new Vector3(1.9f,.35f,.2f)).AddComponent<ChaseHurdle>();
            Box("Hurdle B",new Vector3(.3f,.175f,3.5f),
                new Vector3(1.9f,.35f,.2f)).AddComponent<ChaseHurdle>();
            Vector3 low=new Vector3(2.8f,0,3.55f);
            Box("crouch roof",low+new Vector3(0,1.715f,0),
                new Vector3(2.2f,.15f,.55f)).AddComponent<ChaseOverheadPassage>();
            foreach(float x in new[]{-1.01f,1.01f})
                Box("crouch post",low+new Vector3(x,.855f,0),
                    new Vector3(.18f,1.71f,.55f)).AddComponent<ChaseBlocker>();
            Vector3 dive=new Vector3(-3.05f,0,-3.55f);
            Box("headfirst lower shelf",dive+new Vector3(0,.275f,0),
                new Vector3(2.2f,.55f,.6f)).AddComponent<ChaseBlocker>();
            Box("headfirst upper shelf",dive+new Vector3(0,2.025f,0),
                new Vector3(2.2f,.35f,.6f)).AddComponent<ChaseOverheadPassage>();
            foreach(float x in new[]{-1.01f,1.01f})
                Box("headfirst shelf support",dive+new Vector3(x,1.1f,0),
                    new Vector3(.18f,2.2f,.6f)).AddComponent<ChaseBlocker>();
        }

        [UnityTest] public IEnumerator OneHundredDeterministicMatchesCompleteWithoutInvalidStates()
        {
            StressArena();
            ChaseMatch match=MatchFixture();
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var begin=typeof(ChaseMatch).GetMethod("BeginRound",flags);
            var fixedUpdate=typeof(ChaseMatch).GetMethod("FixedUpdate",flags);
            var roundField=typeof(ChaseMatch).GetField("<RoundNumber>k__BackingField",flags);
            int tags=0,timeouts=0,stalls=0,nonFinite=0;
            float duration=0f;
            var stationary=new StillSource();
            for(int trial=0;trial<100;trial++)
            {
                roundField.SetValue(match,trial+1);
                begin.Invoke(match,new object[]{false});
                match.AssignActionSource(ChaseRole.Evader,stationary);
                SetMatchActive(match);
                Vector3 oldPosition=match.Chaser.Body.position;
                int stuckWindows=0;
                for(int step=0;step<1002;step++)
                {
                    fixedUpdate.Invoke(match,null);
                    Physics.Simulate(.02f);
                    Vector3 position=match.Chaser.Body.position;
                    Vector3 vel=match.Chaser.Body.linearVelocity;
                    if(float.IsNaN(position.x)||float.IsNaN(position.y)||
                        float.IsNaN(vel.x)||float.IsNaN(vel.y)||float.IsInfinity(vel.x))
                    {nonFinite++;break;}
                    if(step%100==99 && !match.Winner.HasValue)
                    {
                        if(Vector3.Distance(position,oldPosition)<.30f) stuckWindows++;
                        oldPosition=position;
                    }
                    if(match.Winner.HasValue) break;
                }
                stalls+=stuckWindows;
                duration+=20f-match.Remaining;
                if(match.Winner==ChaseRole.Chaser)tags++;
                else if(match.Winner==ChaseRole.Evader)timeouts++;
                else Assert.Fail("No winner in match "+trial);
                Assert.LessOrEqual(Mathf.Abs(match.Chaser.Body.position.x),5.9f);
                Assert.LessOrEqual(Mathf.Abs(match.Chaser.Body.position.z),5.9f);
            }
            Debug.Log("CHASE_STRESS_100 matches=100 tags="+tags+" timeouts="+timeouts+
                " avgSeconds="+(duration/100f).ToString("F2")+
                " stalled2secWindows="+stalls+" nonFinite="+nonFinite+
                " seedBase="+match.Tuning.spawnSeed);
            Assert.AreEqual(100,tags+timeouts);
            Assert.AreEqual(0,nonFinite);
            // Some timeouts are legitimate; a nonmoving bot is not.
            Assert.Less(stalls,100);
            yield return null;
        }
        [UnityTest] public IEnumerator SpeedAccelerationBrakingReversalAndAirSteeringStayBounded()
        {
            Box("floor",new Vector3(0,-.13f,0),new Vector3(20,.26f,20));
            var actor=Actor(new Vector3(0,.9f,0));
            for(int i=0;i<15;i++)Step(actor,Vector2.right);
            Assert.That(actor.HorizontalVelocity.x,Is.InRange(5.8f,6.01f));
            for(int i=0;i<5;i++)Step(actor,Vector2.left);
            Assert.Greater(actor.HorizontalVelocity.x,0f,
                "Five reversal steps may brake, not instantly flip");
            Assert.LessOrEqual(actor.HorizontalVelocity.magnitude,6.01f);
            for(int i=0;i<20;i++)Step(actor,Vector2.zero);
            Assert.That(actor.HorizontalVelocity.magnitude,Is.LessThan(.09f));
            for(int i=0;i<25;i++)Step(actor,new Vector2(1,1));
            Assert.LessOrEqual(actor.HorizontalVelocity.magnitude,6.01f);
            // Air steering cannot take a 6m/s direction reversal in one step.
            actor.Body.position=new Vector3(0,3f,0);
            actor.Body.linearVelocity=new Vector3(3,0,0);
            Physics.SyncTransforms();
            Step(actor,Vector2.left);
            Assert.Greater(actor.HorizontalVelocity.x,2.90f);
            yield return null;
        }

        [UnityTest] public IEnumerator SlidingUnderShelfCompletesWithCollision()
        {
            Box("floor",new Vector3(0,-.13f,0),new Vector3(12,.26f,12));
            var roof=Box("roof",new Vector3(0,1.715f,0),new Vector3(2.2f,.15f,.55f));
            var actor=Actor(new Vector3(0,.9f,-.93f));
            actor.Body.linearVelocity=Vector3.forward*6f;
            bool slid=false,cleared=false;
            for(int i=0;i<70;i++)
            {
                Step(actor,Vector2.up,false,false,false,i==0);
                if(actor.State==ChaseMovementState.Sliding)slid=true;
                if(actor.Body.position.z>.5f) {cleared=true;break;}
            }
            Assert.IsTrue(slid);
            Assert.IsTrue(cleared);
            Assert.IsFalse(actor.Capsule.isTrigger);
            Assert.IsTrue(roof.GetComponent<BoxCollider>().enabled);
            yield return null;
        }

        [UnityTest] public IEnumerator ShortJumpPressCanBeBufferedBeforeLanding()
        {
            Box("floor",new Vector3(0,-.13f,0),new Vector3(10,.26f,10));
            var actor=Actor(new Vector3(0,1.10f,0));
            actor.Body.linearVelocity=new Vector3(0,-2.4f,0);
            bool launched=false;
            for(int i=0;i<18;i++)
            {
                float previous=actor.Velocity.y;
                Step(actor,Vector2.zero,i==0);
                if(actor.Velocity.y>4.5f && actor.Velocity.y>previous+3f)
                    launched=true;
                if(launched)break;
            }
            Assert.IsTrue(launched,"Buffered press before contact must jump at landing");
            yield return null;
        }

        [UnityTest] public IEnumerator FixedCameraFramesEntireArenaAcrossAspectRatios()
        {
            ChaseMatch match=MatchFixture();
            Camera cam=match.ArenaCamera;
            var rig=cam.GetComponent<ChaseCameraRig>();
            foreach(float aspect in new[]{16f/9f,16f/10f,4f/3f,1f,.5f})
            {
                cam.aspect=aspect;
                rig.ResetView();
                foreach(float x in new[]{-6.3f,6.3f})
                    foreach(float z in new[]{-6.3f,6.3f})
                        foreach(float y in new[]{0f,2.7f})
                        {
                            Vector3 viewport=cam.WorldToViewportPoint(new Vector3(x,y,z));
                            Assert.Greater(viewport.z,0f);
                            Assert.That(viewport.x,Is.InRange(0f,1f),"aspect="+aspect);
                            Assert.That(viewport.y,Is.InRange(0f,1f),"aspect="+aspect);
                        }
                Assert.AreEqual(45f,cam.transform.rotation.eulerAngles.y,.01f);
                Assert.AreEqual(55f,cam.transform.rotation.eulerAngles.x,.01f);
                Assert.AreEqual(40f,cam.fieldOfView,.01f);
            }
            yield return null;
        }
        sealed class MovingSource : IChaseActionSource
        {
            readonly ChaseMotor actor;
            readonly ChaseMotor opponent;
            public int Steps;
            public int Seed;
            public MovingSource(ChaseMotor self,ChaseMotor other)
            {actor=self;opponent=other;}
            public void ResetSource() {Steps=0;}
            public ChaseAction ReadAction(float clock)
            {
                Steps++;
                Vector3 delta=actor.Body.position-opponent.Body.position;
                delta.y=0f;
                Vector3 tangent=new Vector3(-delta.z,0,delta.x);
                Vector3 target=delta.normalized+(Seed%2==0 ? .48f : -.48f)*tangent.normalized;
                Vector3 position=actor.Body.position;
                if(Mathf.Abs(position.x)>4.5f) target.x+=(position.x>0 ? -1.6f : 1.6f);
                if(Mathf.Abs(position.z)>4.5f) target.z+=(position.z>0 ? -1.6f : 1.6f);
                target.Normalize();
                return new ChaseAction(new Vector2(target.x,target.z),
                    Steps%83==13,false,false,Steps%143>=40 && Steps%143<=54);
            }
        }

        [UnityTest] public IEnumerator OneHundredMovingOpponentMatchesHaveValidOutcomes()
        {
            StressArena();
            ChaseMatch match=MatchFixture();
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var begin=typeof(ChaseMatch).GetMethod("BeginRound",flags);
            var fixedUpdate=typeof(ChaseMatch).GetMethod("FixedUpdate",flags);
            var roundField=typeof(ChaseMatch).GetField("<RoundNumber>k__BackingField",flags);
            var moving=new MovingSource(match.Evader,match.Chaser);
            int tags=0,timeouts=0,nonFinite=0,stalled=0;
            float seconds=0f;
            for(int trial=0;trial<100;trial++)
            {
                roundField.SetValue(match,trial+1);
                begin.Invoke(match,new object[]{false});
                moving.Seed=trial;
                match.AssignActionSource(ChaseRole.Evader,moving);
                SetMatchActive(match);
                Vector3 lastPosition=match.Chaser.Body.position;
                for(int step=0;step<1002;step++)
                {
                    fixedUpdate.Invoke(match,null);
                    Physics.Simulate(.02f);
                    Vector3 p=match.Chaser.Body.position;
                    Vector3 q=match.Evader.Body.position;
                    if(float.IsNaN(p.x)||float.IsNaN(q.x)||float.IsInfinity(p.z)||
                        float.IsNaN(match.Evader.Body.linearVelocity.y))
                    {nonFinite++;break;}
                    if(step%100==99 && !match.Winner.HasValue)
                    {
                        if(Vector3.Distance(p,lastPosition)<.30f)stalled++;
                        lastPosition=p;
                    }
                    if(match.Winner.HasValue)break;
                }
                seconds+=20f-match.Remaining;
                if(match.Winner==ChaseRole.Chaser)tags++;
                else if(match.Winner==ChaseRole.Evader)timeouts++;
                else Assert.Fail("No winner in moving match trial "+trial);
                Assert.LessOrEqual(Mathf.Abs(match.Chaser.Body.position.x),5.9f);
                Assert.LessOrEqual(Mathf.Abs(match.Evader.Body.position.z),5.9f);
            }
            Debug.Log("CHASE_STRESS_MOVING_100 matches=100 tags="+tags+
                " timeouts="+timeouts+" avgSeconds="+(seconds/100f).ToString("F2")+
                " stalled2secWindows="+stalled+" nonFinite="+nonFinite+" seedBase=2026");
            Assert.AreEqual(100,tags+timeouts);
            Assert.AreEqual(0,nonFinite);
            yield return null;
        }
        [UnityTest] public IEnumerator CoyoteWindowPermitsOneJumpThenExpires()
        {
            var platform=Box("platform",new Vector3(-1f,-.13f,0),
                new Vector3(2f,.26f,3f));
            var actor=Actor(new Vector3(-.3f,.9f,0));
            Step(actor,Vector2.zero);
            Assert.IsTrue(actor.Grounded);
            actor.Body.position=new Vector3(.8f,.9f,0);
            Physics.SyncTransforms();
            Step(actor,Vector2.zero,true);
            Assert.Greater(actor.Velocity.y,4.9f,
                "Jump just after leaving ledge should obey coyote time");
            Object.DestroyImmediate(actor.gameObject);
            var late=Actor(new Vector3(-.3f,.9f,0));
            Step(late,Vector2.zero);
            late.Body.position=new Vector3(.8f,.9f,0);
            Physics.SyncTransforms();
            for(int i=0;i<6;i++)Step(late,Vector2.zero);
            Step(late,Vector2.zero,true);
            Assert.Less(late.Velocity.y,2f,
                "Jump after coyote expiration should fail safely");
            yield return null;
        }

        [UnityTest] public IEnumerator CrouchStandingClearanceIncludesAnotherCapsule()
        {
            Box("floor",new Vector3(0,-.13f,0),new Vector3(10,.26f,10));
            var actor=Actor(new Vector3(0,.9f,0));
            Step(actor,Vector2.zero,false,true);
            Assert.AreEqual(1.55f,actor.Capsule.height,.001f);
            var other=Actor(new Vector3(0,1.7f,0));
            other.Body.isKinematic=true;
            Physics.SyncTransforms();
            Step(actor,Vector2.zero,false,false);
            Assert.AreEqual(1.55f,actor.Capsule.height,.001f,
                "Solid competitor overhead must prevent standing penetration");
            Object.DestroyImmediate(other.gameObject);
            Physics.SyncTransforms();
            for(int i=0;i<4;i++)Step(actor,Vector2.zero,false,false);
            Debug.Log("CHASE_CLEARANCE_TRACE pos="+actor.Body.position+
                " grounded="+actor.Grounded+" canStand="+actor.CanStand+
                " height="+actor.Capsule.height);
            Assert.AreEqual(1.8f,actor.Capsule.height,.001f);
            yield return null;
        }

        sealed class PursuitSource : IChaseActionSource
        {
            readonly ChaseMotor chaser,evader;
            public PursuitSource(ChaseMotor self,ChaseMotor other)
            {chaser=self;evader=other;}
            public void ResetSource(){}
            public ChaseAction ReadAction(float clock)
            {
                Vector3 delta=evader.Body.position-chaser.Body.position;
                delta.y=0f;
                delta.Normalize();
                return new ChaseAction(new Vector2(delta.x,delta.z),false,false,false,false);
            }
        }

        [UnityTest] public IEnumerator OneHundredEvaderRoleMatchesCompleteWithoutInvalidState()
        {
            StressArena();
            ChaseMatch match=MatchFixture();
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var begin=typeof(ChaseMatch).GetMethod("BeginRound",flags);
            var tick=typeof(ChaseMatch).GetMethod("FixedUpdate",flags);
            typeof(ChaseMatch).GetField("<HumanRole>k__BackingField",flags)
                .SetValue(match,ChaseRole.Chaser);
            var round=typeof(ChaseMatch).GetField("<RoundNumber>k__BackingField",flags);
            var pursuer=new PursuitSource(match.Chaser,match.Evader);
            int tags=0,timeouts=0,stalls=0;
            float seconds=0f;
            for(int trial=0;trial<100;trial++)
            {
                round.SetValue(match,trial+1);
                begin.Invoke(match,new object[]{false});
                match.AssignActionSource(ChaseRole.Chaser,pursuer);
                SetMatchActive(match);
                Vector3 previous=match.Evader.Body.position;
                for(int step=0;step<1002;step++)
                {
                    tick.Invoke(match,null);
                    Physics.Simulate(.02f);
                    if(step%100==99 && !match.Winner.HasValue)
                    {
                        if(Vector3.Distance(previous,match.Evader.Body.position)<.30f &&
                            Vector3.Distance(match.Evader.Body.position,match.Chaser.Body.position)<3.5f)
                            stalls++;
                        previous=match.Evader.Body.position;
                    }
                    if(match.Winner.HasValue)break;
                }
                if(match.Winner==ChaseRole.Chaser)tags++;
                else if(match.Winner==ChaseRole.Evader)timeouts++;
                else Assert.Fail("No winner with AI Evader, trial "+trial);
                seconds+=20f-match.Remaining;
            }
            Debug.Log("CHASE_STRESS_AI_EVADER_100 matches=100 tags="+tags+
                " timeouts="+timeouts+" avgSeconds="+(seconds/100f).ToString("F2")+
                " stalled2secWindows="+stalls+" seedBase=2026");
            Assert.AreEqual(100,tags+timeouts);
            Assert.Less(stalls,100,
                "Repeated immobility near a moving pursuer signals a navigation issue");
            yield return null;
        }
        [UnityTest] public IEnumerator ReusedPolicyBuffersRemainNormalizedAndSymmetric()
        {
            Box("floor",new Vector3(0,-.13f,0),new Vector3(12,.26f,12));
            var a=Actor(new Vector3(-2,.9f,-1));
            var b=Actor(new Vector3(2,.9f,1));
            float[] red=new float[ChaseObservationSensor.PolicySize];
            float[] blue=new float[ChaseObservationSensor.PolicySize];
            ChaseObservationSensor.FillPolicyVector(a,b,ChaseRole.Chaser,10f,red);
            ChaseObservationSensor.FillPolicyVector(b,a,ChaseRole.Evader,10f,blue);
            Assert.AreEqual(66,red.Length);
            for(int i=0;i<red.Length;i++)
            {
                Assert.IsFalse(float.IsNaN(red[i]) || float.IsInfinity(red[i]));
                Assert.That(red[i],Is.InRange(-1f,1f));
                Assert.That(blue[i],Is.InRange(-1f,1f));
            }
            for(int i=6;i<=11;i++)
                Assert.AreEqual(-red[i],blue[i],.001f);
            Assert.AreEqual(1f,red[17]);
            Assert.AreEqual(-1f,blue[17]);
            Assert.AreEqual(.5f,red[18]);
            yield return null;
        }
        [UnityTest] public IEnumerator PhysicalTagRemainsValidDuringHeadfirstSupermanDive()
        {
            ChaseMatch match=MatchFixture();
            SetMatchActive(match);
            match.Chaser.ResetMotor(new Vector3(0,.9f,-3f));
            match.Evader.ResetMotor(new Vector3(0,.9f,0));
            Physics.SyncTransforms();
            bool actuallyDove=false;
            for(int i=0;i<45;i++)
            {
                match.Chaser.Simulate(
                    new ChaseAction(Vector2.up,false,i==0,false,false),
                    .02f,10f+i*.02f);
                actuallyDove |= match.Chaser.DiveColliderActive;
                Physics.Simulate(.02f);
                if(match.Winner.HasValue)break;
            }
            Assert.IsTrue(actuallyDove,
                "Tag must occur with the actual long solid dive collider");
            Assert.AreEqual(ChaseRole.Chaser,match.Winner,
                "Head-first body collision must still count as an ordinary tag");
            yield return null;
        }

        [UnityTest] public IEnumerator NearOpponentDiveCausesOrdinaryChaserTag()
        {
            ChaseMatch match=MatchFixture();
            SetMatchActive(match);
            match.Chaser.ResetMotor(new Vector3(0,.9f,-.95f));
            match.Evader.ResetMotor(new Vector3(0,.9f,0));
            Physics.SyncTransforms();
            match.Chaser.Simulate(
                new ChaseAction(Vector2.up,false,true,false,false),.02f,10f);
            Assert.AreEqual(ChaseMovementState.Leaping,match.Chaser.State);
            Assert.IsTrue(match.Chaser.DiveCompactContact,
                "Full horizontal body must not spawn intersecting the Evader");
            bool collided=false;
            for(int i=0;i<18;i++)
            {
                Physics.Simulate(.02f);
                if(match.Winner.HasValue){collided=true;break;}
                match.Chaser.Simulate(new ChaseAction(Vector2.up,false,false,false,false),
                    .02f,10.02f+i*.02f);
            }
            Assert.IsTrue(collided,"Close dive should cause legitimate physical contact");
            Assert.AreEqual(ChaseRole.Chaser,match.Winner,
                "Chaser contact tags the Evader during a compact-body attempted dive");
            yield return null;
        }

        [UnityTest] public IEnumerator EvaderDivingIntoNearbyChaserIsAlsoTagged()
        {
            ChaseMatch match=MatchFixture();
            SetMatchActive(match);
            match.Chaser.ResetMotor(new Vector3(0,.9f,0));
            match.Evader.ResetMotor(new Vector3(0,.9f,-.95f));
            Physics.SyncTransforms();
            match.Evader.Simulate(
                new ChaseAction(Vector2.up,false,true,false,false),.02f,10f);
            Assert.AreEqual(ChaseMovementState.Leaping,match.Evader.State);
            Assert.IsTrue(match.Evader.DiveCompactContact);
            for(int i=0;i<18;i++)
            {
                Physics.Simulate(.02f);
                if(match.Winner.HasValue)break;
                match.Evader.Simulate(new ChaseAction(Vector2.up,false,false,false,false),
                    .02f,10.02f+i*.02f);
            }
            Assert.AreEqual(ChaseRole.Chaser,match.Winner,
                "Evader's compact physical dive cannot confer tag immunity");
            yield return null;
        }

        [UnityTest] public IEnumerator PhysicalTagRemainsValidDuringSlide()
        {
            ChaseMatch match=MatchFixture();
            SetMatchActive(match);
            match.Chaser.ResetMotor(new Vector3(-1.2f,.9f,0));
            match.Evader.ResetMotor(new Vector3(.35f,.9f,0));
            match.Chaser.Body.linearVelocity=Vector3.right*4.6f;
            Physics.SyncTransforms();
            bool sliding=false;
            for(int i=0;i<25;i++)
            {
                match.Chaser.Simulate(new ChaseAction(Vector2.right,false,false,i==0,false),.02f,10f+i*.02f);
                sliding |= match.Chaser.State==ChaseMovementState.Sliding;
                Physics.Simulate(.02f);
                if(match.Winner.HasValue)break;
            }
            Assert.IsTrue(sliding);
            Assert.AreEqual(ChaseRole.Chaser,match.Winner.Value);
            yield return null;
        }



        [UnityTest] public IEnumerator NortheastLowPassageSlidesTraverseInBothDirections()
        {
            Box("floor",new Vector3(0,-.13f,0),new Vector3(12,.26f,12));
            foreach(Vector3 center in new[]{new Vector3(2.8f,0,3.55f)})
                foreach(int sign in new[]{-1,1})
                {
                    var roof=Box("roof",center+new Vector3(0,1.715f,0),
                        new Vector3(2.2f,.15f,.55f));
                    foreach(float x in new[]{-1.01f,1.01f})
                        Box("post",center+new Vector3(x,.855f,0),
                            new Vector3(.18f,1.71f,.55f));
                    var actor=Actor(center+new Vector3(0,.9f,-sign*.93f));
                    actor.Body.linearVelocity=Vector3.forward*sign*6f;
                    bool slid=false,passed=false;
                    for(int i=0;i<70;i++)
                    {
                        Step(actor,new Vector2(0,sign),false,false,false,i==0);
                        slid |= actor.State==ChaseMovementState.Sliding;
                        if((actor.Body.position.z-center.z)*sign>.68f)
                        {passed=true;break;}
                    }
                    Assert.IsTrue(slid,"Slide entered");
                    Assert.IsTrue(passed,"Under-shelf slide completed at "+center+" direction "+sign);
                    Assert.IsFalse(actor.Capsule.isTrigger);
                    Object.DestroyImmediate(actor.gameObject);
                    Object.DestroyImmediate(roof);
                }
            yield return null;
        }
    }
}
