using Courtyard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// The clerk's hand: appears at the pointer while a tool is pressed or dragged, hidden otherwise.
    /// Swap <see cref="openHand"/> / <see cref="grabHand"/> sprites for the hand-drawn art.
    /// </summary>
    public sealed class HandsView : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SpriteRenderer hand;
        [SerializeField] private Sprite openHand;
        [SerializeField] private Sprite grabHand;
        [Tooltip("Offset from the pointer, so the fingertips sit on the tool.")]
        [SerializeField] private Vector2 offset = new(0.2f, -0.35f);
        [SerializeField, Min(0.01f)] private float followSmoothing = 18f;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 10f;

        private SpriteRenderer[] _renderers;
        private int _holds;
        private float _alpha;

        private void OnEnable()
        {
            PointerTarget.AnyPressed += OnGrab;
            PointerTarget.AnyReleased += OnRelease;
            DragTarget.AnyDragStarted += OnGrab;
            DragTarget.AnyDragEnded += OnRelease;
        }

        private void OnDisable()
        {
            PointerTarget.AnyPressed -= OnGrab;
            PointerTarget.AnyReleased -= OnRelease;
            DragTarget.AnyDragStarted -= OnGrab;
            DragTarget.AnyDragEnded -= OnRelease;
        }

        private void Start()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            SetAlpha(0f);
        }

        private void OnGrab(Transform _)
        {
            if (_holds++ == 0) SnapToPointer();
        }

        private void OnRelease(Transform _) => _holds = Mathf.Max(0, _holds - 1);

        private void Update()
        {
            var holding = _holds > 0;
            if (grabHand != null && openHand != null) hand.sprite = holding ? grabHand : openHand;

            var target = PointerWorld() + offset;
            transform.position = Vector3.Lerp(transform.position, new Vector3(target.x, target.y, transform.position.z),
                1f - Mathf.Exp(-followSmoothing * Time.deltaTime));

            _alpha = Mathf.MoveTowards(_alpha, holding ? 1f : 0f, fadeSpeed * Time.deltaTime);
            SetAlpha(_alpha);
        }

        private void SnapToPointer()
        {
            var target = PointerWorld() + offset;
            transform.position = new Vector3(target.x, target.y, transform.position.z);
        }

        private Vector2 PointerWorld()
        {
            var screen = Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
            return worldCamera.ScreenToWorldPoint(screen);
        }

        /// <summary>Fades the hand and anything drawn with it (sleeve, etc.).</summary>
        private void SetAlpha(float alpha)
        {
            foreach (var renderer in _renderers)
            {
                var colour = renderer.color;
                renderer.color = new Color(colour.r, colour.g, colour.b, alpha);
            }
        }
    }
}
