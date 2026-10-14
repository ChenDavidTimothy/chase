using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chase;

// Separately authored reference-study course. Never mutates the approved Arena 01.
// Geometry is hand-specified from a perspective reference, NOT surveyed WCT dimensions.
// All movement remains on normal solid PhysX colliders with separate nonphysical art.
public static class ChaseReferenceArenaBuilder
{
    public const string ScenePath = "Assets/Scenes/ArenaWCTTrial.unity";
    public const string Folder = "Assets/Obstacles/WCTTrial";
    public const string GroupName = "CHASE / WCT REFERENCE TRIAL";
    const string MaterialFolder = "Assets/Materials/ArenaV1/";
    const string OriginalGroup = "CHASE ARENA V1 / modular course";

    public readonly struct LayoutPart
    {
        public readonly string prefab;
        public readonly Vector3 position;
        public readonly float yaw;
        public LayoutPart(string prefab, float x, float z, float yaw=0)
        { this.prefab=prefab; position=new Vector3(x,0,z); this.yaw=yaw; }
    }

    // World +Z is image rear, -Z image foreground. Geometry fits 12 x 12 m.
    // Space around the two role-side spawns is intentionally reserved.
    public static readonly LayoutPart[] Layout = {
        new LayoutPart("WestPortal",-4.05f,3.82f),
        new LayoutPart("NorthLinks",-2.34f,4.35f),
        new LayoutPart("BackBench",-.51f,4.74f),
        new LayoutPart("CrownTower",1.60f,3.68f),
        new LayoutPart("NorthEastTruss",4.13f,4.51f),
        new LayoutPart("EastRamp",3.78f,1.55f),
        new LayoutPart("EastDeck",3.63f,-.62f),
        new LayoutPart("SouthEastTower",3.70f,-3.62f),
        new LayoutPart("EastCrossbar",1.72f,.95f),
        new LayoutPart("CenterDeck",.10f,-.24f),
        new LayoutPart("FrontGrid",.06f,-3.13f),
        new LayoutPart("FrontBench",1.64f,-4.51f),
        new LayoutPart("WestBridge",-2.71f,.31f),
        new LayoutPart("WestSnake",-3.39f,-3.59f),
        new LayoutPart("SouthWestRails",-1.41f,-4.40f),
        new LayoutPart("NorthInnerRail",-1.41f,2.35f),
        // Proven production collision envelopes, source-linked and unmodified.
        new LayoutPart("UnderOverRail",-.06f,-1.79f),
        new LayoutPart("UnderOverBench",-3.39f,-2.16f)
    };
    static readonly HashSet<string> Certified = new HashSet<string> {
        "UnderOverRail","UnderOverBench"
    };

    static Material steel, slate, top, ivory, mint, coral, gold;
    static Mesh softBox;

