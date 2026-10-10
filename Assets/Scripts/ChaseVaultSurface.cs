using UnityEngine;

namespace Chase
{
    // Only the thin top slab, never its supports, is designated vaultable.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ChaseVaultSurface : MonoBehaviour
    {
        public float TopHeight => GetComponent<BoxCollider>().bounds.max.y;
        public bool TryExitDistance(Vector3 worldEntry, Vector3 worldDirection, out float distance)
        {
            var box = GetComponent<BoxCollider>();
            Vector3 point = transform.InverseTransformPoint(worldEntry + worldDirection * .003f) - box.center;
            Vector3 direction = transform.InverseTransformVector(worldDirection);
            float result = float.PositiveInfinity;
            if (Mathf.Abs(direction.x) > .0001f)
            {
                float edge = Mathf.Sign(direction.x) * box.size.x * .5f;
                float t = (edge - point.x) / direction.x;
                if (t > 0f) result = Mathf.Min(result,t);
            }
            if (Mathf.Abs(direction.z) > .0001f)
            {
                float edge = Mathf.Sign(direction.z) * box.size.z * .5f;
                float t = (edge - point.z) / direction.z;
                if (t > 0f) result = Mathf.Min(result,t);
            }
            distance = result;
            return !float.IsNaN(result) && !float.IsInfinity(result) && result > .01f;
        }
        public float DepthAlong(Vector3 direction)
        {
            Bounds b = GetComponent<BoxCollider>().bounds;
            return Mathf.Abs(direction.x) > Mathf.Abs(direction.z) ? b.size.x : b.size.z;
        }
    }
}
