using System;
using System.IO;
using UnityEngine;

namespace CatchAR.Editor
{
    public static class GuidanceChecks
    {
        public static string Projection()
        {
            var holder = new GameObject("Temporary projection check") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = holder.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = 60;
                camera.pixelRect = new Rect(0, 0, 1280, 800);
                camera.aspect = 1.6f;
                var area = new Rect(-500, -350, 1000, 700);
                Check(camera, area, new Vector3(0, 0, 3), false, Vector2.zero, "visible");
                Check(camera, area, new Vector3(5, 0, 2), true, Vector2.right, "right");
                Check(camera, area, new Vector3(-5, 0, 2), true, Vector2.left, "left");
                Check(camera, area, new Vector3(0, 5, 2), true, Vector2.up, "above");
                Check(camera, area, new Vector3(0, -5, 2), true, Vector2.down, "below");
                Check(camera, area, new Vector3(1, 0, -3), true, Vector2.right, "behind right");
                Check(camera, area, new Vector3(-1, 0, -3), true, Vector2.left, "behind left");
                Check(camera, area, new Vector3(0, 0, -3), true, Vector2.right, "directly behind");
                camera.transform.rotation = Quaternion.Euler(0, 90, 0);
                Check(camera, area, new Vector3(0, 0, 5), true, Vector2.left, "rotated camera");
                Check(camera, area, new Vector3(5, 0, 0), false, Vector2.zero, "turned toward target");
                camera.transform.rotation = Quaternion.identity;
                camera.aspect = 390f / 754;
                area = new Rect(-180, -340, 360, 680);
                Check(camera, area, new Vector3(1, 0, 2), true, Vector2.right, "portrait");
                Directory.CreateDirectory("Evidence");
                const string passed = "Passed: hidden for visible targets; left/right/up/down; rear hemisphere; exact rear; camera rotation; portrait screen; edge bounds; finite coordinates";
                File.WriteAllText("Evidence/guidance-check.txt", passed);
                return passed;
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }

        static void Check(Camera camera, Rect area, Vector3 target, bool expected, Vector2 direction, string label)
        {
            bool shown = OffscreenIndicators.TryGetEdgePosition(camera, target, area, 30, out Vector2 point, out float angle);
            if (shown != expected) throw new Exception(label + ": incorrect visibility");
            if (!shown) return;
            if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y))
                throw new Exception(label + ": invalid coordinates");
            if (point.x < area.xMin + 29.9f || point.x > area.xMax - 29.9f || point.y < area.yMin + 29.9f || point.y > area.yMax - 29.9f)
                throw new Exception(label + ": outside safe bounds");
            Vector2 heading = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            if (Vector2.Dot(heading, direction) < .9f) throw new Exception(label + ": incorrect turn direction");
        }

        public static string Runtime()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<CatchGame>();
            var indicators = UnityEngine.Object.FindFirstObjectByType<OffscreenIndicators>();
            if (game == null || indicators == null || game.Targets.Count == 0 || game.Phase != GamePhase.Playing)
                throw new Exception("Start a round and wait for a target");
            Canvas.ForceUpdateCanvases();
            var camera = game.ViewCamera;
            var originalRotation = camera.transform.rotation;
            var target = game.Targets[0];
            for (int i = 0; i < game.Targets.Count; i++)
                game.Targets[i].transform.position = camera.transform.position + camera.transform.forward * 3;
            target.transform.position = camera.transform.position + camera.transform.right * 6 + camera.transform.forward * 2;
            indicators.RefreshIndicators();
            if (indicators.VisibleCount != 1 || indicators.markers[0].rectTransform.anchoredPosition.x <= 0)
                throw new Exception("Offscreen target did not show a right-edge marker");
            if (indicators.markers[0].raycastTarget) throw new Exception("Marker blocks touch input");
            camera.transform.rotation = Quaternion.LookRotation(target.transform.position - camera.transform.position);
            // Test the chosen target independently of the other targets after turning.
            bool stillOffscreen = OffscreenIndicators.TryGetEdgePosition(camera, target.transform.position, indicators.bounds.rect,
                indicators.edgeInset, out _, out _);
            if (stillOffscreen) throw new Exception("Turned-toward target still has a marker");
            camera.transform.rotation = originalRotation;
            target.Catch();
            indicators.RefreshIndicators();
            if (indicators.VisibleCount != 0) throw new Exception("Caught target left a stale marker");
            const string passed = "Runtime passed: right-edge marker; no touch blocking; hidden after turning; no stale marker after catch";
            File.AppendAllText("Evidence/guidance-check.txt", "\n" + passed);
            return passed;
        }
    }
}
