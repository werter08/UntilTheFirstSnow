using System.Collections;
using System.Collections.Generic;
using Courtyard.Audio;
using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// The safety net under every player action: anything you hover ticks softly, anything you click answers with a
    /// sound, anything you pick up lifts a little and is set down with a sound. Fallback sounds only play when the
    /// action didn't make its own (checked via <see cref="AudioManager.LastSfxFrame"/>), so nothing doubles up.
    /// </summary>
    public sealed class InteractionFeedback : MonoBehaviour
    {
        [SerializeField] private FeedbackFx fx;
        [Tooltip("Scale while something is carried.")]
        [SerializeField, Min(1f)] private float liftScale = 1.06f;
        [SerializeField, Min(0.01f)] private float liftSeconds = 0.08f;
        [Tooltip("Shortest gap between two hover ticks.")]
        [SerializeField, Min(0f)] private float hoverGap = 0.15f;
        [SerializeField] private Color clickDust = new(1f, 0.96f, 0.85f, 0.7f);

        private readonly Dictionary<Transform, Vector3> _lifted = new();
        private int _pressFrame = -1;
        private Transform _pressed;
        private float _lastHover = float.NegativeInfinity;

        private void OnEnable()
        {
            PointerTarget.AnyPressed += OnPressed;
            PointerTarget.AnyReleased += OnReleased;
            PointerTarget.AnyHoverStarted += OnHover;
            DragTarget.AnyDragStarted += OnDragStarted;
            DragTarget.AnyDragEnded += OnDragEnded;
        }

        private void OnDisable()
        {
            PointerTarget.AnyPressed -= OnPressed;
            PointerTarget.AnyReleased -= OnReleased;
            PointerTarget.AnyHoverStarted -= OnHover;
            DragTarget.AnyDragStarted -= OnDragStarted;
            DragTarget.AnyDragEnded -= OnDragEnded;
            foreach (var (target, scale) in _lifted)
                if (target != null) target.localScale = scale;
            _lifted.Clear();
        }

        private void OnHover(Transform target)
        {
            if (target == null || Time.unscaledTime - _lastHover < hoverGap) return;
            _lastHover = Time.unscaledTime;
            AudioManager.Play(SfxId.Hover);
        }

        private void OnPressed(Transform target, HandPose pose)
        {
            _pressFrame = Time.frameCount;
            _pressed = target;
        }

        /// <summary>A click that made no sound of its own (press → release, plus a frame for late handlers) gets one.</summary>
        private void OnReleased(Transform target)
        {
            if (target != _pressed) return;
            StartCoroutine(ClickFallback(target, _pressFrame));
        }

        private IEnumerator ClickFallback(Transform target, int pressFrame)
        {
            yield return null;
            if (AudioManager.LastSfxFrame >= pressFrame || target == null) yield break;
            AudioManager.Play(SfxId.UiClick);
            if (fx != null) fx.Puff(Centre(target), clickDust, 4, 0.4f, 0.18f, 0f);
        }

        private void OnDragStarted(Transform target, HandPose pose)
        {
            if (target == null) return;
            var frame = Time.frameCount;
            if (!_lifted.ContainsKey(target)) _lifted[target] = target.localScale;
            StartCoroutine(Scale(target, _lifted[target] * liftScale));
            StartCoroutine(SoundFallback(SfxId.Pickup, frame));
        }

        private void OnDragEnded(Transform target)
        {
            if (target == null || !_lifted.TryGetValue(target, out var rest)) return;
            _lifted.Remove(target);
            StartCoroutine(Scale(target, rest));
            StartCoroutine(SoundFallback(SfxId.PutDown, Time.frameCount));
        }

        private static IEnumerator SoundFallback(SfxId id, int frame)
        {
            yield return null;
            if (AudioManager.LastSfxFrame < frame) AudioManager.Play(id);
        }

        private IEnumerator Scale(Transform target, Vector3 to)
        {
            var from = target.localScale;
            yield return Tweens.Run(liftSeconds, k =>
            {
                if (target != null) target.localScale = Vector3.LerpUnclamped(from, to, k);
            });
        }

        private static Vector3 Centre(Transform target) =>
            target.TryGetComponent<Renderer>(out var renderer) ? renderer.bounds.center : target.position;
    }
}
