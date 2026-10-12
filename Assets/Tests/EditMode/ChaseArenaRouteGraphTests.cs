using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chase.Tests
{
    // Conservative standing-body walkability for spectator-relevant bypasses.
    // Movement actions are exercised separately with actual shared-motor PhysX.
    // This test demonstrates counter-routes without scripting the opponent.
    public sealed class ChaseArenaRouteGraphTests
    {
        const int Side=45;
        const float Pitch=.25f;
        const float Start=-5.5f;
        readonly bool[,] open=new bool[Side,Side];

        static Vector3 Point(int x,int z) =>
            new Vector3(Start+x*Pitch,.9f,Start+z*Pitch);

        static (int,int) Cell(Vector3 p) => (
            Mathf.Clamp(Mathf.RoundToInt((p.x-Start)/Pitch),0,Side-1),
            Mathf.Clamp(Mathf.RoundToInt((p.z-Start)/Pitch),0,Side-1));

        void Scan(float effectiveRadius)
        {
            Physics.SyncTransforms();
            for(int x=0;x<Side;x++)for(int z=0;z<Side;z++)
            {
                var p=Point(x,z);
                // A full-size standing capsule with 1 cm inset from floor.
                // The Chibi's physical body cannot pass through a blocked
                // square merely because the visual mesh is thinner.
                open[x,z]=!Physics.CheckCapsule(
                    new Vector3(p.x,effectiveRadius+.01f,p.z),
                    new Vector3(p.x,1.79f-effectiveRadius,p.z),
                    effectiveRadius,
                    ~0,QueryTriggerInteraction.Ignore);
            }
        }

        // Exactly four-neighbor connectivity, so diagonal corner-cutting is
        // not counted as an alternate viable route for a large-headed Chibi.
        int Length(Vector3 a,Vector3 b,Func<Vector3,bool> permitted)
        {
            var start=Cell(a);var goal=Cell(b);
            var queue=new Queue<(int x,int z,int steps)>();
            var seen=new bool[Side,Side];
            if(!open[start.Item1,start.Item2]||!open[goal.Item1,goal.Item2])
                return -1;
            queue.Enqueue((start.Item1,start.Item2,0));
            seen[start.Item1,start.Item2]=true;
            while(queue.Count>0)
            {
                var p=queue.Dequeue();
                if(p.x==goal.Item1&&p.z==goal.Item2)return p.steps;
                foreach(var d in new[]{(1,0),(-1,0),(0,1),(0,-1)})
                {
                    int x=p.x+d.Item1,z=p.z+d.Item2;
                    if(x<0||x>=Side||z<0||z>=Side||seen[x,z]||
                       !open[x,z]||!permitted(Point(x,z)))continue;
                    seen[x,z]=true;queue.Enqueue((x,z,p.steps+1));
                }
            }
            return -1;
        }

        [Test] public void FlagshipArenaHasShortIndependentGroundBypasses()
        {
            const string path="Assets/Scenes/SampleScene.unity";
            var existing=SceneManager.GetSceneByPath(path);
            bool wasOpen=existing.IsValid()&&existing.isLoaded;
            var scene=wasOpen?existing:EditorSceneManager.OpenScene(
                path,OpenSceneMode.Additive);
            try
            {
                foreach(float effectiveRadius in new[]{.345f,.49f})
                {
                Scan(effectiveRadius);
                // Each solid crossing has a high and a low path (verified by
                // real PhysX), PLUS two distinct standing counter-routes
                // around opposite ends. Routes must be short enough to
                // matter in a 20-second match.
                foreach(var crossing in new[]{
                    new {name="Crossline",x=0f,z=0f,left=-1.65f,right=1.65f},
                    new {name="West Switchback",x=-3.55f,z=.55f,
                        left=-4.65f,right=-2.45f}
                })
                {
                    var from=new Vector3(crossing.x,.9f,crossing.z-1.5f);
                    var to=new Vector3(crossing.x,.9f,crossing.z+1.5f);
                    int left=Length(from,to,p=>Mathf.Abs(p.z-crossing.z)>.62f ||
                        p.x<crossing.left);
                    int right=Length(from,to,p=>Mathf.Abs(p.z-crossing.z)>.62f ||
                        p.x>crossing.right);
                    Assert.Greater(left,0,crossing.name+" lacks west counter-route at width "+(effectiveRadius*2));
                    Assert.Greater(right,0,crossing.name+" lacks east counter-route at width "+(effectiveRadius*2));
                    Assert.Less(left*Pitch,14f,crossing.name+" west bypass too long");
                    Assert.Less(right*Pitch,14f,crossing.name+" east bypass too long");
                    Debug.Log("CHASE_ARENA_COUNTERPLAY "+crossing.name+
                        " radius="+effectiveRadius+" left="+
                        (left*Pitch).ToString("F2")+"m right="+
                        (right*Pitch).ToString("F2")+"m");
                }
                }
            }
            finally {if(!wasOpen)EditorSceneManager.CloseScene(scene,true);}
        }

        [Test] public void AllMatchSpawnsRemainStandingConnected()
        {
            const string path="Assets/Scenes/SampleScene.unity";
            var existing=SceneManager.GetSceneByPath(path);
            bool wasOpen=existing.IsValid()&&existing.isLoaded;
            var scene=wasOpen?existing:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                Scan(.345f);
                var pairs=new[]{
                    (new Vector3(-4.6f,.9f,-1.4f),new Vector3(4.6f,.9f,3.9f)),
                    (new Vector3(4.6f,.9f,1.4f),new Vector3(-4.6f,.9f,-3.8f)),
                    (new Vector3(-1.2f,.9f,4.6f),new Vector3(3.8f,.9f,-4.4f)),
                    (new Vector3(-1.5f,.9f,-4.7f),new Vector3(3.2f,.9f,4.4f))
                };
                foreach(var pair in pairs)
                {
                    int steps=Length(pair.Item1,pair.Item2,_=>true);
                    Assert.Greater(steps,0,"Unreachable spawn-to-spawn path");
                    Assert.Less(steps*Pitch,19f,
                        "Excessive mandatory detour in 12m quad");
                }
            }
            finally {if(!wasOpen)EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