    static void LoadArt()
    {
        steel=Mat("B-Soft-Steel"); slate=Mat("A-Deep-Slate");
        top=Mat("C-Sky-Deck"); ivory=Mat("D-Porcelain");
        mint=Mat("E-Seafoam"); coral=Mat("G-Papaya"); gold=Mat("F-Soft-Gold");
        softBox=AssetDatabase.LoadAssetAtPath<Mesh>(
            "Assets/Obstacles/ArenaV1/ChibiRoundedBox.asset");
        if(softBox==null)throw new InvalidOperationException("Missing existing shared rounded arena mesh");
    }
    static Material Mat(string name)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder+name+".mat");
        if(m==null)throw new InvalidOperationException("Missing approved CHASE material "+name);
        return m;
    }
    static Transform Node(Transform parent,string name,Vector3 at)
    {
        var t=new GameObject(name).transform;
        t.SetParent(parent,false); t.localPosition=at;
        return t;
    }
    static void Art(Transform parent,string name,Vector3 p,Vector3 size,Material m)
    {
        var t=Node(parent,name+" / visual",p);
        t.localScale=size;
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=softBox;
        t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=m;
    }
    static void Block(Transform parent,string name,Vector3 p,Vector3 size,
        Material m,bool blocker=true,bool overhead=false,bool hurdle=false)
    {
        var t=Node(parent,name+" / solid",p);
        var c=t.gameObject.AddComponent<BoxCollider>();
        c.size=size; c.isTrigger=false;
        if(blocker)t.gameObject.AddComponent<ChaseBlocker>();
        if(overhead)t.gameObject.AddComponent<ChaseOverheadPassage>();
        if(hurdle)t.gameObject.AddComponent<ChaseHurdle>();
        if(blocker || overhead)t.gameObject.AddComponent<ChaseOccluder>();
        Art(parent,name,p,size,m);
    }
    static void Rail(Transform p,string name,Vector3 at,Vector3 scale,Material surface= null,
        bool blocker=true,bool overhead=false)
    {
        Block(p,name,at,scale,surface==null?steel:surface,blocker,overhead);
    }
    static void Post(Transform p,string name,float x,float z,float h,float thick=.11f)
    {
        Block(p,name,new Vector3(x,h*.5f,z),new Vector3(thick,h,thick),steel);
        Art(p,name+" / mint cap",new Vector3(x,h-.035f,z),
            new Vector3(thick+.012f,.04f,thick+.012f),mint);
    }
    static void Deck(Transform p,string name,float w,float d,float y,
        bool legs=true,bool edge=true)
    {
        // Flat real solid panel; physical underpass whenever y >= .90.
        Block(p,name,new Vector3(0,y-.065f,0),new Vector3(w,.13f,d),
            slate,true,true,true);
        Art(p,name+" / walking enamel",new Vector3(0,y+.009f,0),
            new Vector3(w-.04f,.025f,d-.04f),ivory);
        if(edge)
        {
            Art(p,name+" / front mint",new Vector3(0,y-.05f,-d*.5f-.01f),
                new Vector3(w-.06f,.056f,.018f),mint);
            Art(p,name+" / right coral",new Vector3(w*.5f+.01f,y-.05f,0),
                new Vector3(.018f,.056f,d-.06f),coral);
        }
        if(legs)
            foreach(float x in new[]{-w*.5f+.09f,w*.5f-.09f})
                foreach(float z in new[]{-d*.5f+.09f,d*.5f-.09f})
                    Post(p,name+" / support "+x+"/"+z,x,z,y-.13f,.10f);
    }
    static void Portal(Transform p,float width,float topY,bool mid=true)
    {
        foreach(float x in new[]{-width*.5f,width*.5f})
            Post(p,"portal leg "+x,x,0,topY);
        Rail(p,"portal crown",new Vector3(0,topY-.055f,0),
            new Vector3(width+.10f,.10f,.10f),slate);
        if(mid)
            Rail(p,"eye-line bar",new Vector3(0,1.42f,0),
                new Vector3(width+.1f,.085f,.09f),steel,true,true);
    }
    static void LowCross(Transform p,float width,float beamY=.95f)
    {
        foreach(float x in new[]{-width*.5f,width*.5f})
            Post(p,"low-cross leg "+x,x,0,1.4f,.11f);
        Rail(p,"dual-choice crossing",new Vector3(0,beamY,0),
            new Vector3(width,.09f,.20f),gold,true,true);
        Rail(p,"top sightline",new Vector3(0,1.36f,0),
            new Vector3(width+.08f,.07f,.09f),steel,true,true);
    }
    static void Tower(Transform p,float w,float d,bool tall)
    {
        float floor=tall?1.09f:.83f;
        foreach(float x in new[]{-w*.5f,w*.5f})
            foreach(float z in new[]{-d*.5f,d*.5f})
                Post(p,"four-post tower "+x+"/"+z,x,z,2.27f,.115f);
        Deck(p,"full elevated deck",w-.02f,d-.02f,floor,false);
        foreach(float z in new[]{-d*.5f,d*.5f})
            Rail(p,"upper bridge "+z,new Vector3(0,2.20f,z),
                new Vector3(w,.085f,.085f),slate);
        foreach(float x in new[]{-w*.5f,w*.5f})
            Rail(p,"tower side crown "+x,new Vector3(x,2.20f,0),
                new Vector3(.085f,.085f,d),slate);
        Art(p,"tower color facing",new Vector3(0,floor-.29f,-d*.5f-.015f),
            new Vector3(w-.12f,.24f,.025f),tall?coral:mint);
    }
    static void Ramp(Transform p)
    {
        // Genuine 13-degree solid walking ramp. The front lip is at floor height.
        var t=Node(p,"connected angled run",new Vector3(0,.30f,0));
        t.localRotation=Quaternion.Euler(-13f,0,0);
        Block(t,"inclined walking surface",Vector3.zero,
            new Vector3(1.16f,.13f,2.26f),ivory,true,false,true);
        foreach(float x in new[]{-.56f,.56f})
            Art(t,"long aqua edge",new Vector3(x,.071f,0),
                new Vector3(.038f,.027f,2.18f),mint);
        Art(t,"leading coral stripe",new Vector3(0,.076f,-1.09f),
            new Vector3(1.08f,.022f,.035f),coral);
    }
    static void BuildPart(string id,Transform p)
    {
        switch(id)
        {
            case "WestPortal":
                Portal(p,1.76f,2.12f);
                Rail(p,"left turnaround",new Vector3(.80f,.56f,.31f),
                    new Vector3(.09f,.10f,.70f),gold);
                break;
            case "NorthLinks":
                Portal(p,1.28f,1.93f,false);
                Deck(p,"high link tabletop",1.36f,.52f,.96f,false);
                break;
            case "BackBench":
                Deck(p,"rear long running bench",1.50f,.56f,.91f);
                break;
            case "CrownTower":
                Tower(p,2.04f,1.65f,true);
                break;
            case "NorthEastTruss":
                Portal(p,1.65f,2.22f);
                Rail(p,"canopy rear spine",new Vector3(0,2.20f,.32f),
                    new Vector3(1.70f,.10f,.10f),slate);
                break;
            case "EastRamp":
                Ramp(p);
                break;
            case "EastDeck":
                Deck(p,"east long platform",1.97f,1.16f,.94f);
                break;
            case "SouthEastTower":
                Tower(p,2.0f,1.32f,false);
                break;
            case "EastCrossbar":
                Portal(p,1.36f,2.04f,false);
                Rail(p,"north connector",new Vector3(0,1.01f,.05f),
                    new Vector3(1.35f,.09f,.19f),gold,true,true);
                break;
            case "CenterDeck":
                Deck(p,"central decision slab",1.58f,1.12f,.55f);
                break;
            case "FrontGrid":
                LowCross(p,1.78f,.93f);
                // A staggered parallel bar reproduces the foreground lattice.
                Rail(p,"parallel grid",new Vector3(.1f,.57f,.33f),
                    new Vector3(1.76f,.095f,.10f),mint);
                break;
            case "FrontBench":
                Deck(p,"front small table",1.40f,.75f,.85f);
                break;
            case "WestBridge":
                Deck(p,"west long switchback",2.05f,.70f,.97f);
                break;
            case "WestSnake":
                Portal(p,1.64f,1.96f,false);
                Rail(p,"inside corner",new Vector3(.76f,.90f,.34f),
                    new Vector3(.11f,.10f,.78f),gold,true,true);
                break;
            case "SouthWestRails":
                LowCross(p,1.65f,.92f);
                break;
            case "NorthInnerRail":
                Portal(p,1.74f,2.10f,false);
                Rail(p,"northern waist bar",new Vector3(0,.92f,0),
                    new Vector3(1.70f,.09f,.21f),gold,true,true);
                break;
            default:
                throw new InvalidOperationException("Unknown reference module "+id);
        }
    }
    static void MakeSource(string name)
    {
        string path=Folder+"/"+name+".prefab";
        var root=new GameObject("CHASE / reference / "+name);
        try
        {
            BuildPart(name,root.transform);
            bool ok;
            if(PrefabUtility.SaveAsPrefabAsset(root,path,out ok)==null || !ok)
                throw new InvalidOperationException("Unable to author "+path);
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }
    [MenuItem("Chase/Arena/Build Reference Trial")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build the trial only outside Play Mode");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var original=SceneManager.GetActiveScene();
        var existing=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if(existing==null)
        {
            if(!AssetDatabase.CopyAsset(ChaseArenaV1Builder.ScenePath,ScenePath))
                throw new InvalidOperationException("Could not clone production scene");
            AssetDatabase.ImportAsset(ScenePath);
        }
        if(!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Obstacles","WCTTrial");
        LoadArt();
        foreach(string name in Layout.Select(s=>s.prefab).Distinct())
            if(!Certified.Contains(name)) MakeSource(name);
        var scene=SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded=scene.IsValid()&&scene.isLoaded;
        if(!wasLoaded)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            var arena=scene.GetRootGameObjects().Single(o=>o.name.StartsWith("ARENA  /"));
            foreach(var child in arena.transform.Cast<Transform>().ToArray())
                if(child.name==OriginalGroup || child.name==GroupName)
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            var group=Node(arena.transform,GroupName,Vector3.zero);
            foreach(var spec in Layout)
            {
                string path=Certified.Contains(spec.prefab)?
                    ChaseArenaV1Builder.Folder+"/"+spec.prefab+".prefab":
                    Folder+"/"+spec.prefab+".prefab";
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(asset==null)throw new InvalidOperationException("Missing trial module "+path);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);
                go.name="REFERENCE / "+spec.prefab;
                go.transform.SetParent(group,false);
                go.transform.localPosition=spec.position;
                go.transform.localRotation=Quaternion.Euler(0,spec.yaw,0);
                go.transform.localScale=Vector3.one;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save trial scene");
        }
        finally
        {
            if(!wasLoaded) EditorSceneManager.CloseScene(scene,true);
            if(original.IsValid() && original.isLoaded)SceneManager.SetActiveScene(original);
        }
        AssetDatabase.SaveAssets();
        ChaseReferenceArenaContract.Validate();
        Debug.Log("CHASE_REFERENCE_TRIAL_BUILT: separate source-linked course, production unchanged");
    }
}
