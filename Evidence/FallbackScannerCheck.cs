if(!UnityEngine.Application.isPlaying)throw new System.Exception("Play Mode required");
var g=UnityEngine.Object.FindFirstObjectByType<CatchAR.CatchGame>();g.SelectRoomScanMode();var s=g.scanner;
void Cell(int x,int z){for(int i=0;i<3;i++)s.ReceiveScanFrame(UnityEngine.JsonUtility.ToJson(new CatchAR.ScanPacket{backend="hit-test",tracking=true,hits=new[]{new CatchAR.ScanHit{ray=0,verified=true,fullCell=true,position=new UnityEngine.Vector3(x*.05f+.025f,.14f,z*.05f+.025f),normal=UnityEngine.Vector3.up}}}));}
Cell(0,0);if(s.Count!=0||s.CandidateCount!=1)throw new System.Exception("Minimum area regression");
for(int x=0;x<4;x++)for(int z=0;z<4;z++)Cell(x,z);
if(s.Count!=16||!s.PrecisionAvailable)throw new System.Exception("Fallback grid hidden");
s.TogglePause();if(!s.IsPaused)throw new System.Exception("Fallback cannot freeze");
s.End();return "PASS: stable plane hit-test samples produce 16 visible cells without depth/mesh; isolated cell hidden; freeze works. Synthetic data only.";
