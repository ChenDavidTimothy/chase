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
        var options = new BuildPlayerOptions
        {
            scenes = new[] { scene },
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
    public static void WebGLGym() => Build(BuildTarget.WebGL,
        "Assets/Scenes/MovementGym.unity", "Builds/ChaseAnimationGymWebGL");
    public static void LinuxProduction() => Build(BuildTarget.StandaloneLinux64,
        "Assets/Scenes/SampleScene.unity", "Builds/ChaseLinux/Chase.x86_64");
}
