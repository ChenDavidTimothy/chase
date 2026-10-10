using UnityEngine;

namespace Chase
{
    // Immutable per-call snapshot. Low-level policies may reuse the caller-owned
    // LocalObstacleBuffer via FillLocal rather than allocate per decision step.
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
        // 5 channels x 8 rays: waist blockers, low objects, overhead,
        // vaultable slabs, hurdles. All distances normalized [0,1].
        public readonly float[] LocalObstacles;
        public readonly bool HasStandingClearance;
        public ChaseObservation(Vector3 position, Vector3 horizontal, float vertical,
            Vector3 relativePosition, Vector3 relativeVelocity, bool grounded,
            ChaseMovementState state, bool canJump, bool canVault, ChaseRole role,
            float remaining, float[] obstacles, float[] local, bool clearance)
        {
            Position=position; HorizontalVelocity=horizontal; VerticalVelocity=vertical;
            RelativeOpponentPosition=relativePosition; RelativeOpponentVelocity=relativeVelocity;
            Grounded=grounded; MovementState=state; CanJump=canJump; CanVault=canVault;
            Role=role; RemainingTime=remaining; ObstacleDistances=obstacles;
            LocalObstacles=local; HasStandingClearance=clearance;
        }
    }

    public static class ChaseObservationSensor
    {
        public const int Directions=8;
        public const int Channels=5;
        public const int LocalSize=Directions * Channels;
        public const float Range=4f;
        static readonly RaycastHit[] hits = new RaycastHit[48];
        public const int PolicySize=19+LocalSize;
        static readonly float[] policyLocalScratch=new float[LocalSize];

        // Fixed-size, symmetric, normalized flat observation for a future policy.
        // Values 0..18 describe physical state; 19..58 are local geometry.
        // All slots are bounded to [-1,1], and no per-call objects are created.
        public static void FillPolicyVector(ChaseMotor self,ChaseMotor opponent,
            ChaseRole role,float remaining,float[] output)
        {
            if(output==null || output.Length<PolicySize)
                throw new System.ArgumentException("At least 59 policy floats required");
            Vector3 own=self.Body.position,relative=opponent.Body.position-own;
            Vector3 velocity=self.Velocity,relVelocity=opponent.Velocity-velocity;
            output[0]=Mathf.Clamp(own.x/6f,-1f,1f);
            output[1]=Mathf.Clamp(own.y/4f,-1f,1f);
            output[2]=Mathf.Clamp(own.z/6f,-1f,1f);
            output[3]=Mathf.Clamp(velocity.x/6f,-1f,1f);
            output[4]=Mathf.Clamp(velocity.y/10f,-1f,1f);
            output[5]=Mathf.Clamp(velocity.z/6f,-1f,1f);
            output[6]=Mathf.Clamp(relative.x/12f,-1f,1f);
            output[7]=Mathf.Clamp(relative.y/4f,-1f,1f);
            output[8]=Mathf.Clamp(relative.z/12f,-1f,1f);
            output[9]=Mathf.Clamp(relVelocity.x/12f,-1f,1f);
            output[10]=Mathf.Clamp(relVelocity.y/20f,-1f,1f);
            output[11]=Mathf.Clamp(relVelocity.z/12f,-1f,1f);
            output[12]=self.Grounded?1f:0f;
            output[13]=self.CanJump?1f:0f;
            output[14]=self.CanVault?1f:0f;
            output[15]=self.CanStand?1f:0f;
            output[16]=role==ChaseRole.Chaser?1f:-1f;
            output[17]=Mathf.Clamp01(remaining/20f);
            output[18]=(float)self.State/4f;
            FillLocal(self,policyLocalScratch);
            System.Array.Copy(policyLocalScratch,0,output,19,LocalSize);
        }


        public static void FillLocal(ChaseMotor actor, float[] output)
        {
            if (output==null || output.Length < LocalSize)
                throw new System.ArgumentException("At least 40 sensor samples required");
            for(int i=0;i<LocalSize;i++) output[i]=1f;
            Vector3 pos=actor.Body.position;
            float bottom=pos.y - actor.Settings.standingHeight * .5f;
            for(int i=0;i<Directions;i++)
            {
                float radians=i*Mathf.PI*2f/Directions;
                Vector3 direction=new Vector3(Mathf.Cos(radians),0f,Mathf.Sin(radians));
                // Three samples distinguish an overhead shelf and the open
                // space underneath it from an ordinary opaque wall.
                for(int layer=0;layer<3;layer++)
                {
                    float height=layer==0 ? .22f : layer==1 ? .92f : 1.14f;
                    Vector3 origin=new Vector3(pos.x,bottom+height,pos.z);
                    int count=Physics.RaycastNonAlloc(origin,direction,hits,Range,
                        ~0,QueryTriggerInteraction.Ignore);
                    if(count==hits.Length) { for(int j=0;j<Channels;j++)output[j*Directions+i]=0f;continue; }
                    for(int j=0;j<count;j++)
                    {
                        Collider collider=hits[j].collider;
                        if(collider==null || collider.transform.IsChildOf(actor.transform) ||
                            collider.GetComponentInParent<ChaseMotor>()!=null) continue;
                        float value=Mathf.Clamp01(hits[j].distance/Range);
                        if(layer==0) output[Directions+i]=Mathf.Min(output[Directions+i],value);
                        if(layer==1) output[i]=Mathf.Min(output[i],value);
                        if(layer==2) output[Directions*2+i]=Mathf.Min(output[Directions*2+i],value);
                        if(collider.GetComponent<ChaseVaultSurface>()!=null)
                            output[Directions*3+i]=Mathf.Min(output[Directions*3+i],value);
                        if(collider.GetComponent<ChaseHurdle>()!=null)
                            output[Directions*4+i]=Mathf.Min(output[Directions*4+i],value);
                    }
                }
            }
        }
        public static ChaseObservation Read(ChaseMotor self,ChaseMotor opponent,
            ChaseRole role,float remaining)
        {
            float[] local=new float[LocalSize];
            float[] waist=new float[Directions];
            FillLocal(self,local);
            System.Array.Copy(local,waist,Directions);
            return new ChaseObservation(self.Body.position,self.HorizontalVelocity,
                self.Velocity.y,opponent.Body.position-self.Body.position,
                opponent.Velocity-self.Velocity,self.Grounded,self.State,self.CanJump,
                self.CanVault,role,remaining,waist,local,self.CanStand);
        }
    }
}
