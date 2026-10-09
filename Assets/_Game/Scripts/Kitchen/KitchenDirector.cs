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
        [Tooltip("One colour per TreeStage, in enum order.")]
        [SerializeField] private Color[] treeStageColors =
        {
            new(0.62f, 0.7f, 0.3f), new(0.9f, 0.5f, 0.2f), new(0.95f, 0.75f, 0.3f), new(0.7f, 0.45f, 0.3f), new(0.92f, 0.94f, 0.97f),
        };

        [Header("Weather (seen through the window)")]
        [SerializeField] private WeatherPreset[] weatherPresets;

        [Header("Mira's window")]
        [SerializeField] private SpriteRenderer miraWindow;
        [SerializeField] private Color miraWindowDark = new(0.2f, 0.22f, 0.3f);
        [SerializeField] private GameObject miraSilhouette;
        [SerializeField] private Transform miraArm;
        [SerializeField] private GameObject miraNote;
        [SerializeField, Min(0.05f)] private float waveFrameSeconds = 0.35f;
        [SerializeField] private float waveArmAngle = 35f;
        [SerializeField] private GameObject miraAtDoor;

        [Header("Wall")]
        [SerializeField] private TMP_Text calendarDay;
        [SerializeField] private TMP_Text clockTime;

        [Header("Props")]
        [SerializeField] private GameObject beans;
        [SerializeField] private GameObject cinnamon;
        [SerializeField] private GameObject herMug;
        [SerializeField] private GameObject scarf;
        [SerializeField] private GameObject blanket;
        [SerializeField] private SpriteRenderer plant;
        [Tooltip("One colour per PlantStage, in enum order.")]
        [SerializeField] private Color[] plantStageColors =
        {
            new(0.55f, 0.5f, 0.3f), new(0.5f, 0.62f, 0.32f), new(0.35f, 0.65f, 0.35f), new(0.95f, 0.55f, 0.65f),
        };
        [SerializeField] private GameObject[] fridgeNotes;

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

            courtyard.color = level.TimeOfDay switch
            {
                TimeOfDay.Evening => eveningCourtyard,
                TimeOfDay.Night => nightCourtyard,
                _ => morningCourtyard,
            };
            treeCrown.color = PickColor(treeStageColors, (int)level.TreeStage);

            calendarDay.text = level.DayNumber.ToString();
            clockTime.text = level.ClockTime;

            var props = level.Props;
            beans.SetActive(props.beans);
            cinnamon.SetActive(props.cinnamon);
            herMug.SetActive(props.herMug);
            scarf.SetActive(props.scarf);
            blanket.SetActive(props.blanket);
            plant.color = PickColor(plantStageColors, (int)props.plantStage);
            for (var i = 0; i < fridgeNotes.Length; i++)
                fridgeNotes[i].SetActive(i < props.fridgeNotes);

            ApplyWeather(level.Weather);
            SetMiraAtDoor(false);
            SetMiraWindow(level.MiraWindow);
        }

        public void SetMiraWindow(MiraWindowState state)
        {
            _windowState = state;
            var lit = state is MiraWindowState.LitWarm or MiraWindowState.LitWarmSilhouette
                or MiraWindowState.Waving or MiraWindowState.NoteOnGlass;
            var windowColor = _level != null ? _level.Palette.miraWindowColor : Color.white;

            miraWindow.color = lit ? windowColor : miraWindowDark;
            miraLight.enabled = lit;
            miraLight.color = windowColor;
            miraSilhouette.SetActive(state is MiraWindowState.LitWarmSilhouette or MiraWindowState.Waving);
            miraNote.SetActive(state == MiraWindowState.NoteOnGlass);

            _waveTimer = 0f;
            SetArm(false);
        }

        public void SetMiraAtDoor(bool visible) => miraAtDoor.SetActive(visible);

        private void Update()
        {
            if (_windowState != MiraWindowState.Waving) return;

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
                velocity.x = preset.wind;
                preset.system.Simulate(5f, true, true); // start mid-weather, not with an empty sky
                preset.system.Play(true);
            }
        }

        private static Color PickColor(Color[] colors, int index) =>
            colors.Length == 0 ? Color.white : colors[Mathf.Clamp(index, 0, colors.Length - 1)];
    }
}
