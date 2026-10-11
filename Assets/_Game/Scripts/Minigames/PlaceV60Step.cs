using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Pour-over days: the V60 waits on the window sill. Pick it up and set it on the cup — it snaps on (the cup's
    /// opening closes under it). Dropped anywhere else, it goes back to the sill. Unscored.
    /// </summary>
    public sealed class PlaceV60Step : MinigameStep
    {
        [Tooltip("How close to the cup's rim (world units) the V60 has to be let go.")]
        [SerializeField, Min(0.1f)] private float snapReach = 1.1f;
        [SerializeField, Min(0f)] private float snapSeconds = 0.2f;
        [SerializeField, Min(0f)] private float returnSeconds = 0.3f;

        private CounterStation _counter;
        private FeedbackFx _fx;
        private DragTarget _drag;
        private Vector3 _grab;
        private Coroutine _move;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _fx = ctx.Fx;
            _drag = _counter.DripperDrag;
            if (_drag == null || _counter.DripperOnMug)
            {
                _counter.PlaceDripper(true);
                Complete(1f);
                return;
            }

            _drag.DragStarted += OnDragStarted;
            _drag.Dragged += OnDragged;
            _drag.DragEnded += OnDragEnded;
            _drag.Interactable = true;
            NudgeTarget = _drag.transform;
        }

        private void OnDestroy()
        {
            if (_drag == null) return;
            _drag.DragStarted -= OnDragStarted;
            _drag.Dragged -= OnDragged;
            _drag.DragEnded -= OnDragEnded;
            _drag.Interactable = false;
        }

        private void OnDragStarted(Vector2 world)
        {
            if (IsComplete) return;
            if (_move != null) StopCoroutine(_move);
            _grab = _drag.transform.position - (Vector3)world;
        }

        private void OnDragged(Vector2 world)
        {
            if (IsComplete) return;
            var t = _drag.transform;
            t.position = new Vector3(world.x + _grab.x, world.y + _grab.y, t.position.z);
        }

        private void OnDragEnded(Vector2 world)
        {
            if (IsComplete) return;
            var top = _counter.MugTop;
            var bottom = _counter.DripperTop; // its top, but the V60 is short: compare its centre line to the cup's rim
            var near = Mathf.Abs(bottom.x - top.x) <= snapReach && bottom.y >= top.y - 0.3f && bottom.y <= top.y + 2.5f;
            _move = StartCoroutine(near ? Snap() : Back());
        }

        private IEnumerator Snap()
        {
            _drag.Interactable = false;
            yield return MoveTo(_counter.DripperOnMugLocal, snapSeconds);
            AudioManager.Play(SfxId.PutDown);
            _fx.Punch(_drag.transform, 0.5f);
            _fx.Punch(_counter.Mug.transform, 0.3f);
            Complete(1f);
        }

        private IEnumerator Back()
        {
            yield return MoveTo(_counter.DripperSideLocal, returnSeconds);
            AudioManager.Play(SfxId.PutDown);
        }

        private IEnumerator MoveTo(Vector3 local, float seconds)
        {
            var t = _drag.transform;
            var from = t.localPosition;
            yield return Tweens.Run(seconds, k => t.localPosition = Vector3.Lerp(from, local, k));
            t.localPosition = local;
        }
    }
}
