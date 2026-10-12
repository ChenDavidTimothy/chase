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
        public IEnumerator OneRoundedCapsuleFollowsSupermanHeadingAndNeverBecomesTrigger()
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
                bool horizontal=m.DiveColliderActive;
                Assert.IsTrue(m.Capsule.enabled,
                    "The same solid capsule must remain enabled throughout");
                Assert.IsFalse(m.Capsule.isTrigger);
                Assert.IsFalse(m.Body.isKinematic);
                if(horizontal)
                {
                    sawBox=true;
                    Assert.IsTrue(m.State==ChaseMovementState.Leaping ||
                        m.State==ChaseMovementState.DiveProne ||
                        m.State==ChaseMovementState.LeapRecovery);
                    Vector3 longAxis=m.Body.rotation*Vector3.forward;
                    Assert.That(Vector3.Dot(longAxis,Vector3.right),Is.GreaterThan(.99f));
                    Assert.That(m.Capsule.radius,Is.GreaterThan(.40f));
                    Assert.That(m.Capsule.height,Is.GreaterThan(1.8f));
                    Assert.AreEqual(2,m.Capsule.direction,
                        "Horizontal capsule must lie along the dive heading");
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
        public IEnumerator RoundedDiveUsesOneAlwaysSolidCapsuleWithoutExtraDiveCollider()
        {
            Floor();
            var m=Actor(new Vector3(0,.9f,0));
            Assert.AreEqual(1,m.GetComponents<CapsuleCollider>().Length,
                "Do not introduce another capsule for recovery");
            Assert.AreEqual(1,m.GetComponents<BoxCollider>().Length,
                "The only box belongs to the independent football slide");
            Step(m,Vector2.up,false,true);
            Assert.IsTrue(m.DiveColliderActive);
            Assert.AreEqual(2,m.Capsule.direction);
            Assert.That(m.Capsule.height,Is.EqualTo(1.95f).Within(.001f));
            Assert.That(m.Capsule.radius,Is.EqualTo(.43f).Within(.001f));
            bool sawImpact=false,sawRecovery=false;
            for(int frame=0;frame<160;frame++)
            {
                Step(m);
                Assert.IsTrue(m.Capsule.enabled,
                    "Dive never disables the only solid capsule");
                Assert.IsFalse(m.Capsule.isTrigger);
                Assert.IsFalse(m.SlideBox.enabled,
                    "Football slide must not be used as a hidden dive body");
                Assert.AreEqual(1,
                    (m.Capsule.enabled?1:0)+(m.SlideBox.enabled?1:0),
                    "No union, no gap, and no extra collision body");
                sawImpact|=m.State==ChaseMovementState.DiveProne;
                sawRecovery|=m.State==ChaseMovementState.LeapRecovery;
                if(m.State==ChaseMovementState.LeapRecovery)
                {
                    Assert.AreEqual(1,m.Capsule.direction,
                        "The only collider becomes crouched before get-up");
                    Assert.That(m.Capsule.height,
                        Is.EqualTo(m.Settings.crouchingHeight).Within(.001f));
                }
            }
            Assert.IsTrue(sawImpact && sawRecovery);
            Assert.AreEqual(ChaseMovementState.Standing,m.State);
            Assert.IsFalse(m.DiveColliderActive);
            Assert.AreEqual(1,m.Capsule.direction);
            Assert.That(m.Capsule.height,
                Is.EqualTo(m.Settings.standingHeight).Within(.001f));
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
            Assert.IsTrue(cleared,"A correctly timed dive must reach the far side; actor="+m.Body.position+" state="+m.State+" box="+m.DiveColliderActive);
            Assert.IsTrue(everProne,"Landing must occur through real ground collision");
            Assert.AreEqual(ChaseMovementState.Standing,m.State);
            yield return null;
        }
        [UnityTest]
        public IEnumerator GymShelfEdgeHitCannotLeaveDiverInLeapingForever()
        {
            // Recreate the user's actual gym: stand at z=1, sail towards
            // lower shelf at z=4 with a .55m sill / 1.85m overhead.
            // The long body can rest on a shelf's leading edge while its
            // center-point-only ground ray reports no floor beneath it.
            Floor();
            Shelf(4f,.55f,1.85f,.6f);
            var m=Actor(new Vector3(0,.9f,1f));
            Step(m,Vector2.zero,false,true);
            bool sawImpact=false,sawRecovery=false;
            for(int i=0;i<350;i++)
            {
                Step(m);
                sawImpact|=m.State==ChaseMovementState.DiveProne;
                sawRecovery|=m.State==ChaseMovementState.LeapRecovery;
            }
            Assert.IsTrue(sawImpact && sawRecovery,
                "Stalled gym shelf dive never recovered. State="+m.State+
                " position="+m.Body.position+" velocity="+m.Velocity+
                " grounded="+m.Grounded+" forwardImpact="+m.DiveFlightEnded+
                " box="+m.DiveColliderActive);
            Assert.AreNotEqual(ChaseMovementState.Leaping,m.State,
                "A zero-velocity dive must not persist indefinitely");
            yield return null;
        }

        [UnityTest]
        public IEnumerator VariedShelfApproachesAlwaysExitProneWithoutPlayerIntervention()
        {
            // Regression for the observed Movement Gym freeze: sometimes
            // the horizontal box is edge-supported near the shelf's lip,
            // giving zero velocity with the ground probe under empty air.
            // All cases must return control automatically even with no keys.
            foreach(float startZ in new[]{.7f,1f,1.3f,1.6f,1.9f,2.2f,2.4f})
            {
                Floor();
                Shelf(4f,.55f,1.85f,.6f);
                var m=Actor(new Vector3(0,.9f,startZ));
                Step(m,Vector2.zero,false,true);
                bool everProne=false,everRecovered=false;
                for(int i=0;i<360;i++)
                {
                    Step(m);
                    everProne|=m.State==ChaseMovementState.DiveProne;
                    everRecovered|=m.State==ChaseMovementState.LeapRecovery;
                }
                Assert.IsTrue(everProne&&everRecovered,
                    "One shelf approach never completed impact. startZ="+startZ+
                    " state="+m.State+" pos="+m.Body.position+
                    " velocity="+m.Velocity+" grounded="+m.Grounded);
                Assert.IsTrue(m.State==ChaseMovementState.Standing ||
                    m.State==ChaseMovementState.Airborne,
                    "Player must not remain in committed dive after 7.2s, " +
                    "even without pressing W or restarting. startZ="+startZ+
                    " state="+m.State+" pos="+m.Body.position);
                Assert.IsTrue(m.Capsule.enabled && !m.DiveColliderActive,
                    "Return to ordinary solid capsule after every impact");
                foreach(var obj in spawned)
                    if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);
                spawned.Clear();
            }
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
            Assert.AreEqual(2,m.Capsule.direction);
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
                Assert.AreEqual(2,m.Capsule.direction);
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
        public IEnumerator NearWallStillLaunchesAndPaysFullRecoveryWithoutIntersectingWall()
        {
            Floor();
            // Only 0.13m of clearance ahead of the standing capsule; a
            // 2.30m horizontal body certainly cannot be spawned here.
            var wall=Cube("solid wall immediately ahead",
                new Vector3(0,1.2f,.58f),new Vector3(5,2.4f,.2f));
            var m=Actor(new Vector3(0,.9f,0));
            Step(m,Vector2.zero,false,true);
            Assert.AreEqual(ChaseMovementState.Leaping,m.State,
                "Up always attempts the committed move even by a wall");
            Assert.IsTrue(m.DiveCompactContact,
                "When full body will not fit, preserve the existing solid capsule");
            Assert.IsTrue(m.Capsule.enabled);
            Assert.IsFalse(m.DiveColliderActive);
            Assert.That(m.Velocity.z,Is.GreaterThan(8.5f));
            Assert.That(m.Velocity.y,Is.GreaterThan(4f),
                "Blocked forward motion must not suppress the attempted launch");
            float maxForward=m.Body.position.z;
            bool sawContact=false,sawRecovery=false;
            for(int i=0;i<140;i++)
            {
                // Attempt adversarial steering only while actually airborne;
                // release inputs once recovery begins so a *new* legal slide
                // after standing does not confound the wall-impact test.
                if (m.State==ChaseMovementState.Leaping)
                    Step(m,Vector2.left,false,i%5==0,i%4==0,true);
                else Step(m);
                maxForward=Mathf.Max(maxForward,m.Body.position.z);
                Assert.IsTrue(m.Capsule.enabled);
                Assert.IsFalse(m.Capsule.isTrigger);
                Assert.IsFalse(m.Capsule.isTrigger);
                Assert.IsFalse(m.DiveColliderActive,
                    "Do not expand the horizontal box through nearby wall");
                if(i==4)
                {
                    sawContact=m.Velocity.z<.7f;
                    Assert.LessOrEqual(m.Velocity.y,0f,
                        "Contact must end the rising portion of wall impact");
                }
                sawRecovery |= m.State==ChaseMovementState.LeapRecovery;
            }
            // 0.48 is the front face of wall; capsule extends 0.35m.
            Assert.Less(maxForward,.21f,
                "PhysX must prevent the actual solid capsule intersecting the wall");
            Assert.IsTrue(sawContact,"Forward launch must physically strike the wall");
            Assert.IsTrue(sawRecovery,"Impact must incur a real committed recovery");
            Assert.AreEqual(ChaseMovementState.Standing,m.State,
                "Recovered wall impact eventually returns full movement control");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForwardWallImpactTerminatesProjectileForBothColliderShapes()
        {
            // Direct wall uses compact capsule; farther wall permits the
            // horizontal box at takeoff. BOTH stop the flight on contact,
            // fall under gravity, and pay the two recovery phases.
            foreach(float wallZ in new[]{.58f,2.40f})
            {
                Floor();
                Cube("solid contact wall",new Vector3(0,1.2f,wallZ),
                    new Vector3(5,2.4f,.2f));
                var m=Actor(new Vector3(0,.9f,0));
                Step(m,Vector2.up,false,true);
                bool compact=m.DiveCompactContact;
                Assert.AreEqual(wallZ<1f,compact,
                    "Near vs distant wall differs only in collision shape");
                bool hit=false,prone=false,getup=false;
                float maxForward=m.Body.position.z;
                for(int i=0;i<135;i++)
                {
                    // Airborne controls must be ignored; after recovery,
                    // release keys so a subsequent legitimate new jump does
                    // not count as a failure of the completed dive.
                    if(m.State==ChaseMovementState.Leaping)
                        Step(m,i%2==0?Vector2.left:Vector2.right,
                            i%4==0,false,i%5==0);
                    else Step(m);
                    maxForward=Mathf.Max(maxForward,m.Body.position.z);
                    if(m.DiveFlightEnded)
                    {
                        hit=true;
                        Assert.LessOrEqual(m.Velocity.y,.001f,
                            "Wall impact must terminate upward projectile flight");
                        Assert.Less(m.HorizontalVelocity.magnitude,.01f,
                            "No forward thrust after physical impact");
                    }
                    prone |= m.State==ChaseMovementState.DiveProne;
                    getup |= m.State==ChaseMovementState.LeapRecovery;
                    Assert.IsTrue(m.Capsule.enabled);
                }
                Assert.IsTrue(hit,"Wall contact should end both collider flights");
                Assert.IsTrue(prone && getup,
                    "wallZ="+wallZ+" compact="+compact+
                    " hit="+hit+" prone="+prone+" getup="+getup+
                    " state="+m.State+" pos="+m.Body.position+
                    " vel="+m.Velocity+" box="+m.DiveColliderActive);
                Assert.AreEqual(ChaseMovementState.Standing,m.State,
                    "wallZ="+wallZ+" position="+m.Body.position+
                    " grounded="+m.Grounded+" velocity="+m.Velocity+
                    " box="+m.DiveColliderActive+" capsule="+m.Capsule.enabled);
                Assert.Less(maxForward,wallZ,
                    "Never cross the solid wall on either approach");
                // Clean only the objects made by this sub-case before making
                // another floor. The fixture's TearDown also handles cleanup.
                foreach(var obj in spawned)
                    if(obj!=null)UnityEngine.Object.DestroyImmediate(obj);
                spawned.Clear();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LateralObstaclePreventsBoxButDoesNotCancelTheAttempt()
        {
            Floor();
            Cube("side guard that blocks extended shoulders",
                new Vector3(.57f,1.15f,1.5f),new Vector3(.35f,2.3f,8f));
            var m=Actor(new Vector3(0,.9f,0));
            Step(m,Vector2.zero,false,true);
            Assert.AreEqual(ChaseMovementState.Leaping,m.State);
            Assert.IsTrue(m.DiveCompactContact);
            Assert.That(m.Velocity.y,Is.GreaterThan(4f));
            bool recovered=false;
            for(int i=0;i<140;i++)
            {
                Step(m);
                Assert.IsTrue(m.Capsule.enabled && !m.DiveColliderActive);
                recovered|=m.State==ChaseMovementState.LeapRecovery;
            }
            Assert.IsTrue(recovered);
            Assert.AreEqual(ChaseMovementState.Standing,m.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CloseAthleteAllowsCommittedDiveAndNormalPhysicsContact()
        {
            Floor();
            var diver=Actor(new Vector3(0,.9f,-.95f));
            var target=Actor(new Vector3(0,.9f,0));
            // Other athlete is outside 0.7m combined standing capsules but
            // occupies the forward extent of the 2.3m Superman collider.
            Step(diver,Vector2.up,false,true);
            Assert.AreEqual(ChaseMovementState.Leaping,diver.State);
            Assert.IsTrue(diver.DiveCompactContact,
                "Never spawn long overlapping body into another athlete");
            Assert.IsTrue(diver.Capsule.enabled);
            bool impact=false,stoppedProjectile=false;
            for(int i=0;i<30;i++)
            {
                Step(diver,Vector2.left,false,false,true);
                if(diver.Velocity.z<1f)impact=true;
                if(diver.DiveFlightEnded)
                {
                    stoppedProjectile=true;
                    Assert.LessOrEqual(diver.Velocity.y,.001f,
                        "Opponent contact ends upward projectile flight too");
                    Assert.Less(diver.HorizontalVelocity.magnitude,.01f);
                }
            }
            Assert.IsTrue(impact && stoppedProjectile,
                "Opponent collision must end projectile, not only block translation");
            Assert.Less(diver.Body.position.z,target.Body.position.z,
                "Diver cannot phase through opponent collider");
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnderLowRoofCannotPerformAnUprightDiveTakeoff()
        {
            Floor();
            var m=Actor(new Vector3(0,.9f,0));
            // A 1.64m underside forces crouching for a 1.8m athlete.
            Cube("real overhead shelf underside 1.64m",
                new Vector3(0,1.84f,0),new Vector3(3,.4f,3));
            // Crouch collider safely before placing the roof nearby.
            Step(m,Vector2.zero,false,false,false,true);
            Physics.SyncTransforms();
            Assert.AreEqual(ChaseMovementState.Crouching,m.State);
            Step(m,Vector2.up,false,true,false,true);
            Assert.AreNotEqual(ChaseMovementState.Leaping,m.State,
                "Cannot launch a dive through an overhead roof while crouched");
            Assert.IsFalse(m.DiveColliderActive);
            Assert.Less(m.Velocity.y,1f,
                "Up under the ceiling must not inject upward launch velocity");
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
                Assert.IsTrue(m.Capsule.enabled);
            }
            Assert.Less(furthest,2.04f,
                "No solid body can pass a wall: after get-up the normal " +
                "0.35m-radius capsule may approach its front face at z=2.0");
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
                Assert.IsTrue(m.Capsule.enabled);
            }
            Assert.IsTrue(trappedLow,"Under a low roof, character stays prone");
            Assert.IsTrue(escapedLow,"After crawling out, character rises physically. " +
                "state="+m.State+" z="+m.Body.position.z+
                " y="+m.Body.position.y+" vy="+m.Velocity.y+
                " grounded="+m.Grounded+" prone="+m.DiveColliderActive+
                " recoveryLeft="+m.RecoveryRemaining);
            yield return null;
        }
    }
}
