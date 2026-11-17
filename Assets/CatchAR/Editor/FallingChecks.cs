using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatchAR.Editor
{
    // A short Play Mode integration check against the actual scene and physics raycasts.
    public static class FallingChecks
    {
        static CatchGame game;
        static CatchTarget target;
        static Vector3 initial;
        static double started, sampled;
        static int stage;
        public static string Result { get; private set; } = "Not run";

        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first");
            game = UnityEngine.Object.FindFirstObjectByType<CatchGame>();
            game.maxTargets = 1;
            game.StartDemo();
            stage = 0;
            started = EditorApplication.timeSinceStartup;
            Result = "Running";
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 20) throw new Exception("Falling checks timed out");
                if (!EditorApplication.isPlaying) throw new Exception("Play Mode ended during checks");
                if (game.Phase != GamePhase.Playing) return;
                if (stage == 0)
                {
                    target = UnityEngine.Object.FindFirstObjectByType<CatchTarget>();
                    if (target == null) return;
                    if (target.transform.position.y < game.ViewCamera.transform.position.y + .8f)
                        throw new Exception("Target was not spawned above the player");
                    initial = target.transform.position;
                    sampled = EditorApplication.timeSinceStartup;
                    stage = 1;
                }
                else if (stage == 1 && EditorApplication.timeSinceStartup - sampled >= .3)
                {
                    if (target == null || target.transform.position.y >= initial.y - .01f)
                        throw new Exception("Target did not fall down");
                    if (Mathf.Abs(target.transform.position.x - initial.x) > .001f || Mathf.Abs(target.transform.position.z - initial.z) > .001f)
                        throw new Exception("Target drifted horizontally");
                    // Move the falling target to a clear, visible point and exercise real raycast selection.
                    var camera = game.ViewCamera.transform;
                    target.transform.position = camera.position + camera.forward * 2;
                    Physics.SyncTransforms();
                    game.Act(new Ray(camera.position, target.transform.position - camera.position));
                    if (game.Caught != 1 || game.Score != 10) throw new Exception("Click/raycast did not award one catch");
                    target.Catch();
                    if (game.Score != 10) throw new Exception("One target awarded points twice");
                    stage = 2;
                }
                else if (stage == 2)
                {
                    target = UnityEngine.Object.FindFirstObjectByType<CatchTarget>();
                    if (target == null || target.Resolved) return;
                    var position = target.transform.position;
                    position.y = game.GroundHeight + .151f;
                    target.transform.position = position;
                    stage = 3;
                }
                else if (stage == 3)
                {
                    if (target != null) return;
                    if (game.Missed != 1 || game.Score != 10 || game.Caught != 1)
                        throw new Exception("Ground contact did not remove the target and count exactly one miss");
                    game.maxTargets = 5;
                    game.Restart();
                    if (game.Score != 0 || game.Caught != 0 || game.Missed != 0 || game.Remaining != 60)
                        throw new Exception("Restart did not reset the round");
                    Result = "Passed: spawn above player; vertical accelerated fall; raycast catch; no duplicate score; removal at ground; miss count; restart";
                    Directory.CreateDirectory("Evidence");
                    File.WriteAllText("Evidence/falling-check.txt", Result);
                    Debug.Log(Result);
                    EditorApplication.update -= Tick;
                }
            }
            catch (Exception error)
            {
                Result = "FAILED: " + error.Message;
                Debug.LogError(Result);
                EditorApplication.update -= Tick;
            }
        }
    }
}
