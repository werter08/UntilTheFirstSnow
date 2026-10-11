using Courtyard.Audio;
using Courtyard.Core;
using Courtyard.Kitchen;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Courtyard.Minigames
{
    /// <summary>
    /// After steaming: pick the milk jug up (it follows the pointer, like the kettle), hold it over the cup and it tilts
    /// and pours; let go to put it back. Only the fill level matters: stop in the green. The coffee turns into a latte as
    /// the milk goes in and the foam rises. Overflowing spills foam over the rim — a cold, poor cup, but the drink goes
    /// on (no remake, and it never empties a warmth meter).
    /// </summary>
    public sealed class PourMilkStep : MinigameStep
    {
        [Header("Gauge")]
        [SerializeField] private Sprite gaugeIcon;

        [Header("Tuning (cup fill 0..1)")]
        [SerializeField, Range(0f, 1f)] private float greenMin = 0.75f;
        [SerializeField, Range(0f, 1f)] private float greenMax = 0.9f;
        [Tooltip("At this fill the milk spills over the rim and the step ends.")]
        [SerializeField, Range(0f, 1f)] private float overflowAt = 0.95f;
        [Tooltip("Under the green by at most this much is still an ok cup.")]
        [SerializeField, Range(0f, 0.5f)] private float okBelow = 0.15f;
        [SerializeField, Min(0.01f)] private float pourPerSecond = 0.2f;
        [Tooltip("Letting go before this much milk is in just puts the jug back (the step goes on).")]
        [SerializeField, Min(0f)] private float minPourToFinish = 0.05f;

        [Header("Quality")]
        [SerializeField, Range(0f, 1f)] private float greenEdgeQuality = 0.8f;
        [SerializeField, Range(0f, 1f)] private float underQuality = 0.6f;
        [SerializeField, Range(0f, 1f)] private float farUnderQuality = 0.45f;
        [SerializeField, Range(0f, 1f)] private float overQuality = 0.7f;
        [SerializeField, Range(0f, 1f)] private float spilledQuality = 0.3f;

        [Header("Carrying the jug")]
        [Tooltip("How far (sideways) from the cup's opening the spout may be and still pour into it.")]
        [SerializeField, Min(0.1f)] private float pourReach = 0.6f;
        [SerializeField, Min(0.1f)] private float follow = 14f;
        [SerializeField] private float tiltDegrees = 35f;
        [Tooltip("Over the cup, the spout stays at least this high above the rim, so the jug pours from above.")]
        [SerializeField, Min(0f)] private float pourHeight = 0.55f;
        [SerializeField, Min(0.1f)] private float tiltSpeed = 10f;
        [SerializeField, Min(0f)] private float returnSeconds = 0.25f;

        [Header("Look and sound")]
        [SerializeField] private Color milkColour = new(0.957f, 0.929f, 0.886f); // #f4ede2
        [Tooltip("Where the spout is on the jug drawing (0..1 from its bottom-left), for a jug pouring to the left.")]
        [SerializeField] private Vector2 spoutInSprite = new(0.03f, 0.95f);
        [Tooltip("A soft round drop for the milk (the kettle's drops are drawn water streaks).")]
        [SerializeField] private Sprite dropSprite;
        [SerializeField] private Vector2 dropSize = new(0.07f, 0.12f);
        [Header("Carried: the team's drawing of his arm holding the jug")]
        [Tooltip("Arm + jug drawing per sweater (Grey, Beige); drawn pouring to the right, mirrored for a cup on the left.")]
        [SerializeField] private Sprite[] carrySprites;
        [Tooltip("Sleeve strips for the drawings, so the sleeve runs past the screen edge like every other hand.")]
        [SerializeField] private SleeveLibrary sleeves;
        [Tooltip("Jug centre and spout tip in the drawing (pixels from its top-left), and the jug's drawn height.")]
        [SerializeField] private Vector2 artJugCentre = new(358f, 68f);
        [SerializeField] private Vector2 artSpout = new(396f, 1f);
        [SerializeField, Min(1f)] private float artJugHeight = 136f;
        [Tooltip("Tilt while pouring with the drawing (the whole arm turns with it, so less than the bare jug's).")]
        [SerializeField] private float carriedTiltDegrees = 10f;
        [Tooltip("Turns the continued sleeve off the drawn sleeve's line (0: it simply carries on, no visible joint).")]
        [SerializeField] private float sleeveBendDegrees;
        [Tooltip("The foam on top of the jug shrinks to this as the milk pours out.")]
        [SerializeField, Range(0f, 1f)] private float foamLeft = 0.35f;
        [SerializeField, Min(0.1f)] private float pourPitch = 1.2f;
        [SerializeField, Range(0f, 1f)] private float pourVolume = 0.6f;

        [Header("Why it went wrong")]
        [SerializeField] private string tooLittleText = "Needs more milk — fill to the green.";
        [SerializeField] private string tooMuchText = "A bit too much milk.";
        [SerializeField] private string spilledText = "Too much — the milk spilled over.";

        private CounterStation _counter;
        private FeedbackFx _fx;
        private Camera _camera;
        private UI.MinigameGauge _gauge;
        private Transform _jug;
        private Vector3 _rest;
        private Quaternion _restRotation;
        private Vector3 _grab;
        private Vector3 _spoutLocal;
        private SpriteRenderer _stream;
        private ParticleSystem _drops;
        private float _startLevel;
        private bool _holding;
        private bool _streamOn;
        private float _splashTimer;
        private HandsView _hands;
        private SpriteRenderer _carry;
        private HandPose _jugPose;
        private bool _blobWasShown;
        private Transform _blob;
        private Transform _blobParent;
        private Vector3 _blobLocalPosition;
        private Quaternion _blobLocalRotation;
        private Vector3 _blobScale;

        public override bool RemakesWhenBad => false;
        public override bool CanFreeze => false;

        public override void Begin(MinigameContext ctx)
        {
            _counter = ctx.Counter;
            _fx = ctx.Fx;
            _camera = ctx.WorldCamera;
            _gauge = ctx.Gauge;
            _hands = ctx.Hands;
            if (_hands != null) _hands.UseRightHand(true); // the milk is poured with the right hand
            _jug = _counter.MilkJug.transform;
            _rest = _jug.position;
            _restRotation = _jug.rotation;
            _startLevel = _counter.MugLevel;
            ResultAt = _counter.MugBody;
            NudgeTarget = _jug;

            // The spout: the counter jug's top corner that faces the cup (or the drawn spout of the carried jug, below).
            var body = _counter.MilkJugBody.bounds;
            var towardCup = _counter.MugTop.x < body.center.x ? -1f : 1f;
            var u = towardCup < 0f ? spoutInSprite.x : 1f - spoutInSprite.x;
            var spout = new Vector3(body.min.x + body.size.x * u, body.min.y + body.size.y * spoutInSprite.y, _jug.position.z);
            tiltDegrees = Mathf.Abs(tiltDegrees) * -towardCup; // tip toward the cup
            if (CreateCarryArt(ctx.Level.Props.sweater, towardCup < 0f, out var drawnSpout)) spout = drawnSpout;
            _spoutLocal = _jug.InverseTransformPoint(spout);
            CreateStream(spout);
            CarryFoam();

            _counter.MilkJug.Pressed += OnPressed;
            _counter.MilkJug.Released += OnReleased;
            _counter.MilkJug.Interactable = true;
            if (_gauge != null)
                _gauge.Show(_counter.MugBody, _camera, new Vector2(greenMin, greenMax), new Vector2(overflowAt, 1f), gaugeIcon, _startLevel);
        }

        /// <summary>
        /// While carried, the counter jug and the loose hand give way to the drawing of his arm holding the jug, lined up
        /// on the jug. Returns where the drawn spout is.
        /// </summary>
        private bool CreateCarryArt(Sweater sweater, bool mirrored, out Vector3 spout)
        {
            spout = default;
            var index = (int)sweater;
            var sprite = carrySprites != null && index < carrySprites.Length ? carrySprites[index] : null;
            if (sprite == null) return false;

            var body = _counter.MilkJugBody;
            _carry = new GameObject("CarriedJug", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            _carry.transform.SetParent(_jug, false);
            _carry.sprite = sprite;
            _carry.sortingLayerID = body.sortingLayerID;
            _carry.sortingOrder = body.sortingOrder + 3;
            _carry.enabled = false;

            // Same jug size as on the counter; the drawn jug sits exactly where the counter jug is.
            var ppu = sprite.pixelsPerUnit;
            var k = body.bounds.size.y / (artJugHeight / ppu);
            // Mirrored by scale (not flipX), so the sleeve that continues past the screen edge mirrors with it.
            var size = k / Mathf.Max(0.0001f, _jug.lossyScale.y);
            _carry.transform.localScale = new Vector3(mirrored ? -size : size, size, 1f);
            _carry.gameObject.AddComponent<SleeveExtender>().Init(sleeves, sleeveBendDegrees);
            tiltDegrees = Mathf.Sign(tiltDegrees) * Mathf.Abs(carriedTiltDegrees);
            Vector3 FromCentre(Vector2 px) // drawing pixels (from its top-left) → world offset from the sprite's pivot
            {
                var dx = px.x - sprite.pivot.x;
                var dy = (sprite.rect.height - px.y) - sprite.pivot.y;
                return new Vector3(mirrored ? -dx : dx, dy, 0f) / ppu * k;
            }

            var centre = body.bounds.center;
            _carry.transform.position = new Vector3(centre.x, centre.y, _jug.position.z) - FromCentre(artJugCentre);
            spout = _carry.transform.position + FromCentre(artSpout);
            _jugPose = _counter.MilkJug.HandPose;
            _counter.MilkJug.HandPose = HandPose.Hidden; // the drawing has its own hand
            return true;
        }

        /// <summary>Carried: the drawing shows; on the counter: the counter jug (and its foam) again.</summary>
        private void ShowCarried(bool carried)
        {
            if (_carry == null) return;
            _carry.enabled = carried;
            _counter.MilkJugBody.enabled = !carried;
            if (_blob == null) return;
            if (carried)
            {
                _blobWasShown = _blob.gameObject.activeSelf;
                _blob.gameObject.SetActive(false);
            }
            else if (!IsComplete) _blob.gameObject.SetActive(_blobWasShown);
        }

        /// <summary>The milk falls as drops, like the kettle's water: a copy of the kettle's stream, milk-white, at the spout.</summary>
        private void CreateStream(Vector3 spout)
        {
            var source = _counter.KettleStream;
            if (source == null) return;
            _stream = Instantiate(source.gameObject, _jug).GetComponent<SpriteRenderer>();
            _stream.name = "MilkStream";
            _stream.transform.position = spout;
            _stream.transform.localRotation = Quaternion.identity;
            _stream.enabled = false;
            _stream.color = Color.white;
            _drops = _stream.GetComponentInChildren<ParticleSystem>(true);
            if (_drops == null) return;
            _drops.transform.position = spout; // the kettle's drops start above their stream; the milk leaves the spout
            _drops.Clear();
            var main = _drops.main;
            main.startColor = milkColour;
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(dropSize.x, dropSize.y);
            main.startRotation = 0f;
            if (dropSprite == null) return;
            var sheet = _drops.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            while (sheet.spriteCount > 0) sheet.RemoveSprite(0);
            sheet.AddSprite(dropSprite);
        }

        /// <summary>The steamed foam sits in the jug's opening: it travels (and tips) with the jug while it pours.</summary>
        private void CarryFoam()
        {
            _blob = _counter.MilkFoamBlob;
            if (_blob == null) return;
            _blobParent = _blob.parent;
            _blobLocalPosition = _blob.localPosition;
            _blobLocalRotation = _blob.localRotation;
            _blobScale = _blob.localScale;
            _blob.SetParent(_jug, true);
        }

        private void ReleaseFoam()
        {
            if (_blob == null) return;
            _blob.SetParent(_blobParent, false);
            _blob.localPosition = _blobLocalPosition;
            _blob.localRotation = _blobLocalRotation;
            _blob.localScale = _blobScale;
            _blob = null;
        }

        private void OnDestroy()
        {
            if (_hands != null) _hands.UseRightHand(false);
            if (_counter == null) return;
            if (_carry != null)
            {
                _counter.MilkJug.HandPose = _jugPose;
                _counter.MilkJugBody.enabled = true;
                Destroy(_carry.gameObject);
            }

            _counter.MilkJug.Pressed -= OnPressed;
            _counter.MilkJug.Released -= OnReleased;
            SetStream(false);
            AudioManager.StopTrack(SfxId.Pour);
            AudioManager.SetLoopPitch(SfxId.Pour, 1f); // the kettle pours at its own pitch again
            AudioManager.SetTrackVolume(SfxId.Pour, 1f);
            if (_stream != null) Destroy(_stream.gameObject);
            if (!IsComplete) PutJugBack(0f);
            ReleaseFoam();
            if (_gauge != null) _gauge.Hide();
        }

        private void OnPressed()
        {
            if (IsComplete) return;
            _holding = true;
            _grab = _jug.position - (Vector3)PointerWorld();
            ShowCarried(true);
        }

        private void OnReleased()
        {
            if (!_holding) return;
            _holding = false;
            SetStream(false);
            if (IsComplete) return;
            if (_counter.MugLevel - _startLevel >= minPourToFinish) Finish(false);
            else
            {
                ShowCarried(false);
                PutJugBack(returnSeconds);
            }
        }

        private void Update()
        {
            if (IsComplete || !_holding) return;

            var pointer = PointerWorld();
            var k = 1f - Mathf.Exp(-follow * Time.deltaTime);
            var target = new Vector3(pointer.x + _grab.x, pointer.y + _grab.y, _jug.position.z);
            // Over the cup the jug rides above the rim (never sinks into the cup): the stream shows between them.
            var spoutOffset = _jug.TransformPoint(_spoutLocal) - _jug.position;
            var top = _counter.MugTop;
            if (Mathf.Abs(target.x + spoutOffset.x - top.x) <= pourReach * 1.5f)
                target.y = Mathf.Max(target.y, top.y + pourHeight - spoutOffset.y);
            _jug.position = Vector3.Lerp(_jug.position, target, k);

            var spout = _jug.TransformPoint(_spoutLocal);
            var pouring = _counter.IsAboveMug(spout, pourReach);
            var tilt = Quaternion.Euler(0f, 0f, pouring ? tiltDegrees : 0f) * _restRotation;
            _jug.rotation = Quaternion.Slerp(_jug.rotation, tilt, 1f - Mathf.Exp(-tiltSpeed * Time.deltaTime));
            SetStream(pouring);
            if (!pouring) return;

            var level = Mathf.Min(1f, _counter.MugLevel + pourPerSecond * Time.deltaTime);
            _counter.SetMugLevel(level);
            var share = Mathf.InverseLerp(_startLevel, (greenMin + greenMax) * 0.5f, level);
            _counter.SetMilkInMug(share);
            if (_blob != null) _blob.localScale = _blobScale * Mathf.Lerp(1f, foamLeft, share); // the jug empties
            if (_gauge != null) _gauge.SetValue(level);
            _splashTimer -= Time.deltaTime;
            if (_splashTimer <= 0f)
            {
                _splashTimer = 0.15f;
                _fx.Burst(_counter.MugTop, milkColour, 2, 0.6f, 0.05f, 1.2f);
            }

            if (level >= overflowAt) Finish(true);
        }

        private void SetStream(bool on)
        {
            if (_streamOn == on) return;
            _streamOn = on;
            if (_stream != null) _stream.enabled = on;
            if (on)
            {
                AudioManager.ResumeTrack(SfxId.Pour);
                AudioManager.SetLoopPitch(SfxId.Pour, pourPitch); // milk: a little higher and softer than water
                AudioManager.SetTrackVolume(SfxId.Pour, pourVolume);
            }
            else AudioManager.PauseTrack(SfxId.Pour);
        }

        private void Finish(bool spilled)
        {
            if (IsComplete) return;
            _holding = false;
            SetStream(false);
            _counter.MilkJug.Interactable = false;
            ShowCarried(false); // set down: the counter jug again
            PutJugBack(returnSeconds);
            if (_gauge != null) _gauge.Release();

            var level = _counter.MugLevel;
            _counter.SetMugDrink(MugDrink.Latte, Color.white); // the finished latte (the served drink shows later, as before)
            _counter.SetMilkInMug(0f);
            _counter.SetFoamCap(1f);
            if (_blob != null) _blob.gameObject.SetActive(false); // the foam is in the cup now
            _fx.Punch(_counter.Mug.transform, 0.5f);

            float quality;
            if (spilled)
            {
                // Foam runs over the rim on both sides.
                var top = _counter.MugTop;
                var half = _counter.MugBody.bounds.extents.x * 0.6f;
                _fx.Burst(top + Vector3.left * half, milkColour, 6, 0.5f, 0.06f, 1.5f);
                _fx.Burst(top + Vector3.right * half, milkColour, 6, 0.5f, 0.06f, 1.5f);
                quality = spilledQuality;
                CameraShake.Kick(0.07f, 0.35f);
                Explain(_fx, spilledText, _counter.MugBody);
            }
            else if (level >= greenMin && level <= greenMax)
            {
                var centre = (greenMin + greenMax) * 0.5f;
                var off = Mathf.Abs(level - centre) / Mathf.Max(0.001f, (greenMax - greenMin) * 0.5f);
                quality = Mathf.Lerp(1f, greenEdgeQuality, off);
            }
            else if (level > greenMax)
            {
                quality = overQuality;
                Explain(_fx, tooMuchText, _counter.MugBody);
            }
            else
            {
                quality = level >= greenMin - okBelow ? underQuality : farUnderQuality;
                Explain(_fx, tooLittleText, _counter.MugBody);
            }

            Complete(quality);
        }

        /// <summary>The jug goes back to its spot, upright (on the counter's coroutine: this step is destroyed soon).</summary>
        private void PutJugBack(float seconds)
        {
            var jug = _jug;
            var from = jug.position;
            var fromRotation = jug.rotation;
            var rest = _rest;
            var restRotation = _restRotation;
            if (seconds <= 0f || !_counter.isActiveAndEnabled)
            {
                jug.SetPositionAndRotation(rest, restRotation);
                return;
            }

            _counter.StartCoroutine(Tweens.Run(seconds, t =>
            {
                if (jug == null) return;
                jug.SetPositionAndRotation(Vector3.Lerp(from, rest, t), Quaternion.Slerp(fromRotation, restRotation, t));
            }));
        }

        private Vector2 PointerWorld()
        {
            var screen = Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
            return _camera.ScreenToWorldPoint(screen);
        }
    }
}
