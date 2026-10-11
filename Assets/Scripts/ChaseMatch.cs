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
        public ChaseTuning Tuning => tuning;
        public ChasePhase Phase { get; private set; }
        public ChaseRole HumanRole { get; private set; } = ChaseRole.Evader;
        public ChaseRole? Winner { get; private set; }
        public float Remaining { get; private set; }
        public float CountdownRemaining { get; private set; }
        public int ChaserWins { get; private set; }
        public int EvaderWins { get; private set; }
        public int RoundNumber { get; private set; } = 1;
        public ChaseMotor Chaser { get; private set; }
        public ChaseMotor Evader { get; private set; }
        public ChaseMotor Human => HumanRole == ChaseRole.Evader ? Evader : Chaser;
        public ChaseMotor Opponent => HumanRole == ChaseRole.Evader ? Chaser : Evader;
        public Camera ArenaCamera => arenaCamera;
        public event Action<ChaseRole> RoundEnded;

        ChaseBot bot;
        ChaseKeyboard keyboard;
        ChaseCameraRig cameraRig;
        IChaseActionSource chaserSource;
        IChaseActionSource evaderSource;
        ChasePhase beforePause;
        bool timeoutPending;
        float activePhysicsClock;
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
            gameObject.AddComponent<ChaseHUD>();
            Chaser = CreateCompetitor("CHASER", ChaseRole.Chaser, new Color(.92f,.22f,.25f));
            Evader = CreateCompetitor("EVADER", ChaseRole.Evader, new Color(.16f,.55f,1f));
            cameraRig.Initialize(this);
            BeginRound(false);
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
            bot.Initialize(Opponent, Human, HumanRole == ChaseRole.Evader ?
                ChaseRole.Chaser : ChaseRole.Evader);
            chaserSource = HumanRole == ChaseRole.Chaser ? (IChaseActionSource)keyboard : bot;
            evaderSource = HumanRole == ChaseRole.Evader ? (IChaseActionSource)keyboard : bot;
            chaserSource.ResetSource();
            evaderSource.ResetSource();
            Physics.SyncTransforms();
            cameraRig.ResetView();
        }

        void Update()
        {
            Keyboard keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.escapeKey.wasPressedThisFrame)
                {
                    if (Phase == ChasePhase.Paused)
                    { Phase = beforePause; Time.timeScale = 1f; }
                    else if (Phase != ChasePhase.Ended)
                    { beforePause = Phase; Phase = ChasePhase.Paused; Time.timeScale = 0f; }
                }
                if (keys.tabKey.wasPressedThisFrame && Phase == ChasePhase.Ended)
                    HumanRole = HumanRole == ChaseRole.Evader ? ChaseRole.Chaser : ChaseRole.Evader;
                if (keys.rKey.wasPressedThisFrame)
                {
                    Time.timeScale = 1f;
                    BeginRound(true);
                }
                else if (keys.enterKey.wasPressedThisFrame && Phase == ChasePhase.Ended)
                {
                    RoundNumber++;
                    BeginRound(false);
                }
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
            if (Phase != ChasePhase.Active) return;
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
