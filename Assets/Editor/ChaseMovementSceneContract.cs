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
        t!=null && Mathf.Abs(t.runSpeed-6f)<.002f &&
        Mathf.Abs(t.groundAcceleration-36f)<.002f &&
        Mathf.Abs(t.groundBraking-44f)<.002f &&
        Mathf.Abs(t.airAcceleration-5f)<.002f &&
        Mathf.Abs(t.jumpSpeed-7f)<.002f &&
        Mathf.Abs(t.jumpBuffer-.13f)<.002f &&
        Mathf.Abs(t.coyoteTime-.12f)<.002f &&
        Mathf.Abs(t.actionBuffer-.11f)<.002f &&
        Mathf.Abs(t.leapHorizontalSpeed-8.2f)<.002f &&
        Mathf.Abs(t.leapVerticalSpeed-7.25f)<.002f &&
        Mathf.Abs(t.leapRecovery-.27f)<.002f &&
        Mathf.Abs(t.diveImpactRecovery-.20f)<.002f &&
        Mathf.Abs(t.recoverySpeed-3.4f)<.002f &&
        Mathf.Abs(t.proneCrawlSpeed-2.6f)<.002f &&
        Mathf.Abs(t.slideBodyLength-1.98f)<.002f &&
        Mathf.Abs(t.slideBodyWidth-.84f)<.002f &&
        Mathf.Abs(t.slideBodyHeight-.82f)<.002f &&
        Mathf.Abs(t.slideBodyCenterY+.48f)<.002f &&
        Mathf.Abs(t.slideBodyCenterForward+.24f)<.002f &&
        Mathf.Abs(t.slideCrawlSpeed-2.7f)<.002f &&
        Mathf.Abs(t.slideDuration-.42f)<.002f &&
        Mathf.Abs(t.slideDeceleration-6f)<.002f &&
        Mathf.Abs(t.slideRecovery-.14f)<.002f &&
        Mathf.Abs(t.diveCapsuleLength-1.95f)<.002f &&
        Mathf.Abs(t.diveCapsuleRadius-.43f)<.002f &&
        Mathf.Abs(t.diveCapsuleCenterY+.29f)<.002f &&
        Mathf.Abs(t.diveCapsuleCenterForward-.16f)<.002f;

    static void Apply(ChaseTuning t)
    {
        t.runSpeed=6f;
        t.groundAcceleration=36f;
        t.groundBraking=44f;
        t.airAcceleration=5f;
        t.jumpSpeed=7f;
        t.jumpBuffer=.13f;
        t.coyoteTime=.12f;
        t.actionBuffer=.11f;
        t.leapHorizontalSpeed=8.2f;
        t.leapRecovery=.27f;
        t.diveImpactRecovery=.20f;
        t.recoverySpeed=3.4f;
        t.proneCrawlSpeed=2.6f;
        t.leapVerticalSpeed=7.25f;
        t.slideBodyLength=1.98f;
        t.slideBodyWidth=.84f;
        t.slideBodyHeight=.82f;
        t.slideBodyCenterY=-.48f;
        t.slideBodyCenterForward=-.24f;
        t.slideCrawlSpeed=2.7f;
        t.slideDuration=.42f;
        t.slideDeceleration=6f;
        t.slideRecovery=.14f;
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
                        ("runSpeed",6f),("groundAcceleration",36f),
                        ("groundBraking",44f),("airAcceleration",5f),
                        ("jumpSpeed",7f),("jumpBuffer",.13f),
                        ("coyoteTime",.12f),("actionBuffer",.11f),
                        ("leapHorizontalSpeed",8.2f),
                        ("leapRecovery",.27f),("diveImpactRecovery",.20f),
                        ("recoverySpeed",3.4f),("proneCrawlSpeed",2.6f),
                    ("leapVerticalSpeed",7.25f),("slideBodyLength",1.98f),("slideBodyWidth",.84f),
                        ("slideBodyHeight",.82f),("slideBodyCenterY",-.48f),
                        ("slideBodyCenterForward",-.24f),("slideCrawlSpeed",2.7f),
                        ("slideDuration",.42f),("slideDeceleration",6f),
                        ("slideRecovery",.14f),
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
                    ("runSpeed",6f),("groundAcceleration",36f),
                    ("groundBraking",44f),("airAcceleration",5f),
                    ("jumpSpeed",7f),("jumpBuffer",.13f),
                    ("coyoteTime",.12f),("actionBuffer",.11f),
                    ("leapHorizontalSpeed",8.2f),
                    ("leapRecovery",.27f),("diveImpactRecovery",.20f),
                    ("recoverySpeed",3.4f),("proneCrawlSpeed",2.6f),
                    ("leapVerticalSpeed",7.25f),("slideBodyLength",1.98f),("slideBodyWidth",.84f),
                    ("slideBodyHeight",.82f),("slideBodyCenterY",-.48f),
                    ("slideBodyCenterForward",-.24f),("slideCrawlSpeed",2.7f),
                    ("slideDuration",.42f),("slideDeceleration",6f),
                    ("slideRecovery",.14f),
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
