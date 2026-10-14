using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chase.Tests
{
    public sealed class ChaseReferenceArenaTests
    {
        const int Side=45;
        const float Step=.245f;
        const float Origin=-5.39f;
        static Vector3 Point(int x,int z)=>new Vector3(Origin+x*Step,.9f,Origin+z*Step);
        static (int,int) Cell(Vector3 p)=>(
            Mathf.Clamp(Mathf.RoundToInt((p.x-Origin)/Step),0,Side-1),
            Mathf.Clamp(Mathf.RoundToInt((p.z-Origin)/Step),0,Side-1));

        [Test] public void TrialIsIndependentAndPhysicallySourceLinked()
        {
            const string trial="Assets/Scenes/ArenaWCTTrial.unity";
            var s=SceneManager.GetSceneByPath(trial);
            bool already=s.IsValid()&&s.isLoaded;
            if(!already)s=EditorSceneManager.OpenScene(trial,OpenSceneMode.Additive);
            try
            {
                var root=s.GetRootGameObjects().Single(o=>o.name.StartsWith("ARENA  /"));
                var group=root.GetComponentsInChildren<Transform>(true)
                    .Single(t=>t.name=="CHASE / WCT REFERENCE TRIAL");
                Assert.AreEqual(18,group.childCount);
                foreach(Transform t in group)
                {
                    Assert.IsTrue(UnityEditor.PrefabUtility.IsPartOfPrefabInstance(t.gameObject),
                        "Trial module must remain source linked: "+t.name);
                    Assert.AreEqual(Vector3.one,t.localScale);
                }
                Assert.IsFalse(root.GetComponentsInChildren<Transform>(true).Any(t=>
                    t.name=="CHASE ARENA V1 / modular course"));
                Assert.NotNull(UnityEditor.AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/Scenes/SampleScene.unity"));
            }
            finally {if(!already)EditorSceneManager.CloseScene(s,true);}
        }

        [Test] public void FullStandingBodyCanNavigateAroundCrowdedCourse()
        {
            string path="Assets/Scenes/ArenaWCTTrial.unity";
            var s=SceneManager.GetSceneByPath(path);
            bool loaded=s.IsValid()&&s.isLoaded;
            if(!loaded)s=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var root=s.GetRootGameObjects().Single(o=>o.name.StartsWith("ARENA  /"));
                var solids=root.GetComponentsInChildren<BoxCollider>(true)
                    .Where(c=>c.gameObject.name!="Ground 12x12").ToArray();
                var open=new bool[Side,Side];
                for(int x=0;x<Side;x++)
                    for(int z=0;z<Side;z++)
                    {
                        var body=new Bounds(Point(x,z),new Vector3(.69f,1.77f,.69f));
                        open[x,z]=!solids.Any(c=>c.bounds.Intersects(body));
                    }
                bool HasRoute(Vector3 start,Vector3 end)
                {
                    var a=Cell(start);var b=Cell(end);
                    if(!open[a.Item1,a.Item2]||!open[b.Item1,b.Item2])return false;
                    var q=new Queue<(int x,int z)>();
                    var seen=new bool[Side,Side];
                    q.Enqueue((a.Item1,a.Item2));seen[a.Item1,a.Item2]=true;
                    while(q.Count>0)
                    {
                        var c=q.Dequeue();
                        if(c.x==b.Item1 && c.z==b.Item2)return true;
                        foreach(var offset in new[]{(1,0),(-1,0),(0,1),(0,-1)})
                        {
                            int nx=c.x+offset.Item1,nz=c.z+offset.Item2;
                            if(nx<0||nx>=Side||nz<0||nz>=Side ||
                               seen[nx,nz]||!open[nx,nz])continue;
                            seen[nx,nz]=true;q.Enqueue((nx,nz));
                        }
                    }
                    return false;
                }
                var a=Chase.ChaseMatch.ChaserStart;
                var b=Chase.ChaseMatch.EvaderStart;
                Assert.IsTrue(HasRoute(a,b),"No standing-body route between competitors");
                Assert.IsTrue(HasRoute(a,new Vector3(.0f,.9f,5.39f)),
                    "Northern bypass not reachable");
                Assert.IsTrue(HasRoute(b,new Vector3(0f,.9f,-5.39f)),
                    "Southern bypass not reachable");
            }
            finally {if(!loaded)EditorSceneManager.CloseScene(s,true);}
        }
    }
}
