using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chase;

// Nonphysical competition markings: repeated starts, clear for both full-size
// Chibi heads and certified modular obstacle colliders. Not new gameplay rules.
public static class ChaseArenaStartPlates
{
    public const string Group="ARENA 01 / fixed role-entry plates";
    const string MaterialFolder="Assets/Materials/ArenaV1/";
    public const string West="CHASER / WEST ENTRY";
    public const string East="EVADER / EAST ENTRY";

    [MenuItem("Chase/Arena/Refresh Fixed Side Entries")]
    public static void Refresh()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Edit Mode required");
        var scene=SceneManager.GetSceneByPath(ChaseArenaV1Builder.ScenePath);
        bool already=scene.IsValid()&&scene.isLoaded;
        if(!already)scene=EditorSceneManager.OpenScene(
            ChaseArenaV1Builder.ScenePath,OpenSceneMode.Additive);
        try
        {
            foreach(var existing in scene.GetRootGameObjects()
                .Where(go=>go.name==Group).ToArray())
                UnityEngine.Object.DestroyImmediate(existing);
            var root=new GameObject(Group);
            SceneManager.MoveGameObjectToScene(root,scene);
            Create(root.transform,West,ChaseMatch.ChaserStart,
                "G-Papaya.mat");
            Create(root.transform,East,ChaseMatch.EvaderStart,
                "C-Sky-Deck.mat");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally {if(!already)EditorSceneManager.CloseScene(scene,true);}
        Validate();
    }

    static void Create(Transform parent,string label,Vector3 body,string color)
    {
        Material surface=AssetDatabase.LoadAssetAtPath<Material>(
            MaterialFolder+color);
        if(surface==null)throw new InvalidOperationException("Missing "+color);
        var plate=new GameObject(label);
        plate.transform.SetParent(parent,false);
        plate.transform.localPosition=new Vector3(body.x,0,body.z);
        // Thin inset stripes rather than a raised physical platform. The
        // marked start is a specific side, never a mandatory movement path.
        for(int z=-1;z<=1;z+=2)
        {
            var stripe=GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name="Painted side line / visual only";
            stripe.transform.SetParent(plate.transform,false);
            stripe.transform.localPosition=new Vector3(0,.007f,z*.46f);
            stripe.transform.localScale=new Vector3(1.38f,.012f,.08f);
            stripe.GetComponent<Renderer>().sharedMaterial=surface;
            UnityEngine.Object.DestroyImmediate(stripe.GetComponent<Collider>());
        }
        var outer=GameObject.CreatePrimitive(PrimitiveType.Cube);
        outer.name="Painted edge / visual only";
        outer.transform.SetParent(plate.transform,false);
        outer.transform.localPosition=new Vector3(0,.007f,0);
        outer.transform.localScale=new Vector3(.07f,.012f,.92f);
        outer.GetComponent<Renderer>().sharedMaterial=surface;
        UnityEngine.Object.DestroyImmediate(outer.GetComponent<Collider>());
    }

    public static void Validate()
    {
        var scene=SceneManager.GetSceneByPath(ChaseArenaV1Builder.ScenePath);
        bool already=scene.IsValid()&&scene.isLoaded;
        if(!already)scene=EditorSceneManager.OpenScene(
            ChaseArenaV1Builder.ScenePath,OpenSceneMode.Additive);
        try
        {
            var root=scene.GetRootGameObjects().Single(g=>g.name==Group);
            if(root.transform.childCount!=2)
                throw new InvalidOperationException("Both fixed starts required");
            foreach(var side in new[]{
                (name:West,place:ChaseMatch.ChaserStart),
                (name:East,place:ChaseMatch.EvaderStart)
            })
            {
                var t=root.transform.Cast<Transform>()
                    .Single(c=>c.name==side.name);
                var p=new Vector3(side.place.x,0,side.place.z);
                if(Vector3.Distance(t.position,p)>.001f ||
                   t.childCount!=3 ||
                   t.GetComponentsInChildren<Collider>(true).Length!=0)
                    throw new InvalidOperationException(
                        "Fixed start plate geometry drift: "+side.name);
            }
            Debug.Log("CHASE_FIXED_SIDE_STARTS_PASS chaser="+
                ChaseMatch.ChaserStart+" evader="+ChaseMatch.EvaderStart);
        }
        finally {if(!already)EditorSceneManager.CloseScene(scene,true);}
    }
}
