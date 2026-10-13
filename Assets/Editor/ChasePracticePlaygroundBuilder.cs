using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chase;

// Authored public playground, intentionally not the developer measurement Gym.
// All traversal is ordinary production Prefab/ChaseMotor/PhysX.
public static class ChasePracticePlaygroundBuilder
{
    public const string ScenePath="Assets/Scenes/PracticePlayground.unity";
    public const string Group="CHASE PRACTICE / authored playground";
    public static readonly ChaseArenaV1Builder.Placement[] Layout={
        new ChaseArenaV1Builder.Placement("UnderOverRail",-5f,-1.6f),
        new ChaseArenaV1Builder.Placement("UnderOverBench",5f,-1.6f),
        new ChaseArenaV1Builder.Placement("LowHurdle",0f,-1.6f),
        new ChaseArenaV1Builder.Placement("LowDeck",-5f,5.0f),
        new ChaseArenaV1Builder.Placement("TurnFrame",0f,5f),
        new ChaseArenaV1Builder.Placement("AerialSlot",5f,5f)
    };

    static Material Mat(string id)=>
        AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/ArenaV1/"+id+".mat");
    static Transform Node(string name,Transform parent,Vector3 position)
    {
        var t=new GameObject(name).transform;
        t.SetParent(parent,false);t.localPosition=position;return t;
    }
    static Transform Cube(string name,Transform parent,Vector3 at,
        Vector3 scale,Material material,bool solid)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name=name;
        go.transform.SetParent(parent,false);
        go.transform.localPosition=at;
        go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=material;
        if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go.transform;
    }

    [MenuItem("Chase/Create Player Practice Playground")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build playground in Edit mode");
        if(System.IO.File.Exists(ScenePath) &&
           !EditorUtility.DisplayDialog("Rebuild practice playground?",
               "Replace the authored public practice scene, preserving the developer Gym?",
               "Rebuild","Cancel"))return;
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var previous=SceneManager.GetActiveScene().path;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        try
        {
            var root=Node(Group,null,Vector3.zero);
            var groundMat=Mat("A-Deep-Slate");
            var frame=Mat("B-Soft-Steel");
            foreach(var material in new[]{groundMat,frame})
                if(material==null)throw new InvalidOperationException(
                    "Missing authoritative Arena V1 material. Build production library first.");

            Cube("SOLID FLOOR / 24 x 24",root,new Vector3(0,-.16f,0),
                new Vector3(24f,.32f,24f),groundMat,true);
            // Bounded practice floor, real side walls but no lethal penalty.
            foreach(int side in new[]{-1,1})
            {
                Cube("West-east guard / "+side,root,new Vector3(side*11.95f,.45f,0),
                    new Vector3(.10f,.90f,23.9f),frame,true);
                Cube("North-south guard / "+side,root,new Vector3(0,.45f,side*11.95f),
                    new Vector3(23.9f,.90f,.10f),frame,true);
            }

            var obstacles=Node("PRACTICE / certified production obstacles",root,Vector3.zero);
            foreach(var placement in Layout)
            {
                string path=ChaseArenaV1Builder.Folder+"/"+placement.prefab+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null)throw new InvalidOperationException("Missing production prefab: "+path);
                var instance=PrefabUtility.InstantiatePrefab(prefab,scene) as GameObject;
                instance.transform.SetParent(obstacles,false);
                instance.transform.localPosition=placement.at;
                instance.transform.localRotation=Quaternion.Euler(0,placement.yaw,0);
                instance.transform.localScale=Vector3.one;
                instance.name="PRACTICE / "+placement.prefab;
            }

            // Unmarked continuous free-roam floor. Obstacles, not staged
            // arrows or teleport pads, suggest whatever route players invent.

            var spawn=Node("PRACTICE / FREE START",root,new Vector3(0,.9f,-8.5f));
            var camera=new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag="MainCamera";
            camera.fieldOfView=53f;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.057f,.14f,.18f);
            camera.transform.rotation=Quaternion.Euler(53f,0f,0f);
            camera.transform.position=spawn.position-camera.transform.forward*9.7f;
            camera.gameObject.AddComponent<AudioListener>();
            var light=new GameObject("Directional Light").AddComponent<Light>();
            light.type=LightType.Directional;
            light.intensity=1.45f;
            light.transform.rotation=Quaternion.Euler(55f,-30f,0f);
            var systems=new GameObject("PRACTICE / player session");
            systems.transform.SetParent(root,false);
            var controller=systems.AddComponent<ChasePracticePlayground>();
            var props=new SerializedObject(controller);
            props.FindProperty("start").objectReferenceValue=spawn;
            props.FindProperty("practiceCamera").objectReferenceValue=camera;
            props.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene,ScenePath))
                throw new InvalidOperationException("Unable to save public practice scene");
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if(!string.IsNullOrEmpty(previous) && previous!=ScenePath)
                EditorSceneManager.OpenScene(previous,OpenSceneMode.Single);
        }
        Debug.Log("CHASE_PUBLIC_PRACTICE_AUTHORED: "+ScenePath);
    }
}
