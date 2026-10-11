using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Chase.Editor
{
    // Editor and CI both fail closed if an FBX reimport silently changes state
    // references to Unity's transient __preview__ clips or stale frame ranges.
    public sealed class ChibiAnimationAssetContract : IPreprocessBuildWithReport
    {
        public const string ModelPath = "Assets/Art/Characters/Chibi/Models/Chibi_Rigged.fbx";
        public const string ControllerPath = "Assets/Art/Characters/Chibi/Animations/Chibi_Validation.controller";
        public static readonly string[] States = { "Idle", "Run", "Jump", "JumpLanding", "Leap", "LeapRecovery",
            "Crouch", "CrouchWalk", "Slide", "SlideRecovery", "Turn" };
        public int callbackOrder => -100;

        public static void ValidateOrThrow()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (importer == null || controller == null || controller.layers.Length != 1)
                throw new InvalidOperationException("CHASE: character FBX or production Animator Controller is missing");
            if (importer.animationType != ModelImporterAnimationType.Generic)
                throw new InvalidOperationException("CHASE: production rig must use Generic import");
            var expectedTakes = importer.defaultClipAnimations;
            var configured = importer.clipAnimations;
            if (expectedTakes.Length != States.Length || configured.Length != States.Length)
                throw new InvalidOperationException("CHASE: require precisely 11 FBX clip overrides");
            foreach (var original in expectedTakes)
            {
                var actual = configured.SingleOrDefault(c => c.name == original.name);
                if (actual == null || Mathf.Abs(actual.firstFrame - original.firstFrame) > .01f ||
                    Mathf.Abs(actual.lastFrame - original.lastFrame) > .01f)
                    throw new InvalidOperationException("CHASE: stale imported animation frame range: " + original.name);
                var simple = original.name.Split(new[]{"CHASE_"}, StringSplitOptions.None).Last();
                bool shouldLoop = simple == "Idle" || simple == "Run" || simple == "CrouchWalk";
                if (actual.loopTime != shouldLoop)
                    throw new InvalidOperationException("CHASE: incorrect looping on " + original.name);
            }
            var states = controller.layers[0].stateMachine.states.Select(x => x.state).ToArray();
            if (states.Length != States.Length)
                throw new InvalidOperationException("CHASE: Animator must contain exactly 11 states");
            foreach (string name in States)
            {
                var state = states.SingleOrDefault(x => x.name == name);
                var clip = state != null ? state.motion as AnimationClip : null;
                string required = "Chibi_Armature|CHASE_" + name;
                if (clip == null || clip.name != required || clip.name.StartsWith("__preview__") ||
                    AssetDatabase.GetAssetPath(clip) != ModelPath ||
                    !AssetDatabase.Contains(clip) ||
                    AnimationUtility.GetCurveBindings(clip).Length < 40)
                    throw new InvalidOperationException("CHASE: Animator state " + name +
                        " must reference a real FBX action, never a __preview__ clip");
                if (name == "SlideRecovery" && Math.Abs(clip.length - .2f) > .045f)
                    throw new InvalidOperationException("CHASE: slide recovery clip has unexpected duration");
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ChibiVisual.prefab");
            var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
            if (animator == null || animator.runtimeAnimatorController != controller || animator.applyRootMotion)
                throw new InvalidOperationException("CHASE: production visual must use the validated Controller without root motion");
        }

        [MenuItem("Chase/Repair Production Character Animation Imports")]
        public static void RepairImportedClipsAndController()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("CHASE: character FBX is missing");
            // Derive the current take ranges from the FBX itself. Stale importer
            // frame overrides caused production builds to use outdated timing.
            var sourceTakes = importer.defaultClipAnimations;
            if (sourceTakes.Length != States.Length)
                throw new InvalidOperationException("CHASE: expected exactly 11 FBX animation takes");
            foreach (var clip in sourceTakes)
            {
                var shortName = clip.name.Split(new[]{"CHASE_"}, StringSplitOptions.None).Last();
                clip.loopTime = shortName == "Idle" || shortName == "Run" || shortName == "CrouchWalk";
                clip.loopPose = clip.loopTime;
            }
            importer.clipAnimations = sourceTakes;
            importer.SaveAndReimport();
            var realClips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Where(c => c.name.StartsWith("Chibi_Armature|CHASE_"))
                .ToArray();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || realClips.Length != States.Length)
                throw new InvalidOperationException("CHASE: missing clips or controller after reimport");
            foreach (var child in controller.layers[0].stateMachine.states)
            {
                var clip = realClips.Single(c => c.name == "Chibi_Armature|CHASE_" + child.state.name);
                child.state.motion = clip;
                child.state.writeDefaultValues = false;
                EditorUtility.SetDirty(child.state);
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            ValidateOrThrow();
            Debug.Log("CHASE_ANIMATION_IMPORT_REPAIRED: 11 real FBX clips, current frame ranges, no previews");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            try { ValidateOrThrow(); }
            catch (Exception ex) { throw new BuildFailedException(ex.Message); }
        }

        [MenuItem("Chase/Validate Production Character Animation")]
        static void VerifyFromMenu()
        {
            ValidateOrThrow();
            Debug.Log("CHASE_ANIMATION_IMPORT_CONTRACT_PASS: 11 correct production clips and stable frame ranges");
        }
    }
}
