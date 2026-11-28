using UnityEngine;

namespace CatchAR
{
    public sealed class CatchTarget : MonoBehaviour
    {
        public Transform core;
        public Transform ring;
        public Renderer coreRenderer;
        public Renderer ringRenderer;
        public Material mintMaterial;
        public Material goldMaterial;
        public Material mintCoreMaterial;
        public Material goldCoreMaterial;
        public bool IsGold { get; private set; }
        public float GroundHeight { get; private set; }
        public bool Resolved => resolved;
        public bool IsSurfaceTarget { get; private set; }
        public Vector3 SurfacePoint { get; private set; }
        Vector3 surfaceNormal;
        CatchGame game;
        float fallSpeed, acceleration, age, landedAge, landingClearance;
        const float LandingDuration = .25f;
        bool resolved, landed;

        public void Initialize(CatchGame owner, bool gold, float groundHeight, float speed, float gravity)
        {
            game = owner;
            IsGold = gold;
            GroundHeight = groundHeight;
            fallSpeed = speed;
            acceleration = gravity;
            coreRenderer.sharedMaterial = gold ? goldCoreMaterial : mintCoreMaterial;
            ringRenderer.sharedMaterial = gold ? goldMaterial : mintMaterial;
        }

        void Update()
        {
            if (!game.SimulationActive) return;
            float dt = Time.deltaTime;
            if (resolved)
            {
                if (!landed) return;
                landedAge += dt;
                float remainingScale = 1 - Mathf.SmoothStep(0, 1, landedAge / LandingDuration);
                transform.localScale = Vector3.one * remainingScale;
                Vector3 grounded = transform.position;
                grounded.y = GroundHeight + landingClearance * remainingScale;
                transform.position = grounded;
                return;
            }
            age += dt;
            core.Rotate(24 * dt, 50 * dt, 16 * dt);
            ring.Rotate(0, 0, -70 * dt, Space.Self);
            float scale = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .22f));
            transform.localScale = Vector3.one * scale * (IsSurfaceTarget ? .65f : 1);
            if (IsSurfaceTarget)
            {
                float Offset(Renderer r) => Vector3.Dot(new Vector3(Mathf.Abs(surfaceNormal.x), Mathf.Abs(surfaceNormal.y), Mathf.Abs(surfaceNormal.z)), r.bounds.extents)
                    - Vector3.Dot(surfaceNormal, r.bounds.center - transform.position);
                float offset = Mathf.Max(Offset(coreRenderer), Offset(ringRenderer));
                transform.position = SurfacePoint + surfaceNormal * (offset + .005f);
                return;
            }
            // Contact follows the visible mesh, including its current rotation.
            float clearance = transform.position.y - Mathf.Min(coreRenderer.bounds.min.y, ringRenderer.bounds.min.y);
            Vector3 position = transform.position;
            position.y -= fallSpeed * dt + .5f * acceleration * dt * dt;
            fallSpeed += acceleration * dt;
            bool touchesGround = position.y <= GroundHeight + clearance;
            position.y = Mathf.Max(position.y, GroundHeight + clearance);
            transform.position = position;
            if (touchesGround)
            {
                landed = resolved = true;
                landingClearance = clearance;
                GetComponent<Collider>().enabled = false;
                game.RegisterEscape(this);
                // Finish cleanup even if the round ends during the landing animation.
                Destroy(gameObject, LandingDuration);
            }
        }

        public void Catch()
        {
            if (resolved) return;
            resolved = true;
            game.RegisterCatch(this);
            Destroy(gameObject);
        }

        public void InitializeSurface(CatchGame owner, Vector3 point, Vector3 normal)
        {
            Initialize(owner, false, point.y, 0, 0);
            IsSurfaceTarget = true; SurfacePoint = point; surfaceNormal = normal.normalized;
            transform.position = point + surfaceNormal * .15f;
            transform.localScale = Vector3.one * .65f;
        }

        public void KeepClearOfPlayer(Transform player, float spawnDistance)
        {
            if (IsSurfaceTarget) return;
            Vector3 delta = Vector3.ProjectOnPlane(transform.position - player.position, Vector3.up);
            if (delta.sqrMagnitude >= 1) return;
            if (delta.sqrMagnitude < .0001f) delta = Vector3.ProjectOnPlane(player.forward, Vector3.up);
            if (delta.sqrMagnitude < .0001f) delta = Vector3.forward;
            Vector3 position = transform.position;
            Vector3 safe = player.position + delta.normalized * Mathf.Max(1.2f, spawnDistance);
            position.x = safe.x; position.z = safe.z;
            transform.position = position;
        }
    }
}

