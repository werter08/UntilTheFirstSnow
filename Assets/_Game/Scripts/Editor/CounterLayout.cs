using System.Linq;
using Courtyard.Core;
using Courtyard.Kitchen;
using Courtyard.Minigames;
using UnityEngine;
using static Courtyard.Editor.BuilderKit;

namespace Courtyard.Editor
{
    /// <summary>
    /// The counter, left → right: kettle, grinder, mug (+ V60), milk jug, topping jars / serving spot.
    /// Every tool for the day stands on the counter at once; minigame gauges sit next to the tool they belong to.
    /// </summary>
    internal static class CounterLayout
    {
        public const float Top = -2.75f; // counter surface
        public const float GaugeHeight = 2.4f;
        public const float KettleX = -7f;
        public const float GrinderX = -4.3f;
        public const float MugX = -0.8f;
        public const float MilkX = 2.3f;
        public const float ServeX = 5.3f; // the counter ends at the fridge (x = 6.8)

        public static readonly Color HerMugColor = new(0.93f, 0.9f, 0.82f);
        private static readonly Color Leaf = new(0.95f, 0.55f, 0.2f);
        private static readonly Color Water = new(0.75f, 0.85f, 0.95f);

        public static CounterStation Build(Transform parent)
        {
            var root = Empty(parent, "Counter Tools", Vector2.zero);
            var station = root.gameObject.AddComponent<CounterStation>();

            // Kettle on its base, switch on the base. The kettle's origin is the spout tip, so pouring follows the pointer.
            var kettle = Empty(root, "Kettle", new Vector2(KettleX + 0.9f, -1.55f));
            var kettleBody = Box(kettle, "Body", new Vector2(-0.9f, -0.35f), new Vector2(1.4f, 1.6f), Gray(0.6f), 2, true);
            var kettleTarget = Pose(kettleBody.gameObject.AddComponent<PointerTarget>(), HandPose.Grip);
            Box(kettle, "Spout", new Vector2(-0.2f, 0f), new Vector2(0.45f, 0.14f), Gray(0.5f), 2);
            var stream = Box(kettle, "Stream", new Vector2(0f, -0.6f), new Vector2(0.08f, 1.1f), Water, 1);
            Box(root, "KettleBase", new Vector2(KettleX, -2.62f), new Vector2(1.6f, 0.26f), Gray(0.25f), 1);
            var switchLight = Box(root, "KettleSwitch", new Vector2(KettleX + 0.55f, -2.62f), new Vector2(0.4f, 0.22f), Gray(0.35f), 3, true);
            switchLight.GetComponent<BoxCollider2D>().size = new Vector2(0.7f, 0.5f); // easier to hit
            var kettleSwitch = switchLight.gameObject.AddComponent<PointerTarget>();

            // Hand grinder.
            var grinder = Empty(root, "Grinder", Vector2.zero);
            Box(grinder, "Body", new Vector2(GrinderX, -2.15f), new Vector2(1f, 1.2f), new Color(0.5f, 0.36f, 0.25f), 1);
            Box(grinder, "Hopper", new Vector2(GrinderX, -1.35f), new Vector2(0.8f, 0.4f), Gray(0.55f), 1);
            var arm = Empty(grinder, "Arm", new Vector2(GrinderX, -1.1f));
            Box(arm, "ArmBar", new Vector2(0.5f, 0f), new Vector2(1f, 0.1f), Gray(0.3f), 2);
            var knob = Box(arm, "Knob", new Vector2(1f, 0f), new Vector2(0.4f, 0.4f), new Color(0.75f, 0.3f, 0.25f), 3);
            knob.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f);
            var handle = knob.gameObject.AddComponent<DragTarget>();
            WireEnum(handle, "handPose", HandPose.Grip);

            // Mug with fill, foam, spoon markers and the V60 on top.
            var mugBody = Box(root, "Mug", new Vector2(MugX, -2.1f), new Vector2(1.3f, 1.3f), Gray(0.9f), 1, true);
            var mug = mugBody.gameObject.AddComponent<PointerTarget>();
            var fillBar = Empty(mugBody.transform, "Fill", new Vector2(0f, -0.55f));
            var fill = Box(fillBar, "Liquid", Vector2.zero, new Vector2(1.1f, 0f), new Color(0.35f, 0.2f, 0.1f), 2);
            var mugFill = fillBar.gameObject.AddComponent<SpriteBar>();
            Wire(mugFill, "fill", fill);
            Wire(mugFill, "width", 1.1f);
            Wire(mugFill, "maxHeight", 1.1f);
            var fillLine = Box(fillBar, "FillLine", new Vector2(0f, 0.9f), new Vector2(1.2f, 0.05f), Color.white, 4);
            var foam = Box(mugBody.transform, "Foam", new Vector2(0f, 0.5f), new Vector2(1.15f, 0.22f), new Color(0.98f, 0.95f, 0.88f), 3);
            var markers = new Object[3];
            for (var i = 0; i < markers.Length; i++)
                markers[i] = Box(mugBody.transform, $"SpoonMarker{i}", new Vector2(-0.3f + 0.3f * i, -0.35f), new Vector2(0.2f, 0.2f), new Color(0.35f, 0.2f, 0.1f), 3);
            var leaf = LeafPrint(mugBody.transform, "Props");
            var dripper = Box(root, "V60", new Vector2(MugX, -1.05f), new Vector2(1.5f, 0.8f), new Color(0.95f, 0.95f, 0.97f), 3);

