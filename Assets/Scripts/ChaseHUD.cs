using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chase
{
    // Screen-space, stream-readable presentation. No debug telemetry, no
    // physics ownership, no authored image/font dependencies, no EventSystem.
    // One compact UI works in WebGL and standalone at any canvas aspect.
    public sealed class ChaseHUD : MonoBehaviour
    {
        static readonly Color Ink = new Color(.052f,.105f,.153f,1f);
        static readonly Color PanelColor = new Color(.068f,.144f,.207f,.94f);
        static readonly Color Cream = new Color(.96f,.952f,.88f,1f);
        static readonly Color Muted = new Color(.66f,.78f,.81f,1f);
        static readonly Color Mint = new Color(.27f,.83f,.70f,1f);
        static readonly Color Gold = new Color(1f,.72f,.34f,1f);
        static readonly Color Coral = new Color(.98f,.41f,.36f,1f);
        static readonly Color Blue = new Color(.32f,.67f,1f,1f);

        ChaseMatch match;
        readonly Dictionary<(int,Color,FontStyle,TextAnchor),GUIStyle> styleCache =
            new Dictionary<(int,Color,FontStyle,TextAnchor),GUIStyle>();
        GUIStyle roundShape, title, brand, body, small, micro, buttonText,
            buttonPrimaryText, buttonSmall, large, centered, keyText, keyLabel;
        Texture2D rounded, arrowUp, arrowDown;
        ChasePhase previousPhase;
        float goUntil;
        float hintBrightUntil;

        void Awake() { match=GetComponent<ChaseMatch>(); }

        void Update()
        {
            if(match==null || match.MenuPage!=ChaseMenuPage.InMatch) return;
            if(previousPhase==ChasePhase.Countdown && match.Phase==ChasePhase.Active)
            {
                goUntil=Time.unscaledTime+.52f;
                hintBrightUntil=Time.unscaledTime+7f;
            }
            previousPhase=match.Phase;
        }

        // The standard WebGL runtime font cannot render Unicode arrow
        // glyphs reliably. Draw the physical arrow keys as tiny RGBA icons
        // instead of depending on a platform-specific font fallback.
        static Texture2D ArrowTexture(bool up)
        {
            const int size=32;
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
            tex.name=up?"CHASE / Up key":"CHASE / Down key";
            tex.filterMode=FilterMode.Bilinear;
            tex.wrapMode=TextureWrapMode.Clamp;
            tex.hideFlags=HideFlags.HideAndDontSave;
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float yy=up?y:31-y;
                float spread=(27f-yy)*.86f;
                bool head=yy>=15f && yy<=27f && Mathf.Abs(x-15.5f)<spread;
                bool stem=yy>=6f && yy<18f && x>=13 && x<=18;
                pixels[y*size+x]=new Color(1,1,1,head||stem?1:0);
            }
            tex.SetPixels(pixels);
            tex.Apply(false,true);
            return tex;
        }

        GUIStyle Text(int size,Color color,FontStyle font=FontStyle.Normal,
            TextAnchor align=TextAnchor.MiddleLeft)
        {
            var key=(size,color,font,align);
            if(styleCache.TryGetValue(key,out GUIStyle cached))return cached;
            var s=new GUIStyle(GUI.skin.label);
            s.fontSize=size;
            s.fontStyle=font;
            s.alignment=align;
            s.normal.textColor=color;
            s.wordWrap=false;
            s.clipping=TextClipping.Clip;
            s.padding=new RectOffset(0,0,0,0);
            styleCache.Add(key,s);
            return s;
        }

        void Setup()
        {
            if(roundShape!=null)return;
            const int n=48;
            rounded=new Texture2D(n,n,TextureFormat.RGBA32,false);
            rounded.name="CHASE / runtime nine-slice soft tile";
            rounded.filterMode=FilterMode.Bilinear;
            rounded.wrapMode=TextureWrapMode.Clamp;
            rounded.hideFlags=HideFlags.HideAndDontSave;
            var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                // SDF for a rounded square, antialiased at the silhouette.
                float a=Mathf.Abs(x+.5f-n*.5f)-(n*.5f-11f);
                float b=Mathf.Abs(y+.5f-n*.5f)-(n*.5f-11f);
                float outside=Mathf.Sqrt(Mathf.Max(a,0)*Mathf.Max(a,0)+
                    Mathf.Max(b,0)*Mathf.Max(b,0));
                float d=outside+Mathf.Min(Mathf.Max(a,b),0)-11f;
                pixels[y*n+x]=new Color(1,1,1,Mathf.Clamp01(.65f-d));
            }
            rounded.SetPixels(pixels);
            rounded.Apply(false,true);
            arrowUp=ArrowTexture(true);
            arrowDown=ArrowTexture(false);
            roundShape=new GUIStyle(GUIStyle.none);
            roundShape.normal.background=rounded;
            roundShape.border=new RectOffset(12,12,12,12);
            title=Text(60,Cream,FontStyle.Bold);
            brand=Text(78,Cream,FontStyle.Bold);
            body=Text(20,Cream);
            small=Text(15,Muted,FontStyle.Bold);
            micro=Text(12,Muted,FontStyle.Bold);
            large=Text(66,Cream,FontStyle.Bold,TextAnchor.MiddleCenter);
            centered=Text(19,Cream,FontStyle.Bold,TextAnchor.MiddleCenter);
            buttonText=Text(21,Cream,FontStyle.Bold,TextAnchor.MiddleCenter);
            buttonPrimaryText=Text(21,Ink,FontStyle.Bold,TextAnchor.MiddleCenter);
            buttonSmall=Text(16,Cream,FontStyle.Bold,TextAnchor.MiddleCenter);
            keyText=Text(18,Cream,FontStyle.Bold,TextAnchor.MiddleCenter);
            keyLabel=Text(12,Muted,FontStyle.Bold,TextAnchor.MiddleCenter);
        }

        void Tile(Rect r,Color c)
        {
            Color old=GUI.color;
            GUI.color=c;
            GUI.Box(r,GUIContent.none,roundShape);
            GUI.color=old;
        }

        void Label(Rect r,string value,GUIStyle style) =>
            GUI.Label(r,value,style);

        bool Button(Rect r,string label,Color fill,bool primary=false)
        {
            bool hover=r.Contains(Event.current.mousePosition);
            Tile(new Rect(r.x,r.y+3,r.width,r.height),new Color(0,.035f,.065f,.30f));
            Tile(r,hover?Color.Lerp(fill,Cream,.13f):fill);
            if(primary)
                Tile(new Rect(r.x+9,r.y+8,5,r.height-16),Cream);
            return GUI.Button(r,label,primary?buttonPrimaryText:buttonText);
        }

        void SmallButton(Rect r,string label,Color fill,System.Action action)
        {
            bool hover=r.Contains(Event.current.mousePosition);
            Tile(r,hover?Color.Lerp(fill,Cream,.13f):fill);
            if(GUI.Button(r,label,buttonSmall))action();
        }

        void Back(float x,float y)
        {
            SmallButton(new Rect(x,y,102,42),"<  BACK",
                new Color(.18f,.29f,.36f,.95f),match.MenuBack);
        }

        void Brand(float x,float y)
        {
            Label(new Rect(x,y,470,83),"CHASE",brand);
            Tile(new Rect(x+3,y+80,90,5),Mint);
            Tile(new Rect(x+103,y+80,36,5),Gold);
            Label(new Rect(x+4,y+98,480,24),"THE CHASE IS ON",small);
        }

        void Backdrop(float w,float h)
        {
            // Arena remains a real 3D preview. One opaque edge gives the copy
            // contrast without hiding all of the playable course.
            GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture,
                ScaleMode.StretchToFill,true,0,
                new Color(.025f,.05f,.075f,.50f),0,0);
        }

        void MenuPanel(float w,float h)
        {
            float x=Mathf.Max(28f,w*.065f);
            float ph=Mathf.Min(h-52,630f);
            float y=(h-ph)*.5f;
            Tile(new Rect(x-13,y-9,554,ph+18),
                new Color(.035f,.091f,.14f,.92f));
            Tile(new Rect(x-13,y-9,7,ph+18),Mint);

            Brand(x+33,y+30);
            switch(match.MenuPage)
            {
                case ChaseMenuPage.MainMenu:
                    Label(new Rect(x+35,y+196,460,52),
                        "ONE TAG. YOUR ROUTE.",Text(29,Cream,FontStyle.Bold));
                    Label(new Rect(x+35,y+253,430,35),
                        "20 seconds to outplay the chase.",body);
                    if(Button(new Rect(x+34,y+329,451,62),"PLAY  >",Mint,true))
                        match.OpenPlay();
                    if(Button(new Rect(x+34,y+402,218,53),"HOW TO PLAY",
                        new Color(.10f,.22f,.30f)))
                        match.OpenHowTo();
                    if(Button(new Rect(x+266,y+402,219,53),"SETTINGS",
                        new Color(.10f,.22f,.30f)))
                        match.OpenSettings();
                    if(Application.platform!=RuntimePlatform.WebGLPlayer)
                        SmallButton(new Rect(x+34,y+469,142,40),"QUIT",
                            new Color(.16f,.28f,.36f),match.QuitGame);
                    Label(new Rect(x+36,y+ph-40,455,20),
                        "ARENA 01   /   MASTERED ROUTES",micro);
                    break;
                case ChaseMenuPage.Difficulty:
                    Back(x+33,y+166);
                    Label(new Rect(x+35,y+231,465,42),"PICK YOUR RIVAL",
                        Text(29,Cream,FontStyle.Bold));
                    DifficultyButton(x+34,y+292,"ROOKIE","A relaxed warm-up",ChaseDifficulty.Rookie);
                    DifficultyButton(x+266,y+292,"RUNNER","Keeps up with you",ChaseDifficulty.Runner);
                    DifficultyButton(x+34,y+397,"RIVAL","Reads your angles",ChaseDifficulty.Rival);
                    DifficultyButton(x+266,y+397,"PHANTOM","Fastest reactions",ChaseDifficulty.Phantom);
                    Label(new Rect(x+36,y+ph-41,460,23),
                        "ALL RIVALS PLAY BY THE SAME PHYSICS",micro);
                    break;
                case ChaseMenuPage.Arena:
                    Back(x+33,y+166);
                    Label(new Rect(x+35,y+240,440,28),"SELECT ARENA",small);
                    Tile(new Rect(x+34,y+286,452,167),
                        new Color(.09f,.21f,.29f,.96f));
                    Label(new Rect(x+57,y+303,410,34),"01   /   MASTERED ROUTES",
                        Text(24,Cream,FontStyle.Bold));
                    Tile(new Rect(x+57,y+352,57,5),Gold);
                    Label(new Rect(x+57,y+373,396,28),
                        "Crossline   ·   Switchback   ·   Elevation",
                        Text(16,Cream));
                    Label(new Rect(x+57,y+410,390,25),
                        "One arena. Countless lines.",small);
                    if(Button(new Rect(x+34,y+481,452,64),"PLAY ARENA  >",Mint,true))
                        match.StartSelectedArena();
                    break;
                case ChaseMenuPage.HowTo:
                    Back(x+33,y+161);
                    Label(new Rect(x+35,y+229,450,42),"FIND YOUR LINE",
                        Text(29,Cream,FontStyle.Bold));
                    Label(new Rect(x+35,y+276,430,28),
                        "Run, dive, slide. Keep moving.",body);
                    Controls(new Rect(x+28,y+334,485,145),1f);
                    Label(new Rect(x+37,y+496,430,24),
                        "Chaser tags. Evader survives 20 seconds.",small);
                    break;
                case ChaseMenuPage.Settings:
                    Back(x+33,y+166);
                    Label(new Rect(x+35,y+239,445,42),"YOUR DISPLAY",
                        Text(29,Cream,FontStyle.Bold));
                    if(Button(new Rect(x+34,y+310,452,65),
                        "CONTROL KEYS     "+(match.ShowKeyHints?"ON":"OFF"),
                        new Color(.10f,.22f,.30f)))
                        match.SetKeyHints(!match.ShowKeyHints);
                    if(Button(new Rect(x+34,y+389,452,65),
                        "KEY HIGHLIGHTS     "+(match.HighlightKeys?"ON":"OFF"),
                        new Color(.10f,.22f,.30f)))
                        match.SetKeyHighlight(!match.HighlightKeys);
                    Label(new Rect(x+37,y+482,437,44),
                        "Changes apply immediately.",small);
                    break;
            }
        }

        void DifficultyButton(float x,float y,string name,string info,
            ChaseDifficulty choice)
        {
            Rect card=new Rect(x,y,219,90);
            bool hover=card.Contains(Event.current.mousePosition);
            Tile(card,hover?new Color(.14f,.29f,.38f):new Color(.09f,.20f,.27f));
            Label(new Rect(x+19,y+12,182,31),name,
                Text(21,Cream,FontStyle.Bold));
            Label(new Rect(x+19,y+49,188,22),info,Text(13,Muted));
            if(GUI.Button(card,GUIContent.none,GUIStyle.none))
                match.SelectDifficulty(choice);
        }

        bool IsDown(string key)
        {
            if(!match.HighlightKeys)return false;
            var kb=Keyboard.current;
            if(kb==null)return false;
            switch(key)
            {
                case "W":return kb.wKey.isPressed;
                case "A":return kb.aKey.isPressed;
                case "S":return kb.sKey.isPressed;
                case "D":return kb.dKey.isPressed;
                case "SPACE":return kb.spaceKey.isPressed;
                case "↑":return kb.upArrowKey.isPressed;
                case "↓":return kb.downArrowKey.isPressed;
                case "SHIFT":return kb.leftShiftKey.isPressed;
                default:return false;
            }
        }

        void Key(float x,float y,float width,string glyph)
        {
            bool active=IsDown(glyph);
            Tile(new Rect(x,y,width,34),active?Mint:
                new Color(.16f,.31f,.41f,.95f));
            if(glyph=="↑" || glyph=="↓")
            {
                Color old=GUI.color;
                GUI.color=active?Ink:Cream;
                GUI.DrawTexture(new Rect(x+(width-22)*.5f,y+6,22,22),
                    glyph=="↑"?arrowUp:arrowDown,ScaleMode.StretchToFill,true);
                GUI.color=old;
            }
            else Label(new Rect(x,y,width,34),glyph,
                active?Text(17,Ink,FontStyle.Bold,TextAnchor.MiddleCenter):keyText);
        }

        void ControlAction(float x,float y,string glyph,string caption,float wide)
        {
            Key(x,y,wide,glyph);
            Label(new Rect(x-7,y+38,wide+14,19),caption,keyLabel);
        }

        void Controls(Rect rect,float opacity)
        {
            Color prior=GUI.color;
            GUI.color=new Color(1,1,1,opacity);
            Tile(rect,new Color(.055f,.13f,.19f,.87f));
            float x=rect.x+19,y=rect.y+23;
            Label(new Rect(x,y-15,135,18),"MOVE",micro);
            Key(x+43,y,33,"W");
            Key(x+4,y+38,33,"A");
            Key(x+43,y+38,33,"S");
            Key(x+82,y+38,33,"D");
            float ax=x+147;
            ControlAction(ax,y+14,"SPACE","JUMP",70);
            ControlAction(ax+82,y+14,"↑","DIVE",42);
            ControlAction(ax+136,y+14,"↓","SLIDE",42);
            ControlAction(ax+190,y+14,"SHIFT","CROUCH",66);
            GUI.color=prior;
        }

        void Scoreboard(float w)
        {
            const float width=600,height=77;
            float x=(w-width)*.5f,y=17;
            Tile(new Rect(x,y,width,height),PanelColor);
            Tile(new Rect(x+6,y+6,178,height-12),
                new Color(.38f,.12f,.19f,.87f));
            Tile(new Rect(x+width-184,y+6,178,height-12),
                new Color(.11f,.31f,.50f,.87f));
            Label(new Rect(x+22,y+12,128,20),"CHASER",Text(13,Coral,FontStyle.Bold));
            Label(new Rect(x+22,y+31,142,34),
                match.ChaserWins.ToString("00"),Text(28,Cream,FontStyle.Bold));
            Label(new Rect(x+width-162,y+12,142,20),
                "EVADER",Text(13,Blue,FontStyle.Bold,TextAnchor.MiddleRight));
            Label(new Rect(x+width-161,y+31,140,34),
                match.EvaderWins.ToString("00"),
                Text(28,Cream,FontStyle.Bold,TextAnchor.MiddleRight));
            Label(new Rect(x+195,y+5,211,22),
                "ROUND "+match.RoundNumber.ToString("00"),
                Text(12,Muted,FontStyle.Bold,TextAnchor.MiddleCenter));
            Label(new Rect(x+195,y+25,211,44),
                match.Remaining.ToString("00.0"),
                Text(38,match.Remaining<=5f?Gold:Cream,
                    FontStyle.Bold,TextAnchor.MiddleCenter));
            // A single unobtrusive marker identifies which role the local
            // player controls, without duplicating the broadcast scoreboard.
            bool chaser=match.HumanRole==ChaseRole.Chaser;
            Label(new Rect(chaser?x+136:x+width-180,y+12,43,18),"YOU",
                Text(11,Mint,FontStyle.Bold,
                    chaser?TextAnchor.MiddleRight:TextAnchor.MiddleLeft));
            Tile(new Rect(chaser?x+140:x+width-182,y+61,34,5),Mint);
        }

        void Overlay(float w,float h,Color shade)
        {
            GUI.DrawTexture(new Rect(0,0,w,h),Texture2D.whiteTexture,
                ScaleMode.StretchToFill,true,0,shade,0,0);
        }

        void Countdown(float w,float h)
        {
            float x=(w-330)*.5f,y=h*.30f;
            Tile(new Rect(x,y,330,172),new Color(.065f,.15f,.21f,.88f));
            Label(new Rect(x+22,y+20,286,91),
                Mathf.Max(1,Mathf.CeilToInt(match.CountdownRemaining)).ToString(),large);
            Label(new Rect(x+28,y+114,274,32),
                "GET READY",Text(16,Gold,FontStyle.Bold,TextAnchor.MiddleCenter));
        }

        void Go(float w,float h)
        {
            Label(new Rect((w-400)*.5f,h*.31f,400,105),"CHASE!",
                Text(67,Cream,FontStyle.Bold,TextAnchor.MiddleCenter));
        }

        void OverlayCard(float w,float h,string kicker,string headline,
            string description)
        {
            float x=(w-510)*.5f,y=(h-352)*.5f;
            Tile(new Rect(x,y,510,352),new Color(.045f,.12f,.18f,.97f));
            Tile(new Rect(x+44,y+36,55,5),Mint);
            Label(new Rect(x+44,y+58,425,24),kicker,
                Text(14,Muted,FontStyle.Bold));
            Label(new Rect(x+40,y+86,430,87),headline,
                Text(47,Cream,FontStyle.Bold,TextAnchor.MiddleCenter));
            Label(new Rect(x+45,y+176,420,28),description,
                Text(16,Muted,FontStyle.Normal,TextAnchor.MiddleCenter));
        }

        void Paused(float w,float h)
        {
            Overlay(w,h,new Color(.015f,.04f,.07f,.54f));
            OverlayCard(w,h,"ARENA 01","PAUSED","Your next move can wait.");
            float x=(w-510)*.5f,y=(h-352)*.5f;
            if(Button(new Rect(x+44,y+221,422,47),"RESUME",Mint,true))
                match.ResumeMatch();
            SmallButton(new Rect(x+44,y+280,202,45),"RESTART",
                new Color(.22f,.37f,.45f),match.Rematch);
            SmallButton(new Rect(x+264,y+280,202,45),"MAIN MENU",
                new Color(.22f,.37f,.45f),match.ReturnToMenu);
        }

        void Result(float w,float h)
        {
            Overlay(w,h,new Color(.015f,.04f,.07f,.60f));
            bool caught=match.Winner==ChaseRole.Chaser;
            OverlayCard(w,h,"ROUND "+match.RoundNumber.ToString("00"),
                caught?"TAGGED!":"ESCAPED!",
                caught?"Chaser takes the point.":"Evader survived the clock.");
            float x=(w-510)*.5f,y=(h-352)*.5f;
            if(Button(new Rect(x+44,y+221,422,47),"NEXT ROUND",Mint,true))
                match.NextRound();
            SmallButton(new Rect(x+44,y+280,132,45),"REMATCH",
                new Color(.22f,.37f,.45f),match.Rematch);
            SmallButton(new Rect(x+188,y+280,133,45),"SWAP ROLE",
                new Color(.22f,.37f,.45f),match.SwitchRole);
            SmallButton(new Rect(x+333,y+280,133,45),"MENU",
                new Color(.22f,.37f,.45f),match.ReturnToMenu);
        }

        void OnGUI()
        {
            if(match==null)return;
            Setup();
            float screenWidth=Mathf.Max(1,Screen.width);
            float screenHeight=Mathf.Max(1,Screen.height);
            // Fixed layout units; uniformly scale down on 960px or portrait
            // WebGL while keeping main arena visible beneath the overlay.
            float scale=Mathf.Min(screenWidth/1280f,screenHeight/720f);
            float w=screenWidth/scale,h=screenHeight/scale;
            Matrix4x4 saved=GUI.matrix;
            int oldDepth=GUI.depth;
            GUI.depth=-80;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            if(match.MenuPage!=ChaseMenuPage.InMatch)
            {
                Backdrop(w,h);
                MenuPanel(w,h);
            }
            else
            {
                Scoreboard(w);
                if(match.ShowKeyHints &&
                    (match.Phase==ChasePhase.Active || match.Phase==ChasePhase.Countdown))
                {
                    float opacity=Time.unscaledTime<hintBrightUntil?1f:.70f;
                    Controls(new Rect(22,h-141,485,119),opacity);
                }
                switch(match.Phase)
                {
                    case ChasePhase.Countdown:Countdown(w,h);break;
                    case ChasePhase.Active:
                        if(Time.unscaledTime<goUntil)Go(w,h);
                        break;
                    case ChasePhase.Paused:Paused(w,h);break;
                    case ChasePhase.Ended:Result(w,h);break;
                }
            }
            GUI.matrix=saved;
            GUI.depth=oldDepth;
        }

        void OnDestroy()
        {
            if(rounded!=null)Destroy(rounded);
            if(arrowUp!=null)Destroy(arrowUp);
            if(arrowDown!=null)Destroy(arrowDown);
        }
    }
}
