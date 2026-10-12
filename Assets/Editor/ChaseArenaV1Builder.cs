using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chase;

// Production arena, built from ordinary reusable physics. Art is separate,
// collision-free primitive meshwork, never hidden gameplay authority.
// This builder deliberately preserves the saved Match, player spawn contract,
// scene camera, lights, floor and boundary. It only owns the obstacle group.
public static class ChaseArenaV1Builder
{
    public const string Folder="Assets/Obstacles/ArenaV1";
    public const string GroupName="CHASE ARENA V1 / modular course";
    public const string ScenePath="Assets/Scenes/SampleScene.unity";

    public readonly struct Placement
    {
        public readonly string prefab;
        public readonly Vector3 at;
        public readonly float yaw;
        public Placement(string p,float x,float z,float turn=0f)
        {prefab=p;at=new Vector3(x,0,z);yaw=turn;}
    }

    // Deliberate distribution, not a random obstacle scatter.
    // The central Z runway stays open for diving; flanking pieces offer
    // loops, changes of elevation, concealment and competing escape routes.
    // ARENA 01: three recognizable, connected contest areas.
    // CROSSLINE (center): the committed Superman / slide rail and both ends.
    // WEST SWITCHBACK: short bench, reversal frame and crouch roof; chasers
    // can cut the corner rather than having to follow an identical action.
    // EAST ELEVATION: jumpable deck, slope, open offset gates and aerial slot.
    // Open floor between these pieces is intentional room for interception.
    public static readonly Placement[] Layout = {
        new Placement("UnderOverRail",0f,0f),
        new Placement("UnderOverBench",-3.55f,.55f),
        new Placement("AerialSlot",3.35f,2.45f),
        new Placement("LowCanopy",-3.20f,4.35f),
        new Placement("LowHurdle",0f,-4.90f),
        new Placement("TurnFrame",-1.55f,-3.55f),
        new Placement("LowDeck",2.35f,-3.35f),
        new Placement("OffsetGates",3.75f,-.55f,0f),
        new Placement("InclineRun",1.65f,4.20f,0f)
    };
    static readonly string[] Old = {
        "Tall barrier A","Tall barrier B","Pillar A","Pillar B",
        "Hurdle A","Hurdle B","Southwest Superman shelf slot",
        "Northeast low passage","MODULAR OBSTACLES / reusable prefabs"
    };

    static readonly string MaterialFolder="Assets/Materials/ArenaV1";
    static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();

