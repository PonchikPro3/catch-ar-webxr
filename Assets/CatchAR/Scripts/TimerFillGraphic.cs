using UnityEngine;
using UnityEngine.UI;
namespace CatchAR
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TimerFillGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float cap = Mathf.Min(r.height * .35f, r.width * .5f);
            Vector2[] points = { new Vector2(r.xMin, r.center.y), new Vector2(r.xMin + cap, r.yMax),
                new Vector2(r.xMax - cap, r.yMax), new Vector2(r.xMax, r.center.y),
                new Vector2(r.xMax - cap, r.yMin), new Vector2(r.xMin + cap, r.yMin) };
            foreach (var p in points) vh.AddVert(p, color, Vector2.zero);
            for (int i = 1; i < 5; i++) vh.AddTriangle(0, i, i + 1);
        }
    }
}
