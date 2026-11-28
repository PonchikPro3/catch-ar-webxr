var g=UnityEngine.Object.FindFirstObjectByType<CatchAR.CatchGame>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
for(int i=0;i<100;i++)
{
 if(!g.scanner.TryGetRandomSurfacePoint(out var p,out var n))throw new System.Exception("No sampled surface point");
 bool supported=false;for(int j=0;j<8;j++){float x=(j%4-1.5f)*.8f,z=1.3f+(j/4)*1.2f,y=.14f+(j%2)*.12f;if(p.x>=x-.001f&&p.x<=x+.651f&&p.z>=z-.001f&&p.z<=z+.651f&&UnityEngine.Mathf.Abs(p.y-y)<.001f)supported=true;}
 if(!supported)throw new System.Exception("Spawn sample outside scanned geometry");
}
for(int i=0;i<10;i++)
{
 typeof(CatchAR.CatchGame).GetField("nextSpawn",flags).SetValue(g,0f);g.SendMessage("Update");
 if(g.ActiveTargets<1||g.ActiveTargets>3||g.Caught+g.ActiveTargets>10)throw new System.Exception("Target budget violated");
 foreach(var t in g.Targets){if(!t.IsSurfaceTarget)throw new System.Exception("Falling target in surface mode");typeof(CatchAR.CatchTarget).GetField("age",flags).SetValue(t,1f);t.SendMessage("Update");}
 var target=g.Targets[0];var before=target.SurfacePoint;target.KeepClearOfPlayer(g.ViewCamera.transform,10);if(target.SurfacePoint!=before)throw new System.Exception("Surface anchor moved");
 var bounds=CatchAR.TargetPicking.VisibleBounds(target);var origin=g.ViewCamera.transform.position;
 typeof(CatchAR.CatchGame).GetField("lastActionFrame",flags).SetValue(g,-1);
 g.Act(new UnityEngine.Ray(origin,bounds.center-origin));
 if(g.Caught!=i+1)throw new System.Exception("Tap/ray failed at catch "+i);
 if(i<9&&g.Phase!=CatchAR.GamePhase.Playing)throw new System.Exception("Round ended early");
}
if(g.Phase!=CatchAR.GamePhase.Result||g.ActiveTargets!=0||g.Caught!=10||g.Score!=10)throw new System.Exception("Result after ten catches failed");
float elapsed=g.Elapsed;g.SendMessage("Update");if(g.Elapsed!=elapsed)throw new System.Exception("Result stopwatch still running");
g.hud.Refresh(0);if(g.hud.resultScore.text!=CatchAR.CatchHud.FormatElapsed(elapsed))throw new System.Exception("Result time not displayed");
return "PASS: automatic start, 100 spawn samples inside scanned polygons, <=3 active targets, stationary surface targets, ray picking, replenishment, ten catches -> result, frozen elapsed time. Synthetic data only.";
