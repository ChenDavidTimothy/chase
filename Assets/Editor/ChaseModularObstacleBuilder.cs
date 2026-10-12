using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chase;

// Additive, idempotent prefab authoring. Unlike ChaseSceneBuilder this tool
// never deletes, regenerates or otherwise edits the existing arena obstacles.
// A module is a root transform plus ordinary solid colliders: no route-specific
// triggers, scripts, animation controls, movement modifiers or prefab rotations.
public static class ChaseModularObstacleBuilder
{
    const string Folder = "Assets/Obstacles";
    public const string Rail = Folder + "/UnderOverRail.prefab";
    public const string Bench = Folder + "/UnderOverBench.prefab";
    const string Gym = "Assets/Scenes/MovementGym.unity";

    struct Specification
    {
        public string name;
        public float width, depth, underside, top, supportHeight;
        public Specification(string n,float w,float d,float bottom,float upper,float support)
        { name=n;width=w;depth=d;underside=bottom;top=upper;supportHeight=support; }
    }

    // Rail and short shallow bench are distinct footprints, but they obey
    // the same movement envelopes and use exactly the same shared motor.
    static readonly Specification RailShape =
        new Specification("Under-over rail",2.30f,.26f,.86f,.94f,1.20f);
    static readonly Specification BenchShape =
        new Specification("Under-over narrow bench",1.70f,.34f,.85f,.92f,1.23f);

    // Transform.Find parses slashes as hierarchy paths; our descriptive
    // module names contain slashes, so always match immediate child names.
    static Transform Child(Transform parent,string name) =>
        parent.Cast<Transform>().FirstOrDefault(t=>t.name==name);

    static Material Material(string name)
    {
        Material material=AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/"+name+".mat");
        if(material==null)throw new InvalidOperationException(
            "Missing existing CHASE material: "+name);
        return material;
    }

    static GameObject Solid(string name,Transform parent,Vector3 center,Vector3 size,
        Material material,bool support=false,bool overhead=false)
    {
        GameObject cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name=name;cube.transform.SetParent(parent,false);
        cube.transform.localPosition=center;
        cube.transform.localScale=size;
        cube.GetComponent<MeshRenderer>().sharedMaterial=material;
        if(support)cube.AddComponent<ChaseBlocker>();
        if(overhead)cube.AddComponent<ChaseOverheadPassage>();
        if(support||overhead)cube.AddComponent<ChaseOccluder>();
        return cube;
    }

    static void Decorative(string name,Transform parent,Vector3 center,
        Vector3 size,Material material)
    {
        GameObject stripe=Solid(name,parent,center,size,material);
        UnityEngine.Object.DestroyImmediate(stripe.GetComponent<BoxCollider>());
    }

    static GameObject EnsurePrefab(string assetPath,Specification s)
    {
        var found=AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if(found!=null)return found;  // Never overwrite an authored prefab.
        if(!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets","Obstacles");
        GameObject root=new GameObject(s.name);
        try
        {
            Solid("Horizontal solid crossing / under or over",root.transform,
                new Vector3(0,(s.underside+s.top)*.5f,0),
                new Vector3(s.width,s.top-s.underside,s.depth),
                Material("Shelf"),overhead:true);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*(s.width*.5f+.06f);
                Solid(side<0?"Left standing post":"Right standing post",
                    root.transform,new Vector3(x,s.supportHeight*.5f,0),
                    new Vector3(.12f,s.supportHeight,s.depth),
                    Material("Pillar"),support:true);
                // Contrasting caps make the two traversal elevations legible.
                Decorative(side<0?"Left colored cap":"Right colored cap",
                    root.transform,new Vector3(x,s.supportHeight+.015f,0),
                    new Vector3(.14f,.03f,s.depth+.02f),Material("Accent"));
            }
            Decorative("Crossing edge highlight",root.transform,
                new Vector3(0,s.top+.009f,-s.depth*.5f),
                new Vector3(s.width,.016f,.016f),Material("Accent"));
            bool success;
            GameObject saved=PrefabUtility.SaveAsPrefabAsset(root,assetPath,out success);
            if(!success||saved==null)
                throw new InvalidOperationException("Could not create "+assetPath);
            return saved;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static bool Add(Scene scene,GameObject prefab,string name,Vector3 point,float yaw)
    {
        var arena=scene.GetRootGameObjects().FirstOrDefault(
            o=>o.name.StartsWith("ARENA  /")||o.name.StartsWith("MOVEMENT GYM  /"));
        if(arena==null)throw new InvalidOperationException("Unknown CHASE scene: "+scene.path);
        Transform group=Child(arena.transform,"MODULAR OBSTACLES / reusable prefabs");
        bool changed=false;
        if(group==null)
        {
            group=new GameObject("MODULAR OBSTACLES / reusable prefabs").transform;
            group.SetParent(arena.transform,false);
            changed=true;
        }
        var existing=Child(group,name);
        if(existing!=null)
        {
            if(PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject)!=prefab)
                throw new InvalidOperationException(
                    "Conflicting obstacle with same name: "+name);
            return changed;
        }
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
        instance.name=name;
        instance.transform.SetParent(group,false);
        instance.transform.localPosition=point;
        instance.transform.localRotation=Quaternion.Euler(0,yaw,0);
        changed=true;
        return changed;
    }

    static void InstallScene(string path,GameObject rail,GameObject bench)
    {
        Scene loaded=SceneManager.GetSceneByPath(path);
        bool alreadyOpen=loaded.IsValid()&&loaded.isLoaded;
        Scene scene=alreadyOpen?loaded:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            bool changed=false;
            // Legacy calibration modules belong only in the independent Gym.
            // The production arena uses the authored ArenaV1 library instead.
            changed|=Add(scene,rail,"GYM under-over rail",
                new Vector3(-8.5f,0,2f),45);
            changed|=Add(scene,bench,"GYM narrow under-over bench",
                new Vector3(-8.5f,0,-4.5f),45);
            if(changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Failed to save "+path);
            }
            Debug.Log("CHASE_MODULAR_OBSTACLES_INSTALLED: "+path+" changed="+changed);
        }
        finally
        {
            if(!alreadyOpen)EditorSceneManager.CloseScene(scene,true);
        }
    }

