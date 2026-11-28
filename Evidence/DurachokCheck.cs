if(!UnityEngine.Application.isPlaying)throw new System.Exception("Play Mode required");
var g=UnityEngine.Object.FindFirstObjectByType<CatchAR.CatchGame>();
var colors=new System.Collections.Generic.HashSet<UnityEngine.Material>();var spins=new System.Collections.Generic.HashSet<UnityEngine.Vector3>();UnityEngine.Material previous=null;
for(int i=0;i<24;i++)
{
 var t=UnityEngine.Object.Instantiate(g.targetPrefab);t.gameObject.SetActive(false);t.Initialize(g,false,-1000,.28f,.14f);
 if(t.visualRenderers.Length!=4||t.core.Find("Durachok Model")==null)throw new System.Exception("FBX not connected");
 if(t.AngularVelocity.magnitude<34.99f||t.AngularVelocity.magnitude>160.01f)throw new System.Exception("Invalid spin speed");
 if(!System.Linq.Enumerable.Contains(t.bodyMaterials,t.coreRenderer.sharedMaterial)||t.coreRenderer.sharedMaterial==previous)throw new System.Exception("Invalid or repeated adjacent palette color");
 previous=t.coreRenderer.sharedMaterial;colors.Add(previous);spins.Add(t.AngularVelocity);UnityEngine.Object.Destroy(t.gameObject);
}
if(colors.Count!=4||spins.Count!=24)throw new System.Exception("Missing color/rotation variety");
foreach(var t in g.Targets)
{
 t.FacePlayer(g.ViewCamera.transform);var n=UnityEngine.Vector3.up;
 var toward=UnityEngine.Vector3.ProjectOnPlane(g.ViewCamera.transform.position-t.SurfacePoint,n).normalized;
 if(UnityEngine.Vector3.Dot(t.core.forward,toward)<.999f)throw new System.Exception("Model is not facing player");
 var anchor=t.SurfacePoint;var camera=g.ViewCamera.transform;var saved=camera.position;camera.position+=UnityEngine.Vector3.right;
 t.KeepClearOfPlayer(camera,2.5f);toward=UnityEngine.Vector3.ProjectOnPlane(camera.position-anchor,n).normalized;
 if(UnityEngine.Vector3.Dot(t.core.forward,toward)<.999f||t.SurfacePoint!=anchor)throw new System.Exception("Facing did not follow moving player");
 camera.position=saved;t.FacePlayer(camera);
 if(t.VisualBounds.size.magnitude<.1f||t.VisualBounds.size.magnitude>1)throw new System.Exception("Model size invalid");
}
return "PASS: FBX with four renderers; four palette materials across 24 targets; distinct random spin vectors at 35-160 degrees/s; surface models face the moving player and keep their anchors. Synthetic data only.";