            // Milk jug with steam wand.
            var milk = Empty(root, "MilkStation", Vector2.zero);
            Box(milk, "Wand", new Vector2(MilkX, -0.6f), new Vector2(0.12f, 1.4f), Gray(0.3f), 1);
            var jugBody = Box(milk, "Jug", new Vector2(MilkX, -2.1f), new Vector2(1.1f, 1.3f), new Color(0.8f, 0.82f, 0.85f), 2, true);
            var jug = Pose(jugBody.gameObject.AddComponent<PointerTarget>(), HandPose.Grip);
            var milkBar = Empty(jugBody.transform, "Milk", new Vector2(0f, -0.55f));
            var milkFill = Box(milkBar, "Foam", Vector2.zero, new Vector2(0.9f, 0f), new Color(0.98f, 0.95f, 0.88f), 3);
            var milkFoam = milkBar.gameObject.AddComponent<SpriteBar>();
            Wire(milkFoam, "fill", milkFill);
            Wire(milkFoam, "width", 0.9f);
            Wire(milkFoam, "maxHeight", 1.1f);

            // Topping jars.
            var shelf = Empty(root, "Toppings", Vector2.zero);
            var toppings = new[]
            {
                (Topping.Cinnamon, new Color(0.65f, 0.35f, 0.2f)),
                (Topping.Honey, new Color(0.95f, 0.7f, 0.2f)),
                (Topping.Cocoa, new Color(0.35f, 0.22f, 0.15f)),
                (Topping.Salt, new Color(0.92f, 0.92f, 0.95f)),
            };
            var jars = toppings.Select((t, i) =>
            {
                var body = Box(shelf, t.Item1.ToString(), new Vector2(ServeX - 1.1f + 0.75f * i, -2.3f), new Vector2(0.65f, 0.9f), t.Item2, 1, true);
                WorldText(body.transform, "Label", new Vector2(0f, 0.75f), t.Item1.ToString(), 1.6f, Gray(0.15f), "Props", 2);
                var picked = Box(body.transform, "Picked", new Vector2(0f, -0.58f), new Vector2(0.8f, 0.14f), Leaf, 2);
                var target = Pose(body.gameObject.AddComponent<PointerTarget>(), HandPose.Pinch);
                var jar = body.gameObject.AddComponent<ToppingJar>();
                WireEnum(jar, "topping", t.Item1);
                Wire(jar, "target", target);
                Wire(jar, "pickedMarker", picked.gameObject);
                return (Object)jar;
            }).ToArray();

            Wire(station, "kettle", kettle);
            Wire(station, "kettleTarget", kettleTarget);
            Wire(station, "kettleBody", kettleBody);
            Wire(station, "kettleSwitch", kettleSwitch);
            Wire(station, "switchLight", switchLight);
            Wire(station, "kettleStream", stream);
            Wire(station, "grinder", grinder.gameObject);
            Wire(station, "grinderArm", arm);
            Wire(station, "grinderHandle", handle);
            Wire(station, "mug", mug);
            Wire(station, "mugBody", mugBody);
            Wire(station, "mugFill", mugFill);
            Wire(station, "mugFillLine", fillLine.transform);
            Wire(station, "mugFoam", foam.gameObject);
            WireArray(station, "spoonMarkers", markers);
            Wire(station, "mugLeafPrint", leaf.gameObject);
            Wire(station, "dripper", dripper.gameObject);
            Wire(station, "milkStation", milk.gameObject);
            Wire(station, "milkJug", jug);
            Wire(station, "milkJugBody", jugBody);
            Wire(station, "milkFoam", milkFoam);
            Wire(station, "toppingShelf", shelf.gameObject);
            WireArray(station, "toppingJars", jars);
            return station;
        }

        private static PointerTarget Pose(PointerTarget target, HandPose pose)
        {
            WireEnum(target, "handPose", pose);
            return target;
        }

        public static SpriteRenderer LeafPrint(Transform mug, string layer)
        {
            var leaf = Box(mug, "LeafPrint", new Vector2(0.15f, 0.1f), new Vector2(0.3f, 0.3f), Leaf, 4, false, layer);
            leaf.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return leaf;
        }
    }
}
