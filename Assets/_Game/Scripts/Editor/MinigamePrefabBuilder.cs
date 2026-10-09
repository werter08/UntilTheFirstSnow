using System.Collections.Generic;
using System.Linq;
using Courtyard.Core;
using Courtyard.Minigames;
using UnityEngine;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>Placeholder prefabs for every minigame step (grey boxes until real art exists).</summary>
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
        private static readonly Color Leaf = new(0.95f, 0.55f, 0.2f);
        private static readonly Color HerMugColor = new(0.93f, 0.9f, 0.82f);

        private static Recipe _miraUsual;

        public static Dictionary<string, MinigameStep> BuildAll(Recipe miraUsual)
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
                (GuessHerDrink, () => BuildPickToppings(GuessHerDrink, "Pick two toppings she'd like. Then click her mug.")),
                (HerDrinkFromMemory, () => BuildPickToppings(HerDrinkFromMemory, "Make her usual. Pick two toppings, then click her mug.")),
                (WaveBack, BuildWaveBack),
                (MiraVisit, BuildMiraVisit),
            };

            return builders.ToDictionary(
                b => b.name,
                b => LoadOrCreatePrefab(Folder + b.name + ".prefab", b.build).GetComponent<MinigameStep>());
        }

        // ---------------------------------------------------------------- Day 1

        private static GameObject BuildKettleBoil()
        {
            var root = new GameObject(KettleBoil);
            var step = root.AddComponent<KettleBoilStep>();

            Box(root.transform, "KettleBody", new Vector2(-3f, -1.3f), new Vector2(1.6f, 2f), Gray(0.7f), 0);
            var switchRenderer = Box(root.transform, "Switch", new Vector2(-3f, -2.55f), new Vector2(0.8f, 0.4f), Gray(0.35f), 1, true);
            var kettleSwitch = switchRenderer.gameObject.AddComponent<PointerTarget>();
            var (thermometer, band, needle) = Thermometer(root.transform, new Vector2(-1.5f, -2.4f));

            Wire(step, "hintText", "Click the switch. Click again when the needle is in the green.");
            Wire(step, "kettleSwitch", kettleSwitch);
            Wire(step, "switchLight", switchRenderer);
            Wire(step, "thermometer", thermometer);
            Wire(step, "needle", needle);
            Wire(step, "greenBand", band);
            Wire(step, "scaleHeight", 3f);
            return root;
        }

        private static GameObject BuildInstantCoffee()
        {
            var root = new GameObject(InstantCoffee);
            var step = root.AddComponent<InstantCoffeeStep>();

            var jar = Box(root.transform, "Jar", new Vector2(-5f, -1.6f), new Vector2(1.1f, 1.4f), new Color(0.55f, 0.4f, 0.3f), 0, true);
            var mug = Box(root.transform, "Mug", new Vector2(0f, -1.7f), new Vector2(1.4f, 1.6f), new Color(0.9f, 0.88f, 0.82f), 0, true);
            var fillBar = Empty(root.transform, "MugFill", new Vector2(0f, -2.35f));
            var fill = Box(fillBar, "Fill", Vector2.zero, new Vector2(1.2f, 0f), Water, 1);
            var spriteBar = fillBar.gameObject.AddComponent<Kitchen.SpriteBar>();
            Wire(spriteBar, "fill", fill);
            Wire(spriteBar, "width", 1.2f);
            Wire(spriteBar, "maxHeight", 1.3f);
            var fillLine = Box(fillBar, "FillLine", new Vector2(0f, 1.1f), new Vector2(1.3f, 0.05f), Color.white, 3);

            var markers = new Object[3];
            for (var i = 0; i < markers.Length; i++)
                markers[i] = Box(root.transform, $"SpoonMarker{i}", new Vector2(-0.35f + 0.35f * i, -2.2f), new Vector2(0.22f, 0.22f), Coffee, 2);

            var spoonRenderer = Box(root.transform, "Spoon", new Vector2(-3.2f, -2.6f), new Vector2(1.2f, 0.18f), Gray(0.8f), 5);
            spoonRenderer.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(1.4f, 0.6f); // easier to grab
            var spoon = spoonRenderer.gameObject.AddComponent<DragTarget>();
            var spoonLoad = Box(spoonRenderer.transform, "Load", new Vector2(0.45f, 0.1f), new Vector2(0.3f, 0.22f), Coffee, 6);

            var kettleBody = Box(root.transform, "Kettle", new Vector2(3.2f, -1.3f), new Vector2(1.6f, 2f), Gray(0.6f), 0, true);
            var kettle = kettleBody.gameObject.AddComponent<PointerTarget>();

            Wire(step, "hintText", "Drag two spoons of coffee into the mug. Then hold the kettle to pour to the line.");
            Wire(step, "spoon", spoon);
            Wire(step, "spoonLoad", spoonLoad);
            Wire(step, "jar", jar.GetComponent<Collider2D>());
            Wire(step, "mug", mug.GetComponent<Collider2D>());
            WireArray(step, "spoonMarkers", markers);
            Wire(step, "mugFill", spriteBar);
            Wire(step, "fillLine", fillLine.transform);
            Wire(step, "kettle", kettle);
            Wire(step, "kettleBody", kettleBody);
            return root;
        }

        // ---------------------------------------------------------------- M2 steps

        private static GameObject BuildGrindBeans()
        {
            var root = new GameObject(GrindBeans);
            var step = root.AddComponent<GrindBeansStep>();

            Box(root.transform, "Body", new Vector2(-2f, -1.7f), new Vector2(1.3f, 1.8f), new Color(0.5f, 0.36f, 0.25f), 0);
            Box(root.transform, "Hopper", new Vector2(-2f, -0.55f), new Vector2(1f, 0.5f), Gray(0.55f), 0);
            var arm = Empty(root.transform, "Arm", new Vector2(-2f, -0.3f));
            Box(arm, "ArmBar", new Vector2(0.6f, 0f), new Vector2(1.2f, 0.12f), Gray(0.3f), 2);
            var knob = Box(arm, "Knob", new Vector2(1.2f, 0f), new Vector2(0.45f, 0.45f), new Color(0.75f, 0.3f, 0.25f), 3);
            knob.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f);
            var handle = knob.gameObject.AddComponent<DragTarget>();

            var meter = Bar(root.transform, "GrindMeter", new Vector2(0.8f, -2.5f), 0.4f, 3f, new Color(0.45f, 0.3f, 0.2f), Gray(0.2f), 0);
            var zone = Box(meter.transform, "TargetZone", Vector2.zero, new Vector2(0.55f, 0.2f), Green, 3);

            Wire(step, "hintText", "Turn the handle in circles. Let go when the meter is in the green.");
            Wire(step, "handle", handle);
            Wire(step, "arm", arm);
            Wire(step, "meter", meter);
            Wire(step, "targetZone", zone);
            return root;
        }

        private static GameObject BuildPourOver()
        {
            var root = new GameObject(PourOver);
            var step = root.AddComponent<PourOverStep>();

            Box(root.transform, "Mug", new Vector2(0f, -2.05f), new Vector2(1.6f, 1.3f), new Color(0.9f, 0.88f, 0.82f), 0);
            var fill = Bar(root.transform, "MugFill", new Vector2(0f, -2.6f), 1.4f, 1.15f, Coffee, new Color(0.85f, 0.83f, 0.78f), 1);
            var fillLine = Box(fill.transform, "FillLine", new Vector2(0f, 0.9f), new Vector2(1.5f, 0.05f), Color.white, 4);
            var dripper = Box(root.transform, "V60", new Vector2(0f, -0.95f), new Vector2(1.7f, 0.9f), new Color(0.95f, 0.95f, 0.97f), 2);

            var zone = Empty(root.transform, "PourZone", new Vector2(0f, -0.4f));
            zone.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(4.5f, 3.6f);
            var pourZone = zone.gameObject.AddComponent<PointerTarget>();

            var kettle = Empty(root.transform, "KettleSpout", new Vector2(3.4f, 0.4f));
            Box(kettle, "KettleBody", new Vector2(0.8f, 0.55f), new Vector2(1.3f, 1.2f), Gray(0.65f), 6);
            Box(kettle, "Spout", new Vector2(0.15f, 0.25f), new Vector2(0.5f, 0.12f), Gray(0.5f), 6);
            var stream = Box(kettle, "Stream", new Vector2(0f, -0.55f), new Vector2(0.08f, 1.1f), Water, 5);

            var flow = Bar(root.transform, "FlowMeter", new Vector2(-2.6f, -2.5f), 0.4f, 3f, Water, Gray(0.2f), 0);
            var band = Box(flow.transform, "FlowBand", Vector2.zero, new Vector2(0.55f, 0.2f), Green, 3);

            Wire(step, "hintText", "Hold over the dripper and pour in slow circles. Keep the flow in the green. Fill to the line.");
            Wire(step, "pourZone", pourZone);
            Wire(step, "dripperCenter", dripper.transform);
            Wire(step, "kettle", kettle);
            Wire(step, "stream", stream);
            Wire(step, "fill", fill);
            Wire(step, "fillLine", fillLine.transform);
            Wire(step, "flowMeter", flow);
            Wire(step, "flowBand", band);
            return root;
        }

        private static GameObject BuildSteamMilk()
        {
            var root = new GameObject(SteamMilk);
            var step = root.AddComponent<SteamMilkStep>();

            Box(root.transform, "Wand", new Vector2(0f, 0.2f), new Vector2(0.15f, 1.6f), Gray(0.3f), 0);
            var jugBody = Box(root.transform, "Jug", new Vector2(0f, -1.6f), new Vector2(1.4f, 1.8f), new Color(0.8f, 0.82f, 0.85f), 1, true);
            var jug = jugBody.gameObject.AddComponent<PointerTarget>();
            var foamBar = Empty(root.transform, "Foam", new Vector2(0f, -2.4f));
            var foamFill = Box(foamBar, "Fill", Vector2.zero, new Vector2(1.1f, 0f), new Color(0.98f, 0.95f, 0.88f), 2);
            var foam = foamBar.gameObject.AddComponent<Kitchen.SpriteBar>();
            Wire(foam, "fill", foamFill);
            Wire(foam, "width", 1.1f);
            Wire(foam, "maxHeight", 1.5f);
            var (_, band, needle) = Thermometer(root.transform, new Vector2(1.6f, -2.4f));

            Wire(step, "hintText", "Hold the jug to steam the milk. Let go in the green.");
            Wire(step, "jug", jug);
            Wire(step, "jugBody", jugBody);
            Wire(step, "foam", foam);
            Wire(step, "needle", needle);
            Wire(step, "greenBand", band);
            Wire(step, "scaleHeight", 3f);
            return root;
        }

        private static GameObject BuildChooseMug()
        {
            var root = new GameObject(ChooseMug);
            var step = root.AddComponent<ChooseMugStep>();

            var colors = new[] { new Color(0.55f, 0.65f, 0.75f), new Color(0.6f, 0.72f, 0.6f), HerMugColor };
            var mugs = new Object[colors.Length];
            for (var i = 0; i < colors.Length; i++)
            {
                var mug = Box(root.transform, $"Mug{i}", new Vector2(-2f + 2f * i, -1.7f), new Vector2(1.2f, 1.4f), colors[i], 0, true);
                mugs[i] = mug.gameObject.AddComponent<PointerTarget>();
            }

            LeafPrint(((PointerTarget)mugs[2]).transform);

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

            var toppings = new[]
            {
                (Topping.Cinnamon, new Color(0.65f, 0.35f, 0.2f)),
                (Topping.Honey, new Color(0.95f, 0.7f, 0.2f)),
                (Topping.Cocoa, new Color(0.35f, 0.22f, 0.15f)),
                (Topping.Salt, new Color(0.92f, 0.92f, 0.95f)),
            };

            var options = new List<(Topping, PointerTarget, GameObject)>();
            for (var i = 0; i < toppings.Length; i++)
            {
                var (topping, color) = toppings[i];
                var jar = Box(root.transform, topping.ToString(), new Vector2(-5f + 1.5f * i, -1.7f), new Vector2(1f, 1.3f), color, 0, true);
                WorldText(jar.transform, "Label", new Vector2(0f, 1.0f), topping.ToString(), 3f, Gray(0.15f), "Props", 2);
                var marker = Box(jar.transform, "Picked", new Vector2(0f, -0.85f), new Vector2(0.9f, 0.15f), Leaf, 2);
                options.Add((topping, jar.gameObject.AddComponent<PointerTarget>(), marker.gameObject));
            }

            var herMug = Box(root.transform, "HerMug", new Vector2(2.5f, -1.7f), new Vector2(1.2f, 1.4f), HerMugColor, 0, true);
            LeafPrint(herMug.transform);
            var serve = herMug.gameObject.AddComponent<PointerTarget>();

            Wire(step, "hintText", hint);
            Wire(step, "recipe", _miraUsual);
            Wire(step, "serveMug", serve);
            Edit(step, so =>
            {
                var array = Property(so, "options");
                array.arraySize = options.Count;
                for (var i = 0; i < options.Count; i++)
                {
                    var element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("topping").enumValueIndex = (int)options[i].Item1;
                    element.FindPropertyRelative("target").objectReferenceValue = options[i].Item2;
                    element.FindPropertyRelative("pickedMarker").objectReferenceValue = options[i].Item3;
                }
            });
            return root;
        }

        private static GameObject BuildWaveBack()
        {
            var root = new GameObject(WaveBack);
            var step = root.AddComponent<WaveBackStep>();

            var mug = Box(root.transform, "Mug", new Vector2(1f, -1.8f), new Vector2(1f, 1.2f), new Color(0.9f, 0.88f, 0.82f), 0, true);
            var target = mug.gameObject.AddComponent<PointerTarget>();

            Wire(step, "hintText", "She's waving! Click your mug to wave back.");
            Wire(step, "scored", false);
            Wire(step, "raiseMug", target);
            Wire(step, "mug", mug.transform);
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

        // ---------------------------------------------------------------- pieces

        private static (Transform thermometer, SpriteRenderer band, Transform needle) Thermometer(Transform parent, Vector2 bottom)
        {
            var thermometer = Empty(parent, "Thermometer", bottom);
            Box(thermometer, "Scale", new Vector2(0f, 1.5f), new Vector2(0.4f, 3f), Gray(0.2f), 0);
            var band = Box(thermometer, "GreenBand", Vector2.zero, new Vector2(0.4f, 0.2f), new Color(0.4f, 0.8f, 0.4f), 1);
            var needle = Box(thermometer, "Needle", Vector2.zero, new Vector2(0.8f, 0.08f), new Color(0.9f, 0.25f, 0.2f), 2);
            return (thermometer, band, needle.transform);
        }

        private static void LeafPrint(Transform mug)
        {
            var leaf = Box(mug, "LeafPrint", new Vector2(0.15f, 0.1f), new Vector2(0.35f, 0.35f), Leaf, 1);
            leaf.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
    }
}
