using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chase.Tests
{
    public sealed class ChibiRigPlayTests
    {
        [UnityTest]
        public IEnumerator RiggedPrefabContainsAllSeventeenAnimatedParts()
        {
            GameObject source=Resources.Load<GameObject>("ChibiVisual");
            Assert.NotNull(source,"Rigged chibi prefab must be in Resources");
            GameObject root=UnityEngine.Object.Instantiate(source);
            try
            {
                Animator animator=root.GetComponentInChildren<Animator>(true);
                Assert.NotNull(animator);
                Assert.IsTrue(animator.avatar.isValid);
                Assert.IsFalse(animator.avatar.isHuman,"Generic preserves authored pelvis motion");
                Assert.IsFalse(animator.applyRootMotion);
                Assert.AreEqual(0,root.GetComponentsInChildren<Collider>(true).Length);
                var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.AreEqual(17,skins.Length);
                foreach(var skin in skins)
                {
                    Assert.Greater(skin.sharedMesh.vertexCount,0);
                    Assert.Greater(skin.bones.Length,0,"Unbound mesh: "+skin.name);
                    var weights=skin.sharedMesh.boneWeights;
                    Assert.AreEqual(skin.sharedMesh.vertexCount,weights.Length);
                    foreach(var weight in weights)
                    {
                        Assert.Greater(weight.weight0,.99f,"Rigid part must have full bone assignment: "+skin.name);
                        Assert.Less(weight.weight1,.01f);
                    }
                }
                var states=new[]{"Idle","Run","Jump","Crouch","Slide","Vault","Leap","Turn"};
                foreach(string state in states)
                {
                    Assert.IsTrue(animator.HasState(0,Animator.StringToHash(state)),state+" not playable");
                    animator.Play(state,0,0.5f);
                    animator.Update(1f/30f);
                    Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName(state),
                        "Clip did not play: "+state);
                }
                Assert.IsFalse(animator.applyRootMotion);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
            yield return null;
        }

        [UnityTest]
        public IEnumerator CrouchUsesSkeletonWithoutScalingOrMovingTheWholeVisual()
        {
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position=new Vector3(0,-.13f,0);
            ground.transform.localScale=new Vector3(10,.26f,10);
            var actor=new GameObject("Chibi Motor Test");
            GameObject visual=null;
            try
            {
                actor.transform.position=new Vector3(0,.9f,0);
                actor.AddComponent<Rigidbody>();
                actor.AddComponent<CapsuleCollider>();
                var motor=actor.AddComponent<ChaseMotor>();
                visual=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("ChibiVisual"),actor.transform,false);
                motor.Initialize(new ChaseTuning(),visual.transform);
                var originalScale=visual.transform.localScale;
                var originalPos=visual.transform.localPosition;
                var animator=visual.GetComponentInChildren<Animator>();
                animator.Play("Idle",0,0f);
                animator.Update(.1f);
                var head=animator.GetComponentsInChildren<Transform>(true)
                    .First(t=>t.name=="head");
                float headIdle=head.position.y;
                Physics.SyncTransforms();
                motor.Simulate(new ChaseAction(Vector2.zero,false,true),.02f,10f);
                Assert.AreEqual(ChaseMovementState.Crouching,motor.State);
                Assert.AreEqual(.85f,motor.Capsule.height,.001f);
                Assert.AreEqual(originalScale,visual.transform.localScale);
                Assert.AreEqual(originalPos,visual.transform.localPosition);
                animator.Play("Crouch",0,.55f);
                animator.Update(.1f);
                float headCrouch=head.position.y;
                Assert.Less(headCrouch,headIdle-.15f,
                    "The crouch must visibly lower the head through bone animation");
                Assert.IsFalse(animator.applyRootMotion);
                Assert.AreEqual(0,visual.GetComponentsInChildren<Collider>(true).Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(ground);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChibiFaceStaysInFrontOfTheHeadDuringTurn()
        {
            var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("ChibiVisual"));
            try
            {
                var animator=root.GetComponentInChildren<Animator>();
                var meshes=root.GetComponentsInChildren<SkinnedMeshRenderer>();
                var face=meshes.First(r=>r.name=="face_vis");
                var head=meshes.First(r=>r.name=="head_vis");
                foreach (float progress in new[]{0f,.25f,.5f,.8f})
                {
                    animator.Play("Turn",0,progress);
                    animator.Update(.02f);
                    Vector3 offset=face.bounds.center-head.bounds.center;
                    Assert.That(offset.magnitude,Is.InRange(.18f,.42f),
                        "Face separated from head in turn clip");
                    Assert.IsFalse(float.IsNaN(offset.z));
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            yield return null;
        }
        [UnityTest]
        public IEnumerator AnimationDriverReadsOnlyTheSharedMotorState()
        {
            GameObject actor=new GameObject("Animation parity test");
            GameObject visual=null;
            try
            {
                actor.transform.position=new Vector3(0,.9f,0);
                actor.AddComponent<Rigidbody>();
                actor.AddComponent<CapsuleCollider>();
                var motor=actor.AddComponent<ChaseMotor>();
                visual=UnityEngine.Object.Instantiate(
                    Resources.Load<GameObject>("ChibiVisual"),actor.transform,false);
                motor.Initialize(new ChaseTuning(),visual.transform);
                var driver=visual.GetComponent<ChibiVisualDriver>();
                var animator=driver.Animator;
                Assert.AreSame(motor,driver.Motor);
                var stateField=typeof(ChaseMotor).GetField("<State>k__BackingField",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                var update=typeof(ChibiVisualDriver).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.NonPublic|
                    System.Reflection.BindingFlags.Instance);
                var originalPosition=actor.transform.position;
                var originalScale=visual.transform.localScale;
                foreach(var pair in new[]{
                    (ChaseMovementState.Crouching,"Crouch"),
                    (ChaseMovementState.Sliding,"Slide"),
                    (ChaseMovementState.Vaulting,"Vault"),
                    (ChaseMovementState.Airborne,"Jump"),
                    (ChaseMovementState.Standing,"Idle")
                })
                {
                    stateField.SetValue(motor,pair.Item1);
                    update.Invoke(driver,null);
                    animator.Update(.20f);
                    Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName(pair.Item2),
                        "Motor state not reflected by Animator: "+pair.Item2);
                    Assert.AreEqual(originalPosition,actor.transform.position);
                    Assert.AreEqual(originalScale,visual.transform.localScale);
                    Assert.IsFalse(animator.applyRootMotion);
                    Assert.IsFalse(motor.Capsule.isTrigger);
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(actor);}
            yield return null;
        }
        [UnityTest]
        public IEnumerator VisualCreatedBeforeMotorCanBindAndPlayRun()
        {
            var actor = new GameObject("Late motor fixture");
            try
            {
                actor.transform.position = new Vector3(0f, .9f, 0f);
                actor.AddComponent<Rigidbody>();
                actor.AddComponent<CapsuleCollider>();
                // Reproduce actual ChaseMatch.CreateCompetitor order, not the
                // reversed order that masked the original broken binding.
                var visual = UnityEngine.Object.Instantiate(
                    Resources.Load<GameObject>("ChibiVisual"), actor.transform, false);
                var driver = visual.GetComponent<ChibiVisualDriver>();
                Assert.IsNull(driver.Motor,
                    "Awake is called before ChaseMotor exists on this actor");
                var motor = actor.AddComponent<ChaseMotor>();
                motor.Initialize(new ChaseTuning(), visual.transform);
                driver.BindMotor(motor);
                Assert.AreSame(motor, driver.Motor);
                var animator = driver.Animator;
                var thigh = animator.GetComponentsInChildren<Transform>(true)
                    .First(t => t.name == "thigh_left");
                animator.Play("Run", 0, 0f);
                animator.Update(.02f);
                Quaternion firstPose = thigh.localRotation;
                animator.Update(.38f);
                Assert.Greater(Quaternion.Angle(firstPose, thigh.localRotation), 20f,
                    "Running must visibly articulate the legs, not stay neutral");
                motor.Body.linearVelocity = Vector3.forward * 3.2f;
                var update = typeof(ChibiVisualDriver).GetMethod("LateUpdate",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                update.Invoke(driver, null);
                animator.Update(.20f);
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Run"));
                driver.ResetAnimation();
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"));
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChibiFacePointsAlongEveryWorldMovementDirection()
        {
            var actor = new GameObject("Visual facing fixture");
            try
            {
                actor.transform.position = new Vector3(0f, .9f, 0f);
                actor.AddComponent<Rigidbody>();
                actor.AddComponent<CapsuleCollider>();
                var motor = actor.AddComponent<ChaseMotor>();
                var visual = UnityEngine.Object.Instantiate(
                    Resources.Load<GameObject>("ChibiVisual"), actor.transform, false);
                motor.Initialize(new ChaseTuning(), visual.transform);
                visual.GetComponent<ChibiVisualDriver>().BindMotor(motor);
                var animator = visual.GetComponentInChildren<Animator>();
                var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                var face = skins.First(r => r.name == "face_vis");
                var head = skins.First(r => r.name == "head_vis");
                var orient = typeof(ChaseMotor).GetMethod("OrientVisual",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                foreach (var direction in new[] {
                    Vector3.forward, Vector3.back, Vector3.left, Vector3.right
                })
                {
                    orient.Invoke(motor, new object[] { direction * 4f, 2f });
                    animator.Play("Idle", 0, 0f);
                    animator.Update(.05f);
                    Vector3 faceDirection = face.bounds.center - head.bounds.center;
                    faceDirection.y = 0f;
                    Assert.Greater(Vector3.Dot(faceDirection.normalized, direction), .92f,
                        "Face must point in the movement direction " + direction);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator IdleKeepsPelvisAndBothFeetGroundedWithoutVerticalBob()
        {
            var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("ChibiVisual"));
            try
            {
                var animator = root.GetComponentInChildren<Animator>();
                var bones = animator.GetComponentsInChildren<Transform>(true);
                Transform pelvis = bones.First(t => t.name == "pelvis");
                Transform leftFoot = bones.First(t => t.name == "foot_left");
                Transform rightFoot = bones.First(t => t.name == "foot_right");
                float pelvisAtRest = 0f, leftAtRest = 0f, rightAtRest = 0f;
                foreach (float progress in new[] { 0f, .25f, .5f, .75f, .995f })
                {
                    animator.Play("Idle", 0, progress);
                    animator.Update(.02f);
                    if (progress == 0f)
                    {
                        pelvisAtRest = pelvis.position.y;
                        leftAtRest = leftFoot.position.y;
                        rightAtRest = rightFoot.position.y;
                    }
                    else
                    {
                        Assert.AreEqual(pelvisAtRest, pelvis.position.y, .003f,
                            "Idle should never lift the pelvis");
                        Assert.AreEqual(leftAtRest, leftFoot.position.y, .003f,
                            "Left foot must remain planted during idle");
                        Assert.AreEqual(rightAtRest, rightFoot.position.y, .003f,
                            "Right foot must remain planted during idle");
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            yield return null;
        }
        [UnityTest]
        public IEnumerator MatchSpawnsBothRolesWithFeetAtGroundAndUnrotatedArt()
        {
            var host=new GameObject("Character coordinate contract match");
            ChaseMatch match=null;
            try
            {
                match=host.AddComponent<ChaseMatch>();
                foreach(ChaseMotor motor in new[]{match.Chaser,match.Evader})
                {
                    var visual=motor.GetComponentInChildren<ChibiVisualDriver>();
                    Assert.NotNull(visual);
                    Assert.AreSame(motor,visual.Motor);
                    Assert.Less(Mathf.Abs(visual.transform.localPosition.y+
                        match.Tuning.standingHeight*.5f),.0001f,
                        "The capsule center to feet-anchor offset belongs on the visual root");
                    Assert.Less(Quaternion.Angle(visual.transform.localRotation,
                        Quaternion.identity),.0001f);
                    Assert.Less((visual.transform.localScale-Vector3.one).magnitude,.0001f);
                    Transform importedModel=visual.transform.GetChild(0);
                    Assert.Less(importedModel.localPosition.magnitude,.0001f);
                    Assert.Less(Quaternion.Angle(importedModel.localRotation,
                        Quaternion.identity),.0001f);
                    Assert.Less((importedModel.localScale-Vector3.one).magnitude,.0001f);
                    Assert.AreEqual(0,visual.GetComponentsInChildren<Collider>(true).Length);
                    Assert.AreEqual(1.8f,motor.Capsule.height,.0001f);
                    Assert.AreEqual(.35f,motor.Capsule.radius,.0001f);
                    var skins=visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                    Bounds combined=skins[0].bounds;
                    foreach(var renderer in skins.Skip(1)) combined.Encapsulate(renderer.bounds);
                    Assert.That(combined.min.y,Is.InRange(-.04f,.04f),
                        "Both roles should place the art feet at the arena floor");
                    Assert.That(combined.max.y,Is.InRange(1.75f,1.85f));
                    Assert.IsFalse(visual.Animator.applyRootMotion);
                }
            }
            finally
            {
                if(match!=null && match.ArenaCamera!=null)
                    UnityEngine.Object.DestroyImmediate(match.ArenaCamera.gameObject);
                UnityEngine.Object.DestroyImmediate(host);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredRunningCycleStridesTowardUnityPositiveZ()
        {
            var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("ChibiVisual"));
            try
            {
                var anim=root.GetComponentInChildren<Animator>(true);
                var joints=anim.GetComponentsInChildren<Transform>(true);
                var pelvis=joints.First(t=>t.name=="pelvis");
                var left=joints.First(t=>t.name=="shin_left");
                var right=joints.First(t=>t.name=="shin_right");
                anim.Play("Run",0,0f);
                anim.Update(.02f);
                float first=left.position.z-right.position.z;
                anim.Play("Run",0,.5f);
                anim.Update(.02f);
                float opposite=left.position.z-right.position.z;
                Assert.Greater(Mathf.Abs(first),.10f,"Run must stride visibly");
                Assert.Less(first*opposite,-.015f,
                    "Leg swing must alternate forward/backward on Unity +Z, not reverse the model");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            yield return null;
        }
    }
}
