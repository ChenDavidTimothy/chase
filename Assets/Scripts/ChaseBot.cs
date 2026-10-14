using System.Collections.Generic;
using UnityEngine;

namespace Chase
{
    // Simple grid navigator: plans around solid tall obstacles and support posts.
    // Low passages are crouched through; low hurdles can be jumped.
    public sealed class ChaseBot : MonoBehaviour, IChaseActionSource
    {
        const int Size = 21;
        const float Spacing = 0.53f;
        readonly bool[,] walkable = new bool[Size, Size];
        readonly List<Vector3> path = new List<Vector3>();
        readonly float[] cost = new float[Size*Size];
        readonly float[] estimate = new float[Size*Size];
        readonly int[] previous = new int[Size*Size];
        readonly bool[] closed = new bool[Size*Size];
        readonly bool[] open = new bool[Size*Size];
        readonly Collider[] scanHits = new Collider[32];
        ChaseMotor motor;
        ChaseMotor opponent;
        ChaseRole role;
        float nextPlan;
        float nextChoice;
        float lastJumpTime = -10f;
        float lastTrialSlideTime = -10f;
        Vector3 trialSlideHeading;
        bool skillAwareTrial;
        Vector3 escapeGoal;
        int waypointIndex;
        bool initialized;
        ChaseDifficulty difficulty = ChaseDifficulty.Runner;
        public ChaseDifficulty Difficulty => difficulty;
        public void Configure(ChaseDifficulty setting) => difficulty=setting;

        float MoveFactor => difficulty==ChaseDifficulty.Rookie ? .72f :
            difficulty==ChaseDifficulty.Runner ? 1f : 1f;
        float ReplanPeriod => difficulty==ChaseDifficulty.Rookie ? .60f :
            difficulty==ChaseDifficulty.Runner ? .35f :
            difficulty==ChaseDifficulty.Rival ? .24f : .16f;
        float DecisionPeriod => difficulty==ChaseDifficulty.Rookie ? 1.10f :
            difficulty==ChaseDifficulty.Runner ? .70f :
            difficulty==ChaseDifficulty.Rival ? .48f : .34f;
        float Prediction => difficulty==ChaseDifficulty.Rookie ? 0f :
            difficulty==ChaseDifficulty.Runner ? .12f :
            difficulty==ChaseDifficulty.Rival ? .20f : .30f;

        public void Initialize(ChaseMotor self, ChaseMotor other, ChaseRole botRole)
        {
            motor = self;
            opponent = other;
            role = botRole;
            // Trial-only low passage links. The certified production course
            // preserves its original conservative AI routing.
            skillAwareTrial=UnityEngine.SceneManagement.SceneManager
                .GetActiveScene().name=="ArenaWCTTrial";
            ScanGrid();
            path.Clear();
            nextPlan = nextChoice = -1f;
            lastJumpTime = -10f;
            lastTrialSlideTime = -10f;
            trialSlideHeading = Vector3.zero;
            initialized = true;
        }

        void ScanGrid()
        {
            for (int x = 0; x < Size; x++)
                for (int z = 0; z < Size; z++)
                {
                    Vector3 p = ToWorld(x, z);
                    bool good = Mathf.Abs(p.x) < 5.5f && Mathf.Abs(p.z) < 5.5f;
                    if (good)
                    {
                        // Sense a *standing body column*, including low
                        // podium/ramp sides. The old waist sphere missed
                        // shallow raised geometry and repeatedly steered the
                        // bot into an unwalkable vertical side of the incline.
                        int hitCount=Physics.OverlapCapsuleNonAlloc(
                            p + Vector3.up * .35f,p + Vector3.up * 1.35f,
                            .47f,scanHits,~0,QueryTriggerInteraction.Ignore);
                        if(hitCount==scanHits.Length) good=false;
                        for(int i=0;i<hitCount && good;i++)
                        {
                            var candidate=scanHits[i];
                            bool blocker=candidate.GetComponent<ChaseBlocker>()!=null;
                            bool passableLowRoof=skillAwareTrial &&
                                candidate.GetComponent<ChaseOverheadPassage>()!=null;
                            if(blocker && !passableLowRoof)good=false;
                        }
                    }
                    walkable[x, z] = good;
                }
        }

