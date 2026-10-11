using UnityEngine;
namespace Chase
{
    public readonly struct ChaseObservation
    {
        public readonly Vector3 Position, HorizontalVelocity, RelativeOpponentPosition, RelativeOpponentVelocity;
        public readonly float VerticalVelocity, RemainingTime, RecoveryRemaining;
        public readonly bool Grounded, CanJump, CanLeap, CanSlide, CanStand, IsCommitted;
        public readonly ChaseMovementState MovementState;
        public readonly ChaseRole Role;
        public readonly Vector3 FacingDirection;
        public readonly float[] LocalObstacles;
        public ChaseObservation(ChaseMotor self,ChaseMotor opponent,ChaseRole role,float remaining,float[] local)
        {
            Position=self.Body.position; HorizontalVelocity=self.HorizontalVelocity;
            VerticalVelocity=self.Velocity.y; RelativeOpponentPosition=opponent.Body.position-Position;
            RelativeOpponentVelocity=opponent.Velocity-self.Velocity; Grounded=self.Grounded;
            CanJump=self.CanJump; CanLeap=self.CanLeap; CanSlide=self.CanSlide; CanStand=self.CanStand;
            MovementState=self.State; Role=role; RemainingTime=remaining;
            RecoveryRemaining=self.RecoveryRemaining; IsCommitted=self.IsCommitted;
            FacingDirection=self.FacingDirection; LocalObstacles=local;
        }
    }
    public static class ChaseObservationSensor
    {
        public const int Directions=8, Channels=5, LocalSize=Directions*Channels, PolicySize=26+LocalSize;
        public const float Range=4f;
        static readonly RaycastHit[] hits=new RaycastHit[48];
        static readonly float[] scratch=new float[LocalSize];
        // 0..11: own xyz, velocity xyz, opponent relative xyz, relative velocity xyz.
        // 12 grounded, 13 jump, 14 leap, 15 slide, 16 stand, 17 role, 18 time,
        // 19 state/8, 20/21 facing XZ, 22 recovery/.75, 23 committed,
        // 24 collision height/1.8, 25 planar speed/9.
        // 26..65: five 8-ray channels (waist, low, overhead, hurdle, solid wall).
        public static void FillPolicyVector(ChaseMotor self,ChaseMotor opponent,
            ChaseRole role,float remaining,float[] output)
        {
            if(output==null||output.Length<PolicySize)throw new System.ArgumentException(
                "At least 66 policy floats required");
            Vector3 own=self.Body.position,relative=opponent.Body.position-own;
            Vector3 v=self.Velocity,rv=opponent.Velocity-v;
            output[0]=Mathf.Clamp(own.x/6f,-1,1);
            output[1]=Mathf.Clamp(own.y/4f,-1,1);
            output[2]=Mathf.Clamp(own.z/6f,-1,1);
            output[3]=Mathf.Clamp(v.x/9f,-1,1);
            output[4]=Mathf.Clamp(v.y/10f,-1,1);
            output[5]=Mathf.Clamp(v.z/9f,-1,1);
            output[6]=Mathf.Clamp(relative.x/12f,-1,1);
            output[7]=Mathf.Clamp(relative.y/4f,-1,1);
            output[8]=Mathf.Clamp(relative.z/12f,-1,1);
            output[9]=Mathf.Clamp(rv.x/18f,-1,1);
            output[10]=Mathf.Clamp(rv.y/20f,-1,1);
            output[11]=Mathf.Clamp(rv.z/18f,-1,1);
            output[12]=self.Grounded?1:0;
            output[13]=self.CanJump?1:0;
            output[14]=self.CanLeap?1:0;
            output[15]=self.CanSlide?1:0;
            output[16]=self.CanStand?1:0;
            output[17]=role==ChaseRole.Chaser?1:-1;
            output[18]=Mathf.Clamp01(remaining/20f);
            output[19]=(float)self.State/8f;
            output[20]=self.FacingDirection.x;
            output[21]=self.FacingDirection.z;
            output[22]=Mathf.Clamp01(self.RecoveryRemaining/.75f);
            output[23]=self.IsCommitted?1:0;
            output[24]=Mathf.Clamp01((self.SlideColliderActive ?
                self.SlideBox.size.y : self.DiveColliderActive ?
                self.DiveBox.size.y : self.Capsule.height)/
                self.Settings.standingHeight);
            output[25]=Mathf.Clamp01(self.HorizontalVelocity.magnitude/9f);
            FillLocal(self,scratch);
            System.Array.Copy(scratch,0,output,26,LocalSize);
        }
        public static void FillLocal(ChaseMotor actor,float[] output)
        {
            if(output==null||output.Length<LocalSize)throw new System.ArgumentException(
                "At least 40 local samples required");
            for(int i=0;i<LocalSize;i++)output[i]=1f;
            Vector3 pos=actor.Body.position;
            float bottom=pos.y-actor.Settings.standingHeight*.5f;
            for(int i=0;i<Directions;i++)
            {
                float a=i*Mathf.PI*2f/Directions;
                Vector3 dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                for(int layer=0;layer<3;layer++)
                {
                    float height=layer==0?.22f:layer==1?.92f:1.7f;
                    Vector3 origin=new Vector3(pos.x,bottom+height,pos.z);
                    int count=Physics.RaycastNonAlloc(origin,dir,hits,Range,
                        ~0,QueryTriggerInteraction.Ignore);
                    if(count==hits.Length)
                    {for(int j=0;j<Channels;j++)output[j*Directions+i]=0f;continue;}
                    for(int j=0;j<count;j++)
                    {
                        Collider c=hits[j].collider;
                        if(c==null||c.transform.IsChildOf(actor.transform)||
                            c.GetComponentInParent<ChaseMotor>()!=null)continue;
                        float d=Mathf.Clamp01(hits[j].distance/Range);
                        output[(layer==0?1:layer==1?0:2)*Directions+i]=
                            Mathf.Min(output[(layer==0?1:layer==1?0:2)*Directions+i],d);
                        if(c.GetComponent<ChaseHurdle>()!=null)
                            output[3*Directions+i]=Mathf.Min(output[3*Directions+i],d);
                        if(c.GetComponent<ChaseBlocker>()!=null)
                            output[4*Directions+i]=Mathf.Min(output[4*Directions+i],d);
                    }
                }
            }
        }
        public static ChaseObservation Read(ChaseMotor self,ChaseMotor opponent,
            ChaseRole role,float remaining)
        {
            float[] local=new float[LocalSize];FillLocal(self,local);
            return new ChaseObservation(self,opponent,role,remaining,local);
        }
    }
}
