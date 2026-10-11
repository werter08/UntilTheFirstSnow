using Courtyard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Applies a <see cref="LevelDefinition"/> to the kitchen scene: lights, Mira's window, the tree, calendar, clock and wall props.
    /// Counter tools are handled by <see cref="CounterStation"/>.
    /// Minigame steps use it for story moments (Mira waving, Mira at the door).
    /// </summary>
    public sealed class KitchenDirector : MonoBehaviour
    {
        [Header("Lights")]
        [SerializeField] private Light2D globalLight;
        [SerializeField] private Light2D lampLight;
        [SerializeField] private Light2D miraLight;

        [Header("Courtyard")]
        [SerializeField] private SpriteRenderer courtyard;
        [SerializeField] private Color morningCourtyard = new(0.62f, 0.66f, 0.72f);
        [SerializeField] private Color eveningCourtyard = new(0.42f, 0.4f, 0.48f);
        [SerializeField] private Color nightCourtyard = new(0.16f, 0.18f, 0.28f);
        [SerializeField] private SpriteRenderer treeCrown;
        [Tooltip("Outside pieces shaded by the time of day (Mira's window is not: it stays the warm spot at night).")]
        [SerializeField] private SpriteRenderer[] outsideShaded;
        [SerializeField] private Color morningOutside = Color.white;
        [SerializeField] private Color eveningOutside = new(0.82f, 0.8f, 0.86f);
        [SerializeField] private Color nightOutside = new(0.38f, 0.42f, 0.6f);
        [Tooltip("One colour per TreeStage, in enum order.")]
        [SerializeField] private Color[] treeStageColors =
        {
            new(0.62f, 0.7f, 0.3f), new(0.9f, 0.5f, 0.2f), new(0.95f, 0.75f, 0.3f), new(0.7f, 0.45f, 0.3f), new(0.92f, 0.94f, 0.97f),
        };

        [Header("Weather (seen through the window)")]
        [SerializeField] private WeatherPreset[] weatherPresets;

        [Header("Mira's window")]
        [Tooltip("The drawn window (Lit/Dark/Silhouette/Wave/Note); without its sprites the placeholder below is used.")]
        [SerializeField] private MiraWindow miraWindowArt;
        [SerializeField] private SpriteRenderer miraWindow;
        [SerializeField] private Color miraWindowDark = new(0.2f, 0.22f, 0.3f);
        [SerializeField] private GameObject miraSilhouette;
        [SerializeField] private Transform miraArm;
        [SerializeField] private GameObject miraNote;
        [SerializeField, Min(0.05f)] private float waveFrameSeconds = 0.35f;
        [SerializeField] private float waveArmAngle = 35f;
        [SerializeField] private GameObject miraAtDoor;
        [Tooltip("Click Mira's lit window and, from this day on, she waves back.")]
        [SerializeField] private Fidget miraWindowFidget;
        [SerializeField] private int waveBackFromDay = 5;
        [SerializeField, Min(0.1f)] private float waveBackSeconds = 2f;

        [Header("Wind (ambient motion strength per weather)")]
        [SerializeField] private float calmWind = 1f;
        [SerializeField] private float rainWind = 1.6f;
        [SerializeField] private float windyWind = 3.2f;

        [Header("Ceiling lamp (optional art)")]
        [SerializeField] private SpriteRenderer lampRenderer;
        [SerializeField] private Sprite lampOnSprite;
        [SerializeField] private Sprite lampOffSprite;
        [SerializeField] private SpriteRenderer lampGlow;
        [SerializeField, Range(0f, 1f)] private float lampGlowAlpha = 0.45f;

        [Header("Wall")]
        [SerializeField] private TMP_Text calendarDay;
        [SerializeField] private TMP_Text clockTime;
        [Tooltip("Optional drawn clock hands (pivot at the clock centre); set from the level's clock time.")]
        [SerializeField] private Transform clockHourHand;
        [SerializeField] private Transform clockMinuteHand;

        [Header("Props")]
        [SerializeField] private GameObject beans;
        [SerializeField] private GameObject cinnamon;
        [SerializeField] private GameObject herMug;
        [SerializeField] private GameObject scarf;
        [SerializeField] private SpriteRenderer plant;
        [Tooltip("One colour per PlantStage, in enum order.")]
        [SerializeField] private Color[] plantStageColors =
        {
            new(0.55f, 0.5f, 0.3f), new(0.5f, 0.62f, 0.32f), new(0.35f, 0.65f, 0.35f), new(0.95f, 0.55f, 0.65f),
        };
        [Tooltip("Optional drawn plant per PlantStage (Wilted, Recovering, Healthy, Blooming); replaces the tint.")]
        [SerializeField] private Sprite[] plantStageSprites;
        [SerializeField] private GameObject[] fridgeNotes;
        [Tooltip("The notes pinned at the end of each day (one slot per note, in order). Empty = the old per-day count.")]
        [SerializeField] private Story.NotesBook notesBook;
        [SerializeField] private UI.NoteCard noteCard;
        [Tooltip("Day 30: his and her mugs side by side on the window sill.")]
        [SerializeField] private GameObject sillMugs;
        [Tooltip("Wall rack slots, left to right; filled with earlier days' mugs.")]
        [SerializeField] private SpriteRenderer[] rackSlots;
        [Tooltip("Mug art per MugStyle (Chipped, His, Hers, Yellow, Green).")]
        [SerializeField] private Sprite[] rackMugSprites;

        private LevelDefinition _level;
        private MiraWindowState _windowState;
        private float _waveTimer;
        private bool _armUp;

        public MiraWindowState WindowState => _windowState;

        public void Apply(LevelDefinition level)
        {
            _level = level;
            var palette = level.Palette;

            globalLight.color = palette.globalLightColor;
            globalLight.intensity = palette.globalLightIntensity;
            lampLight.color = palette.lampColor;
            lampLight.intensity = palette.lampIntensity;
            ApplyLamp(level.Props.lampOff, palette.lampColor);

            // Sky behind the opposite building: the level's colour, or a time-of-day fallback.
            courtyard.color = level.SkyColor.a > 0f
                ? level.SkyColor
                : level.TimeOfDay switch
                {
                    TimeOfDay.Evening => eveningCourtyard,
                    TimeOfDay.Night => nightCourtyard,
                    _ => morningCourtyard,
                };
            ApplyTreeCrown(level);
            var shade = level.TimeOfDay switch
            {
                TimeOfDay.Evening => eveningOutside,
                TimeOfDay.Night => nightOutside,
                _ => morningOutside,
            };
            if (outsideShaded != null)
            {
                _outsideBase ??= System.Array.ConvertAll(outsideShaded, p => p != null ? p.color : Color.white);
                for (var i = 0; i < outsideShaded.Length; i++)
                {
                    var piece = outsideShaded[i];
                    if (piece == null) continue;
                    var baseColour = piece == treeCrown ? treeCrown.color : _outsideBase[i]; // crown: this day's colour
                    piece.color = baseColour * shade;
                }
            }

            calendarDay.text = level.DayNumber.ToString();
            clockTime.text = level.ClockTime;
            SetClockHands(level.ClockTime);
            _lampBroken = false;
            BeginClock(level);

            var props = level.Props;
            beans.SetActive(props.beans);
            cinnamon.SetActive(props.cinnamon);
            if (herMug != null) herMug.SetActive(props.herMug);
            FillRack(props.rackMugs);
            ProtectHerMug();
            scarf.SetActive(props.scarf);
            var stage = (int)props.plantStage;
            var drawn = plantStageSprites != null && stage < plantStageSprites.Length ? plantStageSprites[stage] : null;
            if (drawn != null)
            {
                plant.sprite = drawn;
                plant.color = Color.white;
            }
            else
            {
                plant.color = PickColor(plantStageColors, stage);
            }
            if (notesBook != null) ShowPinnedNotes(level.DayNumber);
            else
                for (var i = 0; i < fridgeNotes.Length; i++)
                    fridgeNotes[i].SetActive(i < props.fridgeNotes);
            // On the finale day the two mugs reach the sill only once the drink is served (LevelRunner.Finale).
            if (sillMugs != null) sillMugs.SetActive(props.mugsOnSill && !level.Finale);

            ApplyWeather(level.Weather);
            AmbientMotion.Wind = level.Weather switch
            {
                Weather.Wind => windyWind,
                Weather.HeavyRain => rainWind,
                _ => calmWind,
            };
            SetMiraAtDoor(false);
            SetMiraWindow(level.MiraWindow);
        }

        public void SetMiraWindow(MiraWindowState state)
        {
            _windowState = state;
            if (miraWindowArt != null && miraWindowArt.HasArt)
            {
                miraWindowArt.SetState(state); // drawn window: sprite per state, and it switches its own light
                return;
            }

            var lit = state is MiraWindowState.Lit or MiraWindowState.Silhouette
                or MiraWindowState.Wave or MiraWindowState.Note;
            var windowColor = _level != null ? _level.Palette.miraWindowColor : Color.white;

            miraWindow.color = lit ? windowColor : miraWindowDark;
            miraLight.enabled = lit;
            miraLight.color = windowColor;
            miraSilhouette.SetActive(state is MiraWindowState.Silhouette or MiraWindowState.Wave);
            miraNote.SetActive(state == MiraWindowState.Note);

            _waveTimer = 0f;
            SetArm(false);
        }

        public void SetMiraAtDoor(bool visible) => miraAtDoor.SetActive(visible);

        private int _pinned;

        /// <summary>Every note written after an earlier day hangs on the fridge and can be clicked to read.</summary>
        private void ShowPinnedNotes(int day)
        {
            var pinned = notesBook.PinnedBefore(day);
            _pinned = Mathf.Min(pinned.Count, fridgeNotes.Length);
            for (var i = 0; i < fridgeNotes.Length; i++)
            {
                var has = i < _pinned;
                fridgeNotes[i].SetActive(has);
                if (has && fridgeNotes[i].TryGetComponent<ReadableNote>(out var readable)) readable.Set(pinned[i], noteCard);
            }
        }

        /// <summary>
        /// End of the day: the note written today pops onto the fridge (paper sound, a little sparkle) in the next free
        /// slot. Returns that slot, or null when there is no note for today (or no room).
        /// </summary>
        public Transform PinNoteAfter(int day)
        {
            var note = notesBook != null ? notesBook.WrittenAfter(day) : null;
            if (note == null || _pinned >= fridgeNotes.Length) return null;

            var slot = fridgeNotes[_pinned++];
            slot.SetActive(true);
            if (slot.TryGetComponent<ReadableNote>(out var readable)) readable.Set(note, noteCard);
            StartCoroutine(PopIn(slot.transform));
            Audio.AudioManager.Play(Audio.SfxId.PaperNote);
            return slot.transform;
        }

        private System.Collections.IEnumerator PopIn(Transform note)
        {
            var size = note.localScale;
            yield return Tweens.RunLinear(0.35f, t =>
            {
                var overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;
                note.localScale = size * (t * overshoot);
            });
            note.localScale = size;
        }

        public UI.NoteCard NoteCard => noteCard;

        [Header("Day 10: at the front door")]
        [Tooltip("The door scene (Mira with her cup), placed away from the kitchen; the camera visits it.")]
        [SerializeField] private Transform doorView;
        [SerializeField] private PointerTarget herCupAtDoor;

        private Vector3? _kitchenCamera;

        public PointerTarget HerCupAtDoor => doorView != null ? herCupAtDoor : null;

        /// <summary>Her cup at the door: empty when she brings it, full of her drink when it goes back to her.</summary>
        public void SetHerDoorCupFull(bool full)
        {
            var drink = herCupAtDoor != null ? herCupAtDoor.transform.Find("Drink") : null;
            if (drink != null) drink.gameObject.SetActive(full);
        }

        /// <summary>Moves the camera to the front door (Mira with her cup) and back (call while the screen is black).</summary>
        public void ShowDoor(Camera cam, bool atDoor)
        {
            if (doorView == null || cam == null) return;
            var t = cam.transform;
            if (atDoor)
            {
                _kitchenCamera ??= t.position;
                doorView.gameObject.SetActive(true);
                t.position = new Vector3(doorView.position.x, doorView.position.y, t.position.z);
                return;
            }

            if (_kitchenCamera.HasValue) t.position = _kitchenCamera.Value;
            _kitchenCamera = null;
            doorView.gameObject.SetActive(false);
        }

        public void SetSillMugs(bool visible)
        {
            if (sillMugs != null) sillMugs.SetActive(visible);
            if (!visible) return;
            // His and her mugs leave the rack for the sill: never two copies of the same mug.
            for (var i = 0; i < _rack.Count; i++)
                if (_rack[i].style is MugStyle.His or MugStyle.Hers) _rack[i].slot.gameObject.SetActive(false);
        }

        private readonly System.Collections.Generic.List<(Transform slot, MugStyle style)> _rack = new();

        /// <summary>Mugs on the wall rack right now (for ChooseMug).</summary>
        public System.Collections.Generic.IReadOnlyList<(Transform slot, MugStyle style)> RackMugs => _rack;

        private void FillRack(MugStyle[] mugs)
        {
            _rack.Clear();
            if (rackSlots == null) return;
            for (var i = 0; i < rackSlots.Length; i++)
            {
                var has = mugs != null && i < mugs.Length && (int)mugs[i] < rackMugSprites.Length && rackMugSprites[(int)mugs[i]] != null;
                rackSlots[i].gameObject.SetActive(has);
                if (!has) continue;
                rackSlots[i].sprite = rackMugSprites[(int)mugs[i]];
                _rack.Add((rackSlots[i].transform, mugs[i]));
            }
        }

        private void OnEnable()
        {
            if (miraWindowFidget != null) miraWindowFidget.Poked += OnMiraWindowPoked;
        }

        private void OnDisable()
        {
            if (miraWindowFidget != null) miraWindowFidget.Poked -= OnMiraWindowPoked;
        }

        private Coroutine _waveBack;

        /// <summary>The player tapped Mira's window (Day 20 reads her note from it).</summary>
        public event System.Action MiraWindowPoked;

        /// <summary>Her window, to nudge the player toward it.</summary>
        public Transform MiraWindowTarget => miraWindowFidget != null ? miraWindowFidget.transform : null;

        private void OnMiraWindowPoked()
        {
            MiraWindowPoked?.Invoke();
            if (_level == null || _level.DayNumber < waveBackFromDay) return;
            // She's home (light on, or at the window): she comes to the window and waves.
            if (_windowState is not (MiraWindowState.Lit or MiraWindowState.Silhouette) || _waveBack != null) return;
            _waveBack = StartCoroutine(WaveBackOnce(_windowState));
        }

        private System.Collections.IEnumerator WaveBackOnce(MiraWindowState before)
        {
            SetMiraWindow(MiraWindowState.Wave);
            yield return new WaitForSeconds(waveBackSeconds);
            if (_windowState == MiraWindowState.Wave) SetMiraWindow(before);
            _waveBack = null;
        }

        /// <summary>Mira waves (silhouette ↔ waving frame) for a while, then stands at her window again.</summary>
        public System.Collections.IEnumerator MiraWaves(float seconds)
        {
            SetMiraWindow(MiraWindowState.Wave);
            yield return new WaitForSeconds(seconds);
            if (_windowState == MiraWindowState.Wave) SetMiraWindow(MiraWindowState.Silhouette);
        }

        [Header("Chestnut tree")]
        [Tooltip("Crown rest position; it slides left (never the window) if a drawn crown would cover Mira's window.")]
        [SerializeField] private float crownClearance = 0.1f;

        private Vector3? _crownRest;
        private Color[] _outsideBase;

        private void ApplyTreeCrown(LevelDefinition level)
        {
            _crownRest ??= treeCrown.transform.position;
            treeCrown.transform.position = _crownRest.Value;
            if (level.TreeCrownSprite != null)
            {
                treeCrown.sprite = level.TreeCrownSprite;
                treeCrown.drawMode = SpriteDrawMode.Simple;
                treeCrown.color = Color.white;
            }
            else
            {
                treeCrown.color = PickColor(treeStageColors, (int)level.TreeStage);
            }

            // The crown must never cover Mira's window: if it overlaps, move the crown left.
            var windowRenderer = miraWindowArt != null && miraWindowArt.HasArt ? miraWindowArt.GetComponent<SpriteRenderer>() : miraWindow;
            if (windowRenderer == null) return;
            var crown = treeCrown.bounds;
            var window = windowRenderer.bounds;
            var overlapsVertically = crown.min.y < window.max.y && crown.max.y > window.min.y;
            var overlap = crown.max.x + crownClearance - window.min.x;
            if (overlapsVertically && overlap > 0f) treeCrown.transform.position += Vector3.left * overlap;
        }

        private void Update()
        {
            if (miraWindowArt != null && miraWindowArt.HasArt) return; // the drawn window animates itself
            if (_windowState != MiraWindowState.Wave) return;

            _waveTimer += Time.deltaTime;
            if (_waveTimer < waveFrameSeconds) return;

            _waveTimer = 0f;
            SetArm(!_armUp);
        }

        private void SetArm(bool up)
        {
            _armUp = up;
            miraArm.localRotation = Quaternion.Euler(0f, 0f, up ? waveArmAngle : 0f);
        }

        private void ApplyWeather(Weather weather)
        {
            foreach (var preset in weatherPresets)
            {
                if (preset.system == null) continue;
                preset.system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            foreach (var preset in weatherPresets)
            {
                if (preset.system == null || preset.weather != weather) continue;

                var emission = preset.system.emission;
                emission.rateOverTime = preset.emissionRate;
                var velocity = preset.system.velocityOverLifetime;
                velocity.enabled = true;
                // Wind as a small range: every velocity axis must use the same curve mode as the builder's ranges.
                velocity.x = new ParticleSystem.MinMaxCurve(preset.wind * 0.8f - 0.1f, preset.wind * 1.2f + 0.1f);
                preset.system.Simulate(14f, true, true); // start mid-weather, not with an empty sky
                preset.system.Play(true);
            }
        }

        private void ApplyLamp(bool off, Color colour)
        {
            lampLight.enabled = !off;
            if (lampRenderer != null && lampOnSprite != null && lampOffSprite != null)
                lampRenderer.sprite = off ? lampOffSprite : lampOnSprite;
            if (lampGlow == null) return;
            lampGlow.enabled = !off;
            lampGlow.color = new Color(colour.r, colour.g, colour.b, lampGlowAlpha);
        }

        private void SetClockHands(string time)
        {
            if (clockHourHand == null || clockMinuteHand == null) return;
            var parts = time.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var hours) || !int.TryParse(parts[1], out var minutes)) return;

            clockHourHand.localRotation = Quaternion.Euler(0f, 0f, -((hours % 12) + minutes / 60f) * 30f);
            clockMinuteHand.localRotation = Quaternion.Euler(0f, 0f, -minutes * 6f);
        }

        // ---------------------------------------------------------------- clock: turn time, and the light follows

        [Header("Clock you can turn (each click moves it on; the light slowly follows the hour)")]
        [SerializeField, Min(1)] private int minutesPerClick = 15;
        [Tooltip("Minutes of turning before the light fully follows the clock instead of the day's own look.")]
        [SerializeField, Min(1f)] private float minutesToFollow = 180f;
        [SerializeField] private Color dayLight = new(1f, 0.97f, 0.92f);
        [SerializeField] private float dayLightIntensity = 1f;
        [SerializeField] private Color nightLight = new(0.42f, 0.48f, 0.75f);
        [SerializeField] private float nightLightIntensity = 0.55f;
        [SerializeField] private Color daySky = new(0.66f, 0.8f, 0.92f);
        [SerializeField] private Color nightSky = new(0.12f, 0.14f, 0.24f);

        private int _startMinutes;
        private int _minutes;
        private Color _baseLight;
        private float _baseIntensity;
        private Color _baseSky;
        private Color[] _baseShade;

        private void BeginClock(LevelDefinition level)
        {
            _startMinutes = _minutes = ParseMinutes(level.ClockTime);
            _baseLight = globalLight.color;
            _baseIntensity = globalLight.intensity;
            _baseSky = courtyard.color;
            _baseShade = outsideShaded != null ? System.Array.ConvertAll(outsideShaded, p => p != null ? p.color : Color.white) : null;
        }

        /// <summary>Turns the clock on by <see cref="minutesPerClick"/>: hands move, and day and night slowly follow.</summary>
        public void AdvanceClock()
        {
            _minutes = (_minutes + minutesPerClick) % (24 * 60);
            var time = $"{_minutes / 60:00}:{_minutes % 60:00}";
            clockTime.text = time;
            SetClockHands(time);

            // Night factor of the clock's hour (0 at 13:00, 1 at 01:00), blended in as more time is turned.
            var hours = _minutes / 60f;
            var night = Mathf.SmoothStep(0f, 1f, 0.5f - 0.5f * Mathf.Cos((hours - 13f) / 24f * Mathf.PI * 2f + Mathf.PI));
            var turned = (_minutes - _startMinutes + 24 * 60) % (24 * 60);
            var follow = Mathf.Clamp01(turned / minutesToFollow);
            globalLight.color = Color.Lerp(_baseLight, Color.Lerp(dayLight, nightLight, night), follow);
            globalLight.intensity = Mathf.Lerp(_baseIntensity, Mathf.Lerp(dayLightIntensity, nightLightIntensity, night), follow);
            courtyard.color = Color.Lerp(_baseSky, Color.Lerp(daySky, nightSky, night), follow);
            if (outsideShaded == null || _baseShade == null) return;
            var shade = Color.Lerp(morningOutside, nightOutside, night);
            for (var i = 0; i < outsideShaded.Length; i++)
                if (outsideShaded[i] != null)
                    outsideShaded[i].color = Color.Lerp(_baseShade[i], _outsideBase != null && i < _outsideBase.Length ? _outsideBase[i] * shade : shade, follow);
        }

        private static int ParseMinutes(string time)
        {
            var parts = time.Split(':');
            return parts.Length == 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m) ? h * 60 + m : 12 * 60;
        }

        // ---------------------------------------------------------------- lamp switch

        private bool _lampBroken;

        public bool LampOn => lampLight.enabled;

        /// <summary>Click the lamp: on / off (unless it's broken).</summary>
        public void ToggleLamp()
        {
            if (_lampBroken || _level == null) return;
            ApplyLamp(LampOn, _level.Palette.lampColor);
        }

        /// <summary>Clicked once too often: the bulb pops and the lamp stays dark for the rest of the day.</summary>
        public void BreakLamp()
        {
            _lampBroken = true;
            if (_level != null) ApplyLamp(true, _level.Palette.lampColor);
        }

        // ---------------------------------------------------------------- calendar

        /// <summary>Click the calendar: today's page with a hopeful line (one per day).</summary>
        public void OpenCalendar()
        {
            if (noteCard == null || _level == null) return;
            noteCard.Open($"<size=200%><color=#C0392B>{_level.DayNumber}</color></size>\n{_level.CalendarLine}", string.Empty,
                UI.NotePaper.Calendar);
        }

        /// <summary>Her mug on the rack never breaks (Day 20 needs it).</summary>
        private void ProtectHerMug()
        {
            foreach (var (slot, style) in _rack)
                if (slot.TryGetComponent<Breakable>(out var breakable)) breakable.enabled = style != MugStyle.Hers;
        }

        private static Color PickColor(Color[] colors, int index) =>
            colors.Length == 0 ? Color.white : colors[Mathf.Clamp(index, 0, colors.Length - 1)];
    }
}
