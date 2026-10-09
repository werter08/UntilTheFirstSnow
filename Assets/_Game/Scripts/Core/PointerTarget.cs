using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Courtyard.Core
{
    /// <summary>
    /// Click / press-and-hold target for world sprites. Needs a Collider2D and a Physics2DRaycaster on the camera.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class PointerTarget : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] private bool interactable = true;

        private Collider2D _collider;

        public event Action Pressed;
        public event Action Released;
        public event Action Clicked;

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

        public bool IsHeld { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable || eventData.button != PointerEventData.InputButton.Left) return;
            IsHeld = true;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!IsHeld) return;
            IsHeld = false;
            Released?.Invoke();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable || eventData.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke();
        }

        private void OnDisable()
        {
            if (!IsHeld) return;
            IsHeld = false;
            Released?.Invoke();
        }
    }
}
