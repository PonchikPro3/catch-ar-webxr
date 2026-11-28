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
        public Material[] bodyMaterials;
        public Renderer[] visualRenderers;
        public Vector3 AngularVelocity { get; private set; }
        static int lastColor = -1;
        public Bounds VisualBounds
        {
            get
            {
                Bounds bounds = coreRenderer.bounds;
                if (visualRenderers != null) foreach (var r in visualRenderers) if (r != null) bounds.Encapsulate(r.bounds);
                return bounds;
            }
        }
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
            if (bodyMaterials != null && bodyMaterials.Length > 0)
            {
                int index = lastColor < 0 || bodyMaterials.Length == 1 ? Random.Range(0, bodyMaterials.Length)
                    : (lastColor + Random.Range(1, bodyMaterials.Length)) % bodyMaterials.Length;
                coreRenderer.sharedMaterial = bodyMaterials[index]; lastColor = index;
            }
            AngularVelocity = Random.onUnitSphere * Random.Range(35f, 160f);
            core.localRotation = Random.rotationUniform;
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
            if (!IsSurfaceTarget) core.Rotate(AngularVelocity * dt, Space.Self);
            float scale = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .22f));
            transform.localScale = Vector3.one * scale * (IsSurfaceTarget ? .65f : 1);
            if (IsSurfaceTarget)
            {
                FacePlayer(game.ViewCamera.transform);
                return;
            }
            // Contact follows the visible mesh, including its current rotation.
            float clearance = transform.position.y - VisualBounds.min.y;
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
            FacePlayer(owner.ViewCamera.transform);
        }

        public void KeepClearOfPlayer(Transform player, float spawnDistance)
        {
            if (IsSurfaceTarget) { FacePlayer(player); return; }
            Vector3 delta = Vector3.ProjectOnPlane(transform.position - player.position, Vector3.up);
            if (delta.sqrMagnitude >= 1) return;
            if (delta.sqrMagnitude < .0001f) delta = Vector3.ProjectOnPlane(player.forward, Vector3.up);
            if (delta.sqrMagnitude < .0001f) delta = Vector3.forward;
            Vector3 position = transform.position;
            Vector3 safe = player.position + delta.normalized * Mathf.Max(1.2f, spawnDistance);
            position.x = safe.x; position.z = safe.z;
            transform.position = position;
        }

        public void FacePlayer(Transform player)
        {
            Vector3 direction = Vector3.ProjectOnPlane(player.position - SurfacePoint, surfaceNormal);
            if (direction.sqrMagnitude > .0001f) core.rotation = Quaternion.LookRotation(direction, surfaceNormal);
            Bounds bounds = VisualBounds;
            float offset = Vector3.Dot(new Vector3(Mathf.Abs(surfaceNormal.x), Mathf.Abs(surfaceNormal.y), Mathf.Abs(surfaceNormal.z)), bounds.extents)
                - Vector3.Dot(surfaceNormal, bounds.center - transform.position);
            transform.position = SurfacePoint + surfaceNormal * (offset + .005f);
        }
    }
}

