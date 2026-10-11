using System.Collections;
using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;

namespace Courtyard.Minigames
{
    /// <summary>
    /// Pour-over days: the V60 waits on the window sill. Pick it up and set it on the cup — it snaps on (the cup's
    /// opening closes under it) as soon as it gets near, without having to aim and let go. Dropped anywhere else, it goes back to the sill. Unscored.
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
        private bool _placing;

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
            if (IsComplete || _placing) return;
            if (_move != null) StopCoroutine(_move);
            _grab = _drag.transform.position - (Vector3)world;
        }

        private void OnDragged(Vector2 world)
        {
            if (IsComplete || _placing) return;
            var t = _drag.transform;
            t.position = new Vector3(world.x + _grab.x, world.y + _grab.y, t.position.z);
            if (NearCup()) _move = StartCoroutine(Snap()); // close enough: it settles on by itself
        }

        private void OnDragEnded(Vector2 world)
        {
            if (IsComplete || _placing) return;
            _move = StartCoroutine(NearCup() ? Snap() : Back());
        }

        private bool NearCup()
        {
            var top = _counter.MugTop;
            var v60 = _counter.DripperTop; // its top, but the V60 is short: compare its centre line to the cup's rim
            return Mathf.Abs(v60.x - top.x) <= snapReach && v60.y >= top.y - 0.3f && v60.y <= top.y + 2.5f;
        }

        private IEnumerator Snap()
        {
            _placing = true;
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
