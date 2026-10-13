using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chase;

// Both saved scenes must serialize the same actual slide and dive collider.
// Missing values fail release builds rather than silently diverging.
public static class ChaseMovementSceneContract
{
    const string Production="Assets/Scenes/SampleScene.unity";
    const string Gym="Assets/Scenes/MovementGym.unity";

    static bool Correct(ChaseTuning t) =>
        t!=null && Mathf.Abs(t.leapVerticalSpeed-6.8f)<.002f &&
        Mathf.Abs(t.slideBodyLength-1.98f)<.002f &&
        Mathf.Abs(t.slideBodyWidth-.84f)<.002f &&
        Mathf.Abs(t.slideBodyHeight-.82f)<.002f &&
        Mathf.Abs(t.slideBodyCenterY+.48f)<.002f &&
        Mathf.Abs(t.slideBodyCenterForward+.24f)<.002f &&
        Mathf.Abs(t.slideCrawlSpeed-1.10f)<.002f &&
        Mathf.Abs(t.slideDuration-.45f)<.002f &&
        Mathf.Abs(t.slideDeceleration-8f)<.002f &&
        Mathf.Abs(t.slideRecovery-.20f)<.002f &&
        Mathf.Abs(t.diveCapsuleLength-1.95f)<.002f &&
        Mathf.Abs(t.diveCapsuleRadius-.43f)<.002f &&
        Mathf.Abs(t.diveCapsuleCenterY+.29f)<.002f &&
        Mathf.Abs(t.diveCapsuleCenterForward-.16f)<.002f;

    static void Apply(ChaseTuning t)
    {
        t.leapVerticalSpeed=6.8f;
        t.slideBodyLength=1.98f;
        t.slideBodyWidth=.84f;
        t.slideBodyHeight=.82f;
        t.slideBodyCenterY=-.48f;
        t.slideBodyCenterForward=-.24f;
        t.slideCrawlSpeed=1.10f;
        t.slideDuration=.45f;
        t.slideDeceleration=8f;
        t.slideRecovery=.20f;
        t.diveCapsuleLength=1.95f;
        t.diveCapsuleRadius=.43f;
        t.diveCapsuleCenterY=-.29f;
        t.diveCapsuleCenterForward=.16f;
    }

    static void Inspect(string path,bool repair)
    {
        // Never close or overwrite the scene a designer already has open.
        // Editor menu and build validation must both preserve editor context.
        var existing=SceneManager.GetSceneByPath(path);
        bool alreadyLoaded=existing.IsValid() && existing.isLoaded;
        var scene=alreadyLoaded?existing:
            EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            if(path==Production)
            {
                var match=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ChaseMatch>(true))
                    .Single();
                if(repair)
                {
                    Apply(match.Tuning);
                    EditorUtility.SetDirty(match);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                if(!Correct(match.Tuning))
                    throw new InvalidOperationException("CHASE mismatched movement collider config in "+path);
                if(!match.FrontEndEnabled)
                    throw new InvalidOperationException("Production must open at public main menu");
                // The authored match intentionally discovers the tagged Main
                // Camera during Awake, so the serialized field can be null
                // when validation runs in Edit Mode before Awake is called.
                var cam=match.ArenaCamera ?? scene.GetRootGameObjects()
                    .SelectMany(g=>g.GetComponentsInChildren<Camera>(true))
                    .SingleOrDefault(c=>c.CompareTag("MainCamera"));
                if(cam==null || Mathf.Abs(Mathf.DeltaAngle(
                    cam.transform.eulerAngles.y,ChaseCameraRig.ArenaYaw))>.1f)
                    throw new InvalidOperationException(
                        "Production camera must use fixed arena-aligned yaw");
            }
            else
            {
                var gym=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ChaseMovementGym>(true))
                    .Single();
                var serialized=new SerializedObject(gym);
                var settings=serialized.FindProperty("tuning");
                if(settings==null)throw new InvalidOperationException("Missing gym tuning");
                if(repair)
                {
                    foreach(var field in new[]{
                        ("leapVerticalSpeed",6.8f),("slideBodyLength",1.98f),("slideBodyWidth",.84f),
                        ("slideBodyHeight",.82f),("slideBodyCenterY",-.48f),
                        ("slideBodyCenterForward",-.24f),("slideCrawlSpeed",1.10f),
                        ("slideDuration",.45f),("slideDeceleration",8f),
                        ("slideRecovery",.20f),
                        ("diveCapsuleLength",1.95f),
                        ("diveCapsuleRadius",.43f),
                        ("diveCapsuleCenterY",-.29f),
                        ("diveCapsuleCenterForward",.16f)
                    })
                        settings.FindPropertyRelative(field.Item1).floatValue=field.Item2;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(gym);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                // The values read back from serialized scene content are the
                // actual values a standalone WebGL Player will deserialize.
                foreach(var field in new[]{
                    ("leapVerticalSpeed",6.8f),("slideBodyLength",1.98f),("slideBodyWidth",.84f),
                    ("slideBodyHeight",.82f),("slideBodyCenterY",-.48f),
                    ("slideBodyCenterForward",-.24f),("slideCrawlSpeed",1.10f),
                    ("slideDuration",.45f),("slideDeceleration",8f),
                    ("slideRecovery",.20f),
                        ("diveCapsuleLength",1.95f),
                        ("diveCapsuleRadius",.43f),
                        ("diveCapsuleCenterY",-.29f),
                        ("diveCapsuleCenterForward",.16f)
                })
                {
                    var f=settings.FindPropertyRelative(field.Item1);
                    if(f==null || Mathf.Abs(f.floatValue-field.Item2)>.002f)
                        throw new InvalidOperationException("CHASE gym "+field.Item1+
                            " differs from authoritative physics: "+path);
                }
            }
            Debug.Log("CHASE_MOVEMENT_SCENE_CONTRACT_PASS: "+path);
        }
        finally
        {
            if(!alreadyLoaded)EditorSceneManager.CloseScene(scene,true);
        }
    }

    [MenuItem("Chase/Repair Authored Movement Physics")]
    public static void RepairScenes()
    {
        Inspect(Production,true);
        Inspect(Gym,true);
        AssetDatabase.SaveAssets();
        Debug.Log("CHASE_MOVEMENT_SCENES_REPAIRED");
    }
    public static void ValidateScenes()
    {
        Inspect(Production,false);
        Inspect(Gym,false);
    }
}