    [MenuItem("Chase/Gym/Repair Original Dual-Route Fixtures")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before authoring");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var rail=EnsurePrefab(Rail,RailShape);
        var bench=EnsurePrefab(Bench,BenchShape);
        InstallScene(Gym,rail,bench);
        AssetDatabase.SaveAssets();
        Validate();
    }

    [MenuItem("Chase/Repair Modular Camera Occlusion")]
    public static void RepairCameraOccluders()
    {
        foreach(var path in new[]{Rail,Bench})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if((t.GetComponent<ChaseBlocker>()!=null ||
                        t.GetComponent<ChaseOverheadPassage>()!=null) &&
                        t.GetComponent<ChaseOccluder>()==null)
                        t.gameObject.AddComponent<ChaseOccluder>();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
        Debug.Log("CHASE_MODULAR_CAMERA_OCCLUDERS_REPAIRED");
    }

    static void VerifyPrefab(string path,Specification expected)
    {
        GameObject asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(asset==null)throw new InvalidOperationException("Missing obstacle prefab "+path);
        var beam=Child(asset.transform,"Horizontal solid crossing / under or over");
        if(beam==null)throw new InvalidOperationException("Missing crossing on "+path);
        var collider=beam.GetComponent<BoxCollider>();
        Vector3 size=beam.localScale;
        float underside=beam.localPosition.y-size.y*.5f;
        float top=beam.localPosition.y+size.y*.5f;
        if(collider==null||!collider.enabled||collider.isTrigger||
            Mathf.Abs(size.x-expected.width)>.001f||
            Mathf.Abs(size.z-expected.depth)>.001f||
            Mathf.Abs(underside-expected.underside)>.001f||
            Mathf.Abs(top-expected.top)>.001f)
            throw new InvalidOperationException("Modified solid crossing geometry: "+path);
        if(beam.GetComponent<ChaseOccluder>()==null)
            throw new InvalidOperationException("Missing camera occluder: "+path);
        if(underside<.845f||underside>.91f || top>.96f)
            throw new InvalidOperationException("Unsupported low/high traversal envelope");
        foreach(string post in new[]{"Left standing post","Right standing post"})
        {
            Transform p=Child(asset.transform,post);
            if(p==null||p.GetComponent<BoxCollider>()==null||
                p.GetComponent<BoxCollider>().isTrigger ||
                p.GetComponent<ChaseOccluder>()==null)
                throw new InvalidOperationException("Missing solid support in "+path);
        }
    }

    static void VerifyScene(string path,int expected)
    {
        Scene loaded=SceneManager.GetSceneByPath(path);
        bool alreadyOpen=loaded.IsValid()&&loaded.isLoaded;
        Scene scene=alreadyOpen?loaded:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            var objects=scene.GetRootGameObjects().SelectMany(
                o=>o.GetComponentsInChildren<Transform>(true))
                .Where(t=>t.name=="MODULAR OBSTACLES / reusable prefabs")
                .ToArray();
            if(objects.Length!=1 || objects[0].childCount!=expected)
                throw new InvalidOperationException("Module placement missing: "+path);
            foreach(Transform instance in objects[0])
            {
                GameObject source=PrefabUtility.GetCorrespondingObjectFromSource(
                    instance.gameObject);
                if(source==null|| (AssetDatabase.GetAssetPath(source)!=Rail &&
                    AssetDatabase.GetAssetPath(source)!=Bench))
                    throw new InvalidOperationException("Broken module prefab link: "+path);
                if(instance.localScale!=Vector3.one)
                    throw new InvalidOperationException("Non-unit module scale: "+path);
            }
        }
        finally {if(!alreadyOpen)EditorSceneManager.CloseScene(scene,true);}
    }

    public static void Validate()
    {
        VerifyPrefab(Rail,RailShape);
        VerifyPrefab(Bench,BenchShape);
        VerifyScene(Gym,2);
        Debug.Log("CHASE_MODULAR_OBSTACLE_CONTRACT_PASS: legacy prefab variants in gym");
    }
}
