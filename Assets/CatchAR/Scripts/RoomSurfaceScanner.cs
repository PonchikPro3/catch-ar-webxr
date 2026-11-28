using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CatchAR
{
    [Serializable] public sealed class ScanHit
    {
        public Vector3 position, normal;
        public int ray;
        public bool verified, fullCell, rejected;
    }
    [Serializable] public sealed class ScanPacket
    {
        public string backend;
        public bool tracking, unavailable;
        public ScanHit[] hits;
    }
    public sealed class RoomSurfaceScanner : MonoBehaviour
    {
        public Material surfaceMaterial, lineMaterial;
        [Min(.01f)] public float minimumSurfaceArea = .04f;
        [Min(.05f)] public float minimumSurfaceWidth = .20f;
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsTracking { get; private set; }
        public string Backend { get; private set; } = "waiting";
        public bool Unavailable { get; private set; }
        public bool PrecisionAvailable => Backend == "depth" || Backend == "mesh" || Backend == "hit-test";
        public int Count { get; private set; }
        public int CandidateCount => regions.Count;
        public int SurfaceCount { get; private set; }
        public int HorizontalCount => SurfaceCount;
        public int VerticalCount => 0;
        public bool AtLimit => regions.Count >= 2048;
        sealed class Region
        {
            public Vector3 center, normal;
            public Vector3[] points;
            public Vector2Int cell;
            public int coverage, rejects;
            public float seen;
            public bool visible;
        }
        sealed class History { public readonly List<Vector3> points=new List<Vector3>(); public Vector3 normal; }
        readonly Dictionary<int,Region> regions=new Dictionary<int,Region>();
        readonly Dictionary<int,History> histories=new Dictionary<int,History>();
        Transform origin;
        GameObject visualRoot,sampleRoot;
        Mesh sampleFill,sampleLines;
        bool dirty;
        int nextId;
        float lastExpiry;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void CatchARScanStart(string receiver);
        [DllImport("__Internal")] static extern void CatchARScanStop();
        [DllImport("__Internal")] static extern void CatchARScanConfigure(int enabled);
#endif
        public static void Configure(bool enabled)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CatchARScanConfigure(enabled?1:0);
#endif
        }
        public void Begin(Transform trackingOrigin)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Room scanning requires Play Mode");
            End();origin=trackingOrigin;visualRoot=new GameObject("Scanned Room Surfaces");visualRoot.transform.SetParent(origin,false);
            IsRunning=true;Backend="waiting";
#if UNITY_WEBGL && !UNITY_EDITOR
            CatchARScanStart(gameObject.name);
#elif UNITY_EDITOR
            Backend="editor";
#endif
        }
        public void End()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(IsRunning)CatchARScanStop();
