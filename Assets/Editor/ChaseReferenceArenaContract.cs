using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chase;

// Release-independent guard for the experimental comparison scene.
// Production's nine-prefab contract is never modified to accept this layout.
public static class ChaseReferenceArenaContract
{
    const float Limit=5.84f;
    const float PenetrationTolerance=.012f;

    [MenuItem("Chase/Arena/Validate Reference Trial")]
    public static void Validate()
    {
        string path=ChaseReferenceArenaBuilder.ScenePath;
        var scene=SceneManager.GetSceneByPath(path);
        bool wasOpen=scene.IsValid()&&scene.isLoaded;
        if(!wasOpen)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            var arena=scene.GetRootGameObjects().Single(o=>o.name.StartsWith("ARENA  /"));
            var groups=arena.transform.Cast<Transform>().ToArray();
            var group=groups.SingleOrDefault(t=>
                t.name==ChaseReferenceArenaBuilder.GroupName);
            if(group==null||group.childCount!=ChaseReferenceArenaBuilder.Layout.Length)
                throw new InvalidOperationException("Trial missing correct authored modules");
            if(groups.Any(t=>t.name==ChaseArenaV1Builder.GroupName))
                throw new InvalidOperationException("Trial still has original Arena 01 obstacle group");
            if(scene.GetRootGameObjects().SelectMany(
                o=>o.GetComponentsInChildren<ChaseMatch>(true)).Count()!=1)
                throw new InvalidOperationException("Trial must retain the real match");
            if(arena.GetComponentsInChildren<Transform>(true).Any(t=>
                t.name.StartsWith("Floor marker X ") ||
                t.name.StartsWith("Floor marker Z ")))
                throw new InvalidOperationException("Trial must retain clean floor");
            var floor=arena.GetComponentsInChildren<BoxCollider>(true)
                .FirstOrDefault(c=>c.gameObject.name=="Ground 12x12");
            if(floor==null||floor.isTrigger || !floor.enabled)
                throw new InvalidOperationException("Missing 12 x 12 solid playing floor");

            var modules=group.Cast<Transform>().ToArray();
            var colliders=new Collider[modules.Length][];
            for(int i=0;i<modules.Length;i++)
            {
                var t=modules[i]; var spec=ChaseReferenceArenaBuilder.Layout[i];
                string folder=spec.prefab=="UnderOverRail"||spec.prefab=="UnderOverBench"?
                    ChaseArenaV1Builder.Folder:ChaseReferenceArenaBuilder.Folder;
                var source=PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if(AssetDatabase.GetAssetPath(source)!=folder+"/"+spec.prefab+".prefab")
                    throw new InvalidOperationException("Broken trial source prefab: "+spec.prefab);
                if(t.localScale!=Vector3.one || Vector3.Distance(t.localPosition,spec.position)>.003f
                    || Mathf.Abs(Mathf.DeltaAngle(t.localEulerAngles.y,spec.yaw))>.05f)
                    throw new InvalidOperationException("Trial layout position drift: "+spec.prefab);
                colliders[i]=t.GetComponentsInChildren<Collider>(true);
                if(colliders[i].Length==0)
                    throw new InvalidOperationException("Nonphysical trial module: "+t.name);
                foreach(var c in colliders[i])
                {
                    if(!(c is BoxCollider) || c.isTrigger || !c.enabled)
                        throw new InvalidOperationException("Invalid solid: "+c.name);
                    if(c.GetComponent<ChaseBlocker>()==null &&
                       c.GetComponent<ChaseHurdle>()==null &&
                       c.GetComponent<ChaseOverheadPassage>()==null)
                        throw new InvalidOperationException("Unclassified solid: "+c.name);
                    Bounds b=c.bounds;
                    if(Mathf.Abs(b.min.x)>Limit || Mathf.Abs(b.max.x)>Limit ||
                       Mathf.Abs(b.min.z)>Limit || Mathf.Abs(b.max.z)>Limit)
                        throw new InvalidOperationException("Out-of-bounds trial collision: "+c.name);
                    foreach(var start in new[]{ChaseMatch.ChaserStart,ChaseMatch.EvaderStart})
                    {
                        var body=new Bounds(start,new Vector3(.70f,1.80f,.70f));
                        if(c.bounds.Intersects(body))
                            throw new InvalidOperationException("Trial collides with role spawn: "+c.name);
                    }
                }
                foreach(var r in t.GetComponentsInChildren<Renderer>(true))
                    if(r.GetComponent<Collider>()!=null)
                        throw new InvalidOperationException("Visible trial art is a physical collider: "+r.name);
            }
            Physics.SyncTransforms();
            for(int i=0;i<modules.Length;i++)
                for(int j=i+1;j<modules.Length;j++)
                    foreach(var a in colliders[i])
                        foreach(var b in colliders[j])
                        {
                            if(!a.bounds.Intersects(b.bounds))continue;
                            if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,
                                b,b.transform.position,b.transform.rotation,
                                out _,out float depth) && depth>PenetrationTolerance)
                                throw new InvalidOperationException("Trial modules overlap: "+
                                    modules[i].name+" / "+modules[j].name+
                                    " at "+a.name+" / "+b.name+" depth "+depth);
                        }
            // Baseline existing production scene remains an independently certified asset.
            // The reference trial deliberately does NOT change source movement or rig tuning.
            Debug.Log("CHASE_REFERENCE_TRIAL_CONTRACT_PASS: "+
                modules.Length+" linked physical modules, clear starts, no overlaps");
        }
        finally { if(!wasOpen)EditorSceneManager.CloseScene(scene,true); }
    }
}
