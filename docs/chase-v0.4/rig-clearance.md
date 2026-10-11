# CHASE v0.4 character collision and animation clearance

The exact unmodified raw-mesh geometry digest, computed from all vertex positions and polygon topology of the 17 original parts, is `447ea6c601e05f189f4091111121a7dc207964f5c27257bc2fcf48809880aab4` and **matches the pre-v0.4 backup**. The only editable production source is `Chibi/Chibi_Rigged.blend`; Blender -Y forward/+Z up; Unity +Z/+Y with identity imported transform, Generic rig and no root motion.

Method: Blender 5.2.2 LTS in a fresh background process, evaluate the Armature deformations at five representative frames per saved action using `depsgraph`, transform every vertex of each of 17 `*_vis` meshes to world space, and record global minimum Z, maximum Z, head/face maximum Z, and maximum planar radius. Full samples: [`mesh-pose-audit.tsv`](mesh-pose-audit.tsv). At rest, physical feet are Y=0, actor physics origin Y=0.9, and visual child local Y=-0.9. Both player colliders are dynamic CapsuleColliders, **not** animated mesh colliders.

| Visual pose | Evaluated shell elevation bounds, meters | Max planar reach, meters | Collider stance / issue |
|---|---|---:|---|
| Idle standing | 0.000 to 1.800 | 0.595 | Upright capsule 1.80 high / 0.35 radius |
| Running samples | -0.087 to 1.802 | 0.594 | Upright; up to 8.7 cm visual foot penetration remains |
| Settled crouch-walk | +0.018 to 1.531 | 0.599 | Low 1.55 m capsule covers head height; horizontal shell remains wider |
| Crouch transition | -0.204 to 1.800 | 0.597 | At first frame, shell remains upright, and feet intersect floor during bend |
| Committed slide | -0.143 to 1.628 | 0.655 | Low capsule 1.55 m, visual top can exceed capsule by 7.8 cm during phases |
| Slide recovery | -0.096 to 1.628 | 0.659 | Get-up remains physically collidable; transitional vertical mismatch |
| Jump | -0.140 to 1.898 | 0.694 | 1.80 m standing capsule remains; animated limbs may exceed footprint |
| Leap | -0.125 to 1.814 | 0.705 | 1.80 m capsule; strong forward limb silhouette increases visible reach |
| Leap recovery | -0.278 to 1.800 | 0.600 | Low transient bone pose may penetrate floor while collision is upright |

v0.4 adjustments: the low crouch walk now places evaluated mesh feet above the ground and the head no higher than 1.531 m. The core slide clears the ground around mid-action; all five checked slide frames remain below 1.64 m canopy underside. The physics capsule height was **raised to 1.55 m** for crouch and slide to avoid treating a visibly 1.5 m-tall figure as 0.85 m. Both solid-roof passages were raised accordingly. No mesh geometry, model scaling, or axis conventions were changed. The active Blender validator now checks low-pose shell bounds from evaluated geometry and rejects missing required actions or reintroduced Vault actions.

**Remaining limitations, not claimed solved:** the instantaneous collider height change precedes visible rig interpolation; during the first ~0.1 seconds of a crouch the head is still close to standing height. Some articulated shoes/shins intersect the floor, particularly in transition, and limbs extend beyond the 0.35 m capsule radius. A single vertical capsule cannot perfectly model these large rigid shell pieces. Movement and tag collisions are conservative/simple physics, not pixel-exact mesh collision. The two underpasses have 1.64 m underside clearance, not the original 1.125 m shelf. Continue real-player visual/collision inspection before approving thinner passages or declaring animation/collision polish finished. A full compound-collider solution would change the collision footprint and requires explicit design review; we did not secretly introduce it.
