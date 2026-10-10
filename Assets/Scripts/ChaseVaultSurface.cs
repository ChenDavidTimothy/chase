using UnityEngine;

namespace Chase
{
    // Only the thin top slab, never its supports, is designated vaultable.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ChaseVaultSurface : MonoBehaviour
    {
        public float TopHeight => GetComponent<BoxCollider>().bounds.max.y;
        public float DepthAlong(Vector3 direction)
        {
            Bounds b = GetComponent<BoxCollider>().bounds;
            return Mathf.Abs(direction.x) > Mathf.Abs(direction.z) ? b.size.x : b.size.z;
        }
    }
}
