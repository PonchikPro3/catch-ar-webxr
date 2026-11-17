using UnityEngine;
namespace CatchAR
{
    public sealed class SafeArea : MonoBehaviour
    {
        Rect previous;
        void Update()
        {
            Rect area = Screen.safeArea;
            if (area == previous || Screen.width == 0 || Screen.height == 0) return;
            previous = area;
            var rect = (RectTransform)transform;
            rect.anchorMin = area.position / new Vector2(Screen.width, Screen.height);
            rect.anchorMax = (area.position + area.size) / new Vector2(Screen.width, Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
