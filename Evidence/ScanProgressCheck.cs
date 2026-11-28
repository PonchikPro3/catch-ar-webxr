if(!UnityEngine.Application.isPlaying)throw new System.Exception("Play Mode required");
var g=UnityEngine.Object.FindFirstObjectByType<CatchAR.CatchGame>();g.SelectRoomScanMode();var s=g.scanner;
CatchAR.ScanPlane Plane(int id)=>new CatchAR.ScanPlane{id=id,normal=UnityEngine.Vector3.up,points=new[]{new UnityEngine.Vector3(id*.4f,0,1),new UnityEngine.Vector3(id*.4f+.3f,0,1),new UnityEngine.Vector3(id*.4f+.3f,0,1.3f),new UnityEngine.Vector3(id*.4f,0,1.3f)}};
void Planes(int n)=>s.ReceiveScanFrame(UnityEngine.JsonUtility.ToJson(new CatchAR.ScanPacket{backend="planes",tracking=true,planes=System.Linq.Enumerable.Range(1,n).Select(Plane).ToArray()}));
Planes(7);g.hud.Refresh(0);if(s.IsComplete||s.IsPaused||g.hud.scanProgress.value!=.875f)throw new System.Exception("Seven planes must remain scanning at 7/8");
Planes(8);g.hud.Refresh(0);if(!s.IsComplete||!s.IsPaused||g.hud.scanProgress.value!=1||g.hud.scanPauseButton.interactable)throw new System.Exception("Eight planes must complete and freeze");
Planes(1);if(s.SurfaceCount!=8)throw new System.Exception("Completed scan changed");
s.ResetScan();g.hud.Refresh(0);if(s.IsComplete||s.IsPaused||s.Progress!=0||s.Count!=0||g.hud.scanProgress.value!=0)throw new System.Exception("Reset failed");
void Hit(int x,int z){for(int i=0;i<3;i++)s.ReceiveScanFrame(UnityEngine.JsonUtility.ToJson(new CatchAR.ScanPacket{backend="hits",tracking=true,hits=new[]{new CatchAR.ScanHit{position=new UnityEngine.Vector3(x*.05f+.025f,.14f,z*.05f+.025f),normal=UnityEngine.Vector3.up}}}));}
for(int x=0;x<8;x++)Hit(x,0);if(s.SurfaceCount!=1||s.IsComplete)throw new System.Exception("Adjacent floor cells counted as eight surfaces");
s.ResetScan();for(int x=0;x<8;x++)Hit(x*4,0);if(!s.IsComplete||s.SurfaceCount!=8)throw new System.Exception("Eight separate patches not completed");
s.ResetScan();Planes(4);g.hud.Refresh(0);g.hud.scanSource.text="СИНТЕТИЧЕСКИЙ ТЕСТ · НЕ СКАН КОМНАТЫ";UnityEngine.Canvas.ForceUpdateCanvases();
if(g.hud.scanProgress.GetComponent<UnityEngine.RectTransform>().rect.width<=0)throw new System.Exception("Progress layout has zero width");
UnityEditor.EditorApplication.isPaused=true;return "PASS: 7/8, automatic completion at 8, frozen snapshot, reset to zero, adjacent hit cells grouped, eight separate patches complete. Synthetic data only.";
