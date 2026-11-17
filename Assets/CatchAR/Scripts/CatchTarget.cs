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
        float fallSpeed, acceleration, age;
        const float GroundClearance = .15f;
        bool resolved;

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
            if (resolved || !game.SimulationActive) return;
            age += Time.deltaTime;
            float dt = Time.deltaTime;
            Vector3 position = transform.position;
            position.y -= fallSpeed * dt + .5f * acceleration * dt * dt;
            fallSpeed += acceleration * dt;
            position.y = Mathf.Max(position.y, GroundHeight + GroundClearance);
            transform.position = position;
            core.Rotate(24 * Time.deltaTime, 50 * Time.deltaTime, 16 * Time.deltaTime);
            ring.Rotate(0, 0, -70 * Time.deltaTime, Space.Self);
            float scale = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .22f));
            transform.localScale = Vector3.one * scale;
            if (position.y <= GroundHeight + GroundClearance)
            {
                resolved = true;
                game.RegisterEscape(this);
                Destroy(gameObject);
            }
        }

        public void Catch()
        {
            if (resolved) return;
            resolved = true;
            game.RegisterCatch(this);
            // Small reusable-looking shards give immediate spatial feedback without textures.
            for (int i = 0; i < 8; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Catch sparkle";
                Destroy(shard.GetComponent<Collider>());
                shard.transform.position = transform.position;
                shard.transform.localScale = Vector3.one * .025f;
                shard.GetComponent<Renderer>().sharedMaterial = IsGold ? goldMaterial : mintMaterial;
                shard.AddComponent<CatchSpark>().velocity = Random.onUnitSphere * .55f;
            }
            Destroy(gameObject);
        }
    }
}
