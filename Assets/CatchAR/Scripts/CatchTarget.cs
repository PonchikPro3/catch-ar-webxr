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
            transform.localScale = Vector3.one * scale;
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
    }
}

