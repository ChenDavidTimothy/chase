using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chase;

// Explicitly separate from SampleScene and never included in production builds.
public static class ChaseMovementGymBuilder
{
    const string Path="Assets/Scenes/MovementGym.unity";
    static Material GetMaterial(string filename)=>
        AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/"+filename+".mat");
    static GameObject Box(string name,Transform parent,Vector3 position,
        Vector3 size,Material mat,bool solid=true)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);
        o.name=name;o.transform.SetParent(parent,false);
        o.transform.localPosition=position;o.transform.localScale=size;
        if(mat!=null)o.GetComponent<Renderer>().sharedMaterial=mat;
        if(!solid)Object.DestroyImmediate(o.GetComponent<Collider>());
        return o;
    }
    static void Passage(Transform root,string name,float x,float depth)
    {
        var group=new GameObject(name);group.transform.SetParent(root,false);
        group.transform.position=new Vector3(x,0,0);
        var roof=Box("Solid overhead canopy, bottom 1.64m",group.transform,
            new Vector3(0,1.715f,0),new Vector3(2.3f,.15f,depth),GetMaterial("Shelf"));
        roof.AddComponent<ChaseOverheadPassage>();
        roof.AddComponent<ChaseOccluder>();
        foreach(float offset in new[]{-1.06f,1.06f})
        {
            var support=Box("Full-height solid support",group.transform,
                new Vector3(offset,.855f,0),new Vector3(.18f,1.71f,depth),GetMaterial("Pillar"));
            support.AddComponent<ChaseBlocker>();
        }
    }
    [MenuItem("Chase/Create Separate Movement Gym")]
    public static void CreateGym()
    {
        if(System.IO.File.Exists(Path) &&
            !EditorUtility.DisplayDialog("Replace movement gym?",
                "An authored gym already exists. Replace it?","Replace","Cancel"))return;
        string current=SceneManager.GetActiveScene().path;
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("MOVEMENT GYM  /  independent test facility");
        Box("Measurement ground 25 x 26",root.transform,new Vector3(0,-.13f,0),
            new Vector3(25,.26f,26),GetMaterial("Ground"));
        for(int x=-10;x<=10;x+=5)
        {
            Box("Lane x "+x,root.transform,new Vector3(x,.002f,0),
                new Vector3(.025f,.004f,24f),GetMaterial("Accent"),false);
        }
        for(int z=-10;z<=10;z+=1)
        {
            Box("Meter marker "+z,root.transform,new Vector3(0,.003f,z),
                new Vector3(23f,.005f,.018f),GetMaterial("Stripe"),false);
        }
        var hurdle=Box("JUMP / solid 0.35m hurdle",root.transform,new Vector3(-5,.175f,0),
            new Vector3(2f,.35f,.20f),GetMaterial("Hurdle"));
        hurdle.AddComponent<ChaseHurdle>();
        var wall=Box("WALL / blocking 2.4m barrier",root.transform,
            new Vector3(0,1.2f,9f),new Vector3(5f,2.4f,.35f),GetMaterial("Barrier"));
        wall.AddComponent<ChaseBlocker>();
        Passage(root.transform,"SLIDE / 0.7m deep passage, 1.64m clearance",5,.7f);
        Passage(root.transform,"CROUCH / 2.2m deep passage, 1.64m clearance",9,2.2f);
        Box("LEAP / clean 9m testing corridor",root.transform,
            new Vector3(0,.001f,-2f),new Vector3(.05f,.003f,16f),GetMaterial("Accent"),false);
        var camObj=new GameObject("Main Camera");camObj.tag="MainCamera";
        var cam=camObj.AddComponent<Camera>();cam.fieldOfView=55f;
        cam.clearFlags=CameraClearFlags.SolidColor;
        cam.backgroundColor=new Color(.055f,.09f,.14f);
        cam.transform.rotation=Quaternion.Euler(55,45,0);
        cam.transform.position=-cam.transform.forward*27f;
        camObj.AddComponent<AudioListener>();
        var light=new GameObject("Directional Light").AddComponent<Light>();
        light.type=LightType.Directional;light.intensity=1.4f;
        light.transform.rotation=Quaternion.Euler(55,-30,0);
        var spawn=new GameObject("TEST SPAWN / move to a lane");
        spawn.transform.position=new Vector3(-9,.9f,-8f);
        var systems=new GameObject("GYM / test systems");
        var gym=systems.AddComponent<ChaseMovementGym>();
        var serialized=new SerializedObject(gym);
        serialized.FindProperty("spawn").objectReferenceValue=spawn.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene,Path))
            throw new System.Exception("Gym scene save failed");
        if(!string.IsNullOrEmpty(current) && current!=Path)
            EditorSceneManager.OpenScene(current,OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log("CHASE_V04_MOVEMENT_GYM_CREATED: "+Path);
    }
}
