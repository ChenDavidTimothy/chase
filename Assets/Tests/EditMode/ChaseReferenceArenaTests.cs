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
                Assert.AreEqual(23,group.childCount);
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

        [Test] public void CompetitionCannotBeSolvedByStandingOnlyWalking()
        {
            string path="Assets/Scenes/ArenaWCTTrial.unity";
            var s=SceneManager.GetSceneByPath(path);
            bool loaded=s.IsValid()&&s.isLoaded;
            if(!loaded)s=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var root=s.GetRootGameObjects().Single(o=>o.name.StartsWith("ARENA  /"));
                // PhysX capsule clearance, not collider AABBs. A rotated
                // rail's axis-aligned bounds are much larger than the actual
                // solid and falsely report that diagonal escape lanes are
                // sealed. The capsules use the motor's .35m physical radius.
                Physics.SyncTransforms();
                var open=new bool[Side,Side];
                for(int x=0;x<Side;x++)
                    for(int z=0;z<Side;z++)
                    {
                        var position=Point(x,z);
                        open[x,z]=!Physics.CheckCapsule(
                            new Vector3(position.x,.355f,position.z),
                            new Vector3(position.x,1.445f,position.z),
                            .345f,~0,QueryTriggerInteraction.Ignore);
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
                // Explicit skill links connect distinct standing regions.
                Assert.IsFalse(HasRoute(a,b),
                    "A full-body standing run still crosses the entire trial");
                Assert.IsTrue(HasRoute(a,new Vector3(0f,.9f,-5.39f)),
                    "Chaser start lacks a ground-level local escape");
                Assert.IsTrue(HasRoute(b,new Vector3(5.39f,.9f,2.7f)),
                    "Evader start lacks a ground-level local escape");
            }
            finally {if(!loaded)EditorSceneManager.CloseScene(s,true);}
        }
        [Test] public void WallRunningIsInterruptedWithoutSealingTheCourse()
        {
            string path="Assets/Scenes/ArenaWCTTrial.unity";
            var scene=SceneManager.GetSceneByPath(path);
            bool loaded=scene.IsValid()&&scene.isLoaded;
            if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var root=scene.GetRootGameObjects().Single(o=>o.name.StartsWith("ARENA  /"));
                var group=root.GetComponentsInChildren<Transform>(true)
                    .Single(t=>t.name=="CHASE / WCT REFERENCE TRIAL");
                Physics.SyncTransforms();
                // A true standing character near each LONG side wall must
                // encounter more than one physical obstruction to a wall run.
                foreach(float x in new[]{-5.05f,5.05f})
                {
                    int separateBlocks=0;
                    bool previouslyBlocked=false;
                    for(float z=-4.85f;z<=4.85f;z+=.10f)
                    {
                        bool blocked=Physics.CheckCapsule(
                            new Vector3(x,.355f,z),new Vector3(x,1.445f,z),
                            .345f,~0,QueryTriggerInteraction.Ignore);
                        if(blocked && !previouslyBlocked)separateBlocks++;
                        previouslyBlocked=blocked;
                    }
                    Assert.GreaterOrEqual(separateBlocks,2,
                        "A side wall permits one long featureless running circuit: "+x);
                }
            }
            finally {if(!loaded)EditorSceneManager.CloseScene(scene,true);}
        }

        [Test] public void TrialBotCanPlanAcrossRealSlidePassages()
        {
            const string path="Assets/Scenes/ArenaWCTTrial.unity";
            var scene=SceneManager.GetSceneByPath(path);
            bool loaded=scene.IsValid()&&scene.isLoaded;
            if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                // Matches the released ChaseBot coarse grid, conservative
                // radius and classification. A low SOLID roof is a navigable
                // skill link only in the trial; a pillar stays impassable.
                const int n=21;
                const float pitch=.53f;
                var open=new bool[n,n];
                var hits=new Collider[32];
                Physics.SyncTransforms();
                for(int x=0;x<n;x++)for(int z=0;z<n;z++)
                {
                    Vector3 p=new Vector3((x-10)*pitch,0,(z-10)*pitch);
                    bool pass=Mathf.Abs(p.x)<5.5f&&Mathf.Abs(p.z)<5.5f;
                    if(pass)
                    {
                        int count=Physics.OverlapCapsuleNonAlloc(
                            p+Vector3.up*.35f,p+Vector3.up*1.35f,
                            .47f,hits,~0,QueryTriggerInteraction.Ignore);
                        if(count==hits.Length)pass=false;
                        for(int i=0;i<count&&pass;i++)
                            if(hits[i].GetComponent<ChaseBlocker>()!=null &&
                               hits[i].GetComponent<ChaseOverheadPassage>()==null)
                                pass=false;
                    }
                    open[x,z]=pass;
                }
                var q=new Queue<(int x,int z)>();
                var reached=new bool[n,n];
                Assert.IsTrue(open[0,6]&&open[20,14],
                    "Both fixed role entries must be valid bot cells");
                q.Enqueue((0,6));reached[0,6]=true;
                while(q.Count>0)
                {
                    var p=q.Dequeue();
                    foreach(var d in new[]{(1,0),(-1,0),(0,1),(0,-1)})
                    {
                        int x=p.x+d.Item1,z=p.z+d.Item2;
                        if(x<0||x>=n||z<0||z>=n||reached[x,z]||!open[x,z])continue;
                        reached[x,z]=true;q.Enqueue((x,z));
                    }
                }
                Assert.IsTrue(reached[20,14],
                    "Trial bot cannot navigate from its chaser spawn to evader");
            }
            finally {if(!loaded)EditorSceneManager.CloseScene(scene,true);}
        }

        [Test] public void AllEdgeSwitchesPresentSolidDualRoutesAndVariedAxes()
        {
            const string path="Assets/Scenes/ArenaWCTTrial.unity";
            var scene=SceneManager.GetSceneByPath(path);
            bool loaded=scene.IsValid()&&scene.isLoaded;
            if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var root=scene.GetRootGameObjects().Single(o=>o.name.StartsWith("ARENA  /"));
                var group=root.GetComponentsInChildren<Transform>(true)
                    .Single(t=>t.name=="CHASE / WCT REFERENCE TRIAL");
                Assert.GreaterOrEqual(group.Cast<Transform>().Count(t=>
                    Mathf.Abs(Mathf.DeltaAngle(t.localEulerAngles.y,0f))>10f),12,
                    "Scaffold axes are still effectively unidirectional");
                foreach(string side in new[]{"WallSwitchWestNorth","WallSwitchWestSouth",
                    "WallSwitchEastNorth","WallSwitchEastSouth","CrossFork"})
                {
                    var t=group.Cast<Transform>().Single(x=>x.name=="REFERENCE / "+side);
                    var beam=t.GetComponentsInChildren<BoxCollider>(true)
                        .Single(c=>c.name.EndsWith("genuine under-over / solid"));
                    float roofBottom=beam.transform.position.y-beam.size.y*.5f;
                    float roofTop=beam.transform.position.y+beam.size.y*.5f;
                    Assert.Greater(roofBottom,.83f,
                        "Feet-first slide body must actually fit beneath "+side);
                    Assert.Less(roofTop,1.12f,
                        "A committed jump/dive must be physically able to clear "+side);
                    Assert.IsFalse(beam.isTrigger);
                    Assert.NotNull(beam.GetComponent<ChaseOverheadPassage>());
                    Assert.NotNull(beam.GetComponent<ChaseHurdle>());
                }
            }
            finally {if(!loaded)EditorSceneManager.CloseScene(scene,true);}
        }

        [Test] public void BothTowerDecksAreGenuinelyOpenOnFourSides()
        {
            string path="Assets/Scenes/ArenaWCTTrial.unity";
            var scene=SceneManager.GetSceneByPath(path);
            bool loaded=scene.IsValid()&&scene.isLoaded;
            if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var root=scene.GetRootGameObjects().Single(o=>o.name.StartsWith("ARENA  /"));
                var group=root.GetComponentsInChildren<Transform>(true)
                    .Single(t=>t.name=="CHASE / WCT REFERENCE TRIAL");
                Physics.SyncTransforms();
                foreach(string id in new[]{"CrownTower","SouthEastTower"})
                {
                    var tower=group.Cast<Transform>().Single(t=>t.name=="REFERENCE / "+id);
                    Assert.IsFalse(tower.GetComponentsInChildren<Transform>(true)
                        .Any(t=>t.name.Contains("tower color facing")),
                        "Old decorative wall skirt hides the tower entrance");
                    // Character-crawl-sized volume at the center is open,
                    // while a standing body intersects the genuine overhead.
                    var small=new Bounds(new Vector3(tower.position.x,.43f,tower.position.z),
                        new Vector3(.70f,.75f,.70f));
                    var standing=new Bounds(new Vector3(tower.position.x,.9f,tower.position.z),
                        new Vector3(.70f,1.77f,.70f));
                    var cs=tower.GetComponentsInChildren<BoxCollider>(true);
                    Assert.IsFalse(cs.Any(c=>c.bounds.Intersects(small)),
                        id+" has no actual crawl/slide underpass");
                    Assert.IsTrue(cs.Any(c=>c.bounds.Intersects(standing)),
                        id+" would not force a movement or route decision");
                }
            }
            finally {if(!loaded)EditorSceneManager.CloseScene(scene,true);}
        }

    }
}
