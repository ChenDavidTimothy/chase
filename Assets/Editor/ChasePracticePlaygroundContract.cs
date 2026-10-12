using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chase;

// Release gate: public practice is built from real production obstacles,
// never detached collider approximations or developer Gym telemetry.
public static class ChasePracticePlaygroundContract
{
    public static void Validate()
    {
        var scene=SceneManager.GetSceneByPath(ChasePracticePlaygroundBuilder.ScenePath);
        bool already=scene.IsValid()&&scene.isLoaded;
        if(!already)scene=EditorSceneManager.OpenScene(
            ChasePracticePlaygroundBuilder.ScenePath,OpenSceneMode.Additive);
        try
        {
            var root=scene.GetRootGameObjects()
                .Single(x=>x.name==ChasePracticePlaygroundBuilder.Group);
            var practice=root.GetComponentInChildren<ChasePracticePlayground>(true);
            if(practice==null || root.GetComponentInChildren<ChaseMovementGym>(true)!=null)
                throw new InvalidOperationException("Public practice source mixed with developer Gym");
            if(scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ChaseMatch>(true)).Any())
                throw new InvalidOperationException("Public practice must not run a match or opponent");
            var obstacles=root.transform.Cast<Transform>().SingleOrDefault(t=>t.name=="PRACTICE / certified production obstacles");
            if(obstacles==null ||
               obstacles.childCount!=ChasePracticePlaygroundBuilder.Layout.Length)
                throw new InvalidOperationException("Practice module count drifted");
            for(int i=0;i<obstacles.childCount;i++)
            {
                var reference=ChasePracticePlaygroundBuilder.Layout[i];
                var instance=obstacles.GetChild(i);
                string expected=ChaseArenaV1Builder.Folder+"/"+reference.prefab+".prefab";
                var source=PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject);
                if(source==null || AssetDatabase.GetAssetPath(source)!=expected)
                    throw new InvalidOperationException("Practice obstacle is not approved source "+expected);
                if(Vector3.Distance(instance.localPosition,reference.at)>.002f ||
                   instance.localScale!=Vector3.one ||
                   Mathf.Abs(Mathf.DeltaAngle(instance.localEulerAngles.y,reference.yaw))>.1f)
                    throw new InvalidOperationException("Scaled or relocated public obstacle "+reference.prefab);
            }
            var floor=root.transform.Cast<Transform>().SingleOrDefault(t=>t.name=="SOLID FLOOR / 24 x 24");
            if(floor==null || floor.GetComponent<BoxCollider>()==null ||
               Vector3.Distance(floor.localScale,new Vector3(24f,.32f,24f))>.002f)
                throw new InvalidOperationException("Public practice floor changed");
            var serialized=new SerializedObject(practice);
            var tuning=serialized.FindProperty("tuning");
            foreach(var spec in new[]{
                ("runSpeed",6f),("standingHeight",1.8f),
                ("radius",.35f),("leapHorizontalSpeed",9f),
                ("leapVerticalSpeed",6.8f),("slideBodyLength",1.98f),
                ("slideBodyWidth",.84f),("slideBodyHeight",.82f),
                ("slideBodyCenterY",-.48f),("slideBodyCenterForward",-.24f),
                ("slideDuration",.45f),("slideRecovery",.20f),
                ("slideCrawlSpeed",1.10f),("diveCapsuleLength",1.95f),
                ("diveCapsuleRadius",.43f),("diveCapsuleCenterY",-.29f),
                ("diveCapsuleCenterForward",.16f)
            })
            {
                var field=tuning.FindPropertyRelative(spec.Item1);
                if(field==null || Mathf.Abs(field.floatValue-spec.Item2)>.002f)
                    throw new InvalidOperationException("Public practice physics deviates: "+spec.Item1);
            }
            var cam=serialized.FindProperty("practiceCamera").objectReferenceValue as Camera;
            var spawn=serialized.FindProperty("start").objectReferenceValue as Transform;
            if(cam==null || spawn==null ||
               Vector3.Distance(spawn.position,new Vector3(0,.9f,-8.5f))>.001f)
                throw new InvalidOperationException("Public practice spawn/camera missing");
            Debug.Log("CHASE_PUBLIC_PRACTICE_CONTRACT_PASS modules="+obstacles.childCount);
        }
        finally
        {
            if(!already)EditorSceneManager.CloseScene(scene,true);
        }
    }
}
