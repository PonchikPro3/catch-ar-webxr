using UnityEngine;

namespace CatchAR
{
    public sealed class OffscreenIndicators : MonoBehaviour
    {
        public CatchGame game;
        public RectTransform bounds;
        public OffscreenArrow[] markers;
        public float edgeInset = 30;
        public float spacing = 49;
        public int VisibleCount { get; private set; }
        Vector2[] positions;
        static readonly Color Mint = new Color(.48f, 1, .76f);
        static readonly Color Gold = new Color(1, .66f, .22f);

        void LateUpdate() { RefreshIndicators(); }

        public void RefreshIndicators()
        {
            VisibleCount = 0;
            if (markers == null) return;
            if (game != null && game.SimulationActive && bounds != null)
            {
                if (positions == null || positions.Length != markers.Length) positions = new Vector2[markers.Length];
                Camera camera = game.ViewCamera;
                var targets = game.Targets;
                for (int i = 0; i < targets.Count && VisibleCount < markers.Length; i++)
                {
                    var target = targets[i];
                    if (target == null || target.Resolved) continue;
                    if (!TryGetEdgePosition(camera, target.transform.position, bounds.rect, edgeInset, out Vector2 point, out float angle)) continue;
                    point = SeparateAlongEdge(point, bounds.rect, VisibleCount);
                    var marker = markers[VisibleCount];
                    if (marker == null) continue;
                    marker.gameObject.SetActive(true);
                    marker.rectTransform.anchoredPosition = point;
                    marker.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                    marker.color = target.IsGold ? Gold : Mint;
                    positions[VisibleCount++] = point;
                }
            }
            for (int i = VisibleCount; i < markers.Length; i++)
                if (markers[i] != null) markers[i].gameObject.SetActive(false);
        }

        Vector2 SeparateAlongEdge(Vector2 original, Rect area, int used)
        {
            bool verticalEdge = Mathf.Abs(Mathf.Abs(original.x - area.center.x) - (area.width * .5f - edgeInset)) < 1;
            for (int attempt = 0; attempt < 11; attempt++)
            {
                Vector2 candidate = original;
                float offset = attempt == 0 ? 0 : ((attempt + 1) / 2) * spacing * (attempt % 2 == 1 ? 1 : -1);
                if (verticalEdge) candidate.y = Mathf.Clamp(original.y + offset, area.yMin + edgeInset, area.yMax - edgeInset);
                else candidate.x = Mathf.Clamp(original.x + offset, area.xMin + edgeInset, area.xMax - edgeInset);
                bool overlaps = false;
                for (int j = 0; j < used; j++)
                    if ((positions[j] - candidate).sqrMagnitude < spacing * spacing * .9f) overlaps = true;
                if (!overlaps) return candidate;
            }
            return original;
        }

        // Uses the camera's actual projection matrix, including WebXR's asymmetric AR view.
        // A point behind the camera gets a horizontal turn direction instead of a mirrored projection.
        public static bool TryGetEdgePosition(Camera camera, Vector3 world, Rect area, float inset,
            out Vector2 point, out float angle)
        {
            point = Vector2.zero;
            angle = 0;
            if (camera == null || area.width <= inset * 2 || area.height <= inset * 2) return false;
            Vector3 local = camera.transform.InverseTransformPoint(world);
            if (local.sqrMagnitude < .000001f) return false;
            Vector3 viewport = camera.WorldToViewportPoint(world);
            if (viewport.z > camera.nearClipPlane && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1) return false;
            Vector2 direction;
            if (local.z <= camera.nearClipPlane)
            {
                float turn = local.x < -.001f ? -1 : 1;
                float horizontalDistance = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                direction = new Vector2(turn, Mathf.Clamp(local.y / Mathf.Max(.1f, horizontalDistance), -.7f, .7f));
            }
            else direction = new Vector2((viewport.x - .5f) * camera.pixelWidth, (viewport.y - .5f) * camera.pixelHeight);
            if (direction.sqrMagnitude < .000001f) direction = Vector2.right;
            direction.Normalize();
            Vector2 half = area.size * .5f - Vector2.one * inset;
            float scaleX = Mathf.Abs(direction.x) > .0001f ? half.x / Mathf.Abs(direction.x) : float.PositiveInfinity;
            float scaleY = Mathf.Abs(direction.y) > .0001f ? half.y / Mathf.Abs(direction.y) : float.PositiveInfinity;
            point = area.center + direction * Mathf.Min(scaleX, scaleY);
            angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return true;
        }
    }
}