#endif
            IsRunning=IsPaused=IsTracking=false;ResetScan();if(visualRoot!=null)visualRoot.SetActive(false);Release(visualRoot);visualRoot=null;
        }
        void OnDisable(){End();}
        public void TogglePause(){if(IsRunning&&(IsPaused||(Count>0&&PrecisionAvailable&&IsTracking)))IsPaused=!IsPaused;}
        public void ResetScan()
        {
            regions.Clear();histories.Clear();Count=SurfaceCount=0;IsPaused=false;nextId=0;
            Release(sampleFill);Release(sampleLines);Release(sampleRoot);sampleFill=sampleLines=null;sampleRoot=null;dirty=false;
        }
        static void Release(UnityEngine.Object owned)
        {
            if(owned==null)return;if(Application.isPlaying)Destroy(owned);else DestroyImmediate(owned);
        }
        [UnityEngine.Scripting.Preserve]
        public void ReceiveScanFrame(string json)
        {
            if(!IsRunning||IsPaused)return;
            ScanPacket data;try{data=JsonUtility.FromJson<ScanPacket>(json);}catch(ArgumentException){return;}if(data==null)return;
            Backend=data.backend;IsTracking=data.tracking;Unavailable=data.unavailable;
            visualRoot.SetActive(IsTracking&&PrecisionAvailable);
            if(!IsTracking||!PrecisionAvailable){histories.Clear();return;}
            var seenRays=new HashSet<int>();
            if(data.hits!=null)foreach(var hit in data.hits)
            {
                if(hit==null||hit.ray<0||hit.ray>8||!Finite(hit.position)||!Finite(hit.normal)||hit.normal.sqrMagnitude<.5f)continue;
                Vector3 normal=hit.normal.normalized;
                float up=Vector3.Dot(origin.TransformDirection(normal),Vector3.up);
                if(Mathf.Abs(up)<.98480775f)continue;if(up<0)normal=-normal;
                if(hit.rejected){Invalidate(hit.position,normal);histories.Remove(hit.ray);continue;}
                if(!hit.verified){histories.Remove(hit.ray);continue;}
                seenRays.Add(hit.ray);
                if(!histories.TryGetValue(hit.ray,out History h)){h=new History();histories.Add(hit.ray,h);}
                if(h.points.Count>0&&(Vector3.Dot(h.normal,normal)<.9961947f||Vector3.Distance(h.points[h.points.Count-1],hit.position)>.25f||Mathf.Abs(Vector3.Dot(normal,hit.position-h.points[h.points.Count-1]))>.012f))h.points.Clear();
                h.normal=normal;h.points.Add(hit.position);if(h.points.Count>3)h.points.RemoveAt(0);
                if(h.points.Count<3)continue;
                float[] heights={Vector3.Dot(normal,h.points[0]),Vector3.Dot(normal,h.points[1]),Vector3.Dot(normal,h.points[2])};Array.Sort(heights);
                AddSample(hit.position+normal*(heights[1]-Vector3.Dot(normal,hit.position)),normal,hit.position,hit.fullCell);
            }
            var missing=new List<int>();foreach(int ray in histories.Keys)if(!seenRays.Contains(ray))missing.Add(ray);foreach(int ray in missing)histories.Remove(ray);
            if(dirty)Classify();
        }
        Vector2Int Cell(Vector3 point)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x/.05f),Mathf.FloorToInt(point.z/.05f));
        }
        void Invalidate(Vector3 point,Vector3 normal)
        {
            var cell=Cell(point);var gone=new List<int>();
            foreach(var pair in regions)if(pair.Value.cell==cell&&Mathf.Abs(Vector3.Dot(normal,pair.Value.center-point))<.025f&&++pair.Value.rejects>=2)gone.Add(pair.Key);
            foreach(int id in gone){regions.Remove(id);dirty=true;}
        }
        void AddSample(Vector3 point,Vector3 normal,Vector3 observed,bool fullCell)
        {
            var cell=Cell(point);Region sample=null;
            foreach(var r in regions.Values)if(r.cell==cell&&Vector3.Dot(r.normal,normal)>.9961947f&&Mathf.Abs(Vector3.Dot(normal,r.center-point))<.015f){sample=r;break;}
            if(sample==null){if(AtLimit)return;sample=new Region{cell=cell,center=point,normal=normal};regions.Add(nextId++,sample);}
            else{sample.center=Vector3.Lerp(sample.center,point,.25f);sample.normal=Vector3.Slerp(sample.normal,normal,.25f).normalized;}
            sample.seen=Time.unscaledTime;sample.rejects=0;
            int quadrant=(observed.x-(cell.x*.05f)>=.025f?1:0)+(observed.z-(cell.y*.05f)>=.025f?2:0);
            sample.coverage=fullCell?15:sample.coverage|(1<<quadrant);
            Vector3 n=sample.normal;
            float height=Vector3.Dot(n,sample.center);sample.points=new Vector3[4];
            for(int i=0;i<4;i++)
            {
                float x=(cell.x+(i==1||i==2?1:0))*.05f,z=(cell.y+(i>=2?1:0))*.05f;
                sample.points[i]=new Vector3(x,(height-n.x*x-n.z*z)/n.y,z);
            }
            dirty=true;
        }
        void Classify()
        {
            Count=SurfaceCount=0;
            var byCell=new Dictionary<Vector2Int,List<int>>();
            foreach(var pair in regions)
            {
                pair.Value.visible=false;if(pair.Value.coverage!=15)continue;
                if(!byCell.TryGetValue(pair.Value.cell,out var ids)){ids=new List<int>();byCell.Add(pair.Value.cell,ids);}ids.Add(pair.Key);
            }
            var visited=new HashSet<int>();var queue=new Queue<int>();
            foreach(var pair in regions)
            {
                if(pair.Value.coverage!=15||!visited.Add(pair.Key))continue;
                queue.Enqueue(pair.Key);var component=new List<int>();Vector2Int min=pair.Value.cell,max=min;
                while(queue.Count>0)
                {
                    int id=queue.Dequeue();var r=regions[id];component.Add(id);min=Vector2Int.Min(min,r.cell);max=Vector2Int.Max(max,r.cell);
                    foreach(var offset in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                    {
                        if(!byCell.TryGetValue(r.cell+offset,out var neighbours))continue;
                        foreach(int other in neighbours)
                        {
                            var b=regions[other];if(visited.Contains(other)||Vector3.Dot(r.normal,b.normal)<.9961947f||Mathf.Abs(Vector3.Dot(r.normal,r.center-b.center))>.015f)continue;
                            visited.Add(other);queue.Enqueue(other);
                        }
                    }
                }
                if(component.Count*.0025f+.000001f<minimumSurfaceArea||(max.x-min.x+1)*.05f<minimumSurfaceWidth||(max.y-min.y+1)*.05f<minimumSurfaceWidth)continue;
                SurfaceCount++;Count+=component.Count;foreach(int id in component)regions[id].visible=true;
            }
        }
        void LateUpdate()
        {
            if(!IsRunning)return;
            if(!IsPaused&&Time.unscaledTime-lastExpiry>1)
            {
                lastExpiry=Time.unscaledTime;var expired=new List<int>();foreach(var pair in regions)if(Time.unscaledTime-pair.Value.seen>30)expired.Add(pair.Key);
                foreach(int id in expired){regions.Remove(id);dirty=true;}if(expired.Count>0)Classify();
            }
            if(!dirty)return;dirty=false;
            if(sampleRoot==null)
            {
                sampleRoot=new GameObject("Verified surface cells");sampleRoot.transform.SetParent(visualRoot.transform,false);
                sampleFill=new Mesh{name="Verified cells"};sampleLines=new Mesh{name="Verified grid"};sampleRoot.AddComponent<MeshFilter>().sharedMesh=sampleFill;Tint(sampleRoot.AddComponent<MeshRenderer>(),.14f);
                var lines=new GameObject("Grid");lines.transform.SetParent(sampleRoot.transform,false);lines.AddComponent<MeshFilter>().sharedMesh=sampleLines;Tint(lines.AddComponent<MeshRenderer>(),.65f);
            }
            var fv=new List<Vector3>();var fi=new List<int>();var lv=new List<Vector3>();var li=new List<int>();
            foreach(var sample in regions.Values)
            {
                if(!sample.visible)continue;var points=new Vector3[4];for(int i=0;i<4;i++)points[i]=sample.points[i]+sample.normal*.001f;
                ScanMeshBuilder.Polygon(fv,fi,points);for(int i=0;i<4;i++)ScanMeshBuilder.Strip(lv,li,points[i],points[(i+1)%4],sample.normal,.0012f);
            }
            ScanMeshBuilder.Write(sampleFill,fv,fi);ScanMeshBuilder.Write(sampleLines,lv,li);
        }
        void Tint(MeshRenderer renderer,float alpha)
        {
            renderer.sharedMaterial=surfaceMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            var block=new MaterialPropertyBlock();Color c=WarmPalette.Orange;c.a=alpha;block.SetColor("_Color",c);renderer.SetPropertyBlock(block);
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);
    }
}
