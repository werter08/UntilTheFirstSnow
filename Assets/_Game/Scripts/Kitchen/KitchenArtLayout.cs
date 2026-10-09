using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>
    /// Kitchen layout from the art manifest, in Figma pixels on the 1920×1080 reference canvas (origin top-left).
    /// The editor decor pass reads these instead of hard-coded positions, so moving art is editing this asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Courtyard/Kitchen Art Layout", fileName = "KitchenArtLayout")]
    public sealed class KitchenArtLayout : ScriptableObject
    {
        [Header("Counter zones (Figma x, centre)")]
        public float kettleX = 260f;
        public float grinderX = 520f;
        public float brewX = 960f;
        public float milkX = 1240f;
        public float rackX = 1450f;
        [Tooltip("Not in the manifest: topping jars sit between the milk zone and the fridge.")]
        public float toppingsX = 1485f;

        [Header("Counter heights (Figma y)")]
        public float counterTopY = 700f;
        public float counterBottomY = 880f;
        [Tooltip("Props stand on this line.")]
        public float propsBaselineY = 790f;
        [Tooltip("The mug (main focus) stands a little closer to the player.")]
        public float mugBottomY = 840f;

        [Header("Window")]
        [Tooltip("Window opening for the SpriteMask (x, y, width, height).")]
        public Rect windowOpening = new(660f, 110f, 600f, 450f);

        [Header("Fridge")]
        public float fridgeLeftX = 1640f;
        [Tooltip("Mira's notes and the polaroid go here (x, y, width, height).")]
        public Rect fridgeNotesArea = new(1720f, 400f, 160f, 320f);

        [Header("Day 30 sill mugs (x = centre, y = bottom)")]
        public Vector2 sillMugHis = new(900f, 600f);
        public Vector2 sillMugHers = new(1020f, 600f);

        [Header("Served drink")]
        [Tooltip("Drink icon pops up this many pixels above the mug.")]
        public float drinkIconAbovePx = 140f;

        public const float PixelsPerUnit = 100f;

        public static Vector2 ToWorld(Vector2 figma) => new((figma.x - 960f) / PixelsPerUnit, (540f - figma.y) / PixelsPerUnit);
        public static float ToWorldX(float figmaX) => (figmaX - 960f) / PixelsPerUnit;
        public static float ToWorldY(float figmaY) => (540f - figmaY) / PixelsPerUnit;

        public float ZoneX(CounterZone zone) => ToWorldX(zone switch
        {
            CounterZone.Kettle => kettleX,
            CounterZone.Grinder => grinderX,
            CounterZone.Brew => brewX,
            CounterZone.Milk => milkX,
            CounterZone.Rack => rackX,
            CounterZone.Toppings => toppingsX,
            _ => 960f,
        });
    }
}
