using UnityEngine;
using UnityEngine.InputSystem;

namespace Chase
{
    // The only keyboard adapter. Neither the motor nor the AI reads keyboard state.
    public sealed class ChaseKeyboard : MonoBehaviour
    {
        Vector2 move;
        bool duck;
        bool queuedJump;
        public void Poll(Transform cameraTransform)
        {
            Keyboard k = Keyboard.current;
            if (k == null) { move = Vector2.zero; duck = false; return; }
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
            if (k.upArrowKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
                queuedJump = true;
        }
        public ChaseAction Consume()
        {
            ChaseAction result = new ChaseAction(move, queuedJump, duck);
            queuedJump = false;
            return result;
        }
        public void Clear()
        {
            move = Vector2.zero;
            duck = queuedJump = false;
        }
    }
}
