using System.Collections.Generic;
using System.Linq;
using Courtyard.Core;
using Courtyard.Minigames;
using UnityEngine;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// Placeholder prefabs for every minigame step. Shared tools (kettle, grinder, mug, milk jug, topping jars) live on the
    /// counter in the Kitchen scene (see <see cref="CounterLayout"/>); a step prefab holds only its own gauges and extras,
    /// placed next to the tool it uses.
    /// </summary>
    internal static class MinigamePrefabBuilder
    {
        private const string Folder = "Assets/_Game/Prefabs/Minigames/";

        public const string KettleBoil = "KettleBoil";
        public const string InstantCoffee = "InstantCoffee";
        public const string GrindBeans = "GrindBeans";
        public const string PourOver = "PourOver";
        public const string SteamMilk = "SteamMilk";
        public const string ChooseMug = "ChooseMug";
        public const string GuessHerDrink = "GuessHerDrink";
        public const string HerDrinkFromMemory = "HerDrinkFromMemory";
        public const string WaveBack = "WaveBack";
        public const string MiraVisit = "MiraVisit";

        private static readonly Color Coffee = new(0.35f, 0.2f, 0.1f);
        private static readonly Color Water = new(0.75f, 0.85f, 0.95f);
        private static readonly Color Green = new(0.4f, 0.8f, 0.4f, 0.55f);

        private static Recipe _miraUsual;

        public static Dictionary<string, MinigameStep> BuildAll(Recipe miraUsual, bool overwrite)
        {
            _miraUsual = miraUsual;
            var builders = new (string name, System.Func<GameObject> build)[]
            {
                (KettleBoil, BuildKettleBoil),
                (InstantCoffee, BuildInstantCoffee),
                (GrindBeans, BuildGrindBeans),
                (PourOver, BuildPourOver),
                (SteamMilk, BuildSteamMilk),
                (ChooseMug, BuildChooseMug),
                (GuessHerDrink, () => BuildPickToppings(GuessHerDrink, "Pick two toppings she'd like. Then click the mug.")),
                (HerDrinkFromMemory, () => BuildPickToppings(HerDrinkFromMemory, "Make her usual. Pick two toppings, then click the mug.")),
                (WaveBack, BuildWaveBack),
                (MiraVisit, BuildMiraVisit),
            };

            return builders.ToDictionary(
                b => b.name,
                b => LoadOrCreatePrefab(Folder + b.name + ".prefab", b.build, overwrite).GetComponent<MinigameStep>());
        }

        private static GameObject BuildKettleBoil()
        {
            var root = new GameObject(KettleBoil);
            var step = root.AddComponent<KettleBoilStep>();
            var (band, needle) = Thermometer(root.transform, new Vector2(CounterLayout.KettleX + 1.1f, CounterLayout.Top));

            Wire(step, "hintText", "Click the kettle switch. Click again when the needle is in the green.");
            Wire(step, "needle", needle);
            Wire(step, "greenBand", band);
            Wire(step, "scaleHeight", CounterLayout.GaugeHeight);
            return root;
        }

        private static GameObject BuildInstantCoffee()
        {
            var root = new GameObject(InstantCoffee);
            var step = root.AddComponent<InstantCoffeeStep>();

            // Day 1 has no grinder, so the instant coffee jar and spoon use the grinder's spot.
            var jar = Box(root.transform, "Jar", new Vector2(CounterLayout.GrinderX, -2.2f), new Vector2(1f, 1.2f), new Color(0.55f, 0.4f, 0.3f), 0, true);
            var spoonRenderer = Box(root.transform, "Spoon", new Vector2(CounterLayout.GrinderX + 1.6f, -2.65f), new Vector2(1.1f, 0.16f), Gray(0.8f), 5);
            spoonRenderer.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(1.3f, 0.6f); // easier to grab
            var spoon = spoonRenderer.gameObject.AddComponent<DragTarget>();
            var spoonLoad = Box(spoonRenderer.transform, "Load", new Vector2(0.4f, 0.1f), new Vector2(0.3f, 0.22f), Coffee, 6);

            Wire(step, "hintText", "Drag two spoons of coffee into the mug. Then hold the kettle to pour to the line.");
            Wire(step, "spoon", spoon);
            Wire(step, "spoonLoad", spoonLoad);
            Wire(step, "jar", jar.GetComponent<Collider2D>());
            return root;
        }

        private static GameObject BuildGrindBeans()
        {
            var root = new GameObject(GrindBeans);
            var step = root.AddComponent<GrindBeansStep>();

            var meter = Bar(root.transform, "GrindMeter", new Vector2(CounterLayout.GrinderX + 1.5f, CounterLayout.Top), 0.4f,
                CounterLayout.GaugeHeight, new Color(0.45f, 0.3f, 0.2f), Gray(0.2f), 0);
            var zone = Box(meter.transform, "TargetZone", Vector2.zero, new Vector2(0.55f, 0.2f), Green, 3);

            Wire(step, "hintText", "Turn the grinder handle in circles. Let go when the meter is in the green.");
            Wire(step, "meter", meter);
            Wire(step, "targetZone", zone);
            return root;
        }

        private static GameObject BuildPourOver()
        {
            var root = new GameObject(PourOver);
            var step = root.AddComponent<PourOverStep>();

            var zone = Empty(root.transform, "PourZone", new Vector2(CounterLayout.MugX, -1.0f));
            zone.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(3.4f, 3.4f);
            var pourZone = zone.gameObject.AddComponent<PointerTarget>();
            WireEnum(pourZone, "handPose", HandPose.Grip);

            var flow = Bar(root.transform, "FlowMeter", new Vector2(CounterLayout.MugX + 1.3f, CounterLayout.Top), 0.4f,
                CounterLayout.GaugeHeight, Water, Gray(0.2f), 0);
            var band = Box(flow.transform, "FlowBand", Vector2.zero, new Vector2(0.55f, 0.2f), Green, 3);

            Wire(step, "hintText", "Hold over the dripper and pour in slow circles. Keep the flow in the green. Fill to the line.");
            Wire(step, "pourZone", pourZone);
            Wire(step, "flowMeter", flow);
            Wire(step, "flowBand", band);
            return root;
        }

        private static GameObject BuildSteamMilk()
        {
            var root = new GameObject(SteamMilk);
            var step = root.AddComponent<SteamMilkStep>();
            var (band, needle) = Thermometer(root.transform, new Vector2(CounterLayout.MilkX + 0.9f, CounterLayout.Top));

            Wire(step, "hintText", "Hold the milk jug to steam it. Let go in the green.");
            Wire(step, "needle", needle);
            Wire(step, "greenBand", band);
            Wire(step, "scaleHeight", CounterLayout.GaugeHeight);
            return root;
        }

        private static GameObject BuildChooseMug()
        {
            var root = new GameObject(ChooseMug);
            var step = root.AddComponent<ChooseMugStep>();

            var colors = new[] { new Color(0.55f, 0.65f, 0.75f), new Color(0.6f, 0.72f, 0.6f), CounterLayout.HerMugColor };
            var mugs = new Object[colors.Length];
            for (var i = 0; i < colors.Length; i++)
            {
                var mug = Box(root.transform, $"Mug{i}", new Vector2(CounterLayout.ServeX - 1f + 1f * i, -2.15f), new Vector2(0.9f, 1.1f), colors[i], 0, true);
                mugs[i] = mug.gameObject.AddComponent<PointerTarget>();
            }

            CounterLayout.LeafPrint(((PointerTarget)mugs[2]).transform, "Props");

            Wire(step, "hintText", "Which one is her mug?");
            WireEnum(step, "warmthTarget", WarmthTarget.Mira);
            WireArray(step, "mugs", mugs);
            Wire(step, "herMugIndex", 2);
            return root;
        }

        private static GameObject BuildPickToppings(string name, string hint)
        {
            var root = new GameObject(name);
            var step = root.AddComponent<PickToppingsStep>();
            Wire(step, "hintText", hint);
            Wire(step, "recipe", _miraUsual);
            return root;
        }

        private static GameObject BuildWaveBack()
        {
            var root = new GameObject(WaveBack);
            var step = root.AddComponent<WaveBackStep>();
            Wire(step, "hintText", "She's waving! Click your mug to wave back.");
            Wire(step, "scored", false);
            return root;
        }

        private static GameObject BuildMiraVisit()
        {
            var root = new GameObject(MiraVisit);
            var step = root.AddComponent<MiraVisitStep>();
            Wire(step, "hintText", "");
            Wire(step, "scored", false);
            return root;
        }

        /// <summary>Thermometer whose origin is the bottom of the scale.</summary>
        private static (SpriteRenderer band, Transform needle) Thermometer(Transform parent, Vector2 bottom)
        {
            var thermometer = Empty(parent, "Thermometer", bottom);
            var height = CounterLayout.GaugeHeight;
            Box(thermometer, "Scale", new Vector2(0f, height * 0.5f), new Vector2(0.4f, height), Gray(0.2f), 0);
            var band = Box(thermometer, "GreenBand", Vector2.zero, new Vector2(0.4f, 0.2f), new Color(0.4f, 0.8f, 0.4f), 1);
            var needle = Box(thermometer, "Needle", Vector2.zero, new Vector2(0.8f, 0.08f), new Color(0.9f, 0.25f, 0.2f), 2);
            return (band, needle.transform);
        }
    }
}
