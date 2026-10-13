using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chase;

// Fail the release if authored geometry drifts away from tested modules.
// The library is prefabs, not the set of ad-hoc children in a scene.
public static class ChaseArenaV1Contract
{
    static void Near(float actual,float expected,string what)
    {
        if(Mathf.Abs(actual-expected)>.003f)
            throw new InvalidOperationException("CHASE geometry mismatch "+what+
                ": "+actual+" expected "+expected);
    }
    static Transform Descendant(Transform root,string fragment) =>
        root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t=>t.name==fragment);
    static void CheckPart(GameObject obj,string part,Vector3 p,Vector3 size)
    {
        var t=Descendant(obj.transform,part);
        if(t==null)throw new InvalidOperationException(obj.name+" missing "+part);
        var c=t.GetComponent<BoxCollider>();
        if(c==null||!c.enabled||c.isTrigger)
            throw new InvalidOperationException(obj.name+" non-solid "+part);
        Near(t.localPosition.x,p.x,part+" x");
        Near(t.localPosition.y,p.y,part+" y");
        Near(t.localPosition.z,p.z,part+" z");
        Near(c.size.x,size.x,part+" width");
        Near(c.size.y,size.y,part+" height");
        Near(c.size.z,size.z,part+" depth");
    }
    static void ValidatePrefab(string name)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(
            ChaseArenaV1Builder.Folder+"/"+name+".prefab");
        if(asset==null)throw new InvalidOperationException("Missing arena prefab "+name);
        var physics=asset.GetComponentsInChildren<Collider>(true);
        if(physics.Length==0||physics.Any(c=>c.isTrigger||!c.enabled))
            throw new InvalidOperationException(name+" missing solid physics");
        // Every visible mesh must have NO collider: visual fidelity can be
        // improved later without silently altering the playable shape.
        foreach(var r in asset.GetComponentsInChildren<Renderer>(true))
            if(r.GetComponent<Collider>()!=null)
                throw new InvalidOperationException(name+" decorative mesh is physical "+r.name);
        foreach(var c in physics)
        {
            var tags=c.GetComponents<MonoBehaviour>();
            if(!tags.Any(t=>t is ChaseBlocker||t is ChaseHurdle||t is ChaseOverheadPassage))
                throw new InvalidOperationException(name+" unclassified physical part "+c.name);
        }
        switch(name)
        {
            case "UnderOverRail":
                CheckPart(asset,"Crossing beam / stand-blocking solid",
                    new Vector3(0,.9f,0),new Vector3(2.3f,.08f,.26f));
                break;
            case "UnderOverBench":
                CheckPart(asset,"Crossing beam / stand-blocking solid",
                    new Vector3(0,.885f,0),new Vector3(1.7f,.07f,.34f));
                break;
            case "AerialSlot":
                CheckPart(asset,"Ground sill / solid",new Vector3(0,.275f,0),
                    new Vector3(2.2f,.55f,.6f));
                CheckPart(asset,"Overhead tier / solid",new Vector3(0,2.025f,0),
                    new Vector3(2.2f,.35f,.6f));
                break;
            case "LowCanopy":
                CheckPart(asset,"Thin overhead / solid",new Vector3(0,1.715f,0),
                    new Vector3(2.2f,.15f,.55f));
                break;
            case "LowHurdle":
                CheckPart(asset,"Jumpable low body / solid",
                    new Vector3(0,.19f,0),new Vector3(1.9f,.38f,.2f));
                break;
            case "InclineRun":
                CheckPart(asset,"Inclined walking plane / solid",Vector3.zero,
                    new Vector3(1.9f,.14f,2.2f));
                break;
            case "OffsetGates":
                // Blender CHASE_Leap has an animated 1.182m-wide pose,
                // even though the narrow physical capsule is 0.86m.
                // A centered dive through the gate therefore needs real
                // visual width and margin, not just a collider-sized slot.
                foreach(int lane in new[]{-1,1})
                {
                    var gate=Descendant(asset.transform,"Gate "+lane);
                    if(gate==null)throw new InvalidOperationException("Missing offset gate");
                    var left=Descendant(gate,"Gate pillar -0.75 / solid");
                    var right=Descendant(gate,"Gate pillar 0.75 / solid");
                    if(left==null||right==null)
                        throw new InvalidOperationException("Missing physical gate posts");
                    float width=right.localPosition.x-left.localPosition.x-
                        (left.GetComponent<BoxCollider>().size.x+
                         right.GetComponent<BoxCollider>().size.x)*.5f;
                    if(width<1.36f || width-1.182f<.15f)
                        throw new InvalidOperationException(
                            "Gate too narrow for Chibi's visible flying limbs");
                }
                break;
            case "LowDeck":
                CheckPart(asset,"Podium deck / solid",
                    new Vector3(0,.22f,0),new Vector3(1.85f,.44f,1.25f));
                break;
        }
    }
    public static void ValidateGym()
    {
        const string path="Assets/Scenes/MovementGym.unity";
        var scene=SceneManager.GetSceneByPath(path);
        bool alreadyLoaded=scene.IsValid()&&scene.isLoaded;
        if(!alreadyLoaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            var root=scene.GetRootGameObjects()
                .Single(x=>x.name.StartsWith("MOVEMENT GYM  /"));
            if(root.transform.Cast<Transform>().Any(t=>
                t.name=="MODULAR OBSTACLES / reusable prefabs"))
                throw new InvalidOperationException("Obsolete Gym fixture group remains");
            var group=root.transform.Cast<Transform>().SingleOrDefault(t=>
                t.name=="GYM / certified production crossings");
            if(group==null||group.childCount!=2)
                throw new InvalidOperationException("Missing verified Gym crossings");
            foreach(var spec in new[]{
                new{index=0,name="UnderOverRail",x=-8.5f,z=2f},
                new{index=1,name="UnderOverBench",x=-8.5f,z=-4.5f}
            })
            {
                var t=group.GetChild(spec.index);
                if(AssetDatabase.GetAssetPath(
                    PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject))
                    !=ChaseArenaV1Builder.Folder+"/"+spec.name+".prefab")
                    throw new InvalidOperationException("Gym references old prefab "+t.name);
                Near(t.localPosition.x,spec.x,"Gym "+spec.name+" X");
                Near(t.localPosition.z,spec.z,"Gym "+spec.name+" Z");
                Near(t.localEulerAngles.y,45f,"Gym crossing yaw");
                if(t.localScale!=Vector3.one)
                    throw new InvalidOperationException("Scaled Gym crossing");
            }
        }
        finally {if(!alreadyLoaded)EditorSceneManager.CloseScene(scene,true);}
        Debug.Log("CHASE_GYM_PRODUCTION_FIXTURES_PASS");
    }

    public static void Validate()
    {
        foreach(var spec in ChaseArenaV1Builder.Layout.Select(p=>p.prefab).Distinct())
            ValidatePrefab(spec);
        var scene=SceneManager.GetSceneByPath(ChaseArenaV1Builder.ScenePath);
        bool wasOpen=scene.IsValid()&&scene.isLoaded;
        if(!wasOpen)scene=EditorSceneManager.OpenScene(
            ChaseArenaV1Builder.ScenePath,OpenSceneMode.Additive);
        try
        {
            var arena=scene.GetRootGameObjects().Single(o=>o.name.StartsWith("ARENA  /"));
            var root=arena.transform.Cast<Transform>()
                .SingleOrDefault(t=>t.name==ChaseArenaV1Builder.GroupName);
            if(root==null||root.childCount!=ChaseArenaV1Builder.Layout.Length)
                throw new InvalidOperationException("Missing complete production arena");
            var found=root.Cast<Transform>().ToArray();
            for(int i=0;i<found.Length;i++)
            {
                var t=found[i];
                var spec=ChaseArenaV1Builder.Layout[i];
                var actual=PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                if(AssetDatabase.GetAssetPath(actual)!=
                    ChaseArenaV1Builder.Folder+"/"+spec.prefab+".prefab")
                    throw new InvalidOperationException("Broken obstacle prefab link "+t.name);
                if(t.localScale!=Vector3.one)
                    throw new InvalidOperationException("Nonunit obstacle scale "+t.name);
                Near(t.localPosition.x,spec.at.x,t.name+" position x");
                Near(t.localPosition.z,spec.at.z,t.name+" position z");
                Near(t.localEulerAngles.y,spec.yaw,t.name+" rotation");
            }
            // A module must remain reusable at a different placement. Do not
            // conceal interpenetrations with decorative art or rely on PhysX
            // depenetration to create a route at runtime.
            Physics.SyncTransforms();
            for(int i=0;i<found.Length;i++)
                for(int j=i+1;j<found.Length;j++)
                    foreach(var first in found[i].GetComponentsInChildren<Collider>(true))
                        foreach(var second in found[j].GetComponentsInChildren<Collider>(true))
                        {
                            if(!first.bounds.Intersects(second.bounds))continue;
                            if(Physics.ComputePenetration(first,
                                first.transform.position,first.transform.rotation,
                                second,second.transform.position,second.transform.rotation,
                                out _,out float depth)&&depth>.008f)
                                throw new InvalidOperationException(
                                    "Overlapping placed modules "+found[i].name+
                                    " / "+found[j].name+" depth="+depth);
                        }
            // Reserve full standing takeoff volumes for the two central
            // approaches and the viable aerial-slot approach. Actual timed
            // traversal is independently verified in PhysX tests.
            foreach(var station in new[]{new Vector3(0,.9f,-2.7f),
                new Vector3(0,.9f,2.7f),
                new Vector3(-3.55f,.9f,-2.15f),
                new Vector3(-3.55f,.9f,3.25f),
                new Vector3(3.35f,.9f,5.15f)})
            {
                var body=new Bounds(station,new Vector3(.7f,1.8f,.7f));
                foreach(var c in root.GetComponentsInChildren<Collider>(true))
                    if(c.bounds.Intersects(body))
                        throw new InvalidOperationException(
                            "Blocked dive approach "+station+" / "+c.name);
            }
            string[] forbidden={"Tall barrier A","Tall barrier B","Pillar A",
                "Pillar B","Hurdle A","Hurdle B","Southwest Superman shelf slot",
                "Northeast low passage","MODULAR OBSTACLES / reusable prefabs"};
            foreach(var t in arena.transform.Cast<Transform>())
                if(forbidden.Contains(t.name))
                    throw new InvalidOperationException("Legacy arena obstacle "+t.name);
            if(scene.GetRootGameObjects().SelectMany(
                g=>g.GetComponentsInChildren<ChaseMatch>(true)).Count()!=1)
                throw new InvalidOperationException("Missing single production Match");
            // Non-colliding role start marks are part of the authored
            // competition presentation, never obstacle geometry.
            ChaseArenaStartPlates.Validate();
            // A layout cannot place a nonfloor collider through any spawn.
            foreach(var start in new[]{ChaseMatch.ChaserStart,ChaseMatch.EvaderStart})
            {
                // Both permanently marked side entries must fit a full standing body.
                var footprint=new Bounds(new Vector3(start.x,.9f,start.z),
                    new Vector3(.70f,1.8f,.70f));
                foreach(var c in root.GetComponentsInChildren<Collider>(true))
                    if(c.bounds.Intersects(footprint))
                        throw new InvalidOperationException(
                            "Arena obstacle overlaps spawn "+start+" / "+c.name);
            }
        }
        finally {if(!wasOpen)EditorSceneManager.CloseScene(scene,true);}
        Debug.Log("CHASE_ARENA_V1_CONTRACT_PASS: 9 prefabs, placements, spawn safety");
    }
}
