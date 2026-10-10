using System.Collections.Generic;
using UnityEngine;

namespace Chase
{
    // Simple grid navigator: plans around solid tall obstacles and support posts.
    // Hurdles and shelf roofs remain traversable by the shared motor.
    public sealed class ChaseBot : MonoBehaviour
    {
        const int Size = 21;
        const float Spacing = 0.53f;
        readonly bool[,] walkable = new bool[Size, Size];
        readonly List<Vector3> path = new List<Vector3>();
        ChaseMotor motor;
        ChaseMotor opponent;
        ChaseRole role;
        float nextPlan;
        float nextChoice;
        float lastJumpTime = -10f;
        Vector3 escapeGoal;
        int waypointIndex;
        bool initialized;

        public void Initialize(ChaseMotor self, ChaseMotor other, ChaseRole botRole)
        {
            motor = self;
            opponent = other;
            role = botRole;
            ScanGrid();
            path.Clear();
            nextPlan = nextChoice = -1f;
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
                        Collider[] hits = Physics.OverlapSphere(p + Vector3.up * 0.95f,
                            0.47f, ~0, QueryTriggerInteraction.Ignore);
                        foreach (Collider c in hits)
                            if (c.GetComponent<ChaseBlocker>() != null) { good = false; break; }
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
            float[] cost = new float[count];
            float[] estimate = new float[count];
            int[] previous = new int[count];
            bool[] closed = new bool[count];
            bool[] open = new bool[count];
            for (int i = 0; i < count; i++) { cost[i] = float.MaxValue; previous[i] = -1; }
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

        public ChaseAction Decide(float now)
        {
            if (!initialized) return new ChaseAction(Vector2.zero, false, false);
            if (now >= nextChoice)
            {
                if (role == ChaseRole.Evader) escapeGoal = ChooseEscapeGoal();
                nextChoice = now + 0.7f;
            }
            Vector3 goal = role == ChaseRole.Chaser ?
                opponent.Body.position + opponent.HorizontalVelocity * 0.12f : escapeGoal;
            goal.x = Mathf.Clamp(goal.x, -5.25f, 5.25f);
            goal.z = Mathf.Clamp(goal.z, -5.25f, 5.25f);
            if (now >= nextPlan || waypointIndex >= path.Count)
            {
                PlanPath(goal);
                nextPlan = now + 0.35f;
            }
            while (waypointIndex < path.Count - 1 &&
                Vector3.Distance(new Vector3(motor.Body.position.x,0,motor.Body.position.z),
                path[waypointIndex]) < 0.42f) waypointIndex++;
            Vector3 target = waypointIndex < path.Count ? path[waypointIndex] : goal;
            Vector3 delta = target - motor.Body.position;
            delta.y = 0f;
            Vector3 direction = delta.sqrMagnitude > .001f ? delta.normalized : Vector3.zero;
            bool duck = !motor.CanStand;
            bool jump = false;
            if (direction.sqrMagnitude > .5f)
            {
                Vector3 origin = motor.Body.position + Vector3.up * 0.21f;
                if (Physics.Raycast(origin, direction, out RaycastHit shelfHit, 1.12f, ~0, QueryTriggerInteraction.Ignore)
                    && shelfHit.collider.GetComponent<ChaseVaultSurface>() != null)
                {
                    if (motor.HorizontalVelocity.magnitude >= 2.5f &&
                        shelfHit.distance >= 0.39f && shelfHit.distance <= 1f &&
                        now - lastJumpTime > 0.9f)
                    { jump = true; lastJumpTime = now; }
                    else duck = true;
                }
                origin = motor.Body.position + Vector3.down * 0.55f;
                if (Physics.Raycast(origin, direction, out RaycastHit hurdleHit, 0.85f, ~0, QueryTriggerInteraction.Ignore)
                    && hurdleHit.collider.GetComponent<ChaseHurdle>() != null &&
                    now - lastJumpTime > 0.9f)
                { jump = true; lastJumpTime = now; }
            }
            return new ChaseAction(new Vector2(direction.x, direction.z), jump, duck);
        }
    }
}
