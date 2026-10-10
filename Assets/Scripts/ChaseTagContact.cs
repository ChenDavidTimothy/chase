using UnityEngine;

namespace Chase
{
    public sealed class ChaseTagContact : MonoBehaviour
    {
        public ChaseRole Role { get; private set; }
        ChaseMatch match;
        public void Initialize(ChaseMatch owner, ChaseRole role)
        {
            match = owner;
            Role = role;
        }
        void OnCollisionEnter(Collision collision) => Check(collision);
        void OnCollisionStay(Collision collision) => Check(collision);
        void Check(Collision collision)
        {
            if (match == null || Role != ChaseRole.Chaser || collision.contactCount == 0) return;
            ChaseTagContact other = collision.collider.GetComponentInParent<ChaseTagContact>();
            if (other != null && other != this && other.Role == ChaseRole.Evader)
                match.TryTag(this, other);
        }
    }
}
