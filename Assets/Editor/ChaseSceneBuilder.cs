using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Chase;

public static class ChaseSceneBuilder
{
    static Material Floor, Wall, Barrier, Pillar, Shelf, Hurdle, Accent, Stripe;
    static string folder = "Assets/Materials";
    [MenuItem("Chase/Rebuild Arena")]
    public static void Build()
    {
        // Explicit confirmation protects hand-authored scene edits and assets.
        if (!EditorUtility.DisplayDialog("Rebuild CHASE arena?",
            "This regenerates Assets/Scenes/SampleScene.unity and the CHASE materials. " +
            "Commit or back up any scene changes first. Proceed with overwrite?",
            "Rebuild saved scene", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildConfirmed();
    }

    static void BuildConfirmed()
    {
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets","Materials");
        Floor = MakeMaterial("Ground",new Color(.105f,.17f,.23f));
        Wall = MakeMaterial("Boundary",new Color(.24f,.34f,.45f));
        Barrier = MakeMaterial("Barrier",new Color(.48f,.67f,.77f));
        Pillar = MakeMaterial("Pillar",new Color(.31f,.49f,.62f));
        Shelf = MakeMaterial("Shelf",new Color(.98f,.69f,.29f));
        Hurdle = MakeMaterial("Hurdle",new Color(.82f,.9f,.96f));
        Accent = MakeMaterial("Accent",new Color(.18f,.82f,.71f));
        Stripe = MakeMaterial("Stripe",new Color(.94f,.95f,.87f));
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        GameObject arena = new GameObject("ARENA  /  12 x 12 m");
        MakeCube("Ground 12x12",arena.transform,new Vector3(0,-.13f,0),
            new Vector3(12,.26f,12),Floor,false,false,false,false);
        for (int i=-5;i<=5;i++)
        {
            MakeCube("Floor marker X "+i,arena.transform,new Vector3(i,-.0005f,0),
                new Vector3(.015f,.003f,11.85f),Accent,false,false,false,true);
            MakeCube("Floor marker Z "+i,arena.transform,new Vector3(0,-.0004f,i),
                new Vector3(11.85f,.003f,.015f),Accent,false,false,false,true);
        }
        MakeCube("North wall",arena.transform,new Vector3(0,1.3f,6.15f),
            new Vector3(12.6f,2.6f,.3f),Wall,true,false,false,false);
        MakeCube("South wall",arena.transform,new Vector3(0,1.3f,-6.15f),
            new Vector3(12.6f,2.6f,.3f),Wall,true,false,false,false);
        MakeCube("East wall",arena.transform,new Vector3(6.15f,1.3f,0),
            new Vector3(.3f,2.6f,12.6f),Wall,true,false,false,false);
        MakeCube("West wall",arena.transform,new Vector3(-6.15f,1.3f,0),
            new Vector3(.3f,2.6f,12.6f),Wall,true,false,false,false);
        MakeCube("Tall barrier A",arena.transform,new Vector3(-2.1f,1.15f,-.7f),
            new Vector3(.55f,2.3f,2.5f),Barrier,true,false,false,false);
        MakeCube("Tall barrier B",arena.transform,new Vector3(2.15f,1.15f,.75f),
            new Vector3(.55f,2.3f,2.5f),Barrier,true,false,false,false);
        MakeCube("Pillar A",arena.transform,new Vector3(-3.9f,1.15f,3.25f),
            new Vector3(.85f,2.3f,.85f),Pillar,true,false,false,false);
        MakeCube("Pillar B",arena.transform,new Vector3(3.8f,1.15f,-3f),
            new Vector3(.85f,2.3f,.85f),Pillar,true,false,false,false);
        MakeCube("Hurdle A",arena.transform,new Vector3(-.2f,.175f,-3.5f),
            new Vector3(1.9f,.35f,.2f),Hurdle,false,true,false,false);
        MakeCube("Hurdle B",arena.transform,new Vector3(.3f,.175f,3.5f),
            new Vector3(1.9f,.35f,.2f),Hurdle,false,true,false,false);
        MakeShelf(arena.transform,"Southwest shelf",new Vector3(-3.05f,0,-3.55f));
        MakeShelf(arena.transform,"Northeast shelf",new Vector3(2.8f,0,3.55f));
        foreach (var p in new []{ new Vector3(-4.6f,0,-1.4f),
            new Vector3(4.6f,0,1.4f),new Vector3(-1.2f,0,4.6f),
            new Vector3(-1.5f,0,-4.7f) })
        {
            MakeCube("Spawn marker",arena.transform,p+Vector3.up*.007f,
                new Vector3(.43f,.008f,.43f),Accent,false,false,false,true);
        }
        GameObject mainCamera = new GameObject("Main Camera");
        mainCamera.tag="MainCamera";
        var cam = mainCamera.AddComponent<Camera>();
        cam.fieldOfView=40f;
        cam.clearFlags=CameraClearFlags.SolidColor;
        cam.backgroundColor=new Color(.052f,.085f,.14f);
        mainCamera.AddComponent<AudioListener>();
        mainCamera.transform.rotation=Quaternion.Euler(55f,45f,0f);
        GameObject sun = new GameObject("Directional Light");
        Light light=sun.AddComponent<Light>();
        light.type=LightType.Directional;
        light.intensity=1.4f;
        light.shadows=LightShadows.Soft;
        sun.transform.rotation=Quaternion.Euler(55f,-30f,0f);
        RenderSettings.ambientLight=new Color(.62f,.69f,.79f);
        RenderSettings.fog=false;
        GameObject root=new GameObject("CHASE / Game systems");
        root.AddComponent<ChaseMatch>();
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene,"Assets/Scenes/SampleScene.unity"))
            throw new System.Exception("Scene could not be saved");
        EditorBuildSettings.scenes=new[]{
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity",true)
        };
        AssetDatabase.SaveAssets();
        Debug.Log("CHASE_SCENE_BUILT: fixed arena, walls, obstacles, camera, match and build scene");
    }
    static Material MakeMaterial(string name,Color color)
    {
        string path=folder+"/"+name+".mat";
        Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m==null)
        {
            m=new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m,path);
        }
        m.color=color;
        EditorUtility.SetDirty(m);
        return m;
    }
    static GameObject MakeCube(string name,Transform parent,Vector3 position,Vector3 size,
        Material material,bool blocker,bool hurdle,bool vault,bool decorative)
    {
        GameObject obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name=name;
        obj.transform.SetParent(parent,false);
        obj.transform.localPosition=position;
        obj.transform.localScale=size;
        obj.GetComponent<Renderer>().sharedMaterial=material;
        if (decorative)
            Object.DestroyImmediate(obj.GetComponent<Collider>());
        else
        {
            if (blocker) obj.AddComponent<ChaseBlocker>();
            if (hurdle) obj.AddComponent<ChaseHurdle>();
            if (vault) obj.AddComponent<ChaseVaultSurface>();
            if (blocker || vault) obj.AddComponent<ChaseOccluder>();
        }
        return obj;
    }
    static void MakeShelf(Transform parent,string name,Vector3 p)
    {
        GameObject shelf=new GameObject(name);
        shelf.transform.SetParent(parent,false);
        shelf.transform.localPosition=p;
        MakeCube("Vaultable slab",shelf.transform,new Vector3(0,1.125f,0),
            new Vector3(2.2f,.15f,.55f),Shelf,false,false,true,false);
        foreach (float x in new[]{-1.01f,1.01f})
            MakeCube("Solid support",shelf.transform,new Vector3(x,.6f,0),
                new Vector3(.18f,1.2f,.55f),Pillar,true,false,false,false);
        MakeCube("Shelf edge visibility",shelf.transform,new Vector3(0,1.215f,-.26f),
            new Vector3(2.2f,.025f,.035f),Stripe,false,false,false,true);
        MakeCube("Shelf edge visibility",shelf.transform,new Vector3(0,1.215f,.26f),
            new Vector3(2.2f,.025f,.035f),Stripe,false,false,false,true);
    }
}