    static void FolderAt(string path)
    {
        var seg=path.Split('/');
        string parent=seg[0];
        for(int i=1;i<seg.Length;i++)
        {
            string child=parent+"/"+seg[i];
            if(!AssetDatabase.IsValidFolder(child))AssetDatabase.CreateFolder(parent,seg[i]);
            parent=child;
        }
    }
    static Material Mat(string name,Color c)
    {
        if(materials.TryGetValue(name,out var m))return m;
        string path=MaterialFolder+"/"+name+".mat";
        m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null)
        {
            m=new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m,path);
        }
        m.color=c;
        m.SetFloat("_Glossiness",.22f);
        m.SetFloat("_Metallic",.08f);
        EditorUtility.SetDirty(m);
        materials.Add(name,m);
        return m;
    }
    static Material slate,frame,deck,cream,mint,gold,coral;
    static void Palette()
    {
        FolderAt(Folder);FolderAt(MaterialFolder);materials.Clear();
        slate=Mat("A-Deep-Slate",new Color(.115f,.205f,.29f));
        frame=Mat("B-Soft-Steel",new Color(.33f,.56f,.65f));
        deck=Mat("C-Sky-Deck",new Color(.67f,.81f,.84f));
        cream=Mat("D-Porcelain",new Color(.94f,.91f,.78f));
        mint=Mat("E-Seafoam",new Color(.23f,.79f,.66f));
        gold=Mat("F-Soft-Gold",new Color(1f,.68f,.28f));
        coral=Mat("G-Papaya",new Color(.91f,.40f,.31f));
    }
    static Transform Node(string name,Transform parent,Vector3 p=default)
    {
        var o=new GameObject(name).transform;
        o.SetParent(parent,false);o.localPosition=p;
        return o;
    }
    // All rendered meshes are nonphysical. The precise collision is a
    // separately named, solid, opaque-to-physics box using a stable envelope.
    static Transform Solid(Transform root,string name,Vector3 center,Vector3 size,
        bool blocker=false,bool overhead=false,bool hurdle=false)
    {
        var t=Node(name,root,center);
        var col=t.gameObject.AddComponent<BoxCollider>();
        col.size=size;col.isTrigger=false;
        if(blocker)t.gameObject.AddComponent<ChaseBlocker>();
        if(overhead)t.gameObject.AddComponent<ChaseOverheadPassage>();
        if(hurdle)t.gameObject.AddComponent<ChaseHurdle>();
        if(blocker||overhead)t.gameObject.AddComponent<ChaseOccluder>();
        return t;
    }
    static Transform Primitive(PrimitiveType type,string name,Transform parent,
        Vector3 position,Vector3 scale,Material mat,Quaternion? rotation=null)
    {
        GameObject o=GameObject.CreatePrimitive(type);
        o.name=name;o.transform.SetParent(parent,false);
        o.transform.localPosition=position;
        o.transform.localRotation=rotation??Quaternion.identity;
        o.transform.localScale=scale;
        var c=o.GetComponent<Collider>();
        if(c!=null)UnityEngine.Object.DestroyImmediate(c);
        o.GetComponent<Renderer>().sharedMaterial=mat;
        return o.transform;
    }
    // One lightweight shared rounded box mesh for all decorative frames.
    // PhysX remains a separate exact BoxCollider. No new Blender source.
    static Mesh roundedBox;
    static Mesh RoundedBox()
    {
        if(roundedBox!=null)return roundedBox;
        const string path=Folder+"/ChibiRoundedBox.asset";
        roundedBox=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(roundedBox!=null)return roundedBox;
        roundedBox=new Mesh { name="Arena V1 / shared soft-corner box" };
        var vertices=new List<Vector3>();
        var normals=new List<Vector3>();
        var uv=new List<Vector2>();
        var triangles=new List<int>();
        float[] samples={-.5f,-.40f,0f,.40f,.5f};
        Vector3[] axes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,
            Vector3.forward,Vector3.back};
        Vector3[] us={Vector3.up,Vector3.forward,Vector3.forward,
            Vector3.right,Vector3.right,Vector3.up};
        Vector3[] vs={Vector3.forward,Vector3.up,Vector3.right,
            Vector3.forward,Vector3.up,Vector3.right};
        for(int face=0;face<6;face++)
        {
            int start=vertices.Count;
            for(int j=0;j<5;j++)for(int i=0;i<5;i++)
            {
                Vector3 p=axes[face]*.5f+us[face]*samples[i]+vs[face]*samples[j];
                Vector3 near=new Vector3(Mathf.Clamp(p.x,-.4f,.4f),
                    Mathf.Clamp(p.y,-.4f,.4f),Mathf.Clamp(p.z,-.4f,.4f));
                Vector3 n=(p-near).normalized;
                vertices.Add(near+n*.1f);normals.Add(n);
                uv.Add(new Vector2(samples[i]+.5f,samples[j]+.5f));
            }
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                int a=start+y*5+x,b=a+1,c=a+5,d=c+1;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);
                triangles.Add(b);triangles.Add(d);triangles.Add(c);
            }
        }
        roundedBox.SetVertices(vertices);
        roundedBox.SetNormals(normals);
        roundedBox.SetUVs(0,uv);
        roundedBox.SetTriangles(triangles,0);
        roundedBox.RecalculateBounds();
        AssetDatabase.CreateAsset(roundedBox,path);
        return roundedBox;
    }
    static void BoxArt(Transform root,string name,Vector3 center,Vector3 size,
        Material surface)
    {
        var t=Node(name,root,center);
        t.localScale=size;
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=RoundedBox();
        t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=surface;
    }
    static void Orb(Transform root,string name,Vector3 p,float radius,Material m)=>
        Primitive(PrimitiveType.Sphere,name,root,p,Vector3.one*radius*2,m);
    // Cylinder diameter/length; Unity cylinder mesh is 2 units long.
    static void Tube(Transform root,string name,Vector3 center,Vector3 axis,
        float length,float diameter,Material surface)
    {
        var rot=Quaternion.FromToRotation(Vector3.up,axis.normalized);
        Primitive(PrimitiveType.Cylinder,name,root,center,
            new Vector3(diameter,length*.5f,diameter),surface,rot);
        Orb(root,name+" end A",center-axis.normalized*(length*.5f),diameter*.5f,surface);
        Orb(root,name+" end B",center+axis.normalized*(length*.5f),diameter*.5f,surface);
    }
    static void Post(Transform root,string name,float x,float z,float top,
        float diameter=.12f,Material surface=null)
    {
        var mat=surface==null?frame:surface;
        // Matching solid footprint; tubes sit entirely inside/support the
        // clear gap, so decorative spheres do not overhang the crossing lane.
        Solid(root,name+" / solid",new Vector3(x,top*.5f,z),
            new Vector3(diameter,top,diameter),blocker:true);
        Tube(root,name+" / round finish",new Vector3(x,top*.5f,z),
            Vector3.up,top-diameter,diameter,mat);
        Orb(root,name+" / capped crown",new Vector3(x,top-diameter*.5f,z),
            diameter*.52f,mint);
    }
    static void HorizontalBar(Transform root,string name,float width,
        float y,float z,float thickness,Material surf,bool isLowCrossing=false,
        bool overhead=false,bool hurdle=false)
    {
        Solid(root,name+" / solid",new Vector3(0,y,z),
            new Vector3(width,thickness,thickness),
            blocker:isLowCrossing,overhead:overhead,hurdle:hurdle);
        // PhysX owns squared beam volume; rounded center tube fits inside.
        Tube(root,name+" / round edge",new Vector3(0,y,z),Vector3.right,
            width-thickness,thickness*.95f,surf);
        Orb(root,name+" / stop cap L",new Vector3(-width*.5f+thickness*.5f,y,z),
            thickness*.47f,cream);
        Orb(root,name+" / stop cap R",new Vector3(width*.5f-thickness*.5f,y,z),
            thickness*.47f,cream);
    }

    static void MakeUnderOver(Transform r,bool bench)
    {
        float width=bench?1.70f:2.30f;
        float depth=bench?.34f:.26f;
        float low=bench?.85f:.86f, high=bench?.92f:.94f;
        float height=bench?1.23f:1.20f;
        // Preserve the exact tested solid envelopes from v0.9.0.
        Solid(r,"Crossing beam / stand-blocking solid",
            new Vector3(0,(low+high)*.5f,0),
            new Vector3(width,high-low,depth),blocker:true,overhead:true);
        BoxArt(r,"Soft-top crossing",new Vector3(0,(low+high)*.5f,0),
            new Vector3(width,high-low,depth),bench?cream:gold);
        for(int side=-1;side<=1;side+=2)
        {
            float x=side*(width*.5f+.06f);
            Solid(r,"Crossing foot "+side+" / solid",
                new Vector3(x,height*.5f,0),new Vector3(.12f,height,depth),
                blocker:true);
            // The visible post has rounded finish, but all collision is the
            // authored rigid box. No accidental physical decorative edges.
            Tube(r,"Crossing foot "+side+" / enamel",new Vector3(x,height*.5f,0),
                Vector3.up,height-.12f,.105f,frame);
            Orb(r,"Crossing foot "+side+" / mint cap",
                new Vector3(x,height-.07f,0),.058f,mint);
        }
        // Edge stripe communicates shape, not a move-specific instruction.
        BoxArt(r,"Readable bright front lip",new Vector3(0,high+.012f,-depth*.40f),
            new Vector3(width,.024f,.018f),coral);
    }
    static void MakeAerialSlot(Transform r)
    {
        const float width=2.2f,depth=.6f;
        Solid(r,"Ground sill / solid",new Vector3(0,.275f,0),
            new Vector3(width,.55f,depth),blocker:true,hurdle:true);
        BoxArt(r,"Ground sill / porcelain",new Vector3(0,.275f,0),
            new Vector3(width,.55f,depth),cream);
        Solid(r,"Overhead tier / solid",new Vector3(0,2.025f,0),
            new Vector3(width,.35f,depth),overhead:true);
        BoxArt(r,"Overhead tier / slate",new Vector3(0,2.025f,0),
            new Vector3(width,.35f,depth),slate);
        for(int side=-1;side<=1;side+=2)
        {
            float x=side*1.01f;
            Solid(r,"Upright "+side+" / solid",new Vector3(x,1.10f,0),
                new Vector3(.18f,2.20f,depth),blocker:true);
            BoxArt(r,"Upright "+side+" / seafoam",
                new Vector3(x,1.1f,0),new Vector3(.18f,2.20f,depth),frame);
        }
        BoxArt(r,"Bright lower tier face",new Vector3(0,.52f,-.312f),
            new Vector3(2.02f,.035f,.02f),coral);
        BoxArt(r,"Pale upper face",new Vector3(0,1.86f,-.312f),
            new Vector3(2.02f,.035f,.02f),mint);
    }
    static void MakeCanopy(Transform r)
    {
        Solid(r,"Thin overhead / solid",new Vector3(0,1.715f,0),
            new Vector3(2.2f,.15f,.55f),overhead:true);
        BoxArt(r,"Thin overhead / warm ivory",new Vector3(0,1.715f,0),
            new Vector3(2.2f,.15f,.55f),cream);
        for(int side=-1;side<=1;side+=2)
        {
            float x=side*1.01f;
            Solid(r,"Canopy post "+side+" / solid",new Vector3(x,.855f,0),
                new Vector3(.18f,1.71f,.55f),blocker:true);
            BoxArt(r,"Canopy post "+side+" / painted",new Vector3(x,.855f,0),
                new Vector3(.18f,1.71f,.55f),frame);
        }
        BoxArt(r,"Frosted rim front",new Vector3(0,1.801f,-.268f),
            new Vector3(2.15f,.023f,.023f),mint);
        BoxArt(r,"Frosted rim rear",new Vector3(0,1.801f,.268f),
            new Vector3(2.15f,.023f,.023f),mint);
    }
    static void MakeHurdle(Transform r)
    {
        Solid(r,"Jumpable low body / solid",new Vector3(0,.19f,0),
            new Vector3(1.9f,.38f,.20f),hurdle:true);
        BoxArt(r,"Jumpable low body / warm",new Vector3(0,.19f,0),
            new Vector3(1.9f,.38f,.20f),gold);
        BoxArt(r,"Dark front lower runner",new Vector3(0,.065f,-.105f),
            new Vector3(1.9f,.11f,.017f),slate);
        Tube(r,"Top guard",new Vector3(0,.36f,-.01f),Vector3.right,
            1.88f,.045f,cream);
    }
    static void MakeTurn(Transform r)
    {
        // L-shaped partial wall: cutbacks around either free end, not a
        // novelty script. Slim uprights and rails remain real solid bodies.
        foreach(float x in new[]{-1f,0f,1f})
            Post(r,"Southern upright "+x,x,0,1.65f,.14f);
        foreach(float z in new[]{.72f,1.42f})
            Post(r,"Eastern upright "+z,1f,z,1.65f,.14f);
        HorizontalBar(r,"South eye-line rail",2.13f,1.51f,0,.14f,slate,true);
        HorizontalBar(r,"South low blocking rail",2.13f,.54f,0,.14f,gold,true);
        // East arm is two beams extending into +Z from southeast corner.
        foreach(float y in new[]{.50f,1.51f})
        {
            Solid(r,"Side arm / solid "+y,new Vector3(1f,y,.76f),
                new Vector3(.14f,.14f,1.52f),blocker:true);
            Tube(r,"Side arm / enamel "+y,new Vector3(1f,y,.76f),
                Vector3.forward,1.40f,.11f,y>.8f?mint:gold);
        }
    }
    static void MakeDeck(Transform r)
    {
        // Jumpable, 0.44m solid podium: increased takeoff options without
        // forcing unsupported sloped-ground locomotion.
        const float height=.44f;
        Solid(r,"Podium deck / solid",new Vector3(0,height*.5f,0),
            new Vector3(1.85f,height,1.25f),hurdle:true);
        BoxArt(r,"Podium / slate foundation",new Vector3(0,.20f,0),
            new Vector3(1.85f,.40f,1.25f),slate);
        BoxArt(r,"Podium / warm walking face",new Vector3(0,.425f,0),
            new Vector3(1.85f,.029f,1.25f),cream);
        for(int side=-1;side<=1;side+=2)
            BoxArt(r,"Trim strip "+side,new Vector3(side*.89f,.419f,0),
                new Vector3(.042f,.034f,1.17f),mint);
        // Superficial edge designs have no colliders and no hidden steps.
        BoxArt(r,"Front highlight",new Vector3(0,.29f,-.629f),
            new Vector3(1.72f,.045f,.016f),gold);
    }
    static void MakeIncline(Transform r)
    {
        // Measured 13 degree walkable ramp. A single solid inclined BoxCollider,
        // not an animation-driven climbing or platform state machine.
        var slope=Node("Ground-to-air incline / 13 degrees",r,
            new Vector3(0,.28f,0));
        slope.localRotation=Quaternion.Euler(-13f,0,0);
        Solid(slope,"Inclined walking plane / solid",Vector3.zero,
            new Vector3(1.90f,.14f,2.20f),blocker:true,hurdle:true);
        BoxArt(slope,"Inclined walking plane / warm top",Vector3.zero,
            new Vector3(1.9f,.14f,2.2f),cream);
        foreach(float side in new[]{-.88f,.88f})
            BoxArt(slope,"Seafoam safety stripe " + side,
                new Vector3(side,.079f,0),new Vector3(.045f,.014f,2.08f),mint);
        BoxArt(slope,"Apricot leading edge",new Vector3(0,.077f,-1.07f),
            new Vector3(1.78f,.013f,.048f),coral);
    }

    static void MakeGates(Transform r)
    {
        // Two alternating slits. Their stagger gives both side approaches
        // opportunities to weave; there is no imposed authored move.
        for(int lane=-1;lane<=1;lane+=2)
        {
            float z=lane*.70f,x=lane*.48f;
            var t=Node("Gate "+lane,r,new Vector3(x,0,z));
            foreach(float side in new[]{-0.75f,.75f})
            {
                Solid(t,"Gate pillar "+side+" / solid",
                    new Vector3(side,.72f,0),new Vector3(.13f,1.44f,.19f),
                    blocker:true);
                BoxArt(t,"Gate pillar "+side+" / paint",
                    new Vector3(side,.72f,0),new Vector3(.13f,1.44f,.19f),frame);
            }
            Solid(t,"Gate upper rail / solid",new Vector3(0,1.39f,0),
                new Vector3(1.63f,.10f,.19f),blocker:true,overhead:true);
            BoxArt(t,"Gate upper rail / ivory",new Vector3(0,1.39f,0),
                new Vector3(1.63f,.10f,.19f),cream);
            BoxArt(t,"Upper accent",new Vector3(0,1.45f,-.098f),
                new Vector3(1.58f,.02f,.015f),mint);
        }
    }
    static void WritePrefab(string name,Action<Transform> author)
    {
        string path=Folder+"/"+name+".prefab";
        GameObject root=new GameObject("CHASE / "+name);
        try
        {
            author(root.transform);
            bool ok;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path,out ok);
            if(!ok||prefab==null)throw new InvalidOperationException("Unable to save "+path);
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }
    public static void AuthorLibrary()
    {
        Palette();
        WritePrefab("UnderOverRail",r=>MakeUnderOver(r,false));
        WritePrefab("UnderOverBench",r=>MakeUnderOver(r,true));
        WritePrefab("AerialSlot",MakeAerialSlot);
        WritePrefab("LowCanopy",MakeCanopy);
        WritePrefab("LowHurdle",MakeHurdle);
        WritePrefab("TurnFrame",MakeTurn);
        WritePrefab("LowDeck",MakeDeck);
        WritePrefab("OffsetGates",MakeGates);
        WritePrefab("InclineRun",MakeIncline);
        AssetDatabase.SaveAssets();
    }

    // The independent Gym uses the SAME production crossing prefabs. The
    // historic v0.9.0 authoring assets are retired, but the calibration
    // scene and its separate measurement obstacles remain valid regression
    // tools. Only the two crossing fixtures owned by this method are replaced.
    [MenuItem("Chase/Gym/Refresh Production Crossing Fixtures")]
    public static void RefreshGymFixtures()
    {
        const string path="Assets/Scenes/MovementGym.unity";
        var scene=SceneManager.GetSceneByPath(path);
        bool loaded=scene.IsValid()&&scene.isLoaded;
        if(!loaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            var root=scene.GetRootGameObjects()
                .Single(o=>o.name.StartsWith("MOVEMENT GYM  /"));
            foreach(string oldName in new[]{
                "MODULAR OBSTACLES / reusable prefabs",
                "GYM / certified production crossings"})
            {
                var old=root.transform.Cast<Transform>()
                    .FirstOrDefault(t=>t.name==oldName);
                if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var group=Node("GYM / certified production crossings",root.transform);
            foreach(var stage in new[]{
                new { prefab="UnderOverRail",name="GYM under-over rail",
                    x=-8.5f,z=2f},
                new { prefab="UnderOverBench",name="GYM narrow under-over bench",
                    x=-8.5f,z=-4.5f}
            })
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(
                    Folder+"/"+stage.prefab+".prefab");
                if(source==null)throw new InvalidOperationException(
                    "Missing production Gym crossing "+stage.prefab);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
                instance.name=stage.name;
                instance.transform.SetParent(group,false);
                instance.transform.localPosition=new Vector3(stage.x,0,stage.z);
                instance.transform.localRotation=Quaternion.Euler(0,45f,0);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unable to save Gym fixture scene");
        }
        finally {if(!loaded)EditorSceneManager.CloseScene(scene,true);}
    }

    // This method is explicit and safe to repeat. It does not alter the
    // user-authored camera, floor, exterior, spawn markers or match systems.
    [MenuItem("Chase/Rebuild Arena")]
    public static void Rebuild()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before rebuilding");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        AuthorLibrary();
        var s=SceneManager.GetSceneByPath(ScenePath);
        bool wasOpen=s.IsValid()&&s.isLoaded;
        var active=SceneManager.GetActiveScene();
        if(!wasOpen)s=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            var arena=s.GetRootGameObjects().Single(o=>o.name.StartsWith("ARENA  /"));
            foreach(string name in Old)
            {
                var t=arena.transform.Cast<Transform>().FirstOrDefault(x=>x.name==name);
                if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
            var group=arena.transform.Cast<Transform>().FirstOrDefault(x=>x.name==GroupName);
            if(group!=null)UnityEngine.Object.DestroyImmediate(group.gameObject);
            group=Node(GroupName,arena.transform);
            foreach(var placement in Layout)
            {
                string path=Folder+"/"+placement.prefab+".prefab";
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(asset==null)throw new InvalidOperationException("Missing module "+path);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,s);
                instance.name=placement.prefab+" / production";
                instance.transform.SetParent(group,false);
                instance.transform.localPosition=placement.at;
                instance.transform.localRotation=Quaternion.Euler(0,placement.yaw,0);
            }
            // Remove former static nonmodular obstacle geometry only.
            EditorSceneManager.MarkSceneDirty(s);
            if(!EditorSceneManager.SaveScene(s))
                throw new InvalidOperationException("Unable to save production scene");
        }
        finally
        {
            if(!wasOpen)EditorSceneManager.CloseScene(s,true);
            if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active);
        }
        AssetDatabase.SaveAssets();
        ChaseArenaV1Contract.Validate();
        Debug.Log("CHASE_ARENA_V1_BUILT: nine modular physical obstacle families");
    }
}
