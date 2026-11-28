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
        public bool AtLimit => Count >= MaxSamples;
        public const int RequiredSurfaces = 8;
        public int SurfaceCount { get; private set; }
        public bool IsComplete { get; private set; }
        public float Progress => Mathf.Clamp01((float)SurfaceCount / RequiredSurfaces);
        readonly Dictionary<int, Region> regions = new Dictionary<int, Region>();
        readonly List<int> remove = new List<int>();
        Transform origin;
        GameObject visualRoot;
        readonly List<Vector3> hitWindow = new List<Vector3>();
        Vector3 previousNormal;
        Mesh sampleFill, sampleLines;
        GameObject sampleRoot;
        bool samplesDirty;
        int nextSample = -1;
        const int MaxSamples = 2048;
        const float HorizontalCos = .98480775f;
        sealed class Region
        {
            public GameObject root;
            public Mesh mesh, grid;
            public MeshRenderer fill;
            public LineRenderer edge;
            public Vector3 center, normal;
            public Vector3[] points;
            public Vector2Int cell;
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
            if (!IsRunning || IsComplete || (Count == 0 && !IsPaused)) return;
            IsPaused = !IsPaused;
        }
        public void ResetScan()
        {
            ClearGeometry();
            IsPaused = false;
        }
        public void HideGeometry() { if (visualRoot != null) visualRoot.SetActive(false); }
        public bool TryGetRandomSurfacePoint(out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            Vector3 a = Vector3.zero, b = a, c = a;
            float total = 0;
            foreach (var region in regions.Values)
            {
                if (region.points == null) continue;
                for (int i = 1; i + 1 < region.points.Length; i++)
                {
                    Vector3 p = origin.TransformPoint(region.points[0]);
                    Vector3 q = origin.TransformPoint(region.points[i]);
                    Vector3 r = origin.TransformPoint(region.points[i + 1]);
                    float area = Vector3.Cross(q - p, r - p).magnitude * .5f;
                    if (area <= .000001f) continue;
                    total += area;
                    if (UnityEngine.Random.value * total > area) continue;
                    a = p; b = q; c = r;
                    normal = origin.TransformDirection(region.normal).normalized;
                }
            }
            if (total <= 0) return false;
            float u = Mathf.Sqrt(UnityEngine.Random.value), v = UnityEngine.Random.value;
            point = (1 - u) * a + u * (1 - v) * b + u * v * c;
            if (Vector3.Dot(normal, Vector3.up) < 0) normal = -normal;
            return true;
        }
        void ClearGeometry()
        {
            foreach (var region in regions.Values) { Destroy(region.mesh); Destroy(region.grid); Destroy(region.root); }
            Destroy(sampleFill); Destroy(sampleLines); Destroy(sampleRoot);
            sampleFill=sampleLines=null; sampleRoot=null; samplesDirty=false;
            regions.Clear();
            HorizontalCount = VerticalCount = 0;
            SurfaceCount = 0;
            IsComplete = false;
            hitWindow.Clear();
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
            if (!IsTracking) { hitWindow.Clear(); return; }
            if (backend == "planes")
            {
                var live = new HashSet<int>();
                if (data.planes != null)
                    foreach (var plane in data.planes)
                    {
                        if (live.Count >= 64 || plane == null || plane.id <= 0 || !ValidNormal(plane.normal) ||
                            plane.points == null || plane.points.Length < 3 || plane.points.Length > 128) continue;
                        if (!Horizontal(plane.normal)) continue;
                        bool valid = true;
                        foreach (var p in plane.points) if (!Finite(p)) valid = false;
                        if (!valid) continue;
                        live.Add(plane.id);
                        SetRegion(plane.id, plane.points, plane.normal.normalized);
                    }
                remove.Clear();
                foreach (int id in regions.Keys) if (!live.Contains(id)) remove.Add(id);
                foreach (int id in remove) { Destroy(regions[id].mesh); Destroy(regions[id].grid); Destroy(regions[id].root); regions.Remove(id); }
            }
            else if (data.hits != null && data.hits.Length > 0 && data.hits[0] != null)
            {
                var hit = data.hits[0];
                if (Finite(hit.position) && ValidNormal(hit.normal) && Horizontal(hit.normal))
                {
                    Vector3 normal=hit.normal.normalized;
                    if(Vector3.Dot(origin.TransformDirection(normal),Vector3.up)<0)normal=-normal;
                    if(hitWindow.Count>0 && (Vector3.Dot(normal,previousNormal)<.9961947f ||
                        Vector3.Distance(hitWindow[hitWindow.Count-1],hit.position)>.25f ||
                        Mathf.Abs(Vector3.Dot(normal,hit.position-hitWindow[hitWindow.Count-1]))>.012f))hitWindow.Clear();
                    previousNormal=normal;
                    hitWindow.Add(hit.position);
                    if(hitWindow.Count>3)hitWindow.RemoveAt(0);
                    if(hitWindow.Count==3)
                    {
                        float[] heights={Vector3.Dot(normal,hitWindow[0]),Vector3.Dot(normal,hitWindow[1]),Vector3.Dot(normal,hitWindow[2])};
                        Array.Sort(heights);
                        AddSample(hit.position+normal*(heights[1]-Vector3.Dot(normal,hit.position)),normal);
                    }
                }
                else hitWindow.Clear();
            }
            else hitWindow.Clear();
            Recount();
            RecountSurfaces();
            if (SurfaceCount >= RequiredSurfaces) { IsComplete = true; IsPaused = true; }
        }
        void RecountSurfaces()
        {
            if (Backend == "planes") { SurfaceCount = Count; return; }
            // Hit-test returns cells rather than plane identities. Count connected
            // coplanar patches, keeping different height layers separate.
            var cells = new Dictionary<Vector2Int, List<Region>>();
            foreach (var r in regions.Values)
            {
                if (!cells.TryGetValue(r.cell, out var list)) { list = new List<Region>(); cells.Add(r.cell, list); }
                list.Add(r);
            }
            SurfaceCount = 0;
            var visited = new HashSet<Region>();
            var queue = new Queue<Region>();
            var offsets = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            foreach (var seed in regions.Values)
            {
                if (!visited.Add(seed)) continue;
                SurfaceCount++; queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    var r = queue.Dequeue();
                    foreach (var offset in offsets)
                    {
                        if (!cells.TryGetValue(r.cell + offset, out var neighbours)) continue;
                        foreach (var b in neighbours)
                        {
                            if (visited.Contains(b) || Vector3.Dot(r.normal, b.normal) < .9961947f ||
                                Mathf.Abs(Vector3.Dot(r.normal, b.center - r.center)) > .025f) continue;
                            visited.Add(b); queue.Enqueue(b);
                        }
                    }
                }
            }
        }
        void AddSample(Vector3 point, Vector3 normal)
        {
            Vector3 world=origin.TransformPoint(point);
            var cell=new Vector2Int(Mathf.FloorToInt(world.x/ScanMeshBuilder.CellSize),Mathf.FloorToInt(world.z/ScanMeshBuilder.CellSize));
            Region sample=null;
            foreach(var region in regions.Values)
                if(region.cell==cell && Vector3.Dot(region.normal,normal)>.9961947f && Mathf.Abs(Vector3.Dot(normal,region.center-point))<.015f){sample=region;break;}
            if(sample==null)
            {
                if(regions.Count>=MaxSamples)return;
                sample=new Region{cell=cell,center=point,normal=normal};regions.Add(nextSample--,sample);
            }
            else {sample.center=Vector3.Lerp(sample.center,point,.25f);sample.normal=Vector3.Slerp(sample.normal,normal,.25f).normalized;}
            Vector3 worldNormal=origin.TransformDirection(sample.normal).normalized;
            float height=Vector3.Dot(worldNormal,origin.TransformPoint(sample.center));
            sample.points=new Vector3[4];
            for(int i=0;i<4;i++)
            {
                float x=(cell.x+(i==1||i==2?1:0))*ScanMeshBuilder.CellSize;
                float z=(cell.y+(i>=2?1:0))*ScanMeshBuilder.CellSize;
                sample.points[i]=origin.InverseTransformPoint(new Vector3(x,(height-worldNormal.x*x-worldNormal.z*z)/worldNormal.y,z));
            }
            samplesDirty=true;
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
                var gridObject=new GameObject("5 cm grid");gridObject.transform.SetParent(region.root.transform,false);
                region.grid=new Mesh{name="Clipped horizontal grid"};gridObject.AddComponent<MeshFilter>().sharedMesh=region.grid;
                Tint(gridObject.AddComponent<MeshRenderer>(),.6f);
                var edge = new GameObject("Boundary");
                edge.transform.SetParent(region.root.transform, false);
                region.edge = edge.AddComponent<LineRenderer>();
                region.edge.useWorldSpace = false;
                region.edge.loop = true;
                region.edge.widthMultiplier = .0025f;
                region.edge.sharedMaterial = lineMaterial;
                regions.Add(id, region);
            }
            region.center = Vector3.zero;
            region.points = points;
            region.normal = normal;
            var vertices = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) { region.center += points[i]; vertices[i] = points[i] + normal * .004f; }
            region.center /= points.Length;
            // WebXR plane polygons are convex; fan triangulation preserves their actual boundary.
            var triangles = new int[(points.Length - 2) * 3];
            for (int i = 0; i < points.Length - 2; i++) { triangles[i*3] = 0; triangles[i*3+1] = i+1; triangles[i*3+2] = i+2; }
            region.mesh.Clear(); region.mesh.vertices = vertices; region.mesh.triangles = triangles;
            region.mesh.RecalculateBounds();
            ScanMeshBuilder.Grid(region.grid,points,normal,origin.InverseTransformDirection(Vector3.right));
            region.edge.positionCount = vertices.Length;
            region.edge.SetPositions(vertices);
            Color color = WarmPalette.Orange;
            region.edge.startColor = region.edge.endColor = color;
            var block = new MaterialPropertyBlock(); color.a = .18f; block.SetColor("_Color", color); region.fill.SetPropertyBlock(block);
        }
        void LateUpdate()
        {
            if(!samplesDirty || !IsRunning)return;
            samplesDirty=false;
            if(sampleRoot==null)
            {
                sampleRoot=new GameObject("Confirmed 5 cm cells");sampleRoot.transform.SetParent(visualRoot.transform,false);
                sampleFill=new Mesh{name="Sample cells"};sampleLines=new Mesh{name="Sample grid"};
                sampleRoot.AddComponent<MeshFilter>().sharedMesh=sampleFill;Tint(sampleRoot.AddComponent<MeshRenderer>(),.14f);
                var lines=new GameObject("Grid");lines.transform.SetParent(sampleRoot.transform,false);
                lines.AddComponent<MeshFilter>().sharedMesh=sampleLines;Tint(lines.AddComponent<MeshRenderer>(),.65f);
            }
            var fillV=new List<Vector3>();var fillI=new List<int>();var lineV=new List<Vector3>();var lineI=new List<int>();
            foreach(var sample in regions.Values)
            {
                var points=new Vector3[4];for(int i=0;i<4;i++)points[i]=sample.points[i]+sample.normal*.001f;
                ScanMeshBuilder.Polygon(fillV,fillI,points);
                for(int i=0;i<4;i++)ScanMeshBuilder.Strip(lineV,lineI,points[i],points[(i+1)%4],sample.normal,.0012f);
            }
            ScanMeshBuilder.Write(sampleFill,fillV,fillI);ScanMeshBuilder.Write(sampleLines,lineV,lineI);
        }
        void Tint(MeshRenderer renderer,float alpha)
        {
            renderer.sharedMaterial=surfaceMaterial;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            var block=new MaterialPropertyBlock();Color color=WarmPalette.Orange;color.a=alpha;block.SetColor("_Color",color);renderer.SetPropertyBlock(block);
        }
        bool Horizontal(Vector3 normal)=>Mathf.Abs(Vector3.Dot(origin.TransformDirection(normal).normalized,Vector3.up))>=HorizontalCos;
        void Recount()
        {
            HorizontalCount = VerticalCount = 0;
            foreach (var region in regions.Values)
            {
                Vector3 normal = origin.TransformDirection(region.normal).normalized;
                if (Mathf.Abs(normal.y) >= HorizontalCos) HorizontalCount++;
                else if (Mathf.Abs(normal.y) < .25f) VerticalCount++;
            }
        }
        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        static bool ValidNormal(Vector3 n) => Finite(n) && n.sqrMagnitude > .5f;
    }
}
