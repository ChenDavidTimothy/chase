using UnityEngine;
namespace Chase
{
    // Explicit, controller-independent, physics-only commands.
    public struct ChaseAction
    {
        public Vector2 Move;
        public bool Jump, Leap, Slide, Crouch;
        public ChaseAction(Vector2 move, bool jump, bool leap, bool slide, bool crouch)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            Jump = jump; Leap = leap; Slide = slide; Crouch = crouch;
        }
    }
    public enum ChaseMovementState
    {
        Standing, Crouching, Airborne, Jumping, Leaping, Sliding, LeapRecovery, SlideRecovery
    }
    public enum ChaseRole { Chaser, Evader }
    public enum ChasePhase { Countdown, Active, Ended, Paused }
}
