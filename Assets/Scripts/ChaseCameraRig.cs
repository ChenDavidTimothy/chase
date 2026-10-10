using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chase
{
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCameraRig : MonoBehaviour
    {
        const float Smoothing = 0.2f;
        const float MaxOffset = 1.5f;
        ChaseMatch match;
        Camera attachedCamera;
        Vector3 basePosition;
        Vector3 followOffset;
        Vector3 smoothVelocity;
        float lastAspect;
        readonly Dictionary<Renderer,Color> faded = new Dictionary<Renderer,Color>();
        readonly HashSet<Renderer> currentOccluders = new HashSet<Renderer>();
        readonly List<Renderer> toRestore = new List<Renderer>();
        readonly RaycastHit[] occlusionHits = new RaycastHit[64];

        public void Initialize(ChaseMatch owner)
        {
            match = owner;
            attachedCamera = GetComponent<Camera>();
            attachedCamera.orthographic = false;
            attachedCamera.fieldOfView = 40f;
            transform.rotation = Quaternion.Euler(55f,45f,0f);
            lastAspect = -1f;
            RecalculateFraming();
        }

        void RecalculateFraming()
        {
            if (attachedCamera == null) return;
            float aspect = Mathf.Max(0.15f, attachedCamera.aspect);
            float tanY = Mathf.Tan(attachedCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            float tanX = tanY * aspect;
            Vector3 forward = transform.forward, right = transform.right, up = transform.up;
            float distance = 1f;
            // Calculate a distance from camera-space constraints for every arena
            // corner, both floor and wall-top; reserve room for follow translation.
            foreach (float x in new float[]{-6.35f,6.35f})
                foreach (float z in new float[]{-6.35f,6.35f})
                    foreach (float y in new float[]{0f,2.8f})
                    {
                        Vector3 v = new Vector3(x,y-1.15f,z);
                        float depth = Vector3.Dot(v,forward);
                        float lateral = Mathf.Abs(Vector3.Dot(v,right)) + MaxOffset;
                        float vertical = Mathf.Abs(Vector3.Dot(v,up)) + MaxOffset;
                        distance = Mathf.Max(distance,
                            lateral / tanX - depth + MaxOffset,
                            vertical / tanY - depth + MaxOffset);
                    }
            distance = distance * 1.08f + 0.5f;
            basePosition = new Vector3(0f,1.15f,0f) - forward * distance;
            lastAspect = aspect;
        }

        public void ResetView()
        {
            if (match == null) return;
            RecalculateFraming();
            followOffset = Vector3.zero;
            smoothVelocity = Vector3.zero;
            transform.position = basePosition;
            RestoreAll();
        }

        void LateUpdate()
        {
            if (match == null || match.Human == null) return;
            if (Mathf.Abs(attachedCamera.aspect-lastAspect)>.01f) RecalculateFraming();
            Vector3 pos = match.Human.Body.position;
            Vector3 desired = Vector3.ClampMagnitude(new Vector3(pos.x,0f,pos.z) * .25f,
                MaxOffset);
            followOffset = Vector3.SmoothDamp(followOffset,desired,ref smoothVelocity,Smoothing);
            followOffset.y = 0f;
            transform.position = basePosition + followOffset;
            RevealOccluders();
        }

        void RevealOccluders()
        {
            currentOccluders.Clear();
            FindOccluders(match.Human);
            FindOccluders(match.Opponent);
            foreach (Renderer r in currentOccluders)
            {
                if (faded.ContainsKey(r)) continue;
                Material material = r.material;
                faded.Add(r,material.color);
                if (material.HasProperty("_Mode"))
                {
                    material.SetFloat("_Mode",3f);
                    material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite",0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue=3000;
                }
                Color c=material.color; c.a=.25f; material.color=c;
            }
            toRestore.Clear();
            foreach (Renderer r in faded.Keys)
                if (!currentOccluders.Contains(r)) toRestore.Add(r);
            foreach (Renderer r in toRestore) Restore(r);
        }

        void FindOccluders(ChaseMotor actor)
        {
            if (actor == null) return;
            Vector3 target=actor.Body.position + Vector3.up*.20f;
            Vector3 delta=target-transform.position;
            int count=Physics.RaycastNonAlloc(transform.position,delta.normalized,
                occlusionHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                Collider collider=occlusionHits[i].collider;
                if(collider == null || collider.GetComponent<ChaseOccluder>()==null) continue;
                Renderer renderer=collider.GetComponent<Renderer>();
                if(renderer!=null) currentOccluders.Add(renderer);
            }
        }

        void Restore(Renderer r)
        {
            if (!faded.TryGetValue(r,out Color original)) return;
            if (r != null)
            {
                Material material=r.material;
                material.color=original;
                if (material.HasProperty("_Mode"))
                {
                    material.SetFloat("_Mode",0f);
                    material.SetInt("_SrcBlend",(int)BlendMode.One);
                    material.SetInt("_DstBlend",(int)BlendMode.Zero);
                    material.SetInt("_ZWrite",1);
                    material.DisableKeyword("_ALPHABLEND_ON");
                    material.renderQueue=-1;
                }
            }
            faded.Remove(r);
        }
        void RestoreAll()
        {
            toRestore.Clear();
            toRestore.AddRange(faded.Keys);
            foreach (Renderer r in toRestore) Restore(r);
        }
        void OnDisable() => RestoreAll();
    }
}
