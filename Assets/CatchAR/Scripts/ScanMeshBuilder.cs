using System.Collections.Generic;
using UnityEngine;

namespace CatchAR
{
    public static class ScanMeshBuilder
    {
        public const float CellSize = .05f;
        public static void Polygon(List<Vector3> vertices, List<int> triangles, Vector3[] points)
        {
            int first=vertices.Count;
            vertices.AddRange(points);
            for(int i=1;i<points.Length-1;i++){triangles.Add(first);triangles.Add(first+i);triangles.Add(first+i+1);}
        }
        public static void Strip(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 normal, float width)
        {
            if((a-b).sqrMagnitude<.0000001f)return;
            Vector3 side=Vector3.Cross(normal,b-a).normalized*(width*.5f);
            Polygon(vertices,triangles,new[]{a-side,b-side,b+side,a+side});
        }
        public static void Grid(Mesh mesh, Vector3[] polygon, Vector3 normal, Vector3 axis)
        {
            Vector3 u=Vector3.ProjectOnPlane(axis,normal).normalized;
            Vector3 v=Vector3.Cross(normal,u).normalized;
            float height=Vector3.Dot(polygon[0],normal);
            var projected=new Vector2[polygon.Length];
            Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
            for(int i=0;i<polygon.Length;i++)
            {
                projected[i]=new Vector2(Vector3.Dot(polygon[i],u),Vector3.Dot(polygon[i],v));
                min=Vector2.Min(min,projected[i]);max=Vector2.Max(max,projected[i]);
            }
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int axisIndex=0;axisIndex<2;axisIndex++)
            {
                float start=Mathf.Ceil(min[axisIndex]/CellSize)*CellSize;
                int lines=Mathf.Min(1024,Mathf.FloorToInt((max[axisIndex]-start)/CellSize)+1);
                for(int line=0;line<lines;line++)
                {
                    float coordinate=start+line*CellSize,low=float.PositiveInfinity,high=float.NegativeInfinity;
                    int other=1-axisIndex;
                    for(int j=0;j<projected.Length;j++)
                    {
                        Vector2 a=projected[j],b=projected[(j+1)%projected.Length];
                        float delta=b[axisIndex]-a[axisIndex];
                        if(Mathf.Abs(delta)<.000001f)
                        {
                            if(Mathf.Abs(coordinate-a[axisIndex])<.00001f){low=Mathf.Min(low,Mathf.Min(a[other],b[other]));high=Mathf.Max(high,Mathf.Max(a[other],b[other]));}
                            continue;
                        }
                        float t=(coordinate-a[axisIndex])/delta;
                        if(t<0||t>1)continue;
                        float hit=Mathf.Lerp(a[other],b[other],t);low=Mathf.Min(low,hit);high=Mathf.Max(high,hit);
                    }
                    if(float.IsInfinity(low)||high-low<.001f)continue;
                    Vector3 a3=(axisIndex==0?u*coordinate+v*low:u*low+v*coordinate)+normal*(height+.0015f);
                    Vector3 b3=(axisIndex==0?u*coordinate+v*high:u*high+v*coordinate)+normal*(height+.0015f);
                    Strip(vertices,triangles,a3,b3,normal,.0012f);
                }
            }
            Write(mesh,vertices,triangles);
        }
        public static void Write(Mesh mesh,List<Vector3> vertices,List<int> triangles)
        {
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
    }
}
