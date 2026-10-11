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
        public const string Folder = "Assets/_Game/Prefabs/Minigames/";

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
        public const string TakeHerCup = "TakeHerCup";
        public const string AutoFillHisMug = "AutoFillHisMug";
        public const string PourWater = "PourWater";
        public const string ReadHerWindow = "ReadHerWindow";
        public const string PlaceV60 = "PlaceV60";

        private static readonly Color Coffee = new(0.35f, 0.2f, 0.1f);
        private static readonly Color Water = new(0.75f, 0.85f, 0.95f);
        private static readonly Color Green = new(0.4f, 0.8f, 0.4f, 0.55f);

        // No gauge: the player reads the boil from the kettle itself (rattling lid, sound).
        public const string KettleBoilHint = "Turn the kettle on. Turn it off when it boils."; // final copy lives in GameTexts

        private static Recipe _miraUsual;

        /// <summary>
        /// In-place update of the kettle steps after the kettle art arrived (no gauge, untinted kettle), keeping the
        /// prefabs' file IDs so level assets stay linked. Safe to run more than once.
        /// </summary>
        [UnityEditor.MenuItem("Courtyard/Setup/Patch Art Prefabs")]
        public static void PatchKettlePrefabs()
        {
            DecorBuilder.ConfigureArtImports(); // sprites must have their final IDs before prefabs reference them
            PatchChooseMug();
            PatchZones();
            PatchHints();
            Patch(Folder + KettleBoil + ".prefab", root =>
            {
                var thermometer = root.transform.Find("Thermometer");
                if (thermometer != null) Object.DestroyImmediate(thermometer.gameObject);
                var step = root.GetComponent<KettleBoilStep>();
                Wire(step, "hintText", KettleBoilHint);
                // The lid rattles from 90 °C: give the player ~1.5 s after that to switch off (90–108 is "good").
                Wire(step, "bandMax", 108f);
                Wire(step, "qualityFalloff", 30f);
            });
            LoadOrCreatePrefab(Folder + TakeHerCup + ".prefab", BuildTakeHerCup); // Day 10 at the door
        }

        /// <summary>Day 20 ChooseMug with the drawn mugs: two decoys and hers (the maple leaf, seen on Day 10).</summary>
        private static void PatchChooseMug()
        {
            Sprite Load(string n) => UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Game/Art/Props/{n}.png");
            var sprites = new[] { Load("mug_decoy_1"), Load("mug_decoy_2"), Load("mug_hers") };
            if (sprites.Any(s => s == null)) return;

            const float scale = 0.8f;
            Patch(Folder + ChooseMug + ".prefab", root =>
            {
                for (var i = 0; i < sprites.Length; i++)
                {
                    var mug = root.transform.Find($"Mug{i}");
                    var renderer = mug.GetComponent<SpriteRenderer>();
                    renderer.sprite = sprites[i];
                    renderer.drawMode = SpriteDrawMode.Simple;
                    renderer.color = Color.white;
                    mug.localScale = new Vector3(scale, scale, 1f);
                    mug.localPosition = new Vector3(CounterLayout.ServeX - 1.65f + 1.3f * i, -2.15f, 0f);
                    var collider = mug.GetComponent<BoxCollider2D>();
                    collider.size = sprites[i].bounds.size;
                    collider.offset = sprites[i].bounds.center;
                    var leaf = mug.Find("LeafPrint");
                    if (leaf != null) Object.DestroyImmediate(leaf.gameObject);
                    mug.gameObject.SetActive(false); // the real wall rack is used; these are a fallback only
                }

                Wire(root.GetComponent<ChooseMugStep>(), "herMugIndex", 2);
            });
        }

        public const string InstantCoffeeHint = "Two spoons of coffee into the cup. Then pour water to the line.";
        public const string GuessHerDrinkHint = "Add two spices to her cup, one at a time. Then click the cup.";
        public const string HerUsualHint = "Make her usual. You know it by now.";
        public const string PourOverHint = "Pour in slow circles. Keep the flow in the green. Fill to the line.";

        private static void PatchHints()
        {
            Patch(Folder + InstantCoffee + ".prefab", root => Wire(root.GetComponent<MinigameStep>(), "hintText", InstantCoffeeHint));
            Patch(Folder + GuessHerDrink + ".prefab", root => Wire(root.GetComponent<MinigameStep>(), "hintText", GuessHerDrinkHint));
            Patch(Folder + HerDrinkFromMemory + ".prefab", root => Wire(root.GetComponent<MinigameStep>(), "hintText", HerUsualHint));
            Patch(Folder + PourOver + ".prefab", root => Wire(root.GetComponent<MinigameStep>(), "hintText", PourOverHint));
        }

        /// <summary>Each step's own parts belong to a counter zone and move with it (see KitchenArtLayout).</summary>
        private static void PatchZones()
        {
            var zones = new Dictionary<string, Kitchen.CounterZone>
            {
                [KettleBoil] = Kitchen.CounterZone.Kettle,
                [InstantCoffee] = Kitchen.CounterZone.Grinder, // jar + spoon use the grinder's spot on Day 1
                [GrindBeans] = Kitchen.CounterZone.Grinder,
                [PourOver] = Kitchen.CounterZone.Brew,
                [SteamMilk] = Kitchen.CounterZone.Milk,
                [ChooseMug] = Kitchen.CounterZone.Rack,
            };

            foreach (var (name, zone) in zones)
                Patch(Folder + name + ".prefab", root => WireEnum(root.GetComponent<MinigameStep>(), "zone", zone));

            // Grind meter clear of the drawn crank (it reaches ~1.4 units from the axle).
            Patch(Folder + GrindBeans + ".prefab", root =>
            {
                var meter = root.transform.Find("GrindMeter");
                var back = root.transform.Find("GrindMeterBack");
                if (meter == null || back == null) return; // replaced by the MinigameGauge
                var x = CounterLayout.GrinderX + 1.95f;
                meter.localPosition = new Vector3(x, meter.localPosition.y, 0f);
                back.localPosition = new Vector3(x, back.localPosition.y, 0f);
            });

            // Milk thermometer on the left of the jug, clear of the topping jars; drawn jug untinted (pale red when squealing).
            Patch(Folder + SteamMilk + ".prefab", root =>
            {
                Edit(root.GetComponent<SteamMilkStep>(), so =>
                {
                    so.FindProperty("jugColor").colorValue = Color.white;
                    so.FindProperty("squealColor").colorValue = new Color(1f, 0.78f, 0.75f);
                });
                var thermometer = root.transform.Find("Thermometer");
                if (thermometer != null) thermometer.localPosition = new Vector3(CounterLayout.MilkX - 0.9f, thermometer.localPosition.y, 0f);
            });

            // Day 1: the drawn instant coffee jar the spoon scoops from.
            var instantJar = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Props/jar_instant.png");
            if (instantJar != null)
            {
                Patch(Folder + InstantCoffee + ".prefab", root =>
                {
                    var jar = root.transform.Find("Jar");
                    var renderer = jar.GetComponent<SpriteRenderer>();
                    renderer.sprite = instantJar;
                    renderer.drawMode = SpriteDrawMode.Simple;
                    renderer.color = Color.white;
                    jar.localScale = Vector3.one;
                    jar.localPosition = new Vector3(jar.localPosition.x, CounterLayout.Top + instantJar.bounds.extents.y, 0f);
                    var collider = jar.GetComponent<BoxCollider2D>();
                    collider.size = instantJar.bounds.size;
                    collider.offset = instantJar.bounds.center;
                });
            }

            // The spoon is held in front of everything on the counter. With the drawn spoon (spoon.png /
            // spoon_full.png), it shows the art and swaps to the full spoon after a scoop.
            var spoonArt = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Props/spoon.png");
            var spoonFullArt = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Props/spoon_full.png");
            Patch(Folder + InstantCoffee + ".prefab", root =>
            {
                var spoon = root.transform.Find("Spoon");
                foreach (var r in spoon.GetComponentsInChildren<SpriteRenderer>(true)) r.sortingLayerName = "PropsFront";
                if (spoonArt == null || spoonFullArt == null) return;

                var renderer = spoon.GetComponent<SpriteRenderer>();
                renderer.sprite = spoonArt;
                renderer.drawMode = SpriteDrawMode.Simple;
                renderer.color = Color.white;
                spoon.localScale = Vector3.one;
                var collider = spoon.GetComponent<BoxCollider2D>();
                collider.size = spoonArt.bounds.size + new Vector3(0.2f, 0.2f, 0f); // a little easier to grab
                collider.offset = spoonArt.bounds.center;
                var step = root.GetComponent<InstantCoffeeStep>();
                Wire(step, "spoonEmpty", spoonArt);
                Wire(step, "spoonFull", spoonFullArt);
            });
        }

        private static void Patch(string path, System.Action<GameObject> edit)
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root);
                UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEditor.PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static Dictionary<string, MinigameStep> BuildAll(Recipe miraUsual, bool overwrite)
        {
            _miraUsual = miraUsual;
            EnsurePourWater();
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
                (TakeHerCup, BuildTakeHerCup),
                (AutoFillHisMug, BuildAutoFillHisMug),
                (PourWater, () => throw new System.InvalidOperationException("PourWater is copied from InstantCoffee")),
                (ReadHerWindow, BuildReadHerWindow),
                (PlaceV60, BuildPlaceV60),
            };

            return builders.ToDictionary(
                b => b.name,
                b => LoadOrCreatePrefab(Folder + b.name + ".prefab", b.build, overwrite).GetComponent<MinigameStep>());
        }

        private static GameObject BuildKettleBoil()
        {
            var root = new GameObject(KettleBoil);
            var step = root.AddComponent<KettleBoilStep>();
            Wire(step, "hintText", KettleBoilHint);
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

            Wire(step, "hintText", InstantCoffeeHint);
            Wire(step, "spoon", spoon);
            Wire(step, "spoonLoad", spoonLoad);
            Wire(step, "jar", jar.GetComponent<Collider2D>());
            return root;
        }

        private static GameObject BuildGrindBeans()
        {
            var root = new GameObject(GrindBeans);
            var step = root.AddComponent<GrindBeansStep>();

            // The meter is the shared MinigameGauge (screen space, above the grinder).

            Wire(step, "hintText", "Turn the grinder handle in circles. Let go when the meter is in the green.");
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


            Wire(step, "hintText", PourOverHint);
            Wire(step, "pourZone", pourZone);
            return root;
        }

        private static GameObject BuildSteamMilk()
        {
            var root = new GameObject(SteamMilk);
            var step = root.AddComponent<SteamMilkStep>();

            Wire(step, "hintText", "Hold the milk jug to steam it. Let go in the green.");
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

        private static GameObject BuildTakeHerCup()
        {
            var root = new GameObject(TakeHerCup);
            var step = root.AddComponent<TakeHerCupStep>();
            Wire(step, "hintText", "Take her cup.");
            Wire(step, "scored", false);
            return root;
        }

        /// <summary>
        /// Her coffee (Days 10 and 30): one spoon of coffee into her cup — while the kettle heats, like Day 1 — then the
        /// hot water to the line. Copied once from InstantCoffee (same spoon and kettle handling), settings kept here.
        /// </summary>
        private const float HerJarShiftX = 1.45f; // into the coffee bag's spot (the bag steps aside)

        private static void EnsurePourWater()
        {
            var path = Folder + PourWater + ".prefab";
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                UnityEditor.AssetDatabase.CopyAsset(Folder + InstantCoffee + ".prefab", path);
            Patch(path, root =>
            {
                root.name = PourWater;
                var step = root.GetComponent<InstantCoffeeStep>();
                Wire(step, "useSpoon", true);
                Wire(step, "targetSpoons", 1);
                Wire(step, "startsWithPrevious", true);
                Wire(step, "hintText", "One spoon of coffee into her cup. Then pour water to the line.");
                Wire(step, "tooMuchCoffeeText", "Too much coffee — she takes just one spoon.");
                Wire(step, "tooLittleCoffeeText", "No coffee in it — one spoon.");
                Wire(step, "hideCoffeeBag", true);

                // Days 10 and 30 have the grinder where Day 1's jar stands: her jar goes in the gap between the
                // grinder and the coffee bag (always measured from the Day 1 jar, so re-running doesn't drift).
                var day1Jar = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Folder + InstantCoffee + ".prefab").transform.Find("Jar");
                var jar = root.transform.Find("Jar");
                if (day1Jar != null && jar != null) jar.localPosition = day1Jar.localPosition + new Vector3(HerJarShiftX, 0f, 0f);
            });
            // Day 1: spoon the coffee in while the kettle heats.
            Patch(Folder + InstantCoffee + ".prefab", root => Wire(root.GetComponent<MinigameStep>(), "startsWithPrevious", true));

            // The spoon counter sits on the light counter: dark ink, a touch bigger.
            foreach (var name in new[] { InstantCoffee, PourWater })
                Patch(Folder + name + ".prefab", root => Edit(root.GetComponent<InstantCoffeeStep>(), so =>
                {
                    Property(so, "countColour").colorValue = new Color(0.24f, 0.16f, 0.12f);
                    Property(so, "countDoneColour").colorValue = new Color(0.16f, 0.45f, 0.14f);
                    Property(so, "countOverColour").colorValue = new Color(0.7f, 0.16f, 0.1f);
                }));
        }

        /// <summary>Pour-over days: carry the V60 from the sill onto the cup (unscored).</summary>
        private static GameObject BuildPlaceV60()
        {
            var root = new GameObject(PlaceV60);
            var step = root.AddComponent<PlaceV60Step>();
            Wire(step, "hintText", "Put the V60 on the cup.");
            Wire(step, "scored", false);
            return root;
        }

        /// <summary>Day 20: her note on the window — tap it to read (unscored).</summary>
        private static GameObject BuildReadHerWindow()
        {
            var root = new GameObject(ReadHerWindow);
            var step = root.AddComponent<ReadHerWindowStep>();
            Wire(step, "hintText", "Something's on her window. Tap it.");
            Wire(step, "scored", false);
            Wire(step, "alwaysShowHint", true);
            return root;
        }

        /// <summary>Day 20: his cup fills by itself beside hers (no input, no hint); warms him only.</summary>
        private static GameObject BuildAutoFillHisMug()
        {
            var root = new GameObject(AutoFillHisMug);
            var step = root.AddComponent<AutoFillHisMugStep>();
            Wire(step, "hintText", "");
            WireEnum(step, "warmthTarget", WarmthTarget.Him);
            Wire(step, "blackCoffee", UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/Drinks/drink_black.png"));
            return root;
        }

        /// <summary>All step prefabs by name (missing ones are created; existing ones are kept as they are).</summary>
        public static Dictionary<string, MinigameStep> EnsureAll() => BuildAll(LevelDataBuilder.EnsureMiraUsual(), false);

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
