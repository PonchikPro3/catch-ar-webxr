using UnityEngine;
using UnityEngine.UI;
namespace CatchAR
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TimerFillGraphic : UnityEngine.UI.Image
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0 || (type == Type.Filled && fillAmount <= 0)) return;
            float cap = Mathf.Min(r.height * .35f, r.width * .5f);
            Vector2[] points = { new Vector2(r.xMin, r.center.y), new Vector2(r.xMin + cap, r.yMax),
                new Vector2(r.xMax - cap, r.yMax), new Vector2(r.xMax, r.center.y),
                new Vector2(r.xMax - cap, r.yMin), new Vector2(r.xMin + cap, r.yMin) };
            // Clip a fixed shape instead of shrinking its pointed ends with the
            // Slider rect. A centre fan keeps every part of the fill covered.
            float edge = type == Type.Filled ? Mathf.Lerp(r.xMin, r.xMax, fillAmount) : r.xMax;
            var polygon = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Length];
                if (a.x <= edge) polygon.Add(a);
                if ((a.x <= edge) != (b.x <= edge))
                    polygon.Add(Vector2.Lerp(a, b, (edge - a.x) / (b.x - a.x)));
            }
            if (polygon.Count < 3) return;
            Vector2 center = Vector2.zero;
            foreach (var p in polygon) center += p;
            center /= polygon.Count;
            vh.AddVert(center, color, new Vector2(.5f, .5f));
            foreach (var p in polygon) vh.AddVert(p, color, new Vector2(.5f, .5f));
            for (int i = 0; i < polygon.Count; i++) vh.AddTriangle(0, i + 1, (i + 1) % polygon.Count + 1);
        }
    }
}
