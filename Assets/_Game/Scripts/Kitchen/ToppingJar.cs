using Courtyard.Core;
using Courtyard.Minigames;
using UnityEngine;

namespace Courtyard.Kitchen
{
    /// <summary>A clickable topping jar on the counter.</summary>
    [RequireComponent(typeof(PointerTarget))]
    public sealed class ToppingJar : MonoBehaviour
    {
        [SerializeField] private Topping topping;
        [SerializeField] private PointerTarget target;
        [Tooltip("Lets the player carry the jar to the cup.")]
        [SerializeField] private DragTarget drag;
        [Tooltip("Shown while this topping is picked.")]
        [SerializeField] private GameObject pickedMarker;
        [Tooltip("Colour of the little sprinkle when this topping is picked.")]
        [SerializeField] private Color sprinkleColour = new(0.6f, 0.4f, 0.25f);

        public Topping Topping => topping;
        public PointerTarget Target => target;
        public DragTarget Drag => drag;
        public bool IsPicked => pickedMarker.activeSelf;
        public Color SprinkleColour => sprinkleColour;

        public void SetPicked(bool picked) => pickedMarker.SetActive(picked);
    }
}
