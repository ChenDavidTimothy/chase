using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chase
{
    [DefaultExecutionOrder(-100)]
    public sealed class ChaseMatch : MonoBehaviour
    {
        [SerializeField] ChaseTuning tuning = new ChaseTuning();
        [SerializeField] Camera arenaCamera;
        // Enabled only on the authored public production scene. Dynamically
        // spawned test matches remain auto-starting for physics regressions.
        [SerializeField] bool showFrontEnd;
        public bool FrontEndEnabled => showFrontEnd;
        public ChaseMenuPage MenuPage { get; private set; } = ChaseMenuPage.InMatch;
        public ChaseDifficulty Difficulty { get; private set; } = ChaseDifficulty.Runner;
        public bool ShowKeyHints { get; private set; } = true;
        public bool HighlightKeys { get; private set; } = true;
        public ChaseTuning Tuning => tuning;
        public ChasePhase Phase { get; private set; }
        public ChaseRole HumanRole { get; private set; } = ChaseRole.Evader;
        public ChaseRole? Winner { get; private set; }
        public float Remaining { get; private set; }
        public float CountdownRemaining { get; private set; }
        public int ChaserWins { get; private set; }
        public int EvaderWins { get; private set; }
        // Competitor scores must stay attached to the player/opponent even
        // when a round changes which one wears the Chaser role.
        public int PlayerWins { get; private set; }
        public int BotWins { get; private set; }
        public bool FocusSuspended { get; private set; }
        public int MenuSelection { get; private set; }
        public bool MenuKeyboardActive { get; private set; }
        public int RoundNumber { get; private set; } = 1;
        public ChaseMotor Chaser { get; private set; }
        public ChaseMotor Evader { get; private set; }
        public ChaseMotor Human => HumanRole == ChaseRole.Evader ? Evader : Chaser;
        public ChaseMotor Opponent => HumanRole == ChaseRole.Evader ? Chaser : Evader;
        public Camera ArenaCamera => arenaCamera;
        public event Action<ChaseRole> RoundEnded;

        ChaseBot bot;
        ChaseMenuShowcase menuShowcase;
        ChaseKeyboard keyboard;
        ChaseCameraRig cameraRig;
        IChaseActionSource chaserSource;
        IChaseActionSource evaderSource;
        ChasePhase beforePause;
        bool timeoutPending;
        float activePhysicsClock;
        bool applicationFocused = true;
        public bool ApplicationFocused => applicationFocused;
        int currentSpawn;

        // Pair positions are separated by solid routes so the 3-second countdown
        // is followed by genuine reaction time, not an immediate contact tag.
        static readonly Vector3[,] SpawnPairs = {
            { new Vector3(-4.6f, .9f, -1.4f), new Vector3(4.6f, .9f, 3.9f) },
            { new Vector3(4.6f, .9f, 1.4f), new Vector3(-4.6f, .9f, -3.8f) },
            { new Vector3(-1.2f, .9f, 4.6f), new Vector3(3.8f, .9f, -4.4f) },
            { new Vector3(-1.5f, .9f, -4.7f), new Vector3(3.2f, .9f, 4.4f) }
        };

        void Awake()
        {
            Time.fixedDeltaTime = 0.02f;
            if (arenaCamera == null) arenaCamera = Camera.main;
            if (arenaCamera == null)
            {
                var cameraObject = new GameObject("Chase Camera");
                arenaCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            cameraRig = arenaCamera.GetComponent<ChaseCameraRig>();
            if (cameraRig == null) cameraRig = arenaCamera.gameObject.AddComponent<ChaseCameraRig>();
            keyboard = gameObject.AddComponent<ChaseKeyboard>();
            bot = gameObject.AddComponent<ChaseBot>();
            if (showFrontEnd)
            {
                ShowKeyHints = PlayerPrefs.GetInt("CHASE_Hints",1)!=0;
                HighlightKeys = PlayerPrefs.GetInt("CHASE_Highlights",1)!=0;
                MenuPage = ChaseMenuPage.MainMenu;
            }
            gameObject.AddComponent<ChaseHUD>();
            Chaser = CreateCompetitor("CHASER", ChaseRole.Chaser, new Color(.92f,.22f,.25f));
            Evader = CreateCompetitor("EVADER", ChaseRole.Evader, new Color(.16f,.55f,1f));
            cameraRig.Initialize(this);
            BeginRound(false);
            if (showFrontEnd)
            {
                SetActorsVisible(false);
                menuShowcase=gameObject.AddComponent<ChaseMenuShowcase>();
                menuShowcase.Initialize(arenaCamera);
            }
        }

        void SetActorsVisible(bool visible)
        {
            Chaser.gameObject.SetActive(visible);
            Evader.gameObject.SetActive(visible);
        }

        void SyncPresentation()
        {
            if (!showFrontEnd) return;
            if (menuShowcase!=null) menuShowcase.SetPage(MenuPage);
        }

        ChaseMotor CreateCompetitor(string label, ChaseRole role, Color color)
        {
            GameObject actor = new GameObject(label);
            actor.transform.SetParent(transform, true);
            actor.transform.position = new Vector3(0f,.9f,0f);
            Rigidbody body = actor.AddComponent<Rigidbody>();
            CapsuleCollider collider = actor.AddComponent<CapsuleCollider>();
            collider.height = tuning.standingHeight;
            collider.radius = tuning.radius;
            GameObject artPrefab = Resources.Load<GameObject>("ChibiVisual");
            GameObject visual = artPrefab != null ?
                Instantiate(artPrefab, actor.transform, false) :
                GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = artPrefab != null ? "Chibi Visual / animation only" : "Body / front marker";
            if (artPrefab == null)
            {
                visual.transform.SetParent(actor.transform, false);
                visual.transform.localScale = new Vector3(tuning.radius * 2f,
                    tuning.standingHeight * .5f, tuning.radius * 2f);
                // Primitive mesh colliders would otherwise remain active until the
                // next frame, creating transient extra physical bodies.
                Collider visualCollider=visual.GetComponent<Collider>();
                visualCollider.enabled=false;
                if (Application.isPlaying) Destroy(visualCollider);
                else DestroyImmediate(visualCollider);
                Material material = new Material(Shader.Find("Standard"));
                material.color = color;
                visual.GetComponent<Renderer>().material = material;
                // Unity's WebGL code stripping may omit SphereCollider when no sphere
                // exists in the authored scene. A small cube uses the already
                // included BoxCollider (immediately disabled), so the visual marker
                // does not introduce a platform-specific physics dependency.
                GameObject front = GameObject.CreatePrimitive(PrimitiveType.Cube);
                front.name = "Facing indicator";
                front.transform.SetParent(visual.transform, false);
                front.transform.localPosition = new Vector3(0f,.25f,.53f);
                front.transform.localScale = new Vector3(.42f,.28f,.2f);
                front.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) {
                    color = new Color(.97f,.96f,.72f)
                };
                Collider frontCollider=front.GetComponent<Collider>();
                frontCollider.enabled=false;
                if (Application.isPlaying) Destroy(frontCollider);
                else DestroyImmediate(frontCollider);
            }
            else
            {
                // Physics actors are centered at standing capsule midpoint.
                // The normalized art pivot is at the feet. This deliberate,
                // documented -height/2 visual anchor is NOT an axis workaround.
                // The FBX/prefab itself has identity transform and unit scale.
                visual.transform.localPosition = Vector3.down * (tuning.standingHeight * 0.5f);
                // Role cues are provisional material property overrides, not
                // irreversible textures or mesh edits.
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    Color tint = renderer.name.StartsWith("face") ?
                        new Color(.075f,.09f,.11f) :
                        renderer.name.StartsWith("head") ?
                        new Color(.80f,.83f,.86f) : color;
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_Color", tint);
                    block.SetColor("_BaseColor", tint);
                    renderer.SetPropertyBlock(block);
                }
            }
            ChaseMotor motor = actor.AddComponent<ChaseMotor>();
            motor.Initialize(tuning, visual.transform);
            // Awake on the visual ran before ChaseMotor existed on this actor.
            ChibiVisualDriver driver = visual.GetComponent<ChibiVisualDriver>();
            if (driver != null) driver.BindMotor(motor);
            ChaseTagContact tag = actor.AddComponent<ChaseTagContact>();
            tag.Initialize(this,role);
            return motor;
        }

        void BeginRound(bool restart)
        {
            if (!restart)
            {
                System.Random rng = new System.Random(tuning.spawnSeed + (RoundNumber - 1) * 17);
                currentSpawn = rng.Next(0, SpawnPairs.GetLength(0));
            }
            Winner = null;
            timeoutPending = false;
            activePhysicsClock = 0f;
            Remaining = tuning.roundDuration;
            CountdownRemaining = tuning.countdown;
            Phase = ChasePhase.Countdown;
            Vector3 first = SpawnPairs[currentSpawn,0], second = SpawnPairs[currentSpawn,1];
            Chaser.ResetMotor(HumanRole == ChaseRole.Chaser ? first : second);
            Evader.ResetMotor(HumanRole == ChaseRole.Evader ? first : second);
            bot.Configure(Difficulty);
            bot.Initialize(Opponent, Human, HumanRole == ChaseRole.Evader ?
                ChaseRole.Chaser : ChaseRole.Evader);
            chaserSource = HumanRole == ChaseRole.Chaser ? (IChaseActionSource)keyboard : bot;
            evaderSource = HumanRole == ChaseRole.Evader ? (IChaseActionSource)keyboard : bot;
            chaserSource.ResetSource();
            evaderSource.ResetSource();
            Physics.SyncTransforms();
            cameraRig.ResetView();
        }

        public void OpenPlay()
        {
            if (showFrontEnd && MenuPage == ChaseMenuPage.MainMenu)
            {
                MenuPage = ChaseMenuPage.Difficulty;
                ResetMenuSelection();
                SyncPresentation();
            }
        }

        public void SelectDifficulty(ChaseDifficulty selection)
        {
            if (!showFrontEnd || MenuPage != ChaseMenuPage.Difficulty) return;
            Difficulty = selection;
            MenuPage = ChaseMenuPage.Arena;
            ResetMenuSelection();
            SyncPresentation();
        }

        public void StartSelectedArena()
        {
            if (!showFrontEnd || MenuPage != ChaseMenuPage.Arena) return;
            Time.timeScale = 1f;
            RoundNumber = 1;
            ChaserWins = 0;
            EvaderWins = 0;
            PlayerWins = 0;
            BotWins = 0;
            HumanRole = ChaseRole.Evader;
            MenuPage = ChaseMenuPage.InMatch;
            FocusSuspended = false;
            MenuKeyboardActive = false;
            SyncPresentation();
            SetActorsVisible(true);
            BeginRound(false);
        }

        public void OpenHowTo()
        {
            if (showFrontEnd && MenuPage == ChaseMenuPage.MainMenu)
            { MenuPage = ChaseMenuPage.HowTo; ResetMenuSelection(); SyncPresentation(); }
        }

        public void OpenSettings()
        {
            if (showFrontEnd && MenuPage == ChaseMenuPage.MainMenu)
            { MenuPage = ChaseMenuPage.Settings; ResetMenuSelection(); SyncPresentation(); }
        }

        public void SetKeyHints(bool enabled)
        {
            ShowKeyHints = enabled;
            if (showFrontEnd)
            { PlayerPrefs.SetInt("CHASE_Hints",enabled?1:0); PlayerPrefs.Save(); }
        }

        public void SetKeyHighlight(bool enabled)
        {
            HighlightKeys = enabled;
            if (showFrontEnd)
            { PlayerPrefs.SetInt("CHASE_Highlights",enabled?1:0); PlayerPrefs.Save(); }
        }

        public void MenuBack()
        {
            if (!showFrontEnd) return;
            switch (MenuPage)
            {
                case ChaseMenuPage.Difficulty:
                case ChaseMenuPage.HowTo:
                case ChaseMenuPage.Settings:
                    MenuPage = ChaseMenuPage.MainMenu; break;
                case ChaseMenuPage.Arena:
                    MenuPage = ChaseMenuPage.Difficulty; break;
            }
            ResetMenuSelection();
            SyncPresentation();
        }

        public void ReturnToMenu()
        {
            if (!showFrontEnd) return;
            Time.timeScale = 1f;
            keyboard.Clear();
            MenuPage = ChaseMenuPage.MainMenu;
            FocusSuspended = false;
            ResetMenuSelection();
            SetActorsVisible(false);
            cameraRig.ResetView();
            SyncPresentation();
        }

        public void ResumeMatch()
        {
            if (MenuPage != ChaseMenuPage.InMatch || Phase != ChasePhase.Paused)
                return;
            Phase = beforePause;
            Time.timeScale = 1f;
            keyboard.Clear();
            FocusSuspended = false;
        }

        public void Rematch()
        {
            if (MenuPage != ChaseMenuPage.InMatch) return;
            Time.timeScale = 1f;
            FocusSuspended = false;
            BeginRound(true);
        }

        public void NextRound()
        {
            if (MenuPage != ChaseMenuPage.InMatch || Phase != ChasePhase.Ended)
                return;
            RoundNumber++;
            BeginRound(false);
        }

        public void SwitchRole()
        {
            if (MenuPage != ChaseMenuPage.InMatch || Phase != ChasePhase.Ended)
                return;
            HumanRole = HumanRole == ChaseRole.Evader ? ChaseRole.Chaser : ChaseRole.Evader;
            RoundNumber++;
            BeginRound(false);
        }

        public void QuitGame()
        {
            if (!showFrontEnd) return;
            Application.Quit();
        }

        int MenuOptionCount()
        {
            switch(MenuPage)
            {
                case ChaseMenuPage.MainMenu:
                    return Application.platform==RuntimePlatform.WebGLPlayer?3:4;
                case ChaseMenuPage.Difficulty: return 5; // back + four rivals
                case ChaseMenuPage.Arena: return 2;
                case ChaseMenuPage.HowTo: return 1;
                case ChaseMenuPage.Settings: return 3;
                default:return Phase==ChasePhase.Paused?3:
                    Phase==ChasePhase.Ended?4:0;
            }
        }
        void ResetMenuSelection()
        {
            MenuSelection=0;
            MenuKeyboardActive=false;
        }
        public bool IsMenuSelection(int index) => MenuKeyboardActive &&
            MenuSelection==index;
        public void MenuPointerInput() { MenuKeyboardActive=false; }

        // One menu selection state shared by mouse and keyboard. Order is
        // visual reading order. No InputAction asset or second EventSystem.
        void NavigateMenu(int delta)
        {
            int count=MenuOptionCount();
            if(count<=0)return;
            if(!MenuKeyboardActive) {MenuKeyboardActive=true;MenuSelection=0;}
            else MenuSelection=(MenuSelection+delta+count)%count;
        }
        void ConfirmMenu()
        {
            switch(MenuPage)
            {
                case ChaseMenuPage.MainMenu:
                    if(MenuSelection==0) OpenPlay();
                    else if(MenuSelection==1) OpenHowTo();
                    else if(MenuSelection==2) OpenSettings();
                    else QuitGame();
                    break;
                case ChaseMenuPage.Difficulty:
                    if(MenuSelection==0) MenuBack();
                    else SelectDifficulty((ChaseDifficulty)(MenuSelection-1));
                    break;
                case ChaseMenuPage.Arena:
                    if(MenuSelection==0) MenuBack();
                    else StartSelectedArena();
                    break;
                case ChaseMenuPage.HowTo: MenuBack();break;
                case ChaseMenuPage.Settings:
                    if(MenuSelection==0) MenuBack();
                    else if(MenuSelection==1) SetKeyHints(!ShowKeyHints);
                    else SetKeyHighlight(!HighlightKeys);
                    break;
                case ChaseMenuPage.InMatch:
                    if(Phase==ChasePhase.Paused)
                    {
                        if(MenuSelection==0) ResumeMatch();
                        else if(MenuSelection==1) Rematch();
                        else ReturnToMenu();
                    }
                    else if(Phase==ChasePhase.Ended)
                    {
                        if(MenuSelection==0) NextRound();
                        else if(MenuSelection==1) Rematch();
                        else if(MenuSelection==2) SwitchRole();
                        else ReturnToMenu();
                    }
                    break;
            }
        }

        void PauseForFocusLoss()
        {
            if(!showFrontEnd || MenuPage!=ChaseMenuPage.InMatch ||
               Phase==ChasePhase.Paused || Phase==ChasePhase.Ended)return;
            beforePause=Phase;
            Phase=ChasePhase.Paused;
            Time.timeScale=0f;
            keyboard?.Clear();
            FocusSuspended=true;
            ResetMenuSelection();
        }
        void OnApplicationFocus(bool hasFocus)
        {
            applicationFocused=hasFocus;
            if(!hasFocus)PauseForFocusLoss();
            else keyboard?.Clear();
        }
        void OnApplicationPause(bool paused)
        {
            applicationFocused=!paused;
            if(paused)PauseForFocusLoss();
            else keyboard?.Clear();
        }

        void Update()
        {
            Keyboard keys = Keyboard.current;
            bool menuOpen=MenuPage!=ChaseMenuPage.InMatch ||
                Phase==ChasePhase.Paused || Phase==ChasePhase.Ended;
            if(menuOpen)
            {
                keyboard.Clear();
                if(keys==null)return;
                if(keys.escapeKey.wasPressedThisFrame)
                {
                    if(MenuPage!=ChaseMenuPage.InMatch)MenuBack();
                    else if(Phase==ChasePhase.Paused)ResumeMatch();
                    return;
                }
                if(keys.upArrowKey.wasPressedThisFrame ||
                   keys.leftArrowKey.wasPressedThisFrame)
                    NavigateMenu(-1);
                else if(keys.downArrowKey.wasPressedThisFrame ||
                        keys.rightArrowKey.wasPressedThisFrame ||
                        keys.tabKey.wasPressedThisFrame)
                    NavigateMenu(1);
                if(keys.enterKey.wasPressedThisFrame ||
                   keys.numpadEnterKey.wasPressedThisFrame ||
                   keys.spaceKey.wasPressedThisFrame)
                    ConfirmMenu();
                return;
            }
            if(keys!=null && keys.escapeKey.wasPressedThisFrame)
            {
                beforePause=Phase;
                Phase=ChasePhase.Paused;
                Time.timeScale=0f;
                keyboard.Clear();
                ResetMenuSelection();
                return;
            }
            // Legacy developer keyboard shortcuts remain available only
            // in regression matches, never in the public presentation.
            if(!showFrontEnd && keys!=null && keys.rKey.wasPressedThisFrame)
            {
                Time.timeScale=1f;BeginRound(true);return;
            }
            if (Phase == ChasePhase.Active)
                keyboard.Poll(arenaCamera.transform);
            else keyboard.Clear();

            if (Phase == ChasePhase.Countdown)
            {
                CountdownRemaining = Mathf.Max(0f, CountdownRemaining - Time.deltaTime);
                if (CountdownRemaining <= 0f) Phase = ChasePhase.Active;
            }
        }

        // A self-play harness can replace either source without changing match rules
        // or the physical motor. Sources are reset whenever assigned.
        public void AssignActionSource(ChaseRole role, IChaseActionSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (role == ChaseRole.Chaser) chaserSource = source;
            else evaderSource = source;
            source.ResetSource();
        }

        void FixedUpdate()
        {
            if (MenuPage != ChaseMenuPage.InMatch || Phase != ChasePhase.Active) return;
            // Physics collision callbacks for the previous step have already run.
            // Deferring timeout gives collision/tag priority in the final step.
            if (timeoutPending) { Finish(ChaseRole.Evader); return; }
            activePhysicsClock += Time.fixedDeltaTime;
            float now = activePhysicsClock;
            Chaser.Simulate(chaserSource.ReadAction(now), Time.fixedDeltaTime, now);
            Evader.Simulate(evaderSource.ReadAction(now), Time.fixedDeltaTime, now);
            Remaining = Mathf.Max(0f, Remaining - Time.fixedDeltaTime);
            if (Remaining <= 0f) timeoutPending = true;
        }

        public bool TryTag(ChaseTagContact source, ChaseTagContact target)
        {
            if (Phase != ChasePhase.Active || Winner.HasValue || source == null || target == null
                || source.Role != ChaseRole.Chaser || target.Role != ChaseRole.Evader)
                return false;
            Finish(ChaseRole.Chaser);
            return true;
        }

        void Finish(ChaseRole winner)
        {
            if (Phase != ChasePhase.Active || Winner.HasValue) return;
            Winner = winner;
            Phase = ChasePhase.Ended;
            timeoutPending = false;
            if (winner == ChaseRole.Chaser) ChaserWins++;
            else EvaderWins++;
            if(winner==HumanRole)PlayerWins++;
            else BotWins++;
            Chaser.Body.linearVelocity = Vector3.zero;
            Evader.Body.linearVelocity = Vector3.zero;
            RoundEnded?.Invoke(winner);
        }

        public ChaseObservation Observe(ChaseMotor actor) =>
            ChaseObservationSensor.Read(actor, actor == Chaser ? Evader : Chaser,
                actor == Chaser ? ChaseRole.Chaser : ChaseRole.Evader, Remaining);

        void OnDisable() { Time.timeScale = 1f; }
    }
}
