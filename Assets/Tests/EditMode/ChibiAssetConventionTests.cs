using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Chase.Tests
{
    // Production import contract. This test must fail if another export or
    // automated asset edit reintroduces hidden corrective transforms.
    public sealed class ChibiAssetConventionTests
    {
        const string ModelPath="Assets/Art/Characters/Chibi/Models/Chibi_Rigged.fbx";
        const string PrefabPath="Assets/Resources/ChibiVisual.prefab";

        [Test]
        public void ProductionAnimatorNeverReferencesPreviewOrStaleImportedRanges()
        {
            var importer=AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Assert.NotNull(importer);
            var defaults=importer.defaultClipAnimations;
            var configured=importer.clipAnimations;
            Assert.AreEqual(13,defaults.Length);
            Assert.AreEqual(13,configured.Length);
            foreach(var expected in defaults)
            {
                var actual=configured.SingleOrDefault(c=>c.name==expected.name);
                Assert.NotNull(actual,"Missing FBX take: "+expected.name);
                Assert.That(actual.firstFrame,Is.EqualTo(expected.firstFrame).Within(.01f));
                Assert.That(actual.lastFrame,Is.EqualTo(expected.lastFrame).Within(.01f),
                    "Stale export frame range: "+expected.name);
            }
            var controller=AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                "Assets/Art/Characters/Chibi/Animations/Chibi_Validation.controller");
            Assert.NotNull(controller);
            var states=controller.layers[0].stateMachine.states.Select(x=>x.state).ToArray();
            Assert.AreEqual(13,states.Length);
            foreach(var state in states)
            {
                var clip=state.motion as AnimationClip;
                Assert.NotNull(clip,state.name);
                Assert.AreEqual("Chibi_Armature|CHASE_"+state.name,clip.name,
                    "Preview clip assigned to state: "+state.name);
                Assert.AreEqual(ModelPath,AssetDatabase.GetAssetPath(clip));
                Assert.Greater(AnimationUtility.GetCurveBindings(clip).Length,40);
            }
        }

        [Test]
        public void UnrotatedPrefabUsesMeterScaleAndGroundPivot()
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.NotNull(prefab);
            Assert.AreEqual(1,prefab.transform.childCount);
            Transform model=prefab.transform.GetChild(0);
            Assert.Less(model.localPosition.magnitude,.0001f,
                "Character model must have a feet-at-origin pivot, not a concealed height offset");
            Assert.Less(Quaternion.Angle(model.localRotation,Quaternion.identity),.001f,
                "Do not reintroduce +90/-90/180 corrective rotation in Unity");
            Assert.Less((model.localScale-Vector3.one).magnitude,.0001f,
                "Meters and final world height must be authored in Blender, not compensated here");
            Assert.AreEqual(0,prefab.GetComponentsInChildren<Collider>(true).Length);
        }

        [Test]
        public void GenericFbxFacesUnityPositiveZAtUnitScaleWithFeetAtZero()
        {
            ModelImporter importer=AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Assert.NotNull(importer);
            Assert.AreEqual(ModelImporterAnimationType.Generic,importer.animationType);
            GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Assert.NotNull(model);
            GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                Assert.Less(instance.transform.position.magnitude,.00001f);
                Assert.Less(Quaternion.Angle(instance.transform.rotation,Quaternion.identity),.001f);
                Assert.Less((instance.transform.localScale-Vector3.one).magnitude,.0001f);
                var animator=instance.GetComponent<Animator>();
                Assert.NotNull(animator);
                Assert.IsTrue(animator.avatar.isValid);
                Assert.IsFalse(animator.avatar.isHuman);
                animator.Play("CHASE_Idle",0,0f);
                animator.Update(.03f);
                var parts=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.AreEqual(17,parts.Length);
                var head=parts.Single(x=>x.name=="head_vis");
                var face=parts.Single(x=>x.name=="face_vis");
                Vector3 direction=face.bounds.center-head.bounds.center;
                direction.y=0f;
                Assert.Greater(Vector3.Dot(direction.normalized,Vector3.forward),.98f,
                    "Character's facial geometry must face +Z unrotated");
                Bounds bounds=parts[0].bounds;
                foreach(var r in parts.Skip(1))bounds.Encapsulate(r.bounds);
                Assert.That(bounds.min.y,Is.InRange(-.03f,.03f),"Feet must sit at local Y=0");
                Assert.That(bounds.max.y,Is.InRange(1.77f,1.83f),
                    "Standing height must be approximately 1.8 meters");
                Assert.IsFalse(float.IsNaN(direction.x)||float.IsNaN(direction.z));
            }
            finally {UnityEngine.Object.DestroyImmediate(instance);}
        }

        [Test]
        public void ProductionAnimationsRemainImportableAfterFreshAssetReload()
        {
            var clips=AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(x=>!x.name.StartsWith("__preview__")).ToArray();
            foreach(string name in new[]{"Idle","Run","Jump","Crouch",
                       "Slide","Leap","DiveProne","DiveCrawl","Turn","JumpLanding","LeapRecovery","CrouchWalk","SlideRecovery"})
                Assert.IsTrue(clips.Any(c=>c.name.EndsWith("|CHASE_"+name)),
                    "Source Blender Action must persist in saved file and FBX: "+name);
            Assert.AreEqual(13,clips.Length);
        }
    }
}
