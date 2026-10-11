using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chase.Tests
{
    // Dedicated v0.5 real Rigidbody/PhysX tests. Never disables collisions.
    public sealed class ChaseSupermanDiveTests
    {
        const float Dt=.02f;
        readonly List<GameObject> spawned=new List<GameObject>();
        SimulationMode former;
        float clock;
        [SetUp] public void Setup()
        {
            former=Physics.simulationMode;
            Physics.simulationMode=SimulationMode.Script;
            clock=20f;
        }
        [TearDown] public void Cleanup()
        {
            Physics.simulationMode=former;
            foreach(var o in spawned)
                if(o!=null)UnityEngine.Object.DestroyImmediate(o);
            spawned.Clear();
        }
        GameObject Cube(string name,Vector3 pos,Vector3 size)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name=name;obj.transform.position=pos;obj.transform.localScale=size;
            spawned.Add(obj);return obj;
        }
        void Floor() => Cube("physical ground",new Vector3(0,-.1f,0),
            new Vector3(36,.2f,36));
        ChaseMotor Actor(Vector3 pos,ChaseTuning tuning=null)
        {
            var go=new GameObject("athlete");spawned.Add(go);
            go.transform.position=pos;
            go.AddComponent<Rigidbody>();go.AddComponent<CapsuleCollider>();
            var visual=new GameObject("visual");visual.transform.SetParent(go.transform,false);
            var motor=go.AddComponent<ChaseMotor>();
            motor.Initialize(tuning??new ChaseTuning(),visual.transform);
            Physics.SyncTransforms();return motor;
        }
        void Shelf(float z=0f,float lowerTop=.55f,float upperBottom=1.85f,
            float depth=.6f)
        {
            Cube("solid lower shelf",new Vector3(0,lowerTop*.5f,z),
                new Vector3(2.2f,lowerTop,depth));
            Cube("solid upper shelf",new Vector3(0,upperBottom+.175f,z),
                new Vector3(2.2f,.35f,depth));
            foreach(float x in new[]{-1.01f,1.01f})
                Cube("solid support",new Vector3(x,1.1f,z),
                    new Vector3(.18f,2.2f,depth));
            Physics.SyncTransforms();
        }
        void Step(ChaseMotor motor,Vector2 move=default,bool jump=false,
            bool dive=false,bool slide=false,bool crouch=false)
        {
            motor.Simulate(new ChaseAction(move,jump,dive,slide,crouch),Dt,clock);
            Physics.Simulate(Dt);clock+=Dt;
        }
        [UnityTest]
        public IEnumerator FullBodyColliderFollowsSupermanHeadingAndNeverBecomesTrigger()
        {
            Floor();
            var m=Actor(new Vector3(0,.9f,0));
            for(int i=0;i<25;i++)Step(m,Vector2.right);
            Assert.That(m.FacingDirection.x,Is.GreaterThan(.97f));
            Step(m,Vector2.right,false,true);
            Assert.AreEqual(ChaseMovementState.Leaping,m.State);
            Assert.That(m.HorizontalVelocity.x,Is.EqualTo(9f).Within(.15f));
            bool sawBox=false,sawImpact=false,sawGetup=false;
            for(int i=0;i<120;i++)
            {
                Step(m,Vector2.left,false,true,true);
                bool capsule=m.Capsule.enabled,box=m.DiveBox.enabled;
                Assert.IsTrue(capsule||box,"At least one solid Collider must remain enabled");
                if (capsule&&box)
                    Assert.AreEqual(ChaseMovementState.LeapRecovery,m.State,
                        "Only the get-up animation may use the conservative collider union");
                Assert.IsFalse(m.Capsule.isTrigger);
                Assert.IsFalse(m.DiveBox.isTrigger);
                Assert.IsFalse(m.Body.isKinematic);
                if(box)
                {
                    sawBox=true;
                    Assert.IsTrue(m.State==ChaseMovementState.Leaping ||
                        m.State==ChaseMovementState.DiveProne ||
                        m.State==ChaseMovementState.LeapRecovery);
                    Vector3 longAxis=m.Body.rotation*Vector3.forward;
                    Assert.That(Vector3.Dot(longAxis,Vector3.right),Is.GreaterThan(.99f));
                    Assert.That(m.DiveBox.size.x,Is.GreaterThan(1f));
                    Assert.That(m.DiveBox.size.z,Is.GreaterThan(2f));
                }
                sawImpact|=m.State==ChaseMovementState.DiveProne;
                sawGetup|=m.State==ChaseMovementState.LeapRecovery;
            }
            Assert.IsTrue(sawBox&&sawImpact&&sawGetup,
                "Diver must become horizontal, contact ground chest-first, then get up");
            Assert.AreEqual(ChaseMovementState.Standing,m.State);
            Assert.IsFalse(m.DiveColliderActive);
            yield return null;
        }
        [UnityTest]
        public IEnumerator SupermanShelfSlotCanBeTraversedOnTimedTakeoff()
        {
            Floor();Shelf();
            var m=Actor(new Vector3(0,.9f,-3.0f));
            Step(m,Vector2.zero,false,true);
            float furthest=m.Body.position.z;
            bool entered=false,cleared=false,everProne=false;
            for(int i=0;i<145;i++)
            {
                Step(m,Vector2.up);
                furthest=Mathf.Max(furthest,m.Body.position.z);
                if(m.DiveColliderActive && m.Body.position.z>-1.1f &&
                    m.Body.position.z<1.1f)entered=true;
                if(m.Body.position.z>1.4f)cleared=true;
                everProne|=m.State==ChaseMovementState.DiveProne;
            }
            Assert.IsTrue(entered,"Actual horizontal collider must enter shelf slot");
            Assert.IsTrue(cleared,"A correctly timed dive must reach the far side; actor="+m.Body.position+" state="+m.State+" box="+m.DiveBox.enabled);
            Assert.IsTrue(everProne,"Landing must occur through real ground collision");
            Assert.AreEqual(ChaseMovementState.Standing,m.State);
            yield return null;
        }
        [UnityTest]
        public IEnumerator SimultaneousDirectionAndUpFromIdleUsesThatDirection()
        {
            Floor();
            var m=Actor(new Vector3(0,.9f,0));
            Assert.That(m.FacingDirection.z,Is.GreaterThan(.99f),
                "Initial idle faces +Z, not the player's requested right input");
            Step(m,Vector2.right,false,true);
            Assert.AreEqual(ChaseMovementState.Leaping,m.State);
            Assert.IsTrue(m.DiveColliderActive,
                "There must be no upright jump frame even for same-frame input");
            Assert.That(m.HorizontalVelocity.x,Is.EqualTo(9f).Within(.02f));
            Assert.That(Mathf.Abs(m.HorizontalVelocity.z),Is.LessThan(.01f));
            Assert.That(Vector3.Dot(m.Body.rotation*Vector3.forward,Vector3.right),
                Is.GreaterThan(.999f),
                "Launch body must align with simultaneous WASD intent");
            // Opposite intent midair has no authority over the projectile.
            for(int i=0;i<12;i++)
            {
                Step(m,Vector2.left,false,false,true);
                Assert.That(m.HorizontalVelocity.x,Is.EqualTo(9f).Within(.001f));
                Assert.That(Mathf.Abs(m.HorizontalVelocity.z),Is.LessThan(.001f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoldingWAndUpInstantlyLaunchesHeadfirstWithNoJumpPhase()
        {
            Floor();
            var m=Actor(new Vector3(0,.9f,0));
            for(int i=0;i<16;i++)Step(m,Vector2.up);
            Step(m,Vector2.up,false,true);
            Assert.AreEqual(ChaseMovementState.Leaping,m.State);
            Assert.IsTrue(m.DiveColliderActive,
                "The full-body solid collider must already be active on the launch frame");
            Assert.IsFalse(m.Capsule.enabled);
            Assert.That(m.Velocity.z,Is.EqualTo(9f).Within(.01f));
            Assert.That(m.Velocity.y,Is.GreaterThan(4.4f));
            Assert.That(Vector3.Dot(m.Body.rotation*Vector3.forward,Vector3.forward),
                Is.GreaterThan(.999f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator WASDJumpSlideSpamCannotSteerOrRepowerAirborneProjectile()
        {
            Floor();
            var m=Actor(new Vector3(0,.9f,0));
            Step(m,Vector2.up,false,true);
            Assert.AreEqual(ChaseMovementState.Leaping,m.State);
            Assert.IsTrue(m.DiveColliderActive);
            Vector3 launch=m.HorizontalVelocity;
            float previousHeight=m.Body.position.y;
            float maxHeight=previousHeight;
            int airFrames=0;
            for(int i=0;i<17;i++)
            {
                Vector2 adversarial = i%4==0?Vector2.right:
                    i%4==1?Vector2.left:i%4==2?Vector2.down:Vector2.up;
                Step(m,adversarial,i%2==0,i%3==0,i%4==0,i%2==1);
                Assert.AreEqual(ChaseMovementState.Leaping,m.State,
                    "Projectile must remain committed before physical contact");
                Assert.That(m.HorizontalVelocity.x,Is.EqualTo(launch.x).Within(.0005f));
                Assert.That(m.HorizontalVelocity.z,Is.EqualTo(launch.z).Within(.0005f));
                Assert.IsTrue(m.DiveColliderActive);
                Assert.IsFalse(m.Capsule.enabled);
                Assert.That(Vector3.Dot(m.Body.rotation*Vector3.forward,Vector3.forward),
                    Is.GreaterThan(.999f));
                Assert.That(m.Velocity.y,Is.LessThan(5.2f-i*.15f));
                maxHeight=Mathf.Max(maxHeight,m.Body.position.y);
                airFrames++;
            }
            Assert.Greater(airFrames,15);
            Assert.Greater(maxHeight,previousHeight+.15f,
                "The committed arc must rise from physically integrated gravity");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OrdinaryJumpAndPowerSlideCannotExploitTheSameShelfSlot()
        {
            Floor();Shelf();
            var jumper=Actor(new Vector3(0,.9f,-3f));
            var slider=Actor(new Vector3(0,.9f,-3f));
            // Keep the competitors well separated so they cannot collide.
            jumper.Body.position=new Vector3(-.1f,.9f,-3f);
            slider.Body.position=new Vector3(.1f,.9f,-3f);
            Physics.SyncTransforms();
            jumper.Body.linearVelocity=Vector3.forward*6f;
            slider.Body.linearVelocity=Vector3.forward*6f;
            for(int i=0;i<110;i++)
            {
                jumper.Simulate(new ChaseAction(Vector2.up,i==0,false,false,false),Dt,clock);
                slider.Simulate(new ChaseAction(Vector2.up,false,false,i==0,false),Dt,clock);
                Physics.Simulate(Dt);clock+=Dt;
            }
            Assert.Less(jumper.Body.position.z,1.0f,
                "Normal upright jump must be blocked by shelf geometry");
            Assert.Less(slider.Body.position.z,1.0f,
                "A grounded slide must strike the solid lower shelf");
            yield return null;
        }
        [UnityTest]
        public IEnumerator MissedSideLaneHitsSolidShelfSupport()
        {
            Floor();Shelf();
            var m=Actor(new Vector3(.82f,.9f,-3f));
            Step(m,Vector2.zero,false,true);
            float far=0;
            for(int i=0;i<120;i++)
            {Step(m,Vector2.up);far=Mathf.Max(far,m.Body.position.z);}
            Assert.Less(far,1.2f,"Wide dive collider cannot pass a side support");
            yield return null;
        }
        [UnityTest]
        public IEnumerator BlockedTakeoffIsRejectedBeforeAnyProjectileVelocity()
        {
            Floor();
            // Between 0.35m capsule and 0.60m box half-width: body must
            // remain upright rather than clipping through a broad side panel.
            Cube("long lateral guard blocking full-body shape",
                new Vector3(.82f,1.15f,1.5f),new Vector3(.52f,2.3f,8f));
            var m=Actor(new Vector3(0,.9f,0));
            float originalY = m.Body.position.y;
            Step(m,Vector2.zero,false,true);
            Assert.AreNotEqual(ChaseMovementState.Leaping,m.State,
                "No upright jump fallback when full-size launch is blocked");
            Assert.IsFalse(m.DiveColliderActive);
            Assert.Less(Mathf.Abs(m.Velocity.y),.5f,
                "A rejected dive must not create any vertical takeoff");
            Assert.Greater(m.DiveBlockedTimeLeft,.5f,
                "Physically blocked takeoffs need immediate, explicit feedback");
            bool remainedUpright=true;
            for(int i=0;i<110;i++)
            {
                Step(m);
                remainedUpright &= !m.DiveColliderActive;
                Assert.IsTrue(m.Capsule.enabled||m.DiveBox.enabled);
            }
            Assert.IsTrue(remainedUpright,
                "A blocked takeoff cannot morph later or launch vertically");
            Assert.AreEqual(ChaseMovementState.Standing,m.State,
                "A physically blocked maneuver must still finish recovery");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LongWallStopsDiveRatherThanDisablingContact()
        {
            Floor();
            Cube("solid broad wall",new Vector3(0,1.1f,2.4f),
                new Vector3(5f,2.2f,.2f));
            var m=Actor(new Vector3(0,.9f,0));
            Step(m,Vector2.zero,false,true);
            float furthest=0,postHitMax=0;
            for(int i=0;i<90;i++)
            {
                Step(m,Vector2.up);
                furthest=Mathf.Max(furthest,m.Body.position.z);
                if(i>15)postHitMax=Mathf.Max(postHitMax,m.Velocity.z);
                Assert.IsTrue(m.Capsule.enabled||m.DiveBox.enabled);
            }
            Assert.Less(furthest,2.0f,
                "A non-trigger 2.3m long dive body must stop before wall");
            Assert.Less(postHitMax,8f,"Collision must remove speed, no new impulse");
            yield return null;
        }
        [UnityTest]
        public IEnumerator GetupCannotExpandColliderThroughOverheadShelf()
        {
            Floor();
            var roof=Cube("low overhead after dive",new Vector3(0,1.45f,6.6f),
                new Vector3(2.6f,.22f,2.8f));
            var m=Actor(new Vector3(0,.9f,0));
            Step(m,Vector2.zero,false,true);
            bool trappedLow=false,escapedLow=false;
            for(int i=0;i<250;i++)
            {
                Step(m,Vector2.up);
                if(m.Body.position.z>5.2f && m.Body.position.z<8.0f &&
                    m.State==ChaseMovementState.DiveProne && m.DiveColliderActive)
                    trappedLow=true;
                if(m.Body.position.z>8.9f && !m.DiveColliderActive)
                    escapedLow=true;
                Assert.IsTrue(m.Capsule.enabled||m.DiveBox.enabled);
            }
            Assert.IsTrue(trappedLow,"Under a low roof, character stays prone");
            Assert.IsTrue(escapedLow,"After crawling out, character rises physically");
            yield return null;
        }
    }
}
