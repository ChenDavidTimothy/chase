using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Reproducible CI/editor recovery route that does not require a live MCP editor.
// -batchmode -nographics -buildTarget WebGL -executeMethod ChaseReleaseBuild.WebGLGym
public static class ChaseReleaseBuild
{
    static void Build(BuildTarget target, string scene, string output)
    {
        // Never permit a browser build to ship transient animation previews.
        Chase.Editor.ChibiAnimationAssetContract.ValidateOrThrow();
        // Source scenes must serialize these dimensions explicitly; build
        // must fail before exporting a browser player with old zero defaults.
        ChaseMovementSceneContract.ValidateScenes();
        ChaseArenaV1Contract.ValidateGym();
        ChaseMenuReplayContract.Validate();
        ChaseArenaV1Contract.Validate();
        ChasePracticePlaygroundContract.Validate();
        var options = new BuildPlayerOptions
        {
            scenes = scene=="Assets/Scenes/SampleScene.unity" ||
                scene==ChaseReferenceArenaBuilder.ScenePath ?
                new[] { scene, ChasePracticePlaygroundBuilder.ScenePath } :
                new[] { scene },
            target = target,
            locationPathName = output,
            options = BuildOptions.DetailedBuildReport
        };
        BuildReport result = BuildPipeline.BuildPlayer(options);
        Debug.Log($"CHASE_RELEASE_BUILD target={target} scene={scene} " +
            $"result={result.summary.result} errors={result.summary.totalErrors} " +
            $"warnings={result.summary.totalWarnings} output={output}");
        if (result.summary.result != BuildResult.Succeeded || result.summary.totalErrors > 0)
            throw new BuildFailedException("CHASE release Player build failed: " + output);
    }

    public static void WebGLProduction() => Build(BuildTarget.WebGL,
        "Assets/Scenes/SampleScene.unity", "Builds/ChaseWebGL");
    // Explicitly isolated comparison build. Not part of production releases.
    public static void WebGLReferenceTrial()
    {
        ChaseReferenceArenaContract.Validate();
        Build(BuildTarget.WebGL,
            ChaseReferenceArenaBuilder.ScenePath,"Builds/ChaseReferenceTrialWebGL");
    }

    public static void WebGLGym() => Build(BuildTarget.WebGL,
        "Assets/Scenes/MovementGym.unity", "Builds/ChaseAnimationGymWebGL");
    public static void LinuxProduction() => Build(BuildTarget.StandaloneLinux64,
        "Assets/Scenes/SampleScene.unity", "Builds/ChaseLinux/Chase.x86_64");
}
