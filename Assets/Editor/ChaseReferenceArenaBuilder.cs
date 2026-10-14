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

    // Strict 0/90 degree orthogonal course: parallel X/Z sightlines,
    // WCT-style joined frames and physically executable under/over
    // choices. No diagonal placements or uncontrolled scatter.
    // Distances are hand-authored, NOT surveyed reference dimensions.
    // Spawns retain their fixed certified side-entry positions.
    public static readonly LayoutPart[] Layout = {
        new LayoutPart("WestPortal",-4.05f,3.82f),
        new LayoutPart("NorthLinks",-2.34f,4.35f,90f),
        new LayoutPart("BackBench",-.51f,4.74f),
        // Real 70cm walking link between NorthLinks gallery and BackBench.
        new LayoutPart("NorthGallerySpan",-1.61f,4.71f),
        new LayoutPart("CrownTower",1.60f,4.74f),
        new LayoutPart("NorthEastTruss",4.13f,4.78f,90f),
        new LayoutPart("EastRamp",3.85f,.40f,270f),
        new LayoutPart("EastDeck",3.63f,-1.25f,90f),
        new LayoutPart("SouthEastTower",3.70f,-3.62f),
        new LayoutPart("EastCrossbar",-1.55f,.95f,90f),
        new LayoutPart("CenterDeck",-.96f,-.78f),
        new LayoutPart("FrontGrid",.06f,-3.13f,90f),
        new LayoutPart("FrontBench",1.64f,-4.51f),
        new LayoutPart("WestBridge",-2.71f,.31f,90f),
        new LayoutPart("WestSnake",-3.39f,-3.59f),
        new LayoutPart("SouthWestRails",-1.41f,-4.40f,90f),
        new LayoutPart("NorthInnerRail",-1.41f,2.35f),
        new LayoutPart("UnderOverRail",-.06f,-1.79f),
        new LayoutPart("UnderOverBench",-3.39f,-2.16f,90f),
        // Edge controls cross the long wall run, instead of adding random
        // obstructions deep in the middle. Both ends allow a standing detour.
        new LayoutPart("WallSwitchWestNorth",-4.65f,1.30f),
        new LayoutPart("WallSwitchWestSouth",-4.65f,-.65f),
        new LayoutPart("WallSwitchEastNorth",4.65f,3.22f),
        new LayoutPart("WallSwitchEastSouth",4.65f,-2.40f),
        // The former central open runway becomes a two-way choice:
        // slow lateral bypass or timed physical crossing.
        new LayoutPart("CrossFork",-.45f,1.35f,90f)
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
        // Thin, real solid deck. A 0.10m panel provides 3cm more low
        // clearance at the SAME top height than the old 0.13m platform.
        Block(p,name,new Vector3(0,y-.05f,0),new Vector3(w,.10f,d),
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
                    Post(p,name+" / support "+x+"/"+z,x,z,y-.10f,.10f);
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
    static void LowCross(Transform p,float width,float beamY=1.0f)
    {
        // This is an OVER/UNDER SKILL crossing. The old 1.36m
        // horizontal bar was an unintended ceiling for a jump or dive.
        // Keep real solid posts and a single low beam: body contact
        // decides success, without an impossible roof overhead.
        foreach(float x in new[]{-width*.5f-.065f,width*.5f+.065f})
            Post(p,"open crossing terminal "+x,x,0,1.26f,.10f);
        Block(p,"dual-choice crossing",new Vector3(0,beamY,0),
            new Vector3(width,.09f,.18f),gold,false,true,true);
    }
    static void Tower(Transform p,float w,float d,bool tall)
    {
        // The four corner posts are intentional. However the original four
        // upper rails enclosed a podium with no usable athletic approach.
        // Crown opens toward -Z (center arena), southeast tower toward -X.
        // A 1.02m deck is reachable by the real 7m/s jump (peak ~1.11m)
        // and still offers .89m of solid clearance for a .82m slide body.
        const float floor=1.02f;
        foreach(float x in new[]{-w*.5f,w*.5f})
            foreach(float z in new[]{-d*.5f,d*.5f})
                Post(p,"four-post tower "+x+"/"+z,x,z,2.27f,.115f);
        Deck(p,"full elevated deck",w-.02f,d-.02f,floor,false);
        // Exactly THREE overhead perimeter members. The center of the
        // fourth side remains physically clear, not an invisible trigger.
        foreach(float z in new[]{-d*.5f,d*.5f})
            if(!tall || z>0f) // taller Crown: open SOUTH (-Z)
                Rail(p,"upper bridge "+z,new Vector3(0,2.20f,z),
                    new Vector3(w,.085f,.085f),slate);
        foreach(float x in new[]{-w*.5f,w*.5f})
            if(tall || x>0f) // southeast: open WEST (-X)
                Rail(p,"tower side crown "+x,new Vector3(x,2.20f,0),
                    new Vector3(.085f,.085f,d),slate);
        if(tall)
            Art(p,"clear entry / mint threshold",
                new Vector3(0,floor+.016f,-d*.5f-.03f),
                new Vector3(w-.24f,.024f,.035f),mint);
        else
            Art(p,"clear entry / mint threshold",
                new Vector3(-w*.5f-.03f,floor+.016f,0),
                new Vector3(.035f,.024f,d-.22f),mint);
        // No skirt or opaque podium box. A skinned front face falsely
        // communicated a closed body and visually hid the playable passage.
        // Identifying color is confined to a thin NONPHYSICAL rim above
        // the open underside.
        Art(p,"elevated edge identity",new Vector3(0,floor+.035f,-d*.5f-.014f),
            new Vector3(w-.18f,.032f,.025f),tall?coral:mint);
    }
    static void Ramp(Transform p)
    {
        // EAST ELEVATION ROUTE (one source-linked solid assembly):
        // ground -> inclined walk -> genuinely flush flat runout ->
        // perpendicular flat turn -> 0.67m jump onto high landing.
        // Opponents may instead cut UNDER the final high shelf or
        // stay on the ground and intercept the far end. Nothing snaps,
        // vaults automatically, or replaces contact with an animation.
        var slope=Node(p,"connected angled run",new Vector3(0,.30f,0));
        slope.localRotation=Quaternion.Euler(-13f,0,0);
        Block(slope,"inclined walking surface",Vector3.zero,
            new Vector3(1.16f,.13f,2.26f),ivory,true,false,true);
        foreach(float x in new[]{-.56f,.56f})
            Art(slope,"long aqua edge",new Vector3(x,.071f,0),
                new Vector3(.038f,.027f,2.18f),mint);
        Art(slope,"leading coral stripe",new Vector3(0,.076f,-1.09f),
            new Vector3(1.08f,.022f,.035f),coral);
        // Ramp upper end is approximately 0.62m at local z=+1.13.
        // No upward step at the connection: the runout top is 0.62m.
        Block(p,"level runout",new Vector3(0,.56f,2.35f),
            new Vector3(1.16f,.12f,2.46f),ivory,true,false,true);
        Art(p,"runout direction stripe",new Vector3(0,.638f,2.32f),
            new Vector3(.054f,.024f,2.27f),mint);
        // A same-height 90-degree corner arm. The intersecting top
        // panels deliberately form ONE continuous surface.
        Block(p,"flat corner turn",new Vector3(.57f,.56f,2.60f),
            new Vector3(1.05f,.12f,1.90f),ivory,true,false,true);
        Art(p,"corner visual cue",new Vector3(.97f,.634f,2.60f),
            new Vector3(.035f,.025f,1.70f),coral);
        // The roof can also be passed UNDER in a slide. Supports
        // remain at the outer corners rather than the entry line.
        Block(p,"jump landing",new Vector3(2.15f,1.24f,2.65f),
            new Vector3(2.40f,.10f,2.25f),slate,true,true,true);
        Art(p,"landing surface",new Vector3(2.15f,1.301f,2.65f),
            new Vector3(2.35f,.024f,2.20f),ivory);
        Art(p,"landing mint lip",new Vector3(2.15f,1.294f,1.49f),
            new Vector3(2.30f,.020f,.024f),mint);
        foreach(float z in new[]{1.60f,3.70f})
            Post(p,"landing outer supports "+z,3.27f,z,1.19f,.095f);
    }
    static void WallChoice(Transform p,string label,float w=1.76f)
    {
        // Inspired by the perimeter links in the reference quad.
        // Frictionless standing wall-loop is physically interrupted here.
        // A real .94m underside gives the .82m slide body 12cm of
        // vertical tolerance. A committed dive clears its 1.02m top.
        // Open ends leave a longer but still playable standing counter-line.

        foreach(float x in new[]{-(w*.5f+.06f),w*.5f+.06f})
            Post(p,label+" / post "+x,x,0,1.18f,.12f);
        // Explicitly traversable via the actual slide and leap. Classify
        // the beam as a hurdle/overhead passage, not a permanent tall
        // navigation blocker. Solid posts remain full blockers. The trial
        // bot can plan a skill crossing, then must perform it in PhysX.
        Block(p,label+" / genuine under-over",
            new Vector3(0,.98f,0),new Vector3(w,.08f,.20f),
            gold,false,true,true);
        Art(p,label+" / front lip",
            new Vector3(0,1.026f,-.10f),new Vector3(w-.10f,.020f,.022f),mint);
    }

    static void BuildPart(string id,Transform p)
    {
        switch(id)
        {
            case "WallSwitchWestNorth":
            case "WallSwitchWestSouth":
            case "WallSwitchEastNorth":
            case "WallSwitchEastSouth":
                WallChoice(p,id);
                break;
            case "CrossFork":
                WallChoice(p,"central timed crossing",1.46f);
                // Visual track heading is supplied by authored prefab yaw.
                break;
            case "WestPortal":
                Portal(p,1.76f,2.12f);
                Rail(p,"left turnaround",new Vector3(.80f,.56f,.31f),
                    new Vector3(.09f,.10f,.70f),gold);
                break;
            case "NorthLinks":
                // ONE connected, framed gallery: two matching portals,
                // high structural side rails, and an accessible deck.
                // The horizontal members JOIN the portal frames into one
                // continuous rigid physical module, not a floating obstacle.
                foreach(float z in new[]{-.70f,.70f})
                {
                    var frame=Node(p,"gallery portal at "+z,
                        new Vector3(0,0,z));
                    Portal(frame,1.28f,3.10f,false);
                }
                foreach(float x in new[]{-.64f,.64f})
                    Rail(p,"gallery connecting spine "+x,
                        new Vector3(x,3.05f,0),
                        new Vector3(.09f,.09f,1.48f),slate);
                var deck=Node(p,"gallery central high deck",Vector3.zero);
                Deck(deck,"under-or-over table",1.36f,.76f,1.07f,false);
                break;
            case "BackBench":
                // Cantilever from the adjoining NorthGallerySpan and
                // its four grounded gallery frames. Standalone posts
                // closed the coarse (and real player) corridor below.
                // The linked 1.07m surface is solid with its underside
                // accessible from either long side.
                Deck(p,"rear long running bench",1.50f,.56f,1.07f,false);
                break;
            case "NorthGallerySpan":
                // Bridges the real elevated edges of NorthLinks and
                // BackBench across a ~0.70m opening. Its top is exactly
                // the same as the two connected 1.07m surfaces.
                Block(p,"continuous gallery span",new Vector3(0,1.02f,0),
                    new Vector3(.68f,.10f,.40f),ivory,true,true,true);
                Art(p,"gallery joint stripe",new Vector3(0,1.08f,0),
                    new Vector3(.55f,.020f,.035f),mint);
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
                Deck(p,"east long platform",1.97f,1.16f,1.08f);
                break;
            case "SouthEastTower":
                Tower(p,2.0f,1.32f,false);
                break;
            case "EastCrossbar":
                LowCross(p,1.36f,1.00f);
                break;
            case "CenterDeck":
                Deck(p,"central decision slab",1.40f,.92f,1.07f);
                break;
            case "FrontGrid":
                // Connected foreground two-bay rail rack. Parallel
                // crossing gates share overhead side members. Both bays
                // provide real, independent timed traversal opportunities.
                foreach(float z in new[]{-.53f,.53f})
                {
                    var gate=Node(p,"joined crossing bay "+z,new Vector3(0,0,z));
                    LowCross(gate,1.78f,z<0?.98f:1.02f);
                }
                foreach(float x in new[]{-.89f,.89f})
                    Rail(p,"joined lateral spine "+x,
                        new Vector3(x,1.37f,0),
                        new Vector3(.09f,.08f,1.14f),slate);
                Art(p,"connected rack signature",new Vector3(.89f,1.42f,0),
                    new Vector3(.035f,.03f,1.0f),mint);
                break;
            case "FrontBench":
                Deck(p,"front small table",1.40f,.75f,1.08f);
                break;
            case "WestBridge":
                // Remove all four central support legs and instead
                // support from the OUTSIDE corner. A 0.70m-deep slide
                // roof must not leave the 1.98m-long body trapped
                // between decorative feet and the linked turn.
                Deck(p,"west long switchback",1.89f,.70f,1.09f,false);
                foreach(float x in new[]{-.90f,.90f})
                    Post(p,"outside west support "+x,x,.36f,.99f,.09f);
                // Extend ONE side into a joined turning rail, making a
                // choice between immediate slide/dive and looping around
                // the end. The narrow rail touches the deck's end support.
                Rail(p,"connected west turn rail",
                    new Vector3(1.30f,.98f,0),
                    new Vector3(.74f,.10f,.14f),gold,false,true);
                foreach(float z in new[]{-.29f,.29f})
                    Post(p,"connected turn terminal "+z,1.67f,z,1.20f,.10f);
                break;
            case "WestSnake":
                Portal(p,1.64f,1.96f,false);
                Rail(p,"inside corner",new Vector3(.76f,.90f,.34f),
                    new Vector3(.11f,.10f,.78f),gold,true,true);
                break;
            case "SouthWestRails":
                LowCross(p,1.65f,1.00f);
                break;
            case "NorthInnerRail":
                LowCross(p,1.74f,1.00f);
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
