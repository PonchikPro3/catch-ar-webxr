if(!UnityEngine.Application.isPlaying)throw new System.Exception("Play Mode required");
var g=UnityEngine.Object.FindFirstObjectByType<CatchAR.CatchGame>();g.SelectRoomScanMode();
var planes=new System.Collections.Generic.List<CatchAR.ScanPlane>();
for(int i=0;i<8;i++){float x=(i%4-1.5f)*.8f,z=1.3f+(i/4)*1.2f,y=.14f+(i%2)*.12f;planes.Add(new CatchAR.ScanPlane{id=i+1,normal=UnityEngine.Vector3.up,points=new[]{new UnityEngine.Vector3(x,y,z),new UnityEngine.Vector3(x+.65f,y,z),new UnityEngine.Vector3(x+.65f,y,z+.65f),new UnityEngine.Vector3(x,y,z+.65f)}});}
g.scanner.ReceiveScanFrame(UnityEngine.JsonUtility.ToJson(new CatchAR.ScanPacket{backend="planes",tracking=true,planes=planes.ToArray()}));
g.SendMessage("Update");if(g.Phase!=CatchAR.GamePhase.Countdown)throw new System.Exception("Scan did not automatically start the round");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
typeof(CatchAR.CatchGame).GetField("phaseTime",flags).SetValue(g,3.1f);g.SendMessage("Update");
if(g.Phase!=CatchAR.GamePhase.Playing||g.ActiveTargets!=3)throw new System.Exception("Round must start with three targets");
foreach(var t in g.Targets){typeof(CatchAR.CatchTarget).GetField("age",flags).SetValue(t,1f);t.SendMessage("Update");}
g.hud.Refresh(0);UnityEngine.Canvas.ForceUpdateCanvases();UnityEditor.EditorApplication.isPaused=true;
return "Synthetic scan starts surface game automatically with three targets. Not a phone scan.";
