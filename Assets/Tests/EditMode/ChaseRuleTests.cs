using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chase.Tests
{
    public sealed class ChaseRuleTests
    {
        [Test]
        public void SharedAction_NormalizesDiagonal()
        {
            ChaseAction action = new ChaseAction(new Vector2(1f,1f),true,false,false,false);
            Assert.That(action.Move.magnitude,Is.EqualTo(1f).Within(.0001f));
            Assert.IsTrue(action.Jump);
        }
        [Test]
        public void EveryMatchSpawnHasAtLeastEightMetersOfInitialSeparation()
        {
            var field=typeof(ChaseMatch).GetField("SpawnPairs",
                BindingFlags.NonPublic|BindingFlags.Static);
            Assert.NotNull(field);
            var pairs=(Vector3[,])field.GetValue(null);
            for(int i=0;i<pairs.GetLength(0);i++)
            {
                Vector3 a=pairs[i,0],b=pairs[i,1];
                a.y=b.y=0f;
                Assert.That(Vector3.Distance(a,b),Is.GreaterThanOrEqualTo(8f));
                Assert.LessOrEqual(Mathf.Abs(a.x),5.25f);
                Assert.LessOrEqual(Mathf.Abs(a.z),5.25f);
                Assert.LessOrEqual(Mathf.Abs(b.x),5.25f);
                Assert.LessOrEqual(Mathf.Abs(b.z),5.25f);
            }
        }

        [Test]
        public void SharedConfiguration_MatchesSpecification()
        {
            var settings=new ChaseTuning();
            Assert.AreEqual(6f,settings.runSpeed);
            Assert.AreEqual(70f,settings.mass);
            Assert.AreEqual(.35f,settings.radius);
            Assert.AreEqual(1.55f,settings.crouchingHeight);
            Assert.AreEqual(1.55f,settings.slideHeight);
            Assert.AreEqual(.82f,settings.slideBodyHeight);
            Assert.Greater(settings.slideBodyLength,1.8f);
            Assert.AreEqual(9f,settings.leapHorizontalSpeed);
            Assert.AreEqual(20f,settings.roundDuration);
            Assert.AreEqual(3f,settings.countdown);
        }
        [Test]
        public void Countdown_RejectsTag_ActiveTagCountsOnce_RestartClearsWinner()
        {
            GameObject host=new GameObject("Test match");
            ChaseMatch match=null;
            try
            {
                match=host.AddComponent<ChaseMatch>();
                typeof(ChaseMatch).GetMethod("Awake",
                    BindingFlags.NonPublic|BindingFlags.Instance).Invoke(match,null);
                var chaser=match.Chaser.GetComponent<ChaseTagContact>();
                var evader=match.Evader.GetComponent<ChaseTagContact>();
                Assert.AreEqual(ChasePhase.Countdown,match.Phase);
                Assert.IsFalse(match.TryTag(chaser,evader));
                Assert.AreEqual(0,match.ChaserWins);
                typeof(ChaseMatch).GetField("<Phase>k__BackingField",
                    BindingFlags.NonPublic|BindingFlags.Instance).SetValue(match,ChasePhase.Active);
                Assert.IsTrue(match.TryTag(chaser,evader));
                Assert.AreEqual(ChaseRole.Chaser,match.Winner.Value);
                Assert.AreEqual(ChasePhase.Ended,match.Phase);
                Assert.AreEqual(1,match.ChaserWins);
                Assert.IsFalse(match.TryTag(chaser,evader));
                Assert.AreEqual(1,match.ChaserWins);
                typeof(ChaseMatch).GetMethod("BeginRound",
                    BindingFlags.NonPublic|BindingFlags.Instance).Invoke(match,new object[]{true});
                Assert.IsFalse(match.Winner.HasValue);
                Assert.AreEqual(ChasePhase.Countdown,match.Phase);
                Assert.AreEqual(20f,match.Remaining);
                Assert.AreEqual(1,match.ChaserWins);
            }
            finally
            {
                if(host!=null) Object.DestroyImmediate(host);
                if(match!=null && match.ArenaCamera!=null)
                    Object.DestroyImmediate(match.ArenaCamera.gameObject);
            }
        }
        [Test]
        public void Timeout_ResolvesAfterContactOpportunity()
        {
            GameObject host=new GameObject("Timeout match");
            ChaseMatch match=null;
            try
            {
                match=host.AddComponent<ChaseMatch>();
                var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                typeof(ChaseMatch).GetMethod("Awake",flags).Invoke(match,null);
                typeof(ChaseMatch).GetField("<Phase>k__BackingField",flags)
                    .SetValue(match,ChasePhase.Active);
                typeof(ChaseMatch).GetField("<Remaining>k__BackingField",flags)
                    .SetValue(match,0.01f);
                var tick=typeof(ChaseMatch).GetMethod("FixedUpdate",flags);
                tick.Invoke(match,null);
                Assert.IsFalse(match.Winner.HasValue,
                    "Tag collisions must have one final physics callback opportunity");
                tick.Invoke(match,null);
                Assert.AreEqual(ChaseRole.Evader,match.Winner.Value);
                Assert.AreEqual(1,match.EvaderWins);
            }
            finally
            {
                if(host!=null) Object.DestroyImmediate(host);
                if(match!=null && match.ArenaCamera!=null)
                    Object.DestroyImmediate(match.ArenaCamera.gameObject);
            }
        }

        [Test]
        public void Motor_AcceleratesWithoutInstantSpeedOrDiagonalBoost()
        {
            GameObject actor=new GameObject("Motor test");
            GameObject ground=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                ground.transform.position=new Vector3(0f,-.13f,0f);
                ground.transform.localScale=new Vector3(10f,.26f,10f);
                actor.transform.position=new Vector3(0,.9f,0);
                Rigidbody rigid=actor.AddComponent<Rigidbody>();
                actor.AddComponent<CapsuleCollider>();
                ChaseMotor motor=actor.AddComponent<ChaseMotor>();
                GameObject visuals=new GameObject("Visuals");
                visuals.transform.SetParent(actor.transform,false);
                motor.Initialize(new ChaseTuning(),visuals.transform);
                Physics.SyncTransforms();
                motor.Simulate(new ChaseAction(new Vector2(1f,1f),false,false,false,false),.02f,1f);
                Assert.LessOrEqual(motor.HorizontalVelocity.magnitude,.401f);
                Assert.That(motor.HorizontalVelocity.magnitude,Is.GreaterThan(0f));
                Assert.AreEqual(70f,rigid.mass);
                Assert.AreEqual(RigidbodyCollisionDetectionModeDummy(),(int)rigid.collisionDetectionMode);
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(ground);
            }
        }
        static int RigidbodyCollisionDetectionModeDummy() =>
            (int)CollisionDetectionMode.ContinuousDynamic;
        [Test]
        public void SlideUsesSolidLowBoxAndCrouchUsesSolidCapsule()
        {
            GameObject actor=new GameObject("Crouch motor test");
            GameObject ground=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                ground.transform.position=new Vector3(0,-.13f,0);
                ground.transform.localScale=new Vector3(10,.26f,10);
                actor.transform.position=new Vector3(0,.9f,0);
                Rigidbody body=actor.AddComponent<Rigidbody>();
                CapsuleCollider capsule=actor.AddComponent<CapsuleCollider>();
                ChaseMotor motor=actor.AddComponent<ChaseMotor>();
                GameObject visual=new GameObject("Visual");
                visual.transform.SetParent(actor.transform,false);
                motor.Initialize(new ChaseTuning(),visual.transform);
                Physics.SyncTransforms();
                body.linearVelocity=new Vector3(4f,0f,0f);
                motor.Simulate(new ChaseAction(new Vector2(1f,0f),false,false,true,false),.02f,1f);
                Assert.AreEqual(ChaseMovementState.Sliding,motor.State);
                Assert.IsTrue(motor.SlideColliderActive,
                    "A football tackle uses the full low solid box");
                Assert.IsFalse(capsule.enabled);
                Assert.IsFalse(motor.SlideBox.isTrigger);
                Assert.AreEqual(.82f,motor.SlideBox.size.y,.001f);
                Assert.AreEqual(3.84f,motor.HorizontalVelocity.magnitude,.02f);
                motor.ResetMotor(new Vector3(0,.9f,0));
                Physics.SyncTransforms();
                motor.Simulate(new ChaseAction(Vector2.zero,false,false,false,true),.02f,2f);
                Assert.AreEqual(ChaseMovementState.Crouching,motor.State);
                Assert.AreEqual(1.55f,capsule.height);
                motor.Simulate(new ChaseAction(Vector2.zero,false,false,false,false),.02f,2.02f);
                Assert.AreEqual(1.8f,capsule.height);
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(ground);
            }
        }
        [Test]
        public void Jump_PreservesPlanarMomentum_AndBlocksDoubleJump()
        {
            GameObject actor=new GameObject("Jump test");
            GameObject floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                actor.transform.position=new Vector3(0,.9f,0);
                floor.transform.position=new Vector3(0,-.13f,0);
                floor.transform.localScale=new Vector3(10,.26f,10);
                Rigidbody body=actor.AddComponent<Rigidbody>();
                actor.AddComponent<CapsuleCollider>();
                ChaseMotor motor=actor.AddComponent<ChaseMotor>();
                Transform visual=new GameObject("visual").transform;
                visual.SetParent(actor.transform,false);
                motor.Initialize(new ChaseTuning(),visual);
                body.linearVelocity=new Vector3(3f,0f,0f);
                Physics.SyncTransforms();
                motor.Simulate(new ChaseAction(new Vector2(.5f,0),true,false,false,false),.02f,1f);
                Assert.Greater(motor.Velocity.y,4.9f);
                Assert.GreaterOrEqual(motor.HorizontalVelocity.x,3f);
                float upward=motor.Velocity.y;
                motor.Simulate(new ChaseAction(new Vector2(.5f,0),true,false,false,false),.02f,1.02f);
                Assert.Less(motor.Velocity.y,upward);
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(floor);
            }
        }


        [Test] public void ExplicitLeapNeverChangesCollisionMode()
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var actor=new GameObject("Leap actor");
            try {
                floor.transform.position=new Vector3(0,-.13f,0);
                floor.transform.localScale=new Vector3(10,.26f,10);
                actor.transform.position=new Vector3(0,.9f,0);
                actor.AddComponent<Rigidbody>();actor.AddComponent<CapsuleCollider>();
                var visual=new GameObject("visual");visual.transform.SetParent(actor.transform,false);
                var motor=actor.AddComponent<ChaseMotor>();
                motor.Initialize(new ChaseTuning(),visual.transform);
                Physics.SyncTransforms();
                motor.Simulate(new ChaseAction(Vector2.zero,false,true,false,false),.02f,1f);
                Assert.AreEqual(ChaseMovementState.Leaping,motor.State);
                Assert.AreEqual(9f,motor.HorizontalVelocity.magnitude,.001f);
                Assert.IsFalse(motor.Capsule.isTrigger);
                Assert.IsFalse(motor.Body.isKinematic);
            }finally{Object.DestroyImmediate(actor);Object.DestroyImmediate(floor);}
        }

        sealed class ConstantSource : IChaseActionSource
        {
            public int Calls;
            public int Resets;
            public ChaseAction ReadAction(float clock)
            {
                Calls++;
                return new ChaseAction(new Vector2(9f,9f),true,false,false,false);
            }
            public void ResetSource() => Resets++;
        }

        [Test]
        public void IndependentActionSourcesCannotBypassSharedMotor()
        {
            GameObject host=new GameObject("Action assignment match");
            ChaseMatch match=null;
            try
            {
                match=host.AddComponent<ChaseMatch>();
                var flags=BindingFlags.NonPublic|BindingFlags.Instance;
                typeof(ChaseMatch).GetMethod("Awake",flags).Invoke(match,null);
                var red=new ConstantSource();
                var blue=new ConstantSource();
                match.AssignActionSource(ChaseRole.Chaser,red);
                match.AssignActionSource(ChaseRole.Evader,blue);
                typeof(ChaseMatch).GetField("<Phase>k__BackingField",flags)
                    .SetValue(match,ChasePhase.Active);
                typeof(ChaseMatch).GetMethod("FixedUpdate",flags).Invoke(match,null);
                Assert.AreEqual(1,red.Calls);
                Assert.AreEqual(1,blue.Calls);
                Assert.AreEqual(1,red.Resets);
                Assert.AreEqual(1,blue.Resets);
                Assert.Less(match.Chaser.HorizontalVelocity.magnitude,.41f);
                Assert.Less(match.Evader.HorizontalVelocity.magnitude,.41f);
                Assert.AreEqual(70f,match.Chaser.Body.mass);
                Assert.AreEqual(70f,match.Evader.Body.mass);
            }
            finally
            {
                if(host!=null)Object.DestroyImmediate(host);
                if(match!=null && match.ArenaCamera!=null)
                    Object.DestroyImmediate(match.ArenaCamera.gameObject);
            }
        }


        [Test] public void NoContextualMovementStateOrActionExists()
        {
            Assert.IsFalse(System.Enum.GetNames(typeof(ChaseMovementState))
                .Any(x=>x.ToLowerInvariant().Contains("vault")));
            Assert.That(ChaseObservationSensor.PolicySize,Is.EqualTo(66));
        }

    }
}
