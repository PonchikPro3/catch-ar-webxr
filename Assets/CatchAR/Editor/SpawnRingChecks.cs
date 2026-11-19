using System;
using System.IO;
using UnityEngine;

namespace CatchAR.Editor
{
    public static class SpawnRingChecks
    {
        public static string Geometry()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<CatchGame>();
            if (game == null) throw new Exception("Open the CatchAR scene first");
            var players = new[] { Vector3.zero, new Vector3(0, 1.6f, 0), new Vector3(-7, -.8f, 11) };
            int samples = 0;
            foreach (var player in players)
            {
                for (int yaw = -180; yaw < 180; yaw += 5)
                {
                    Vector3 point = game.GetSpawnPosition(player, yaw);
                    if (Mathf.Abs(Vector3.Distance(point, player) - 2.5f) > .0001f)
                        throw new Exception("Unequal spawn distance");
                    if (Mathf.Abs(point.y - player.y - .5f) > .0001f)
                        throw new Exception("Spawn height is not relative to the camera");
                    if (Vector3.ProjectOnPlane(point - player, Vector3.up).magnitude < 2.4f)
                        throw new Exception("Target spawned overhead");
                    samples++;
                }
            }
            const string result = "Passed: 216 spawn points at exactly 2.5 m; fixed 0.5 m eye-relative height; no overhead positions; translated and elevated player origins";
            Directory.CreateDirectory("Evidence");
            File.WriteAllText("Evidence/spawn-ring-check.txt", result);
            return result;
        }

        public static string Runtime()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<CatchGame>();
            if (game == null || game.Targets.Count != 1) throw new Exception("Start a round with maxTargets=1");
            var target = game.Targets[0];
            var camera = game.ViewCamera;
            float radius = Vector3.ProjectOnPlane(target.transform.position - camera.transform.position, Vector3.up).magnitude;
            float expectedRadius = Mathf.Sqrt(2.5f * 2.5f - .5f * .5f);
            if (Mathf.Abs(radius - expectedRadius) > .001f) throw new Exception("Actual spawned target is outside the fixed ring");
            Vector3 view = camera.WorldToViewportPoint(target.transform.position);
            if (view.z <= 0 || view.x <= .03f || view.x >= .97f || view.y <= 0 || view.y >= .8f)
                throw new Exception("Level-camera spawn is outside the visible play area");
            const string result = "Runtime passed: actual target on the fixed ring; visible in front of a level camera below the top HUD";
            File.AppendAllText("Evidence/spawn-ring-check.txt", "\n" + result);
            return result;
        }
    }
}
