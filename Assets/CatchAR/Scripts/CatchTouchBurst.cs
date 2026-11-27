using UnityEngine;
namespace CatchAR
{
    public sealed class CatchTouchBurst : MonoBehaviour
    {
        public CanvasGroup opacity;
        float elapsed;
        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / .42f);
            float overshoot = 1 + 2.7f * Mathf.Pow(t - 1, 3) + 1.7f * Mathf.Pow(t - 1, 2);
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(.22f, 1.15f, overshoot);
            opacity.alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1, t));
            if (t >= 1) Destroy(gameObject);
        }
    }
}
