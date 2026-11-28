if(!UnityEngine.Application.isPlaying)throw new System.Exception("Play Mode required");
var h=UnityEngine.Object.FindFirstObjectByType<CatchAR.CatchHud>();
h.playHud.SetActive(true);
foreach(var slider in new[]{h.timeSlider,h.scanProgress})
{
 foreach(float fraction in new[]{0f,.001f,.125f,.5f,.875f,1f})
 {
  slider.SetValueWithoutNotify(fraction);UnityEngine.Canvas.ForceUpdateCanvases();
  var graphic=slider.fillRect.GetComponent<CatchAR.TimerFillGraphic>();
  if(graphic.type!=UnityEngine.UI.Image.Type.Filled||System.Math.Abs(graphic.fillAmount-fraction)>.00001)throw new System.Exception("Slider fill not connected");
  var vh=new UnityEngine.UI.VertexHelper();
  typeof(CatchAR.TimerFillGraphic).GetMethod("OnPopulateMesh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly).Invoke(graphic,new object[]{vh});
  var mesh=new UnityEngine.Mesh();vh.FillMesh(mesh);var vertices=mesh.vertices;var tris=mesh.triangles;double area=0;
  for(int i=0;i<tris.Length;i+=3)area+=UnityEngine.Vector3.Cross(vertices[tris[i+1]]-vertices[tris[i]],vertices[tris[i+2]]-vertices[tris[i]]).magnitude*.5;
  var r=graphic.rectTransform.rect;double w=r.width,ht=r.height,c=System.Math.Min(ht*.35,w*.5),d=w*fraction;
  double expected=d<=c?ht*d*d/(2*c):d>=w-c?w*ht-c*ht-ht*(w-d)*(w-d)/(2*c):ht*(d-c/2);
  if(System.Math.Abs(area-expected)>System.Math.Max(.02,expected*.0001))throw new System.Exception("Fill hole or overlap: "+area+" != "+expected);
  foreach(var v in vertices)if(v.x>r.xMin+d+.002)throw new System.Exception("Fill exceeds value");
  UnityEngine.Object.Destroy(mesh);vh.Dispose();
 }
 slider.SetValueWithoutNotify(.5f);
}
h.playHud.SetActive(false);
return "PASS: both sliders clip a solid polygon at 0%, 0.1%, 12.5%, 50%, 87.5%, 100%; mesh area matches the filled shape without missing triangles or overlaps.";
