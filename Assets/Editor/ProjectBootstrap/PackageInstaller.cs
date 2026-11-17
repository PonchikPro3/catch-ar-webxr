using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace CatchAR.Editor
{
    public static class PackageInstaller
    {
        static AddAndRemoveRequest request;
        static double deadline;

        public static void Install()
        {
            request = Client.AddAndRemove(new[] { "file:../Vendor/WebXR", "com.unity.xr.management" },
                new[] { "com.unity.ai.navigation", "com.unity.collab-proxy", "com.unity.multiplayer.center",
                    "com.unity.visualscripting", "com.unity.timeline", "com.unity.ide.rider", "com.unity.ide.visualstudio" });
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            if (request.IsCompleted && request.Status == StatusCode.Success)
            {
                Debug.Log("CATCHAR_PACKAGES_OK");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError(request.Error?.message ?? "Package installation timeout");
                EditorApplication.Exit(1);
            }
        }
    }
}
