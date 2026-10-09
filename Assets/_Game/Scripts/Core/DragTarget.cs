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
    public sealed class DragTarget : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private bool interactable = true;

        public event Action<Vector2> DragStarted;
        public event Action<Vector2> Dragged;
        public event Action<Vector2> DragEnded;

        public bool Interactable
        {
            get => interactable;
            set => interactable = value;
        }

        public bool IsDragging { get; private set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!interactable || eventData.button != PointerEventData.InputButton.Left) return;
            IsDragging = true;
            DragStarted?.Invoke(ToWorld(eventData));
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
        }

        private static Vector2 ToWorld(PointerEventData eventData)
        {
            var cam = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
            return cam.ScreenToWorldPoint(eventData.position);
        }
    }
}
