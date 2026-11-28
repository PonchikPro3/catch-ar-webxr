var g=UnityEngine.Object.FindFirstObjectByType<CatchAR.CatchGame>();
if(g.Phase!=CatchAR.GamePhase.Result)throw new System.Exception("Complete SurfaceGameCheck first");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
g.Restart();if(g.Phase!=CatchAR.GamePhase.Countdown||g.Elapsed!=0||g.Caught!=0||!g.scanner.IsComplete)throw new System.Exception("Replay lost cached scan or did not reset stopwatch");
typeof(CatchAR.CatchGame).GetField("phaseTime",flags).SetValue(g,3.1f);g.SendMessage("Update");if(g.ActiveTargets!=3)throw new System.Exception("Replay did not spawn");
g.SelectFallingMode();typeof(CatchAR.CatchGame).GetField("phaseTime",flags).SetValue(g,3.1f);g.SendMessage("Update");g.hud.Refresh(0);
if(g.IsSurfaceMode||g.Phase!=CatchAR.GamePhase.Playing||g.ActiveTargets!=1||g.Targets[0].IsSurfaceTarget||g.hud.elapsedText.gameObject.activeSelf||g.Remaining<0||g.Remaining>g.roundDuration)throw new System.Exception("Falling-mode regression");
g.ReturnToMenu();return "PASS: replay reuses frozen scan, resets caught/time, spawns three; falling mode keeps countdown timer and falling targets; menu clears all targets.";
