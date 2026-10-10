using UnityEngine;

namespace Chase
{
    public sealed class ChaseHUD : MonoBehaviour
    {
        ChaseMatch match;
        GUIStyle header, details, center, small;
        void Awake() { match = GetComponent<ChaseMatch>(); }

        void Setup()
        {
            if (header != null) return;
            header = new GUIStyle(GUI.skin.label) {
                fontSize=22,fontStyle=FontStyle.Bold,
                normal={textColor=new Color(.96f,.98f,1f)}
            };
            details = new GUIStyle(header) { fontSize=16,fontStyle=FontStyle.Normal };
            center = new GUIStyle(header) {
                fontSize=42,alignment=TextAnchor.MiddleCenter,
                normal={textColor=Color.white}
            };
            small = new GUIStyle(details) { fontSize=14 };
        }
        void Block(Rect rect, Color shade)
        {
            Color original=GUI.color;
            GUI.color=shade;
            GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=original;
        }
        void OnGUI()
        {
            if (match == null) return;
            Setup();
            float w=Screen.width,h=Screen.height;
            Block(new Rect(14,12,320,108),new Color(.035f,.065f,.12f,.83f));
            GUI.Label(new Rect(28,19,300,32),"CHASE  /  ROUND "+match.RoundNumber,header);
            GUI.Label(new Rect(28,55,300,25),"YOU: "+match.HumanRole.ToString().ToUpperInvariant(),details);
            GUI.Label(new Rect(28,82,300,25),"SCORE   RED "+match.ChaserWins+"  :  "+match.EvaderWins+" BLUE",details);
            Block(new Rect(w-176,12,162,75),new Color(.035f,.065f,.12f,.83f));
            GUI.Label(new Rect(w-162,16,150,27),"TIME LEFT",small);
            GUI.Label(new Rect(w-162,40,155,40),match.Remaining.ToString("00.0")+" s",header);
            Block(new Rect(14,h-59,Mathf.Min(w-28,780),45),new Color(.035f,.065f,.12f,.80f));
            GUI.Label(new Rect(24,h-52,w-45,36),
                "WASD MOVE    UP / SPACE JUMP-VAULT    DOWN / SHIFT DUCK-SLIDE    R RESTART    ESC PAUSE",small);
            if (match.Phase == ChasePhase.Active) return;
            float panelWidth=Mathf.Min(500,w-30);
            Rect rect=new Rect((w-panelWidth)/2f,h*.23f,panelWidth,137);
            Block(rect,new Color(.035f,.065f,.12f,.84f));
            string main="",sub="";
            if (match.Phase == ChasePhase.Countdown)
            { main=Mathf.CeilToInt(match.CountdownRemaining).ToString();sub="GET READY"; }
            else if (match.Phase == ChasePhase.Paused)
            { main="PAUSED";sub="ESC TO RESUME  /  R TO RESTART"; }
            else if (match.Phase == ChasePhase.Ended)
            {
                main=match.Winner==ChaseRole.Chaser ? "RED TAGS!" : "BLUE SURVIVES!";
                sub="ENTER NEXT ROUND    R RETRY    TAB SWITCH ROLE";
            }
            GUI.Label(new Rect(rect.x,rect.y+16,rect.width,65),main,center);
            GUIStyle subStyle=new GUIStyle(details) { alignment=TextAnchor.MiddleCenter,fontSize=15 };
            GUI.Label(new Rect(rect.x+5,rect.y+91,rect.width-10,29),sub,subStyle);
        }
    }
}
