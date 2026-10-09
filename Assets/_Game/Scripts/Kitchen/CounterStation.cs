using Courtyard.Core;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// The shared tools on the counter (kettle, grinder, mug, V60, milk jug, topping jars).
    /// They stay on the counter all level; each minigame step activates the tools it needs and owns only its own gauges.
    /// </summary>
    public sealed class CounterStation : MonoBehaviour
    {
        [Header("Kettle")]
        [SerializeField] private Transform kettle;
        [SerializeField] private PointerTarget kettleTarget;
        [SerializeField] private SpriteRenderer kettleBody;
        [SerializeField] private PointerTarget kettleSwitch;
        [SerializeField] private SpriteRenderer switchLight;
        [SerializeField] private SpriteRenderer kettleStream;
        [SerializeField] private KettleView kettleView;

        [Header("Grinder")]
        [SerializeField] private GameObject grinder;
        [SerializeField] private Transform grinderArm;
        [SerializeField] private DragTarget grinderHandle;

        [Header("Mug")]
        [SerializeField] private PointerTarget mug;
        [SerializeField] private SpriteRenderer mugBody;
        [SerializeField] private SpriteBar mugFill;
        [SerializeField] private Transform mugFillLine;
        [SerializeField] private GameObject mugFoam;
        [SerializeField] private SpriteRenderer[] spoonMarkers;
        [SerializeField] private GameObject mugLeafPrint;
        [SerializeField] private GameObject dripper;
        [Tooltip("V60 art: empty, and with ground coffee (after GrindBeans). Empty = keep the placeholder.")]
        [SerializeField] private SpriteRenderer dripperRenderer;
        [SerializeField] private Sprite dripperEmpty;
        [SerializeField] private Sprite dripperGrounds;
        [Tooltip("Mug art per MugStyle (Chipped, His, Hers). Empty = keep the placeholder.")]
        [SerializeField] private Sprite[] mugSprites;
        [Tooltip("Optional: fill gauge beside the mug (unused once MugContents shows the drink inside).")]
        [SerializeField] private GameObject mugGauge;
        [Tooltip("Drink shown inside the mug (art). Empty = the placeholder fill bar.")]
        [SerializeField] private MugContents mugContents;
        [Tooltip("Drink tiles per MugDrink (Instant, Black, Latte).")]
        [SerializeField] private Sprite[] mugDrinkSprites;

        [Tooltip("Steam above the mug while there is a hot drink in it.")]
        [SerializeField] private ParticleSystem mugSteam;

        [Header("Zones")]
        [Tooltip("Offset from where step prefabs were authored to the manifest zone, indexed by CounterZone. Written by the decor pass.")]
        [SerializeField] private Vector2[] zoneShift;

        [Header("Milk")]
        [SerializeField] private GameObject milkStation;
        [SerializeField] private PointerTarget milkJug;
        [SerializeField] private SpriteRenderer milkJugBody;
        [SerializeField] private SpriteBar milkFoam;

        [Header("Milk art (optional)")]
        [Tooltip("Hand frother over the jug; shakes while frothing.")]
        [SerializeField] private Transform frother;
        [SerializeField] private SpriteRenderer frotherButton;
        [Tooltip("Foam that rises from the jug's rim; swells into a spill when overheated.")]
        [SerializeField] private Transform milkFoamBlob;
        [SerializeField] private float foamMinScale = 0.15f;
        [SerializeField] private float foamMaxScale = 0.95f;
        [SerializeField, Min(0f)] private float frothJitter = 0.025f;
        [SerializeField] private Color buttonIdle = Color.white;
        [SerializeField] private Color buttonOn = new(1f, 0.8f, 0.55f);

        [Header("Toppings")]
        [SerializeField] private GameObject toppingShelf;
        [SerializeField] private ToppingJar[] toppingJars;

        private Vector3 _kettleRest;
        private Vector3 _frotherRest;
        private Vector3 _foamBase;
        private bool _frothing;
        private float _groundsLevel;

        public Transform Kettle => kettle;
        public Vector3 KettleRest => _kettleRest;
        public PointerTarget KettleTarget => kettleTarget;
        public SpriteRenderer KettleBody => kettleBody;
        public PointerTarget KettleSwitch => kettleSwitch;
        public SpriteRenderer SwitchLight => switchLight;
        public SpriteRenderer KettleStream => kettleStream;
        public KettleView KettleView => kettleView;
        public Transform GrinderArm => grinderArm;
        public DragTarget GrinderHandle => grinderHandle;
        public PointerTarget Mug => mug;
        public bool IsOverMug(Vector2 world) =>
            mugBody.bounds.Contains(new Vector3(world.x, world.y, mugBody.bounds.center.z));
        public float MugLevel => mugFill.Value;
        public Transform MugFillLine => mugFillLine;
        public GameObject MugFoam => mugFoam;
        public SpriteRenderer[] SpoonMarkers => spoonMarkers;
        public Transform Dripper => dripper.transform;
        public PointerTarget MilkJug => milkJug;
        public SpriteRenderer MilkJugBody => milkJugBody;
        public SpriteBar MilkFoam => milkFoam;
        public ToppingJar[] ToppingJars => toppingJars;

        public bool HasDrink => mugFill.Value > 0.05f || mugFoam.activeSelf;

        private void Awake()
        {
            _kettleRest = kettle.position;
            if (frother != null) _frotherRest = frother.localPosition;
            if (milkFoamBlob != null) _foamBase = milkFoamBlob.localPosition;
        }

        private void Update()
        {
            var emission = mugSteam.emission;
            emission.enabled = HasDrink;

            if (frother != null)
            {
                frother.localPosition = _frothing
                    ? _frotherRest + (Vector3)(Random.insideUnitCircle * frothJitter)
                    : _frotherRest;
            }
        }

        /// <summary>Shows the tools this day uses and puts everything back to a clean, inactive state.</summary>
        public void Prepare(LevelDefinition level)
        {
            var props = level.Props;
            grinder.SetActive(props.grinder);
            dripper.SetActive(HasStep<Minigames.PourOverStep>(level)); // only on days that actually pour over
            milkStation.SetActive(props.milk);
            toppingShelf.SetActive(props.toppings);
            var style = (int)props.brewMug;
            var hasArt = mugSprites != null && style < mugSprites.Length && mugSprites[style] != null;
            if (hasArt) mugBody.sprite = mugSprites[style];
            mugLeafPrint.SetActive(!hasArt && props.brewMug == MugStyle.Hers);
            SetMugGauge(false);
            SetMugLevel(0f);
            SetMugDrink(MugDrink.Instant, Color.white);
            SetDripperGrounds(false);

            kettle.position = _kettleRest;
            kettleStream.enabled = false;
            kettleView.Prepare(props.sweater);
            mugFill.SetValue(0f);
            mugFillLine.gameObject.SetActive(false);
            mugFoam.SetActive(false);
            SetMilkFoam(0f);
            SetFrothing(false);
            foreach (var marker in spoonMarkers) marker.enabled = false;
            foreach (var jar in toppingJars) jar.SetPicked(false);

            SetAllInteractable(false);
        }

        public ParticleSystem MugSteam => mugSteam;

        public Vector2 ZoneShift(CounterZone zone)
        {
            var index = (int)zone;
            return zoneShift != null && index < zoneShift.Length ? zoneShift[index] : Vector2.zero;
        }

        /// <summary>Mug fill 0..1 (drives the drink inside the mug, or the placeholder bar).</summary>
        public void SetMugLevel(float level)
        {
            mugFill.SetValue(level);
            if (level <= 0f) _groundsLevel = 0f;
            if (mugContents != null) mugContents.SetLevel(Mathf.Max(level, _groundsLevel));
        }

        /// <summary>Spoonfuls of instant coffee in the empty mug: grounds inside the mug (or the placeholder dots).</summary>
        public void ShowSpoons(int spoons)
        {
            if (mugContents == null)
            {
                for (var i = 0; i < spoonMarkers.Length; i++) spoonMarkers[i].enabled = i < spoons;
                return;
            }

            _groundsLevel = Mathf.Min(0.24f, spoons * 0.08f);
            if (MugLevel <= 0f)
            {
                SetMugDrink(MugDrink.Instant, mugFill.Fill.color);
                mugContents.SetLevel(_groundsLevel);
            }
        }

        /// <summary>What's in the mug; <paramref name="placeholderColour"/> tints the placeholder bar when there's no art.</summary>
        public void SetMugDrink(MugDrink drink, Color placeholderColour)
        {
            var index = (int)drink;
            if (mugContents != null && mugDrinkSprites != null && index < mugDrinkSprites.Length)
                mugContents.SetSprite(mugDrinkSprites[index]);
            mugFill.Fill.color = placeholderColour;
        }

        /// <summary>The served drink (e.g. cinnamon latte) shown inside a full mug.</summary>
        public void ShowServedDrink(Sprite drink)
        {
            if (mugContents == null || drink == null) return;
            mugContents.SetSprite(drink);
            mugContents.SetLevel(1f);
        }

        /// <summary>Target line drawn across the mug at <paramref name="level"/> (0..1).</summary>
        public void ShowFillLine(float level)
        {
            mugFillLine.gameObject.SetActive(true);
            mugFillLine.localPosition = new Vector3(mugFillLine.localPosition.x, level * mugFill.MaxHeight, 0f);
        }

        public void HideFillLine() => mugFillLine.gameObject.SetActive(false);

        /// <summary>Foam level 0..1: the drawn foam rises from the rim (or the placeholder bar fills).</summary>
        public void SetMilkFoam(float level)
        {
            if (milkFoamBlob == null)
            {
                milkFoam.SetValue(level);
                return;
            }

            milkFoamBlob.gameObject.SetActive(level > 0.02f);
            var scale = Mathf.Lerp(foamMinScale, foamMaxScale, Mathf.Clamp01(level));
            milkFoamBlob.localScale = new Vector3(scale, scale, 1f);
            var half = milkFoamBlob.GetComponent<SpriteRenderer>().sprite.bounds.extents.y;
            milkFoamBlob.localPosition = _foamBase + Vector3.up * (half * scale * 0.55f);
        }

        public void SetFrothing(bool frothing)
        {
            _frothing = frothing;
            if (frotherButton != null) frotherButton.color = frothing ? buttonOn : buttonIdle;
        }

        /// <summary>Ground coffee in the V60 (after grinding).</summary>
        public void SetDripperGrounds(bool hasGrounds)
        {
            var sprite = hasGrounds ? dripperGrounds : dripperEmpty;
            if (dripperRenderer != null && sprite != null) dripperRenderer.sprite = sprite;
        }

        /// <summary>Shows the fill gauge (and its target line) while a pouring step runs.</summary>
        public void SetMugGauge(bool visible)
        {
            if (mugGauge != null) mugGauge.SetActive(visible);
        }

        private static bool HasStep<T>(LevelDefinition level) where T : Minigames.MinigameStep
        {
            foreach (var step in level.Steps)
                if (step is T) return true;
            return false;
        }

        public void SetAllInteractable(bool value)
        {
            kettleTarget.Interactable = value;
            kettleSwitch.Interactable = value;
            grinderHandle.Interactable = value;
            mug.Interactable = value;
            milkJug.Interactable = value;
            foreach (var jar in toppingJars) jar.Target.Interactable = value;
        }
    }
}
