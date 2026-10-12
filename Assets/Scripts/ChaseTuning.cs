using System;
using UnityEngine;
namespace Chase
{
    [Serializable] public sealed class ChaseTuning
    {
        [Header("Shared motor")]
        public float standingHeight = 1.8f, crouchingHeight = 1.55f, slideHeight = 1.55f;
        public float radius = .35f, runSpeed = 6f, crouchSpeed = 2f;
        public float groundAcceleration = 20f, groundBraking = 24f, airAcceleration = 4f;
        public float jumpSpeed = 6.3f, gravity = 22f, mass = 70f;
        public float jumpBuffer = .08f, coyoteTime = .08f;
        [Header("Explicit committed moves")]
        public float leapHorizontalSpeed = 9f, leapVerticalSpeed = 5.2f;
        public float leapRecovery = .35f, diveImpactRecovery = .40f, recoverySpeed = 2f;
        [Header("One rounded solid dive collider (same standing capsule)")]
        public float diveCapsuleLength = 1.95f;
        public float diveCapsuleRadius = .43f;
        public float diveCapsuleCenterY = -.29f;
        public float diveCapsuleCenterForward = .16f;
        public float proneCrawlSpeed = 1.5f;
        [Header("Feet-first grounded sliding tackle")]
        public float slideBodyLength = 1.98f, slideBodyWidth = .84f;
        public float slideBodyHeight = .82f, slideBodyCenterY = -.48f;
        public float slideBodyCenterForward = -.24f;
        public float slideCrawlSpeed = 1.10f;
        public float slideDuration = .45f, slideDeceleration = 8f, slideRecovery = .20f;
        [Header("Match")]
        public float countdown = 3f, roundDuration = 20f;
        public int spawnSeed = 2026;
    }
}
