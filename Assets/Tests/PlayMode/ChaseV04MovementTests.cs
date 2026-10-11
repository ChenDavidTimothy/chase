using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chase.Tests
{
    // Isolated, repeatable Physics.Simulate gym. No animation or presentation dependency.
    public sealed class ChaseV04MovementTests
    {
        readonly List<GameObject> objects=new List<GameObject>();
        SimulationMode original;
        float clock;
        const float Dt=.02f;
        [SetUp] public void Setup()
        {
            original=Physics.simulationMode;
            Physics.simulationMode=SimulationMode.Script;
            clock=10f;
        }
        [TearDown] public void Cleanup()
        {
            Physics.simulationMode=original;
            foreach(var o in objects)if(o!=null)UnityEngine.Object.DestroyImmediate(o);
            objects.Clear();
        }
        GameObject Cube(string name,Vector3 center,Vector3 size)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name=name;o.transform.position=center;o.transform.localScale=size;
            objects.Add(o);return o;
        }
        void Floor()=>Cube("calibration floor",new Vector3(0,-.1f,0),
            new Vector3(200,.2f,200));
        ChaseMotor Actor(Vector3? position=null,ChaseTuning tuning=null)
        {
            var obj=new GameObject("calibration actor");objects.Add(obj);
            obj.transform.position=position??new Vector3(0,.9f,0);
            obj.AddComponent<Rigidbody>();obj.AddComponent<CapsuleCollider>();
            var vis=new GameObject("nonphysical visual");vis.transform.SetParent(obj.transform,false);
            var m=obj.AddComponent<ChaseMotor>();m.Initialize(tuning??new ChaseTuning(),vis.transform);
            Physics.SyncTransforms();return m;
        }
        void Step(ChaseMotor m,Vector2 direction=default,
            bool jump=false,bool leap=false,bool slide=false,bool crouch=false)
        {
            m.Simulate(new ChaseAction(direction,jump,leap,slide,crouch),Dt,clock);
            Physics.Simulate(Dt);clock+=Dt;
        }
        [UnityTest] public IEnumerator DivePhysicsTraceAndLanding()
        {
            Floor();var m=Actor();
            var log=new List<string>{"step,y,z,vy,vz,state,ground,direction,centerY,height,radius"};
            for(int i=0;i<130;i++)
            {
                Step(m,Vector2.zero,false,i==0);
                if(i%2==0 || m.State!=ChaseMovementState.Leaping)
                    log.Add(string.Join(",",i,m.Body.position.y.ToString("F3"),
                        m.Body.position.z.ToString("F3"),m.Velocity.y.ToString("F3"),
                        m.Velocity.z.ToString("F3"),m.State,m.Grounded,m.Capsule.direction,
                        m.Capsule.center.y.ToString("F3"),m.Capsule.height.ToString("F3"),
                        m.Capsule.radius.ToString("F3")));
            }
            System.IO.File.WriteAllLines("/tmp/chase-v05-dive-physics-trace.csv",log);
            Assert.AreNotEqual(ChaseMovementState.Leaping,m.State,"Diver must land on solid floor");
            Assert.AreEqual(ChaseMovementState.Standing,m.State,
                "Dive must fully recover and restore the upright collider");
            yield return null;
        }

        [UnityTest] public IEnumerator ExplicitInputsHavePriorityAndNeverBecomeVaults()
        {
            Floor();var m=Actor();
            Step(m,Vector2.up,true,true,true,false);
            Assert.AreEqual(ChaseMovementState.Jumping,m.State);
            Assert.That(m.Velocity.y,Is.GreaterThan(5f));
            m.ResetMotor(new Vector3(0,.9f,0));Physics.SyncTransforms();
            Step(m,Vector2.zero,false,true,true,false);
            Assert.AreEqual(ChaseMovementState.Leaping,m.State);
            Assert.That(m.HorizontalVelocity.magnitude,Is.EqualTo(9f).Within(.1f));
            m.ResetMotor(new Vector3(0,.9f,0));Physics.SyncTransforms();
            Step(m,Vector2.zero,false,false,true,true);
            Assert.AreEqual(ChaseMovementState.Sliding,m.State);
            Assert.That(m.HorizontalVelocity.magnitude,Is.LessThan(.02f));
            m.ResetMotor(new Vector3(0,.9f,0));Physics.SyncTransforms();
            Step(m,Vector2.zero,true,false,false,true);
            Assert.AreEqual(ChaseMovementState.Crouching,m.State);
            Assert.That(m.Velocity.y,Is.LessThan(1f));
            yield return null;
        }
        [UnityTest] public IEnumerator LeapCannotBeSteeredOrRepropelledAgainstWall()
        {
            Floor();
            Cube("blocking wall",new Vector3(0,1.2f,2.40f),new Vector3(3,2.4f,.20f));
            var m=Actor();
            Step(m,Vector2.zero,false,true);
            Assert.AreEqual(ChaseMovementState.Leaping,m.State);
            float initial=m.Velocity.z;
            float largestZ=0,postHitMax=0;
            for(int i=0;i<35;i++)
            {
                Step(m,Vector2.right,false,true);
                largestZ=Mathf.Max(largestZ,m.Body.position.z);
                if(i>9)postHitMax=Mathf.Max(postHitMax,m.Velocity.z);
            }
            Assert.That(largestZ,Is.LessThan(1.13f),"Long solid projectile body must stop at wall");
            Assert.That(postHitMax,Is.LessThan(initial*.7f),"No impulse replenishment");
            Assert.IsFalse(m.Body.isKinematic);
            Assert.IsFalse(m.Capsule.isTrigger);
            yield return null;
        }
        [UnityTest] public IEnumerator LeapAimsAtCurrentKeyPressThenLocksDirection()
        {
            Floor();var m=Actor();
            for(int i=0;i<28;i++)Step(m,Vector2.right);
            Assert.That(m.FacingDirection.x,Is.GreaterThan(.97f));
            Step(m,Vector2.left,false,true);
            Assert.AreEqual(ChaseMovementState.Leaping,m.State);
            Assert.That(m.HorizontalVelocity.x,Is.LessThan(-8.5f));
            Assert.That(Mathf.Abs(m.HorizontalVelocity.z),Is.LessThan(.5f));
            Step(m,Vector2.right,false,false);
            Assert.That(m.HorizontalVelocity.x,Is.LessThan(-8.5f),
                "Once launched, right input must not reverse the projectile");
            yield return null;
        }

        [UnityTest] public IEnumerator SlideIsMomentumOnlyWithMandatoryRecovery()
        {
            Floor();
            foreach(float speed in new[]{0f,2f,4f,6f})
            {
                var m=Actor();
                m.Body.linearVelocity=Vector3.forward*speed;
                Vector3 origin=m.Body.position;
                Step(m,Vector2.up,false,false,true);
                Assert.AreEqual(ChaseMovementState.Sliding,m.State);
                Assert.That(m.HorizontalVelocity.magnitude,Is.LessThanOrEqualTo(speed+.01f));
                for(int i=0;i<23;i++)Step(m,Vector2.up,false,false,true);
                float distance=m.Body.position.z-origin.z;
                Assert.That(distance,Is.GreaterThanOrEqualTo(-.02f));
                Assert.That(distance,Is.LessThanOrEqualTo(1.96f));
                if(speed==0)Assert.Less(distance,.04f);
                Assert.AreEqual(ChaseMovementState.SlideRecovery,m.State);
                Assert.IsFalse(m.CanSlide);
                for(int i=0;i<8;i++)Step(m,Vector2.zero,false,false,true);
                Assert.IsFalse(m.CanSlide);
                for(int i=0;i<4;i++)Step(m,Vector2.zero);
                Assert.AreNotEqual(ChaseMovementState.SlideRecovery,m.State);
                UnityEngine.Object.DestroyImmediate(m.gameObject);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator CrouchAndSlideNeverExpandThroughSolidCanopy()
        {
            Floor();
            var roof=Cube("low passage canopy",new Vector3(0,1.715f,0),
                new Vector3(2.2f,.15f,.60f));
            roof.AddComponent<ChaseOverheadPassage>();
            var m=Actor(new Vector3(0,.9f,-1.1f));
            bool crossed=false;
            for(int i=0;i<100;i++)
            {
                Step(m,Vector2.up,false,false,false,true);
                if(m.Body.position.z>.65f){crossed=true;break;}
            }
            Assert.IsTrue(crossed);
            Assert.That(m.Capsule.height,Is.EqualTo(1.55f).Within(.001f));
            m.Body.position=new Vector3(0,.9f,0);Physics.SyncTransforms();
            for(int i=0;i<10;i++)Step(m,Vector2.zero,false,false,false,true);
            for(int i=0;i<8;i++)Step(m);
            Assert.IsFalse(m.CanStand);
            Assert.That(m.Capsule.height,Is.EqualTo(1.55f).Within(.001f));
            m.Body.position=new Vector3(0,.9f,-2f);Physics.SyncTransforms();
            for(int i=0;i<5;i++)Step(m);
            Assert.IsTrue(m.CanStand);
            Assert.That(m.Capsule.height,Is.EqualTo(1.8f).Within(.001f));
            yield return null;
        }
        [UnityTest] public IEnumerator HoldingLeapCannotRepeatAndRecoveryCannotBeSkipped()
        {
            Floor();var m=Actor();
            Step(m,Vector2.up,false,true);
            int bursts=1;
            bool sawRecovery=false;float minRecovery=1f;
            for(int i=0;i<140;i++)
            {
                float before=m.HorizontalVelocity.magnitude;
                Step(m,i%2==0?Vector2.right:Vector2.left,false,true,true);
                if(m.State==ChaseMovementState.LeapRecovery)
                {sawRecovery=true;minRecovery=Mathf.Min(minRecovery,m.RecoveryRemaining);}
                if(m.State==ChaseMovementState.Leaping && before<4f &&
                    m.HorizontalVelocity.magnitude>8.5f)bursts++;
            }
            Assert.IsTrue(sawRecovery);
            Assert.AreEqual(1,bursts);
            Assert.That(minRecovery,Is.LessThan(.12f));
            yield return null;
        }
        [UnityTest] public IEnumerator JumpHeightIsNotAffectedByNearbySpecialGeometry()
        {
            Floor();
            var beside=Cube("old shelf geometry, ordinary collision",
                new Vector3(2f,1.715f,.8f),new Vector3(.15f,.15f,.5f));
            beside.AddComponent<ChaseOverheadPassage>();
            var a=Actor(new Vector3(1.2f,.9f,0));
            var b=Actor(new Vector3(-5f,.9f,0));
            // Compare identical launches without collision interference.
            Step(a,Vector2.zero,true);
            Step(b,Vector2.zero,true);
            Assert.That(a.Velocity.y,Is.EqualTo(b.Velocity.y).Within(.001f));
            Assert.AreEqual(ChaseMovementState.Jumping,a.State);
            Assert.AreEqual(ChaseMovementState.Jumping,b.State);
            yield return null;
        }
        [UnityTest] public IEnumerator RealPhysicsCalibrationOutputsMeasurements()
        {
            Floor();
            var lines=new List<string>{"metric\tvalue\tunit"};
            Action<string,float,string> add=(k,v,u)=>lines.Add(k+"\t"+v.ToString("F5",
                System.Globalization.CultureInfo.InvariantCulture)+"\t"+u);
            // Straight-line acceleration and braking.
            var run=Actor();
            float acc0=0,top=0,accelTime=0;
            for(int i=0;i<60;i++)
            {
                float old=run.Velocity.z;
                Step(run,Vector2.up);
                if(i==0)acc0=(run.Velocity.z-old)/Dt;
                if(accelTime==0 && run.HorizontalVelocity.magnitude>=5.99f)accelTime=(i+1)*Dt;
            }
            top=run.HorizontalVelocity.magnitude;
            add("run.top_speed",top,"m/s");add("run.acceleration",acc0,"m/s2");
            add("run.time_to_top",accelTime,"s");
            float start=run.Body.position.z,vold=run.Velocity.z;
            Step(run);add("run.braking", (vold-run.Velocity.z)/Dt,"m/s2");
            for(int i=0;i<25;i++)Step(run);
            add("run.braking_distance",run.Body.position.z-start,"m");
            run.Body.linearVelocity=Vector3.forward*6f;
            float reverse=0;
            for(int i=0;i<70;i++)
            {
                Step(run,Vector2.down);
                if(run.Velocity.z<=-5.99f){reverse=(i+1)*Dt;break;}
            }
            add("run.reversal_time",reverse,"s");
            // Measured unobstructed arc, from real dynamic Rigidbody positions.
            var jump=Actor(new Vector3(-10,.9f,0));
            float y0=jump.Body.position.y,apex=0,apexTime=0,airtime=0;
            Step(jump,Vector2.zero,true);
            add("jump.launch_vertical",jump.Velocity.y+22f*Dt,"m/s");
            for(int i=0;i<85;i++)
            {
                Step(jump);
                float dy=jump.Body.position.y-y0;
                if(dy>apex){apex=dy;apexTime=(i+2)*Dt;}
                if(i>3 && jump.Grounded){airtime=(i+2)*Dt;break;}
            }
            add("jump.apex",apex,"m");add("jump.apex_time",apexTime,"s");
            add("jump.airtime",airtime,"s");
            add("jump.stationary_distance",Mathf.Abs(jump.Body.position.z),"m");
            // Running jump. Motor starts at 6 m/s, retains physical horizontal momentum.
            var rj=Actor(new Vector3(-14,.9f,0));
            rj.Body.linearVelocity=Vector3.forward*6f;
            float z0=rj.Body.position.z;
            Step(rj,Vector2.up,true);
            for(int i=0;i<85;i++)
            {Step(rj,Vector2.up);if(i>3 && rj.Grounded)break;}
            add("jump.running_distance",rj.Body.position.z-z0,"m");
            // Leap from rest and its contact-driven recovery.
            var leap=Actor(new Vector3(10,.9f,0));
            float leapY=leap.Body.position.y;
            Step(leap,Vector2.zero,false,true);
            add("leap.launch_horizontal",leap.HorizontalVelocity.magnitude,"m/s");
            add("leap.launch_vertical",leap.Velocity.y+22f*Dt,"m/s");
            float lapex=0,la=0,lduration=0,recoveryElapsed=0;
            for(int i=0;i<90;i++)
            {
                Step(leap);
                if(leap.Body.position.y-leapY>lapex)
                {lapex=leap.Body.position.y-leapY;la=(i+2)*Dt;}
                if(leap.State==ChaseMovementState.LeapRecovery)
                {lduration=(i+2)*Dt;break;}
            }
            add("leap.apex",lapex,"m");add("leap.apex_time",la,"s");
            add("leap.airtime_to_recovery",lduration,"s");
            add("leap.distance_to_recovery",leap.Body.position.z,"m");
            for(int i=0;i<30;i++)
            {
                if(leap.State!=ChaseMovementState.LeapRecovery)break;
                Step(leap);
                recoveryElapsed+=Dt;
            }
            add("leap.recovery",recoveryElapsed,"s");
            for(float speed=0;speed<=6;speed+=2)
            {
                var m=Actor(new Vector3(18+speed,.9f,0));
                m.Body.linearVelocity=Vector3.forward*speed;
                float z=m.Body.position.z;
                Step(m,Vector2.zero,false,false,true);
                for(int i=0;i<22;i++)Step(m);
                add("slide.distance_"+speed.ToString("F0"),m.Body.position.z-z,"m");
            }
            var crouch=Actor(new Vector3(-18,.9f,0));
            for(int i=0;i<40;i++)Step(crouch,Vector2.up,false,false,false,true);
            add("crouch.speed",crouch.HorizontalVelocity.magnitude,"m/s");
            add("crouch.collider_height",crouch.Capsule.height,"m");
            add("slide.collider_height",new ChaseTuning().slideBodyHeight,"m");
            add("slide.duration",new ChaseTuning().slideDuration,"s");
            add("slide.recovery",new ChaseTuning().slideRecovery,"s");
            // Comparison on identical open lanes over 10 s.
            var runner=Actor(new Vector3(-25,.9f,0));
            var leaper=Actor(new Vector3(25,.9f,0));
            float rStart=runner.Body.position.z,lStart=leaper.Body.position.z;
            bool lastLeap=false;int tries=0;
            for(int i=0;i<500;i++)
            {
                runner.Simulate(new ChaseAction(Vector2.up,false,false,false,false),Dt,clock);
                bool want=(i==25 || (i>25 && i%3==0 && leaper.CanLeap &&
                    leaper.HorizontalVelocity.magnitude>=5f));
                leaper.Simulate(new ChaseAction(Vector2.up,false,want,false,false),Dt,clock);
                Physics.Simulate(Dt);clock+=Dt;
                if(want)tries++;
            }
            add("run.ten_second_distance",runner.Body.position.z-rStart,"m");
            add("leap.ten_second_distance",leaper.Body.position.z-lStart,"m");
            add("leap.attempts",tries,"count");
            UnityEngine.Object.DestroyImmediate(runner.gameObject);
            UnityEngine.Object.DestroyImmediate(leaper.gameObject);
            foreach(float recovery in new[]{.35f,.45f,.50f,.55f,.60f})
            {
                var settings=new ChaseTuning();settings.leapRecovery=recovery;
                var test=Actor(new Vector3(30+recovery,.9f,0),settings);
                float from=test.Body.position.z;int attempts=0;
                for(int i=0;i<500;i++)
                {
                    bool request=i==25 || (i>25 && i%3==0 && test.CanLeap &&
                        test.HorizontalVelocity.magnitude>=5f);
                    test.Simulate(new ChaseAction(Vector2.up,false,request,false,false),Dt,clock);
                    Physics.Simulate(Dt);clock+=Dt;
                    if(request)attempts++;
                }
                add("leap.ten_second_recovery_"+Mathf.RoundToInt(recovery*100),
                    test.Body.position.z-from,"m");
                add("leap.attempts_recovery_"+Mathf.RoundToInt(recovery*100),attempts,"count");
                UnityEngine.Object.DestroyImmediate(test.gameObject);
            }
            Directory.CreateDirectory("/tmp");
            File.WriteAllLines("/tmp/chase-v04-calibration.tsv",lines);
            foreach(var line in lines)Debug.Log("CHASE_V04_CALIBRATION "+line);
            Assert.That(top,Is.EqualTo(6f).Within(.02f));
            Assert.That(apex,Is.InRange(.72f,.95f));
            Assert.That(lapex,Is.InRange(.51f,.61f),
                "Superman dive is a higher, committed head-first launch");
            Assert.That(leap.HorizontalVelocity.magnitude,Is.LessThanOrEqualTo(9.01f));
            yield return null;
        }
    }
}
