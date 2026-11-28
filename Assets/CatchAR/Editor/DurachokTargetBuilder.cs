using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace CatchAR.Editor
{
    public static class DurachokTargetBuilder
    {
        public static CatchTarget Rebuild()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Durachok.fbx");
            if (source == null) throw new Exception("Assets/Durachok.fbx is missing");
            var root = new GameObject("Catchable Durachok");
            try
            {
                var visual = new GameObject("Durachok Visual").transform;
                visual.SetParent(root.transform, false);
                var model = UnityEngine.Object.Instantiate(source, visual);
                model.name = "Durachok Model";
                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new Exception("Durachok has no renderers");
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                float scale = .36f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                model.transform.localScale *= scale;
                model.transform.localPosition -= bounds.center * scale;
                var body = renderers.FirstOrDefault(r => r.name == "Durachok") ?? renderers[0];
                var colors = new[] { WarmPalette.Red, WarmPalette.Orange, WarmPalette.Peach, WarmPalette.Cream };
                var materials = colors.Select((c, i) => Material("Durachok Body " + i, c)).ToArray();
                body.sharedMaterials = Enumerable.Repeat(materials[0], body.sharedMaterials.Length).ToArray();
                BuildFace(model);
                var target = root.AddComponent<CatchTarget>();
                target.core = target.ring = visual;
                target.coreRenderer = target.ringRenderer = body;
                target.visualRenderers = renderers;
                target.bodyMaterials = materials;
                root.AddComponent<SphereCollider>().radius = .20f;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/CatchAR/Prefabs/Catchable Energy.prefab");
                return prefab.GetComponent<CatchTarget>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void BuildFace(GameObject model)
        {
            var parts = model.GetComponentsInChildren<MeshFilter>().ToDictionary(f => f.sharedMesh.name);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            foreach (string eye in new[] { "Glaz_L", "Glaz_R" })
            {
                var surface = new FrontSurface(parts[eye].sharedMesh);
                Vector3 center = parts[eye].sharedMesh.bounds.center;
                const int sides = 28, rings = 4;
                int start = vertices.Count;
                vertices.Add(surface.Point(center.x, center.y));
                for (int ring = 1; ring <= rings; ring++)
                {
                    float radius = (float)ring / rings;
                    for (int i = 0; i < sides; i++)
                    {
                        float angle = i * (2 * Mathf.PI / sides);
                        vertices.Add(surface.Point(center.x + .104f * radius * Mathf.Cos(angle),
                            center.y + .112f * radius * Mathf.Sin(angle)));
                    }
                }
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    triangles.AddRange(new[] { start, start + 1 + i, start + 1 + next });
                    for (int ring = 0; ring < rings - 1; ring++)
                    {
                        int inner = start + 1 + ring * sides, outer = inner + sides;
                        triangles.AddRange(new[] { inner + i, outer + i, outer + next, inner + i, outer + next, inner + next });
                    }
                }
            }

            var head = new FrontSurface(parts["Body"].sharedMesh);
            const int columns = 36, rows = 6;
            int mouthStart = vertices.Count;
            for (int column = 0; column <= columns; column++)
            {
                float t = 2f * column / columns - 1;
                float x = -.017f + .44f * t;
                float y = -.115f + .17f * t * t;
                float halfThickness = .072f * Mathf.Sqrt(1 - t * t);
                for (int row = 0; row <= rows; row++)
                    vertices.Add(head.Point(x, y + halfThickness * (2f * row / rows - 1)));
            }
            for (int column = 0; column < columns; column++)
            for (int row = 0; row < rows; row++)
            {
                int a = mouthStart + column * (rows + 1) + row, b = a + rows + 1;
                triangles.AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
            }

            const string path = "Assets/CatchAR/Art/Durachok Expression.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Durachok expression" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            EditorUtility.SetDirty(mesh);
            parts["Rot"].sharedMesh = mesh;
            parts["Rot"].GetComponent<Renderer>().sharedMaterial = Material("Durachok Face", WarmPalette.Ink);
        }

        // Project face details onto the frontmost triangle of the corresponding FBX mesh.
        sealed class FrontSurface
        {
            readonly Vector3[] vertices;
            readonly int[] triangles;

            public FrontSurface(Mesh mesh)
            {
                using var data = MeshUtility.AcquireReadOnlyMeshData(mesh);
                using var positions = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp);
                using var indices = new NativeArray<int>((int)mesh.GetIndexCount(0), Allocator.Temp);
                data[0].GetVertices(positions);
                data[0].GetIndices(indices, 0);
                vertices = positions.ToArray();
                triangles = indices.ToArray();
            }

            public Vector3 Point(float x, float y)
            {
                float depth = float.NegativeInfinity;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]] - a;
                    Vector3 c = vertices[triangles[i + 2]] - a;
                    float determinant = b.x * c.y - b.y * c.x;
                    if (Mathf.Abs(determinant) < 1e-8f) continue;
                    float u = ((x - a.x) * c.y - (y - a.y) * c.x) / determinant;
                    float v = (b.x * (y - a.y) - b.y * (x - a.x)) / determinant;
                    if (u >= 0 && v >= 0 && u + v <= 1) depth = Mathf.Max(depth, a.z + u * b.z + v * c.z);
                }
                if (float.IsNegativeInfinity(depth)) throw new InvalidOperationException($"Face point outside mesh: {x}, {y}");
                return new Vector3(x, y, depth + .014f);
            }
        }

        static Material Material(string name, Color color)
        {
            string path = "Assets/CatchAR/Art/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .25f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
