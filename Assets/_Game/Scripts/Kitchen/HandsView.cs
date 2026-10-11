using System.Collections.Generic;
using Courtyard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// The clerk's hand. Shows an open hand while hovering a usable tool, the tool's pose while it is pressed or
    /// dragged, and can hold a transform for scripted moments (raising the mug). Hidden otherwise.
    /// Each sprite's pivot is its fingertip / grip point, so the hand lands exactly on the pointer.
    /// </summary>
    public sealed class HandsView : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private SpriteRenderer hand;
        [SerializeField] private HandPoseSprites[] poses;
        [SerializeField] private HandPose hoverPose = HandPose.Open;
        [SerializeField, Range(0f, 1f)] private float hoverAlpha = 0.85f;
        [SerializeField, Min(0.01f)] private float followSmoothing = 18f;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 10f;

        private readonly HashSet<Transform> _hovered = new();
        private SpriteRenderer[] _renderers;
        private Sweater _sweater;
        private HandPose _heldPose;
        private int _holds;
        private Transform _attached;
        private float _alpha;

        public void SetSweater(Sweater sweater) => _sweater = sweater;

        /// <summary>Keeps the hand on <paramref name="target"/> with <paramref name="pose"/> until <see cref="Detach"/>.</summary>
        public void Attach(Transform target, HandPose pose)
        {
            _attached = target;
            _heldPose = pose;
            SetPose(pose);
            Snap(target.position);
        }

        public void Detach() => _attached = null;

        private int _rightHandUsers;

        /// <summary>
        /// The drawn hand mirrored: his right hand, reaching in from the lower right (the milk is on the right). Counted,
        /// so back-to-back steps (steam, then pour) never flip it back in between.
        /// </summary>
        public void UseRightHand(bool right)
        {
            _rightHandUsers = Mathf.Max(0, _rightHandUsers + (right ? 1 : -1));
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (_rightHandUsers > 0 ? -1f : 1f);
            transform.localScale = scale;
        }

        /// <summary>
        /// A second hand that holds something still (e.g. the grinder over the cup while the player's hand turns the
        /// crank): a copy of the hand in <paramref name="pose"/>, parented to <paramref name="holder"/> at
        /// <paramref name="worldPosition"/>. Destroy the returned object to let go.
        /// </summary>
        public GameObject HoldStill(Transform holder, Vector3 worldPosition, HandPose pose)
        {
            var copy = Instantiate(gameObject, holder);
            copy.name = "HoldingHand";
            Destroy(copy.GetComponent<HandsView>());
            copy.transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            copy.transform.rotation = Quaternion.identity;
            var copiedHand = copy.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var entry in poses)
            {
                if (entry.pose != pose) continue;
                var sprite = entry.For(_sweater);
                foreach (var r in copiedHand)
                    if (r.name == hand.name && sprite != null) r.sprite = sprite;
            }

            foreach (var r in copiedHand)
            {
                var c = r.color;
                r.color = new Color(c.r, c.g, c.b, 1f);
            }

            return copy;
        }

        private void OnEnable()
        {
            PointerTarget.AnyPressed += OnGrab;
            PointerTarget.AnyReleased += OnRelease;
            PointerTarget.AnyHoverStarted += OnHoverStarted;
            PointerTarget.AnyHoverEnded += OnHoverEnded;
            DragTarget.AnyDragStarted += OnGrab;
            DragTarget.AnyDragEnded += OnRelease;
        }

        private void OnDisable()
        {
            PointerTarget.AnyPressed -= OnGrab;
            PointerTarget.AnyReleased -= OnRelease;
            PointerTarget.AnyHoverStarted -= OnHoverStarted;
            PointerTarget.AnyHoverEnded -= OnHoverEnded;
            DragTarget.AnyDragStarted -= OnGrab;
            DragTarget.AnyDragEnded -= OnRelease;
        }

        private void Start()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            SetAlpha(0f);
        }

        private void OnGrab(Transform _, HandPose pose)
        {
            _heldPose = pose;
            if (_holds++ == 0) Snap(PointerWorld());
        }

        private void OnRelease(Transform _) => _holds = Mathf.Max(0, _holds - 1);
        private void OnHoverStarted(Transform target) => _hovered.Add(target);
        private void OnHoverEnded(Transform target) => _hovered.Remove(target);

        private void Update()
        {
            _hovered.RemoveWhere(t => t == null);
            var holding = _holds > 0 || _attached != null;
            var hovering = !holding && _hovered.Count > 0;

            if (holding) SetPose(_heldPose);
            else if (hovering) SetPose(hoverPose);

            var target = _attached != null ? (Vector2)_attached.position : PointerWorld();
            transform.position = Vector3.Lerp(transform.position, new Vector3(target.x, target.y, transform.position.z),
                1f - Mathf.Exp(-followSmoothing * Time.deltaTime));

            var wanted = holding ? (_heldPose == HandPose.Hidden ? 0f : 1f) : hovering ? hoverAlpha : 0f;
            _alpha = Mathf.MoveTowards(_alpha, wanted, fadeSpeed * Time.deltaTime);
            SetAlpha(_alpha);
        }

        private void SetPose(HandPose pose)
        {
            foreach (var entry in poses)
            {
                if (entry.pose != pose) continue;
                var sprite = entry.For(_sweater);
                if (sprite != null) hand.sprite = sprite;
                return;
            }
        }

        private void Snap(Vector2 target) => transform.position = new Vector3(target.x, target.y, transform.position.z);

        private Vector2 PointerWorld()
        {
            var screen = Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
            return worldCamera.ScreenToWorldPoint(screen);
        }

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
