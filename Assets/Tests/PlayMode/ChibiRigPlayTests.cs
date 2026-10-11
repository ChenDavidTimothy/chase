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
    }
}
