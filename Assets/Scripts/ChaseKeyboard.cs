using UnityEngine;
using UnityEngine.InputSystem;
namespace Chase
{
    // Poll every rendered frame and retain taps until a physics step.
    public sealed class ChaseKeyboard : MonoBehaviour, IChaseActionSource
    {
        Vector2 move;
        bool crouch, jumpHeld, leapHeld, slideHeld;
        bool jumpQueued, leapQueued, slideQueued;
        bool blockJump, blockLeap, blockSlide;
        public void Poll(Transform cameraTransform)
        {
            Keyboard k=Keyboard.current;
            if(k==null){move=Vector2.zero;crouch=jumpHeld=leapHeld=slideHeld=false;return;}
            float x=(k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0);
            float z=(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0);
            Vector3 forward=cameraTransform.forward;forward.y=0;forward.Normalize();
            Vector3 right=cameraTransform.right;right.y=0;right.Normalize();
            Vector3 world=Vector3.ClampMagnitude(right*x+forward*z,1f);
            move=new Vector2(world.x,world.z);
            crouch=k.leftShiftKey.isPressed;
            bool j=k.spaceKey.isPressed,l=k.upArrowKey.isPressed,s=k.downArrowKey.isPressed;
            if(!j)blockJump=false;
            if(!l)blockLeap=false;
            if(!s)blockSlide=false;
            jumpHeld=j&&!blockJump;leapHeld=l&&!blockLeap;slideHeld=s&&!blockSlide;
            if(!blockJump && k.spaceKey.wasPressedThisFrame)jumpQueued=true;
            if(!blockLeap && k.upArrowKey.wasPressedThisFrame)leapQueued=true;
            if(!blockSlide && k.downArrowKey.wasPressedThisFrame)slideQueued=true;
        }
        public ChaseAction ReadAction(float clock)
        {
            var result=new ChaseAction(move,jumpHeld||jumpQueued,leapHeld||leapQueued,
                slideHeld||slideQueued,crouch);
            jumpQueued=leapQueued=slideQueued=false;
            return result;
        }
        public ChaseAction Consume()=>ReadAction(0f);
        public void ResetSource()=>Clear();
        public void Clear()
        {
            Keyboard k=Keyboard.current;
            if(k!=null){
                if(k.spaceKey.isPressed)blockJump=true;
                if(k.upArrowKey.isPressed)blockLeap=true;
                if(k.downArrowKey.isPressed)blockSlide=true;
            }
            move=Vector2.zero;
            crouch=jumpHeld=leapHeld=slideHeld=false;
            jumpQueued=leapQueued=slideQueued=false;
        }
    }
}
