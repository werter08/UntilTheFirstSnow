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

        [Header("Milk")]
        [SerializeField] private GameObject milkStation;
        [SerializeField] private PointerTarget milkJug;
        [SerializeField] private SpriteRenderer milkJugBody;
        [SerializeField] private SpriteBar milkFoam;

        [Header("Toppings")]
        [SerializeField] private GameObject toppingShelf;
        [SerializeField] private ToppingJar[] toppingJars;

        private Vector3 _kettleRest;

        public Transform Kettle => kettle;
        public Vector3 KettleRest => _kettleRest;
        public PointerTarget KettleTarget => kettleTarget;
        public SpriteRenderer KettleBody => kettleBody;
        public PointerTarget KettleSwitch => kettleSwitch;
        public SpriteRenderer SwitchLight => switchLight;
        public SpriteRenderer KettleStream => kettleStream;
        public Transform GrinderArm => grinderArm;
        public DragTarget GrinderHandle => grinderHandle;
        public PointerTarget Mug => mug;
        public bool IsOverMug(Vector2 world) =>
            mugBody.bounds.Contains(new Vector3(world.x, world.y, mugBody.bounds.center.z));
        public SpriteBar MugFill => mugFill;
        public Transform MugFillLine => mugFillLine;
        public GameObject MugFoam => mugFoam;
        public SpriteRenderer[] SpoonMarkers => spoonMarkers;
        public Transform Dripper => dripper.transform;
        public PointerTarget MilkJug => milkJug;
        public SpriteRenderer MilkJugBody => milkJugBody;
        public SpriteBar MilkFoam => milkFoam;
        public ToppingJar[] ToppingJars => toppingJars;

        private void Awake() => _kettleRest = kettle.position;

        /// <summary>Shows the tools this day uses and puts everything back to a clean, inactive state.</summary>
        public void Prepare(LevelDefinition level)
        {
            var props = level.Props;
            grinder.SetActive(props.grinder);
            dripper.SetActive(props.grinder); // pour-over days are the days with real beans and a grinder
            milkStation.SetActive(props.milk);
            toppingShelf.SetActive(props.toppings);
            mugLeafPrint.SetActive(props.brewInHerMug);

            kettle.position = _kettleRest;
            kettleStream.enabled = false;
            mugFill.SetValue(0f);
            mugFillLine.gameObject.SetActive(false);
            mugFoam.SetActive(false);
            milkFoam.SetValue(0f);
            foreach (var marker in spoonMarkers) marker.enabled = false;
            foreach (var jar in toppingJars) jar.SetPicked(false);

            SetAllInteractable(false);
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
