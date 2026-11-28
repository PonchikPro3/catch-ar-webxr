using System;
using System.Linq;
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
                var face = Material("Durachok Face", WarmPalette.Ink);
                foreach (var r in renderers) r.sharedMaterials = Enumerable.Repeat(r == body ? materials[0] : face, r.sharedMaterials.Length).ToArray();
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
