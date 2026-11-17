using UnityEngine;

namespace CatchAR
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OffscreenArrow : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            Disc(mesh, radius, new Color(color.r, color.g, color.b, .38f));
            Disc(mesh, radius - 2, new Color(.025f, .055f, .075f, .94f));
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(-6, -8), color, Vector2.zero);
            mesh.AddVert(new Vector3(-6, 8), color, Vector2.zero);
            mesh.AddVert(new Vector3(9, 0), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        static void Disc(UnityEngine.UI.VertexHelper mesh, float radius, Color tint)
        {
            const int segments = 32;
            int start = mesh.currentVertCount;
            mesh.AddVert(Vector3.zero, tint, Vector2.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
            }
            for (int i = 0; i < segments; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
        }
    }
}
