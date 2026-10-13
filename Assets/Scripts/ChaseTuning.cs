using System;
using UnityEngine;
namespace Chase
{
    [Serializable] public sealed class ChaseTuning
    {
        [Header("Shared motor")]
        public float standingHeight = 1.8f, crouchingHeight = 1.55f, slideHeight = 1.55f;
        public float radius = .35f, runSpeed = 6f, crouchSpeed = 2f;
        public float groundAcceleration = 36f, groundBraking = 44f, airAcceleration = 5f;
        public float jumpSpeed = 7.0f, gravity = 22f, mass = 70f;
        public float jumpBuffer = .13f, coyoteTime = .12f, actionBuffer = .11f;
        [Header("Explicit committed moves")]
        public float leapHorizontalSpeed = 8.2f, leapVerticalSpeed = 7.25f;
        public float leapRecovery = .27f, diveImpactRecovery = .20f, recoverySpeed = 3.4f;
        [Header("One rounded solid dive collider (same standing capsule)")]
        public float diveCapsuleLength = 1.95f;
        public float diveCapsuleRadius = .43f;
        public float diveCapsuleCenterY = -.29f;
        public float diveCapsuleCenterForward = .16f;
        public float proneCrawlSpeed = 2.6f;
        [Header("Feet-first grounded sliding tackle")]
        public float slideBodyLength = 1.98f, slideBodyWidth = .84f;
        public float slideBodyHeight = .82f, slideBodyCenterY = -.48f;
        public float slideBodyCenterForward = -.24f;
        public float slideCrawlSpeed = 2.7f;
        public float slideDuration = .42f, slideDeceleration = 6f, slideRecovery = .14f;
        [Header("Match")]
        public float countdown = 3f, roundDuration = 20f;
    }
}
