using UnityEngine;

namespace CatchAR
{
    public static class TargetPicking
    {
        public static Bounds VisibleBounds(CatchTarget target)
        {
            return target.VisualBounds;
        }

        public static bool ScreenHit(Camera camera, Bounds bounds, Vector2 point, float padding)
        {
            // Project the visible geometry, using the actual (possibly asymmetric) XR projection.
            Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 screen = camera.WorldToScreenPoint(corner);
                if (screen.z <= camera.nearClipPlane) return false;
                min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
            }
            return point.x >= min.x - padding && point.x <= max.x + padding &&
                point.y >= min.y - padding && point.y <= max.y + padding;
        }

        public static bool RayHit(Ray ray, Bounds bounds, out float distance)
        {
            // These bounds come from current transforms, without a PhysX synchronization delay.
            if (bounds.Contains(ray.origin)) { distance = 0; return true; }
            return bounds.IntersectRay(ray, out distance);
        }
    }
}
