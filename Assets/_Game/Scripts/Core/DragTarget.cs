using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Courtyard.Core
{
    /// <summary>
    /// Drag target for world sprites. Reports the pointer position in world space (z = 0).
    /// Needs a Collider2D and a Physics2DRaycaster on the camera.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class DragTarget : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private bool interactable = true;
        [Tooltip("Hand drawing shown while this target is dragged.")]
        [SerializeField] private HandPose handPose = HandPose.Pinch;

        private Collider2D _collider;

        /// <summary>Raised for any target when a drag starts / ends (used by the hands view).</summary>
        public static event Action<Transform, HandPose> AnyDragStarted;
        public static event Action<Transform> AnyDragEnded;

        public event Action<Vector2> DragStarted;
        public event Action<Vector2> Dragged;
        public event Action<Vector2> DragEnded;

        /// <summary>Inactive targets also disable their collider, so they never block clicks meant for something behind them.</summary>
        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                if (_collider == null) _collider = GetComponent<Collider2D>();
                _collider.enabled = value;
            }
        }

        public bool IsDragging { get; private set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!interactable || eventData.button != PointerEventData.InputButton.Left) return;
            IsDragging = true;
            DragStarted?.Invoke(ToWorld(eventData));
            AnyDragStarted?.Invoke(transform, handPose);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (IsDragging) Dragged?.Invoke(ToWorld(eventData));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;
            IsDragging = false;
            DragEnded?.Invoke(ToWorld(eventData));
            AnyDragEnded?.Invoke(transform);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (interactable) PointerTarget.RaiseHoverStarted(transform);
        }

        public void OnPointerExit(PointerEventData eventData) => PointerTarget.RaiseHoverEnded(transform);

        private static Vector2 ToWorld(PointerEventData eventData)
        {
            var cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
            return cam.ScreenToWorldPoint(eventData.position);
        }
    }
}
