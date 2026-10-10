using UnityEngine;

namespace Chase
{
    // Immutable snapshot. RL adapters can use this without accessing match internals.
    public readonly struct ChaseObservation
    {
        public readonly Vector3 Position;
        public readonly Vector3 HorizontalVelocity;
        public readonly float VerticalVelocity;
        public readonly Vector3 RelativeOpponentPosition;
        public readonly Vector3 RelativeOpponentVelocity;
        public readonly bool Grounded;
        public readonly ChaseMovementState MovementState;
        public readonly bool CanJump;
        public readonly bool CanVault;
        public readonly ChaseRole Role;
        public readonly float RemainingTime;
        public readonly float[] ObstacleDistances;
        public ChaseObservation(Vector3 position, Vector3 horizontal, float vertical,
            Vector3 relativePosition, Vector3 relativeVelocity, bool grounded,
            ChaseMovementState state, bool canJump, bool canVault, ChaseRole role,
            float remaining, float[] obstacles)
        {
            Position=position; HorizontalVelocity=horizontal; VerticalVelocity=vertical;
            RelativeOpponentPosition=relativePosition; RelativeOpponentVelocity=relativeVelocity;
            Grounded=grounded; MovementState=state; CanJump=canJump; CanVault=canVault;
            Role=role; RemainingTime=remaining; ObstacleDistances=obstacles;
        }
    }

    public static class ChaseObservationSensor
    {
        public static ChaseObservation Read(ChaseMotor self, ChaseMotor opponent,
            ChaseRole role, float remaining)
        {
            float[] distances = new float[8];
            for (int i=0;i<8;i++)
            {
                float angle = i * Mathf.PI / 4f;
                Vector3 dir = new Vector3(Mathf.Cos(angle),0f,Mathf.Sin(angle));
                distances[i] = 4f;
                RaycastHit[] hits = Physics.RaycastAll(self.Body.position,dir,4f,
                    ~0,QueryTriggerInteraction.Ignore);
                foreach (RaycastHit hit in hits)
                    if (hit.collider.GetComponentInParent<ChaseMotor>() == null)
                        distances[i] = Mathf.Min(distances[i],hit.distance);
            }
            return new ChaseObservation(self.Body.position,self.HorizontalVelocity,
                self.Velocity.y,opponent.Body.position-self.Body.position,
                opponent.Velocity-self.Velocity,self.Grounded,self.State,self.CanJump,
                self.CanVault,role,remaining,distances);
        }
    }
}
