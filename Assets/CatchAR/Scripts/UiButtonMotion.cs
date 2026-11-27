using UnityEngine;
using UnityEngine.EventSystems;
namespace CatchAR
{
    public sealed class UiButtonMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        bool pressed, highlighted;
        float appeared;
        void OnEnable() { appeared = Time.unscaledTime; pressed = highlighted = false; }
        void Update()
        {
            float enter = Mathf.Clamp01((Time.unscaledTime - appeared) / .32f);
            float pop = 1 + Mathf.Sin(enter * Mathf.PI) * .06f;
            Vector3 target = pressed ? new Vector3(.95f, .91f, 1) : Vector3.one * (highlighted ? 1.035f : pop);
            transform.localScale = Vector3.Lerp(transform.localScale, target, 1 - Mathf.Exp(-22 * Time.unscaledDeltaTime));
        }
        public void OnPointerDown(PointerEventData e) { pressed = true; }
        public void OnPointerUp(PointerEventData e) { pressed = false; }
        public void OnPointerEnter(PointerEventData e) { highlighted = true; }
        public void OnPointerExit(PointerEventData e) { highlighted = pressed = false; }
        public void OnSelect(BaseEventData e) { highlighted = true; }
        public void OnDeselect(BaseEventData e) { highlighted = pressed = false; }
    }
}