        Vector3 ToWorld(int x, int z) =>
            new Vector3((x - (Size - 1) * .5f) * Spacing, 0f,
                        (z - (Size - 1) * .5f) * Spacing);
        Vector2Int ToCell(Vector3 p) => new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(p.x / Spacing + (Size - 1) * .5f), 0, Size - 1),
            Mathf.Clamp(Mathf.RoundToInt(p.z / Spacing + (Size - 1) * .5f), 0, Size - 1));
        int Key(int x, int z) => x + z * Size;
        Vector2Int Cell(int key) => new Vector2Int(key % Size, key / Size);

        Vector2Int ClosestOpen(Vector3 location)
        {
            Vector2Int c = ToCell(location);
            if (walkable[c.x, c.y]) return c;
            float best = float.MaxValue;
            Vector2Int winner = c;
            for (int x = 0; x < Size; x++)
                for (int z = 0; z < Size; z++)
                    if (walkable[x, z])
                    {
                        float sq = (ToWorld(x, z) - location).sqrMagnitude;
                        if (sq < best) { best = sq; winner = new Vector2Int(x, z); }
                    }
            return winner;
        }

        void PlanPath(Vector3 destination)
        {
            path.Clear();
            waypointIndex = 0;
            Vector2Int start = ClosestOpen(motor.Body.position);
            Vector2Int goal = ClosestOpen(destination);
            int count = Size * Size;
            for (int i = 0; i < count; i++)
            { cost[i]=float.MaxValue; estimate[i]=float.MaxValue; previous[i]=-1;
              closed[i]=false; open[i]=false; }
            int first = Key(start.x, start.y), last = Key(goal.x, goal.y);
            cost[first] = 0f;
            estimate[first] = Vector2Int.Distance(start, goal);
            open[first] = true;
            for (int iteration = 0; iteration < count; iteration++)
            {
                int selected = -1;
                float smallest = float.MaxValue;
                for (int i = 0; i < count; i++)
                    if (open[i] && !closed[i] && estimate[i] < smallest)
                    { selected = i; smallest = estimate[i]; }
                if (selected == -1 || selected == last) break;
                closed[selected] = true;
                Vector2Int pos = Cell(selected);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = pos.x + dx, nz = pos.y + dz;
                        if (nx < 0 || nx >= Size || nz < 0 || nz >= Size || !walkable[nx,nz])
                            continue;
                        if (dx != 0 && dz != 0 &&
                            (!walkable[pos.x + dx,pos.y] || !walkable[pos.x,pos.y + dz])) continue;
                        int neighbor = Key(nx,nz);
                        float proposal = cost[selected] + (dx != 0 && dz != 0 ? 1.4142f : 1f);
                        if (proposal >= cost[neighbor]) continue;
                        cost[neighbor] = proposal;
                        previous[neighbor] = selected;
                        estimate[neighbor] = proposal + Vector2Int.Distance(new Vector2Int(nx,nz),goal);
                        open[neighbor] = true;
                    }
            }
            if (first == last) { path.Add(destination); return; }
            if (previous[last] < 0) { path.Add(ToWorld(goal.x, goal.y)); return; }
            List<Vector3> reversed = new List<Vector3>();
            int node = last;
            while (node != first && node >= 0 && reversed.Count < count)
            {
                Vector2Int c = Cell(node);
                reversed.Add(ToWorld(c.x,c.y));
                node = previous[node];
            }
            reversed.Reverse();
            path.AddRange(reversed);
            path.Add(destination);
        }

        Vector3 ChooseEscapeGoal()
        {
            Vector3 origin = motor.Body.position;
            Vector3 pursuer = opponent.Body.position;
            float highest = float.MinValue;
            Vector3 best = origin;
            for (int i = 0; i < 16; i++)
            {
                float angle = Mathf.PI * 2f * i / 16f;
                Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3.8f;
                candidate.x = Mathf.Clamp(candidate.x, -5.05f, 5.05f);
                candidate.z = Mathf.Clamp(candidate.z, -5.05f, 5.05f);
                Vector2Int cell = ClosestOpen(candidate);
                candidate = ToWorld(cell.x, cell.y);
                float separation = Vector3.Distance(candidate, pursuer);
                float boundary = Mathf.Max(Mathf.Abs(candidate.x), Mathf.Abs(candidate.z));
                float score = separation - Mathf.Max(0f, boundary - 4.3f) * 2f
                    - Vector3.Distance(candidate, origin) * 0.07f;
                if (score > highest) { highest = score; best = candidate; }
            }
            return best;
        }

        public void ResetSource()
        {
            path.Clear();
            waypointIndex = 0;
            nextPlan = nextChoice = -1f;
            lastJumpTime = -10f;
            lastTrialSlideTime = -10f;
            trialSlideHeading = Vector3.zero;
        }
        public ChaseAction ReadAction(float clock) => Decide(clock);
        public ChaseAction Decide(float now)
        {
            if (!initialized) return new ChaseAction(Vector2.zero, false, false, false, false);
            if (now >= nextChoice)
            {
                if (role == ChaseRole.Evader) escapeGoal = ChooseEscapeGoal();
                nextChoice = now + DecisionPeriod;
            }
            Vector3 goal = role == ChaseRole.Chaser ?
                opponent.Body.position + opponent.HorizontalVelocity * Prediction : escapeGoal;
            goal.x = Mathf.Clamp(goal.x, -5.25f, 5.25f);
            goal.z = Mathf.Clamp(goal.z, -5.25f, 5.25f);
            if (now >= nextPlan || waypointIndex >= path.Count)
            {
                PlanPath(goal);
                nextPlan = now + ReplanPeriod;
            }
            while (waypointIndex < path.Count - 1 &&
                Vector3.Distance(new Vector3(motor.Body.position.x,0,motor.Body.position.z),
                path[waypointIndex]) < 0.42f) waypointIndex++;
            Vector3 target = waypointIndex < path.Count ? path[waypointIndex] : goal;
            Vector3 delta = target - motor.Body.position;
            delta.y = 0f;
            Vector3 direction = delta.sqrMagnitude > .001f ? delta.normalized : Vector3.zero;
            // During a solid low passage, maintain the committed heading
            // until the PhysX motor has actually finished crawling out.
            // Path replanning is not permitted to steer a half-emerged
            // athlete sideways, stop input, or request an unsafe get-up.
            if (skillAwareTrial && trialSlideHeading.sqrMagnitude > .5f &&
                (motor.State==ChaseMovementState.Sliding ||
                 motor.State==ChaseMovementState.SlideRecovery))
                direction=trialSlideHeading;
            bool crouch = !motor.CanStand;
            bool jump = false;
            bool slide = false;
            if (direction.sqrMagnitude > .5f)
            {
                // The bot only requests the same explicit, physical actions as the human.
                Vector3 low = motor.Body.position + Vector3.down * .55f;
                if (Physics.Raycast(low, direction, out RaycastHit hurdle, 1f,
                    ~0, QueryTriggerInteraction.Ignore) &&
                    hurdle.collider.GetComponent<ChaseHurdle>() != null &&
                    motor.CanJump && now - lastJumpTime > 1f)
                { jump = true; lastJumpTime = now; }
                // A roof at 1.64 m cannot be sensed by a waist-level horizontal ray.
                Vector3 high = motor.Body.position + Vector3.up * .80f;
                if (Physics.Raycast(high, direction, out RaycastHit overhead, .9f,
                    ~0, QueryTriggerInteraction.Ignore) &&
                    overhead.collider.GetComponent<ChaseOverheadPassage>() != null)
                    crouch = true;
                if (skillAwareTrial && motor.CanSlide &&
                    motor.HorizontalVelocity.magnitude > 2.0f &&
                    now-lastTrialSlideTime > 1.0f)
                {
                    // Both the rail and raised-platform underside are solid.
                    // The AI may enter the link ONLY by performing the same
                    // committed feet-first physical slide as the player.
                    // No teleport, collision phase, or bot-only impulse.
                    foreach(float eye in new[]{0f,.11f})
                    {
                        if (!Physics.Raycast(motor.Body.position+
                            Vector3.up*eye,direction,out RaycastHit passage,
                            1.12f,~0,QueryTriggerInteraction.Ignore) ||
                            passage.collider.GetComponent<ChaseOverheadPassage>()==null)
                            continue;
                        slide=true;
                        trialSlideHeading=direction;
                        lastTrialSlideTime=now;
                        break;
                    }
                }
            }
            return new ChaseAction(new Vector2(direction.x,direction.z)*MoveFactor,
                jump,false,slide,crouch);
        }
    }
}
