using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chase.Tests
{
    public sealed class ChasePracticePlaygroundTests
    {
        [Test] public void PublicPracticeUsesOnlyApprovedProductionPhysics()
        {
            const string path="Assets/Scenes/PracticePlayground.unity";
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s=>
                s.enabled && s.path==path),
                "Practice must be selectable through the editor/public build scene list");
            var existing=SceneManager.GetSceneByPath(path);
            bool open=existing.IsValid()&&existing.isLoaded;
            var scene=open?existing:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var root=scene.GetRootGameObjects().Single(x=>
                    x.name=="CHASE PRACTICE / authored playground");
                var group=root.GetComponentsInChildren<Transform>(true).Single(t=>
                    t.name=="PRACTICE / certified production obstacles");
                Assert.AreEqual(6,group.childCount);
                Assert.AreEqual(6,group.Cast<Transform>().Select(t=>
                    AssetDatabase.GetAssetPath(
                        PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)))
                    .Distinct().Count());
                Assert.IsEmpty(root.GetComponentsInChildren<ChaseMovementGym>(true));
                Assert.IsEmpty(root.GetComponentsInChildren<ChaseMatch>(true));
                foreach(var t in group.Cast<Transform>())
                {
                    Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(t.gameObject));
                    Assert.AreEqual(Vector3.one,t.localScale);
                    foreach(var c in t.GetComponentsInChildren<Collider>())
                        Assert.IsFalse(c.isTrigger,"Practice obstacles may not be triggers");
                }
            }
            finally {if(!open)EditorSceneManager.CloseScene(scene,true);}
        }

        [Test] public void PracticeStartPadsAvoidSolidProductionObstacleEnvelopes()
        {
            const string path="Assets/Scenes/PracticePlayground.unity";
            var existing=SceneManager.GetSceneByPath(path);
            bool open=existing.IsValid()&&existing.isLoaded;
            var scene=open?existing:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var obstacleRoot=scene.GetRootGameObjects()
                    .Single(x=>x.name=="CHASE PRACTICE / authored playground")
                    .GetComponentsInChildren<Transform>(true)
                    .Single(t=>t.name=="PRACTICE / certified production obstacles");
                var solids=obstacleRoot.GetComponentsInChildren<Collider>(true);
                var positions=new[]{
                    new Vector3(0,.9f,-8.5f),
                    new Vector3(-5,.9f,-4.8f),
                    new Vector3(5,.9f,-4.8f),
                    new Vector3(0,.9f,1.7f)
                };
                foreach(var position in positions)
                    foreach(var col in solids)
                    {
                        // Conservative 0.98m visual-head standing envelope.
                        Vector3 closest=col.ClosestPoint(position);
                        // The center lies well outside all actual obstacle AABBs;
                        // use XZ separation to avoid considering ground height.
                        var dx=Mathf.Abs(closest.x-position.x);
                        var dz=Mathf.Abs(closest.z-position.z);
                        Assert.IsTrue(dx>.49f || dz>.49f,
                            "Standing Chibi head would intersect "+col.name+
                            " at pad "+position);
                    }
            }
            finally {if(!open)EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
