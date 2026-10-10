using System;
using UnityEngine;

namespace Chase
{
    [Serializable]
    public sealed class ChaseTuning
    {
        [Header("Shared motor")]
        public float standingHeight = 1.8f;
        public float crouchingHeight = 0.85f;
        public float radius = 0.35f;
        public float runSpeed = 6f;
        public float crouchSpeed = 2f;
        public float groundAcceleration = 20f;
        public float groundBraking = 24f;
        public float airAcceleration = 4f;
        public float jumpSpeed = 5.5f;
        public float gravity = 22f;
        public float mass = 70f;
        public float jumpBuffer = 0.08f;
        public float coyoteTime = 0.08f;
        [Header("Slide and vault")]
        public float slideThreshold = 3.5f;
        public float slideDuration = 0.55f;
        public float slideDeceleration = 4f;
        public float vaultApproachSpeed = 2.5f;
        public float vaultReach = 0.65f;
        public float vaultDuration = 0.6f;
        public float vaultVerticalSpeed = 8.4f;
        [Header("Match")]
        public float countdown = 3f;
        public float roundDuration = 20f;
        public int spawnSeed = 2026;
    }
}
