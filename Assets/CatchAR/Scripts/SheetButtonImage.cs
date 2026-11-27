using UnityEngine;
using UnityEngine.UI;

namespace CatchAR
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SheetButtonImage : UnityEngine.UI.Image
    {
        public Vector2[] outline;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (sprite == null || outline == null || outline.Length != 4) { base.OnPopulateMesh(vh); return; }
            vh.Clear();
            Rect r = rectTransform.rect, source = sprite.rect;
            foreach (Vector2 p in outline)
                vh.AddVert(new Vector2(r.xMin + p.x * r.width, r.yMin + p.y * r.height), color,
                    new Vector2((source.xMin + p.x * source.width) / sprite.texture.width,
                        (source.yMin + p.y * source.height) / sprite.texture.height));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
