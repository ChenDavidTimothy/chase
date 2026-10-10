using UnityEngine;

namespace Chase
{
    // Source-agnostic action representation. Move is in world XZ coordinates.
    public struct ChaseAction
    {
        public Vector2 Move;
        public bool JumpOrVault;
        public bool DuckOrSlide;
        public ChaseAction(Vector2 move, bool jump, bool duck)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            JumpOrVault = jump;
            DuckOrSlide = duck;
        }
    }

    public enum ChaseMovementState { Standing, Crouching, Sliding, Airborne, Vaulting }
    public enum ChaseRole { Chaser, Evader }
    public enum ChasePhase { Countdown, Active, Ended, Paused }
}
