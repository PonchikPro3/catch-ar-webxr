using UnityEngine;

namespace CatchAR
{
    public sealed class CatchSpark : MonoBehaviour
    {
        public Vector3 velocity;
        float age;
        void Update()
        {
            age += Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.localScale = Vector3.one * Mathf.Max(0, .025f * (1 - age / .45f));
            if (age > .45f) Destroy(gameObject);
        }
    }
}
