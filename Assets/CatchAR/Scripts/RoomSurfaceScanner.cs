using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatchAR
{
    [Serializable] public sealed class ScanPlane { public int id; public Vector3 normal; public Vector3[] points; }
    [Serializable] public sealed class ScanHit { public Vector3 position, normal; }
    [Serializable] public sealed class ScanPacket
    {
        public string backend;
        public bool tracking, unavailable;
        public ScanPlane[] planes;
        public ScanHit[] hits;
    }

    public sealed class RoomSurfaceScanner : MonoBehaviour
    {
        public Material surfaceMaterial, lineMaterial;
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsTracking { get; private set; }
        public string Backend { get; private set; } = "waiting";
        public bool Unavailable { get; private set; }
        public int Count => regions.Count;
        public int HorizontalCount { get; private set; }
        public int VerticalCount { get; private set; }
        public bool AtLimit => Count >= 256;
        readonly Dictionary<int, Region> regions = new Dictionary<int, Region>();
        readonly List<int> remove = new List<int>();
        Transform origin;
        GameObject visualRoot;
        Vector3 previousHit;
        bool hadHit;
        int nextSample = -1;
        const int MaxSamples = 256;
        sealed class Region
        {
            public GameObject root;
            public Mesh mesh;
            public MeshRenderer fill;
            public LineRenderer edge;
            public Vector3 center, normal;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void CatchARScanStart(string receiver);
        [DllImport("__Internal")] static extern void CatchARScanStop();
        [DllImport("__Internal")] static extern void CatchARScanConfigure(int enabled);
#endif
        public static void Configure(bool enabled)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CatchARScanConfigure(enabled ? 1 : 0);
#endif
        }
        public void Begin(Transform trackingOrigin)
        {
            End();
            origin = trackingOrigin;
            visualRoot = new GameObject("Scanned Room Surfaces");
            visualRoot.transform.SetParent(origin, false);
            IsRunning = true;
            Backend = "waiting";
#if UNITY_WEBGL && !UNITY_EDITOR
            CatchARScanStart(gameObject.name);
#elif UNITY_EDITOR
            Backend = "editor";
#endif
        }
        public void End()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (IsRunning) CatchARScanStop();
#endif
            IsRunning = IsPaused = IsTracking = false;
            ClearGeometry();
            if (visualRoot != null) Destroy(visualRoot);
            visualRoot = null;
        }
        void OnDisable() { End(); }
        public void TogglePause()
        {
            if (!IsRunning || (Count == 0 && !IsPaused)) return;
            IsPaused = !IsPaused;
        }
        public void ResetScan()
        {
            ClearGeometry();
            IsPaused = false;
        }
        void ClearGeometry()
        {
            foreach (var region in regions.Values) { Destroy(region.mesh); Destroy(region.root); }
            regions.Clear();
            HorizontalCount = VerticalCount = 0;
            hadHit = false;
            nextSample = -1;
        }
        [UnityEngine.Scripting.Preserve]
        public void ReceiveScanFrame(string json)
        {
            if (!IsRunning || IsPaused) return;
            ScanPacket data;
            try { data = JsonUtility.FromJson<ScanPacket>(json); }
            catch (ArgumentException) { return; }
            if (data == null) return;
            string backend = data.backend == "planes" ? "planes" : "hits";
            if (Backend != backend) { ClearGeometry(); Backend = backend; }
            IsTracking = data.tracking;
            Unavailable = data.unavailable;
            visualRoot.SetActive(IsTracking);
            if (!IsTracking) { hadHit = false; return; }
            if (backend == "planes")
            {
                var live = new HashSet<int>();
                if (data.planes != null)
                    foreach (var plane in data.planes)
                    {
                        if (live.Count >= 64 || plane == null || plane.id <= 0 || !ValidNormal(plane.normal) ||
                            plane.points == null || plane.points.Length < 3 || plane.points.Length > 128) continue;
                        bool valid = true;
                        foreach (var p in plane.points) if (!Finite(p)) valid = false;
                        if (!valid) continue;
                        live.Add(plane.id);
                        SetRegion(plane.id, plane.points, plane.normal.normalized);
                    }
                remove.Clear();
                foreach (int id in regions.Keys) if (!live.Contains(id)) remove.Add(id);
                foreach (int id in remove) { Destroy(regions[id].mesh); Destroy(regions[id].root); regions.Remove(id); }
            }
            else if (data.hits != null && data.hits.Length > 0 && data.hits[0] != null)
            {
                var hit = data.hits[0];
                if (Finite(hit.position) && ValidNormal(hit.normal))
                {
                    // Two nearby real samples reject a single unstable hit; no floor estimate is used.
                    if (hadHit && Vector3.Distance(previousHit, hit.position) < .12f) AddSample(hit.position, hit.normal.normalized);
                    previousHit = hit.position;
                    hadHit = true;
                }
            }
            else hadHit = false;
            Recount();
        }
        void AddSample(Vector3 point, Vector3 normal)
        {
            foreach (var region in regions.Values)
                if (Vector3.Distance(region.center, point) < .16f && Mathf.Abs(Vector3.Dot(region.normal, normal)) > .96f) return;
            if (regions.Count >= MaxSamples) return;
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < .8f ? Vector3.up : Vector3.right).normalized * .085f;
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized * .085f;
            SetRegion(nextSample--, new[] {point-tangent-bitangent, point+tangent-bitangent, point+tangent+bitangent, point-tangent+bitangent}, normal);
        }
        void SetRegion(int id, Vector3[] points, Vector3 normal)
        {
            if (!regions.TryGetValue(id, out Region region))
            {
                region = new Region { root = new GameObject("Surface " + id), mesh = new Mesh { name = "Scanned surface" } };
                region.root.transform.SetParent(visualRoot.transform, false);
                region.root.AddComponent<MeshFilter>().sharedMesh = region.mesh;
                region.fill = region.root.AddComponent<MeshRenderer>();
                region.fill.sharedMaterial = surfaceMaterial;
                region.fill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                region.fill.receiveShadows = false;
                var edge = new GameObject("Boundary");
                edge.transform.SetParent(region.root.transform, false);
                region.edge = edge.AddComponent<LineRenderer>();
                region.edge.useWorldSpace = false;
                region.edge.loop = true;
                region.edge.widthMultiplier = .009f;
                region.edge.sharedMaterial = lineMaterial;
                regions.Add(id, region);
            }
            region.center = Vector3.zero;
            region.normal = normal;
            var vertices = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) { region.center += points[i]; vertices[i] = points[i] + normal * .004f; }
            region.center /= points.Length;
            // WebXR plane polygons are convex; fan triangulation preserves their actual boundary.
            var triangles = new int[(points.Length - 2) * 3];
            for (int i = 0; i < points.Length - 2; i++) { triangles[i*3] = 0; triangles[i*3+1] = i+1; triangles[i*3+2] = i+2; }
            region.mesh.Clear(); region.mesh.vertices = vertices; region.mesh.triangles = triangles;
            region.mesh.RecalculateBounds();
            region.edge.positionCount = vertices.Length;
            region.edge.SetPositions(vertices);
            Color color = Mathf.Abs(normal.y) > .8f ? WarmPalette.Orange : Mathf.Abs(normal.y) < .25f ? WarmPalette.Red : WarmPalette.Cream;
            region.edge.startColor = region.edge.endColor = color;
            var block = new MaterialPropertyBlock(); color.a = .18f; block.SetColor("_Color", color); region.fill.SetPropertyBlock(block);
        }
        void Recount()
        {
            HorizontalCount = VerticalCount = 0;
            foreach (var region in regions.Values)
            {
                Vector3 normal = origin.TransformDirection(region.normal).normalized;
                if (Mathf.Abs(normal.y) > .8f) HorizontalCount++;
                else if (Mathf.Abs(normal.y) < .25f) VerticalCount++;
            }
        }
        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        static bool ValidNormal(Vector3 n) => Finite(n) && n.sqrMagnitude > .5f;
    }
}
