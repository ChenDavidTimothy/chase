using UnityEngine;
using UnityEngine.InputSystem;

namespace Chase
{
    // The only keyboard adapter. Neither the motor nor the AI reads keyboard state.
    public sealed class ChaseKeyboard : MonoBehaviour, IChaseActionSource
    {
        Vector2 move;
        bool duck;
        bool queuedJump;
        bool heldJump;
        bool requireJumpRelease;
        public void Poll(Transform cameraTransform)
        {
            Keyboard k = Keyboard.current;
            if (k == null) { move = Vector2.zero; duck = heldJump = false; return; }
            float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
            float z = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = cameraTransform.right;
            right.y = 0f;
            right.Normalize();
            Vector3 world = Vector3.ClampMagnitude(right * x + forward * z, 1f);
            move = new Vector2(world.x, world.z);
            duck = k.downArrowKey.isPressed || k.leftShiftKey.isPressed;
            bool physicalJump=k.upArrowKey.isPressed || k.spaceKey.isPressed;
            if (!physicalJump) requireJumpRelease=false;
            heldJump=physicalJump && !requireJumpRelease;
            if (!requireJumpRelease &&
                (k.upArrowKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                queuedJump=true;
        }
        public ChaseAction ReadAction(float clock)
        {
            // Queue short taps until a physics tick, then pass held state unchanged.
            ChaseAction result = new ChaseAction(move, heldJump || queuedJump, duck);
            queuedJump = false;
            return result;
        }
        public ChaseAction Consume() => ReadAction(0f);
        public void ResetSource() => Clear();
        public void Clear()
        {
            Keyboard k=Keyboard.current;
            if(k!=null && (k.upArrowKey.isPressed || k.spaceKey.isPressed))
                requireJumpRelease=true;
            move=Vector2.zero;
            duck=queuedJump=heldJump=false;
        }
    }
}
